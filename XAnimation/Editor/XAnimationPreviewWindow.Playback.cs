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
        private void LoadPreview()
        {
            try
            {
                GameObject prefab = m_PrefabField.value as GameObject;
                TextAsset assetText = m_AssetField.value as TextAsset;
                if (prefab == null)
                {
                    throw new XAnimationException("请选择一个 prefab 资源。");
                }

                if (assetText == null)
                {
                    throw new XAnimationException("请选择一个 .xanimation 或 .xanimationoverride 资源。");
                }

                string assetPath = AssetDatabase.GetAssetPath(assetText);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    throw new XAnimationException("无法获取 XAnimationAsset 的资源路径。");
                }

                DisposeSession();
                m_Session = new XAnimationEditorPreviewSession();
                m_Session.Load(prefab, assetPath);
                m_SelectedPrefab = prefab;
                m_SelectedAsset = assetText;
                SaveLastPreviewAssetPaths(assetText, prefab);
                m_ShouldAutoReloadPreview = true;
                m_IsPaused = false;
                double now = EditorApplication.timeSinceStartup;
                bool isPreviewVisible = IsPreviewTabVisible();
                m_UpdateCoordinator.Reset(now, isPreviewVisible);
                m_Session.SetPaused(!isPreviewVisible);
                m_Session.SetTimeScale(1f);
                m_PreviewRootMotionEnabled = m_Session.GetRootMotionEnabled();
                m_Session.SetRootMotionEnabled(m_PreviewRootMotionEnabled);
                MarkEventUiDirty();
                SetPauseButtonState(false, false);
                SetStepForwardButtonEnabled(true);
                m_RootMotionToggle?.SetValueWithoutNotify(m_PreviewRootMotionEnabled);
                m_GridToggle.SetValueWithoutNotify(true);
                RebuildCorePreviewLists();
                RebuildDefaultTransitionsEditor();
                RebuildChannelPresentation();
                RefreshAssetsToolbarButtons();
                RefreshPlaybackViews();
                RefreshCueLogView(force: true);
                SetStatus("预览已加载。");
                ApplyPendingPlaybackRequest();
                ApplyPendingFocusState();
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

        private void ApplyPendingPlaybackRequest()
        {
            if (m_Session == null || !m_Session.IsLoaded || !m_PendingPlaybackRequest.HasValue)
            {
                return;
            }

            PendingPlaybackRequest request = m_PendingPlaybackRequest.Value;
            m_PendingPlaybackRequest = null;
            bool hasExplicitTransition = request.Transition != null;

            try
            {
                SetPlaybackSpeed(request.Speed, savePrefs: true, updateSession: false);

                if (request.IsStatePlayback)
                {
                    m_IsPaused = false;
                    m_Session.SetPaused(false);
                    SetPauseButtonState(true, false);
                    SetStepForwardButtonEnabled(true);
                    m_Session.SetTimeScale(GetPlaybackSpeed());
                    m_Session.PlayState(request.StateKey, hasExplicitTransition ? CloneTransitionOptions(request.Transition) : null);
                    string stateChannel = FindStateChannelName(request.StateKey);
                    if (!string.IsNullOrWhiteSpace(stateChannel))
                    {
                        m_Session.SetChannelTimeScale(stateChannel, GetPlaybackSpeed());
                    }

                    RefreshPlaybackViews();
                    FocusStateInInspector(request.StateKey);
                    SetStatus($"正在播放 state {request.StateKey}。");
                    return;
                }

                if (request.IsClipPlayback)
                {
                    if (string.IsNullOrWhiteSpace(request.ChannelName))
                    {
                        throw new XAnimationException("预览窗口播放 clip 需要 channelName。");
                    }

                    m_PlayTargetChannelName = request.ChannelName;
                    m_PlayTargetChannelField?.SetValueWithoutNotify(request.ChannelName);
                    SavePlaybackPrefs();

                    m_IsPaused = false;
                    m_Session.SetPaused(false);
                    SetPauseButtonState(true, false);
                    SetStepForwardButtonEnabled(true);
                    m_Session.SetTimeScale(GetPlaybackSpeed());
                    m_Session.PlayClip(request.ClipKey, request.ChannelName, hasExplicitTransition ? CloneTransitionOptions(request.Transition) : null);
                    m_Session.SetChannelTimeScale(request.ChannelName, GetPlaybackSpeed());
                    RefreshPlaybackViews();
                    FocusClipInInspector(request.ClipKey);
                    SetStatus($"正在 {request.ChannelName} 调试播放 {request.ClipKey}。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
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
            m_Session.SetPaused(false);
            SetPauseButtonState(false, false);
            SetStepForwardButtonEnabled(HasAnyPlayingChannel());
            MarkStatePlaybackUiDirty();
            MarkClipPlaybackUiDirty();
            RefreshPlaybackViews();
            SetStatus("已停止全部通道。");
        }

        private void TogglePause()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            if (!HasAnyPlayingChannel())
            {
                if (!TryPlayFirstStateFromOverlay())
                {
                    SetStatus("当前没有可播放的 state。", true);
                }
                return;
            }

            m_IsPaused = !m_IsPaused;
            m_Session.SetPaused(m_IsPaused);
            SetPauseButtonState(true, m_IsPaused);
            SetStepForwardButtonEnabled(true);
            MarkClipPlaybackUiDirty();
            SetStatus(m_IsPaused ? "已暂停动画预览。" : "已继续动画预览。");
        }

        private void StepForward()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            if (!m_IsPaused)
            {
                m_IsPaused = true;
                m_Session.SetPaused(true);
            }

            m_Session.Step(1f / 60f);
            SetPauseButtonState(true, true);
            SetStepForwardButtonEnabled(true);
            MarkEventUiDirty();
            RefreshPlaybackViews();
            RefreshCueLogView(force: true);
            RenderPreview();
            Repaint();
            SetStatus("已向后推进一帧。");
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

            string resolvedClipChannel = string.IsNullOrWhiteSpace(channelName) ? playingChannelName : channelName;
            m_Session.PlayClip(clipKey, resolvedClipChannel, BuildPreviewTransitionOptions());
            m_Session.SetChannelTimeScale(resolvedClipChannel, GetPlaybackSpeed());
            RefreshPlaybackViews();
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

            m_Session.PlayState(stateKey, BuildPreviewTransitionOptions());
            string resolvedStateChannel = string.IsNullOrWhiteSpace(channelName) ? playingChannelName : channelName;
            m_Session.SetChannelTimeScale(resolvedStateChannel, GetPlaybackSpeed());
            RefreshStatePlaybackViews();
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
                RebuildClipPresentation();
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousClip);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void MoveClipToGroup(string clipKey, string groupName)
        {
            string normalizedGroup = NormalizeClipEditorGroupName(groupName);
            m_Session.SetClipEditorGroup(clipKey, normalizedGroup);
            RebuildClipPresentation();
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

            editor.Add(CreateClipCueEditor(clip));

            return editor;
        }

        private void ApplyClipButtonStyle(Button btn, bool isPlaying)
        {
            btn.text = isPlaying && !m_IsPaused ? "Ⅱ" : "▶";
            ApplyClipIconButtonStyle(btn, isPlaying && !m_IsPaused ? AccentColor : null);
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

            if (m_AddClipGroupButton != null)
            {
                m_AddClipGroupButton.SetEnabled(enabled);
                m_AddClipGroupButton.style.opacity = enabled ? 1f : 0.45f;
                m_AddClipGroupButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                    ? "Override 资源不能新增 clip group。"
                    : "新建一个 clip group。";
            }
        }

        private void SetStepForwardButtonEnabled(bool enabled)
        {
            if (m_StepForwardButton == null)
            {
                return;
            }

            m_StepForwardButton.SetEnabled(enabled);
            m_StepForwardButton.style.opacity = enabled ? 1f : 0.45f;
            m_StepForwardButton.tooltip = enabled
                ? "暂停状态下向后推进固定一帧（1/60s）。"
                : "需要先加载预览并存在可调试播放。";
        }

        private void SetAutoTransitionButtonsEnabled(bool addEnabled)
        {
            if (m_AddAutoTransitionButton != null)
            {
                m_AddAutoTransitionButton.SetEnabled(addEnabled);
                m_AddAutoTransitionButton.style.opacity = addEnabled ? 1f : 0.45f;
                m_AddAutoTransitionButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                    ? "Override 资源不能新增 Auto Transition。"
                    : addEnabled
                        ? "新增一个 Auto Transition。"
                        : "所有可用的非循环 state 都已经配置了 Auto Transition。";
            }
        }

        private void SetDefaultTransitionButtonsEnabled(bool addEnabled)
        {
            if (m_AddDefaultTransitionButton != null)
            {
                m_AddDefaultTransitionButton.SetEnabled(addEnabled);
                m_AddDefaultTransitionButton.style.opacity = addEnabled ? 1f : 0.45f;
                m_AddDefaultTransitionButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                    ? "Override 资源不能新增 Default Transition。"
                    : addEnabled
                        ? "新增一个 Default Transition 分组。"
                        : "Default Transition 至少需要两个 state。";
            }
        }

        private void SetPauseButtonState(bool enabled, bool paused, bool? hasActivePlayback = null)
        {
            if (m_PauseButton == null)
            {
                return;
            }

            bool isPlaying = hasActivePlayback ?? HasAnyPlayingChannel();
            m_PauseButton.text = enabled && isPlaying && !paused
                ? "Ⅱ"
                : "▶";
            m_PauseButton.SetEnabled(enabled);
            m_PauseButton.style.opacity = enabled ? 1f : 0.45f;
        }

        private bool TryPlayFirstStateFromOverlay()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            if (states == null || states.Count == 0)
            {
                return false;
            }

            XAnimationCompiledState firstState = states[0];
            if (firstState == null || string.IsNullOrWhiteSpace(firstState.Key))
            {
                return false;
            }

            m_IsPaused = false;
            m_Session.SetPaused(false);
            SetPauseButtonState(true, false);
            SetStepForwardButtonEnabled(true);
            m_Session.SetTimeScale(GetPlaybackSpeed());
            m_Session.PlayState(firstState.Key, BuildPreviewTransitionOptions());
            if (!string.IsNullOrWhiteSpace(firstState.Config.channelName))
            {
                m_Session.SetChannelTimeScale(firstState.Config.channelName, GetPlaybackSpeed());
            }

            RefreshPlaybackViews();
            FocusStateInInspector(firstState.Key);
            SetStatus($"正在播放 state {firstState.Key}。");
            return true;
        }

        private void RefreshClipPlayingStates()
        {
            if (m_ClipRowMap.Count == 0)
            {
                SetStopAllButtonEnabled(false);
                m_IsPaused = false;
                SetPauseButtonState(false, false);
                SetStepForwardButtonEnabled(false);
                return;
            }

            // Collect currently playing clip keys
            HashSet<string> playingClipKeys = null;
            Dictionary<string, float> clipProgressByKey = null;
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    XAnimationChannelState state = m_Session.GetChannelState(channels[i].Name);
                    if (state == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(state.clipKey))
                    {
                        playingClipKeys ??= new HashSet<string>(StringComparer.Ordinal);
                        playingClipKeys.Add(state.clipKey);
                        clipProgressByKey ??= new Dictionary<string, float>(StringComparer.Ordinal);
                        float progress = Mathf.Clamp01(state.normalizedTime);
                        if (!clipProgressByKey.TryGetValue(state.clipKey, out float existingProgress) || progress > existingProgress)
                        {
                            clipProgressByKey[state.clipKey] = progress;
                        }
                    }

                    XAnimationBlendClipState[] blendClips = state.blendClips;
                    if (blendClips == null)
                    {
                        continue;
                    }

                    for (int blendIndex = 0; blendIndex < blendClips.Length; blendIndex++)
                    {
                        XAnimationBlendClipState blendClip = blendClips[blendIndex];
                        if (blendClip == null || string.IsNullOrEmpty(blendClip.clipKey))
                        {
                            continue;
                        }

                        playingClipKeys ??= new HashSet<string>(StringComparer.Ordinal);
                        playingClipKeys.Add(blendClip.clipKey);
                        clipProgressByKey ??= new Dictionary<string, float>(StringComparer.Ordinal);
                        float blendProgress = Mathf.Clamp01(blendClip.normalizedTime);
                        if (!clipProgressByKey.TryGetValue(blendClip.clipKey, out float existingBlendProgress) || blendProgress > existingBlendProgress)
                        {
                            clipProgressByKey[blendClip.clipKey] = blendProgress;
                        }
                    }
                }
            }

            bool hasPlaying = HasAnyPlayingChannel();
            SetStopAllButtonEnabled(hasPlaying);
            if (!hasPlaying)
            {
                m_IsPaused = false;
            }
            bool canPlayFirstState = m_Session != null &&
                                     m_Session.IsLoaded &&
                                     m_Session.CompiledAsset?.States != null &&
                                     m_Session.CompiledAsset.States.Count > 0;
            SetPauseButtonState(hasPlaying || canPlayFirstState, m_IsPaused);
            SetStepForwardButtonEnabled(hasPlaying);
            RefreshPlaybackScrubber();

            foreach (KeyValuePair<string, VisualElement> kvp in m_ClipRowMap)
            {
                bool isPlaying = playingClipKeys != null && playingClipKeys.Contains(kvp.Key);
                if (m_ClipVisualStateMap.TryGetValue(kvp.Key, out ClipRowVisualState visualState))
                {
                    visualState.Playing = isPlaying;
                    visualState.Progress = isPlaying && clipProgressByKey != null && clipProgressByKey.TryGetValue(kvp.Key, out float progress)
                        ? progress
                        : 0f;
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
                RefreshBlendSampleRuntimeState();
                return;
            }

            HashSet<string> playingStateKeys = null;
            Dictionary<string, float> stateProgressByKey = null;
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
                        stateProgressByKey ??= new Dictionary<string, float>(StringComparer.Ordinal);
                        stateProgressByKey[state.stateKey] = Mathf.Clamp01(state.normalizedTime);
                    }
                }
            }

            foreach (KeyValuePair<string, VisualElement> kvp in m_StateRowMap)
            {
                bool isPlaying = playingStateKeys != null && playingStateKeys.Contains(kvp.Key);
                if (m_StateVisualStateMap.TryGetValue(kvp.Key, out RowVisualState visualState))
                {
                    visualState.Playing = isPlaying;
                    visualState.Progress = isPlaying && stateProgressByKey != null && stateProgressByKey.TryGetValue(kvp.Key, out float progress)
                        ? progress
                        : 0f;
                    ApplyStateRowVisualState(kvp.Key);
                }
                else
                {
                    kvp.Value.style.backgroundColor = isPlaying ? PlayingBg : ListRowEvenBg;
                }
                if (m_StateButtonMap.TryGetValue(kvp.Key, out Button button))
                {
                    ApplyClipButtonStyle(button, isPlaying);
                }
            }

            RefreshBlendSampleRuntimeState();
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

        private bool CanScrubPlayback()
        {
            return m_IsPaused && TryGetDominantPlaybackState(out _);
        }

        private bool TryBeginPlaybackScrub()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            if (!TryGetDominantPlaybackState(out _))
            {
                if (!TryPlayFirstStateFromOverlay())
                {
                    return false;
                }

                if (!TryGetDominantPlaybackState(out _))
                {
                    return false;
                }
            }

            m_IsPaused = true;
            m_Session.SetPaused(true);
            SetPauseButtonState(true, true);
            SetStepForwardButtonEnabled(true);
            return true;
        }

        private void RefreshPlaybackScrubber()
        {
            if (m_IsDraggingPlaybackScrubber)
            {
                return;
            }

            if (TryGetDominantPlaybackState(out XAnimationChannelState state))
            {
                UpdatePlaybackScrubber(Mathf.Clamp01(state.normalizedTime), enabled: true);
                return;
            }

            UpdatePlaybackScrubber(0f, enabled: false);
        }

        private void UpdatePlaybackScrubber(float progress, bool enabled)
        {
            m_PlaybackScrubberProgress = Mathf.Clamp01(progress);
            if (m_PlaybackScrubber != null)
            {
                m_PlaybackScrubber.style.opacity = enabled ? 1f : 0.35f;
            }

            if (m_PlaybackScrubberLine == null)
            {
                return;
            }

            float width = Mathf.Max(0f, m_PlaybackScrubber?.resolvedStyle.width ?? PlaybackScrubberWidth);
            float x = Mathf.Clamp(m_PlaybackScrubberProgress * width, 0f, Mathf.Max(0f, width - 2f));
            m_PlaybackScrubberLine.style.left = x;
        }

        private bool TryGetDominantPlaybackState(out XAnimationChannelState dominantState)
        {
            dominantState = null;
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            float bestWeight = -1f;
            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationChannelState state = m_Session.GetChannelState(channels[i].Name);
                if (state == null)
                {
                    continue;
                }

                float stateWeight = Mathf.Max(state.weight, state.channelWeight);
                XAnimationBlendClipState[] blendClips = state.blendClips;
                if (blendClips != null)
                {
                    for (int blendIndex = 0; blendIndex < blendClips.Length; blendIndex++)
                    {
                        XAnimationBlendClipState blendClip = blendClips[blendIndex];
                        if (blendClip != null)
                        {
                            stateWeight = Mathf.Max(stateWeight, blendClip.weight);
                        }
                    }
                }

                if (stateWeight > bestWeight)
                {
                    bestWeight = stateWeight;
                    dominantState = state;
                }
            }

            return dominantState != null;
        }

        private void UpdatePlaybackScrubberFromDrag(float localX)
        {
            if (m_PlaybackScrubber == null)
            {
                return;
            }

            float width = Mathf.Max(1f, m_PlaybackScrubber.resolvedStyle.width);
            float speed = Mathf.Max(PlaybackSpeedMin, GetPlaybackSpeed());
            float progress = Mathf.Clamp01(m_PlaybackScrubberDragStartProgress + ((localX - m_PlaybackScrubberDragStartX) / width * speed));
            UpdatePlaybackScrubber(progress, enabled: true);
        }

        private void SeekDominantPlayback(float normalizedTime)
        {
            if (m_Session == null || !m_Session.IsLoaded || !TryGetDominantPlaybackState(out XAnimationChannelState state))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(state.channelName) ||
                !m_Session.SeekChannel(state.channelName, normalizedTime))
            {
                return;
            }

            m_IsPaused = true;
            m_Session.SetPaused(true);
            SetPauseButtonState(true, true);
            SetStepForwardButtonEnabled(true);
            m_Session.SetTimeScale(GetPlaybackSpeed());
            if (!string.IsNullOrWhiteSpace(state.channelName))
            {
                m_Session.SetChannelTimeScale(state.channelName, GetPlaybackSpeed());
            }

            m_Session.Step(0.0001f);
            UpdatePlaybackScrubber(normalizedTime, enabled: true);
            MarkEventUiDirty();
            RefreshPlaybackAndLogViews();
            RenderPreview();
            Repaint();
        }

        private void ApplyStateRowVisualState(string stateKey)
        {
            if (!m_StateRowMap.TryGetValue(stateKey, out VisualElement row) ||
                !m_StateVisualStateMap.TryGetValue(stateKey, out RowVisualState visualState))
            {
                return;
            }

            ApplyRowVisualState(row, visualState);
        }

        private void RefreshBlendSampleRuntimeState()
        {
            if (m_BlendSampleRowMap.Count == 0 && m_FreeformBlendGraphElement == null)
            {
                return;
            }

            Dictionary<string, float> sampleWeightByRowKey = null;
            Dictionary<string, Dictionary<int, float>> sampleWeightsByState = null;
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    XAnimationChannelState channelState = m_Session.GetChannelState(channels[i].Name);
                    if (channelState?.blendClips == null || string.IsNullOrWhiteSpace(channelState.stateKey))
                    {
                        continue;
                    }

                    for (int blendIndex = 0; blendIndex < channelState.blendClips.Length; blendIndex++)
                    {
                        XAnimationBlendClipState blendClip = channelState.blendClips[blendIndex];
                        if (blendClip == null || string.IsNullOrWhiteSpace(blendClip.clipKey))
                        {
                            continue;
                        }

                        if (!TryFindBlendSampleRowKey(
                                channelState.stateKey,
                                blendClip.clipKey,
                                blendClip.positionX,
                                blendClip.positionY,
                                out string rowKey,
                                out int sampleIndex))
                        {
                            continue;
                        }

                        sampleWeightByRowKey ??= new Dictionary<string, float>(StringComparer.Ordinal);
                        if (!sampleWeightByRowKey.TryGetValue(rowKey, out float existingWeight) || blendClip.weight > existingWeight)
                        {
                            sampleWeightByRowKey[rowKey] = Mathf.Clamp01(blendClip.weight);
                        }

                        sampleWeightsByState ??= new Dictionary<string, Dictionary<int, float>>(StringComparer.Ordinal);
                        if (!sampleWeightsByState.TryGetValue(channelState.stateKey, out Dictionary<int, float> stateWeights))
                        {
                            stateWeights = new Dictionary<int, float>();
                            sampleWeightsByState[channelState.stateKey] = stateWeights;
                        }

                        float clampedWeight = Mathf.Clamp01(blendClip.weight);
                        if (!stateWeights.TryGetValue(sampleIndex, out float existingSampleWeight) || clampedWeight > existingSampleWeight)
                        {
                            stateWeights[sampleIndex] = clampedWeight;
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, RowVisualState> kvp in m_BlendSampleRowMap)
            {
                RowVisualState visualState = kvp.Value;
                visualState.Playing = false;
                visualState.Hovered = false;
                visualState.Progress = sampleWeightByRowKey != null && sampleWeightByRowKey.TryGetValue(kvp.Key, out float weight)
                    ? weight
                    : 0f;
                ApplyRowProgressVisualState(visualState);
            }

            RefreshGlobalBlendGraph(sampleWeightsByState);
        }

        private bool TryFindBlendSampleRowKey(string stateKey, string clipKey, float positionX, float positionY, out string rowKey, out int sampleIndex)
        {
            rowKey = null;
            sampleIndex = -1;
            if (string.IsNullOrWhiteSpace(clipKey) ||
                !TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState compiledState))
            {
                return false;
            }

            if (compiledState is XAnimationCompiledBlend1DState blendState)
            {
                for (int i = 0; i < blendState.Samples.Count; i++)
                {
                    XAnimationBlend1DSampleConfig sample = blendState.Samples[i].Config;
                    if (!string.Equals(sample.clipKey, clipKey, StringComparison.Ordinal) ||
                        !Mathf.Approximately(sample.threshold, positionX))
                    {
                        continue;
                    }

                    sampleIndex = i;
                    rowKey = BuildBlendSampleRuntimeKey(stateKey, i);
                    return true;
                }

                return false;
            }

            if (!TryGetDirectionalBlendSamples(compiledState, out IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> samples))
            {
                return false;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                XAnimationBlend2DSimpleDirectionalSampleConfig sample = samples[i].Config;
                if (!string.Equals(sample.clipKey, clipKey, StringComparison.Ordinal) ||
                    !Mathf.Approximately(sample.positionX, positionX) ||
                    !Mathf.Approximately(sample.positionY, positionY))
                {
                    continue;
                }

                sampleIndex = i;
                rowKey = BuildBlendSampleRuntimeKey(stateKey, i);
                return true;
            }

            return false;
        }

        private void RefreshGlobalBlendGraph(Dictionary<string, Dictionary<int, float>> sampleWeightsByState = null)
        {
            if ((m_FreeformBlendGraphElement == null && m_Blend1DGraphElement == null) || m_FreeformBlendGraphOverlay == null)
            {
                return;
            }

            if (!TryResolveBlendGraphStateKey(out string stateKey))
            {
                m_CurrentFreeformGraphStateKey = null;
                SetBlendGraphOverlayTitle("Blend Graph");
                m_FreeformBlendGraphOverlay.style.display = DisplayStyle.None;
                if (m_FreeformBlendGraphHintLabel != null)
                {
                    m_FreeformBlendGraphHintLabel.style.display = DisplayStyle.None;
                }

                return;
            }

            m_CurrentFreeformGraphStateKey = stateKey;
            m_FreeformBlendGraphOverlay.style.display = DisplayStyle.Flex;
            Dictionary<int, float> stateSampleWeights = sampleWeightsByState != null && sampleWeightsByState.TryGetValue(stateKey, out Dictionary<int, float> stateWeights)
                ? stateWeights
                : null;
            if (!TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState compiledState))
            {
                m_CurrentFreeformGraphStateKey = null;
                m_FreeformBlendGraphOverlay.style.display = DisplayStyle.None;
                return;
            }

            if (compiledState is XAnimationCompiledBlend1DState blend1DState)
            {
                UpdateBlend1DGraph(stateKey, blend1DState, stateSampleWeights);
                return;
            }

            if (compiledState is XAnimationCompiledBlend2DSimpleDirectionalState simpleDirectionalState)
            {
                UpdateDirectionalBlendGraph(
                    stateKey,
                    m_FreeformBlendGraphElement,
                    simpleDirectionalState,
                    "Simple 2D Directional Blend",
                    stateSampleWeights);
                return;
            }

            if (compiledState is XAnimationCompiledBlend2DFreeformDirectionalState freeformState)
            {
                UpdateDirectionalBlendGraph(
                    stateKey,
                    m_FreeformBlendGraphElement,
                    freeformState,
                    "Freeform 2D Blend",
                    stateSampleWeights);
                return;
            }

            m_FreeformBlendGraphOverlay.style.display = DisplayStyle.None;
        }

        private void UpdateBlend1DGraph(
            string stateKey,
            XAnimationCompiledBlend1DState blend1DState,
            Dictionary<int, float> sampleWeights = null)
        {
            if (m_Blend1DGraphElement == null || m_Session == null || !m_Session.IsLoaded || blend1DState == null)
            {
                return;
            }

            SetBlendGraphOverlayTitle("Blend1D");
            m_Blend1DGraphElement.style.display = DisplayStyle.Flex;
            if (m_FreeformBlendGraphElement != null)
            {
                m_FreeformBlendGraphElement.style.display = DisplayStyle.None;
            }

            XAnimationStateConfig config = blend1DState.Config;
            List<XAnimationBlend1DGraphElement.SampleViewData> sampleViews = new(blend1DState.Samples.Count);
            for (int i = 0; i < blend1DState.Samples.Count; i++)
            {
                XAnimationBlend1DSampleConfig sample = blend1DState.Samples[i].Config;
                float weight = sampleWeights != null && sampleWeights.TryGetValue(i, out float sampleWeight) ? sampleWeight : 0f;
                sampleViews.Add(new XAnimationBlend1DGraphElement.SampleViewData(sample.clipKey, sample.threshold, weight));
            }
            sampleViews.Sort((left, right) => left.Threshold.CompareTo(right.Threshold));

            bool hasParameter = !string.IsNullOrWhiteSpace(config.parameterName);
            float currentValue = GetBlend1DPreviewValue(config.parameterName);
            float minValue = 0f;
            float maxValue = 1f;
            if (hasParameter && TryGetBlend1DPreviewRange(config.parameterName, out float parameterMin, out float parameterMax))
            {
                minValue = parameterMin;
                maxValue = parameterMax;
            }
            else if (blend1DState.Samples.Count > 0)
            {
                minValue = blend1DState.Samples[0].Threshold;
                maxValue = blend1DState.Samples[blend1DState.Samples.Count - 1].Threshold;
            }

            if (m_FreeformBlendGraphHintLabel != null)
            {
                m_FreeformBlendGraphHintLabel.text = hasParameter
                    ? stateKey
                    : $"State: {stateKey}\nRead-only because parameter is missing.";
                m_FreeformBlendGraphHintLabel.style.display = DisplayStyle.Flex;
            }

            m_Blend1DGraphElement.SetData(new XAnimationBlend1DGraphElement.GraphData(
                sampleViews,
                currentValue,
                minValue,
                maxValue,
                hasParameter,
                hasParameter ? () => BeginBlend1DDragPreview(stateKey) : null,
                hasParameter ? value => UpdateBlend1DPreviewValue(stateKey, config, value) : null));
        }

        private void UpdateDirectionalBlendGraph(
            string stateKey,
            XAnimationDirectionalBlendGraphElement graph,
            XAnimationCompiledState directionalState,
            string title,
            Dictionary<int, float> sampleWeights = null)
        {
            if (graph == null ||
                m_Session == null ||
                !m_Session.IsLoaded ||
                directionalState == null ||
                string.IsNullOrWhiteSpace(stateKey) ||
                !TryGetDirectionalBlendSamples(directionalState, out IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> samples))
            {
                return;
            }

            SetBlendGraphOverlayTitle(title);
            graph.style.display = DisplayStyle.Flex;
            if (m_Blend1DGraphElement != null)
            {
                m_Blend1DGraphElement.style.display = DisplayStyle.None;
            }

            XAnimationStateConfig config = directionalState.Config;
            List<XAnimationDirectionalBlendGraphElement.SampleViewData> sampleViews = new(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                XAnimationBlend2DSimpleDirectionalSampleConfig sample = samples[i].Config;
                float weight = sampleWeights != null && sampleWeights.TryGetValue(i, out float sampleWeight) ? sampleWeight : 0f;
                sampleViews.Add(new XAnimationDirectionalBlendGraphElement.SampleViewData(
                    sample.clipKey,
                    sample.positionX,
                    sample.positionY,
                    weight));
            }

            bool hasParameters =
                !string.IsNullOrWhiteSpace(config.parameterXName) &&
                !string.IsNullOrWhiteSpace(config.parameterYName);
            Vector2 currentPosition = GetFreeformDirectionalPreviewPosition(config);
            if (m_FreeformBlendGraphHintLabel != null)
            {
                m_FreeformBlendGraphHintLabel.text = hasParameters
                    ? stateKey
                    : $"State: {stateKey}\nRead-only because parameterX / parameterY is missing.";
                m_FreeformBlendGraphHintLabel.style.display = DisplayStyle.Flex;
            }

            graph.SetData(new XAnimationDirectionalBlendGraphElement.GraphData(
                sampleViews,
                currentPosition,
                hasParameters,
                hasParameters ? () => BeginFreeformDirectionalDragPreview(stateKey) : null,
                hasParameters ? position => UpdateFreeformDirectionalPreviewPosition(stateKey, config, position) : null));
        }

        private bool TryResolveBlendGraphStateKey(out string stateKey)
        {
            stateKey = null;
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationChannelState channelState = m_Session.GetChannelState(channels[i].Name);
                if (channelState == null || string.IsNullOrWhiteSpace(channelState.stateKey))
                {
                    continue;
                }

                if (!IsBlendGraphCompatibleState(channelState.stateKey))
                {
                    continue;
                }

                stateKey = channelState.stateKey;
                return true;
            }

            if (TryGetExpandedStateKey(out string expandedStateKey) &&
                IsBlendGraphCompatibleState(expandedStateKey))
            {
                stateKey = expandedStateKey;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(m_LastInteractedFreeformStateKey) &&
                IsBlendGraphCompatibleState(m_LastInteractedFreeformStateKey))
            {
                stateKey = m_LastInteractedFreeformStateKey;
                return true;
            }

            return false;
        }

        private bool TryGetCompiledBlendGraphState(string stateKey, out XAnimationCompiledState compiledState)
        {
            compiledState = null;
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return false;
            }

            XAnimationCompiledAsset compiledAsset = m_Session.CompiledAsset;
            if (compiledAsset == null || !compiledAsset.TryGetStateIndex(stateKey, out int stateIndex))
            {
                return false;
            }

            compiledState = compiledAsset.States[stateIndex];
            return compiledState != null;
        }

        private bool IsBlendGraphCompatibleState(string stateKey)
        {
            if (!TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState compiledState))
            {
                return false;
            }

            return compiledState switch
            {
                XAnimationCompiledBlend1DState blend1DState => blend1DState.Samples.Count > 0,
                XAnimationCompiledBlend2DSimpleDirectionalState simpleDirectionalState => simpleDirectionalState.Samples.Count > 0,
                XAnimationCompiledBlend2DFreeformDirectionalState freeformState => freeformState.Samples.Count > 0,
                _ => false,
            };
        }

        private bool TryGetFreeformDirectionalState(string stateKey, out XAnimationCompiledBlend2DFreeformDirectionalState state)
        {
            state = null;
            if (!TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState compiledState))
            {
                return false;
            }

            state = compiledState as XAnimationCompiledBlend2DFreeformDirectionalState;
            return state != null && state.Samples.Count > 0;
        }

        private void MarkFreeformStateInteracted(string stateKey)
        {
            if (string.IsNullOrWhiteSpace(stateKey) || !IsBlendGraphCompatibleState(stateKey))
            {
                return;
            }

            m_LastInteractedFreeformStateKey = stateKey;
        }

        private Vector2 GetFreeformDirectionalPreviewPosition(XAnimationStateConfig config)
        {
            if (config == null)
            {
                return Vector2.zero;
            }

            float x = 0f;
            float y = 0f;
            if (!string.IsNullOrWhiteSpace(config.parameterXName) &&
                m_Session != null &&
                m_Session.TryGetPreviewParameter(config.parameterXName, out float previewX))
            {
                x = previewX;
            }

            if (!string.IsNullOrWhiteSpace(config.parameterYName) &&
                m_Session != null &&
                m_Session.TryGetPreviewParameter(config.parameterYName, out float previewY))
            {
                y = previewY;
            }

            return new Vector2(x, y);
        }

        private void BeginFreeformDirectionalDragPreview(string stateKey)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            MarkFreeformStateInteracted(stateKey);

            if (!TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState state))
            {
                return;
            }

            if (state == null || IsStateCurrentlyPlaying(stateKey, state.Config.channelName))
            {
                return;
            }

            m_Session.PlayState(stateKey, BuildPreviewTransitionOptions());
            RefreshStatePlaybackViews();
            RenderPreview();
            Repaint();
        }

        private void BeginBlend1DDragPreview(string stateKey)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            MarkFreeformStateInteracted(stateKey);
            if (!TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState state))
            {
                return;
            }

            if (state == null || IsStateCurrentlyPlaying(stateKey, state.Config.channelName))
            {
                return;
            }

            m_Session.PlayState(stateKey, BuildPreviewTransitionOptions());
            RefreshStatePlaybackViews();
            RenderPreview();
            Repaint();
        }

        private void UpdateFreeformDirectionalPreviewPosition(string stateKey, XAnimationStateConfig config, Vector2 position)
        {
            if (m_Session == null || !m_Session.IsLoaded || config == null)
            {
                return;
            }

            MarkFreeformStateInteracted(stateKey);

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(config.parameterXName))
            {
                changed |= TrySetPreviewParameter(config.parameterXName, position.x);
            }

            if (!string.IsNullOrWhiteSpace(config.parameterYName))
            {
                changed |= TrySetPreviewParameter(config.parameterYName, position.y);
            }

            if (changed)
            {
                RefreshPreviewAfterParameterChanged(rebuildParameterList: true);
            }

            if (string.Equals(m_CurrentFreeformGraphStateKey, stateKey, StringComparison.Ordinal))
            {
                if (!TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState currentState))
                {
                    m_CurrentFreeformGraphStateKey = null;
                }
                else if (currentState is XAnimationCompiledBlend2DSimpleDirectionalState simpleDirectionalState)
                {
                    UpdateDirectionalBlendGraph(
                        stateKey,
                        m_FreeformBlendGraphElement,
                        simpleDirectionalState,
                        "Simple 2D Directional Blend");
                }
                else if (currentState is XAnimationCompiledBlend2DFreeformDirectionalState freeformState)
                {
                    UpdateDirectionalBlendGraph(
                        stateKey,
                        m_FreeformBlendGraphElement,
                        freeformState,
                        "Freeform 2D Blend");
                }
            }
        }

        private float GetBlend1DPreviewValue(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName) || m_Session == null || !m_Session.IsLoaded)
            {
                return 0f;
            }

            if (m_Session.TryGetPreviewParameter(parameterName, out float value))
            {
                return value;
            }

            RebuildParameterList();
            return m_Session.TryGetPreviewParameter(parameterName, out value) ? value : 0f;
        }

        private void UpdateBlend1DPreviewValue(string stateKey, XAnimationStateConfig config, float value)
        {
            if (m_Session == null || !m_Session.IsLoaded || config == null)
            {
                return;
            }

            MarkFreeformStateInteracted(stateKey);
            if (!string.IsNullOrWhiteSpace(config.parameterName))
            {
                if (TrySetPreviewParameter(config.parameterName, value))
                {
                    RefreshPreviewAfterParameterChanged(rebuildParameterList: true);
                }
            }

            if (string.Equals(m_CurrentFreeformGraphStateKey, stateKey, StringComparison.Ordinal) &&
                TryGetCompiledBlendGraphState(stateKey, out XAnimationCompiledState currentState) &&
                currentState is XAnimationCompiledBlend1DState blend1DState)
            {
                UpdateBlend1DGraph(stateKey, blend1DState);
            }
        }

        private bool IsStateCurrentlyPlaying(string stateKey, string channelName)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(channelName))
            {
                XAnimationChannelState channelState = m_Session.GetChannelState(channelName);
                return channelState != null && string.Equals(channelState.stateKey, stateKey, StringComparison.Ordinal);
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationChannelState channelState = m_Session.GetChannelState(channels[i].Name);
                if (channelState != null && string.Equals(channelState.stateKey, stateKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildBlendSampleRuntimeKey(string stateKey, int sampleIndex)
        {
            return $"blend-sample:{stateKey}:{sampleIndex}";
        }

        private void RefreshStatePlaybackViews()
        {
            RefreshStatePlayingStates();
            RefreshChannelStates();
        }

        private void RefreshPlaybackViews()
        {
            RefreshStatePlayingStates();
            RefreshClipPlayingStates();
            RefreshChannelStates();
        }

        private void RefreshPlaybackAndLogViews()
        {
            RefreshPlaybackViews();
            RefreshCueLogView(force: true);
        }

        private void RebuildChannelPresentation()
        {
            RebuildChannelControls();
            RefreshPlayTargetChannelChoices();
        }

        private void RebuildCorePreviewLists()
        {
            RebuildParameterList();
            RebuildStateList();
            RebuildClipList();
        }

        private void RebuildStatePresentation(bool includeClipList = false, bool includeChannelPresentation = false)
        {
            RebuildStateList();
            if (includeClipList)
            {
                RebuildClipList();
            }

            if (includeChannelPresentation)
            {
                RebuildChannelPresentation();
            }

            RefreshStatePlaybackViews();
            if (includeClipList)
            {
                RefreshClipPlayingStates();
            }
        }

        private void RebuildClipPresentation()
        {
            RebuildClipList();
            RefreshClipPlayingStates();
            RefreshChannelStates();
        }

        private void RebuildStructureAndPlaybackViews()
        {
            RebuildStateList();
            RebuildClipList();
            RebuildChannelPresentation();
            RefreshPlaybackViews();
        }

        private void ApplyClipRowVisualState(string clipKey)
        {
            if (!m_ClipRowMap.TryGetValue(clipKey, out VisualElement row) ||
                !m_ClipVisualStateMap.TryGetValue(clipKey, out ClipRowVisualState visualState))
            {
                return;
            }

            row.style.backgroundColor = visualState.Playing
                ? visualState.Flashing ? ClipFocusFlashBg : PlayingBg
                : visualState.Flashing
                    ? ClipFocusFlashBg
                    : visualState.Hovered
                        ? HoverBg
                        : visualState.BaseColor;
            ApplyRowProgressVisualState(visualState);
        }

        private void RebuildChannelControls()
        {
            m_ChannelControlsContainer.Clear();
            m_ChannelLabelMap.Clear();
            m_ChannelStateLabels.Clear();
            m_ChannelRowMap.Clear();
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
                m_ChannelRowMap[channel.Name] = controlRow;

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
            RefreshSearchIndex();
        }

    }
}
#endif
