#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UEvent = UnityEngine.Event;
using XFramework.Animation;
using XFramework.Resource;
using XFramework.UI;
using static XFramework.Editor.XAnimationEditorParameterUtility;
using static XFramework.Editor.XAnimationEditorUi;

namespace XFramework.Editor
{
    public sealed partial class XAnimationPreviewWindow : EditorWindow
    {
        private readonly struct DisplayedCueEntry
        {
            public DisplayedCueEntry(int cueIndex, float time, string eventKey, string payload, bool isReadOnlyDerived)
            {
                CueIndex = cueIndex;
                Time = time;
                EventKey = eventKey ?? string.Empty;
                Payload = payload ?? string.Empty;
                IsReadOnlyDerived = isReadOnlyDerived;
            }

            public int CueIndex { get; }
            public float Time { get; }
            public string EventKey { get; }
            public string Payload { get; }
            public bool IsReadOnlyDerived { get; }
        }

        private enum DebugToolbarGroup
        {
            Main,
            Clip,
            Channels,
            Parameters,
            Graph,
        }

        private enum SearchEntryType
        {
            State,
            Clip,
            Transition,
            Cue,
            Parameter,
            Channel,
        }

        private sealed class SearchEntry
        {
            public SearchEntry(SearchEntryType type, string title, string detail, string searchText, Action navigate)
            {
                Type = type;
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
                SearchText = searchText ?? string.Empty;
                Navigate = navigate;
            }

            public SearchEntryType Type { get; }
            public string Title { get; }
            public string Detail { get; }
            public string SearchText { get; }
            public Action Navigate { get; }
        }

        private sealed class StateGroupBucket
        {
            public StateGroupBucket(string channelName, string groupName)
            {
                ChannelName = channelName ?? string.Empty;
                GroupName = groupName ?? string.Empty;
                States = new List<XAnimationCompiledState>();
            }

            public string ChannelName { get; }
            public string GroupName { get; }
            public List<XAnimationCompiledState> States { get; }
            public bool IsUngrouped => string.IsNullOrWhiteSpace(GroupName);
        }

        private sealed class ClipGroupBucket
        {
            public ClipGroupBucket(string groupName)
            {
                GroupName = groupName ?? string.Empty;
                Clips = new List<XAnimationCompiledClip>();
            }

            public string GroupName { get; }
            public List<XAnimationCompiledClip> Clips { get; }
            public bool IsUngrouped => string.IsNullOrWhiteSpace(GroupName);
        }

        private readonly struct StateSelectionItem
        {
            public StateSelectionItem(string stateKey, string channelName, string groupName)
            {
                StateKey = stateKey ?? string.Empty;
                ChannelName = channelName ?? string.Empty;
                GroupName = NormalizeStateEditorGroupName(groupName);
            }

            public string StateKey { get; }
            public string ChannelName { get; }
            public string GroupName { get; }
            public bool IsGrouped => !string.IsNullOrWhiteSpace(GroupName);
        }

        private readonly struct ClipSelectionItem
        {
            public ClipSelectionItem(string clipKey, string groupName)
            {
                ClipKey = clipKey ?? string.Empty;
                GroupName = NormalizeClipEditorGroupName(groupName);
            }

            public string ClipKey { get; }
            public string GroupName { get; }
            public bool IsGrouped => !string.IsNullOrWhiteSpace(GroupName);
        }

        private readonly struct SearchableSelectionItem
        {
            public SearchableSelectionItem(string value, string title, string detail, string searchText, string groupKey = null, bool isGroup = false)
            {
                Value = value ?? string.Empty;
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
                SearchText = searchText ?? string.Empty;
                GroupKey = groupKey ?? string.Empty;
                IsGroup = isGroup;
            }

            public string Value { get; }
            public string Title { get; }
            public string Detail { get; }
            public string SearchText { get; }
            public string GroupKey { get; }
            public bool IsGroup { get; }
        }

