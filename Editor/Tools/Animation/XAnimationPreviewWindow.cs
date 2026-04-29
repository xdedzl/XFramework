#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;
using XFramework.UI;

namespace XFramework.Editor
{
    public sealed class XAnimationPreviewWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/XAnimation Preview";
        private const string DefaultSampleAssetPath = "Assets/Animation/XAnimationSamples/XAnimationPreview_WolfLite.xasset";
        private const string DefaultPrefabPath = "Assets/ThirdParty/malbers-animations/Animal Controller/Wolf Lite/Wolf Lite.prefab";
        private const float DebugPaneInitialWidth = 360f;
        private const float DebugPaneMinWidth = 280f;
        private const float PreviewPaneMinWidth = 520f;
        private const float SectionTitleFontSize = 12f;
        private const float BodyFontSize = 11f;
        private const float InspectorMinHeight = 240f;
        private const float CueLogInitialHeight = 90f;
        private const float CueLogSectionMinHeight = 72f;
        private const float ClipIconButtonSize = 22f;
        private const float ChannelStateLabelHeight = 64f;
        private const string ClipDragDataKey = nameof(XAnimationPreviewWindow) + ".ClipKey";

        // ── Theme Colors ──
        private static readonly Color PaneBg = new(0.18f, 0.18f, 0.19f, 1f);
        private static readonly Color PaneBorder = new(0.30f, 0.30f, 0.32f, 1f);
        private static readonly Color AccentColor = new(0.30f, 0.55f, 0.95f, 1f);
        private static readonly Color DangerColor = new(0.75f, 0.25f, 0.25f, 1f);
        private static readonly Color TextMuted = new(0.60f, 0.60f, 0.62f, 1f);
        private static readonly Color TextNormal = new(0.85f, 0.85f, 0.87f, 1f);
        private static readonly Color SectionDivider = new(0.28f, 0.28f, 0.30f, 1f);
        private static readonly Color ToolbarBg = new(0.14f, 0.14f, 0.15f, 1f);
        private static readonly Color HoverBg = new(0.24f, 0.24f, 0.26f, 1f);
        private static readonly Color AltRowBg = new(0.20f, 0.20f, 0.21f, 1f);
        private static readonly Color ListGroupBg = new(0.17f, 0.18f, 0.20f, 1f);
        private static readonly Color ListRowEvenBg = new(0.16f, 0.16f, 0.17f, 1f);
        private static readonly Color ListRowOddBg = new(0.19f, 0.19f, 0.20f, 1f);
        private static readonly Color ListHeaderBg = new(0.22f, 0.23f, 0.25f, 1f);
        private static readonly Color PlayingBg = new(0.20f, 0.35f, 0.55f, 0.65f);

        private readonly Dictionary<string, Label> m_ChannelStateLabels = new(StringComparer.Ordinal);

        [SerializeField] private TextAsset m_SelectedAsset;
        [SerializeField] private GameObject m_SelectedPrefab;
        [SerializeField] private bool m_ShouldAutoReloadPreview;
        [SerializeField] private bool m_AssetsSectionExpanded = true;
        [SerializeField] private bool m_PlaybackSectionExpanded = true;
        [SerializeField] private bool m_ClipsSectionExpanded = true;
        [SerializeField] private bool m_ChannelsSectionExpanded = true;

        private TextAsset m_PendingAsset;
        private GameObject m_PendingPrefab;
        private bool m_PendingAutoLoad;

        private ObjectField m_PrefabField;
        private ObjectField m_AssetField;
        private Image m_PreviewImage;
        private Label m_StatusLabel;
        private FloatField m_PlaySpeedField;
        private Toggle m_RootMotionToggle;
        private Toggle m_GridToggle;
        private Button m_PauseButton;
        private Button m_StopAllButton;
        private Button m_AddChannelButton;
        private VisualElement m_ClipListView;
        private readonly HashSet<string> m_ExpandedClipKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ClipLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ClipRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ClipRowVisualState> m_ClipVisualStateMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_ClipButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> m_ClipChannelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ChannelLabelMap = new(StringComparer.Ordinal);
        private VisualElement m_ChannelControlsContainer;
        private ScrollView m_CueLogContainer;

        private XAnimationEditorPreviewSession m_Session;
        private double m_LastEditorTime;
        private IVisualElementScheduledItem m_DelayedSaveItem;
        private bool m_IsEditingName;
        private bool m_IsPaused;
        private bool m_IsPreviewDragging;
        private Vector2 m_LastPreviewMousePosition;
        private int m_LastCueLogCount = -1;
        private string m_PendingClipRenameKey;
        private string m_PendingChannelRenameKey;
        private readonly HashSet<KeyCode> m_PressedKeys = new();

        private sealed class ClipRowVisualState
        {
            public Color BaseColor;
            public bool Hovered;
            public bool Playing;
        }

