#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;
using XFramework.Resource;
using XFramework.UI;

namespace XFramework.Editor
{
    public sealed class XAnimationPreviewWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/XAnimation Preview";
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
        private const string StateDragDataKey = nameof(XAnimationPreviewWindow) + ".StateKey";

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
        [SerializeField] private bool m_PlayTargetSectionExpanded = true;
        [SerializeField] private bool m_PlayTransitionSectionExpanded;
        [SerializeField] private bool m_PlayPlaybackSectionExpanded;
        [SerializeField] private bool m_ParametersSectionExpanded = true;
        [SerializeField] private bool m_StatesSectionExpanded = true;
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
        private FloatField m_PlayFadeInField;
        private FloatField m_PlayFadeOutField;
        private FloatField m_PlayWeightField;
        private FloatField m_PlayNormalizedTimeField;
        private IntegerField m_PlayPriorityField;
        private Toggle m_ApplyTransitionRequestToggle;
        private Toggle m_ApplyPlaybackOverrideToggle;
        private Toggle m_PlayInterruptibleToggle;
        private Toggle m_PlayOverrideLoopToggle;
        private Toggle m_PlayLoopValueToggle;
        private Toggle m_PlayOverrideRootMotionToggle;
        private Toggle m_PlayRootMotionValueToggle;
        private Toggle m_GridToggle;
        private DropdownField m_PlayTargetChannelField;
        private Button m_PauseButton;
        private Button m_StopAllButton;
        private Button m_AddClipButton;
        private Button m_AddChannelButton;
        private Button m_AddParameterButton;
        private VisualElement m_ParameterListView;
        private VisualElement m_StateListView;
        private VisualElement m_ClipListView;
        private readonly HashSet<string> m_ExpandedStateKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_StateLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_StateRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_StateButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> m_StateChannelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ParameterLabelMap = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_ExpandedClipKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ClipLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ClipRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ClipRowVisualState> m_ClipVisualStateMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_ClipButtonMap = new(StringComparer.Ordinal);
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
        private string m_PendingStateRenameKey;
        private string m_PendingParameterRenameKey;
        private string m_PendingChannelRenameKey;
        private string m_PlayTargetChannelName;
        private float m_PlayFadeInOverride;
        private float m_PlayFadeOutOverride;
        private float m_PlayWeightOverride = 1f;
        private float m_PlayNormalizedTimeOverride;
        private int m_PlayPriorityOverride;
        private bool m_PlayInterruptibleOverride = true;
        private bool m_ApplyTransitionRequestOverrides;
        private bool m_ApplyPlaybackOverrides;
        private float m_PlaySpeed = 1f;
        private bool m_PlayUseLoopOverride;
        private bool m_PlayLoopOverride = true;
        private bool m_PlayUseRootMotionOverride;
        private bool m_PlayRootMotionOverride;
        private bool m_PlaybackPrefsLoaded;
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
            XAnimationPreviewWindow window = GetOpenWindow() ?? CreateDockedWindow();
            window.titleContent = new GUIContent("XAnimation Preview");
            window.minSize = new Vector2(1180f, 680f);
            window.Show();
        }

        public static XAnimationPreviewWindow ShowWindow(TextAsset animationAsset, GameObject prefab = null, bool autoLoad = true)
        {
            XAnimationPreviewWindow window = GetOpenWindow() ?? CreateDockedWindow();
            window.titleContent = new GUIContent("XAnimation Preview");
            window.minSize = new Vector2(1180f, 680f);
            window.SetPendingOpenRequest(animationAsset, prefab, autoLoad);
            window.Show();
            window.Focus();
            return window;
        }

        private static XAnimationPreviewWindow GetOpenWindow()
        {
            XAnimationPreviewWindow[] windows = Resources.FindObjectsOfTypeAll<XAnimationPreviewWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null)
                {
                    return windows[i];
                }
            }

            return null;
        }

        private static XAnimationPreviewWindow CreateDockedWindow()
        {
            Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            return gameViewType != null
                ? CreateWindow<XAnimationPreviewWindow>("XAnimation Preview", typeof(SceneView), gameViewType)
                : CreateWindow<XAnimationPreviewWindow>("XAnimation Preview", typeof(SceneView));
        }

        internal GameObject CurrentSelectedPrefab => m_PrefabField?.value as GameObject;

        private void OnEnable()
        {
            LoadPlaybackPrefs();
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
            SetStatus("拖入 prefab 和 .xasset，或打开已配置默认 prefab 的 XAnimationAsset。");
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

        private void LoadPlaybackPrefs()
        {
            XAnimationPlaybackSettings settings = XAnimationPlaybackSettingsPrefs.Load();
            m_PlaybackSectionExpanded = settings.PlaybackSectionExpanded;
            m_PlayTargetSectionExpanded = settings.TargetSectionExpanded;
            m_PlayTransitionSectionExpanded = settings.TransitionSectionExpanded;
            m_PlayPlaybackSectionExpanded = settings.PlaybackOptionsSectionExpanded;
            m_PlayTargetChannelName = settings.ChannelName;
            m_PlaySpeed = Mathf.Approximately(settings.Speed, 0f) ? 1f : settings.Speed;
            m_ApplyTransitionRequestOverrides = settings.ApplyTransition;
            m_PlayFadeInOverride = Mathf.Max(0f, settings.FadeIn);
            m_PlayFadeOutOverride = Mathf.Max(0f, settings.FadeOut);
            m_PlayPriorityOverride = settings.Priority;
            m_PlayInterruptibleOverride = settings.Interruptible;
            m_ApplyPlaybackOverrides = settings.ApplyPlayback;
            m_PlayWeightOverride = settings.Weight;
            m_PlayNormalizedTimeOverride = Mathf.Clamp01(settings.NormalizedTime);
            m_PlayUseLoopOverride = settings.UseLoopOverride;
            m_PlayLoopOverride = settings.LoopOverride;
            m_PlayUseRootMotionOverride = settings.UseRootMotionOverride;
            m_PlayRootMotionOverride = settings.RootMotionOverride;
            m_PlaybackPrefsLoaded = true;
        }

        private float GetPlaybackSpeed()
        {
            return Mathf.Approximately(m_PlaySpeed, 0f) ? 1f : m_PlaySpeed;
        }

        private void SavePlaybackPrefs()
        {
            if (!m_PlaybackPrefsLoaded)
            {
                return;
            }

            float speed = m_PlaySpeedField?.value ?? m_PlaySpeed;
            m_PlaySpeed = Mathf.Approximately(speed, 0f) ? 1f : speed;

            XAnimationPlaybackSettingsPrefs.Save(new XAnimationPlaybackSettings
            {
                PlaybackSectionExpanded = m_PlaybackSectionExpanded,
                TargetSectionExpanded = m_PlayTargetSectionExpanded,
                TransitionSectionExpanded = m_PlayTransitionSectionExpanded,
                PlaybackOptionsSectionExpanded = m_PlayPlaybackSectionExpanded,
                ChannelName = m_PlayTargetChannelName,
                Speed = m_PlaySpeed,
                ApplyTransition = m_ApplyTransitionRequestOverrides,
                FadeIn = m_PlayFadeInOverride,
                FadeOut = m_PlayFadeOutOverride,
                Priority = m_PlayPriorityOverride,
                Interruptible = m_PlayInterruptibleOverride,
                ApplyPlayback = m_ApplyPlaybackOverrides,
                Weight = m_PlayWeightOverride,
                NormalizedTime = m_PlayNormalizedTimeOverride,
                UseLoopOverride = m_PlayUseLoopOverride,
                LoopOverride = m_PlayLoopOverride,
                UseRootMotionOverride = m_PlayUseRootMotionOverride,
                RootMotionOverride = m_PlayRootMotionOverride,
            });
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
                "设为默认" => "用当前 Prefab 覆盖 XAnimationAsset 的 DefaultPrefabPath。",
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

            VisualElement prefabRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            m_PrefabField.style.flexGrow = 1;
            m_PrefabField.style.marginBottom = 0;
            prefabRow.Add(m_PrefabField);
            prefabRow.Add(CreateStyledButton("设为默认", SaveCurrentPrefabAsDefault, AccentColor, 6f));
            assetsCard.Content.Add(prefabRow);

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
            FoldoutCard playbackCard = CreateFoldoutCard("播放设置", m_PlaybackSectionExpanded, value =>
            {
                m_PlaybackSectionExpanded = value;
                SavePlaybackPrefs();
            });

            VisualElement speedRow = new VisualElement();
            speedRow.style.flexDirection = FlexDirection.Row;
            speedRow.style.alignItems = Align.Center;

            m_PlaySpeedField = new FloatField("speed") { value = m_PlaybackPrefsLoaded ? GetPlaybackSpeed() : 1f };
            m_PlaySpeedField.tooltip = "request.speed。0 会按 1 处理，只影响当前预览请求。";
            m_PlaySpeedField.style.flexGrow = 1;
            m_PlaySpeedField.RegisterValueChangedCallback(_ => SavePlaybackPrefs());
            speedRow.Add(m_PlaySpeedField);

            m_PauseButton = CreateStyledButton("暂停", TogglePause, AccentColor, 8f);
            SetPauseButtonState(false, false);
            speedRow.Add(m_PauseButton);

            m_StopAllButton = CreateStyledButton("停止全部", StopAllClips, DangerColor, 8f);
            SetStopAllButtonEnabled(false);
            speedRow.Add(m_StopAllButton);

            playbackCard.Content.Add(speedRow);

            FoldoutCard targetCard = CreateSectionFoldoutCard("Targte", m_PlayTargetSectionExpanded, value =>
            {
                m_PlayTargetSectionExpanded = value;
                SavePlaybackPrefs();
            });
            targetCard.Root.style.marginTop = 4;

            m_PlayTargetChannelField = CreateChannelDropdown("channelName", m_PlayTargetChannelName);
            m_PlayTargetChannelField.tooltip = "播放 target.channelName。用于 clip 调试播放，也可覆盖 state 的默认 channel。";
            m_PlayTargetChannelField.RegisterValueChangedCallback(evt =>
            {
                m_PlayTargetChannelName = evt.newValue ?? string.Empty;
                SavePlaybackPrefs();
            });
            targetCard.Content.Add(m_PlayTargetChannelField);

            playbackCard.Content.Add(targetCard.Root);

            FoldoutCard transitionCard = CreateSectionFoldoutCard("Transition", m_PlayTransitionSectionExpanded, value =>
            {
                m_PlayTransitionSectionExpanded = value;
                SavePlaybackPrefs();
            });
            transitionCard.Root.style.marginTop = 4;

            m_ApplyTransitionRequestToggle = new Toggle("Apply Transition Request") { value = m_ApplyTransitionRequestOverrides };
            m_ApplyTransitionRequestToggle.tooltip = "只应用 command.transition.fadeIn / fadeOut / priority / interruptible。";
            m_ApplyTransitionRequestToggle.style.marginBottom = 3;
            m_ApplyTransitionRequestToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyTransitionRequestOverrides = evt.newValue;
                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(m_ApplyTransitionRequestToggle);

            m_PlayFadeInField = new FloatField { value = m_PlayFadeInOverride };
            m_PlayFadeInField.tooltip = "0 表示使用 state/channel 默认值；大于 0 时写入 request.fadeIn。";
            ConfigureCompactPlaybackField(m_PlayFadeInField, "fadeIn", 66);
            m_PlayFadeInField.RegisterValueChangedCallback(evt =>
            {
                m_PlayFadeInOverride = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(m_PlayFadeInOverride, evt.newValue))
                {
                    m_PlayFadeInField.SetValueWithoutNotify(m_PlayFadeInOverride);
                }

                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackFieldContainer("fadeIn", m_PlayFadeInField, 66));

            m_PlayFadeOutField = new FloatField { value = m_PlayFadeOutOverride };
            m_PlayFadeOutField.tooltip = "0 表示使用 state/channel 默认值；大于 0 时写入 request.fadeOut。";
            ConfigureCompactPlaybackField(m_PlayFadeOutField, "fadeOut", 66);
            m_PlayFadeOutField.RegisterValueChangedCallback(evt =>
            {
                m_PlayFadeOutOverride = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(m_PlayFadeOutOverride, evt.newValue))
                {
                    m_PlayFadeOutField.SetValueWithoutNotify(m_PlayFadeOutOverride);
                }

                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackFieldContainer("fadeOut", m_PlayFadeOutField, 66));

            m_PlayPriorityField = new IntegerField { value = m_PlayPriorityOverride };
            m_PlayPriorityField.tooltip = "request.priority。";
            ConfigureCompactPlaybackElement(m_PlayPriorityField, 66);
            m_PlayPriorityField.RegisterValueChangedCallback(evt =>
            {
                m_PlayPriorityOverride = evt.newValue;
                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackFieldContainer("priority", m_PlayPriorityField, 66));

            m_PlayInterruptibleToggle = new Toggle("interruptible") { value = m_PlayInterruptibleOverride };
            m_PlayInterruptibleToggle.tooltip = "request.interruptible。";
            m_PlayInterruptibleToggle.RegisterValueChangedCallback(evt =>
            {
                m_PlayInterruptibleOverride = evt.newValue;
                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(m_PlayInterruptibleToggle);

            playbackCard.Content.Add(transitionCard.Root);

            FoldoutCard playbackOptionsCard = CreateSectionFoldoutCard("PlayBack", m_PlayPlaybackSectionExpanded, value =>
            {
                m_PlayPlaybackSectionExpanded = value;
                SavePlaybackPrefs();
            });
            playbackOptionsCard.Root.style.marginTop = 4;

            m_ApplyPlaybackOverrideToggle = new Toggle("Apply Playback Override") { value = m_ApplyPlaybackOverrides };
            m_ApplyPlaybackOverrideToggle.tooltip = "应用 command.playback.weight / normalizedTime / loopOverride / rootMotionOverride。";
            m_ApplyPlaybackOverrideToggle.style.marginBottom = 3;
            m_ApplyPlaybackOverrideToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyPlaybackOverrides = evt.newValue;
                SavePlaybackPrefs();
            });
            playbackOptionsCard.Content.Add(m_ApplyPlaybackOverrideToggle);

            m_PlayWeightField = new FloatField { value = m_PlayWeightOverride };
            m_PlayWeightField.tooltip = "request.weight。小于等于 0 会被运行时按默认 1 处理。";
            ConfigureCompactPlaybackField(m_PlayWeightField, "weight", 66);
            m_PlayWeightField.RegisterValueChangedCallback(evt =>
            {
                m_PlayWeightOverride = evt.newValue;
                SavePlaybackPrefs();
            });
            playbackOptionsCard.Content.Add(CreatePlaybackFieldContainer("weight", m_PlayWeightField, 66));

            m_PlayNormalizedTimeField = new FloatField { value = m_PlayNormalizedTimeOverride };
            m_PlayNormalizedTimeField.tooltip = "request.normalizedTime，会被夹到 [0, 1]。";
            ConfigureCompactPlaybackField(m_PlayNormalizedTimeField, "normalized", 72);
            m_PlayNormalizedTimeField.RegisterValueChangedCallback(evt =>
            {
                m_PlayNormalizedTimeOverride = Mathf.Clamp01(evt.newValue);
                if (!Mathf.Approximately(m_PlayNormalizedTimeOverride, evt.newValue))
                {
                    m_PlayNormalizedTimeField.SetValueWithoutNotify(m_PlayNormalizedTimeOverride);
                }

                SavePlaybackPrefs();
            });
            playbackOptionsCard.Content.Add(CreatePlaybackFieldContainer("normalized", m_PlayNormalizedTimeField, 72));

            m_PlayOverrideLoopToggle = new Toggle("overrideLoop") { value = m_PlayUseLoopOverride };
            m_PlayOverrideLoopToggle.tooltip = "开启后写入 request.loopOverride。";
            m_PlayOverrideLoopToggle.RegisterValueChangedCallback(evt =>
            {
                m_PlayUseLoopOverride = evt.newValue;
                m_PlayLoopValueToggle.SetEnabled(evt.newValue);
                SavePlaybackPrefs();
            });
            playbackOptionsCard.Content.Add(m_PlayOverrideLoopToggle);

            m_PlayLoopValueToggle = new Toggle("loopValue") { value = m_PlayLoopOverride };
            m_PlayLoopValueToggle.tooltip = "request.loopOverride 的值。";
            m_PlayLoopValueToggle.SetEnabled(m_PlayUseLoopOverride);
            m_PlayLoopValueToggle.RegisterValueChangedCallback(evt =>
            {
                m_PlayLoopOverride = evt.newValue;
                SavePlaybackPrefs();
            });
            playbackOptionsCard.Content.Add(m_PlayLoopValueToggle);

            m_PlayOverrideRootMotionToggle = new Toggle("overrideRootMotion") { value = m_PlayUseRootMotionOverride };
            m_PlayOverrideRootMotionToggle.tooltip = "开启后写入 request.rootMotionOverride。";
            m_PlayOverrideRootMotionToggle.RegisterValueChangedCallback(evt =>
            {
                m_PlayUseRootMotionOverride = evt.newValue;
                m_PlayRootMotionValueToggle.SetEnabled(evt.newValue);
                SavePlaybackPrefs();
            });
            playbackOptionsCard.Content.Add(m_PlayOverrideRootMotionToggle);

            m_PlayRootMotionValueToggle = new Toggle("rootMotionValue") { value = m_PlayRootMotionOverride };
            m_PlayRootMotionValueToggle.tooltip = "request.rootMotionOverride 的值。";
            m_PlayRootMotionValueToggle.SetEnabled(m_PlayUseRootMotionOverride);
            m_PlayRootMotionValueToggle.RegisterValueChangedCallback(evt =>
            {
                m_PlayRootMotionOverride = evt.newValue;
                SavePlaybackPrefs();
            });
            playbackOptionsCard.Content.Add(m_PlayRootMotionValueToggle);

            playbackCard.Content.Add(playbackOptionsCard.Root);

            ScrollView inspectorScrollView = new ScrollView();
            inspectorScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            inspectorScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            inspectorScrollView.style.flexGrow = 1;
            inspectorScrollView.style.minHeight = 0;
            inspectorScrollView.Add(assetsCard.Root);
            inspectorScrollView.Add(playbackCard.Root);

            // ── Card: Parameters ──
            m_AddParameterButton = CreateStyledButton("+", AddParameter, AccentColor);
            m_AddParameterButton.tooltip = "新增一个 XAnimation 参数。";
            SetAddParameterButtonEnabled(false);
            FoldoutCard parametersCard = CreateFoldoutCard("Parameters", m_ParametersSectionExpanded, value => m_ParametersSectionExpanded = value, m_AddParameterButton);
            m_ParameterListView = new VisualElement();
            parametersCard.Content.Add(m_ParameterListView);
            inspectorScrollView.Add(parametersCard.Root);

            // ── Card: States ──
            FoldoutCard statesCard = CreateFoldoutCard("States", m_StatesSectionExpanded, value => m_StatesSectionExpanded = value);
            m_StateListView = new VisualElement();
            statesCard.Content.Add(m_StateListView);
            inspectorScrollView.Add(statesCard.Root);

            // ── Card: Clips ──
            m_AddClipButton = CreateStyledButton("+", AddClip, AccentColor);
            m_AddClipButton.tooltip = "新增一个全局 clip 资源叶子。";
            SetAddClipButtonEnabled(false);
            FoldoutCard clipsCard = CreateFoldoutCard("Clips", m_ClipsSectionExpanded, value => m_ClipsSectionExpanded = value, m_AddClipButton);

            m_ClipListView = new VisualElement();
            clipsCard.Content.Add(m_ClipListView);
            inspectorScrollView.Add(clipsCard.Root);

            // ── Card: Channels ──
            m_AddChannelButton = CreateStyledButton("+", AddChannel, AccentColor);
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

        private static FoldoutCard CreateSectionFoldoutCard(
            string titleText,
            bool expanded,
            Action<bool> setExpanded)
        {
            VisualElement root = CreateSubBox();
            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 0;
            header.style.paddingBottom = 0;

            Label label = new();
            label.style.color = TextNormal;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexGrow = 1;
            header.Add(label);

            VisualElement content = new();
            content.style.marginTop = 4;
            root.Add(header);
            root.Add(content);

            void ApplyExpanded(bool value)
            {
                expanded = value;
                setExpanded?.Invoke(value);
                label.text = value ? $"▾ {titleText}" : $"▸ {titleText}";
                content.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                header.style.marginBottom = value ? 3 : 0;
            }

            ApplyExpanded(expanded);
            header.tooltip = $"点击展开/收起 {titleText}。";
            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                ApplyExpanded(!expanded);
                evt.StopPropagation();
            });

            return new FoldoutCard
            {
                Root = root,
                Content = content,
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
            RefreshStatePlayingStates();
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

            if (m_SelectedAsset != null)
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
            else if (m_PendingAsset != null)
            {
                GameObject defaultPrefab = m_SelectedAsset != null
                    ? LoadDefaultPrefabForAsset(m_SelectedAsset)
                    : null;
                if (defaultPrefab != null)
                {
                    m_SelectedPrefab = defaultPrefab;
                    m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
                }
            }
            else if (m_PrefabField.value == null && m_SelectedAsset != null)
            {
                m_SelectedPrefab = LoadDefaultPrefabForAsset(m_SelectedAsset);
                m_PrefabField.SetValueWithoutNotify(m_SelectedPrefab);
            }

            bool shouldAutoLoad = m_PendingAutoLoad && m_AssetField.value != null && m_PrefabField.value != null;

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

        private GameObject LoadDefaultPrefabForAsset(TextAsset assetText)
        {
            if (!TryGetDefaultPrefabPath(assetText, out string defaultPrefabPath) || string.IsNullOrWhiteSpace(defaultPrefabPath))
            {
                return null;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPrefabPath);
            if (prefab == null)
            {
                SetStatus($"默认 prefab 不存在：{defaultPrefabPath}", true);
                return null;
            }

            return prefab;
        }

        private bool TryGetDefaultPrefabPath(TextAsset assetText, out string defaultPrefabPath)
        {
            defaultPrefabPath = string.Empty;
            if (assetText == null)
            {
                return false;
            }

            XAnimationOverrideAsset overrideAsset = assetText.ToXTextAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                if (!string.IsNullOrWhiteSpace(overrideAsset.DefaultPrefabPath))
                {
                    defaultPrefabPath = overrideAsset.DefaultPrefabPath;
                    return true;
                }

                TextAsset baseTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(overrideAsset.baseAssetPath);
                if (baseTextAsset == null)
                {
                    SetStatus($"Override base asset 不存在：{overrideAsset.baseAssetPath}", true);
                    return false;
                }

                XAnimationAsset baseAsset = baseTextAsset.ToXTextAsset<XAnimationAsset>();
                defaultPrefabPath = baseAsset?.DefaultPrefabPath ?? string.Empty;
                return true;
            }

            XAnimationAsset asset = assetText.ToXTextAsset<XAnimationAsset>();
            defaultPrefabPath = asset?.DefaultPrefabPath ?? string.Empty;
            return true;
        }

        private void SaveCurrentPrefabAsDefault()
        {
            GameObject prefab = m_PrefabField?.value as GameObject;
            if (prefab == null)
            {
                SetStatus("请选择一个 prefab 后再设为默认。", true);
                return;
            }

            TextAsset assetText = m_AssetField?.value as TextAsset;
            if (assetText == null)
            {
                SetStatus("请选择一个 XAnimationAsset 后再设为默认。", true);
                return;
            }

            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                SetStatus("无法获取当前 prefab 的资源路径。", true);
                return;
            }

            XAnimationOverrideAsset overrideAsset = assetText.ToXTextAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                overrideAsset.DefaultPrefabPath = prefabPath;
                overrideAsset.SaveAsset();
                SetStatus($"已写入 Override 默认 prefab：{prefabPath}");
                return;
            }

            XAnimationAsset asset = assetText.ToXTextAsset<XAnimationAsset>();
            if (asset == null)
            {
                SetStatus("当前资源不是有效的 XAnimationAsset。", true);
                return;
            }

            asset.DefaultPrefabPath = prefabPath;
            asset.SaveAsset();
            SetStatus($"已写入默认 prefab：{prefabPath}");
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
                m_GridToggle.SetValueWithoutNotify(true);
                RebuildParameterList();
                RebuildStateList();
                RebuildClipList();
                RebuildChannelControls();
                RefreshPlayTargetChannelChoices();
                RefreshStatePlayingStates();
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
            RefreshStatePlayingStates();
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
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                XAnimationCompiledClip clip = (XAnimationCompiledClip)clips[clipIndex];
                m_ClipListView.Add(CreateClipRow(clip, clipIndex));
            }

            if (clips.Count == 0)
            {
                Label emptyLabel = new("No clips");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_ClipListView.Add(emptyLabel);
            }

            TryBeginPendingRename();
        }

        private void RebuildStateList()
        {
            m_StateListView.Clear();
            m_StateLabelMap.Clear();
            m_StateRowMap.Clear();
            m_StateButtonMap.Clear();
            m_StateChannelMap.Clear();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            if (states.Count == 0)
            {
                Label emptyLabel = new("No states");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_StateListView.Add(emptyLabel);
                return;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            Dictionary<string, List<XAnimationCompiledState>> statesByChannel = new(StringComparer.Ordinal);
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                string channelName = state.Config.channelName;
                if (!statesByChannel.TryGetValue(channelName, out List<XAnimationCompiledState> channelStates))
                {
                    channelStates = new List<XAnimationCompiledState>();
                    statesByChannel.Add(channelName, channelStates);
                }

                channelStates.Add(state);
            }

            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = channels[i];
                if (!statesByChannel.TryGetValue(channel.Name, out List<XAnimationCompiledState> channelStates))
                {
                    channelStates = new List<XAnimationCompiledState>();
                }

                VisualElement group = CreateStateChannelGroup(channel, channelStates);
                m_StateListView.Add(group);
            }

            TryBeginPendingRename();
        }

        private void RebuildParameterList()
        {
            m_ParameterListView.Clear();
            m_ParameterLabelMap.Clear();
            SetAddParameterButtonEnabled(m_Session != null && m_Session.IsLoaded && !m_Session.IsOverrideAsset);

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledParameter> parameters = m_Session.CompiledAsset.Parameters;
            if (parameters.Count == 0)
            {
                Label emptyLabel = new("No parameters");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_ParameterListView.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                m_ParameterListView.Add(CreateParameterRow(parameters[i], i));
            }

            TryBeginPendingRename();
        }

        private VisualElement CreateParameterRow(XAnimationCompiledParameter parameter, int rowIndex)
        {
            XAnimationParameterConfig config = parameter.Config;
            VisualElement container = new VisualElement();
            container.style.marginBottom = 2;
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.paddingTop = 3;
            container.style.paddingBottom = 3;
            container.style.borderTopLeftRadius = 2;
            container.style.borderTopRightRadius = 2;
            container.style.borderBottomLeftRadius = 2;
            container.style.borderBottomRightRadius = 2;
            container.style.backgroundColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            container.Add(row);

            string parameterName = parameter.Name;
            EditableLabel label = new(parameterName);
            ConfigureEditableNameLabel(label, 112f);
            label.tooltip = "右键 Rename 编辑参数名。";
            label.SetEditable(true, EditableLabelEditTrigger.ContextMenu);
            label.EditStarted += BeginNameEdit;
            label.EditEnded += EndNameEdit;
            label.ValueCommitted += (_, newValue) => RenameParameter(parameterName, newValue, label);
            m_ParameterLabelMap[parameterName] = label;
            row.Add(label);

            List<string> typeNames = new(Enum.GetNames(typeof(XAnimationParameterType)));
            DropdownField typeField = new(
                typeNames,
                Mathf.Max(0, typeNames.IndexOf(config.type.ToString())));
            typeField.tooltip = "参数类型。Blend1D 只能绑定 Float 参数。";
            typeField.style.width = 88;
            typeField.style.marginLeft = 4;
            typeField.RegisterValueChangedCallback(evt => ChangeParameterType(parameterName, evt.newValue, evt.previousValue, typeField));
            row.Add(typeField);

            VisualElement valueField = CreateParameterDefaultValueField(parameterName, config);
            valueField.style.flexGrow = 1;
            valueField.style.marginLeft = 6;
            row.Add(valueField);

            Button deleteButton = new(() => DeleteParameter(parameterName))
            {
                text = "⌫"
            };
            deleteButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能删除 parameter。"
                : "删除这个 parameter。";
            deleteButton.SetEnabled(m_Session != null && !m_Session.IsOverrideAsset);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;
            row.Add(deleteButton);

            VisualElement previewEditor = CreateParameterPreviewEditor(parameter);
            if (previewEditor != null)
            {
                previewEditor.style.marginTop = 4;
                container.Add(previewEditor);
            }

            return container;
        }

        private VisualElement CreateParameterDefaultValueField(string parameterName, XAnimationParameterConfig config)
        {
            switch (config.type)
            {
                case XAnimationParameterType.Float:
                {
                    FloatField field = new("default")
                    {
                        value = ConvertParameterDefaultToFloat(config.defaultValue)
                    };
                    field.tooltip = "Float 参数默认值，会保存到资源。";
                    field.RegisterValueChangedCallback(evt => ChangeParameterDefaultValue(parameterName, evt.newValue));
                    return field;
                }
                case XAnimationParameterType.Bool:
                {
                    Toggle toggle = new("default")
                    {
                        value = ConvertParameterDefaultToBool(config.defaultValue)
                    };
                    toggle.tooltip = "Bool 参数默认值，会保存到资源。";
                    toggle.RegisterValueChangedCallback(evt => ChangeParameterDefaultValue(parameterName, evt.newValue));
                    return toggle;
                }
                case XAnimationParameterType.Trigger:
                default:
                {
                    Label label = new("Trigger has no default value");
                    label.style.color = TextMuted;
                    label.style.fontSize = BodyFontSize;
                    return label;
                }
            }
        }

        private VisualElement CreateStateChannelGroup(XAnimationCompiledChannel channel, List<XAnimationCompiledState> channelStates)
        {
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
            groupTitle.tooltip = "点击展开/收起这个 channel 的 state 列表。";
            groupHeader.Add(groupTitle);

            Label groupInfo = new($"{channel.Config.layerType} | {channelStates.Count} states");
            groupInfo.style.color = TextMuted;
            groupInfo.style.fontSize = 10;
            groupInfo.style.flexShrink = 0;
            groupHeader.Add(groupInfo);

            Button addStateButton = new(() => AddState(channel.Name))
            {
                text = "+"
            };
            addStateButton.tooltip = m_Session.IsOverrideAsset
                ? "Override 资源不能新增 state。"
                : "在这个 channel 下新增一个 state。";
            addStateButton.SetEnabled(!m_Session.IsOverrideAsset);
            ApplyClipIconButtonStyle(addStateButton, AccentColor);
            addStateButton.style.marginLeft = 6;
            groupHeader.Add(addStateButton);
            group.Add(groupHeader);
            RegisterStateChannelDropTarget(group, groupHeader, channel.Name);

            VisualElement statesContainer = new VisualElement();
            for (int stateIndex = 0; stateIndex < channelStates.Count; stateIndex++)
            {
                XAnimationCompiledState state = channelStates[stateIndex];
                VisualElement row = CreateStateRow(state, stateIndex);
                RegisterStateRowDropTarget(row, channel.Name, state.Key);
                statesContainer.Add(row);
            }

            groupTitle.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                bool expanded = statesContainer.style.display != DisplayStyle.None;
                statesContainer.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                groupTitle.text = expanded ? $"▸ {channel.Name}" : $"▾ {channel.Name}";
                evt.StopPropagation();
            });

            group.Add(statesContainer);
            return group;
        }

        private VisualElement CreateStateRow(XAnimationCompiledState state, int rowIndex)
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
            row.style.backgroundColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            m_StateRowMap[state.Key] = row;
            m_StateChannelMap[state.Key] = state.Config.channelName;

            string stateKey = state.Key;
            EditableLabel label = new(stateKey);
            ConfigureEditableNameLabel(label, 78f);
            label.tooltip = "单击展开/收起 state 配置；右键 Rename 编辑名称。";
            label.SetEditable(true, EditableLabelEditTrigger.ContextMenu);
            label.EditStarted += BeginNameEdit;
            label.EditEnded += EndNameEdit;
            label.ValueCommitted += (_, newValue) => RenameState(stateKey, newValue, label);
            m_StateLabelMap[stateKey] = label;
            row.Add(label);

            Label infoLabel = new(BuildStateInfoText(state));
            infoLabel.style.flexGrow = 1;
            infoLabel.style.flexShrink = 1;
            infoLabel.style.whiteSpace = WhiteSpace.Normal;
            infoLabel.style.color = TextMuted;
            infoLabel.style.fontSize = BodyFontSize;
            infoLabel.tooltip = infoLabel.text;
            row.Add(infoLabel);

            VisualElement editor = CreateStateEditor(state);
            editor.style.display = m_ExpandedStateKeys.Contains(stateKey) ? DisplayStyle.Flex : DisplayStyle.None;
            RegisterStateNameInteractions(label, editor, stateKey);

            Button playButton = new(() => ToggleStatePlayback(state))
            {
                text = "▶"
            };
            playButton.tooltip = "播放或停止这个 state。Blend1D 会读取绑定参数实时混合。";
            ApplyClipButtonStyle(playButton, false);
            playButton.style.flexShrink = 0;
            playButton.style.marginLeft = 4;
            row.Add(playButton);
            m_StateButtonMap[state.Key] = playButton;

            Button deleteButton = new(() => DeleteState(stateKey))
            {
                text = "⌫"
            };
            deleteButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能删除 state。"
                : "删除这个 state。";
            deleteButton.SetEnabled(m_Session != null && !m_Session.IsOverrideAsset);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.flexShrink = 0;
            deleteButton.style.marginLeft = 3;
            row.Add(deleteButton);

            container.Add(row);
            container.Add(editor);
            return container;
        }

        private static string BuildStateInfoText(XAnimationCompiledState state)
        {
            string channelName = state.Config.channelName;
            return state switch
            {
                XAnimationCompiledSingleState singleState => $"{state.StateType} | channel={channelName} | clip={state.Config.clipKey}",
                XAnimationCompiledBlend1DState blendState => $"{state.StateType} | channel={channelName} | param={state.Config.parameterName} | {BuildBlendSampleSummary(blendState)}",
                _ => $"{state.StateType} | channel={channelName}",
            };
        }

        private static string BuildBlendSampleSummary(XAnimationCompiledBlend1DState state)
        {
            List<string> parts = new();
            for (int i = 0; i < state.Samples.Count; i++)
            {
                XAnimationCompiledBlend1DSample sample = state.Samples[i];
                parts.Add($"{sample.Config.clipKey}@{sample.Threshold:0.###}");
            }

            return string.Join(", ", parts);
        }

        private void ToggleStatePlayback(XAnimationCompiledState state)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            XAnimationChannelState channelState = m_Session.GetChannelState(state.Config.channelName);
            bool isPlaying = channelState != null && string.Equals(channelState.stateKey, state.Key, StringComparison.Ordinal);
            if (isPlaying)
            {
                m_Session.StopChannel(state.Config.channelName);
                RefreshStatePlayingStates();
                RefreshClipPlayingStates();
                RefreshChannelStates();
                SetStatus($"已停止 state {state.Key}。");
                return;
            }

            m_IsPaused = false;
            SetPauseButtonState(true, false);
            m_Session.Play(BuildPreviewPlayCommand(stateKey: state.Key, channelName: state.Config.channelName));
            RefreshStatePlayingStates();
            RefreshClipPlayingStates();
            RefreshChannelStates();
            SetStatus($"正在播放 state {state.Key}。");
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
            label.tooltip = "单击展开/收起 clip 配置；右键 Rename 编辑名称。";
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

                string channelName = m_PlayTargetChannelName;
                if (string.IsNullOrWhiteSpace(channelName))
                {
                    SetStatus("请先在 XAnimationPlayTarget 中选择 channelName 后再调试播放 clip。", true);
                    return;
                }

                string playingChannelName = FindPlayingChannelName(clipKey);
                bool isPlaying = !string.IsNullOrWhiteSpace(playingChannelName);

                if (isPlaying)
                {
                    m_Session.StopChannel(playingChannelName, 0f);
                    RefreshClipPlayingStates();
                    RefreshStatePlayingStates();
                    RefreshChannelStates();
                    SetStatus($"已停止 {clipKey}。");
                }
                else
                {
                    m_IsPaused = false;
                    SetPauseButtonState(true, false);
                    XAnimationPlayCommand command = BuildPreviewPlayCommand(clipKey: clipKey, channelName: channelName);
                    m_Session.Play(command);
                    RefreshClipPlayingStates();
                    RefreshStatePlayingStates();
                    RefreshChannelStates();
                    SetStatus($"正在 {channelName} 调试播放 {clipKey} ({clip.Clip.name})。");
                }
            })
            {
                text = "▶"
            };
            toggleButton.tooltip = "使用 XAnimationPlayTarget.channelName 调试播放或停止这个 clip。";
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

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                if (label.IsEditing)
                {
                    isPressed = false;
                    return;
                }

                isPressed = true;
                evt.StopPropagation();
            });
            label.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!isPressed || evt.button != 0)
                {
                    return;
                }

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

                isPressed = false;
                evt.StopPropagation();
            });
        }

        private void RegisterStateNameInteractions(EditableLabel label, VisualElement editor, string stateKey)
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
                    ClearStateDragData();
                    isPressed = false;
                    movedBeyondClickThreshold = false;
                    dragStarted = false;
                    return;
                }

                isPressed = true;
                movedBeyondClickThreshold = false;
                dragStarted = false;
                startPosition = evt.mousePosition;
                ClearStateDragData();
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
                    DragAndDrop.SetGenericData(StateDragDataKey, stateKey);
                    DragAndDrop.StartDrag($"Move {stateKey}");
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

                if (!movedBeyondClickThreshold && !label.IsEditing)
                {
                    bool expanded = editor.style.display == DisplayStyle.None;
                    editor.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                    if (expanded)
                    {
                        m_ExpandedStateKeys.Add(stateKey);
                    }
                    else
                    {
                        m_ExpandedStateKeys.Remove(stateKey);
                    }
                }

                if (!dragStarted)
                {
                    ClearStateDragData();
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
            ClearStateDragData();
        }

        private void EndNameEdit()
        {
            m_IsEditingName = false;
            ClearStateDragData();
        }

        private static void ClearStateDragData()
        {
            DragAndDrop.SetGenericData(StateDragDataKey, null);
        }

        private void AddChannel()
        {
            try
            {
                string channelName = m_Session.AddChannel();
                m_PendingChannelRenameKey = channelName;
                RebuildStateList();
                RebuildClipList();
                RebuildChannelControls();
                RefreshPlayTargetChannelChoices();
                RefreshStatePlayingStates();
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
            int stateCount = CountStatesInChannel(channelName);
            string message = stateCount > 0
                ? $"确定删除 Channel '{channelName}'？\n\n将同时移除该 Channel 下的 {stateCount} 个 State；Clip 资源不会被删除。"
                : $"确定删除 Channel '{channelName}'？";
            if (!EditorUtility.DisplayDialog("删除 Channel", message, "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteChannel(channelName);
                RebuildStateList();
                RebuildChannelControls();
                RefreshPlayTargetChannelChoices();
                RefreshStatePlayingStates();
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

        private void AddParameter()
        {
            try
            {
                string parameterName = m_Session.AddParameter();
                m_PendingParameterRenameKey = parameterName;
                RebuildParameterList();
                RebuildStateList();
                SetStatus($"已新增 Parameter {parameterName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteParameter(string parameterName)
        {
            if (!EditorUtility.DisplayDialog("删除 Parameter", $"确定删除 Parameter '{parameterName}'？\n\n引用它的 Blend1D state 会清空 parameter。", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteParameter(parameterName);
                RebuildParameterList();
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"已删除 Parameter {parameterName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RenameParameter(string oldName, string newName, EditableLabel label)
        {
            newName = newName?.Trim();
            try
            {
                m_Session.RenameParameter(oldName, newName);
                SetStatus($"Parameter {oldName} 已重命名为 {newName}。");
                RebuildParameterList();
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
            }
            catch (Exception ex)
            {
                label.text = oldName;
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeParameterType(string parameterName, string typeName, string previousValue, DropdownField field)
        {
            try
            {
                if (!Enum.TryParse(typeName, out XAnimationParameterType type))
                {
                    return;
                }

                m_Session.SetParameterType(parameterName, type);
                RebuildParameterList();
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{parameterName} type = {type}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeParameterDefaultValue(string parameterName, object value)
        {
            try
            {
                m_Session.SetParameterDefaultValue(parameterName, value);
                SetStatus($"{parameterName} defaultValue 已更新。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private int CountStatesInChannel(string channelName)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (state != null && string.Equals(state.Config.channelName, channelName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private void AddState(string channelName)
        {
            try
            {
                string stateKey = m_Session.AddState(channelName);
                m_PendingStateRenameKey = stateKey;
                RebuildStateList();
                RebuildChannelControls();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"已在 {channelName} 新增 State {stateKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteState(string stateKey)
        {
            if (!EditorUtility.DisplayDialog("删除 State", $"确定删除 State '{stateKey}'？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteState(stateKey);
                m_ExpandedStateKeys.Remove(stateKey);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"已删除 State {stateKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddClip()
        {
            try
            {
                string clipKey = m_Session.AddClip();
                m_PendingClipRenameKey = clipKey;
                RebuildStateList();
                RebuildClipList();
                RebuildChannelControls();
                RefreshStatePlayingStates();
                RefreshClipPlayingStates();
                RefreshChannelStates();
                SetStatus($"已新增 Clip {clipKey}。");
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
                RebuildStateList();
                RebuildClipList();
                RebuildChannelControls();
                RefreshStatePlayingStates();
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
                RebuildStateList();
                RebuildClipList();
                RebuildChannelControls();
                RefreshStatePlayingStates();
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
                RebuildStateList();
                RebuildChannelControls();
                RefreshPlayTargetChannelChoices();
                RefreshStatePlayingStates();
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

        private void RenameState(string oldKey, string newKey, EditableLabel label)
        {
            newKey = newKey?.Trim();
            try
            {
                m_Session.RenameState(oldKey, newKey);
                SetStatus($"State {oldKey} 已重命名为 {newKey}。");
                if (m_ExpandedStateKeys.Remove(oldKey) && !string.IsNullOrWhiteSpace(newKey))
                {
                    m_ExpandedStateKeys.Add(newKey.Trim());
                }
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
            }
            catch (Exception ex)
            {
                label.text = oldKey;
                SetStatus(ex.Message);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateType(string stateKey, XAnimationStateType stateType, string previousValue, DropdownField field)
        {
            try
            {
                m_Session.SetStateType(stateKey, stateType);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} stateType = {stateType}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateChannel(string stateKey, string channelName, DropdownField field, string previousValue)
        {
            try
            {
                m_Session.SetStateChannel(stateKey, channelName);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} channel = {channelName}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateClipKey(string stateKey, string clipKey, DropdownField field, string previousValue)
        {
            try
            {
                m_Session.SetStateClipKey(stateKey, clipKey);
                RebuildStateList();
                RestartStateIfPlaying(stateKey, null);
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} clipKey = {clipKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateBlendParameter(string stateKey, string parameterName, DropdownField field, string previousValue)
        {
            try
            {
                m_Session.SetStateBlendParameter(stateKey, parameterName);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} parameter = {parameterName}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateRootMotionMode(string stateKey, XAnimationClipRootMotionMode mode, string previousValue, DropdownField field)
        {
            try
            {
                m_Session.SetStateRootMotionMode(stateKey, mode);
                RestartStateIfPlaying(stateKey, null);
                RefreshChannelStates();
                SetStatus($"{stateKey} rootMotionMode = {mode}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddBlendSample(string stateKey)
        {
            try
            {
                m_Session.AddBlendSample(stateKey);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} 已新增 Blend1D sample。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteBlendSample(string stateKey, int sampleIndex)
        {
            try
            {
                m_Session.DeleteBlendSample(stateKey, sampleIndex);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} 已删除 Blend1D sample #{sampleIndex}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeBlendSampleClipKey(string stateKey, int sampleIndex, string clipKey, DropdownField field, string previousValue)
        {
            try
            {
                m_Session.SetBlendSampleClipKey(stateKey, sampleIndex, clipKey);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} sample #{sampleIndex} clip = {clipKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeBlendSampleThreshold(string stateKey, int sampleIndex, float threshold, FloatField field, float previousValue)
        {
            try
            {
                m_Session.SetBlendSampleThreshold(stateKey, sampleIndex, threshold);
                RebuildStateList();
                RefreshStatePlayingStates();
                RefreshChannelStates();
                SetStatus($"{stateKey} sample #{sampleIndex} threshold = {threshold:0.###}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RegisterStateChannelDropTarget(VisualElement group, VisualElement groupHeader, string channelName)
        {
            group.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName))
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
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                groupHeader.style.backgroundColor = ListHeaderBg;
                MoveState(stateKey, channelName);
                evt.StopPropagation();
            });
        }

        private void RegisterStateRowDropTarget(VisualElement row, string channelName, string insertBeforeStateKey)
        {
            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName, insertBeforeStateKey))
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
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName, insertBeforeStateKey))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                row.style.borderTopColor = Color.clear;
                row.style.borderTopWidth = 0;
                MoveState(stateKey, channelName, insertBeforeStateKey);
                evt.StopPropagation();
            });
        }

        private bool CanDropState(string stateKey, string channelName, string insertBeforeStateKey = null)
        {
            if (m_IsEditingName ||
                m_Session == null || !m_Session.IsLoaded ||
                string.IsNullOrWhiteSpace(stateKey) ||
                string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            if (!m_StateChannelMap.TryGetValue(stateKey, out string currentChannel))
            {
                return false;
            }

            return !string.Equals(stateKey, insertBeforeStateKey, StringComparison.Ordinal) ||
                !string.Equals(currentChannel, channelName, StringComparison.Ordinal);
        }

        private void MoveState(string stateKey, string channelName, string insertBeforeStateKey = null)
        {
            m_Session.MoveState(stateKey, channelName, insertBeforeStateKey);
            m_StateChannelMap[stateKey] = channelName;
            RebuildStateList();
            RefreshStatePlayingStates();
            RefreshChannelStates();
            SetStatus($"{stateKey} 已移动到 {channelName}。");
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

            if (!string.IsNullOrWhiteSpace(m_PendingStateRenameKey) &&
                m_StateLabelMap.TryGetValue(m_PendingStateRenameKey, out EditableLabel stateLabel))
            {
                string stateKey = m_PendingStateRenameKey;
                m_PendingStateRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    if (stateLabel != null)
                    {
                        m_ExpandedStateKeys.Remove(stateKey);
                        stateLabel.BeginEdit();
                    }
                }).StartingIn(0);
            }

            if (!string.IsNullOrWhiteSpace(m_PendingParameterRenameKey) &&
                m_ParameterLabelMap.TryGetValue(m_PendingParameterRenameKey, out EditableLabel parameterLabel))
            {
                m_PendingParameterRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    parameterLabel?.BeginEdit();
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

        private VisualElement CreateStateEditor(XAnimationCompiledState state)
        {
            XAnimationStateConfig config = state.Config;
            VisualElement editor = CreateFoldoutRowEditor();

            List<string> stateTypeNames = new(Enum.GetNames(typeof(XAnimationStateType)));
            DropdownField stateTypeField = new(
                "stateType",
                stateTypeNames,
                Mathf.Max(0, stateTypeNames.IndexOf(config.stateType.ToString())));
            stateTypeField.tooltip = "State 类型。Single 播放一个 clip；Blend1D 根据 float 参数混合采样点。";
            stateTypeField.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(evt.newValue, out XAnimationStateType stateType))
                {
                    return;
                }

                ChangeStateType(state.Key, stateType, evt.previousValue, stateTypeField);
            });
            editor.Add(stateTypeField);

            DropdownField channelField = CreateChannelDropdown("channel", config.channelName);
            channelField.tooltip = "State 默认播放 channel。";
            channelField.RegisterValueChangedCallback(evt => ChangeStateChannel(state.Key, evt.newValue, channelField, evt.previousValue));
            editor.Add(channelField);

            if (config.stateType == XAnimationStateType.Single)
            {
                DropdownField clipField = CreateClipKeyDropdown("clipKey", config.clipKey);
                clipField.tooltip = "Single state 播放的 clip。";
                clipField.RegisterValueChangedCallback(evt => ChangeStateClipKey(state.Key, evt.newValue, clipField, evt.previousValue));
                editor.Add(clipField);
            }
            else if (config.stateType == XAnimationStateType.Blend1D)
            {
                DropdownField parameterField = CreateFloatParameterDropdown("parameter", config.parameterName);
                parameterField.tooltip = "Blend1D 绑定的 Float 参数。";
                parameterField.RegisterValueChangedCallback(evt => ChangeStateBlendParameter(state.Key, evt.newValue, parameterField, evt.previousValue));
                editor.Add(parameterField);
                editor.Add(CreateBlendSampleEditor(state.Key, config));
            }

            Toggle loopField = new("loop") { value = config.loop };
            loopField.tooltip = "State 是否循环。";
            loopField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                m_Session.SetStateLoop(state.Key, evt.newValue);
                RestartStateIfPlaying(state.Key, config.channelName);
                SetStatus($"{state.Key} loop = {evt.newValue}。");
            });
            editor.Add(loopField);

            FloatField speedField = new("speed") { value = config.speed };
            speedField.tooltip = "State 默认速度。0 会按 1 处理。";
            speedField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float speed = Mathf.Approximately(evt.newValue, 0f) ? 1f : evt.newValue;
                if (!Mathf.Approximately(speed, evt.newValue))
                {
                    speedField.SetValueWithoutNotify(speed);
                }

                m_Session.SetStateSpeed(state.Key, speed, save: false);
                ScheduleAssetSave();
                SetStatus($"{state.Key} speed = {speed:0.###}。");
            });
            editor.Add(speedField);

            VisualElement fadeRow = new VisualElement();
            fadeRow.style.flexDirection = FlexDirection.Row;
            fadeRow.style.alignItems = Align.Center;

            FloatField fadeInField = new("fadeIn") { value = config.fadeIn };
            fadeInField.tooltip = "State 默认淡入时间。";
            fadeInField.style.flexGrow = 1;
            fadeInField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float fadeIn = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(fadeIn, evt.newValue))
                {
                    fadeInField.SetValueWithoutNotify(fadeIn);
                }

                m_Session.SetStateFade(state.Key, fadeIn, config.fadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{state.Key} fadeIn = {fadeIn:0.###}。");
            });
            fadeRow.Add(fadeInField);

            FloatField fadeOutField = new("fadeOut") { value = config.fadeOut };
            fadeOutField.tooltip = "State 默认淡出时间。";
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

                m_Session.SetStateFade(state.Key, config.fadeIn, fadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{state.Key} fadeOut = {fadeOut:0.###}。");
            });
            fadeRow.Add(fadeOutField);
            editor.Add(fadeRow);

            List<string> rootMotionModeNames = new(Enum.GetNames(typeof(XAnimationClipRootMotionMode)));
            DropdownField rootMotionModeField = new(
                "rootMotionMode",
                rootMotionModeNames,
                Mathf.Max(0, rootMotionModeNames.IndexOf(config.rootMotionMode.ToString())));
            rootMotionModeField.tooltip = "State 的 Root Motion 策略：继承 channel、强制开启或强制关闭。";
            rootMotionModeField.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(evt.newValue, out XAnimationClipRootMotionMode mode))
                {
                    return;
                }

                ChangeStateRootMotionMode(state.Key, mode, evt.previousValue, rootMotionModeField);
            });
            editor.Add(rootMotionModeField);
            return editor;
        }

        private static VisualElement CreateFoldoutRowEditor()
        {
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
            return editor;
        }

        private VisualElement CreateBlendSampleEditor(string stateKey, XAnimationStateConfig config)
        {
            VisualElement box = CreateSubBox();
            box.style.marginTop = 5;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 3;

            Label title = new("Samples");
            title.style.flexGrow = 1;
            title.style.color = TextNormal;
            title.style.fontSize = BodyFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            bool editable = m_Session != null && !m_Session.IsOverrideAsset;
            Button addButton = new(() => AddBlendSample(stateKey))
            {
                text = "+"
            };
            addButton.tooltip = editable ? "为这个 Blend1D state 新增采样点。" : "Override 资源不能新增采样点。";
            addButton.SetEnabled(editable);
            ApplyClipIconButtonStyle(addButton, AccentColor);
            header.Add(addButton);
            box.Add(header);

            XAnimationBlend1DSampleConfig[] samples = config.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
            for (int i = 0; i < samples.Length; i++)
            {
                box.Add(CreateBlendSampleRow(stateKey, i, samples[i], editable));
            }

            if (samples.Length == 0)
            {
                Label emptyLabel = new("No samples");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                box.Add(emptyLabel);
            }

            return box;
        }

        private VisualElement CreateParameterPreviewEditor(XAnimationCompiledParameter parameter)
        {
            if (parameter == null || parameter.Type != XAnimationParameterType.Float)
            {
                return null;
            }

            float defaultValue = ConvertParameterDefaultToFloat(parameter.Config.defaultValue);
            if (TryGetBlend1DPreviewRange(parameter.Name, out float min, out float max))
            {
                return CreateFloatPreviewParameterRow(parameter.Name, defaultValue, min, max, useSlider: true);
            }

            return CreateFloatPreviewParameterRow(parameter.Name, defaultValue, defaultValue, defaultValue, useSlider: false);
        }

        private bool TryGetBlend1DPreviewRange(string parameterName, out float min, out float max)
        {
            min = 0f;
            max = 0f;
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            bool found = false;
            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] is not XAnimationCompiledBlend1DState blendState)
                {
                    continue;
                }

                if (!string.Equals(blendState.Config.parameterName, parameterName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (blendState.Samples.Count == 0)
                {
                    continue;
                }

                float stateMin = blendState.Samples[0].Threshold;
                float stateMax = blendState.Samples[blendState.Samples.Count - 1].Threshold;
                if (!found)
                {
                    min = stateMin;
                    max = stateMax;
                    found = true;
                }
                else
                {
                    min = Mathf.Min(min, stateMin);
                    max = Mathf.Max(max, stateMax);
                }
            }

            if (!found)
            {
                return false;
            }

            if (Mathf.Approximately(min, max))
            {
                max = min + 1f;
            }

            return true;
        }

        private VisualElement CreateFloatPreviewParameterRow(
            string parameterName,
            float defaultValue,
            float min,
            float max,
            bool useSlider)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            Label label = new(parameterName);
            label.style.width = 82;
            label.style.flexShrink = 0;
            label.style.color = TextMuted;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);

            FloatField valueField = new()
            {
                value = defaultValue
            };
            valueField.tooltip = "预览参数值，只影响当前 Preview Session，不保存到资源。";
            ConfigureCompactNumberField(valueField);

            if (useSlider)
            {
                Slider slider = new(min, max)
                {
                    value = defaultValue
                };
                slider.tooltip = $"Blend 参数范围来自 samples: [{min:0.###}, {max:0.###}]。";
                slider.style.flexGrow = 1;
                slider.RegisterValueChangedCallback(evt =>
                {
                    valueField.SetValueWithoutNotify(evt.newValue);
                    SetPreviewFloatParameter(parameterName, evt.newValue);
                });
                valueField.RegisterValueChangedCallback(evt =>
                {
                    slider.SetValueWithoutNotify(Mathf.Clamp(evt.newValue, min, max));
                    SetPreviewFloatParameter(parameterName, evt.newValue);
                });
                row.Add(slider);
                row.Add(valueField);
            }
            else
            {
                valueField.style.flexGrow = 1;
                valueField.style.width = StyleKeyword.Auto;
                valueField.style.minWidth = 64;
                valueField.style.maxWidth = StyleKeyword.None;
                valueField.RegisterValueChangedCallback(evt => SetPreviewFloatParameter(parameterName, evt.newValue));
                row.Add(valueField);
            }

            Button zeroButton = new(() =>
            {
                valueField.SetValueWithoutNotify(0f);
                SetPreviewFloatParameter(parameterName, 0f);
            })
            {
                text = "0"
            };
            zeroButton.tooltip = "把这个预览参数重置为 0。";
            ApplyClipIconButtonStyle(zeroButton);
            zeroButton.style.marginLeft = 4;
            row.Add(zeroButton);

            return row;
        }

        private List<XAnimationCompiledParameter> GetFloatParameters()
        {
            List<XAnimationCompiledParameter> parameters = new();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return parameters;
            }

            IReadOnlyList<XAnimationCompiledParameter> compiledParameters = m_Session.CompiledAsset.Parameters;
            for (int i = 0; i < compiledParameters.Count; i++)
            {
                XAnimationCompiledParameter parameter = compiledParameters[i];
                if (parameter.Type == XAnimationParameterType.Float)
                {
                    parameters.Add(parameter);
                }
            }

            return parameters;
        }

        private void SetPreviewFloatParameter(string parameterName, float value)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            try
            {
                m_Session.SetPreviewParameter(parameterName, value);
                SetStatus($"Preview parameter {parameterName} = {value:0.###}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private static float ConvertParameterDefaultToFloat(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            try
            {
                return Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0f;
            }
        }

        private static bool ConvertParameterDefaultToBool(object value)
        {
            if (value == null)
            {
                return false;
            }

            try
            {
                return Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private VisualElement CreateBlendSampleRow(string stateKey, int sampleIndex, XAnimationBlend1DSampleConfig sample, bool editable)
        {
            VisualElement row = CreateSubBox();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            Label indexLabel = new($"#{sampleIndex}");
            indexLabel.style.width = 28;
            indexLabel.style.flexShrink = 0;
            indexLabel.style.color = TextMuted;
            indexLabel.style.fontSize = BodyFontSize;
            indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(indexLabel);

            DropdownField clipField = CreateClipKeyDropdown("clip", sample?.clipKey);
            clipField.SetEnabled(editable);
            clipField.style.flexGrow = 1;
            clipField.RegisterValueChangedCallback(evt => ChangeBlendSampleClipKey(stateKey, sampleIndex, evt.newValue, clipField, evt.previousValue));
            row.Add(clipField);

            FloatField thresholdField = new("threshold")
            {
                value = sample?.threshold ?? 0f
            };
            thresholdField.SetEnabled(editable);
            thresholdField.tooltip = "一维 Blend 轴上的采样位置，必须保持严格递增。";
            thresholdField.style.width = 120;
            thresholdField.style.marginLeft = 6;
            thresholdField.RegisterValueChangedCallback(evt => ChangeBlendSampleThreshold(stateKey, sampleIndex, evt.newValue, thresholdField, evt.previousValue));
            row.Add(thresholdField);

            Button deleteButton = new(() => DeleteBlendSample(stateKey, sampleIndex))
            {
                text = "⌫"
            };
            deleteButton.tooltip = editable ? "删除这个采样点。" : "Override 资源不能删除采样点。";
            deleteButton.SetEnabled(editable);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;
            row.Add(deleteButton);

            return row;
        }

        private DropdownField CreateChannelDropdown(string label, string currentValue)
        {
            List<string> choices = new();
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    choices.Add(channels[i].Name);
                }
            }

            EnsureDropdownChoice(choices, currentValue);
            return new DropdownField(label, choices, Mathf.Max(0, choices.IndexOf(currentValue ?? string.Empty)));
        }

        private void RefreshPlayTargetChannelChoices()
        {
            if (m_PlayTargetChannelField == null)
            {
                return;
            }

            List<string> choices = new();
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    choices.Add(channels[i].Name);
                }
            }

            string selected = !string.IsNullOrWhiteSpace(m_PlayTargetChannelName) && choices.Contains(m_PlayTargetChannelName)
                ? m_PlayTargetChannelName
                : choices.Count > 0
                    ? choices[0]
                    : string.Empty;

            m_PlayTargetChannelField.choices = choices;
            m_PlayTargetChannelField.SetValueWithoutNotify(selected);
            m_PlayTargetChannelField.SetEnabled(choices.Count > 0);
            m_PlayTargetChannelName = selected;
        }

        private XAnimationPlayCommand BuildPreviewPlayCommand(string stateKey = null, string clipKey = null, string channelName = null)
        {
            XAnimationPlayCommand command = new()
            {
                target = new XAnimationPlayTarget
                {
                    stateKey = stateKey,
                    clipKey = clipKey,
                    channelName = channelName,
                },
                transition = new XAnimationTransitionOptions
                {
                    interruptible = true,
                },
                playback = new XAnimationPlaybackOptions
                {
                    speed = Mathf.Approximately(m_PlaySpeedField?.value ?? 0f, 0f) ? 1f : m_PlaySpeedField.value,
                    weight = 1f,
                },
            };

            if (m_ApplyTransitionRequestOverrides)
            {
                command.transition.fadeIn = Mathf.Max(0f, m_PlayFadeInOverride);
                command.transition.fadeOut = Mathf.Max(0f, m_PlayFadeOutOverride);
                command.transition.priority = m_PlayPriorityOverride;
                command.transition.interruptible = m_PlayInterruptibleOverride;
            }

            if (m_ApplyPlaybackOverrides)
            {
                command.playback.weight = m_PlayWeightOverride;
                command.playback.normalizedTime = Mathf.Clamp01(m_PlayNormalizedTimeOverride);
                command.playback.loopOverride = m_PlayUseLoopOverride ? m_PlayLoopOverride : null;
                command.playback.rootMotionOverride = m_PlayUseRootMotionOverride ? m_PlayRootMotionOverride : null;
            }

            return command;
        }

        private DropdownField CreateClipKeyDropdown(string label, string currentValue)
        {
            List<string> choices = new();
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
                for (int i = 0; i < clips.Count; i++)
                {
                    choices.Add(clips[i].Key);
                }
            }

            EnsureDropdownChoice(choices, currentValue);
            return new DropdownField(label, choices, Mathf.Max(0, choices.IndexOf(currentValue ?? string.Empty)));
        }

        private DropdownField CreateFloatParameterDropdown(string label, string currentValue)
        {
            List<string> choices = new();
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledParameter> parameters = m_Session.CompiledAsset.Parameters;
                for (int i = 0; i < parameters.Count; i++)
                {
                    XAnimationCompiledParameter parameter = parameters[i];
                    if (parameter.Type == XAnimationParameterType.Float)
                    {
                        choices.Add(parameter.Name);
                    }
                }
            }

            EnsureDropdownChoice(choices, currentValue);
            return new DropdownField(label, choices, Mathf.Max(0, choices.IndexOf(currentValue ?? string.Empty)));
        }

        private static void EnsureDropdownChoice(List<string> choices, string currentValue)
        {
            currentValue ??= string.Empty;
            if (choices.Count == 0 || !choices.Contains(currentValue))
            {
                choices.Insert(0, currentValue);
            }
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

            m_Session.Play(BuildPreviewPlayCommand(
                clipKey: clipKey,
                channelName: string.IsNullOrWhiteSpace(channelName) ? playingChannelName : channelName));
            RefreshClipPlayingStates();
            RefreshStatePlayingStates();
            RefreshChannelStates();
            return true;
        }

        private bool RestartStateIfPlaying(string stateKey, string channelName)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            string playingChannelName = FindPlayingStateChannelName(stateKey);
            if (string.IsNullOrEmpty(playingChannelName))
            {
                return false;
            }

            m_Session.Play(BuildPreviewPlayCommand(
                stateKey: stateKey,
                channelName: string.IsNullOrWhiteSpace(channelName) ? playingChannelName : channelName));
            RefreshStatePlayingStates();
            RefreshChannelStates();
            return true;
        }

        private string FindPlayingStateChannelName(string stateKey)
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
                if (state != null && string.Equals(state.stateKey, stateKey, StringComparison.Ordinal))
                {
                    return channelName;
                }
            }

            return null;
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
                RestartClipIfPlaying(clip.Key, m_PlayTargetChannelName);
                SetStatus($"{clip.Key} clip = {newClip.name}。");
                RebuildClipList();
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

        private void SetAddParameterButtonEnabled(bool enabled)
        {
            if (m_AddParameterButton == null)
            {
                return;
            }

            m_AddParameterButton.SetEnabled(enabled);
            m_AddParameterButton.style.opacity = enabled ? 1f : 0.45f;
            m_AddParameterButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能新增 parameter。"
                : "新增一个 parameter。";
        }

        private void SetAddClipButtonEnabled(bool enabled)
        {
            if (m_AddClipButton == null)
            {
                return;
            }

            m_AddClipButton.SetEnabled(enabled);
            m_AddClipButton.style.opacity = enabled ? 1f : 0.45f;
            m_AddClipButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能新增 clip。"
                : "新增一个全局 clip 资源叶子。";
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

            bool hasPlaying = HasAnyPlayingChannel();
            SetStopAllButtonEnabled(hasPlaying);
            if (!hasPlaying)
            {
                m_IsPaused = false;
            }
            SetPauseButtonState(hasPlaying, m_IsPaused);

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

        private void RefreshStatePlayingStates()
        {
            if (m_StateRowMap.Count == 0)
            {
                return;
            }

            HashSet<string> playingStateKeys = null;
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    XAnimationChannelState state = m_Session.GetChannelState(channels[i].Name);
                    if (state != null && !string.IsNullOrEmpty(state.stateKey))
                    {
                        playingStateKeys ??= new HashSet<string>(StringComparer.Ordinal);
                        playingStateKeys.Add(state.stateKey);
                    }
                }
            }

            foreach (KeyValuePair<string, VisualElement> kvp in m_StateRowMap)
            {
                bool isPlaying = playingStateKeys != null && playingStateKeys.Contains(kvp.Key);
                kvp.Value.style.backgroundColor = isPlaying ? PlayingBg : ListRowEvenBg;
                if (m_StateButtonMap.TryGetValue(kvp.Key, out Button button))
                {
                    ApplyClipButtonStyle(button, isPlaying);
                }
            }
        }

        private bool HasAnyPlayingChannel()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                if (m_Session.GetChannelState(channels[i].Name) != null)
                {
                    return true;
                }
            }

            return false;
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

        private static void ConfigureCompactPlaybackField(BaseField<float> field, string labelText, float valueWidth)
        {
            field.label = string.Empty;
            field.style.width = valueWidth;
            field.style.minWidth = valueWidth;
            field.style.maxWidth = valueWidth;
            field.style.flexShrink = 0;
            field.style.alignSelf = Align.Center;
        }

        private static void ConfigureCompactPlaybackElement(VisualElement field, float valueWidth)
        {
            field.style.width = valueWidth;
            field.style.minWidth = valueWidth;
            field.style.maxWidth = valueWidth;
            field.style.flexShrink = 0;
            field.style.alignSelf = Align.Center;
        }

        private static VisualElement CreatePlaybackFieldContainer(string labelText, VisualElement field, float labelWidth)
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            container.style.minWidth = 0;

            Label label = new(labelText);
            label.style.width = labelWidth;
            label.style.minWidth = labelWidth;
            label.style.maxWidth = labelWidth;
            label.style.flexShrink = 0;
            label.style.fontSize = 10;
            label.style.color = TextMuted;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginRight = 6;
            container.Add(label);
            container.Add(field);
            return container;
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
            string stateKey = string.IsNullOrWhiteSpace(state.stateKey) ? state.clipKey : state.stateKey;
            string blend = BuildBlendStateDebugText(state);
            return $"State: {stateKey} ({state.stateType}) | Clip: {state.clipKey} | PlayId: {state.playbackId} | {loop} | {fade} | {interrupt}\n"
                + $"Time: {state.normalizedTime:0.000} / Total: {state.totalNormalizedTime:0.000} | Channel Weight: {state.channelWeight:0.000} | State Weight: {state.weight:0.000}\n"
                + $"TimeScale: {state.timeScale:0.000} | Effective Speed: {state.speed:0.000} | Priority: {state.priority}{blend}";
        }

        private static string BuildBlendStateDebugText(XAnimationChannelState state)
        {
            if (state.blendClips == null || state.blendClips.Length == 0)
            {
                return string.Empty;
            }

            List<string> parts = new();
            for (int i = 0; i < state.blendClips.Length; i++)
            {
                XAnimationBlendClipState clipState = state.blendClips[i];
                parts.Add($"{clipState.clipKey}:{clipState.weight:0.000}");
            }

            return $"\nBlend: {string.Join(", ", parts)}";
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
            m_ChannelControlsContainer?.Clear();
            m_ChannelLabelMap.Clear();
            m_CueLogContainer?.Clear();
            m_ChannelStateLabels.Clear();
            m_PendingClipRenameKey = null;
            m_PendingChannelRenameKey = null;
            if (m_PreviewImage != null)
            {
                m_PreviewImage.image = null;
            }
            m_LastCueLogCount = -1;
            m_IsPaused = false;
            SetPauseButtonState(false, false);
            SetStopAllButtonEnabled(false);
            SetAddClipButtonEnabled(false);
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
            if (m_PreviewImage != null)
            {
                m_PreviewImage.image = null;
            }
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