        private const string MenuPath = "XFramework/Tools/XAnimation Preview";
        private const float DebugPaneInitialWidth = 360f;
        private const float DebugPaneMinWidth = 280f;
        private const float PreviewPaneMinWidth = 360f;
        private const float SectionTitleFontSize = 12f;
        private const float BodyFontSize = 11f;
        private const float InspectorMinHeight = 240f;
        private const float CueLogInitialHeight = 90f;
        private const float CueLogSectionMinHeight = 72f;
        private const float ClipIconButtonSize = 22f;
        private const float ChannelStateLabelHeight = 64f;
        private const float PlaybackLabelWidth = 118f;
        private const float PlaybackSpeedMin = 0.1f;
        private const float PlaybackSpeedMax = 2f;
        private const float PlaybackScrubberWidth = 132f;
        private const float PlaybackSpeedControlWidth = 96f;
        private const float PlaybackOverlayInitialLeft = 10f;
        private const float PlaybackOverlayInitialTop = 10f;
        private const float PlaybackOverlayMinWidth = 392f;
        private const float PlaybackOverlayClickThreshold = 2f;
        private const float FreeformBlendGraphOverlayInitialLeft = 12f;
        private const float FreeformBlendGraphOverlayInitialBottom = 12f;
        private const float FreeformBlendGraphOverlayWidth = 244f;
        private const float BlendGraphOverlayHeaderMarginBottomExpanded = 4f;
        private const float PlaybackToolbarButtonSize = 20f;
        private const float PlaybackMainFieldLabelWidth = 68f;
        private const float PlaybackMainFieldValueWidth = 112f;
        private const float TransitionFieldLabelWidth = 58f;
        private const float TransitionFieldValueWidth = 64f;
        private const string StateDragDataKey = nameof(XAnimationPreviewWindow) + ".StateKey";
        private const string ClipDragDataKey = nameof(XAnimationPreviewWindow) + ".ClipKey";
        private const string LastAssetPathPrefsKey = "XFramework.Editor.XAnimation.Preview.LastAssetPath";
        private const string LastPrefabPathPrefsKey = "XFramework.Editor.XAnimation.Preview.LastPrefabPath";
        private const double ActivePreviewUpdateIntervalSeconds = 1d / 30d;

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
        private static readonly Color ProgressFillBg = new(0.20f, 0.55f, 0.95f, 0.55f);
        private static readonly Color BlendWeightFillBg = new(0.25f, 0.72f, 0.34f, 0.55f);
        private static readonly Color ClipFocusFlashBg = new(0.92f, 0.73f, 0.20f, 0.95f);

        private readonly Dictionary<string, Label> m_ChannelStateLabels = new(StringComparer.Ordinal);

        [SerializeField] private TextAsset m_SelectedAsset;
        [SerializeField] private GameObject m_SelectedPrefab;
        [SerializeField] private bool m_ShouldAutoReloadPreview;
        [SerializeField] private bool m_AssetsSectionExpanded = true;
        [SerializeField] private bool m_PlaybackSectionExpanded = true;
        [SerializeField] private bool m_PlayTransitionSectionExpanded;

        [SerializeField] private bool m_PreviewParametersSectionExpanded = true;
        [SerializeField] private bool m_ParametersSectionExpanded = true;
        [SerializeField] private bool m_StatesSectionExpanded = true;
        [SerializeField] private bool m_AutoTransitionSectionExpanded = true;
        [SerializeField] private bool m_DefaultTransitionsSectionExpanded = true;
        [SerializeField] private bool m_ClipsSectionExpanded = true;
        [SerializeField] private bool m_ChannelsSectionExpanded = true;
        [SerializeField] private bool m_PreviewRootMotionEnabled = true;
        [SerializeField] private DebugToolbarGroup m_SelectedDebugToolbarGroup;
        [SerializeField] private Vector2 m_FreeformBlendGraphOverlayPosition = new(FreeformBlendGraphOverlayInitialLeft, FreeformBlendGraphOverlayInitialBottom);
        [SerializeField] private bool m_FreeformBlendGraphOverlayExpanded = true;
        [SerializeField] private Vector2 m_PlaybackOverlayPosition = new(PlaybackOverlayInitialLeft, PlaybackOverlayInitialTop);

        private TextAsset m_PendingAsset;
        private GameObject m_PendingPrefab;
        private bool m_PendingAutoLoad;
        private PendingPlaybackRequest? m_PendingPlaybackRequest;

        private ObjectField m_PrefabField;
        private ObjectField m_AssetField;
        private Image m_PreviewImage;
        private Label m_StatusLabel;
        private VisualElement m_PlaybackScrubber;
        private VisualElement m_PlaybackScrubberLine;
        private Slider m_PlaySpeedSlider;
        private Label m_PlaySpeedValueLabel;
        private FloatField m_PlayFadeInField;
        private FloatField m_PlayFadeOutField;

        private FloatField m_PlayEnterTimeField;
        private IntegerField m_PlayPriorityField;
        private Toggle m_ApplyTransitionRequestToggle;

