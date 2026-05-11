#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;

namespace XFramework.Editor
{
    public sealed class XAnimationGraphDebuggerWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/XAnimation Graph Debugger";
        private const double AutoRefreshIntervalSeconds = 0.2d;

        private ObjectField m_ActorField;
        private Toggle m_FollowSelectionToggle;
        private Toggle m_AutoRefreshToggle;
        private Label m_StatusLabel;
        private XAnimationGraphDebugView m_GraphView;
        private double m_LastRefreshTime;

        [SerializeField] private XAnimationActor m_TargetActor;
        [SerializeField] private bool m_FollowSelection = true;
        [SerializeField] private bool m_AutoRefresh = true;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XAnimationGraphDebuggerWindow window = GetWindow<XAnimationGraphDebuggerWindow>("XAnimation Graph");
            window.minSize = new Vector2(720f, 360f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            rootVisualElement.Add(BuildToolbar());
            m_GraphView = new XAnimationGraphDebugView(GetSnapshot);
            rootVisualElement.Add(m_GraphView);

            RefreshFromSelectionIfNeeded();
            RefreshView();
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 4;
            toolbar.style.paddingRight = 4;
            toolbar.style.paddingTop = 3;
            toolbar.style.paddingBottom = 3;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.28f, 0.28f, 0.30f, 1f);

            m_FollowSelectionToggle = new Toggle("Follow Selection") { value = m_FollowSelection };
            m_FollowSelectionToggle.RegisterValueChangedCallback(evt =>
            {
                m_FollowSelection = evt.newValue;
                RefreshFromSelectionIfNeeded();
                RefreshView();
            });
            toolbar.Add(m_FollowSelectionToggle);

            m_ActorField = new ObjectField
            {
                objectType = typeof(XAnimationActor),
                allowSceneObjects = true,
                value = m_TargetActor,
            };
            m_ActorField.style.width = 260;
            m_ActorField.style.marginLeft = 8;
            m_ActorField.RegisterValueChangedCallback(evt =>
            {
                m_TargetActor = evt.newValue as XAnimationActor;
                if (m_TargetActor != null)
                {
                    m_FollowSelection = false;
                    m_FollowSelectionToggle.SetValueWithoutNotify(false);
                }
                RefreshView();
            });
            toolbar.Add(m_ActorField);

            m_AutoRefreshToggle = new Toggle("Auto Refresh") { value = m_AutoRefresh };
            m_AutoRefreshToggle.style.marginLeft = 8;
            m_AutoRefreshToggle.RegisterValueChangedCallback(evt => m_AutoRefresh = evt.newValue);
            toolbar.Add(m_AutoRefreshToggle);

            Button refreshButton = new(RefreshView)
            {
                text = "Refresh Now"
            };
            refreshButton.style.marginLeft = 8;
            toolbar.Add(refreshButton);

            m_StatusLabel = new();
            m_StatusLabel.style.marginLeft = 8;
            m_StatusLabel.style.flexGrow = 1;
            m_StatusLabel.style.color = new Color(0.60f, 0.60f, 0.62f, 1f);
            m_StatusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            toolbar.Add(m_StatusLabel);

            return toolbar;
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnSelectionChanged()
        {
            if (!m_FollowSelection)
            {
                return;
            }

            RefreshFromSelectionIfNeeded();
            RefreshView();
        }

        private void OnEditorUpdate()
        {
            if (!m_AutoRefresh || m_GraphView == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - m_LastRefreshTime < AutoRefreshIntervalSeconds)
            {
                return;
            }

            RefreshFromSelectionIfNeeded();
            RefreshView();
        }

        private void RefreshFromSelectionIfNeeded()
        {
            if (!m_FollowSelection)
            {
                return;
            }

            XAnimationActor selectedActor = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<XAnimationActor>()
                : null;
            if (selectedActor == m_TargetActor)
            {
                return;
            }

            m_TargetActor = selectedActor;
            m_ActorField?.SetValueWithoutNotify(m_TargetActor);
        }

        private void RefreshView()
        {
            m_LastRefreshTime = EditorApplication.timeSinceStartup;
            m_GraphView?.Refresh();
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = m_TargetActor == null
                    ? "未选择 XAnimationActor"
                    : m_TargetActor.IsInitialized
                        ? "Ready"
                        : "Actor 未初始化";
            }
        }

        private XAnimationDebugGraphSnapshot GetSnapshot()
        {
            if (m_TargetActor == null)
            {
                return XAnimationDebugGraphSnapshot.Invalid("未选择 XAnimationActor。选中场景对象，或把 Actor 拖到窗口顶部。");
            }

            return m_TargetActor.GetDebugGraphSnapshot();
        }
    }
}
#endif
