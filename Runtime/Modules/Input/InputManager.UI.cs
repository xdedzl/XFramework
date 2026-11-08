using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using XFramework;
using XFramework.UI;
using Object = UnityEngine.Object;
using UISelectable = UnityEngine.UI.Selectable;

namespace XFramework
{
    public sealed partial class InputManager
    {
        private readonly List<PanelInputScope> m_PanelInputs = new();
        private readonly List<InputActionReference> m_UIReferences = new();
        private readonly List<UISelectable> m_SelectableBuffer = new();
        private InputSystemUIInputModule m_UIInputModule;
        private EventSystem m_EventSystem;
        private InputActionMap m_UIMap;
        private InputAction m_UICancel;
        private InputAction m_UIInventory;
        private InputAction m_UINavigate;
        private InputAction m_UISubmit;
        private PanelInputScope m_CurrentPanel;

        private const string UIPointAction = "Point";
        private const string UIClickAction = "Click";
        private const string UIRightClickAction = "RightClick";
        private const string UIMiddleClickAction = "MiddleClick";
        private const string UIScrollWheelAction = "ScrollWheel";
        private const string UINavigateAction = "Navigate";
        private const string UISubmitAction = "Submit";
        private const string UICancelAction = "Cancel";
        private const string UIInventoryAction = "Inventory";

        public IDisposable CapturePanelInput(PanelBase owner, Action cancel, string actionMapName = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            string resolvedActionMapName = string.IsNullOrEmpty(actionMapName) ? m_UIActionMapName : actionMapName;
            if (resolvedActionMapName != m_UIActionMapName) GetInputContext(resolvedActionMapName);
            var scope = new PanelInputScope(this, owner, cancel, resolvedActionMapName);
            m_PanelInputs.Add(scope);
            RefreshCurrentContext();
            return scope;
        }

        public bool IsCurrentPanel(PanelBase panel)
        {
            return m_CurrentPanel != null && m_CurrentPanel.Owner == panel &&
                   panel.isActiveAndEnabled && Time.frameCount != m_ContextChangedFrame;
        }

        private void InitializeUIInput()
        {
            m_UIInputModule = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (m_UIInputModule == null)
                throw new XFrameworkException("UI 根节点缺少 InputSystemUIInputModule，无法统一游戏与界面输入。");
            m_EventSystem = m_UIInputModule.GetComponent<EventSystem>();
            m_UIInputModule.enabled = false;
            m_UIMap = m_PlayerInput.actions.FindActionMap(m_UIActionMapName, true);
            m_UIInputModule.actionsAsset = m_PlayerInput.actions;
            m_UIInputModule.point = CreateUIReference(UIPointAction);
            m_UIInputModule.leftClick = CreateUIReference(UIClickAction);
            m_UIInputModule.rightClick = CreateUIReference(UIRightClickAction);
            m_UIInputModule.middleClick = CreateUIReference(UIMiddleClickAction);
            m_UIInputModule.scrollWheel = CreateUIReference(UIScrollWheelAction);
            m_UIInputModule.move = CreateUIReference(UINavigateAction);
            m_UIInputModule.submit = CreateUIReference(UISubmitAction);
            // Cancel 统一送给顶层面板，避免选中控件和多个面板重复处理。
            m_UIInputModule.cancel = null;
            m_UIInputModule.trackedDevicePosition = null;
            m_UIInputModule.trackedDeviceOrientation = null;
            m_PlayerInput.uiInputModule = m_UIInputModule;
            m_UICancel = GetAction(m_UIActionMapName, UICancelAction);
            m_UIInventory = GetAction(m_UIActionMapName, UIInventoryAction);
            m_UINavigate = GetAction(m_UIActionMapName, UINavigateAction);
            m_UISubmit = GetAction(m_UIActionMapName, UISubmitAction);
            m_UINavigate.performed += OnUINavigate;
            m_UIMap.Enable();
        }

        private InputActionReference CreateUIReference(string name)
        {
            var reference = InputActionReference.Create(m_UIMap.FindAction(name, true));
            m_UIReferences.Add(reference);
            return reference;
        }

        private void UpdateUIActionState()
        {
            bool ready = Time.frameCount != m_ContextChangedFrame;
            SetActionEnabled(m_UINavigate, ready && CurrentActionMapName == m_UIActionMapName);
            SetActionEnabled(m_UISubmit, ready && IsMenuNavigationActionMap(CurrentActionMapName));
            // 指针动作与地图/对话并行；Cancel、Inventory 由当前面板消费。
            m_UICancel.Enable();
            m_UIInventory.Enable();
        }

