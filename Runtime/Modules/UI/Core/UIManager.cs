using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Entity;
using XFramework.Resource;

namespace XFramework.UI
{
#if UNITY_EDITOR
    public readonly struct UIPanelTagDebugSnapshot
    {
        public UIPanelTagDebugSnapshot(string tag, int activePanelCount)
        {
            Tag = tag;
            ActivePanelCount = activePanelCount;
        }

        public string Tag { get; }
        public int ActivePanelCount { get; }
        public bool IsActive => ActivePanelCount > 0;
    }

    public readonly struct UIPanelDebugSnapshot
    {
        public UIPanelDebugSnapshot(
            string panelName,
            Type panelType,
            string path,
            int level,
            bool isCached,
            bool isOpened,
            bool isVisible,
            bool hasCloseCallback,
            GameObject gameObject,
            string hierarchyPath,
            bool isUIToolkitPanel,
            IReadOnlyList<UIPanelTagDebugSnapshot> tags)
        {
            PanelName = panelName;
            PanelType = panelType;
            Path = path;
            Level = level;
            IsCached = isCached;
            IsOpened = isOpened;
            IsVisible = isVisible;
            HasCloseCallback = hasCloseCallback;
            GameObject = gameObject;
            HierarchyPath = hierarchyPath;
            IsUIToolkitPanel = isUIToolkitPanel;
            Tags = tags;
        }

        public string PanelName { get; }
        public Type PanelType { get; }
        public string Path { get; }
        public int Level { get; }
        public bool IsCached { get; }
        public bool IsOpened { get; }
        public bool IsVisible { get; }
        public bool HasCloseCallback { get; }
        public GameObject GameObject { get; }
        public string HierarchyPath { get; }
        public bool IsUIToolkitPanel { get; }
        public IReadOnlyList<UIPanelTagDebugSnapshot> Tags { get; }
    }
#endif

    /// <summary>
    /// 主界面（所有主界面会放到一个栈中（用于back等操作），主界面不能同时存在多个）
    /// </summary>
    public class MainPanelAttribute : Attribute { }
    
    /// <summary>
    /// 定义面板的名称和路径
    /// </summary>
    public class PanelInfoAttribute : Attribute
    {
        public string name;
        public string path;
        public int level;
        
        public PanelInfoAttribute(string name, string path, int level = 0)
        {
            this.name = name;
            this.path = path;
            this.level = level;
        }
    }
    
    [DependenceModule(typeof(ResourceManager))]
    [ModuleLifecycle(ModuleLifecycle.RuntimePersistent)]
    public class UIManager : MonoGameModuleBase<UIManager>
    {
        private RectTransform canvasTransform;
        private RectTransform CanvasTransform
        {
            get
            {
                if (canvasTransform == null)
                {
                    var canvas = GameObject.FindFirstObjectByType<Canvas>();
                    if (canvas == null)
                    {
                        throw new XFrameworkException("[UI] Canvas is not exist in the scene, please add a Canvas to the scene or open a panel with a valid path to let UIManager create a Canvas for you");
                        // canvas = new GameObject("Canvas").AddComponent<RectTransform>();
                    }
                    canvasTransform = canvas.GetComponent<RectTransform>();
                }
                return canvasTransform;
            }
        }
        
        private readonly Dictionary<string, PanelInfoAttribute> m_PanelName2Info = new ();
        private readonly Dictionary<string, Type> m_PanelName2Type = new ();
        private readonly Dictionary<Type, string> m_PanelType2Name = new ();
        private readonly Dictionary<Type, string[]> m_PanelType2Tags = new ();
        
        private readonly Dictionary<string, PanelBase> m_PanelDict = new ();
        private readonly Dictionary<int, List<PanelBase>> m_OnDisplayPanelDic = new ();
        private readonly Dictionary<string, Action> m_PanelCloseCallbacks = new ();
        private readonly Dictionary<string, HashSet<PanelBase>> m_Tag2Panels = new (StringComparer.Ordinal);
        private readonly Dictionary<string, IPanelTagHandler> m_Tag2Handler = new (StringComparer.Ordinal);
        private readonly List<PanelBase> m_UpdatePanelSnapshot = new ();
        