        private Toggle m_PlayInterruptibleToggle;
        private Toggle m_RootMotionToggle;
        private Toggle m_GridToggle;
        private DropdownField m_PlayTargetChannelField;
        private Button m_PauseButton;
        private Button m_StepForwardButton;
        private Button m_StopAllButton;
        private Button m_AddClipButton;
        private Button m_AddClipGroupButton;
        private Button m_AddChannelButton;
        private Button m_AddParameterButton;
        private Button m_AddAutoTransitionButton;
        private Button m_AddDefaultTransitionButton;
        private Button m_MainGroupButton;
        private Button m_ClipGroupButton;
        private Button m_ChannelsGroupButton;
        private Button m_ParametersGroupButton;
        private Button m_GraphGroupButton;
        private TextField m_SearchField;
        private VisualElement m_SearchResultsPopup;
        private VisualElement m_SearchResultsList;
        private VisualElement m_ParameterListView;
        private VisualElement m_MainParameterPreviewView;
        private VisualElement m_StateListView;
        private VisualElement m_AutoTransitionEditorView;
        private VisualElement m_DefaultTransitionsEditorView;
        private VisualElement m_ClipListView;
        private ScrollView m_InspectorScrollView;
        private VisualElement m_InspectorOverlayLayer;
        private VisualElement m_MainGroupContainer;
        private VisualElement m_ClipGroupContainer;
        private VisualElement m_ChannelsGroupContainer;
        private VisualElement m_ParametersGroupContainer;
        private VisualElement m_GraphGroupContainer;
        private readonly HashSet<string> m_ExpandedStateKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_CollapsedStateGroupKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_CollapsedBlendSampleStateKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_CollapsedDirectionalSampleStateKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_CollapsedAutoTransitionKeys = new(StringComparer.Ordinal);
        private readonly HashSet<int> m_CollapsedDefaultTransitionIndices = new();
        private readonly Dictionary<string, EditableLabel> m_StateLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_StateGroupLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_StateRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_StateEditorMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_StateGroupRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RowVisualState> m_StateVisualStateMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_StateButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> m_StateChannelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ParameterLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ParameterRowMap = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_ExpandedClipKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_CollapsedClipGroupKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ClipLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ClipGroupLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ClipRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ClipGroupRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ClipRowVisualState> m_ClipVisualStateMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RowVisualState> m_BlendSampleRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_ClipButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EditableLabel> m_ChannelLabelMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ChannelRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_AutoTransitionRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<int, VisualElement> m_DefaultTransitionRowMap = new();
        private readonly Dictionary<string, VisualElement> m_CueRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<VisualElement, VisualElement> m_FlashOverlayMap = new();
        private readonly Dictionary<VisualElement, int> m_FlashOverlayVersionMap = new();
        private readonly List<SearchEntry> m_SearchEntries = new();
        private readonly List<SearchEntry> m_VisibleSearchEntries = new();
        private VisualElement m_ChannelControlsContainer;
        private XAnimationGraphDebugView m_GraphDebugView;
        private ScrollView m_CueLogContainer;
        private readonly List<Label> m_LogLabels = new();

        private XAnimationEditorPreviewSession m_Session;
        private IVisualElementScheduledItem m_DelayedSaveItem;
        private bool m_IsEditingName;
        private bool m_IsPaused;
        private bool m_IsPreviewDragging;
        private Vector2 m_LastPreviewMousePosition;
        private int? m_SelectedLogId;
        private bool m_FollowLatestLog = true;
        private string m_PendingClipRenameKey;
        private string m_PendingStateRenameKey;
        private string m_PendingParameterRenameKey;
        private string m_PendingChannelRenameKey;
        private string m_PlayTargetChannelName;
        private string m_LastInteractedFreeformStateKey;
        private string m_CurrentFreeformGraphStateKey;
        private string m_SelectedAutoTransitionStateKey;
        private int m_SelectedDefaultTransitionIndex = -1;
        private float m_PlayFadeInOverride;
        private float m_PlayFadeOutOverride;

        private float m_PlayEnterTimeOverride;
        private int m_PlayPriorityOverride;
        private bool m_PlayInterruptibleOverride = true;
        private bool m_ApplyTransitionRequestOverrides;

        private float m_PlaySpeed = 1f;
        private bool m_PlaybackPrefsLoaded;
        private readonly HashSet<KeyCode> m_PressedKeys = new();
        private readonly XAnimationPreviewPlaybackUiState m_PlaybackUiState = new();
        private readonly XAnimationPreviewUpdateCoordinator m_UpdateCoordinator = new();
        private bool m_IsDraggingPlaybackScrubber;
        private bool m_IsDraggingPlaybackOverlay;
        private bool m_IsDraggingFreeformBlendGraphOverlay;
        private bool m_PlaybackOverlayDragMoved;
        private bool m_FreeformBlendGraphOverlayDragMoved;
        private float m_PlaybackScrubberProgress;
        private float m_PlaybackScrubberDragStartX;
        private float m_PlaybackScrubberDragStartProgress;
        private Vector2 m_PlaybackOverlayDragStartPointer;
        private Vector2 m_PlaybackOverlayDragStartPosition;
        private Vector2 m_FreeformBlendGraphOverlayDragStartPointer;
        private Vector2 m_FreeformBlendGraphOverlayDragStartPosition;
        private FoldoutCard m_StatesCard;
        private FoldoutCard m_ClipsCard;
        private FoldoutCard m_ParametersCard;
        private FoldoutCard m_AutoTransitionCard;
        private FoldoutCard m_DefaultTransitionsCard;
        private FoldoutCard m_ChannelsCard;
        private VisualElement m_FreeformBlendGraphOverlay;
        private VisualElement m_FreeformBlendGraphOverlayHeader;
        private VisualElement m_FreeformBlendGraphOverlayContent;
        private XAnimationDirectionalBlendGraphElement m_FreeformBlendGraphElement;
        private XAnimationBlend1DGraphElement m_Blend1DGraphElement;
        private Label m_FreeformBlendGraphTitleLabel;
        private Label m_FreeformBlendGraphHintLabel;
        private VisualElement m_PlaybackOverlayCard;
        private string m_FreeformBlendGraphTitleText = "Blend Graph";
        private float m_FreeformBlendGraphLastExpandedContentHeight;

