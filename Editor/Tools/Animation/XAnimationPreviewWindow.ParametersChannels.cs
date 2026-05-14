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
    public sealed partial class XAnimationPreviewWindow
    {
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
            ApplyDropdownFieldStyle(layerTypeField);
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
            string next = string.IsNullOrWhiteSpace(state.nextStateKey) ? string.Empty : $" | Next: {state.nextStateKey}";
            string transition = state.isTransitioning
                ? $" | Transition: {state.previousStateKey} -> {state.transitionTargetStateKey} ({state.transitionSource})"
                : string.Empty;
            string reject = state.lastRejectReason == XAnimationTransitionRejectReason.None
                ? string.Empty
                : $"\nLast Reject: {state.lastRejectReason} | Target: {state.lastRejectedStateKey} | Clip: {state.lastRejectedClipKey} | Priority: {state.lastRejectedPriority} | Source: {state.lastRejectedSource}";
            return $"State: {stateKey} ({state.stateType}) | Clip: {state.clipKey} | PlayId: {state.playbackId} | {loop} | {fade} | {interrupt}\n"
                + $"Time: {state.normalizedTime:0.000} / Total: {state.totalNormalizedTime:0.000} | Channel Weight: {state.channelWeight:0.000} | State Weight: {state.weight:0.000}\n"
                + $"TimeScale: {state.timeScale:0.000} | Effective Speed: {state.speed:0.000} | Priority: {state.priority}{next}{transition}{blend}{reject}";
        }

        private static string BuildBlendStateDebugText(XAnimationChannelState state)
        {
            bool hasBlendClips = state.blendClips != null && state.blendClips.Length > 0;
            bool isDirectionalState = IsDirectionalBlendStateType(state.stateType);
            if (!hasBlendClips && !isDirectionalState)
            {
                return string.Empty;
            }

            List<string> parts = new();
            if (isDirectionalState)
            {
                parts.Add($"BlendInput: ({state.blendParameterX:0.###}, {state.blendParameterY:0.###})");
            }

            if (hasBlendClips)
            {
                List<string> clipParts = new(state.blendClips.Length);
                for (int i = 0; i < state.blendClips.Length; i++)
                {
                    XAnimationBlendClipState clipState = state.blendClips[i];
                    bool hasDirectionalPosition =
                        !Mathf.Approximately(clipState.positionX, 0f) ||
                        !Mathf.Approximately(clipState.positionY, 0f);
                    string clipText = hasDirectionalPosition
                        ? $"{clipState.clipKey}@({clipState.positionX:0.###},{clipState.positionY:0.###}):{clipState.weight:0.000}"
                        : $"{clipState.clipKey}:{clipState.weight:0.000}";
                    clipParts.Add(clipText);
                }

                parts.Add($"Blend: {string.Join(", ", clipParts)}");
            }

            return $"\n{string.Join("\n", parts)}";
        }

        private void RefreshCueLogView(bool force = false)
        {
            if (m_CueLogContainer == null)
            {
                return;
            }

            if (!m_PlaybackUiState.ShouldRefreshCueLog(m_Session, force))
            {
                return;
            }

            m_CueLogContainer.Clear();
            m_LogLabels.Clear();

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationEditorPreviewSession.PreviewLogEntry> cueLogs = m_Session.CueLogs;
            bool hasSelection = m_SelectedLogId.HasValue;
            bool selectionStillExists = false;
            Label latestLabel = null;
            Label selectedLabel = null;

            for (int i = 0; i < cueLogs.Count; i++)
            {
                XAnimationEditorPreviewSession.PreviewLogEntry logEntry = cueLogs[i];
                Label label = new(logEntry.Message);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginBottom = 0;
                label.style.paddingLeft = 4;
                label.style.paddingRight = 4;
                label.style.paddingTop = 3;
                label.style.paddingBottom = 3;
                label.style.fontSize = 11;
                label.style.color = TextNormal;
                label.style.unityFontStyleAndWeight = FontStyle.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.borderBottomWidth = 1;
                label.style.borderBottomColor = SectionDivider;
                label.style.flexShrink = 0;
                label.style.backgroundColor = i % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
                label.userData = logEntry.Id;

                int selectedLogId = m_SelectedLogId ?? -1;
                bool isSelected = hasSelection && logEntry.Id == selectedLogId;
                if (isSelected)
                {
                    selectionStillExists = true;
                    selectedLabel = label;
                }

                ApplyLogLabelSelection(label, isSelected, i);
                RegisterLogLabelInteractions(label, logEntry.Id);
                m_LogLabels.Add(label);
                m_CueLogContainer.Add(label);
                latestLabel = label;
            }

            if (hasSelection && !selectionStillExists)
            {
                m_SelectedLogId = null;
                m_FollowLatestLog = true;
            }

            if (m_SelectedLogId.HasValue)
            {
                return;
            }

            Label targetLabel = selectedLabel ?? latestLabel;
            if (!m_FollowLatestLog || targetLabel == null)
            {
                return;
            }

            ScheduleScrollLogIntoView(targetLabel);
        }

        private void ClearCueLog()
        {
            if (m_Session == null)
            {
                return;
            }

            m_Session.ClearCueLogs();
            m_PlaybackUiState.InvalidateCueLogSnapshot();
            m_SelectedLogId = null;
            m_FollowLatestLog = true;
            MarkCueLogUiDirty();
            RefreshCueLogView(force: true);
            SetStatus("Log 已清空。");
        }

        private string FindStateChannelName(string stateKey)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return null;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (state != null && string.Equals(state.Key, stateKey, StringComparison.Ordinal))
                {
                    return state.Config.channelName;
                }
            }

            return null;
        }

        private void FocusStateInInspector(string stateKey)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            SetDebugToolbarGroup(DebugToolbarGroup.Main);
            m_StatesSectionExpanded = true;
            m_StatesCard?.SetExpanded?.Invoke(true);
            ExpandStateGroupForState(stateKey);
            RebuildStateList();
            RefreshStatePlayingStates();
            if (m_StateRowMap.TryGetValue(stateKey, out VisualElement stateRow))
            {
                ScheduleInspectorScrollIntoView(stateRow);
                FlashElement(stateRow);
            }
        }

        private void FocusClipInInspector(string clipKey)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                return;
            }

            SetDebugToolbarGroup(DebugToolbarGroup.Clip);
            m_ClipsSectionExpanded = true;
            m_ClipsCard?.SetExpanded?.Invoke(true);
            ExpandClipGroupForClip(clipKey);
            RebuildClipList();
            RefreshClipPlayingStates();
            if (m_ClipRowMap.TryGetValue(clipKey, out VisualElement clipRow))
            {
                ScheduleInspectorScrollIntoView(clipRow);
                FlashClipRow(clipKey);
            }
        }

        private void FocusAutoTransitionInInspector(string preStateKey)
        {
            if (string.IsNullOrWhiteSpace(preStateKey))
            {
                return;
            }

            SetDebugToolbarGroup(DebugToolbarGroup.Main);
            m_AutoTransitionSectionExpanded = true;
            m_AutoTransitionCard?.SetExpanded?.Invoke(true);
            SetAutoTransitionExpanded(preStateKey, true);
            RebuildAutoTransitionEditor();
            if (m_AutoTransitionRowMap.TryGetValue(preStateKey, out VisualElement row))
            {
                ScheduleInspectorScrollIntoView(row);
                FlashElement(row);
            }
        }

        private void FocusDefaultTransitionInInspector(int transitionIndex)
        {
            if (transitionIndex < 0)
            {
                return;
            }

            SetDebugToolbarGroup(DebugToolbarGroup.Main);
            m_DefaultTransitionsSectionExpanded = true;
            m_DefaultTransitionsCard?.SetExpanded?.Invoke(true);
            m_SelectedDefaultTransitionIndex = transitionIndex;
            SetDefaultTransitionExpanded(transitionIndex, true);
            RebuildDefaultTransitionsEditor();
            if (m_DefaultTransitionRowMap.TryGetValue(transitionIndex, out VisualElement row))
            {
                ScheduleInspectorScrollIntoView(row);
                FlashElement(row);
            }
        }

        private void FocusParameterInInspector(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            SetDebugToolbarGroup(DebugToolbarGroup.Parameters);
            m_ParametersSectionExpanded = true;
            m_ParametersCard?.SetExpanded?.Invoke(true);
            RebuildParameterList();
            if (m_ParameterRowMap.TryGetValue(parameterName, out VisualElement row))
            {
                ScheduleInspectorScrollIntoView(row);
                FlashElement(row);
            }
        }

        private void FocusChannelInInspector(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return;
            }

            SetDebugToolbarGroup(DebugToolbarGroup.Channels);
            m_ChannelsSectionExpanded = true;
            m_ChannelsCard?.SetExpanded?.Invoke(true);
            RebuildChannelControls();
            RefreshChannelStates();
            if (m_ChannelRowMap.TryGetValue(channelName, out VisualElement row))
            {
                ScheduleInspectorScrollIntoView(row);
                FlashElement(row);
            }
        }

        private void FocusCueInInspector(string cueKey, string clipKey)
        {
            if (string.IsNullOrWhiteSpace(cueKey) || string.IsNullOrWhiteSpace(clipKey))
            {
                return;
            }

            SetDebugToolbarGroup(DebugToolbarGroup.Clip);
            m_ClipsSectionExpanded = true;
            m_ClipsCard?.SetExpanded?.Invoke(true);
            if (!string.IsNullOrWhiteSpace(clipKey))
            {
                m_ExpandedClipKeys.Add(clipKey);
            }

            RebuildClipList();
            RefreshClipPlayingStates();
            if (m_CueRowMap.TryGetValue(cueKey, out VisualElement row))
            {
                ScheduleInspectorScrollIntoView(row);
                FlashElement(row);
            }
            else if (m_ClipRowMap.TryGetValue(clipKey, out VisualElement clipRow))
            {
                ScheduleInspectorScrollIntoView(clipRow);
                FlashClipRow(clipKey);
            }
        }

        private void FlashClipRow(string clipKey)
        {
            if (string.IsNullOrWhiteSpace(clipKey) ||
                !m_ClipRowMap.TryGetValue(clipKey, out VisualElement row) ||
                !m_ClipVisualStateMap.TryGetValue(clipKey, out ClipRowVisualState visualState))
            {
                return;
            }

            visualState.FlashVersion++;
            int flashVersion = visualState.FlashVersion;
            visualState.Flashing = true;
            ApplyClipRowVisualState(clipKey);

            row.schedule.Execute(() =>
            {
                if (!m_ClipVisualStateMap.TryGetValue(clipKey, out ClipRowVisualState currentState) ||
                    currentState.FlashVersion != flashVersion)
                {
                    return;
                }

                currentState.Flashing = false;
                ApplyClipRowVisualState(clipKey);
            }).ExecuteLater(420);
        }

        private void FlashElement(VisualElement target)
        {
            if (target == null)
            {
                return;
            }

            if (!m_FlashOverlayMap.TryGetValue(target, out VisualElement overlay) || overlay == null)
            {
                overlay = new VisualElement();
                overlay.pickingMode = PickingMode.Ignore;
                overlay.style.position = Position.Absolute;
                overlay.style.left = 0;
                overlay.style.right = 0;
                overlay.style.top = 0;
                overlay.style.bottom = 0;
                overlay.style.backgroundColor = ClipFocusFlashBg;
                overlay.style.opacity = 0f;
                overlay.style.borderTopLeftRadius = 3;
                overlay.style.borderTopRightRadius = 3;
                overlay.style.borderBottomLeftRadius = 3;
                overlay.style.borderBottomRightRadius = 3;
                target.style.position = Position.Relative;
                target.Add(overlay);
                m_FlashOverlayMap[target] = overlay;
            }

            int version = m_FlashOverlayVersionMap.TryGetValue(target, out int existingVersion) ? existingVersion + 1 : 1;
            m_FlashOverlayVersionMap[target] = version;
            overlay.style.opacity = 0.92f;

            target.schedule.Execute(() =>
            {
                if (!m_FlashOverlayVersionMap.TryGetValue(target, out int latestVersion) || latestVersion != version)
                {
                    return;
                }

                overlay.style.opacity = 0f;
            }).ExecuteLater(460);
        }

        private static string BuildCueSearchKey(string clipKey, int cueIndex)
        {
            return $"cue:{clipKey}:{cueIndex}";
        }

        private static string BuildDerivedCueSearchKey(string clipKey, int cueIndex)
        {
            return $"cue-derived:{clipKey}:{cueIndex}";
        }

        private void ClearDebugViews()
        {
            m_ClipListView?.Clear();
            m_ExpandedClipKeys.Clear();
            m_ClipLabelMap.Clear();
            m_ClipRowMap.Clear();
            m_ClipVisualStateMap.Clear();
            m_ClipButtonMap.Clear();
            m_CueRowMap.Clear();
            m_ParameterRowMap.Clear();
            m_ChannelRowMap.Clear();
            m_AutoTransitionRowMap.Clear();
            m_SearchEntries.Clear();
            m_VisibleSearchEntries.Clear();
            HideSearchResults();
            m_ChannelControlsContainer?.Clear();
            m_ChannelLabelMap.Clear();
            m_CueLogContainer?.Clear();
            m_LogLabels.Clear();
            m_ChannelStateLabels.Clear();
            m_DefaultTransitionsEditorView?.Clear();
            m_DefaultTransitionRowMap.Clear();
            m_CollapsedDefaultTransitionIndices.Clear();
            m_PendingClipRenameKey = null;
            m_PendingChannelRenameKey = null;
            if (m_PreviewImage != null)
            {
                m_PreviewImage.image = null;
            }
            m_PlaybackUiState.InvalidateCueLogSnapshot();
            m_SelectedLogId = null;
            m_FollowLatestLog = true;
            m_IsPaused = false;
            SetPauseButtonState(false, false);
            SetStepForwardButtonEnabled(false);
            SetStopAllButtonEnabled(false);
            UpdatePlaybackScrubber(0f, enabled: false);
            SetAddClipButtonEnabled(false);
            SetAddChannelButtonEnabled(false);
            SetAutoTransitionButtonsEnabled(false);
            SetDefaultTransitionButtonsEnabled(false);
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
            m_PlaybackUiState.Reset();
            m_SelectedLogId = null;
            m_FollowLatestLog = true;
            m_PressedKeys.Clear();
            m_IsDraggingPlaybackScrubber = false;
            m_PlaybackScrubberDragStartX = 0f;
            m_PlaybackScrubberDragStartProgress = 0f;
            UpdatePlaybackScrubber(0f, enabled: false);
            double now = EditorApplication.timeSinceStartup;
            m_UpdateCoordinator.Reset(now);
            MarkEventUiDirty();
        }

        private void RegisterLogLabelInteractions(Label label, int logId)
        {
            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                if (m_SelectedLogId.HasValue && m_SelectedLogId.Value == logId)
                {
                    m_SelectedLogId = null;
                    m_FollowLatestLog = true;
                }
                else
                {
                    m_SelectedLogId = logId;
                    m_FollowLatestLog = false;
                }

                RefreshLogSelectionVisuals();
                evt.StopPropagation();
            });
        }

        private void RefreshLogSelectionVisuals()
        {
            for (int i = 0; i < m_LogLabels.Count; i++)
            {
                Label label = m_LogLabels[i];
                int logId = label.userData is int value ? value : -1;
                bool isSelected = m_SelectedLogId.HasValue && m_SelectedLogId.Value == logId;
                ApplyLogLabelSelection(label, isSelected, i);
            }
        }

        private static void ApplyLogLabelSelection(Label label, bool isSelected, int rowIndex)
        {
            if (label == null)
            {
                return;
            }

            if (isSelected)
            {
                label.style.backgroundColor = PlayingBg;
                label.style.color = Color.white;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                return;
            }

            label.style.backgroundColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            label.style.color = TextNormal;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
        }

        private void ScrollLogIntoView(VisualElement target)
        {
            if (m_CueLogContainer == null || target == null)
            {
                return;
            }

            if (target.parent != m_CueLogContainer.contentContainer)
            {
                return;
            }

            m_CueLogContainer.ScrollTo(target);

            float viewportHeight = m_CueLogContainer.contentViewport.layout.height;
            float contentHeight = m_CueLogContainer.contentContainer.layout.height;
            if (viewportHeight <= 0f || contentHeight <= viewportHeight + 0.5f)
            {
                m_CueLogContainer.scrollOffset = Vector2.zero;
                return;
            }

            float targetBottom = target.layout.yMax;
            float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
            float nextOffsetY = Mathf.Clamp(targetBottom - viewportHeight, 0f, maxOffset);
            m_CueLogContainer.scrollOffset = new Vector2(0f, nextOffsetY);
        }

        private void ScheduleScrollLogIntoView(VisualElement target)
        {
            if (m_CueLogContainer == null || target == null)
            {
                return;
            }

            void ApplyScroll()
            {
                if (m_SelectedLogId.HasValue || !m_FollowLatestLog)
                {
                    return;
                }

                if (target.parent != m_CueLogContainer.contentContainer)
                {
                    return;
                }

                ScrollLogIntoView(target);
            }

            m_CueLogContainer.schedule.Execute(ApplyScroll).ExecuteLater(0);
            m_CueLogContainer.schedule.Execute(ApplyScroll).ExecuteLater(16);
        }

        private void ScheduleInspectorScrollIntoView(VisualElement target)
        {
            if (m_InspectorScrollView == null || target == null)
            {
                return;
            }

            void ApplyScroll()
            {
                ScrollView scrollView = m_InspectorScrollView;
                VisualElement contentContainer = scrollView?.contentContainer;
                if (scrollView == null || contentContainer == null)
                {
                    return;
                }

                if (scrollView.panel == null || target.panel == null || scrollView.panel != target.panel)
                {
                    return;
                }

                if (!IsDescendantOf(target, contentContainer))
                {
                    return;
                }

                scrollView.ScrollTo(target);
            }

            m_InspectorScrollView.schedule.Execute(ApplyScroll).ExecuteLater(0);
            m_InspectorScrollView.schedule.Execute(ApplyScroll).ExecuteLater(16);
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
#endif