        private GameObject m_TipsPrefab;


        public UIManager()
        {
            InitPathDic();
            InitTagHandlers();
            ValidatePanelTagHandlers();
        }

        public void OpenPanel<TPanel>() where TPanel : PanelBase
        {
            OpenPanelInternal(GetPanelName<TPanel>(), null);
        }

        public void OpenPanel<TPanel>(Action onClose) where TPanel : PanelBase
        {
            OpenPanelInternal(GetPanelName<TPanel>(), onClose);
        }

        public void OpenPanel<TPanel>(IPanelOpenRequest request) where TPanel : PanelBase
        {
            OpenPanelInternal(GetPanelName<TPanel>(), null, request);
        }

        public void OpenPanel<TPanel>(IPanelOpenRequest request, Action onClose) where TPanel : PanelBase
        {
            OpenPanelInternal(GetPanelName<TPanel>(), onClose, request);
        }
        
        public void OpenPanel(string uiName)
        {
            OpenPanelInternal(uiName, null);
        }

        public void OpenPanel(string uiName, Action onClose)
        {
            OpenPanelInternal(uiName, onClose);
        }

        public void OpenPanel<TRequest>(string uiName, in TRequest request) where TRequest : struct
        {
            OpenPanelInternal(uiName, null, in request);
        }

        public void OpenPanel<TRequest>(string uiName, in TRequest request, Action onClose) where TRequest : struct
        {
            OpenPanelInternal(uiName, onClose, in request);
        }

        private void OpenPanelInternal(string uiName, Action onClose)
        {
            if (!TryPrepareOpenPanel(uiName, onClose, out PanelBase panel))
            {
                return;
            }

            panel.OnBeforeOpen();
            panel.OnBeforeOpenSubPanels();
            panel.OnOpenedSubPanels();
            panel.OnOpened();
        }

        private void OpenPanelInternal(string uiName, Action onClose, IPanelOpenRequest request)
        {
            ValidatePanelOpenRequest(uiName, request?.GetType());

            if (!TryPrepareOpenPanel(uiName, onClose, out PanelBase panel))
            {
                return;
            }

            if (panel is not PanelBaseWithRequest requestPanel)
            {
                throw new XFrameworkException(
                    $"[UI] Invalid open request for {panel.GetType().Name}. Expected panel derived from PanelBase<TRequest>.");
            }

            requestPanel.OpenRequestObject(request);
            panel.OnBeforeOpenSubPanels();
            panel.OnOpenedSubPanels();
            panel.OnOpened();
        }

        private void OpenPanelInternal<TRequest>(string uiName, Action onClose, in TRequest request)
            where TRequest : struct
        {
            ValidatePanelOpenRequest(uiName, typeof(TRequest));

            if (!TryPrepareOpenPanel(uiName, onClose, out PanelBase panel))
            {
                return;
            }

            if (panel is not PanelBase<TRequest> typedPanel)
            {
                throw new XFrameworkException(
                    $"[UI] Invalid open request for {panel.GetType().Name}. Expected: {typeof(TRequest).Name}.");
            }

            typedPanel.OpenRequest(in request);
            panel.OnBeforeOpenSubPanels();
            panel.OnOpenedSubPanels();
            panel.OnOpened();
        }

        private bool TryPrepareOpenPanel(string uiName, Action onClose, out PanelBase panel)
        {
            panel = GetPanel(uiName);
            if (panel == null)
            {
                return false;
            }

            if (m_OnDisplayPanelDic.TryGetValue(panel.Level, out var value))
            {
                if (value.Contains(panel))
                {
                    return false;
                }
            }
            else
            {
                m_OnDisplayPanelDic.Add(panel.Level, new List<PanelBase>());
            }
            m_OnDisplayPanelDic[panel.Level].Add(panel);

            if (onClose != null)
            {
                m_PanelCloseCallbacks[uiName] = onClose;
            }

            // if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1))
            // {
            //     m_OnDisplayPanelDic[panel.Level - 1].End().OnPause();
            // }
            
            panel.SetVisible(true);
            panel.transform.SetAsLastSibling();

            AcquirePanelTags(panel);
            return true;
        }