        private enum AutoTransitionTimelineDragMode
        {
            ExitTime,
            TransitionDuration,
            EnterTime,
        }

        private readonly struct PendingPlaybackRequest
        {
            public PendingPlaybackRequest(string stateKey, string clipKey, string channelName, float speed, XAnimationTransitionOptions transition)
            {
                StateKey = stateKey ?? string.Empty;
                ClipKey = clipKey ?? string.Empty;
                ChannelName = channelName ?? string.Empty;
                Speed = speed;
                Transition = transition;
            }

            public string StateKey { get; }
            public string ClipKey { get; }
            public string ChannelName { get; }
            public float Speed { get; }
            public XAnimationTransitionOptions Transition { get; }
            public bool IsStatePlayback => !string.IsNullOrWhiteSpace(StateKey);
            public bool IsClipPlayback => !string.IsNullOrWhiteSpace(ClipKey);
        }

        internal sealed class XAnimationPreviewPlaybackUiState
        {
            private readonly HashSet<string> m_LastPlayingClipKeys = new(StringComparer.Ordinal);
            private readonly HashSet<string> m_LastPlayingStateKeys = new(StringComparer.Ordinal);
            private bool m_LastHasPlayingChannels;
            private int m_LastCueLogCount = -1;
            private int m_LastCueLogVersion = -1;

            public bool StatePlaybackDirty { get; private set; }
            public bool ClipPlaybackDirty { get; private set; }
            public bool CueLogDirty { get; private set; }

            public void MarkStatePlaybackDirty() => StatePlaybackDirty = true;

            public void MarkClipPlaybackDirty() => ClipPlaybackDirty = true;

            public void MarkCueLogDirty() => CueLogDirty = true;

            public void MarkAllDirty()
            {
                StatePlaybackDirty = true;
                ClipPlaybackDirty = true;
                CueLogDirty = true;
            }

            public void ClearStatePlaybackDirty() => StatePlaybackDirty = false;

            public void ClearClipPlaybackDirty() => ClipPlaybackDirty = false;

            public void ClearCueLogDirty() => CueLogDirty = false;

            public void Reset()
            {
                m_LastPlayingClipKeys.Clear();
                m_LastPlayingStateKeys.Clear();
                m_LastHasPlayingChannels = false;
                m_LastCueLogCount = -1;
                m_LastCueLogVersion = -1;
                StatePlaybackDirty = false;
                ClipPlaybackDirty = false;
                CueLogDirty = false;
            }

            public bool ShouldRefreshCueLog(XAnimationEditorPreviewSession session, bool force)
            {
                int logCount = session?.CueLogs.Count ?? 0;
                int logVersion = session?.LogVersion ?? -1;
                if (!force && logCount == m_LastCueLogCount && logVersion == m_LastCueLogVersion)
                {
                    return false;
                }

                m_LastCueLogCount = logCount;
                m_LastCueLogVersion = logVersion;
                return true;
            }

            public void InvalidateCueLogSnapshot()
            {
                m_LastCueLogCount = -1;
                m_LastCueLogVersion = -1;
            }

            public void DetectPlaybackUiChanges(XAnimationEditorPreviewSession session)
            {
                if (session == null || !session.IsLoaded)
                {
                    if (m_LastHasPlayingChannels || m_LastPlayingClipKeys.Count > 0 || m_LastPlayingStateKeys.Count > 0)
                    {
                        m_LastHasPlayingChannels = false;
                        m_LastPlayingClipKeys.Clear();
                        m_LastPlayingStateKeys.Clear();
                        MarkStatePlaybackDirty();
                        MarkClipPlaybackDirty();
                    }

                    return;
                }

                HashSet<string> currentPlayingClipKeys = new(StringComparer.Ordinal);
                HashSet<string> currentPlayingStateKeys = new(StringComparer.Ordinal);
                bool hasPlayingChannels = false;

                IReadOnlyList<XAnimationCompiledChannel> channels = session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    XAnimationChannelState state = session.GetChannelState(channels[i].Name);
                    if (state == null)
                    {
                        continue;
                    }

                    hasPlayingChannels = true;
                    if (!string.IsNullOrEmpty(state.clipKey))
                    {
                        currentPlayingClipKeys.Add(state.clipKey);
                    }

                    if (!string.IsNullOrEmpty(state.stateKey))
                    {
                        currentPlayingStateKeys.Add(state.stateKey);
                    }
                }

