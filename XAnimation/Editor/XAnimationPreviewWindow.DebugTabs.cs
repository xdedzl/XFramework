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
using static XFramework.Editor.XAnimationEditorParameterUtility;
using static XFramework.Editor.XAnimationEditorUi;

namespace XFramework.Editor
{
    public sealed partial class XAnimationPreviewWindow
    {
        private VisualElement BuildDebugToolbar()
        {
            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 4;
            toolbar.style.paddingLeft = 2;
            toolbar.style.paddingRight = 2;
            toolbar.style.paddingTop = 2;
            toolbar.style.paddingBottom = 0;
            toolbar.style.backgroundColor = ToolbarBg;
            toolbar.style.borderTopLeftRadius = 3;
            toolbar.style.borderTopRightRadius = 3;
            toolbar.style.borderBottomLeftRadius = 0;
            toolbar.style.borderBottomRightRadius = 0;
            toolbar.style.borderTopWidth = 0;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderLeftWidth = 0;
            toolbar.style.borderRightWidth = 0;
            toolbar.style.borderBottomColor = SectionDivider;
            toolbar.style.justifyContent = Justify.FlexStart;
            toolbar.style.height = 30;

            VisualElement tabContainer = new();
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.alignItems = Align.Center;
            tabContainer.style.flexGrow = 1;
            tabContainer.style.flexShrink = 1;
            tabContainer.style.minWidth = 0;
            tabContainer.style.overflow = Overflow.Hidden;
            toolbar.Add(tabContainer);

            m_SettingGroupButton = CreateToolbarTabButton("Setting", () => SetDebugToolbarGroup(DebugToolbarGroup.Setting));
            m_MainGroupButton = CreateToolbarTabButton("State", () => SetDebugToolbarGroup(DebugToolbarGroup.Main));
            m_ClipGroupButton = CreateToolbarTabButton("Clips", () => SetDebugToolbarGroup(DebugToolbarGroup.Clip));
            m_ChannelsGroupButton = CreateToolbarTabButton("Channels", () => SetDebugToolbarGroup(DebugToolbarGroup.Channels));
            m_ParametersGroupButton = CreateToolbarTabButton("Parameters", () => SetDebugToolbarGroup(DebugToolbarGroup.Parameters));

            tabContainer.Add(m_SettingGroupButton);
            tabContainer.Add(CreateToolbarDivider());
            tabContainer.Add(m_MainGroupButton);
            tabContainer.Add(CreateToolbarDivider());
            tabContainer.Add(m_ClipGroupButton);
            tabContainer.Add(CreateToolbarDivider());
            tabContainer.Add(m_ChannelsGroupButton);
            tabContainer.Add(CreateToolbarDivider());
            tabContainer.Add(m_ParametersGroupButton);

            VisualElement searchDivider = CreateToolbarDivider();
            searchDivider.style.marginLeft = 2;
            searchDivider.style.marginRight = 2;
            searchDivider.style.flexShrink = 0;
            toolbar.Add(searchDivider);

            m_SearchButton = CreateToolbarActionButton(string.Empty, ToggleSearchPopup);
            m_SearchButton.tooltip = "打开搜索面板，搜索 state、clip、transition、cue、parameter、channel。";
            ApplyToolbarButtonIcon(m_SearchButton, "d_Search Icon", "Search Icon", "d_ViewToolZoom", "ViewToolZoom");
            m_SearchButton.style.width = 26;
            m_SearchButton.style.minWidth = 26;
            m_SearchButton.style.maxWidth = 26;
            m_SearchButton.style.height = 26;
            m_SearchButton.style.minHeight = 26;
            m_SearchButton.style.maxHeight = 26;
            m_SearchButton.style.paddingLeft = 0;
            m_SearchButton.style.paddingRight = 0;
            m_SearchButton.style.paddingTop = 0;
            m_SearchButton.style.paddingBottom = 0;
            m_SearchButton.style.alignSelf = Align.Center;
            m_SearchButton.style.flexShrink = 0;
            toolbar.Add(m_SearchButton);

            m_SearchField = new TextField();
            m_SearchField.label = string.Empty;
            m_SearchField.tooltip = "搜索 state、clip、transition、cue、parameter、channel。";
            m_SearchField.style.width = Length.Percent(100);
            m_SearchField.style.minWidth = 0;
            m_SearchField.style.height = 24;
            m_SearchField.style.marginTop = 0;
            m_SearchField.style.marginBottom = 0;
            m_SearchField.style.backgroundColor = PaneBg;
            m_SearchField.style.borderTopWidth = 1;
            m_SearchField.style.borderBottomWidth = 1;
            m_SearchField.style.borderLeftWidth = 1;
            m_SearchField.style.borderRightWidth = 1;
            m_SearchField.style.borderTopColor = SectionDivider;
            m_SearchField.style.borderBottomColor = SectionDivider;
            m_SearchField.style.borderLeftColor = SectionDivider;
            m_SearchField.style.borderRightColor = SectionDivider;
            m_SearchField.style.borderTopLeftRadius = 3;
            m_SearchField.style.borderTopRightRadius = 3;
            m_SearchField.style.borderBottomLeftRadius = 3;
            m_SearchField.style.borderBottomRightRadius = 3;
            m_SearchField.RegisterValueChangedCallback(evt => RefreshSearchResults(evt.newValue));
            m_SearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                m_SearchField?.schedule.Execute(() =>
                {
                    if (m_SearchField == null || m_SearchResultsPopup == null)
                    {
                        return;
                    }

                    VisualElement focusedElement = m_SearchField.panel?.focusController?.focusedElement as VisualElement;
                    if (focusedElement == null || !m_SearchResultsPopup.Contains(focusedElement))
                    {
                        HideSearchResults();
                    }
                }).ExecuteLater(80);
            });
            m_SearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (!string.IsNullOrWhiteSpace(m_SearchField?.value))
                {
                    RefreshSearchResults(m_SearchField.value);
                }
            });
            m_SearchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    HideSearchResults();
                    evt.StopPropagation();
                }
            });
            m_SearchButton.RegisterCallback<GeometryChangedEvent>(_ => UpdateSearchResultsPopupPosition());

            m_SearchResultsPopup = new VisualElement();
            m_SearchResultsPopup.style.position = Position.Absolute;
            m_SearchResultsPopup.style.left = 0;
            m_SearchResultsPopup.style.top = 0;
            m_SearchResultsPopup.style.width = 340;
            m_SearchResultsPopup.style.maxHeight = 320;
            m_SearchResultsPopup.style.paddingLeft = 8;
            m_SearchResultsPopup.style.paddingRight = 8;
            m_SearchResultsPopup.style.paddingTop = 8;
            m_SearchResultsPopup.style.paddingBottom = 8;
            m_SearchResultsPopup.style.backgroundColor = PaneBg;
            m_SearchResultsPopup.style.borderTopWidth = 1;
            m_SearchResultsPopup.style.borderBottomWidth = 1;
            m_SearchResultsPopup.style.borderLeftWidth = 1;
            m_SearchResultsPopup.style.borderRightWidth = 1;
            m_SearchResultsPopup.style.borderTopColor = SectionDivider;
            m_SearchResultsPopup.style.borderBottomColor = SectionDivider;
            m_SearchResultsPopup.style.borderLeftColor = SectionDivider;
            m_SearchResultsPopup.style.borderRightColor = SectionDivider;
            m_SearchResultsPopup.style.borderTopLeftRadius = 4;
            m_SearchResultsPopup.style.borderTopRightRadius = 4;
            m_SearchResultsPopup.style.borderBottomLeftRadius = 4;
            m_SearchResultsPopup.style.borderBottomRightRadius = 4;
            m_SearchResultsPopup.style.display = DisplayStyle.None;
            m_SearchResultsPopup.style.unityOverflowClipBox = OverflowClipBox.PaddingBox;
            m_SearchResultsPopup.pickingMode = PickingMode.Position;

            Label searchPopupTitle = new("Search");
            searchPopupTitle.style.color = TextNormal;
            searchPopupTitle.style.fontSize = BodyFontSize;
            searchPopupTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            searchPopupTitle.style.marginBottom = 6;
            m_SearchResultsPopup.Add(searchPopupTitle);

            m_SearchResultsPopup.Add(m_SearchField);

            ScrollView searchScroll = new();
            searchScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            searchScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            searchScroll.style.maxHeight = 320;
            searchScroll.style.flexGrow = 1;
            searchScroll.style.marginTop = 6;
            m_SearchResultsPopup.Add(searchScroll);

            m_SearchResultsList = new VisualElement();
            searchScroll.Add(m_SearchResultsList);

            return toolbar;
        }

        private static VisualElement CreateToolbarDivider()
        {
            VisualElement divider = new();
            divider.style.width = 1;
            divider.style.minWidth = 1;
            divider.style.height = 16;
            divider.style.alignSelf = Align.Center;
            divider.style.backgroundColor = SectionDivider;
            return divider;
        }

        private Button CreateToolbarActionButton(string label, Action onClick)
        {
            Button button = new(onClick)
            {
                text = label
            };
            button.style.marginRight = 1;
            button.style.marginBottom = 0;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.paddingTop = 3;
            button.style.paddingBottom = 4;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopColor = SectionDivider;
            button.style.borderBottomColor = SectionDivider;
            button.style.borderLeftColor = SectionDivider;
            button.style.borderRightColor = SectionDivider;
            button.style.color = TextNormal;
            button.style.backgroundColor = new Color(0.18f, 0.24f, 0.34f, 1f);
            button.style.fontSize = BodyFontSize;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.flexShrink = 0;
            button.style.justifyContent = Justify.Center;
            button.style.alignItems = Align.Center;
            return button;
        }

        private Button CreateToolbarTabButton(string label, Action onClick)
        {
            Button button = new(onClick)
            {
                text = label
            };
            button.style.marginRight = 0;
            button.style.marginBottom = 0;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            button.style.paddingTop = 3;
            button.style.paddingBottom = 4;
            button.style.borderTopLeftRadius = 0;
            button.style.borderTopRightRadius = 0;
            button.style.borderBottomLeftRadius = 0;
            button.style.borderBottomRightRadius = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.color = TextMuted;
            button.style.backgroundColor = ToolbarBg;
            button.style.fontSize = BodyFontSize;
            button.style.unityFontStyleAndWeight = FontStyle.Normal;
            button.style.flexShrink = 0;
            return button;
        }

        private void SetDebugToolbarGroup(DebugToolbarGroup group)
        {
            if (m_SelectedDebugToolbarGroup == group &&
                m_SettingGroupContainer != null &&
                m_MainGroupContainer != null &&
                m_ClipGroupContainer != null &&
                m_ChannelsGroupContainer != null &&
                m_ParametersGroupContainer != null)
            {
                ApplyDebugToolbarGroup();
                return;
            }

            m_SelectedDebugToolbarGroup = group;
            ApplyDebugToolbarGroup();
        }

        private void ApplyDebugToolbarGroup()
        {
            if (m_SettingGroupContainer == null || m_MainGroupContainer == null || m_ClipGroupContainer == null || m_ChannelsGroupContainer == null || m_ParametersGroupContainer == null)
            {
                return;
            }

            m_SettingGroupContainer.style.display = m_SelectedDebugToolbarGroup == DebugToolbarGroup.Setting ? DisplayStyle.Flex : DisplayStyle.None;
            m_MainGroupContainer.style.display = m_SelectedDebugToolbarGroup == DebugToolbarGroup.Main ? DisplayStyle.Flex : DisplayStyle.None;
            m_ClipGroupContainer.style.display = m_SelectedDebugToolbarGroup == DebugToolbarGroup.Clip ? DisplayStyle.Flex : DisplayStyle.None;
            m_ChannelsGroupContainer.style.display = m_SelectedDebugToolbarGroup == DebugToolbarGroup.Channels ? DisplayStyle.Flex : DisplayStyle.None;
            m_ParametersGroupContainer.style.display = m_SelectedDebugToolbarGroup == DebugToolbarGroup.Parameters ? DisplayStyle.Flex : DisplayStyle.None;

            ApplyToolbarTabVisual(m_SettingGroupButton, m_SelectedDebugToolbarGroup == DebugToolbarGroup.Setting);
            ApplyToolbarTabVisual(m_MainGroupButton, m_SelectedDebugToolbarGroup == DebugToolbarGroup.Main);
            ApplyToolbarTabVisual(m_ClipGroupButton, m_SelectedDebugToolbarGroup == DebugToolbarGroup.Clip);
            ApplyToolbarTabVisual(m_ChannelsGroupButton, m_SelectedDebugToolbarGroup == DebugToolbarGroup.Channels);
            ApplyToolbarTabVisual(m_ParametersGroupButton, m_SelectedDebugToolbarGroup == DebugToolbarGroup.Parameters);
        }

        private void RefreshSearchIndex()
        {
            m_SearchEntries.Clear();

            if (m_Session == null || !m_Session.IsLoaded)
            {
                RefreshSearchResults(m_SearchField?.value);
                return;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (state == null)
                {
                    continue;
                }

                string stateKey = state.Key;
                string groupName = NormalizeStateEditorGroupName(state.Config.editorGroupName);
                string groupSearchText = string.IsNullOrWhiteSpace(groupName) ? string.Empty : $" group={groupName}";
                AddSearchEntry(
                    SearchEntryType.State,
                    stateKey,
                    $"{state.StateType} | channel={state.Config.channelName}{groupSearchText}",
                    $"{stateKey} {state.Config.channelName} {groupName} {state.Config.clipKey} {state.Config.parameterName}",
                    () => FocusStateInInspector(stateKey));
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            XAnimationCueConfig[] cues = m_Session.CompiledAsset.Asset.cues ?? Array.Empty<XAnimationCueConfig>();
            for (int i = 0; i < clips.Count; i++)
            {
                XAnimationCompiledClip clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                string clipKey = clip.Key;
                string clipPath = clip.Config.clipPath ?? string.Empty;
                string groupName = NormalizeClipEditorGroupName(clip.Config.editorGroupName);
                string groupDetail = string.IsNullOrWhiteSpace(groupName) ? string.Empty : $" | group={groupName}";
                AddSearchEntry(
                    SearchEntryType.Clip,
                    clipKey,
                    string.IsNullOrWhiteSpace(clipPath) ? $"clip{groupDetail}" : $"{clipPath}{groupDetail}",
                    $"{clipKey} {clipPath} {clip.Clip?.name} {groupName}",
                    () => FocusClipInInspector(clipKey));

                for (int cueIndex = 0; cueIndex < cues.Length; cueIndex++)
                {
                    XAnimationCueConfig cue = cues[cueIndex];
                    if (cue == null || !string.Equals(cue.clipKey, clipKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string cueKey = BuildCueSearchKey(clipKey, cueIndex);
                    string cueTitle = string.IsNullOrWhiteSpace(cue.eventKey)
                        ? $"{clipKey} @ {cue.time:0.###}"
                        : $"{cue.eventKey} @ {cue.time:0.###}";
                    string cueDetail = $"{clipKey} | payload={cue.payload}";
                    AddSearchEntry(
                        SearchEntryType.Cue,
                        cueTitle,
                        cueDetail,
                        $"{clipKey} {cue.eventKey} {cue.payload} {cue.time:0.###}",
                        () => FocusCueInInspector(cueKey, clipKey));
                }

                List<DisplayedCueEntry> derivedCues = CollectDerivedClipCues(clip);
                for (int derivedIndex = 0; derivedIndex < derivedCues.Count; derivedIndex++)
                {
                    DisplayedCueEntry cue = derivedCues[derivedIndex];
                    string cueKey = BuildDerivedCueSearchKey(clipKey, derivedIndex);
                    string cueTitle = string.IsNullOrWhiteSpace(cue.EventKey)
                        ? $"{clipKey} evt @ {cue.Time:0.###}"
                        : $"{cue.EventKey} @ {cue.Time:0.###}";
                    string cueDetail = $"{clipKey} | Animation Event | payload={cue.Payload}";
                    AddSearchEntry(
                        SearchEntryType.Cue,
                        cueTitle,
                        cueDetail,
                        $"{clipKey} {cue.EventKey} {cue.Payload} {cue.Time:0.###} animation event",
                        () => FocusCueInInspector(cueKey, clipKey));
                }
            }

            IReadOnlyList<XAnimationCompiledAutoTransition> transitions = m_Session.CompiledAsset.AutoTransitions;
            for (int i = 0; i < transitions.Count; i++)
            {
                XAnimationCompiledAutoTransition transition = transitions[i];
                if (transition == null)
                {
                    continue;
                }

                string preStateKey = transition.PreStateKey;
                string nextStateKey = string.IsNullOrWhiteSpace(transition.Config.nextStateKey) ? "None" : transition.Config.nextStateKey;
                string detail = $"{preStateKey} -> {nextStateKey} | exit={transition.Config.exitTime:0.###} | duration={transition.Config.transitionDuration:0.###}";
                AddSearchEntry(
                    SearchEntryType.Transition,
                    preStateKey,
                    detail,
                    $"{preStateKey} {transition.Config.nextStateKey} transition auto exit enter duration",
                    () => FocusAutoTransitionInInspector(preStateKey));
            }

            IReadOnlyList<XAnimationCompiledDefaultTransition> defaultTransitions = m_Session.CompiledAsset.DefaultTransitions;
            for (int i = 0; i < defaultTransitions.Count; i++)
            {
                XAnimationCompiledDefaultTransition transition = defaultTransitions[i];
                if (transition == null)
                {
                    continue;
                }

                string title = string.IsNullOrWhiteSpace(transition.EditorName)
                    ? $"Default Transition {i + 1}"
                    : transition.EditorName;
                string pairs = FormatDefaultTransitionPairSummary(transition.Config);
                string detail = $"{pairs} | fadeIn={transition.Config.fadeIn:0.###} | fadeOut={transition.Config.fadeOut:0.###}";
                int transitionIndex = i;
                AddSearchEntry(
                    SearchEntryType.Transition,
                    title,
                    detail,
                    $"{title} {pairs} transition default fade enter priority interruptible",
                    () => FocusDefaultTransitionInInspector(transitionIndex));
            }

            IReadOnlyList<XAnimationCompiledParameter> parameters = m_Session.CompiledAsset.Parameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                XAnimationCompiledParameter parameter = parameters[i];
                if (parameter == null)
                {
                    continue;
                }

                string parameterName = parameter.Name;
                AddSearchEntry(
                    SearchEntryType.Parameter,
                    parameterName,
                    $"{parameter.Type} | default={parameter.Config.defaultValue}",
                    $"{parameterName} {parameter.Type} {parameter.Config.defaultValue}",
                    () => FocusParameterInInspector(parameterName));
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = channels[i];
                if (channel == null)
                {
                    continue;
                }

                string channelName = channel.Name;
                AddSearchEntry(
                    SearchEntryType.Channel,
                    channelName,
                    $"{channel.Config.layerType} | weight={channel.Config.defaultWeight:0.###}",
                    $"{channelName} {channel.Config.layerType} {channel.Config.maskPath}",
                    () => FocusChannelInInspector(channelName));
            }

            RefreshSearchResults(m_SearchField?.value);
        }

        private void AddSearchEntry(SearchEntryType type, string title, string detail, string searchText, Action navigate)
        {
            m_SearchEntries.Add(new SearchEntry(type, title, detail, searchText, navigate));
        }

        private void RefreshSearchResults(string query)
        {
            if (m_SearchResultsList == null || m_SearchResultsPopup == null)
            {
                return;
            }

            m_SearchResultsList.Clear();
            m_VisibleSearchEntries.Clear();

            string normalizedQuery = query?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                HideSearchResults();
                return;
            }

            List<(SearchEntry Entry, int Score)> matches = new();
            for (int i = 0; i < m_SearchEntries.Count; i++)
            {
                SearchEntry entry = m_SearchEntries[i];
                int titleIndex = entry.Title.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
                int detailIndex = entry.Detail.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
                int searchIndex = entry.SearchText.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
                int score = titleIndex >= 0
                    ? titleIndex
                    : detailIndex >= 0
                        ? detailIndex + 100
                        : searchIndex >= 0
                            ? searchIndex + 200
                            : -1;
                if (score < 0)
                {
                    continue;
                }

                matches.Add((entry, score));
            }

            matches.Sort((left, right) =>
            {
                int scoreCompare = left.Score.CompareTo(right.Score);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }

                int typeCompare = left.Entry.Type.CompareTo(right.Entry.Type);
                if (typeCompare != 0)
                {
                    return typeCompare;
                }

                return string.Compare(left.Entry.Title, right.Entry.Title, StringComparison.OrdinalIgnoreCase);
            });

            int maxCount = Mathf.Min(18, matches.Count);
            for (int i = 0; i < maxCount; i++)
            {
                SearchEntry entry = matches[i].Entry;
                m_VisibleSearchEntries.Add(entry);
                m_SearchResultsList.Add(CreateSearchResultRow(entry, i));
            }

            if (m_VisibleSearchEntries.Count == 0)
            {
                Label emptyLabel = new("No results");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.paddingLeft = 8;
                emptyLabel.style.paddingRight = 8;
                emptyLabel.style.paddingTop = 6;
                emptyLabel.style.paddingBottom = 6;
                m_SearchResultsList.Add(emptyLabel);
            }

            UpdateSearchResultsPopupPosition();
            m_SearchResultsPopup.style.display = DisplayStyle.Flex;
            m_SearchResultsPopup.BringToFront();
        }

        private VisualElement CreateSearchResultRow(SearchEntry entry, int rowIndex)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Column;
            row.style.paddingLeft = 8;
            row.style.paddingRight = 8;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.backgroundColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = SectionDivider;

            Label title = new($"[{GetSearchEntryTypeLabel(entry.Type)}] {entry.Title}");
            title.style.color = TextNormal;
            title.style.fontSize = BodyFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(title);

            if (!string.IsNullOrWhiteSpace(entry.Detail))
            {
                Label detail = new(entry.Detail);
                detail.style.color = TextMuted;
                detail.style.fontSize = 10;
                detail.style.whiteSpace = WhiteSpace.Normal;
                row.Add(detail);
            }

            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = HoverBg);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg);
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                HideSearchResults();
                entry.Navigate?.Invoke();
                evt.StopPropagation();
            });
            return row;
        }

        private void HideSearchResults()
        {
            if (m_SearchResultsPopup != null)
            {
                m_SearchResultsPopup.style.display = DisplayStyle.None;
            }

            m_SearchField?.Blur();
        }

        private void ToggleSearchPopup()
        {
            if (m_SearchResultsPopup == null)
            {
                return;
            }

            bool isVisible = m_SearchResultsPopup.style.display == DisplayStyle.Flex;
            if (isVisible)
            {
                HideSearchResults();
                return;
            }

            UpdateSearchResultsPopupPosition();
            m_SearchResultsPopup.style.display = DisplayStyle.Flex;
            m_SearchResultsPopup.BringToFront();
            m_SearchField?.Focus();
            if (!string.IsNullOrWhiteSpace(m_SearchField?.value))
            {
                RefreshSearchResults(m_SearchField.value);
            }
        }

        private void UpdateSearchResultsPopupPosition()
        {
            if (m_SearchButton == null || m_SearchResultsPopup == null || m_InspectorOverlayLayer == null)
            {
                return;
            }

            Rect buttonWorld = m_SearchButton.worldBound;
            Rect overlayWorld = m_InspectorOverlayLayer.worldBound;
            if (buttonWorld.width <= 0f || overlayWorld.width <= 0f)
            {
                return;
            }

            float left = Mathf.Max(0f, buttonWorld.xMin - overlayWorld.xMin - 220f);
            float top = Mathf.Max(0f, buttonWorld.yMax - overlayWorld.yMin + 2f);
            float width = Mathf.Min(320f, Mathf.Max(220f, overlayWorld.width - left));
            m_SearchResultsPopup.style.left = left;
            m_SearchResultsPopup.style.top = top;
            m_SearchResultsPopup.style.width = width;
        }

        private static string GetSearchEntryTypeLabel(SearchEntryType type)
        {
            return type switch
            {
                SearchEntryType.State => "State",
                SearchEntryType.Clip => "Clip",
                SearchEntryType.Transition => "Transition",
                SearchEntryType.Cue => "Cue",
                SearchEntryType.Parameter => "Parameter",
                SearchEntryType.Channel => "Channel",
                _ => "Item",
            };
        }

        private static void ApplyToolbarTabVisual(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = selected ? PaneBg : ToolbarBg;
            button.style.color = selected ? TextNormal : TextMuted;
            button.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
            button.style.borderTopColor = selected ? PaneBorder : SectionDivider;
            button.style.borderLeftColor = selected ? PaneBorder : SectionDivider;
            button.style.borderRightColor = selected ? PaneBorder : SectionDivider;
        }
    }
}
#endif