        private void ValidatePanelOpenRequest(string uiName, Type requestType)
        {
            if (requestType == null)
            {
                throw new XFrameworkException(
                    $"[UI] Invalid open request. Panel: {uiName}, Request is null.");
            }

            Type expectedRequestType = GetPanelRequestType(uiName, out Type panelType);
            if (expectedRequestType == null)
            {
                throw new XFrameworkException(
                    $"[UI] Invalid open request for {panelType.Name}. Expected panel derived from PanelBase<TRequest>.");
            }

            if (expectedRequestType != requestType)
            {
                throw new XFrameworkException(
                    $"[UI] Invalid open request for {panelType.Name}. Expected: {expectedRequestType.Name}, Actual: {requestType.Name}.");
            }
        }

        private Type GetPanelRequestType(string uiName, out Type panelType)
        {
            if (!m_PanelName2Type.TryGetValue(uiName, out panelType))
            {
                throw new XFrameworkException($"[UI] The panel info you want is not exist, panel name: {uiName}");
            }

            for (Type type = panelType; type != null && type != typeof(PanelBase); type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PanelBase<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            return null;
        }
        
        public void ClosePanel(string uiName)
        {
            if (!TryGetCachedPanel(uiName, out PanelBase panel))
            {
                return;
            }

            if (!m_OnDisplayPanelDic.TryGetValue(panel.Level, out List<PanelBase> panels) || !panels.Contains(panel))
            {
                return;
            }

            panel.OnBeforeClose();
            panel.OnBeforeCloseSubPanels();

            // todo 应该放到OnBeforeClose的时候
            panel.SetVisible(false);

            panels.Remove(panel);
            if (panels.Count == 0)
            {
                m_OnDisplayPanelDic.Remove(panel.Level);
            }

            try
            {
                panel.OnClosedSubPanels();
                panel.OnClosed();
            }
            finally
            {
                ReleasePanelTags(panel);
            }

            InvokePanelCloseCallback(uiName);

            // int index = panel.Level + 1;
            // while (m_OnDisplayPanelDic.ContainsKey(index))
            // {
            //     var temp = m_OnDisplayPanelDic[index];
            //     if (temp.Count > 0)
            //     {
            //         temp.End().OnBeforeClose();
            //         temp.End().OnBeforeCloseSubPanels();
            //
            //         temp.RemoveAt(temp.Count - 1);
            //     }
            //     else
            //     {
            //         break;
            //     }
            // }
            // if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1))
            // {
            //     m_OnDisplayPanelDic[panel.Level - 1].End().OnResume();
            // }
        }

        private void InvokePanelCloseCallback(string uiName)
        {
            if (!m_PanelCloseCallbacks.Remove(uiName, out Action callback))
            {
                return;
            }

            callback?.Invoke();
        }
        
        /// <summary>
        /// 检查面板是否正在显示
        /// </summary>
        public bool IsPanelOpened(string uiName)
        {
            foreach (var list in m_OnDisplayPanelDic.Values)
            {
                foreach (var panel in list)
                {
                    if (panel.PanelName == uiName) return true;
                }
            }
            return false;
        }

        private bool IsPanelOpened(PanelBase panel)
        {
            return panel != null &&
                   m_OnDisplayPanelDic.TryGetValue(panel.Level, out List<PanelBase> panels) &&
                   panels.Contains(panel);
        }

        /// <summary>
        /// 检查标签当前是否被至少一个已打开面板持有。
        /// </summary>
        public bool IsTagActive(string tag)
        {
            return GetTagActivePanelCount(tag) > 0;
        }

        /// <summary>
        /// 获取当前持有标签的已打开面板数量。
        /// </summary>
        public int GetTagActivePanelCount(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return 0;
            }

            return m_Tag2Panels.TryGetValue(tag, out HashSet<PanelBase> panels)
                ? panels.Count
                : 0;
        }

#if UNITY_EDITOR
        public List<UIPanelDebugSnapshot> GetDebugPanelSnapshots()
        {
            var openedPanels = new HashSet<PanelBase>();
            foreach (var list in m_OnDisplayPanelDic.Values)
            {
                foreach (var panel in list)
                {
                    if (panel != null)
                    {
                        openedPanels.Add(panel);
                    }
                }
            }

            var snapshots = new List<UIPanelDebugSnapshot>(m_PanelName2Info.Count);
            foreach (var pair in m_PanelName2Info)
            {
                string panelName = pair.Key;
                PanelInfoAttribute panelInfo = pair.Value;
                m_PanelName2Type.TryGetValue(panelName, out Type panelType);

                bool isCached = m_PanelDict.TryGetValue(panelName, out PanelBase panel);
                GameObject panelGameObject = panel != null ? panel.gameObject : null;
                string[] panelTags = panelType != null ? m_PanelType2Tags[panelType] : Array.Empty<string>();
                var tagSnapshots = new List<UIPanelTagDebugSnapshot>(panelTags.Length);
                foreach (string tag in panelTags)
                {
                    tagSnapshots.Add(new UIPanelTagDebugSnapshot(tag, GetTagActivePanelCount(tag)));
                }

                snapshots.Add(new UIPanelDebugSnapshot(
                    panelName,
                    panelType,
                    panelInfo.path ?? string.Empty,
                    panelInfo.level,
                    isCached,
                    panel != null && openedPanels.Contains(panel),
                    panel != null && panel.IsVisible,
                    m_PanelCloseCallbacks.ContainsKey(panelName),
                    panelGameObject,
                    panelGameObject != null ? GetHierarchyPath(panelGameObject.transform) : string.Empty,
                    panelType != null && typeof(UIToolkitPanelBase).IsAssignableFrom(panelType),
                    tagSnapshots));
            }

            return snapshots;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
#endif

        private PanelBase GetPanel(string uiName)
        {
            if (m_PanelDict.TryGetValue(uiName, out PanelBase _panel))
            {
                if (_panel == null)
                    throw new XFrameworkException("[UI] The panel you want has been unloaded");
                return _panel;
            }

            m_PanelName2Info.TryGetValue(uiName, out var panelInfo);
            if (panelInfo == null)
            {
                throw new XFrameworkException($"[UI] The panel info you want is not exist, panel name: {uiName}");
            }
            Type panelType = m_PanelName2Type[uiName];
            GameObject panelGo = DefaultPanelLoader(uiName, panelInfo.path, panelType);
            var panel = AddPanel(uiName, panelGo);
            return panel;
        }

        private bool TryGetCachedPanel(string uiName, out PanelBase panel)
        {
            if (!m_PanelName2Info.ContainsKey(uiName))
            {
                throw new XFrameworkException($"[UI] The panel info you want is not exist, panel name: {uiName}");
            }

            if (!m_PanelDict.TryGetValue(uiName, out panel))
            {
                return false;
            }

            if (panel == null)
            {
                throw new XFrameworkException("[UI] The panel you want has been unloaded");
            }

            return true;
        }
        
        public T GetPanel<T>() where T : PanelBase
        {
            string uiName = GetPanelName<T>();
            return GetPanel(uiName) as T;
        }

        private string GetPanelName<TPanel>() where TPanel : PanelBase
        {
            Type panelType = typeof(TPanel);
            if (!m_PanelType2Name.TryGetValue(panelType, out string uiName))
            {
                throw new XFrameworkException($"[UI] The panel type is not registered, type: {panelType.FullName}");
            }

            return uiName;
        }

        private PanelBase AddPanel(string uiName, GameObject panelGo)
        {
            if (m_PanelDict.ContainsKey(uiName))
            {
                throw new XFrameworkException("[UI] The panel you want add is already exist");
            }
            
            var panel = panelGo.GetComponent<PanelBase>();
            var panelType = m_PanelName2Type[uiName];
            EnsureRuntimeUIToolkitPanel(uiName, panelGo, panelType);
            if (panel == null)
            {
                panel = (PanelBase)panelGo.AddComponent(panelType);
            }
            else
            {
                if (panel.GetType() != panelType)
                {
                    throw new XFrameworkException($"[UI] The panel type in {m_PanelName2Info[uiName].path} is wrong");
                }
            }

            panel.Init();
            m_PanelDict.Add(uiName, panel);

            Transform uiGroup = CanvasTransform.Find("Level" + panel.Level);
            if (uiGroup == null)
            {
                var rect = (new GameObject("Level" + panel.Level)).AddComponent<RectTransform>();

                int siblingIndex = CanvasTransform.childCount;
                for (int i = 0, length = CanvasTransform.childCount; i < length; i++)
                {
                    string levelName = CanvasTransform.GetChild(i).name;
                    if (TryParseLevelGroupName(levelName, out int level))
                    {
                        if (panel.Level < level)
                        {
                            siblingIndex = i;
                            break;
                        }
                    }
                }
                rect.SetParent(CanvasTransform);
                rect.SetSiblingIndex(siblingIndex);
                rect.sizeDelta = CanvasTransform.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.anchoredPosition3D = Vector3.zero;
                rect.sizeDelta = Vector2.zero;
                rect.localScale = Vector3.one;
                uiGroup = rect;
            }
            panel.transform.SetParent(uiGroup, false);
            return panel;
        }

        private static bool TryParseLevelGroupName(string levelName, out int level)
        {
            const string prefix = "Level";
            level = 0;

            if (string.IsNullOrEmpty(levelName) || !levelName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(levelName.Substring(prefix.Length), out level);
        }

        private void EnsureRuntimeUIToolkitPanel(string uiName, GameObject panelGo, Type panelType)
        {
            var panelInfo = m_PanelName2Info[uiName];
            if (!string.IsNullOrEmpty(panelInfo.path) || !typeof(UIToolkitPanelBase).IsAssignableFrom(panelType))
            {
                return;
            }

            if (!panelGo.TryGetComponent<UIDocument>(out var uiDocument))
            {
                uiDocument = panelGo.AddComponent<UIDocument>();
            }

            if (uiDocument.panelSettings ==null)
            {
                if (XApplication.Setting.defaultUIToolkitPanelSettings == null)
                {
                    throw new XFrameworkException($"[UI] The defaultUIToolkitPanelSettings is not set in XFrameworkSetting, please set it or assign a PanelSettings to {uiName} panel");
                }
                
                uiDocument.panelSettings = XApplication.Setting.defaultUIToolkitPanelSettings;
            }
        }

        /// <summary>
        /// 关闭某个层级的所有面板
        /// </summary>
        public void CloseLevelPanel(int level)
        {
            if (m_OnDisplayPanelDic.TryGetValue(level, out List<PanelBase> panels))
            {
                var panelNames = new List<string>(panels.Count);
                foreach (var item in panels)
                {
                    panelNames.Add(item.PanelName);
                }

                foreach (var panelName in panelNames)
                {
                    ClosePanel(panelName);
                }

                if (panels.Count == 0)
                {
                    m_OnDisplayPanelDic.Remove(level);
                }
            }
        }

        /// <summary>
        /// 初始化面板路径
        /// </summary>
        private void InitPathDic()
        {
            var path2TypeDict = new Dictionary<string, Type>();
            foreach (var type in Utility.Reflection.GetSonTypes<PanelBase>())
            {
                var panelInfo = type.GetCustomAttribute<PanelInfoAttribute>();

                if (panelInfo == null)
                {
                    throw new XFrameworkException($"[{type.Name}] PanelInfoAttribute is missing");
                }
                
                bool hasPanelPath = !string.IsNullOrEmpty(panelInfo.path);
                if (hasPanelPath && path2TypeDict.TryGetValue(panelInfo.path, out Type _type))
                {
                    throw new XFrameworkException($"[UI] The panel path is repeat, type1:{_type}, type2: {type}, path: {panelInfo.path}");
                }
                
                if (m_PanelName2Type.TryGetValue(panelInfo.name, out Type __type))
                {
                    throw new XFrameworkException($"[UI] The panel name is repeat, type1:{__type}, type2: {type}, path: {panelInfo.name}");
                }
                
                m_PanelName2Info.Add(panelInfo.name, panelInfo);
                m_PanelName2Type.Add(panelInfo.name, type);
                m_PanelType2Name.Add(type, panelInfo.name);
                m_PanelType2Tags.Add(type, GetDeclaredTags(type, type.Name));
                if (hasPanelPath)
                {
                    path2TypeDict.Add(panelInfo.path, type);
                }
            }
        }

        private void InitTagHandlers()
        {
            var handlerTypes = Utility.Reflection
                .GetAssignableTypes(typeof(IPanelTagHandler), "Assembly-CSharp")
                .OrderBy(type => type.FullName, StringComparer.Ordinal);

            foreach (Type handlerType in handlerTypes)
            {
                IPanelTagHandler handler;
                try
                {
                    handler = (IPanelTagHandler)Activator.CreateInstance(handlerType);
                }
                catch (Exception exception)
                {
                    throw new XFrameworkException(
                        $"[UI] Failed to create panel tag handler {handlerType.FullName}. " +
                        $"A public parameterless constructor is required. {exception.Message}");
                }

                string tag = NormalizeTag(handler.Tag, handlerType.Name);
                if (m_Tag2Handler.TryGetValue(tag, out IPanelTagHandler existingHandler))
                {
                    throw new XFrameworkException(
                        $"[UI] Panel tag {tag} has multiple handlers: " +
                        $"{existingHandler.GetType().FullName}, {handlerType.FullName}.");
                }

                m_Tag2Handler.Add(tag, handler);
            }
        }

        private void ValidatePanelTagHandlers()
        {
            foreach (string tag in m_PanelType2Tags.Values.SelectMany(tags => tags).Distinct(StringComparer.Ordinal))
            {
                if (!m_Tag2Handler.ContainsKey(tag))
                {
                    throw new XFrameworkException($"[UI] Panel tag {tag} does not have a handler.");
                }
            }
        }

        private static string[] GetDeclaredTags(Type panelType, string ownerName)
        {
            PanelTagAttribute attribute = panelType.GetCustomAttribute<PanelTagAttribute>();
            return attribute == null
                ? Array.Empty<string>()
                : NormalizeTags(attribute.Tags, ownerName);
        }

        private static string[] NormalizeTags(IEnumerable<string> tags, string ownerName)
        {
            var result = new List<string>();
            var uniqueTags = new HashSet<string>(StringComparer.Ordinal);
            foreach (string tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    throw new XFrameworkException($"[UI] Panel tag declared by {ownerName} cannot be null or whitespace.");
                }

                if (uniqueTags.Add(tag))
                {
                    result.Add(tag);
                }
            }

            return result.ToArray();
        }

        private static string NormalizeTag(string tag, string ownerName)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new XFrameworkException($"[UI] Panel tag handler {ownerName} cannot declare a null or whitespace tag.");
            }