                if (m_LastHasPlayingChannels != hasPlayingChannels || !SetEquals(m_LastPlayingClipKeys, currentPlayingClipKeys))
                {
                    m_LastHasPlayingChannels = hasPlayingChannels;
                    ReplaceSet(m_LastPlayingClipKeys, currentPlayingClipKeys);
                    MarkClipPlaybackDirty();
                }

                if (!SetEquals(m_LastPlayingStateKeys, currentPlayingStateKeys))
                {
                    ReplaceSet(m_LastPlayingStateKeys, currentPlayingStateKeys);
                    MarkStatePlaybackDirty();
                }
            }

            public void DetectCueLogChanges(XAnimationEditorPreviewSession session)
            {
                int logCount = session?.CueLogs.Count ?? 0;
                int logVersion = session?.LogVersion ?? -1;
                if (logCount != m_LastCueLogCount || logVersion != m_LastCueLogVersion)
                {
                    MarkCueLogDirty();
                }
            }

            private static bool SetEquals(HashSet<string> left, HashSet<string> right)
            {
                return left.Count == right.Count && left.SetEquals(right);
            }

            private static void ReplaceSet(HashSet<string> target, HashSet<string> source)
            {
                target.Clear();
                foreach (string item in source)
                {
                    target.Add(item);
                }
            }
        }

        internal readonly struct XAnimationPreviewUpdateResult
        {
            public XAnimationPreviewUpdateResult(
                bool shouldRenderPreview,
                bool shouldRefreshChannels,
                bool shouldRefreshPlaybackScrubber,
                bool shouldRefreshStatePlayback,
                bool shouldRefreshClipPlayback,
                bool shouldRefreshCueLog,
                bool forceCueLogRefresh)
            {
                ShouldRenderPreview = shouldRenderPreview;
                ShouldRefreshChannels = shouldRefreshChannels;
                ShouldRefreshPlaybackScrubber = shouldRefreshPlaybackScrubber;
                ShouldRefreshStatePlayback = shouldRefreshStatePlayback;
                ShouldRefreshClipPlayback = shouldRefreshClipPlayback;
                ShouldRefreshCueLog = shouldRefreshCueLog;
                ForceCueLogRefresh = forceCueLogRefresh;
            }

            public bool ShouldRenderPreview { get; }
            public bool ShouldRefreshChannels { get; }
            public bool ShouldRefreshPlaybackScrubber { get; }
            public bool ShouldRefreshStatePlayback { get; }
            public bool ShouldRefreshClipPlayback { get; }
            public bool ShouldRefreshCueLog { get; }
            public bool ForceCueLogRefresh { get; }
            public bool ShouldRepaint =>
                ShouldRefreshChannels ||
                ShouldRefreshPlaybackScrubber ||
                ShouldRefreshStatePlayback ||
                ShouldRefreshClipPlayback ||
                ShouldRefreshCueLog;
        }

        internal sealed class XAnimationPreviewUpdateCoordinator
        {
            private double m_LastActivePreviewUpdateTime;
            private bool m_WasPreviewVisible;

            public void Reset(double now, bool isPreviewVisible = false)
            {
                m_LastActivePreviewUpdateTime = now;
                m_WasPreviewVisible = isPreviewVisible;
            }

