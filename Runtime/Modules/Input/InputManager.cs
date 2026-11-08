using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using XFramework.Resource;
using XFramework.UI;
using Object = UnityEngine.Object;

namespace XFramework
{
    [ModuleLifecycle(ModuleLifecycle.RuntimePersistent)]
    [DependenceModule(typeof(ResourceManager))]
    [DependenceModule(typeof(UIManager))]
    public sealed partial class InputManager : MonoGameModuleBase<InputManager>
    {
        private readonly Dictionary<string, InputContext> m_InputContexts = new(StringComparer.Ordinal);
        private InputActionAsset m_InputActionsAsset;
        private GameObject m_GameInput;
        private PlayerInput m_PlayerInput;
        private InputContext m_CurrentContext;
        private int m_ContextChangedFrame = -1;
        private bool m_RefreshingContext;
        private bool m_RefreshRequested;
        private bool m_ShuttingDown;

        private string m_DefaultActionMapName;
        private string m_UIActionMapName;
        private string[] m_MenuNavigationActionMaps;

        public string CurrentActionMapName { get; private set; }
        public bool IsUsingGamepad => m_PlayerInput != null && m_PlayerInput.currentControlScheme == "Gamepad";

        /// <summary>
        /// 指定 ActionMap 是否为当前激活的输入上下文；上下文切换当帧视为未激活。
        /// </summary>
        public bool IsActionMapActive(string actionMapName) =>
            CurrentActionMapName == actionMapName && Time.frameCount != m_ContextChangedFrame;

        // 在面板 Update 和世界地图快捷键之前处理顶层输入与焦点。
        public override int Priority => 180;

        public override void Initialize()
        {
            XFrameworkSetting setting = XApplication.Setting;
            string inputActionsPath = setting.inputActionsPath;
            if (string.IsNullOrEmpty(inputActionsPath))
            {
                throw new XFrameworkException("XFrameworkSetting.inputActionsPath 未配置，无法初始化输入系统。");
            }

            m_DefaultActionMapName = setting.inputDefaultActionMap;
            m_UIActionMapName = setting.inputUIActionMap;
            m_MenuNavigationActionMaps = setting.inputMenuNavigationActionMaps;

            m_InputActionsAsset = ResourceManager.Instance.Load<InputActionAsset>(inputActionsPath);
            if (m_InputActionsAsset == null)
            {
                throw new XFrameworkException($"无法加载输入配置：{inputActionsPath}");
            }

            // 先配置再激活，避免 PlayerInput 在绑定 Actions 前初始化。
            m_GameInput = new GameObject("GameInput");
            m_GameInput.SetActive(false);
            Object.DontDestroyOnLoad(m_GameInput);
            m_PlayerInput = m_GameInput.AddComponent<PlayerInput>();
            // 使用运行时副本，避免设备配对和 Map 状态影响共享资源。
            m_PlayerInput.actions = Object.Instantiate(m_InputActionsAsset);
            m_PlayerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            m_PlayerInput.neverAutoSwitchControlSchemes = false;
            m_PlayerInput.defaultActionMap = m_DefaultActionMapName;
            m_GameInput.SetActive(true);

            m_PlayerInput.actions.Disable();
            DiscoverContexts();
            InitializeUIInput();
            RefreshCurrentContext();
        }

        public override void Update() => UpdatePanelInput();

        public override void Shutdown()
        {
            m_ShuttingDown = true;
            ShutdownUIInput();
            foreach (InputContext context in m_InputContexts.Values) context.Dispose();
            m_GameInput.SetActive(false);
            InputActionAsset runtimeActions = m_PlayerInput.actions;
            m_PlayerInput.actions = null;
            Object.Destroy(runtimeActions);
            Object.Destroy(m_GameInput);
            ResourceManager.Instance.Release(m_InputActionsAsset);
            m_InputActionsAsset = null;
            m_GameInput = null;
            m_PlayerInput = null;
            m_CurrentContext = null;
            CurrentActionMapName = null;
            m_InputContexts.Clear();
        }

        public InputAction GetAction(string mapName, string actionName) =>
            m_PlayerInput.actions.FindAction($"{mapName}/{actionName}", true);