            return tag;
        }

        private void AcquirePanelTags(PanelBase panel)
        {
            foreach (string tag in m_PanelType2Tags[panel.GetType()])
            {
                if (!m_Tag2Panels.TryGetValue(tag, out HashSet<PanelBase> panels))
                {
                    panels = new HashSet<PanelBase>();
                    m_Tag2Panels.Add(tag, panels);
                }

                bool wasInactive = panels.Count == 0;
                if (panels.Add(panel) && wasInactive)
                {
                    NotifyTagStateChanged(tag, true);
                }
            }
        }

        private void ReleasePanelTags(PanelBase panel)
        {
            foreach (string tag in m_PanelType2Tags[panel.GetType()])
            {
                if (!m_Tag2Panels.TryGetValue(tag, out HashSet<PanelBase> panels) || !panels.Remove(panel))
                {
                    continue;
                }

                if (panels.Count == 0)
                {
                    m_Tag2Panels.Remove(tag);
                    NotifyTagStateChanged(tag, false);
                }
            }
        }

        private void NotifyTagStateChanged(string tag, bool active)
        {
            if (!m_Tag2Handler.TryGetValue(tag, out IPanelTagHandler handler))
            {
                return;
            }

            try
            {
                handler.OnTagStateChanged(active);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[UI] Panel tag handler {handler.GetType().FullName} failed. Tag: {tag}, Active: {active}");
                Debug.LogException(exception);
            }
        }