        private bool IsMenuNavigationActionMap(string actionMapName)
        {
            for (int i = 0; i < m_MenuNavigationActionMaps.Length; i++)
            {
                if (m_MenuNavigationActionMaps[i] == actionMapName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetActionEnabled(InputAction action, bool enabled)
        {
            if (enabled) action.Enable();
            else action.Disable();
        }

        private PanelInputScope FindTopPanel()
        {
            PanelInputScope top = null;
            foreach (PanelInputScope scope in m_PanelInputs)
            {
                if (scope.Owner == null || !scope.Owner.isActiveAndEnabled) continue;
                if (top == null || scope.Owner.Level >= top.Owner.Level) top = scope;
            }
            return top;
        }

        private void UpdatePanelInput()
        {
            m_PanelInputs.RemoveAll(scope => scope.Owner == null);
            RefreshCurrentContext();
            if (Time.frameCount == m_ContextChangedFrame) return;

            if (!m_UIInputModule.enabled) m_UIInputModule.enabled = true;
            UpdateUIActionState();
            if (m_CurrentPanel == null) return;

            EnsurePanelFocus();
            if (m_UICancel.WasPressedThisFrame() && m_CurrentPanel.Cancel != null)
            {
                Action cancel = m_CurrentPanel.Cancel;
                cancel();
                m_ContextChangedFrame = Time.frameCount;
            }
        }

        private void ChangePanelFocus(PanelInputScope next)
        {
            GameObject selected = m_EventSystem.currentSelectedGameObject;
            if (m_CurrentPanel != null && m_CurrentPanel.Owner != null && selected != null &&
                selected.transform.IsChildOf(m_CurrentPanel.Owner.transform))
                m_CurrentPanel.Selected = selected;
            m_CurrentPanel = next;
            m_EventSystem.SetSelectedGameObject(null);
        }

        private void EnsurePanelFocus()
        {
            if (!IsMenuNavigationActionMap(CurrentActionMapName)) return;
            GameObject selected = m_EventSystem.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(m_CurrentPanel.Owner.transform) &&
                selected.TryGetComponent(out UISelectable selectable) && selectable.IsActive() && selectable.IsInteractable())
            {
                if (m_CurrentPanel.Selected != selected) EnsureSelectionVisible(selected);
                m_CurrentPanel.Selected = selected;
                return;
            }

            PreparePanelNavigation();
            selected = m_CurrentPanel.Selected;
            if (selected == null || !selected.TryGetComponent(out UISelectable previous) ||
                !previous.IsActive() || !previous.IsInteractable())
                selected = m_SelectableBuffer.Count > 0 ? m_SelectableBuffer[0].gameObject : null;
            m_EventSystem.SetSelectedGameObject(selected);
            EnsureSelectionVisible(selected);
        }

        private void OnUINavigate(InputAction.CallbackContext context)
        {
            if (m_CurrentPanel == null || !IsCurrentPanel(m_CurrentPanel.Owner)) return;
            // 列表可能刚刚重建，在导航发生时更新范围，避免焦点落到背后的面板。
            PreparePanelNavigation();
            EnsurePanelFocus();
        }

        private void PreparePanelNavigation()
        {
            m_SelectableBuffer.Clear();
            m_CurrentPanel.Owner.GetComponentsInChildren(false, m_SelectableBuffer);
            // 列表跟随选中项滚动，导航不进入滚动条而困在调节滑块的状态。
            m_SelectableBuffer.RemoveAll(item => item is Scrollbar || !item.IsActive() || !item.IsInteractable());
            foreach (UISelectable item in m_SelectableBuffer)
            {
                item.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = FindNeighbour(item, Vector2.up),
                    selectOnDown = FindNeighbour(item, Vector2.down),
                    selectOnLeft = FindNeighbour(item, Vector2.left),
                    selectOnRight = FindNeighbour(item, Vector2.right)
                };
            }
        }

        private UISelectable FindNeighbour(UISelectable origin, Vector2 direction)
        {
            Vector3 center = GetSelectableCenter(origin);
            UISelectable best = null;
            float bestScore = 0f;
            foreach (UISelectable candidate in m_SelectableBuffer)
            {
                if (candidate == origin) continue;
                Vector2 offset = m_CurrentPanel.Owner.transform.InverseTransformVector(GetSelectableCenter(candidate) - center);
                float dot = Vector2.Dot(offset, direction);
                if (dot <= 0f || offset.sqrMagnitude < 0.001f) continue;
                float score = dot / offset.sqrMagnitude;
                if (score <= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        private static Vector3 GetSelectableCenter(UISelectable selectable)
        {
            var rect = (RectTransform)selectable.transform;
            return rect.TransformPoint(rect.rect.center);
        }

        private void ReleasePanelInput(PanelInputScope scope)
        {
            if (m_PanelInputs.Remove(scope)) RefreshCurrentContext();
        }

        private void ShutdownUIInput()
        {
            m_PanelInputs.Clear();
            m_CurrentPanel = null;
            m_UINavigate.performed -= OnUINavigate;
            m_UIInputModule.enabled = false;
            m_PlayerInput.uiInputModule = null;
            m_UIMap.Disable();
            foreach (InputActionReference reference in m_UIReferences) Object.Destroy(reference);
            m_UIReferences.Clear();
        }

        private sealed class PanelInputScope : IDisposable
        {
            private InputManager m_Manager;
            public PanelInputScope(InputManager manager, PanelBase owner, Action cancel, string actionMapName)
            {
                m_Manager = manager;
                Owner = owner;
                Cancel = cancel;
                ActionMapName = actionMapName;
            }
            public PanelBase Owner { get; }
            public Action Cancel { get; }
            public string ActionMapName { get; }
            public GameObject Selected { get; set; }
            public void Dispose()
            {
                InputManager manager = m_Manager;
                m_Manager = null;
                manager?.ReleasePanelInput(this);
            }
        }
    }
}