            public bool TryBuildUpdateResult(
                XAnimationPreviewWindow window,
                double now,
                out XAnimationPreviewUpdateResult result)
            {
                result = default;
                if (window.m_Session == null || !window.m_Session.IsLoaded)
                {
                    m_WasPreviewVisible = false;
                    return false;
                }

                bool isPreviewVisible = window.IsPreviewTabVisible();
                bool becameVisible = isPreviewVisible && !m_WasPreviewVisible;
                bool becameHidden = !isPreviewVisible && m_WasPreviewVisible;
                m_WasPreviewVisible = isPreviewVisible;

                if (!isPreviewVisible)
                {
                    if (becameHidden)
                    {
                        window.m_Session.Pause();
                    }

                    m_LastActivePreviewUpdateTime = now;
                    window.m_PressedKeys.Clear();
                    return false;
                }

                if (becameVisible)
                {
                    window.m_Session.SetPaused(window.m_IsPaused);
                }

                bool shouldUpdatePreview = becameVisible || now - m_LastActivePreviewUpdateTime >= ActivePreviewUpdateIntervalSeconds;
                bool shouldRenderPreview = false;
                bool shouldRefreshChannels = false;
                bool shouldRefreshPlaybackScrubber = false;
                bool didContinuousUiUpdate = false;

                if (shouldUpdatePreview)
                {
                    float deltaTime = (float)Math.Max(0d, now - m_LastActivePreviewUpdateTime);
                    m_LastActivePreviewUpdateTime = now;
                    bool hadSchedulerAdvance = !window.m_IsPaused && deltaTime > 0f;
                    bool didVisualUpdate = window.ProcessCameraMovement(deltaTime);

                    if (hadSchedulerAdvance || becameVisible)
                    {
                        window.m_Session.SyncPreviewFrame();
                        didVisualUpdate = true;
                    }

                    if (becameVisible || didVisualUpdate)
                    {
                        shouldRenderPreview = true;
                        shouldRefreshChannels = true;
                        shouldRefreshPlaybackScrubber = true;
                        didContinuousUiUpdate = true;
                        window.m_PlaybackUiState.DetectPlaybackUiChanges(window.m_Session);
                        window.m_PlaybackUiState.DetectCueLogChanges(window.m_Session);
                    }
                }

                bool shouldRefreshStatePlayback = becameVisible || didContinuousUiUpdate || window.m_PlaybackUiState.StatePlaybackDirty;
                bool shouldRefreshClipPlayback = becameVisible || didContinuousUiUpdate || window.m_PlaybackUiState.ClipPlaybackDirty;
                bool shouldRefreshCueLog = becameVisible || window.m_PlaybackUiState.CueLogDirty;
                if (!shouldRefreshChannels &&
                    !shouldRefreshPlaybackScrubber &&
                    !shouldRefreshStatePlayback &&
                    !shouldRefreshClipPlayback &&
                    !shouldRefreshCueLog)
                {
                    return false;
                }

                result = new XAnimationPreviewUpdateResult(
                    shouldRenderPreview,
                    shouldRefreshChannels,
                    shouldRefreshPlaybackScrubber,
                    shouldRefreshStatePlayback,
                    shouldRefreshClipPlayback,
                    shouldRefreshCueLog,
                    becameVisible || window.m_PlaybackUiState.CueLogDirty);
                return true;
            }
        }

        private sealed class StringInputPromptWindow : EditorWindow
        {
            private string m_Message;
            private string m_Value;
            private string m_SelectedOption;
            private string[] m_Options;
            private string m_OptionsLabel;
            private bool m_Confirmed;

            public static bool ShowPrompt(string title, string message, string initialValue, out string value)
            {
                StringInputPromptWindow window = CreateInstance<StringInputPromptWindow>();
                window.titleContent = new GUIContent(title);
                window.minSize = new Vector2(360f, 92f);
                window.maxSize = new Vector2(360f, 92f);
                window.m_Message = message ?? string.Empty;
                window.m_Value = initialValue ?? string.Empty;
                window.position = new Rect(
                    (Screen.currentResolution.width - 360f) * 0.5f,
                    (Screen.currentResolution.height - 92f) * 0.5f,
                    360f,
                    92f);
                window.ShowModalUtility();
                value = window.m_Confirmed ? window.m_Value : null;
                return window.m_Confirmed;
            }

            public static bool ShowPrompt(
                string title,
                string message,
                string initialValue,
                string optionsLabel,
                string[] options,
                string initialOption,
                out string value,
                out string selectedOption)
            {
                StringInputPromptWindow window = CreateInstance<StringInputPromptWindow>();
                window.titleContent = new GUIContent(title);
                window.minSize = new Vector2(420f, 126f);
                window.maxSize = new Vector2(420f, 126f);
                window.m_Message = message ?? string.Empty;
                window.m_Value = initialValue ?? string.Empty;
                window.m_OptionsLabel = optionsLabel ?? string.Empty;
                window.m_Options = options ?? Array.Empty<string>();
                window.m_SelectedOption = initialOption ?? string.Empty;
                window.position = new Rect(
                    (Screen.currentResolution.width - 420f) * 0.5f,
                    (Screen.currentResolution.height - 126f) * 0.5f,
                    420f,
                    126f);
                window.ShowModalUtility();
                value = window.m_Confirmed ? window.m_Value : null;
                selectedOption = window.m_Confirmed ? window.m_SelectedOption : null;
                return window.m_Confirmed;
            }

            private void OnGUI()
            {
                GUILayout.Space(8);
                EditorGUILayout.LabelField(m_Message, EditorStyles.wordWrappedLabel);
                GUI.SetNextControlName("InputField");
                m_Value = EditorGUILayout.TextField(m_Value);
                if (m_Options != null && m_Options.Length > 0)
                {
                    int selectedIndex = Array.IndexOf(m_Options, m_SelectedOption ?? string.Empty);
                    if (selectedIndex < 0)
                    {
                        selectedIndex = 0;
                    }

                    selectedIndex = EditorGUILayout.Popup(m_OptionsLabel, selectedIndex, m_Options);
                    m_SelectedOption = m_Options[selectedIndex];
                }

                GUILayout.FlexibleSpace();
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("取消", GUILayout.Width(72)))
                    {
                        Close();
                    }