        private void DeactivateAllTags()
        {
            string[] activeTags = m_Tag2Panels.Keys.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
            m_Tag2Panels.Clear();
            foreach (string tag in activeTags)
            {
                NotifyTagStateChanged(tag, false);
            }
        }

        internal void RegisterExistPanel(GameObject panelGo, Type panelType)
        {
            var uiName = m_PanelType2Name[panelType];
            if (!m_PanelDict.ContainsKey(uiName))
            {
                AddPanel(uiName, panelGo);
            }
            OpenPanel(uiName);
        }

        private GameObject DefaultPanelLoader(string uiName, string path, Type panelType)
        {
            if (string.IsNullOrEmpty(path) || IsUIToolkitAssetPath(path, panelType))
            {
                var runtimePanelGo = new GameObject(uiName, typeof(RectTransform));
                return runtimePanelGo;
            }

            var res = ResourceManager.Instance.Load<GameObject>(path);
            if(res == null)
            {
                throw new XFrameworkException($"[UI] The panel you want to load is not exist, path: {path}");
            }

            var panelGo = GameObject.Instantiate(res); 
            return panelGo;
        }

        private static bool IsUIToolkitAssetPath(string path, Type panelType)
        {
            if (!typeof(UIToolkitPanelBase).IsAssignableFrom(panelType))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".uxml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".uss", StringComparison.OrdinalIgnoreCase);
        }