        private sealed class FoldoutCard
        {
            public VisualElement Root;
            public VisualElement Content;
        }

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XAnimationPreviewWindow window = GetWindow<XAnimationPreviewWindow>();
            window.titleContent = new GUIContent("XAnimation Preview");
            window.minSize = new Vector2(1180f, 680f);
            window.Show();
        }

        public static XAnimationPreviewWindow ShowWindow(TextAsset animationAsset, GameObject prefab = null, bool autoLoad = true)
        {
            XAnimationPreviewWindow window = GetWindow<XAnimationPreviewWindow>();
            window.titleContent = new GUIContent("XAnimation Preview");
            window.minSize = new Vector2(1180f, 680f);
            window.SetPendingOpenRequest(animationAsset, prefab, autoLoad);
            window.Show();
            window.Focus();
            return window;
        }

        internal GameObject CurrentSelectedPrefab => m_PrefabField?.value as GameObject;

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
            m_LastEditorTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            DisposeSession();
        }

        public void CreateGUI()
        {
            BuildUI();
            ApplyDefaultSelections();
            SetStatus("拖入 prefab 和 .xasset，或直接加载默认样例。");
            ScheduleAutoReloadPreview();
            ApplyPendingOpenRequest();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 2;
            root.style.paddingRight = 2;
            root.style.paddingTop = 2;
            root.style.paddingBottom = 2;
            root.style.flexDirection = FlexDirection.Column;

            TwoPaneSplitView splitView = new(0, DebugPaneInitialWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            splitView.Add(BuildDebugPane());
            splitView.Add(BuildPreviewPane());
        }

        private static Button CreateStyledButton(string label, Action onClick, Color bgColor, float marginLeft = 0f)
        {
            Button btn = new(onClick) { text = label };
            btn.tooltip = label switch
            {
                "重载" => "重新读取 Prefab 和 XAnimation 资源并刷新预览。",
                "重置位置" => "将预览对象位置和旋转恢复到初始状态。",
                "重置视角" => "将预览相机恢复到默认视角。",
                "停止全部" => "停止所有正在播放的 channel。",
                "暂停" => "暂停或继续当前预览播放。",
                _ => label
            };
            btn.style.backgroundColor = bgColor;
            btn.style.color = Color.white;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.fontSize = BodyFontSize;
            btn.style.paddingLeft = 7;
            btn.style.paddingRight = 7;
            btn.style.paddingTop = 2;
            btn.style.paddingBottom = 2;
            if (marginLeft > 0f) btn.style.marginLeft = marginLeft;
            return btn;
        }

        private VisualElement BuildStatusRow()
        {
            VisualElement statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 4;

            VisualElement statusBar = new VisualElement();
            statusBar.style.width = 2;
            statusBar.style.height = 12;
            statusBar.style.backgroundColor = AccentColor;
            statusBar.style.borderTopLeftRadius = 2;
            statusBar.style.borderTopRightRadius = 2;
            statusBar.style.borderBottomLeftRadius = 2;
            statusBar.style.borderBottomRightRadius = 2;
            statusBar.style.marginRight = 4;
            statusRow.Add(statusBar);

            m_StatusLabel = new Label();
            m_StatusLabel.style.color = TextNormal;
            m_StatusLabel.style.fontSize = BodyFontSize;
            m_StatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            statusRow.Add(m_StatusLabel);

            return statusRow;
        }

        private VisualElement BuildPreviewPane()
        {
            VisualElement pane = CreatePane();
            pane.style.minWidth = PreviewPaneMinWidth;

            m_PreviewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            m_PreviewImage.style.flexGrow = 1;
            m_PreviewImage.style.backgroundColor = new Color(0.11f, 0.11f, 0.12f, 1f);
            m_PreviewImage.style.borderTopWidth = 1;
            m_PreviewImage.style.borderBottomWidth = 1;
            m_PreviewImage.style.borderLeftWidth = 1;
            m_PreviewImage.style.borderRightWidth = 1;
            m_PreviewImage.style.borderTopColor = PaneBorder;
            m_PreviewImage.style.borderBottomColor = PaneBorder;
            m_PreviewImage.style.borderLeftColor = PaneBorder;
            m_PreviewImage.style.borderRightColor = PaneBorder;
            m_PreviewImage.style.borderTopLeftRadius = 4;
            m_PreviewImage.style.borderTopRightRadius = 4;
            m_PreviewImage.style.borderBottomLeftRadius = 4;
            m_PreviewImage.style.borderBottomRightRadius = 4;
            RegisterPreviewEvents();
            pane.Add(m_PreviewImage);

            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.marginTop = 3;
            controls.style.alignItems = Align.Center;
            pane.Add(controls);

            controls.Add(CreateStyledButton("重置位置", ResetPreviewTransform, AccentColor));
            controls.Add(CreateStyledButton("重置视角", ResetPreviewCamera, AccentColor, 6));

            Label hint = new("右键拖拽旋转，WASD 移动，QE 升降，滚轮缩放。");
            hint.style.marginLeft = 4;
            hint.style.color = TextMuted;
            hint.style.fontSize = BodyFontSize;
            controls.Add(hint);

            m_GridToggle = new Toggle("网格") { value = true };
            m_GridToggle.tooltip = "显示或隐藏预览地面网格，只影响当前预览。";
            m_GridToggle.style.marginLeft = 12;
            m_GridToggle.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;
                m_Session.SetGridVisible(evt.newValue);
                RenderPreview();
            });
            controls.Add(m_GridToggle);

            return pane;
        }

        private VisualElement BuildDebugPane()
        {
            VisualElement pane = new VisualElement();
            pane.style.minWidth = DebugPaneMinWidth;
            pane.style.flexGrow = 1;
            pane.style.minHeight = 0;
            pane.style.flexDirection = FlexDirection.Column;
            pane.style.paddingLeft = 3;
            pane.style.paddingRight = 3;
            pane.style.paddingTop = 3;
            pane.style.paddingBottom = 3;
            pane.style.backgroundColor = PaneBg;
            pane.style.borderTopLeftRadius = 6;
            pane.style.borderTopRightRadius = 6;
            pane.style.borderBottomLeftRadius = 6;
            pane.style.borderBottomRightRadius = 6;
            pane.style.borderTopWidth = 1;
            pane.style.borderBottomWidth = 1;
            pane.style.borderLeftWidth = 1;
            pane.style.borderRightWidth = 1;
            pane.style.borderTopColor = PaneBorder;
            pane.style.borderBottomColor = PaneBorder;
            pane.style.borderLeftColor = PaneBorder;
            pane.style.borderRightColor = PaneBorder;

            // ── Card: Assets ──
            FoldoutCard assetsCard = CreateFoldoutCard("资源", m_AssetsSectionExpanded, value => m_AssetsSectionExpanded = value);

            m_PrefabField = new ObjectField("Prefab")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            m_PrefabField.tooltip = "用于预览动画的角色 Prefab，必须包含 Animator。";
            m_PrefabField.RegisterValueChangedCallback(evt =>
            {
                m_SelectedPrefab = evt.newValue as GameObject;
            });
            m_PrefabField.style.marginBottom = 4;
            assetsCard.Content.Add(m_PrefabField);

            m_AssetField = new ObjectField("XAnimation / Override Asset")
            {
                objectType = typeof(TextAsset),
                allowSceneObjects = false
            };
            m_AssetField.tooltip = "要加载和编辑的 XAnimation .xasset 或 Override Asset。";
            m_AssetField.RegisterValueChangedCallback(evt =>
            {
                m_SelectedAsset = evt.newValue as TextAsset;
            });
            m_AssetField.style.marginBottom = 4;
            assetsCard.Content.Add(m_AssetField);

            assetsCard.Content.Add(CreateStyledButton("重载", LoadPreview, AccentColor));

            // ── Card: Playback Settings ──
            FoldoutCard playbackCard = CreateFoldoutCard("播放设置", m_PlaybackSectionExpanded, value => m_PlaybackSectionExpanded = value);

            VisualElement speedRow = new VisualElement();
            speedRow.style.flexDirection = FlexDirection.Row;
            speedRow.style.alignItems = Align.Center;

            m_PlaySpeedField = new FloatField("播放速度") { value = 1f };
            m_PlaySpeedField.tooltip = "点击播放 clip 时使用的预览速度，只影响当前预览，不写入配置。";
            m_PlaySpeedField.style.flexGrow = 1;
            speedRow.Add(m_PlaySpeedField);

            m_PauseButton = CreateStyledButton("暂停", TogglePause, AccentColor, 8f);
            SetPauseButtonState(false, false);
            speedRow.Add(m_PauseButton);

            playbackCard.Content.Add(speedRow);

            VisualElement optionsRow = new VisualElement();
            optionsRow.style.flexDirection = FlexDirection.Row;
            optionsRow.style.alignItems = Align.Center;
            optionsRow.style.marginTop = 4;

            m_RootMotionToggle = new Toggle("Root Motion") { value = false };
            m_RootMotionToggle.tooltip = "切换预览时是否应用 Root Motion，只影响当前预览，不写入配置。";
            m_RootMotionToggle.style.flexShrink = 0;
            m_RootMotionToggle.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;
                m_Session.SetRootMotionEnabled(evt.newValue);
                SetStatus(evt.newValue ? "已开启 Root Motion 预览。" : "已关闭 Root Motion，预览对象回到初始位置。");
            });
            optionsRow.Add(m_RootMotionToggle);

            playbackCard.Content.Add(optionsRow);

            ScrollView inspectorScrollView = new ScrollView();
            inspectorScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            inspectorScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            inspectorScrollView.style.flexGrow = 1;
            inspectorScrollView.style.minHeight = 0;
            inspectorScrollView.Add(assetsCard.Root);
            inspectorScrollView.Add(playbackCard.Root);

            // ── Card: Clips ──
            m_StopAllButton = CreateStyledButton("停止全部", StopAllClips, DangerColor);
            SetStopAllButtonEnabled(false);
            FoldoutCard clipsCard = CreateFoldoutCard("Clips", m_ClipsSectionExpanded, value => m_ClipsSectionExpanded = value, m_StopAllButton);

            m_ClipListView = new VisualElement();
            clipsCard.Content.Add(m_ClipListView);
            inspectorScrollView.Add(clipsCard.Root);

            // ── Card: Channels ──
            m_AddChannelButton = CreateStyledButton("+ Channel", AddChannel, AccentColor);
            m_AddChannelButton.tooltip = "新增一个 channel。";
            SetAddChannelButtonEnabled(false);
            FoldoutCard channelsCard = CreateFoldoutCard("Channels", m_ChannelsSectionExpanded, value => m_ChannelsSectionExpanded = value, m_AddChannelButton);
            m_ChannelControlsContainer = new VisualElement();
            channelsCard.Content.Add(m_ChannelControlsContainer);
            inspectorScrollView.Add(channelsCard.Root);

            // ── Card: Cue Log ──
            VisualElement cueCard = CreateCard("Cue Log");
            m_CueLogContainer = new ScrollView();
            m_CueLogContainer.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_CueLogContainer.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_CueLogContainer.style.flexGrow = 1;
            m_CueLogContainer.style.minHeight = 0;
            cueCard.Add(m_CueLogContainer);
            ConfigureConsoleSection(cueCard);

            pane.Add(BuildInspectorConsoleSplit(inspectorScrollView, cueCard));

            VisualElement statusSpacer = new VisualElement();
            statusSpacer.style.height = 4;
            statusSpacer.style.flexShrink = 0;
            pane.Add(statusSpacer);
            pane.Add(BuildStatusRow());

            return pane;
        }

        private static VisualElement CreateCard(string titleText, VisualElement titleAction = null)
        {
            VisualElement card = new VisualElement();
            card.style.marginBottom = 2;
            card.style.paddingLeft = 3;
            card.style.paddingRight = 3;
            card.style.paddingTop = 3;
            card.style.paddingBottom = 3;
            card.style.backgroundColor = new Color(0.15f, 0.15f, 0.16f, 1f);
            card.style.borderTopLeftRadius = 3;
            card.style.borderTopRightRadius = 3;
            card.style.borderBottomLeftRadius = 3;
            card.style.borderBottomRightRadius = 3;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = SectionDivider;
            card.style.borderBottomColor = SectionDivider;
            card.style.borderLeftColor = SectionDivider;
            card.style.borderRightColor = SectionDivider;

            // title bar with left accent
            VisualElement titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 2;
            titleRow.style.paddingBottom = 2;
            titleRow.style.borderBottomWidth = 1;
            titleRow.style.borderBottomColor = SectionDivider;

            VisualElement accent = new VisualElement();
            accent.style.width = 2;
            accent.style.height = 11;
            accent.style.backgroundColor = AccentColor;
            accent.style.borderTopLeftRadius = 2;
            accent.style.borderTopRightRadius = 2;
            accent.style.borderBottomLeftRadius = 2;
            accent.style.borderBottomRightRadius = 2;
            accent.style.marginRight = 4;
            titleRow.Add(accent);

            Label label = new(titleText);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = SectionTitleFontSize;
            label.style.color = TextNormal;
            label.style.flexGrow = 1;
            titleRow.Add(label);

            if (titleAction != null)
            {
                titleAction.style.flexShrink = 0;
                titleRow.Add(titleAction);
            }

            card.Add(titleRow);
            return card;
        }

        private static FoldoutCard CreateFoldoutCard(
            string titleText,
            bool expanded,
            Action<bool> setExpanded,
            VisualElement titleAction = null)
        {
            VisualElement card = CreateCard(titleText, titleAction);
            VisualElement titleRow = card[0];
            Label label = titleRow.Q<Label>();
            VisualElement content = new VisualElement();
            content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(content);

            void ApplyExpanded(bool value)
            {
                expanded = value;
                setExpanded?.Invoke(value);
                content.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (label != null)
                {
                    label.text = value ? $"▾ {titleText}" : $"▸ {titleText}";
                }

                titleRow.style.marginBottom = value ? 2 : 0;
                titleRow.style.paddingBottom = value ? 2 : 0;
                titleRow.style.borderBottomWidth = value ? 1 : 0;
            }

            ApplyExpanded(expanded);
            titleRow.tooltip = $"点击展开/收起 {titleText} 分区。";
            titleRow.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                if (titleAction != null &&
                    evt.target is VisualElement target &&
                    (ReferenceEquals(target, titleAction) || titleAction.Contains(target)))
                {
                    return;
                }

                ApplyExpanded(!expanded);
                evt.StopPropagation();
            });

            return new FoldoutCard
            {
                Root = card,
                Content = content
            };
        }

        private static void ConfigureConsoleSection(VisualElement section)
        {
            section.style.minHeight = CueLogSectionMinHeight;
            section.style.flexGrow = 1;
            section.style.flexShrink = 1;
            section.style.overflow = Overflow.Hidden;
        }

        private static VisualElement BuildInspectorConsoleSplit(VisualElement inspectorPane, VisualElement cueCard)
        {
            inspectorPane.style.minHeight = InspectorMinHeight;
            inspectorPane.style.flexGrow = 1;
            inspectorPane.style.flexShrink = 1;

            TwoPaneSplitView splitView = new(1, CueLogInitialHeight, TwoPaneSplitViewOrientation.Vertical);
            splitView.style.flexGrow = 1;
            splitView.style.minHeight = 0;
            splitView.Add(inspectorPane);
            splitView.Add(cueCard);
            return splitView;
        }

        private void RegisterPreviewEvents()
        {
            m_PreviewImage.focusable = true;

            // Right-click drag to rotate camera
            m_PreviewImage.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 1 || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_IsPreviewDragging = true;
                m_LastPreviewMousePosition = evt.localMousePosition;
                m_PreviewImage.CaptureMouse();
                m_PreviewImage.Focus();
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!m_IsPreviewDragging || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                Vector2 delta = evt.localMousePosition - m_LastPreviewMousePosition;
                m_LastPreviewMousePosition = evt.localMousePosition;
                m_Session.Orbit(delta);
                RenderPreview();
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                m_IsPreviewDragging = false;
                if (m_PreviewImage.HasMouseCapture())
                {
                    m_PreviewImage.ReleaseMouse();
                }
                evt.StopPropagation();
            });

            // Scroll to zoom
            m_PreviewImage.RegisterCallback<WheelEvent>(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_Session.Zoom(evt.delta.y);
                RenderPreview();
                evt.StopPropagation();
            });

            // Keyboard: WASD + QE for camera movement
            m_PreviewImage.RegisterCallback<KeyDownEvent>(evt =>
            {
                KeyCode key = EventToKeyCode(evt);
                if (key != KeyCode.None)
                {
                    m_PressedKeys.Add(key);
                    evt.StopPropagation();
                }
            });

            m_PreviewImage.RegisterCallback<KeyUpEvent>(evt =>
            {
                KeyCode key = EventToKeyCode(evt);
                if (key != KeyCode.None)
                {
                    m_PressedKeys.Remove(key);
                    evt.StopPropagation();
                }
            });

            m_PreviewImage.RegisterCallback<FocusOutEvent>(_ => m_PressedKeys.Clear());

            m_PreviewImage.RegisterCallback<GeometryChangedEvent>(_ => RenderPreview());
        }

        private static KeyCode EventToKeyCode(KeyboardEventBase<KeyDownEvent> evt)
        {
            return evt.keyCode switch
            {
                UnityEngine.KeyCode.W => KeyCode.W,
                UnityEngine.KeyCode.A => KeyCode.A,
                UnityEngine.KeyCode.S => KeyCode.S,
                UnityEngine.KeyCode.D => KeyCode.D,
                UnityEngine.KeyCode.Q => KeyCode.Q,
                UnityEngine.KeyCode.E => KeyCode.E,
                UnityEngine.KeyCode.LeftShift => KeyCode.LeftShift,
                UnityEngine.KeyCode.RightShift => KeyCode.RightShift,
                _ => KeyCode.None
            };
        }

        private static KeyCode EventToKeyCode(KeyboardEventBase<KeyUpEvent> evt)
        {
            return evt.keyCode switch
            {
                UnityEngine.KeyCode.W => KeyCode.W,
                UnityEngine.KeyCode.A => KeyCode.A,
                UnityEngine.KeyCode.S => KeyCode.S,
                UnityEngine.KeyCode.D => KeyCode.D,
                UnityEngine.KeyCode.Q => KeyCode.Q,
                UnityEngine.KeyCode.E => KeyCode.E,
                UnityEngine.KeyCode.LeftShift => KeyCode.LeftShift,
                UnityEngine.KeyCode.RightShift => KeyCode.RightShift,
                _ => KeyCode.None
            };
        }

        private void ProcessCameraMovement(float deltaTime)
        {
            if (m_PressedKeys.Count == 0 || m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            bool shift = m_PressedKeys.Contains(KeyCode.LeftShift) || m_PressedKeys.Contains(KeyCode.RightShift);
            float speed = (shift ? 9f : 3f) * deltaTime;
            Vector3 move = Vector3.zero;

            if (m_PressedKeys.Contains(KeyCode.W)) move.z += speed;
            if (m_PressedKeys.Contains(KeyCode.S)) move.z -= speed;
            if (m_PressedKeys.Contains(KeyCode.A)) move.x -= speed;
            if (m_PressedKeys.Contains(KeyCode.D)) move.x += speed;
            if (m_PressedKeys.Contains(KeyCode.E)) move.y += speed;
            if (m_PressedKeys.Contains(KeyCode.Q)) move.y -= speed;

            if (move.sqrMagnitude > 0f)
            {
                m_Session.MoveCamera(move);
            }
        }

        private void HandleEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - m_LastEditorTime);
            m_LastEditorTime = now;

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            if (!m_IsPaused)
            {
                m_Session.Update(deltaTime);
            }
            ProcessCameraMovement(deltaTime);
            RenderPreview();
            RefreshClipPlayingStates();
            RefreshChannelStates();
            RefreshCueLogView();
            Repaint();
        }

        private void ApplyDefaultSelections()
        {
            if (m_SelectedPrefab != null)
            {
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }
            else
            {
                m_SelectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);
            }

            if (m_SelectedAsset != null)
            {
                m_AssetField.SetValueWithoutNotify(m_SelectedAsset);
            }
            else
            {
                m_SelectedAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultSampleAssetPath);
            }

            if (m_PrefabField.value == null)
            {
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }

            if (m_AssetField.value == null)
            {
                m_AssetField.SetValueWithoutNotify(m_SelectedAsset);
            }
        }

        private void SetPendingOpenRequest(TextAsset animationAsset, GameObject prefab, bool autoLoad)
        {
            m_PendingAsset = animationAsset;
            m_PendingPrefab = prefab;
            m_PendingAutoLoad = autoLoad;

            if (m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            ApplyPendingOpenRequest();
        }

        private void ApplyPendingOpenRequest()
        {
            if (m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            bool hasPendingRequest = m_PendingAsset != null || m_PendingPrefab != null || m_PendingAutoLoad;
            if (!hasPendingRequest)
            {
                return;
            }

            if (m_PendingAsset != null)
            {
                m_SelectedAsset = m_PendingAsset;
                m_AssetField.SetValueWithoutNotify(m_SelectedAsset);
            }

            if (m_PendingPrefab != null)
            {
                m_SelectedPrefab = m_PendingPrefab;
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }
            else if (m_PrefabField.value == null)
            {
                m_SelectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }

            bool shouldAutoLoad = m_PendingAutoLoad && m_AssetField.value != null;

            m_PendingAsset = null;
            m_PendingPrefab = null;
            m_PendingAutoLoad = false;

            if (shouldAutoLoad)
            {
                LoadPreview();
            }
        }

        private void ScheduleAutoReloadPreview()
        {
            bool hasPendingRequest = m_PendingAsset != null || m_PendingPrefab != null || m_PendingAutoLoad;
            if (hasPendingRequest)
            {
                return;
            }

            if (!m_ShouldAutoReloadPreview || m_SelectedAsset == null || m_SelectedPrefab == null)
            {
                return;
            }

            EditorApplication.delayCall += AutoReloadPreview;
        }

        private void AutoReloadPreview()
        {
            if (this == null || !m_ShouldAutoReloadPreview || m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoReloadPreview;
                return;
            }

            m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            m_AssetField.SetValueWithoutNotify(m_SelectedAsset);
            LoadPreview();
        }

        private void LoadPreview()
        {
            try
            {
                GameObject prefab = m_PrefabField.value as GameObject;
                TextAsset assetText = m_AssetField.value as TextAsset;
                if (prefab == null)
                {
                    throw new XFrameworkException("请选择一个 prefab 资源。");
                }

                if (assetText == null)
                {
                    throw new XFrameworkException("请选择一个 .xasset 资源。");
                }

                string assetPath = AssetDatabase.GetAssetPath(assetText);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    throw new XFrameworkException("无法获取 XAnimationAsset 的资源路径。");
                }

                DisposeSession();
                m_Session = new XAnimationEditorPreviewSession();
                m_Session.Load(prefab, assetPath);
                m_SelectedPrefab = prefab;
                m_SelectedAsset = assetText;
                m_ShouldAutoReloadPreview = true;
                m_IsPaused = false;
                SetPauseButtonState(false, false);
                m_RootMotionToggle.SetValueWithoutNotify(false);
                m_GridToggle.SetValueWithoutNotify(true);
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
                RefreshCueLogView(force: true);
                SetStatus("预览已加载。");
                RenderPreview();
            }
            catch (Exception ex)
            {
                m_ShouldAutoReloadPreview = false;
                DisposeSession();
                ClearDebugViews();
                SetStatus(ex.Message, true);
            }
        }

        private void StopAllClips()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_Session.StopAll();
            m_IsPaused = false;
            SetPauseButtonState(false, false);
            RefreshClipPlayingStates();
            RefreshChannelStates();
            SetStatus("已停止全部通道。");
        }

        private void TogglePause()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_IsPaused = !m_IsPaused;
            SetPauseButtonState(true, m_IsPaused);
            SetStatus(m_IsPaused ? "已暂停动画预览。" : "已继续动画预览。");
        }

        private void ResetPreviewTransform()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_Session.ResetTransform();
            SetStatus("预览对象已回到初始位置。");
        }

        private void ResetPreviewCamera()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_Session.ResetCamera();
            RenderPreview();
            SetStatus("预览视角已重置。");
        }

        private void RenderPreview()
        {
            if (m_Session == null || !m_Session.IsLoaded || m_PreviewImage == null)
            {
                if (m_PreviewImage != null)
                {
                    m_PreviewImage.image = null;
                }
                return;
            }

            Rect rect = m_PreviewImage.contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            m_Session.Render(rect.size);
            m_PreviewImage.image = m_Session.PreviewTexture;
        }

        private void ScheduleAssetSave()
        {
            if (m_Session == null || !m_Session.IsLoaded || rootVisualElement == null)
            {
                return;
            }

            m_DelayedSaveItem?.Pause();
            m_DelayedSaveItem = rootVisualElement.schedule.Execute(() =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_Session.SaveCurrentAsset();
            }).StartingIn(350);
        }

        private void RebuildClipList()
        {
            m_ClipListView.Clear();
            m_ClipLabelMap.Clear();
            m_ClipRowMap.Clear();
            m_ClipVisualStateMap.Clear();
            m_ClipButtonMap.Clear();
            m_ClipChannelMap.Clear();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            Dictionary<string, List<XAnimationCompiledClip>> clipsByChannel = new(StringComparer.Ordinal);
            for (int i = 0; i < clips.Count; i++)
            {
                XAnimationCompiledClip clip = (XAnimationCompiledClip)clips[i];
                if (!clipsByChannel.TryGetValue(clip.Config.defaultChannel, out List<XAnimationCompiledClip> channelClips))
                {
                    channelClips = new List<XAnimationCompiledClip>();
                    clipsByChannel.Add(clip.Config.defaultChannel, channelClips);
                }

                channelClips.Add(clip);
            }

            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = (XAnimationCompiledChannel)channels[i];
                if (!clipsByChannel.TryGetValue(channel.Name, out List<XAnimationCompiledClip> channelClips))
                {
                    channelClips = new List<XAnimationCompiledClip>();
                }

                VisualElement group = new VisualElement();
                group.style.marginBottom = 3;
                group.style.paddingLeft = 3;
                group.style.paddingRight = 3;
                group.style.paddingTop = 3;
                group.style.paddingBottom = 2;
                group.style.backgroundColor = ListGroupBg;
                group.style.borderTopWidth = 1;
                group.style.borderBottomWidth = 1;
                group.style.borderLeftWidth = 1;
                group.style.borderRightWidth = 1;
                group.style.borderTopColor = SectionDivider;
                group.style.borderBottomColor = SectionDivider;
                group.style.borderLeftColor = SectionDivider;
                group.style.borderRightColor = SectionDivider;
                group.style.borderTopLeftRadius = 3;
                group.style.borderTopRightRadius = 3;
                group.style.borderBottomLeftRadius = 3;
                group.style.borderBottomRightRadius = 3;

                VisualElement groupHeader = new VisualElement();
                groupHeader.style.flexDirection = FlexDirection.Row;
                groupHeader.style.alignItems = Align.Center;
                groupHeader.style.marginBottom = 2;
                groupHeader.style.paddingLeft = 3;
                groupHeader.style.paddingRight = 3;
                groupHeader.style.paddingTop = 2;
                groupHeader.style.paddingBottom = 2;
                groupHeader.style.backgroundColor = ListHeaderBg;
                groupHeader.style.borderTopLeftRadius = 3;
                groupHeader.style.borderTopRightRadius = 3;
                groupHeader.style.borderBottomLeftRadius = 3;
                groupHeader.style.borderBottomRightRadius = 3;

                Label groupTitle = new($"▾ {channel.Name}");
                groupTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                groupTitle.style.color = TextNormal;
                groupTitle.style.flexGrow = 1;
                groupTitle.tooltip = "点击展开/收起这个 channel 的 clip 列表；也可以将 clip 拖到这里以移动到该 channel 末尾。";
                groupHeader.Add(groupTitle);

                Label groupInfo = new($"{channel.Config.layerType} | {channelClips.Count} clips");
                groupInfo.style.color = TextMuted;
                groupInfo.style.fontSize = 10;
                groupInfo.style.flexShrink = 0;
                groupHeader.Add(groupInfo);

                Button addClipButton = new(() => AddClip(channel.Name))
                {
                    text = "+"
                };
                addClipButton.tooltip = m_Session.IsOverrideAsset
                    ? "Override 资源不能新增 clip。"
                    : "在这个 channel 下新增一个 clip。";
                addClipButton.SetEnabled(!m_Session.IsOverrideAsset);
                ApplyClipIconButtonStyle(addClipButton, AccentColor);
                addClipButton.style.marginLeft = 6;
                groupHeader.Add(addClipButton);
                group.Add(groupHeader);
                RegisterClipChannelDropTarget(group, groupHeader, channel.Name);

                VisualElement clipsContainer = new VisualElement();
                for (int clipIndex = 0; clipIndex < channelClips.Count; clipIndex++)
                {
                    XAnimationCompiledClip clip = channelClips[clipIndex];
                    VisualElement row = CreateClipRow(clip, clipIndex);
                    RegisterClipRowDropTarget(row, channel.Name, clip.Key);
                    clipsContainer.Add(row);
                }

                groupTitle.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    bool expanded = clipsContainer.style.display != DisplayStyle.None;
                    clipsContainer.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                    groupTitle.text = expanded ? $"▸ {channel.Name}" : $"▾ {channel.Name}";
                    evt.StopPropagation();
                });

                group.Add(clipsContainer);
                m_ClipListView.Add(group);
            }

            TryBeginPendingRename();
        }

        private VisualElement CreateCueRow(int cueIndex, XAnimationCueConfig cue, bool editable)
        {
            VisualElement row = CreateSubBox();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 3;
            row.tooltip = editable
                ? "Cue 会在对应 clip 播放经过 normalized time 时触发。"
                : "Override 资源只能预览 cue，不能编辑 base cue 配置。";

            VisualElement topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;
            topRow.style.marginBottom = 2;
            row.Add(topRow);

            Label indexLabel = new($"#{cueIndex}");
            indexLabel.style.width = 28;
            indexLabel.style.flexShrink = 0;
            indexLabel.style.color = TextMuted;
            indexLabel.style.fontSize = BodyFontSize;
            indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            topRow.Add(indexLabel);

            FloatField timeField = new("time")
            {
                value = cue.time
            };
            timeField.tooltip = "Cue 触发时间，范围是 clip normalized time [0, 1]。";
            timeField.SetEnabled(editable);
            timeField.style.flexGrow = 1;
            timeField.RegisterValueChangedCallback(evt => ChangeCueTime(cueIndex, evt.newValue, timeField));
            topRow.Add(timeField);

            Button deleteButton = new(() => DeleteCue(cueIndex))
            {
                text = "⌫"
            };
            deleteButton.tooltip = editable ? "删除这个 cue。" : "Override 资源不能删除 cue。";
            deleteButton.SetEnabled(editable);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;
            topRow.Add(deleteButton);

            TextField eventKeyField = new("eventKey")
            {
                value = cue.eventKey ?? string.Empty,
                isDelayed = true
            };
            eventKeyField.tooltip = "Cue 触发时派发的事件 key，不能为空。";
            eventKeyField.SetEnabled(editable);
            eventKeyField.RegisterValueChangedCallback(evt => ChangeCueEventKey(cueIndex, evt.newValue, eventKeyField, evt.previousValue));
            row.Add(eventKeyField);

            TextField payloadField = new("payload")
            {
                value = cue.payload ?? string.Empty,
                isDelayed = true
            };
            payloadField.tooltip = "Cue 触发时携带的字符串 payload。";
            payloadField.SetEnabled(editable);
            payloadField.RegisterValueChangedCallback(evt => ChangeCuePayload(cueIndex, evt.newValue));
            row.Add(payloadField);

            return row;
        }

        private VisualElement CreateClipCueEditor(string clipKey)
        {
            VisualElement box = CreateSubBox();
            box.style.marginTop = 5;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 3;

            Label title = new("Cues");
            title.style.flexGrow = 1;
            title.style.color = TextNormal;
            title.style.fontSize = BodyFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            bool editable = m_Session != null && !m_Session.IsOverrideAsset;
            Button addButton = new(() => AddCue(clipKey))
            {
                text = "+"
            };
            addButton.tooltip = editable ? "在这个 clip 下新增一个 cue。" : "Override 资源不能新增 cue。";
            addButton.SetEnabled(editable);
            ApplyClipIconButtonStyle(addButton, AccentColor);
            header.Add(addButton);
            box.Add(header);

            XAnimationCueConfig[] cues = m_Session?.CompiledAsset.Asset.cues ?? Array.Empty<XAnimationCueConfig>();
            bool hasCue = false;
            for (int i = 0; i < cues.Length; i++)
            {
                XAnimationCueConfig cue = cues[i];
                if (cue == null || !string.Equals(cue.clipKey, clipKey, StringComparison.Ordinal))
                {
                    continue;
                }

                hasCue = true;
                box.Add(CreateCueRow(i, cue, editable));
            }

            if (!hasCue)
            {
                Label emptyLabel = new("No cues");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                emptyLabel.style.marginTop = 1;
                box.Add(emptyLabel);
            }

            return box;
        }

        private VisualElement CreateClipRow(XAnimationCompiledClip clip, int rowIndex)
        {
            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.marginBottom = 1;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.borderTopLeftRadius = 2;
            row.style.borderTopRightRadius = 2;
            row.style.borderBottomLeftRadius = 2;
            row.style.borderBottomRightRadius = 2;
            Color baseColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            row.style.backgroundColor = baseColor;
            m_ClipRowMap[clip.Key] = row;
            ClipRowVisualState visualState = new()
            {
                BaseColor = baseColor
            };
            m_ClipVisualStateMap[clip.Key] = visualState;
            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                visualState.Hovered = true;
                ApplyClipRowVisualState(clip.Key);
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                visualState.Hovered = false;
                ApplyClipRowVisualState(clip.Key);
            });

            string clipKey = clip.Key;

            EditableLabel label = new(clipKey);
            ConfigureEditableNameLabel(label, 78f);
            label.tooltip = "单击展开/收起 clip 配置；右键 Rename 编辑名称；按住拖动可移动到其他 channel，或拖到 clip 行上方调整顺序。";
            label.SetEditable(true, EditableLabelEditTrigger.ContextMenu);
            label.EditStarted += BeginNameEdit;
            label.EditEnded += EndNameEdit;
            label.ValueCommitted += (_, newValue) => RenameClip(clipKey, newValue, label);
            m_ClipLabelMap[clipKey] = label;
            row.Add(label);

            VisualElement fileInfo = new VisualElement();
            fileInfo.style.flexGrow = 1;
            fileInfo.style.flexShrink = 1;
            fileInfo.style.minWidth = 140;
            fileInfo.style.marginLeft = 4;
            fileInfo.style.marginRight = 4;
            fileInfo.style.flexDirection = FlexDirection.Row;
            row.Add(fileInfo);

            string activeClipPath = clip.Config.clipPath;
            m_ClipChannelMap[clipKey] = clip.Config.defaultChannel;

            ObjectField activeClipField = CreateClipObjectField(activeClipPath, editable: true);
            activeClipField.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "当前 Override 资源中的覆盖动画。可直接修改，不会写回 base 资源。"
                : "该 clip 对应的 AnimationClip 资源。可直接修改并保存到当前 XAnimation 文件。";
            activeClipField.style.flexGrow = 1;
            activeClipField.style.flexShrink = 1;
            activeClipField.style.minWidth = 120;
            activeClipField.style.maxWidth = 260;
            activeClipField.RegisterValueChangedCallback(evt => ChangeClipPath(clip, activeClipField, evt.previousValue as AnimationClip, evt.newValue as AnimationClip));
            fileInfo.Add(activeClipField);

            VisualElement editor = CreateClipEditor(clip);
            editor.style.display = m_ExpandedClipKeys.Contains(clipKey) ? DisplayStyle.Flex : DisplayStyle.None;
            RegisterClipNameInteractions(label, editor, clip);

            Button toggleButton = new(() =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                // Check if this clip is currently playing
                string channelName = clip.Config.defaultChannel;
                XAnimationChannelState state = m_Session.GetChannelState(channelName);
                bool isPlaying = state != null && string.Equals(state.clipKey, clipKey, StringComparison.Ordinal);

                if (isPlaying)
                {
                    m_Session.StopChannel(channelName);
                    RefreshClipPlayingStates();
                    RefreshChannelStates();
                    SetStatus($"已停止 {clipKey}。");
                }
                else
                {
                    m_IsPaused = false;
                    SetPauseButtonState(true, false);
                    m_Session.Play(clipKey, m_PlaySpeedField.value, clip.Config.defaultChannel);
                    RefreshClipPlayingStates();
                    RefreshChannelStates();
                    SetStatus($"正在播放 {clipKey} ({clip.Clip.name})。");
                }
            })
            {
                text = "▶"
            };
            toggleButton.tooltip = "播放或停止这个 clip。";
            ApplyClipButtonStyle(toggleButton, false);
            toggleButton.style.flexShrink = 0;
            toggleButton.style.marginLeft = 4;
            row.Add(toggleButton);

            Button deleteButton = new(() => DeleteClip(clipKey))
            {
                text = "⌫"
            };
            deleteButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能删除 clip 结构。"
                : "删除这个 clip。";
            deleteButton.SetEnabled(m_Session != null && !m_Session.IsOverrideAsset);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.flexShrink = 0;
            deleteButton.style.marginLeft = 3;
            row.Add(deleteButton);

            m_ClipButtonMap[clipKey] = toggleButton;
            container.Add(row);
            container.Add(editor);
            return container;
        }

        private static void ConfigureEditableNameLabel(EditableLabel label, float width)
        {
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = BodyFontSize;
            label.style.color = TextNormal;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            TextElement textElement = label.Q<TextElement>();
            if (textElement != null)
            {
                textElement.style.fontSize = BodyFontSize;
                textElement.style.color = TextNormal;
                textElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            TextField textField = label.Q<TextField>();
            if (textField != null)
            {
                textField.style.marginTop = 0;
                textField.style.marginBottom = 0;
                textField.style.fontSize = BodyFontSize;
                VisualElement input = textField.Q("unity-text-input");
                if (input != null)
                {
                    input.style.fontSize = BodyFontSize;
                }
            }
        }

        private void RegisterClipNameInteractions(EditableLabel label, VisualElement editor, XAnimationCompiledClip clip)
        {
            bool isPressed = false;
            Vector2 startPosition = Vector2.zero;
            bool movedBeyondClickThreshold = false;
            bool dragStarted = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                if (label.IsEditing)
                {
                    ClearClipDragData();
                    isPressed = false;
                    movedBeyondClickThreshold = false;
                    dragStarted = false;
                    return;
                }

                isPressed = true;
                movedBeyondClickThreshold = false;
                dragStarted = false;
                startPosition = evt.mousePosition;
                ClearClipDragData();
                evt.StopPropagation();
            });
            label.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!isPressed || m_IsEditingName || label.IsEditing)
                {
                    return;
                }

                if (!movedBeyondClickThreshold && (evt.mousePosition - startPosition).sqrMagnitude >= 16f)
                {
                    movedBeyondClickThreshold = true;
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData(ClipDragDataKey, clip.Key);
                    DragAndDrop.StartDrag($"Move {clip.Key}");
                    dragStarted = true;
                    evt.StopPropagation();
                }
            });
            label.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!isPressed || evt.button != 0)
                {
                    return;
                }

                if (!movedBeyondClickThreshold)
                {
                    if (!label.IsEditing)
                    {
                        bool expanded = editor.style.display == DisplayStyle.None;
                        editor.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                        if (expanded)
                        {
                            m_ExpandedClipKeys.Add(clip.Key);
                        }
                        else
                        {
                            m_ExpandedClipKeys.Remove(clip.Key);
                        }
                    }
                }

                if (!dragStarted)
                {
                    ClearClipDragData();
                }

                isPressed = false;
                movedBeyondClickThreshold = false;
                dragStarted = false;
                evt.StopPropagation();
            });
        }

        private void BeginNameEdit()
        {
            m_IsEditingName = true;
            ClearClipDragData();
        }

        private void EndNameEdit()
        {
            m_IsEditingName = false;
            ClearClipDragData();
        }

        private static void ClearClipDragData()
        {
            DragAndDrop.SetGenericData(ClipDragDataKey, null);
        }

        private void AddChannel()
        {
            try
            {
                string channelName = m_Session.AddChannel();
                m_PendingChannelRenameKey = channelName;
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
                SetStatus($"已新增 Channel {channelName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteChannel(string channelName)
        {
            int clipCount = CountClipsInChannel(channelName);
            string message = clipCount > 0
                ? $"确定删除 Channel '{channelName}'？\n\n将同时删除该 Channel 下的 {clipCount} 个 Clip。"
                : $"确定删除 Channel '{channelName}'？";
            if (!EditorUtility.DisplayDialog("删除 Channel", message, "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteChannel(channelName);
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
                SetStatus($"已删除 Channel {channelName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private int CountClipsInChannel(string channelName)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            for (int i = 0; i < clips.Count; i++)
            {
                XAnimationCompiledClip clip = clips[i];
                if (clip != null && string.Equals(clip.Config.defaultChannel, channelName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private void AddClip(string channelName)
        {
            try
            {
                string clipKey = m_Session.AddClip(channelName);
                m_PendingClipRenameKey = clipKey;
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
                SetStatus($"已在 {channelName} 新增 Clip {clipKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteClip(string clipKey)
        {
            if (!EditorUtility.DisplayDialog("删除 Clip", $"确定删除 Clip '{clipKey}'？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteClip(clipKey);
                m_ExpandedClipKeys.Remove(clipKey);
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
                SetStatus($"已删除 Clip {clipKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddCue(string clipKey)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            try
            {
                int cueIndex = m_Session.AddCue(clipKey);
                RebuildClipList();
                SetStatus($"已在 {clipKey} 新增 Cue #{cueIndex}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteCue(int cueIndex)
        {
            if (!EditorUtility.DisplayDialog("删除 Cue", $"确定删除 Cue #{cueIndex}？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteCue(cueIndex);
                RebuildClipList();
                SetStatus($"已删除 Cue #{cueIndex}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCueClipKey(int cueIndex, string clipKey, DropdownField field, string previousValue)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                field.SetValueWithoutNotify(previousValue);
                return;
            }

            try
            {
                m_Session.SetCueClipKey(cueIndex, clipKey);
                RebuildClipList();
                SetStatus($"Cue #{cueIndex} clipKey = {clipKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCueTime(int cueIndex, float time, FloatField field)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            float clampedTime = Mathf.Clamp01(time);
            if (!Mathf.Approximately(clampedTime, time))
            {
                field.SetValueWithoutNotify(clampedTime);
            }

            try
            {
                m_Session.SetCueTime(cueIndex, clampedTime, save: false);
                ScheduleAssetSave();
                SetStatus($"Cue #{cueIndex} time = {clampedTime:0.###}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCueEventKey(int cueIndex, string eventKey, TextField field, string previousValue)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                field.SetValueWithoutNotify(previousValue);
                return;
            }

            try
            {
                m_Session.SetCueEventKey(cueIndex, eventKey);
                SetStatus($"Cue #{cueIndex} eventKey = {eventKey?.Trim()}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCuePayload(int cueIndex, string payload)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            try
            {
                m_Session.SetCuePayload(cueIndex, payload);
                SetStatus($"Cue #{cueIndex} payload 已更新。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RenameClip(string oldKey, string newKey, EditableLabel label)
        {
            newKey = newKey?.Trim();
            try
            {
                m_Session.RenameClip(oldKey, newKey);
                SetStatus($"Clip {oldKey} 已重命名为 {newKey}。");
                if (m_ExpandedClipKeys.Remove(oldKey) && !string.IsNullOrWhiteSpace(newKey))
                {
                    m_ExpandedClipKeys.Add(newKey.Trim());
                }
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
            }
            catch (Exception ex)
            {
                label.text = oldKey;
                SetStatus(ex.Message);
                Debug.LogException(ex);
            }
        }

        private void RenameChannel(string oldName, string newName, EditableLabel label)
        {
            newName = newName?.Trim();
            try
            {
                m_Session.RenameChannel(oldName, newName);
                SetStatus($"Channel {oldName} 已重命名为 {newName}。");
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
            }
            catch (Exception ex)
            {
                label.text = oldName;
                SetStatus(ex.Message);
                Debug.LogException(ex);
            }
        }

        private void RegisterClipChannelDropTarget(VisualElement group, VisualElement groupHeader, string channelName)
        {
            group.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, channelName))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                groupHeader.style.backgroundColor = AccentColor;
                evt.StopPropagation();
            });
            group.RegisterCallback<DragLeaveEvent>(_ => groupHeader.style.backgroundColor = ListHeaderBg);
            group.RegisterCallback<DragPerformEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, channelName))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                groupHeader.style.backgroundColor = ListHeaderBg;
                MoveClip(clipKey, channelName);
                evt.StopPropagation();
            });
        }

        private void RegisterClipRowDropTarget(VisualElement row, string channelName, string insertBeforeClipKey)
        {
            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, channelName, insertBeforeClipKey))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                row.style.borderTopColor = AccentColor;
                row.style.borderTopWidth = 2;
                evt.StopPropagation();
            });
            row.RegisterCallback<DragLeaveEvent>(_ =>
            {
                row.style.borderTopColor = Color.clear;
                row.style.borderTopWidth = 0;
            });
            row.RegisterCallback<DragPerformEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, channelName, insertBeforeClipKey))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                row.style.borderTopColor = Color.clear;
                row.style.borderTopWidth = 0;
                MoveClip(clipKey, channelName, insertBeforeClipKey);
                evt.StopPropagation();
            });
        }

        private bool CanDropClip(string clipKey, string channelName, string insertBeforeClipKey = null)
        {
            if (m_IsEditingName ||
                m_Session == null || !m_Session.IsLoaded ||
                string.IsNullOrWhiteSpace(clipKey) ||
                string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            if (!m_ClipChannelMap.TryGetValue(clipKey, out string currentChannel))
            {
                return false;
            }

            return !string.Equals(clipKey, insertBeforeClipKey, StringComparison.Ordinal) ||
                !string.Equals(currentChannel, channelName, StringComparison.Ordinal);
        }

        private void MoveClip(string clipKey, string channelName, string insertBeforeClipKey = null)
        {
            m_Session.MoveClip(clipKey, channelName, insertBeforeClipKey);
            m_ClipChannelMap[clipKey] = channelName;
            RebuildClipList();
            RefreshClipPlayingStates();
            RefreshChannelStates();
            SetStatus($"{clipKey} 已移动到 {channelName}。");
        }

        private void TryBeginPendingRename()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(m_PendingClipRenameKey) &&
                m_ClipLabelMap.TryGetValue(m_PendingClipRenameKey, out EditableLabel clipLabel))
            {
                string clipKey = m_PendingClipRenameKey;
                m_PendingClipRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    if (clipLabel != null)
                    {
                        m_ExpandedClipKeys.Remove(clipKey);
                        clipLabel.BeginEdit();
                    }
                }).StartingIn(0);
            }

            if (!string.IsNullOrWhiteSpace(m_PendingChannelRenameKey) &&
                m_ChannelLabelMap.TryGetValue(m_PendingChannelRenameKey, out EditableLabel channelLabel))
            {
                m_PendingChannelRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    channelLabel?.BeginEdit();
                }).StartingIn(0);
            }
        }

        private static void ApplyClipIconButtonStyle(Button btn, Color? bgColor = null, float size = ClipIconButtonSize)
        {
            btn.style.backgroundColor = bgColor ?? ListHeaderBg;
            btn.style.color = bgColor.HasValue ? Color.white : TextNormal;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopColor = PaneBorder;
            btn.style.borderBottomColor = PaneBorder;
            btn.style.borderLeftColor = PaneBorder;
            btn.style.borderRightColor = PaneBorder;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.fontSize = 12;
            btn.style.width = size;
            btn.style.minWidth = size;
            btn.style.maxWidth = size;
            btn.style.height = size;
            btn.style.minHeight = size;
            btn.style.maxHeight = size;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.alignItems = Align.Center;
            btn.style.justifyContent = Justify.Center;
        }

        private static void ApplyTrashButtonIcon(Button btn)
        {
            GUIContent iconContent = EditorGUIUtility.IconContent("TreeEditor.Trash");
            Texture icon = iconContent.image != null
                ? iconContent.image
                : EditorGUIUtility.IconContent("d_TreeEditor.Trash").image;

            if (icon == null)
            {
                btn.text = "🗑";
                return;
            }

            btn.text = string.Empty;
            btn.Clear();
            Image image = new()
            {
                image = icon
            };
            image.tintColor = TextNormal;
            image.style.width = 13;
            image.style.height = 13;
            image.style.alignSelf = Align.Center;
            image.style.flexShrink = 0;
            btn.Add(image);
        }

        private VisualElement CreateClipEditor(XAnimationCompiledClip clip)
        {
            XAnimationClipConfig config = clip.Config;
            VisualElement editor = new VisualElement();
            editor.style.marginLeft = 4;
            editor.style.marginRight = 4;
            editor.style.marginTop = 1;
            editor.style.marginBottom = 3;
            editor.style.paddingLeft = 6;
            editor.style.paddingRight = 6;
            editor.style.paddingTop = 4;
            editor.style.paddingBottom = 4;
            editor.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f);
            editor.style.borderTopWidth = 1;
            editor.style.borderBottomWidth = 1;
            editor.style.borderLeftWidth = 1;
            editor.style.borderRightWidth = 1;
            editor.style.borderTopColor = SectionDivider;
            editor.style.borderBottomColor = SectionDivider;
            editor.style.borderLeftColor = SectionDivider;
            editor.style.borderRightColor = SectionDivider;
            editor.style.borderTopLeftRadius = 3;
            editor.style.borderTopRightRadius = 3;
            editor.style.borderBottomLeftRadius = 3;
            editor.style.borderBottomRightRadius = 3;

            if (m_Session != null && m_Session.IsOverrideAsset)
            {
                string originalClipPath = m_Session.GetOriginalClipPath(clip.Key);
                ObjectField originalClipField = CreateClipObjectField(originalClipPath, label: "originalClip");
                originalClipField.tooltip = "Base XAnimation 中的原始动画资源。Override 预览中不允许从这里修改。";
                editor.Add(originalClipField);
            }

            Toggle loopField = new("loop") { value = config.loop };
            loopField.tooltip = "是否循环播放该 clip。会保存到 XAnimation 文件。";
            loopField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                m_Session.SetClipLoop(clip.Key, evt.newValue);
                RestartClipIfPlaying(clip.Key, config.defaultChannel);
                SetStatus($"{clip.Key} loop = {evt.newValue}。");
            });
            editor.Add(loopField);

            VisualElement fadeRow = new VisualElement();
            fadeRow.style.flexDirection = FlexDirection.Row;
            fadeRow.style.alignItems = Align.Center;

            FloatField fadeInField = new("defaultFadeIn") { value = config.defaultFadeIn };
            fadeInField.tooltip = "该 clip 默认淡入时间。会延迟保存到 XAnimation 文件。";
            fadeInField.style.flexGrow = 1;
            fadeInField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float fadeIn = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(fadeIn, evt.newValue))
                {
                    fadeInField.SetValueWithoutNotify(fadeIn);
                }

                m_Session.SetClipFade(clip.Key, fadeIn, config.defaultFadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{clip.Key} defaultFadeIn = {fadeIn:0.###}。");
            });
            fadeRow.Add(fadeInField);

            FloatField fadeOutField = new("defaultFadeOut") { value = config.defaultFadeOut };
            fadeOutField.tooltip = "该 clip 默认淡出时间。会延迟保存到 XAnimation 文件。";
            fadeOutField.style.flexGrow = 1;
            fadeOutField.style.marginLeft = 8;
            fadeOutField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float fadeOut = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(fadeOut, evt.newValue))
                {
                    fadeOutField.SetValueWithoutNotify(fadeOut);
                }

                m_Session.SetClipFade(clip.Key, config.defaultFadeIn, fadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{clip.Key} defaultFadeOut = {fadeOut:0.###}。");
            });
            fadeRow.Add(fadeOutField);
            editor.Add(fadeRow);

            List<string> rootMotionModeNames = new(Enum.GetNames(typeof(XAnimationClipRootMotionMode)));
            DropdownField rootMotionModeField = new(
                "rootMotionMode",
                rootMotionModeNames,
                Mathf.Max(0, rootMotionModeNames.IndexOf(config.rootMotionMode.ToString())));
            rootMotionModeField.tooltip = "该 clip 的 Root Motion 策略：继承 channel、强制开启或强制关闭。会保存到 XAnimation 文件。";
            rootMotionModeField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                if (!Enum.TryParse(evt.newValue, out XAnimationClipRootMotionMode mode))
                {
                    return;
                }

                m_Session.SetClipRootMotionMode(clip.Key, mode);
                RestartClipIfPlaying(clip.Key, config.defaultChannel);
                RefreshChannelStates();
                SetStatus($"{clip.Key} rootMotionMode = {mode}。");
            });
            editor.Add(rootMotionModeField);
            editor.Add(CreateClipCueEditor(clip.Key));

            return editor;
        }

        private bool RestartClipIfPlaying(string clipKey, string channelName)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            string playingChannelName = FindPlayingChannelName(clipKey);
            if (string.IsNullOrEmpty(playingChannelName))
            {
                return false;
            }

            m_Session.StopChannel(playingChannelName);
            m_Session.Play(clipKey, m_PlaySpeedField.value, channelName);
            RefreshClipPlayingStates();
            RefreshChannelStates();
            return true;
        }

        private string FindPlayingChannelName(string clipKey)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return null;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                string channelName = channels[i].Name;
                XAnimationChannelState state = m_Session.GetChannelState(channelName);
                if (state != null && string.Equals(state.clipKey, clipKey, StringComparison.Ordinal))
                {
                    return channelName;
                }
            }

            return null;
        }

        private static ObjectField CreateClipObjectField(string assetPath, float marginLeft = 0f, string label = null, bool editable = false)
        {
            AnimationClip clip = string.IsNullOrWhiteSpace(assetPath)
                ? null
                : XAnimationEditorAssetResolver.ResolveAnimationClip(assetPath);
            ObjectField field = string.IsNullOrWhiteSpace(label) ? new ObjectField() : new ObjectField(label);
            field.objectType = typeof(AnimationClip);
            field.allowSceneObjects = false;
            field.value = clip;
            field.tooltip = assetPath;
            field.style.flexGrow = 1;
            field.style.minHeight = 20;
            field.style.fontSize = 10;
            field.style.alignSelf = Align.Stretch;
            field.pickingMode = editable ? PickingMode.Position : PickingMode.Ignore;
            field.SetEnabled(editable);
            if (string.IsNullOrWhiteSpace(label))
            {
                field.style.flexBasis = 0;
                field.style.minWidth = 0;
            }
            if (marginLeft > 0f)
            {
                field.style.marginLeft = marginLeft;
            }

            if (!editable)
            {
                field.RegisterValueChangedCallback(evt => field.SetValueWithoutNotify(evt.previousValue));
            }

            return field;
        }

        private void ChangeClipPath(XAnimationCompiledClip clip, ObjectField field, AnimationClip previousClip, AnimationClip newClip)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                field.SetValueWithoutNotify(previousClip);
                return;
            }

            if (newClip == null)
            {
                field.SetValueWithoutNotify(previousClip);
                SetStatus("clip 动画资源不能为空。", true);
                return;
            }

            string clipPath = XAnimationEditorAssetResolver.BuildClipPath(newClip);
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                field.SetValueWithoutNotify(previousClip);
                SetStatus("无法获取所选 AnimationClip 的资源路径。", true);
                return;
            }

            try
            {
                m_Session.SetClipPath(clip.Key, clipPath);
                RestartClipIfPlaying(clip.Key, clip.Config.defaultChannel);
                SetStatus($"{clip.Key} clip = {newClip.name}。");
                RebuildClipList();
                RebuildChannelControls();
                RefreshClipPlayingStates();
                RefreshChannelStates();
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousClip);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ApplyClipButtonStyle(Button btn, bool isPlaying)
        {
            btn.text = isPlaying ? "■" : "▶";
            ApplyClipIconButtonStyle(btn, isPlaying ? DangerColor : null);
        }

        private void SetStopAllButtonEnabled(bool enabled)
        {
            if (m_StopAllButton == null)
            {
                return;
            }

            m_StopAllButton.SetEnabled(enabled);
            m_StopAllButton.style.opacity = enabled ? 1f : 0.45f;
        }

        private void SetAddChannelButtonEnabled(bool enabled)
        {
            if (m_AddChannelButton == null)
            {
                return;
            }

            m_AddChannelButton.SetEnabled(enabled);
            m_AddChannelButton.style.opacity = enabled ? 1f : 0.45f;
            m_AddChannelButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能新增 channel。"
                : "新增一个 channel。";
        }

        private void SetPauseButtonState(bool enabled, bool paused)
        {
            if (m_PauseButton == null)
            {
                return;
            }

            m_PauseButton.text = paused ? "继续" : "暂停";
            m_PauseButton.SetEnabled(enabled);
            m_PauseButton.style.opacity = enabled ? 1f : 0.45f;
        }

        private void RefreshClipPlayingStates()
        {
            if (m_ClipRowMap.Count == 0)
            {
                SetStopAllButtonEnabled(false);
                m_IsPaused = false;
                SetPauseButtonState(false, false);
                return;
            }

            // Collect currently playing clip keys
            HashSet<string> playingClipKeys = null;
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    XAnimationChannelState state = m_Session.GetChannelState(channels[i].Name);
                    if (state != null && !string.IsNullOrEmpty(state.clipKey))
                    {
                        playingClipKeys ??= new HashSet<string>(StringComparer.Ordinal);
                        playingClipKeys.Add(state.clipKey);
                    }
                }
            }

            SetStopAllButtonEnabled(playingClipKeys != null && playingClipKeys.Count > 0);
            bool hasPlayingClip = playingClipKeys != null && playingClipKeys.Count > 0;
            if (!hasPlayingClip)
            {
                m_IsPaused = false;
            }
            SetPauseButtonState(hasPlayingClip, m_IsPaused);

            foreach (KeyValuePair<string, VisualElement> kvp in m_ClipRowMap)
            {
                bool isPlaying = playingClipKeys != null && playingClipKeys.Contains(kvp.Key);
                if (m_ClipVisualStateMap.TryGetValue(kvp.Key, out ClipRowVisualState visualState))
                {
                    visualState.Playing = isPlaying;
                    ApplyClipRowVisualState(kvp.Key);
                }
                else
                {
                    kvp.Value.style.backgroundColor = isPlaying ? PlayingBg : Color.clear;
                }

                if (kvp.Value.childCount > 0 && kvp.Value[0] is Label lbl)
                {
                    lbl.style.color = isPlaying ? Color.white : TextNormal;
                }
                if (m_ClipButtonMap.TryGetValue(kvp.Key, out Button btn))
                {
                    ApplyClipButtonStyle(btn, isPlaying);
                }
            }
        }

        private void ApplyClipRowVisualState(string clipKey)
        {
            if (!m_ClipRowMap.TryGetValue(clipKey, out VisualElement row) ||
                !m_ClipVisualStateMap.TryGetValue(clipKey, out ClipRowVisualState visualState))
            {
                return;
            }

            row.style.backgroundColor = visualState.Playing
                ? PlayingBg
                : visualState.Hovered
                    ? HoverBg
                    : visualState.BaseColor;
        }

        private void RebuildChannelControls()
        {
            m_ChannelControlsContainer.Clear();
            m_ChannelLabelMap.Clear();
            m_ChannelStateLabels.Clear();
            SetAddChannelButtonEnabled(m_Session != null && m_Session.IsLoaded && !m_Session.IsOverrideAsset);

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = (XAnimationCompiledChannel)channels[i];

                VisualElement controlRow = new VisualElement();
                controlRow.style.flexDirection = FlexDirection.Column;
                controlRow.style.marginBottom = 3;
                controlRow.style.paddingLeft = 3;
                controlRow.style.paddingRight = 3;
                controlRow.style.paddingTop = 3;
                controlRow.style.paddingBottom = 3;
                controlRow.style.borderTopWidth = 1;
                controlRow.style.borderBottomWidth = 1;
                controlRow.style.borderLeftWidth = 1;
                controlRow.style.borderRightWidth = 1;
                controlRow.style.borderTopColor = SectionDivider;
                controlRow.style.borderBottomColor = SectionDivider;
                controlRow.style.borderLeftColor = SectionDivider;
                controlRow.style.borderRightColor = SectionDivider;
                controlRow.style.borderTopLeftRadius = 3;
                controlRow.style.borderTopRightRadius = 3;
                controlRow.style.borderBottomLeftRadius = 3;
                controlRow.style.borderBottomRightRadius = 3;
                controlRow.style.backgroundColor = i % 2 == 0 ? ListRowEvenBg : ListRowOddBg;

                VisualElement channelHeader = new VisualElement();
                channelHeader.style.flexDirection = FlexDirection.Row;
                channelHeader.style.alignItems = Align.Center;
                channelHeader.tooltip = "单击 channel 名称展开/收起配置和预览调试信息；右键 Rename 编辑名称。";

                Label channelFoldoutLabel = new("▾");
                channelFoldoutLabel.style.width = 14;
                channelFoldoutLabel.style.flexShrink = 0;
                channelFoldoutLabel.style.color = TextMuted;
                channelFoldoutLabel.style.fontSize = BodyFontSize;
                channelHeader.Add(channelFoldoutLabel);

                EditableLabel channelLabel = new(channel.Name);
                ConfigureEditableNameLabel(channelLabel, 160f);
                channelLabel.tooltip = "单击展开/收起这个 channel 的配置和预览调试信息；右键 Rename 编辑名称。";
                channelLabel.SetEditable(true, EditableLabelEditTrigger.ContextMenu);
                channelLabel.EditStarted += BeginNameEdit;
                channelLabel.EditEnded += EndNameEdit;
                channelLabel.ValueCommitted += (_, newValue) => RenameChannel(channel.Name, newValue, channelLabel);
                m_ChannelLabelMap[channel.Name] = channelLabel;
                channelHeader.Add(channelLabel);

                VisualElement channelHeaderSpacer = new();
                channelHeaderSpacer.style.flexGrow = 1;
                channelHeader.Add(channelHeaderSpacer);

                Button deleteChannelButton = new(() => DeleteChannel(channel.Name))
                {
                    text = "⌫"
                };
                deleteChannelButton.tooltip = m_Session.IsOverrideAsset
                    ? "Override 资源不能删除 channel。"
                    : "删除这个 channel，并在确认后连带删除其下 clip。";
                deleteChannelButton.SetEnabled(!m_Session.IsOverrideAsset);
                ApplyTrashButtonIcon(deleteChannelButton);
                ApplyClipIconButtonStyle(deleteChannelButton);
                deleteChannelButton.style.marginLeft = 4;
                channelHeader.Add(deleteChannelButton);
                controlRow.Add(channelHeader);

                VisualElement channelContent = new VisualElement();
                VisualElement configBox = CreateSubBox();
                configBox.Add(CreateChannelConfigEditor(channel));
                channelContent.Add(configBox);
                VisualElement debugBox = CreateSubBox();

                VisualElement timeScaleRow = new VisualElement();
                timeScaleRow.style.flexDirection = FlexDirection.Row;
                timeScaleRow.style.alignItems = Align.Center;
                timeScaleRow.style.flexWrap = Wrap.NoWrap;

                FloatField timeScaleField = new()
                {
                    value = 1f
                };
                timeScaleField.tooltip = "当前 channel 的预览时间缩放，只影响当前预览，不写入配置。";
                ConfigureCompactNumberField(timeScaleField);
                timeScaleField.RegisterValueChangedCallback(evt =>
                {
                    float timeScale = Mathf.Max(0f, evt.newValue);
                    if (!Mathf.Approximately(timeScale, evt.newValue))
                    {
                        timeScaleField.SetValueWithoutNotify(timeScale);
                    }

                    m_Session.SetChannelTimeScale(channel.Name, timeScale);
                });
                timeScaleRow.Add(CreateChannelControlLabel("TimeScale"));
                timeScaleRow.Add(timeScaleField);
                debugBox.Add(timeScaleRow);

                Label stateLabel = new(BuildChannelStateText(channel, null));
                stateLabel.tooltip = stateLabel.text;
                stateLabel.style.whiteSpace = WhiteSpace.Normal;
                stateLabel.style.fontSize = 11;
                stateLabel.style.color = TextMuted;
                stateLabel.style.marginBottom = 4;
                stateLabel.style.height = ChannelStateLabelHeight;
                stateLabel.style.minHeight = ChannelStateLabelHeight;
                stateLabel.style.maxHeight = ChannelStateLabelHeight;
                stateLabel.style.overflow = Overflow.Hidden;
                stateLabel.style.paddingLeft = 3;
                stateLabel.style.paddingRight = 3;
                stateLabel.style.paddingTop = 2;
                stateLabel.style.paddingBottom = 2;
                stateLabel.style.backgroundColor = ListHeaderBg;
                debugBox.Add(stateLabel);
                m_ChannelStateLabels[channel.Name] = stateLabel;
                channelContent.Add(debugBox);

                channelLabel.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    if (channelLabel.IsEditing)
                    {
                        ClearClipDragData();
                        return;
                    }

                    bool expanded = channelContent.style.display != DisplayStyle.None;
                    channelContent.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                    channelFoldoutLabel.text = expanded ? "▸" : "▾";
                    evt.StopPropagation();
                });

                controlRow.Add(channelContent);

                m_ChannelControlsContainer.Add(controlRow);
            }

            TryBeginPendingRename();
        }

        private static VisualElement CreateSubBox()
        {
            VisualElement box = new VisualElement();
            box.style.marginTop = 3;
            box.style.paddingLeft = 4;
            box.style.paddingRight = 4;
            box.style.paddingTop = 4;
            box.style.paddingBottom = 4;
            box.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f, 1f);
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopColor = SectionDivider;
            box.style.borderBottomColor = SectionDivider;
            box.style.borderLeftColor = SectionDivider;
            box.style.borderRightColor = SectionDivider;
            box.style.borderTopLeftRadius = 3;
            box.style.borderTopRightRadius = 3;
            box.style.borderBottomLeftRadius = 3;
            box.style.borderBottomRightRadius = 3;
            return box;
        }

        private static Label CreateChannelControlLabel(string text)
        {
            Label label = new(text);
            label.style.width = 64;
            label.style.minWidth = 64;
            label.style.maxWidth = 64;
            label.style.flexShrink = 0;
            label.style.fontSize = 10;
            label.style.color = TextMuted;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        private static void ConfigureCompactNumberField(FloatField field)
        {
            field.style.width = 64;
            field.style.minWidth = 64;
            field.style.maxWidth = 64;
            field.style.flexShrink = 0;
            field.style.alignSelf = Align.Center;
        }

        private VisualElement CreateChannelConfigEditor(XAnimationCompiledChannel channel)
        {
            XAnimationChannelConfig config = channel.Config;
            VisualElement editor = new VisualElement();
            editor.style.marginTop = 2;
            editor.style.marginBottom = 3;

            VisualElement toggleRow = new VisualElement();
            toggleRow.style.flexDirection = FlexDirection.Column;
            toggleRow.style.alignItems = Align.Stretch;
            toggleRow.style.marginTop = 2;

            Toggle interruptField = new("allowInterrupt") { value = config.allowInterrupt };
            interruptField.tooltip = "当前 channel 上的播放是否允许被新的播放请求打断。会保存到 XAnimation 文件。";
            interruptField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                m_Session.SetChannelAllowInterrupt(channel.Name, evt.newValue);
                SetStatus($"{channel.Name} allowInterrupt = {evt.newValue}。");
            });
            toggleRow.Add(interruptField);

            Toggle rootMotionField = new("rootMotion") { value = config.canDriveRootMotion };
            rootMotionField.tooltip = "该 channel 是否允许驱动 Root Motion。Additive channel 不能开启。会保存到 XAnimation 文件。";
            rootMotionField.SetEnabled(config.layerType != XAnimationChannelLayerType.Additive);
            rootMotionField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;
                if (evt.newValue && config.layerType == XAnimationChannelLayerType.Additive)
                {
                    rootMotionField.SetValueWithoutNotify(false);
                    SetStatus($"{channel.Name} Additive channel 不能开启 rootMotion。", true);
                    return;
                }

                m_Session.SetChannelCanDriveRootMotion(channel.Name, evt.newValue);
                RefreshChannelStates();
                SetStatus($"{channel.Name} canDriveRootMotion = {evt.newValue}。");
            });
            toggleRow.Add(rootMotionField);

            List<string> layerTypeNames = new(Enum.GetNames(typeof(XAnimationChannelLayerType)));
            DropdownField layerTypeField = new(
                "layerType",
                layerTypeNames,
                Mathf.Max(0, layerTypeNames.IndexOf(config.layerType.ToString())));
            layerTypeField.tooltip = "channel 混合层类型。Additive 会自动关闭 rootMotion。会保存到 XAnimation 文件。";
            layerTypeField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;
                if (!Enum.TryParse(evt.newValue, out XAnimationChannelLayerType layerType)) return;

                m_Session.SetChannelLayerType(channel.Name, layerType);
                bool canEditRootMotion = layerType != XAnimationChannelLayerType.Additive;
                rootMotionField.SetEnabled(canEditRootMotion);
                if (!canEditRootMotion)
                {
                    rootMotionField.SetValueWithoutNotify(false);
                }

                RefreshChannelStates();
                SetStatus($"{channel.Name} layerType = {layerType}。");
            });
            editor.Add(layerTypeField);

            ObjectField maskField = new("mask")
            {
                objectType = typeof(AvatarMask),
                allowSceneObjects = false,
                value = string.IsNullOrWhiteSpace(config.maskPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<AvatarMask>(config.maskPath)
            };
            maskField.tooltip = "该 channel 使用的 AvatarMask。为空表示不使用 mask。会保存到 XAnimation 文件。";
            maskField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                string maskPath = evt.newValue == null ? string.Empty : AssetDatabase.GetAssetPath(evt.newValue);
                m_Session.SetChannelMaskPath(channel.Name, maskPath);
                SetStatus(string.IsNullOrEmpty(maskPath) ? $"{channel.Name} mask = None。" : $"{channel.Name} mask = {Path.GetFileNameWithoutExtension(maskPath)}。");
            });
            editor.Add(maskField);

            FloatField defaultWeightField = new("defaultWeight") { value = config.defaultWeight };
            defaultWeightField.tooltip = "该 channel 默认混合权重。会延迟保存到 XAnimation 文件，并立即影响当前预览。";
            defaultWeightField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float defaultWeight = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(defaultWeight, evt.newValue))
                {
                    defaultWeightField.SetValueWithoutNotify(defaultWeight);
                }

                m_Session.SetChannelDefaultWeight(channel.Name, defaultWeight, save: false);
                ScheduleAssetSave();
                SetStatus($"{channel.Name} defaultWeight = {defaultWeight:0.###}。");
            });
            editor.Add(defaultWeightField);
            editor.Add(toggleRow);

            VisualElement fadeRow = new VisualElement();
            fadeRow.style.flexDirection = FlexDirection.Column;
            fadeRow.style.alignItems = Align.Stretch;
            fadeRow.style.marginTop = 2;

            FloatField fadeInField = new("defaultFadeIn") { value = config.defaultFadeIn };
            fadeInField.tooltip = "该 channel 默认淡入时间。会延迟保存到 XAnimation 文件。";
            fadeInField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float fadeIn = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(fadeIn, evt.newValue))
                {
                    fadeInField.SetValueWithoutNotify(fadeIn);
                }

                m_Session.SetChannelFade(channel.Name, fadeIn, config.defaultFadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{channel.Name} defaultFadeIn = {fadeIn:0.###}。");
            });
            fadeRow.Add(fadeInField);

            FloatField fadeOutField = new("defaultFadeOut") { value = config.defaultFadeOut };
            fadeOutField.tooltip = "该 channel 默认淡出时间。会延迟保存到 XAnimation 文件。";
            fadeOutField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float fadeOut = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(fadeOut, evt.newValue))
                {
                    fadeOutField.SetValueWithoutNotify(fadeOut);
                }

                m_Session.SetChannelFade(channel.Name, config.defaultFadeIn, fadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{channel.Name} defaultFadeOut = {fadeOut:0.###}。");
            });
            fadeRow.Add(fadeOutField);
            editor.Add(fadeRow);

            return editor;
        }

        private void RefreshChannelStates()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                foreach (Label label in m_ChannelStateLabels.Values)
                {
                    label.text = "idle";
                    label.tooltip = label.text;
                    label.style.color = TextMuted;
                }
                return;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = (XAnimationCompiledChannel)channels[i];
                XAnimationChannelState state = m_Session.GetChannelState(channel.Name);
                if (!m_ChannelStateLabels.TryGetValue(channel.Name, out Label label))
                {
                    continue;
                }

                label.text = BuildChannelStateText(channel, state);
                label.tooltip = label.text;
                label.style.color = state == null ? TextMuted : TextNormal;
            }
        }

        private static string BuildChannelStateText(XAnimationCompiledChannel channel, XAnimationChannelState state)
        {
            if (state == null)
            {
                return $"State: idle | Channel: {channel.Name}";
            }

            string loop = state.isLooping ? "Loop" : "Once";
            string fade = state.isFading ? "Fading" : "Stable";
            string interrupt = state.interruptible ? "Interruptible" : "Locked";
            return $"State: {state.clipKey} | PlayId: {state.playbackId} | {loop} | {fade} | {interrupt}\n"
                + $"Time: {state.normalizedTime:0.000} / Total: {state.totalNormalizedTime:0.000} | Channel Weight: {state.channelWeight:0.000} | Clip Weight: {state.weight:0.000}\n"
                + $"TimeScale: {state.timeScale:0.000} | Effective Speed: {state.speed:0.000} | Priority: {state.priority}";
        }

        private void RefreshCueLogView(bool force = false)
        {
            if (m_CueLogContainer == null)
            {
                return;
            }

            int cueCount = m_Session?.CueLogs.Count ?? 0;
            if (!force && cueCount == m_LastCueLogCount)
            {
                return;
            }

            m_LastCueLogCount = cueCount;
            m_CueLogContainer.Clear();

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<string> cueLogs = m_Session.CueLogs;
            for (int i = 0; i < cueLogs.Count; i++)
            {
                Label label = new(cueLogs[i]);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginBottom = 1;
                label.style.paddingLeft = 4;
                label.style.paddingRight = 4;
                label.style.paddingTop = 2;
                label.style.paddingBottom = 2;
                label.style.fontSize = 11;
                label.style.color = TextNormal;
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                if (i % 2 == 1)
                {
                    label.style.backgroundColor = AltRowBg;
                    label.style.borderTopLeftRadius = 2;
                    label.style.borderTopRightRadius = 2;
                    label.style.borderBottomLeftRadius = 2;
                    label.style.borderBottomRightRadius = 2;
                }
                m_CueLogContainer.Add(label);
            }
        }

        private void ClearDebugViews()
        {
            m_ClipListView?.Clear();
            m_ExpandedClipKeys.Clear();
            m_ClipLabelMap.Clear();
            m_ClipRowMap.Clear();
            m_ClipVisualStateMap.Clear();
            m_ClipButtonMap.Clear();
            m_ClipChannelMap.Clear();
            m_ChannelControlsContainer?.Clear();
            m_ChannelLabelMap.Clear();
            m_CueLogContainer?.Clear();
            m_ChannelStateLabels.Clear();
            m_PendingClipRenameKey = null;
            m_PendingChannelRenameKey = null;
            m_PreviewImage.image = null;
            m_LastCueLogCount = -1;
            m_IsPaused = false;
            SetPauseButtonState(false, false);
            SetStopAllButtonEnabled(false);
            SetAddChannelButtonEnabled(false);
        }

        private void DisposeSession()
        {
            if (m_Session == null)
            {
                return;
            }

            m_Session.Dispose();
            m_Session = null;
            m_PreviewImage.image = null;
            m_LastCueLogCount = -1;
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            m_StatusLabel.text = message;
            m_StatusLabel.style.color = isError ? new Color(0.95f, 0.40f, 0.40f) : TextNormal;
        }

        private static VisualElement CreatePane(float width = 0f)
        {
            VisualElement pane = new VisualElement();
            if (width > 0f)
            {
                pane.style.width = width;
                pane.style.flexShrink = 0;
            }
            else
            {
                pane.style.flexGrow = 1;
            }

            pane.style.flexDirection = FlexDirection.Column;
            pane.style.paddingLeft = 4;
            pane.style.paddingRight = 4;
            pane.style.paddingTop = 4;
            pane.style.paddingBottom = 4;
            pane.style.backgroundColor = PaneBg;
            pane.style.borderTopLeftRadius = 6;
            pane.style.borderTopRightRadius = 6;
            pane.style.borderBottomLeftRadius = 6;
            pane.style.borderBottomRightRadius = 6;
            pane.style.borderTopWidth = 1;
            pane.style.borderBottomWidth = 1;
            pane.style.borderLeftWidth = 1;
            pane.style.borderRightWidth = 1;
            pane.style.borderTopColor = PaneBorder;
            pane.style.borderBottomColor = PaneBorder;
            pane.style.borderLeftColor = PaneBorder;
            pane.style.borderRightColor = PaneBorder;
            return pane;
        }

    }
}
#endif
