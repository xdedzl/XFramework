#if UNITY_EDITOR
using System;
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
        private Button m_ClearProviderButton;
        private Label m_SourceLabel;
        private Label m_StatusLabel;
        private XAnimationGraphDebugView m_GraphView;
        private Func<XAnimationDebugGraphSnapshot> m_ExternalSnapshotProvider;
        private string m_ExternalSourceName;
        private double m_LastRefreshTime;

        [SerializeField] private XAnimationActor m_TargetActor;
        [SerializeField] private bool m_FollowSelection = true;
        [SerializeField] private bool m_AutoRefresh = true;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XAnimationGraphDebuggerWindow window = GetWindow<XAnimationGraphDebuggerWindow>("XAnimation Graph");
            window.minSize = new Vector2(720f, 360f);
            window.ClearExternalProvider();
            window.Show();
        }

        public static void ShowPreview(Func<XAnimationDebugGraphSnapshot> snapshotProvider, string sourceName)
        {
            XAnimationGraphDebuggerWindow window = GetWindow<XAnimationGraphDebuggerWindow>("XAnimation Graph");
            window.minSize = new Vector2(920f, 520f);
            window.SetExternalProvider(snapshotProvider, string.IsNullOrWhiteSpace(sourceName) ? "Preview" : sourceName);
            window.Show();
            window.Focus();
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

            m_ClearProviderButton = new Button(ClearExternalProvider)
            {
                text = "Actor Mode"
            };
            m_ClearProviderButton.tooltip = "切回场景 Actor 数据源。";
            m_ClearProviderButton.style.marginLeft = 6;
            toolbar.Add(m_ClearProviderButton);

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

            m_SourceLabel = new();
            m_SourceLabel.style.marginLeft = 8;
            m_SourceLabel.style.color = new Color(0.72f, 0.72f, 0.74f, 1f);
            toolbar.Add(m_SourceLabel);

            m_StatusLabel = new();
            m_StatusLabel.style.marginLeft = 8;
            m_StatusLabel.style.flexGrow = 1;
            m_StatusLabel.style.color = new Color(0.60f, 0.60f, 0.62f, 1f);
            m_StatusLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            toolbar.Add(m_StatusLabel);

            return toolbar;
        }

        private void SetExternalProvider(Func<XAnimationDebugGraphSnapshot> snapshotProvider, string sourceName)
        {
            m_ExternalSnapshotProvider = snapshotProvider;
            m_ExternalSourceName = sourceName ?? "Preview";
            m_FollowSelection = false;
            if (m_FollowSelectionToggle != null)
            {
                m_FollowSelectionToggle.SetValueWithoutNotify(false);
            }

            RefreshSourceControls();
            RefreshView();
        }

        private void ClearExternalProvider()
        {
            m_ExternalSnapshotProvider = null;
            m_ExternalSourceName = null;
            RefreshSourceControls();
            RefreshFromSelectionIfNeeded();
            RefreshView();
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
            if (m_ExternalSnapshotProvider != null || !m_FollowSelection)
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
            if (m_ExternalSnapshotProvider != null || !m_FollowSelection)
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
            RefreshSourceControls();
            m_GraphView?.Refresh();
            if (m_StatusLabel != null)
            {
                if (m_ExternalSnapshotProvider != null)
                {
                    m_StatusLabel.text = $"Preview: {m_ExternalSourceName}";
                }
                else
                {
                    m_StatusLabel.text = m_TargetActor == null
                        ? "未选择 XAnimationActor"
                        : m_TargetActor.IsInitialized
                            ? "Ready"
                            : "Actor 未初始化";
                }
            }
        }

        private void RefreshSourceControls()
        {
            bool externalMode = m_ExternalSnapshotProvider != null;
            if (m_FollowSelectionToggle != null)
            {
                m_FollowSelectionToggle.SetEnabled(!externalMode);
            }

            if (m_ActorField != null)
            {
                m_ActorField.SetEnabled(!externalMode);
            }

            if (m_ClearProviderButton != null)
            {
                m_ClearProviderButton.style.display = externalMode ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (m_SourceLabel != null)
            {
                m_SourceLabel.text = externalMode
                    ? $"Source: Preview | {m_ExternalSourceName}"
                    : "Source: Scene Actor";
            }
        }

        private XAnimationDebugGraphSnapshot GetSnapshot()
        {
            if (m_ExternalSnapshotProvider != null)
            {
                try
                {
                    return m_ExternalSnapshotProvider() ??
                           XAnimationDebugGraphSnapshot.Invalid("Preview graph provider returned null.");
                }
                catch (Exception ex)
                {
                    return XAnimationDebugGraphSnapshot.Invalid($"Preview graph provider failed: {ex.Message}");
                }
            }

            if (m_TargetActor == null)
            {
                return XAnimationDebugGraphSnapshot.Invalid("未选择 XAnimationActor。选中场景对象，或把 Actor 拖到窗口顶部。");
            }

            return m_TargetActor.GetDebugGraphSnapshot();
        }
    }
}
#endif