        #region Tips

        public void ShowTips(string content, Vector3 position, Color color)
        {
            var point = Camera.main.WorldToScreenPoint(position).XY();
            ShowTips(content, point, color);
        }

        public void ShowTips(string content, Vector2 position, Color color)
        {
            InitTipsTemplate();
            var entity = EntityManager.Instance.Allocate<TipEntity>("ui-tip-entity");
            entity.transform.SetParent(canvasTransform);
            entity.position = position;
            entity.text = content;
            entity.color = color;
        }

        private void InitTipsTemplate()
        {
            if(!EntityManager.Instance.ContainsTemplate("ui-tip-entity"))
            {
                var root = new GameObject("ui-tip-template");
                root.transform.SetParent(canvasTransform);
                var tmp = root.AddComponent<TextMeshProUGUI>();
                var rectTransform = root.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(200, 100);

                tmp.font = XApplication.Setting.font;
                tmp.alignment = TextAlignmentOptions.Center;

                tmp.transform.position = new Vector3(0, 999999, 0);
                EntityManager.Instance.AddTemplate<TipEntity>("ui-tip-entity", root, "");
            }
        }

        #endregion

        #region 接口实现

        public override int Priority => 200;

        public override void Update()
        {
            m_UpdatePanelSnapshot.Clear();

            foreach (var panels in m_OnDisplayPanelDic.Values)
            {
                for (int i = 0, length = panels.Count; i < length; i++)
                {
                    PanelBase panel = panels[i];
                    if (panel != null)
                    {
                        m_UpdatePanelSnapshot.Add(panel);
                    }
                }
            }

            for (int i = 0, length = m_UpdatePanelSnapshot.Count; i < length; i++)
            {
                PanelBase panel = m_UpdatePanelSnapshot[i];
                if (IsPanelOpened(panel))
                {
                    panel.OnUpdate();
                }
            }

            m_UpdatePanelSnapshot.Clear();
        }

        public override void Shutdown()
        {
            DeactivateAllTags();
            m_PanelDict?.Clear();
            m_PanelName2Info.Clear();
            m_PanelName2Type.Clear();
            m_PanelType2Name.Clear();
            m_PanelType2Tags.Clear();
            m_OnDisplayPanelDic.Clear();
            m_PanelCloseCallbacks.Clear();
            m_Tag2Handler.Clear();
            EntityManager.Instance.RemoveTemplate("ui-tip-entity");
        }

        #endregion
    }
}