        public string GetBindingDisplayString(string mapName, string actionName)
        {
            return GetAction(mapName, actionName).GetBindingDisplayString(
                InputBinding.DisplayStringOptions.DontIncludeInteractions,
                IsUsingGamepad ? "Gamepad" : "Keyboard&Mouse");
        }

        public T GetContext<T>() where T : InputContext
        {
            foreach (InputContext context in m_InputContexts.Values)
            {
                if (context is T typedContext) return typedContext;
            }

            throw new XFrameworkException($"Input context type is not registered: {typeof(T).FullName}.");
        }

        public void SetContextActive(string actionMapName, bool active)
        {
            InputContext context = GetInputContext(actionMapName);
            if (context.IsActive == active) return;
            context.IsActive = active;
            RefreshCurrentContext();
        }

        public bool IsContextActive(string actionMapName) => GetInputContext(actionMapName).IsActive;

        private void DiscoverContexts()
        {
            IEnumerable<Type> contextTypes = Utility.Reflection
                .GetAssignableTypes(typeof(InputContext), "Assembly-CSharp")
                .OrderBy(type => type.FullName, StringComparer.Ordinal);

            foreach (Type contextType in contextTypes)
            {
                InputContextDefinitionAttribute definition =
                    contextType.GetCustomAttribute<InputContextDefinitionAttribute>();
                if (definition == null) continue;
                if (contextType.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new XFrameworkException(
                        $"Input context {contextType.FullName} must have a public parameterless constructor.");
                }

                var context = (InputContext)Activator.CreateInstance(contextType);
                RegisterContext(context, definition);
            }
        }

        private void RegisterContext(InputContext context, InputContextDefinitionAttribute definition)
        {
            if (string.IsNullOrWhiteSpace(definition.ActionMapName))
                throw new XFrameworkException($"Input context {context.GetType().FullName} has an empty action map name.");
            if (definition.ActionMapName == m_UIActionMapName)
                throw new XFrameworkException("The UI action map is managed by the UI input pipeline and cannot be registered as an input context.");
            if (m_InputContexts.ContainsKey(definition.ActionMapName))
                throw new XFrameworkException($"Input context action map {definition.ActionMapName} is duplicated.");
            if (m_InputContexts.Values.Any(item => item.Priority == definition.Priority))
                throw new XFrameworkException($"Input context priority {definition.Priority} is duplicated.");

            context.Initialize(m_PlayerInput.actions, definition);
            m_InputContexts.Add(definition.ActionMapName, context);
        }

        private void RefreshCurrentContext()
        {
            if (m_ShuttingDown) return;
            if (m_RefreshingContext)
            {
                m_RefreshRequested = true;
                return;
            }
            m_RefreshingContext = true;
            try
            {
                do
                {
                    m_RefreshRequested = false;
                    PanelInputScope panel = FindTopPanel();
                    InputContext winner = null;
                    if (panel != null)
                    {
                        if (panel.ActionMapName != m_UIActionMapName)
                            winner = GetInputContext(panel.ActionMapName);
                    }
                    else
                    {
                        foreach (InputContext context in m_InputContexts.Values)
                        {
                            if (context.IsActive && (winner == null || context.Priority < winner.Priority))
                                winner = context;
                        }
                    }
                    string nextActionMapName = panel?.ActionMapName ?? winner?.ActionMapName;
                    if (CurrentActionMapName == nextActionMapName && m_CurrentPanel == panel) continue;

                    ChangePanelFocus(panel);
                    m_ContextChangedFrame = Time.frameCount;
                    // 丢弃切换上下文前 UI 模块缓存的点击/提交，下一帧再恢复。
                    m_UIInputModule.enabled = false;
                    InputContext previous = m_CurrentContext;
                    m_CurrentContext = winner;
                    CurrentActionMapName = nextActionMapName;
                    if (previous != winner)
                    {
                        previous?.Disable();
                        winner?.Enable();
                    }
                    UpdateUIActionState();
                } while (m_RefreshRequested);
            }
            finally
            {
                m_RefreshingContext = false;
            }
        }

        public InputContext GetInputContext(string actionMapName)
        {
            if (!m_InputContexts.TryGetValue(actionMapName, out InputContext context))
                throw new XFrameworkException(
                    $"Unknown input context: {actionMapName}. UI input is managed by panel scopes.");
            return context;
        }
    }
}