                    if (GUILayout.Button("确定", GUILayout.Width(72)))
                    {
                        m_Confirmed = true;
                        m_Value = m_Value?.Trim() ?? string.Empty;
                        Close();
                    }
                }

                if (UEvent.current.type == EventType.Repaint)
                {
                    EditorGUI.FocusTextInControl("InputField");
                }
            }
        }

        private sealed class SearchableSelectionWindow : EditorWindow
        {
            private readonly List<SearchableSelectionItem> m_AllItems = new();
            private readonly List<SearchableSelectionItem> m_FilteredItems = new();
            private readonly Stack<string> m_GroupStack = new();
            private Action<string> m_OnSelected;
            private string m_Query = string.Empty;
            private string m_CurrentValue = string.Empty;
            private Vector2 m_ScrollPosition;
            private int m_SelectedIndex;

            public static void Show(
                Rect activatorRect,
                string title,
                string currentValue,
                IReadOnlyList<SearchableSelectionItem> items,
                Action<string> onSelected)
            {
                SearchableSelectionWindow window = CreateInstance<SearchableSelectionWindow>();
                window.titleContent = new GUIContent(title ?? "Select");
                window.m_CurrentValue = currentValue ?? string.Empty;
                window.m_OnSelected = onSelected;
                window.m_AllItems.Clear();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        window.m_AllItems.Add(items[i]);
                    }
                }

                window.ApplyFilter();
                Vector2 size = new(320f, 360f);
                window.ShowAsDropDown(activatorRect, size);
            }

            private void OnEnable()
            {
                wantsMouseMove = true;
            }

            private void OnGUI()
            {
                HandleKeyboard();

                GUILayout.Space(6);
                GUI.SetNextControlName("SelectionSearchField");
                string nextQuery = EditorGUILayout.TextField(m_Query ?? string.Empty);
                if (!string.Equals(nextQuery, m_Query, StringComparison.Ordinal))
                {
                    m_Query = nextQuery ?? string.Empty;
                    ApplyFilter();
                }

                if (UEvent.current.type == EventType.Repaint)
                {
                    EditorGUI.FocusTextInControl("SelectionSearchField");
                }

                GUILayout.Space(4);
                m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
                if (m_FilteredItems.Count == 0)
                {
                    EditorGUILayout.LabelField("No results", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(22f));
                }
                else
                {
                    for (int i = 0; i < m_FilteredItems.Count; i++)
                    {
                        DrawItemRow(m_FilteredItems[i], i);
                    }
                }

                EditorGUILayout.EndScrollView();
            }

            private void HandleKeyboard()
            {
                UEvent evt = UEvent.current;
                if (evt == null || evt.type != EventType.KeyDown)
                {
                    return;
                }

                if (evt.keyCode == KeyCode.DownArrow)
                {
                    if (m_FilteredItems.Count > 0)
                    {
                        m_SelectedIndex = Mathf.Clamp(m_SelectedIndex + 1, 0, m_FilteredItems.Count - 1);
                    }
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.UpArrow)
                {
                    if (m_FilteredItems.Count > 0)
                    {
                        m_SelectedIndex = Mathf.Clamp(m_SelectedIndex - 1, 0, m_FilteredItems.Count - 1);
                    }
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (m_FilteredItems.Count > 0 && m_SelectedIndex >= 0 && m_SelectedIndex < m_FilteredItems.Count)
                    {
                        ActivateItem(m_FilteredItems[m_SelectedIndex]);
                    }
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.Use();
                }
            }

            private void ApplyFilter()
            {
                m_FilteredItems.Clear();
                string query = m_Query?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(query))
                {
                    string currentGroup = m_GroupStack.Count > 0 ? m_GroupStack.Peek() : string.Empty;
                    if (m_GroupStack.Count > 0)
                    {
                        m_FilteredItems.Add(new SearchableSelectionItem(string.Empty, "..", "Back", "back previous", isGroup: true));
                    }

                    HashSet<string> addedGroups = new(StringComparer.Ordinal);
                    for (int i = 0; i < m_AllItems.Count; i++)
                    {
                        SearchableSelectionItem item = m_AllItems[i];
                        if (m_GroupStack.Count == 0)
                        {
                            if (string.IsNullOrWhiteSpace(item.GroupKey))
                            {
                                m_FilteredItems.Add(item);
                            }
                            else if (addedGroups.Add(item.GroupKey))
                            {
                                m_FilteredItems.Add(new SearchableSelectionItem(string.Empty, item.GroupKey, "Open group", item.GroupKey, item.GroupKey, true));
                            }
                        }
                        else if (string.Equals(item.GroupKey, currentGroup, StringComparison.Ordinal))
                        {
                            string childTitle = BuildGroupedChildTitle(item);
                            m_FilteredItems.Add(new SearchableSelectionItem(item.Value, childTitle, item.Detail, item.SearchText, item.GroupKey, item.IsGroup));
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < m_AllItems.Count; i++)
                    {
                        SearchableSelectionItem item = m_AllItems[i];
                        if (item.IsGroup)
                        {
                            continue;
                        }

                        if (item.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Detail.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            m_FilteredItems.Add(item);
                        }
                    }
                }

                m_SelectedIndex = 0;
                for (int i = 0; i < m_FilteredItems.Count; i++)
                {
                    if (string.Equals(m_FilteredItems[i].Value, m_CurrentValue, StringComparison.Ordinal))
                    {
                        m_SelectedIndex = i;
                        break;
                    }
                }
            }

            private void DrawItemRow(SearchableSelectionItem item, int index)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, string.IsNullOrWhiteSpace(item.Detail) ? 22f : 36f);
                bool selected = index == m_SelectedIndex;
                if (selected)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.38f, 0.62f, 0.9f));
                }
                else if (UEvent.current.type == EventType.Repaint && index % 2 == 0)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.20f, 0.20f, 0.22f, 0.35f));
                }

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    ActivateItem(item);
                    return;
                }

                string displayTitle = item.IsGroup ? $"{item.Title}  >" : item.Title;
                Rect titleRect = new(rowRect.x + 8f, rowRect.y + 3f, rowRect.width - 16f, 16f);
                EditorGUI.LabelField(titleRect, displayTitle, EditorStyles.boldLabel);
                if (!string.IsNullOrWhiteSpace(item.Detail))
                {
                    Rect detailRect = new(rowRect.x + 8f, rowRect.y + 18f, rowRect.width - 16f, 14f);
                    GUIStyle detailStyle = new(EditorStyles.miniLabel);
                    detailStyle.normal.textColor = new Color(0.72f, 0.72f, 0.74f, 1f);
                    EditorGUI.LabelField(detailRect, item.Detail, detailStyle);
                }
            }

            private void SelectItem(string value)
            {
                m_OnSelected?.Invoke(value ?? string.Empty);
                Close();
            }

            private void ActivateItem(SearchableSelectionItem item)
            {
                if (item.IsGroup)
                {
                    if (string.Equals(item.Title, "..", StringComparison.Ordinal))
                    {
                        if (m_GroupStack.Count > 0)
                        {
                            m_GroupStack.Pop();
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(item.GroupKey))
                    {
                        m_GroupStack.Push(item.GroupKey);
                    }

                    ApplyFilter();
                    return;
                }

                SelectItem(item.Value);
            }

            private static string BuildGroupedChildTitle(SearchableSelectionItem item)
            {
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    return string.Empty;
                }

                int slashIndex = item.Title.LastIndexOf('/');
                if (slashIndex >= 0 && slashIndex + 1 < item.Title.Length)
                {
                    return item.Title[(slashIndex + 1)..].Trim();
                }

                return item.Title;
            }
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

        public static XAnimationPreviewWindow ShowWindowAndPlayState(
            TextAsset animationAsset,
            GameObject prefab,
            string stateKey,
            float speed,
            XAnimationTransitionOptions transition = null)
        {
            XAnimationPreviewWindow window = ShowWindow(animationAsset, prefab, autoLoad: true);
            window.SetPendingPlaybackRequest(new PendingPlaybackRequest(stateKey, null, null, speed, CloneTransitionOptions(transition)));
            return window;
        }

        public static XAnimationPreviewWindow ShowWindowAndPlayClip(
            TextAsset animationAsset,
            GameObject prefab,
            string clipKey,
            string channelName,
            float speed,
            XAnimationTransitionOptions transition = null)
        {
            XAnimationPreviewWindow window = ShowWindow(animationAsset, prefab, autoLoad: true);
            window.SetPendingPlaybackRequest(new PendingPlaybackRequest(null, clipKey, channelName, speed, CloneTransitionOptions(transition)));
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
            m_UpdateCoordinator.Reset(EditorApplication.timeSinceStartup, IsPreviewTabVisible());
            MarkEventUiDirty();
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            DisposeSession();
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

        private static XAnimationTransitionOptions CloneTransitionOptions(XAnimationTransitionOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new XAnimationTransitionOptions
            {
                fadeIn = options.fadeIn,
                fadeOut = options.fadeOut,
                enterTime = options.enterTime,
                priority = options.priority,
                interruptible = options.interruptible,
            };
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
