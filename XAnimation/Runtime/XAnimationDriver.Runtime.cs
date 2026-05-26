using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace XFramework.Animation
{
    public sealed partial class XAnimationDriver
    {
        private readonly List<XAnimationChannel> m_Channels = new();
        private readonly Dictionary<string, XAnimationChannel> m_ChannelMap = new(StringComparer.Ordinal);
        private readonly XAnimationCueDispatcher m_CueDispatcher = new();
        private const string TemporaryClipStateKeyPrefix = "__xanimation_temp_clip_state:";
        private const string DirectClipKeyPrefix = "__xanimation_direct_clip:";

        private PlayableGraph m_Graph;
        private AnimationLayerMixerPlayable m_LayerMixer;
        private AnimationPlayableOutput m_Output;
        private RuntimeAnimatorController m_OriginalController;
        private bool m_OriginalApplyRootMotion;
        private bool m_RootMotionEnabled;
        private int m_NextPlaybackId = 1;
        private int m_NextTemporaryStateId = 1;
        private float[] m_LastLayerInputWeights;
        private bool m_UseDirectChannelOutput;
        private bool m_RuntimeInitialized;
        private XAnimationContext Context => m_Context;

        private XAnimationDebugGraphSnapshot BuildDebugGraphSnapshot()
        {
            if (!m_RuntimeInitialized)
            {
                return XAnimationDebugGraphSnapshot.Invalid("XAnimationDriver is not initialized.");
            }

            if (!m_Graph.IsValid())
            {
                return XAnimationDebugGraphSnapshot.Invalid(
                    "PlayableGraph is invalid.",
                    string.Empty);
            }

            XAnimationDebugSnapshotBuilder builder = new();
            XAnimationDebugNodeSnapshot graphNode = builder.CreateNode(0, m_Graph.GetEditorName(), "PlayableGraph");
            graphNode.isConnected = true;
            graphNode.isActive = m_Graph.IsPlaying();
            graphNode.details = $"Time Update Mode: {m_Graph.GetTimeUpdateMode()}";

            XAnimationDebugNodeSnapshot outputNode = builder.CreateNode(graphNode.id, "XAnimationOutput", "AnimationPlayableOutput");
            outputNode.isConnected = m_Output.IsOutputValid();
            outputNode.isActive = outputNode.isConnected;
            outputNode.details = Animator != null ? $"Animator: {Animator.name}" : "Animator: <null>";

            XAnimationDebugChannelSnapshot[] channels = new XAnimationDebugChannelSnapshot[m_Channels.Count];
            if (m_UseDirectChannelOutput)
            {
                XAnimationChannel channel = m_Channels.Count > 0 ? m_Channels[0] : null;
                float layerWeight = channel != null ? channel.ChannelWeight : 0f;
                if (channel != null)
                {
                    channels[0] = channel.BuildDebugChannelSnapshot(layerWeight);
                    outputNode.children = new[]
                    {
                        channel.BuildDebugNode(builder, outputNode.id, 0, layerWeight),
                    };
                }
                else
                {
                    outputNode.children = Array.Empty<XAnimationDebugNodeSnapshot>();
                }
            }
            else
            {
                XAnimationDebugNodeSnapshot layerMixerNode = builder.CreateNode(outputNode.id, "Layer Mixer", "AnimationLayerMixerPlayable");
                layerMixerNode.isConnected = m_LayerMixer.IsValid();
                layerMixerNode.isActive = m_LayerMixer.IsValid();
                layerMixerNode.inputWeight = 1f;
                layerMixerNode.effectiveWeight = 1f;
                layerMixerNode.details = $"Input Count: {(m_LayerMixer.IsValid() ? m_LayerMixer.GetInputCount() : 0)}";

                XAnimationDebugNodeSnapshot[] channelNodes = new XAnimationDebugNodeSnapshot[m_Channels.Count];
                for (int i = 0; i < m_Channels.Count; i++)
                {
                    XAnimationChannel channel = m_Channels[i];
                    float layerWeight = m_LayerMixer.IsValid() ? m_LayerMixer.GetInputWeight(i) : 0f;
                    channels[i] = channel.BuildDebugChannelSnapshot(layerWeight);
                    channelNodes[i] = channel.BuildDebugNode(builder, layerMixerNode.id, i, layerWeight);
                }

                layerMixerNode.children = channelNodes;
                outputNode.children = new[] { layerMixerNode };
            }

            graphNode.children = new[] { outputNode };

            return new XAnimationDebugGraphSnapshot
            {
                graphName = m_Graph.GetEditorName(),
                isValid = true,
                isPlaying = m_Graph.IsPlaying(),
                isDisposed = false,
                animatorName = Animator != null ? Animator.name : string.Empty,
                message = string.Empty,
                channels = channels,
                rootNodes = new[] { graphNode },
            };
        }

        private XAnimationPlaybackStartInfo StartClipPlayback(string clipKey, string channelName, XAnimationTransitionOptions transition = default)
        {
            ThrowIfDisposed();
            if (!CompiledAsset.TryGetClipIndex(clipKey, out int clipIndex))
            {
                throw new XAnimationException($"XAnimation clip '{clipKey}' does not exist.");
            }

            XAnimationCompiledClip clip = (XAnimationCompiledClip)CompiledAsset.Clips[clipIndex];
            XAnimationCompiledChannel channel = ResolveClipChannel(clip, channelName);
            if (!CompiledAsset.TryGetChannelIndex(channel.Name, out int channelIndex))
            {
                throw new XAnimationException($"XAnimation channel '{channel.Name}' does not exist.");
            }

            XAnimationCompiledSingleState temporaryState = CreateTemporaryClipState(clip, clipIndex, channel, channelIndex);
            XAnimationTransitionRequest request = BuildTransitionRequest(
                temporaryState,
                channel,
                transition,
                XAnimationTransitionRequestSource.ExplicitPlay);
            return TryPlayCompiledState(temporaryState, channel, request);
        }

        private XAnimationPlaybackStartInfo StartClipPlayback(AnimationClip animationClip, string channelName, XAnimationTransitionOptions transition = default)
        {
            ThrowIfDisposed();
            if (animationClip == null)
            {
                throw new XAnimationException("XAnimation direct AnimationClip cannot be null.");
            }

            string clipKey = CreateDirectClipKey(animationClip);
            XAnimationCompiledClip clip = new(
                new XAnimationClipConfig
                {
                    key = clipKey,
                    editorGroupName = string.Empty,
                    clipPath = string.Empty,
                },
                animationClip);
            m_CueDispatcher.RegisterClipCues(clip.Key, clip.AnimationEventCues);
            XAnimationCompiledChannel channel = ResolveClipChannel(clip, channelName);
            if (!CompiledAsset.TryGetChannelIndex(channel.Name, out int channelIndex))
            {
                throw new XAnimationException($"XAnimation channel '{channel.Name}' does not exist.");
            }

            XAnimationCompiledSingleState temporaryState = CreateTemporaryDirectClipState(clip, channel, channelIndex);
            XAnimationTransitionRequest request = BuildTransitionRequest(
                temporaryState,
                channel,
                transition,
                XAnimationTransitionRequestSource.ExplicitPlay);
            return TryPlayCompiledState(temporaryState, channel, request);
        }

        private XAnimationPlaybackStartInfo StartStatePlayback(
            string stateKey,
            XAnimationTransitionOptions transition = default,
            bool force = false)
        {
            ThrowIfDisposed();
            XAnimationCompiledState state = CompiledAsset.GetState(stateKey);
            XAnimationCompiledChannel channel = GetStateChannel(state);
            if (!force &&
                !CanTransitionFromCurrentPlayback(m_ChannelMap[channel.Name], state, out XAnimationTransitionRejectReason gateRejectReason))
            {
                string clipKey = state is XAnimationCompiledSingleState singleState
                    ? ResolveSingleStateClip(singleState).Key
                    : string.Empty;
                return XAnimationPlaybackStartInfo.CreateFailed(channel.Name, state.Key, clipKey, IsTemporaryClipState(state.Key), gateRejectReason);
            }

            XAnimationTransitionRequest request = BuildTransitionRequest(
                state,
                channel,
                transition,
                transition != null ? XAnimationTransitionRequestSource.ExplicitPlay : ResolveRequestSource(state, channel),
                force);
            return TryPlayCompiledState(state, channel, request);
        }

        private XAnimationPlaybackStartInfo TryPlayCompiledState(
            XAnimationCompiledState state,
            XAnimationCompiledChannel channel,
            XAnimationTransitionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!m_ChannelMap[channel.Name].TryPlay(
                (playbackId, actualOptions) => CreateStatePlayback(playbackId, channel.Name, state, actualOptions),
                request,
                m_CueDispatcher,
                out XAnimationStatePlaybackInstance playback,
                out XAnimationTransitionRejectReason rejectReason))
            {
                string clipKey = state switch
                {
                    XAnimationCompiledSingleState singleState => ResolveSingleStateClip(singleState).Key,
                    _ => string.Empty,
                };
                return XAnimationPlaybackStartInfo.CreateFailed(channel.Name, state.Key, clipKey, IsTemporaryClipState(state.Key), rejectReason);
            }

            return XAnimationPlaybackStartInfo.CreateStarted(playback);
        }

        private XAnimationTransitionRequest BuildTransitionRequest(
            XAnimationCompiledState state,
            XAnimationCompiledChannel channel,
            XAnimationTransitionOptions transition,
            XAnimationTransitionRequestSource explicitSource,
            bool force = false)
        {
            XAnimationTransitionOptions resolvedTransition = ResolveTransitionOptions(state, channel, transition);
            float fadeIn = resolvedTransition.fadeIn > 0f ? resolvedTransition.fadeIn : state.Config.fadeIn;
            float fadeOut = resolvedTransition.fadeOut > 0f ? resolvedTransition.fadeOut : state.Config.fadeOut;
            float speed = Mathf.Approximately(state.Config.speed, 0f) ? 1f : state.Config.speed;
            bool drivesRootMotion = ResolveRootMotion(state, channel);
            bool isLooping = state.Config.loop;
            string clipKey = state is XAnimationCompiledSingleState singleState
                ? ResolveSingleStateClip(singleState).Key
                : string.Empty;
            XAnimationTransitionRequestSource source = transition != null ? explicitSource : ResolveRequestSource(state, channel);

            return new XAnimationTransitionRequest(
                channel.Name,
                state.Key,
                clipKey,
                source,
                fadeIn,
                fadeOut,
                resolvedTransition.enterTime,
                speed,
                isLooping,
                resolvedTransition.priority,
                resolvedTransition.interruptible,
                drivesRootMotion,
                force);
        }

        private XAnimationTransitionOptions ResolveTransitionOptions(
            XAnimationCompiledState state,
            XAnimationCompiledChannel channel,
            XAnimationTransitionOptions transition)
        {
            if (transition != null)
            {
                return transition;
            }

            if (!IsTemporaryClipState(state.Key) &&
                m_ChannelMap.TryGetValue(channel.Name, out XAnimationChannel runtimeChannel) &&
                runtimeChannel.TryGetCurrentPlayback(out XAnimationStatePlaybackInstance currentPlayback) &&
                currentPlayback != null &&
                !currentPlayback.IsTemporaryState &&
                CompiledAsset.TryGetDefaultTransition(currentPlayback.StateKey, state.Key, out XAnimationCompiledDefaultTransition defaultTransition))
            {
                return defaultTransition.CreateTransitionOptions();
            }

            return new XAnimationTransitionOptions();
        }

        private XAnimationTransitionRequestSource ResolveRequestSource(
            XAnimationCompiledState state,
            XAnimationCompiledChannel channel)
        {
            if (IsTemporaryClipState(state.Key))
            {
                return XAnimationTransitionRequestSource.ExplicitPlay;
            }

            if (m_ChannelMap.TryGetValue(channel.Name, out XAnimationChannel runtimeChannel) &&
                runtimeChannel.TryGetCurrentPlayback(out XAnimationStatePlaybackInstance currentPlayback) &&
                currentPlayback != null &&
                !currentPlayback.IsTemporaryState &&
                CompiledAsset.TryGetDefaultTransition(currentPlayback.StateKey, state.Key, out _))
            {
                return XAnimationTransitionRequestSource.DefaultTransition;
            }

            return XAnimationTransitionRequestSource.ExplicitPlay;
        }

        private bool CanTransitionToState(
            XAnimationCompiledChannel channel,
            XAnimationCompiledState targetState,
            out XAnimationTransitionRejectReason rejectReason)
        {
            rejectReason = XAnimationTransitionRejectReason.None;
            if (!m_ChannelMap.TryGetValue(channel.Name, out XAnimationChannel runtimeChannel) ||
                !runtimeChannel.TryGetCurrentPlayback(out XAnimationStatePlaybackInstance currentPlayback) ||
                currentPlayback == null)
            {
                return true;
            }

            if (!CompiledAsset.TryGetStateIndex(currentPlayback.StateKey, out int currentStateIndex))
            {
                return true;
            }

            XAnimationCompiledState currentState = (XAnimationCompiledState)CompiledAsset.States[currentStateIndex];
            string[] allowedNext = currentState.Config.allowedNextStateKeys ?? Array.Empty<string>();
            if (allowedNext.Length > 0 && Array.IndexOf(allowedNext, targetState.Key) < 0)
            {
                rejectReason = XAnimationTransitionRejectReason.SourceStateDisallowTarget;
                return false;
            }

            string[] allowedPrevious = targetState.Config.allowedPreviousStateKeys ?? Array.Empty<string>();
            if (allowedPrevious.Length > 0 && Array.IndexOf(allowedPrevious, currentState.Key) < 0)
            {
                rejectReason = XAnimationTransitionRejectReason.TargetStateDisallowSource;
                return false;
            }

            return true;
        }

        private bool CanTransitionFromCurrentPlayback(
            XAnimationChannel channel,
            XAnimationCompiledState targetState,
            out XAnimationTransitionRejectReason rejectReason)
        {
            rejectReason = XAnimationTransitionRejectReason.None;
            if (channel == null ||
                !channel.TryGetCurrentPlayback(out XAnimationStatePlaybackInstance currentPlayback) ||
                currentPlayback == null)
            {
                return true;
            }

            string[] allowedNext = GetAllowedNextStateKeys(currentPlayback);
            if (allowedNext.Length > 0 && Array.IndexOf(allowedNext, targetState.Key) < 0)
            {
                rejectReason = XAnimationTransitionRejectReason.SourceStateDisallowTarget;
                return false;
            }

            string[] allowedPrevious = targetState.Config.allowedPreviousStateKeys ?? Array.Empty<string>();
            if (allowedPrevious.Length > 0 && Array.IndexOf(allowedPrevious, currentPlayback.StateKey) < 0)
            {
                rejectReason = XAnimationTransitionRejectReason.TargetStateDisallowSource;
                return false;
            }

            return true;
        }

        private string[] GetAllowedNextStateKeys(XAnimationStatePlaybackInstance playback)
        {
            if (playback == null)
            {
                return Array.Empty<string>();
            }

            if (CompiledAsset.TryGetStateIndex(playback.StateKey, out int stateIndex))
            {
                return ((XAnimationCompiledState)CompiledAsset.States[stateIndex]).Config.allowedNextStateKeys ?? Array.Empty<string>();
            }

            return Array.Empty<string>();
        }

        private void StopRuntime(string channelName, float fadeOut = 0)
        {
            ThrowIfDisposed();
            XAnimationChannel channel = GetChannel(channelName);
            channel.Stop(fadeOut > 0f ? fadeOut : channel.CompiledChannel.Config.defaultFadeOut, m_CueDispatcher);
        }

        private void StopAllRuntime(float fadeOut = 0)
        {
            ThrowIfDisposed();
            foreach (XAnimationChannel channel in m_Channels)
            {
                float actualFadeOut = fadeOut > 0f ? fadeOut : channel.CompiledChannel.Config.defaultFadeOut;
                channel.Stop(actualFadeOut, m_CueDispatcher);
            }
        }

        private void SetChannelWeightRuntime(string channelName, float weight)
        {
            ThrowIfDisposed();
            GetChannel(channelName).SetChannelWeight(weight);
        }

        private void SetChannelTimeScaleRuntime(string channelName, float timeScale)
        {
            ThrowIfDisposed();
            GetChannel(channelName).SetChannelTimeScale(timeScale);
        }

        private bool SeekChannelRuntime(string channelName, float normalizedTime)
        {
            ThrowIfDisposed();
            return GetChannel(channelName).SeekCurrent(Mathf.Clamp01(normalizedTime));
        }

        private void SetRootMotionEnabledRuntime(bool enabled)
        {
            ThrowIfDisposed();
            if (m_RootMotionEnabled == enabled)
            {
                SyncAnimatorRootMotion();
                return;
            }

            m_RootMotionEnabled = enabled;
            SyncAnimatorRootMotion();
        }

        private bool ShouldApplyNativeRootMotionRuntime()
        {
            ThrowIfDisposed();
            return m_RootMotionEnabled;
        }

        private XAnimationChannelState GetChannelStateRuntime(string channelName)
        {
            ThrowIfDisposed();
            XAnimationChannel channel = GetChannel(channelName);
            XAnimationChannelState state = channel.GetState();
            if (state == null)
            {
                return null;
            }

            state.nextStateKey = string.Empty;
            if (CompiledAsset.TryGetAutoTransition(state.stateKey, out XAnimationCompiledAutoTransition transition))
            {
                state.nextStateKey = transition.NextStateKey ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(state.transitionTargetStateKey))
            {
                state.nextStateKey = state.transitionTargetStateKey;
            }

            return state;
        }

        private bool TryGetCurrentStateRuntime(string channelName, out XAnimationChannelState state)
        {
            ThrowIfDisposed();
            state = GetChannelStateRuntime(channelName);
            return state != null;
        }

        private bool IsPlayingRuntime(string stateKey, string channelName = null)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(channelName))
            {
                XAnimationChannelState state = GetChannelStateRuntime(channelName);
                return state != null && string.Equals(state.stateKey, stateKey, StringComparison.Ordinal);
            }

            for (int i = 0; i < m_Channels.Count; i++)
            {
                XAnimationChannelState state = m_Channels[i].GetState();
                if (state != null && string.Equals(state.stateKey, stateKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private float GetStateDurationRuntime(string stateKey)
        {
            ThrowIfDisposed();
            return CompiledAsset.GetStateDuration(stateKey);
        }

        private float GetClipDurationRuntime(string clipKey)
        {
            ThrowIfDisposed();
            return CompiledAsset.GetClipDuration(clipKey);
        }

        private void PreloadAllRuntime()
        {
            ThrowIfDisposed();
            CompiledAsset.PreloadAll();
        }

        private void PreloadStateRuntime(string stateKey)
        {
            ThrowIfDisposed();
            CompiledAsset.PreloadState(stateKey);
        }

        private void EvaluateRuntime(float deltaTime)
        {
            ThrowIfDisposed();
            if (deltaTime < 0f)
            {
                throw new XAnimationException("XAnimation deltaTime cannot be negative.");
            }

            for (int i = 0; i < m_Channels.Count; i++)
            {
                XAnimationChannel channel = m_Channels[i];
                channel.PrepareFrame(deltaTime, Context, m_UseDirectChannelOutput);
                if (!m_UseDirectChannelOutput)
                {
                    SetLayerInputWeight(i, channel.HasActivePlayback ? channel.ChannelWeight : 0f);
                }
            }

            m_Graph.Evaluate(deltaTime);

            for (int i = 0; i < m_Channels.Count; i++)
            {
                m_Channels[i].FinalizeFrame(m_CueDispatcher);
            }

            ProcessCompletedNonLoopTransitions();

        }

        private void DisposeRuntime()
        {
            m_CueDispatcher.CueTriggered -= RaiseCueTriggered;
            if (!m_RuntimeInitialized)
            {
                m_CueDispatcher.Clear();
                return;
            }

            foreach (XAnimationChannel channel in m_Channels)
            {
                channel.Dispose(m_CueDispatcher);
            }

            m_Channels.Clear();
            m_ChannelMap.Clear();

            if (m_Graph.IsValid())
            {
                m_Graph.Destroy();
            }

            RestoreAnimatorController();
            m_CueDispatcher.Clear();
            m_RuntimeInitialized = false;
        }

        private void BuildGraph()
        {
            m_CueDispatcher.Register(CompiledAsset.CuesByClipKey);
            DisableAnimatorController();

            m_Graph = PlayableGraph.Create($"XAnimationDriver_{Animator.name}");
            m_Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            m_Output = AnimationPlayableOutput.Create(m_Graph, "XAnimationOutput", Animator);
            m_UseDirectChannelOutput = ShouldUseDirectChannelOutput();
            if (!m_UseDirectChannelOutput)
            {
                m_LayerMixer = AnimationLayerMixerPlayable.Create(m_Graph, CompiledAsset.Channels.Count);
                m_Output.SetSourcePlayable(m_LayerMixer);
                m_LastLayerInputWeights = new float[CompiledAsset.Channels.Count];
                for (int i = 0; i < m_LastLayerInputWeights.Length; i++)
                {
                    m_LastLayerInputWeights[i] = float.NaN;
                }
            }
            else
            {
                m_LastLayerInputWeights = null;
            }

            for (int i = 0; i < CompiledAsset.Channels.Count; i++)
            {
                XAnimationCompiledChannel compiledChannel = (XAnimationCompiledChannel)CompiledAsset.Channels[i];
                XAnimationChannel channel = new(
                    m_Graph,
                    compiledChannel,
                    NextPlaybackId,
                    OnStateEntered,
                    OnStateExited);
                m_Channels.Add(channel);
                m_ChannelMap.Add(channel.Name, channel);
                if (m_UseDirectChannelOutput)
                {
                    m_Output.SetSourcePlayable(channel.Mixer);
                }
                else
                {
                    m_Graph.Connect(channel.Mixer, 0, m_LayerMixer, i);
                    SetLayerInputWeight(i, compiledChannel.Config.defaultWeight, force: true);
                    m_LayerMixer.SetLayerAdditive((uint)i, compiledChannel.Config.layerType == XAnimationChannelLayerType.Additive);
                    if (compiledChannel.Mask != null)
                    {
                        m_LayerMixer.SetLayerMaskFromAvatarMask((uint)i, compiledChannel.Mask);
                    }
                }
            }

            m_Graph.Play();
            m_RootMotionEnabled = CompiledAsset.RootMotionEnabled;
            SyncAnimatorRootMotion();
            m_RuntimeInitialized = true;
        }

        private bool ShouldUseDirectChannelOutput()
        {
            if (CompiledAsset.Channels.Count != 1)
            {
                return false;
            }

            XAnimationCompiledChannel channel = (XAnimationCompiledChannel)CompiledAsset.Channels[0];
            return channel.Config.layerType == XAnimationChannelLayerType.Base &&
                   channel.Mask == null;
        }

        private void SetLayerInputWeight(int inputIndex, float weight, bool force = false)
        {
            if (!m_LayerMixer.IsValid())
            {
                return;
            }

            if (m_LastLayerInputWeights == null ||
                inputIndex < 0 ||
                inputIndex >= m_LastLayerInputWeights.Length ||
                force ||
                float.IsNaN(m_LastLayerInputWeights[inputIndex]) ||
                Mathf.Abs(m_LastLayerInputWeights[inputIndex] - weight) > 0.00001f)
            {
                m_LayerMixer.SetInputWeight(inputIndex, weight);
                if (m_LastLayerInputWeights != null &&
                    inputIndex >= 0 &&
                    inputIndex < m_LastLayerInputWeights.Length)
                {
                    m_LastLayerInputWeights[inputIndex] = weight;
                }
            }
        }

        private void DisableAnimatorController()
        {
            m_OriginalController = Animator.runtimeAnimatorController;
            m_OriginalApplyRootMotion = Animator.applyRootMotion;
            Animator.runtimeAnimatorController = null;
            Animator.applyRootMotion = false;
        }

        private void RestoreAnimatorController()
        {
            if (Animator == null)
            {
                return;
            }

            if (m_OriginalController != null && Animator.runtimeAnimatorController == null)
            {
                Animator.runtimeAnimatorController = m_OriginalController;
            }

            Animator.applyRootMotion = m_OriginalApplyRootMotion;
        }

        private void SyncAnimatorRootMotion()
        {
            if (Animator != null && Animator.applyRootMotion != m_RootMotionEnabled)
            {
                Animator.applyRootMotion = m_RootMotionEnabled;
            }
        }

        private XAnimationCompiledChannel ResolveClipChannel(XAnimationCompiledClip clip, string channelName)
        {
            if (!string.IsNullOrWhiteSpace(channelName))
            {
                return CompiledAsset.GetChannel(channelName);
            }

            throw new XAnimationException($"XAnimation clip '{clip.Key}' direct playback requires an explicit channelName.");
        }

        private XAnimationCompiledChannel GetStateChannel(XAnimationCompiledState state)
        {
            return (XAnimationCompiledChannel)CompiledAsset.Channels[state.DefaultChannelIndex];
        }

        private bool ResolveRootMotion(XAnimationCompiledState state, XAnimationCompiledChannel channel)
        {
            bool drivesRootMotion = state.Config.rootMotionMode switch
            {
                XAnimationClipRootMotionMode.ForceOn => true,
                XAnimationClipRootMotionMode.ForceOff => false,
                _ => channel.Config.canDriveRootMotion,
            };

            if (drivesRootMotion && !channel.Config.canDriveRootMotion)
            {
                throw new XAnimationException($"XAnimation state '{state.Key}' cannot drive root motion on channel '{channel.Name}'.");
            }

            return drivesRootMotion;
        }

        private XAnimationCompiledSingleState CreateTemporaryClipState(
            XAnimationCompiledClip clip,
            int clipIndex,
            XAnimationCompiledChannel channel,
            int channelIndex)
        {
            XAnimationStateConfig config = CreateTemporaryClipStateConfig(clip, channel);
            config.key = CreateTemporaryClipStateKey(clip.Key);
            return new XAnimationCompiledSingleState(config, channelIndex, clipIndex);
        }

        private XAnimationCompiledSingleState CreateTemporaryDirectClipState(
            XAnimationCompiledClip clip,
            XAnimationCompiledChannel channel,
            int channelIndex)
        {
            XAnimationStateConfig config = CreateTemporaryClipStateConfig(clip, channel);
            config.key = CreateTemporaryClipStateKey(clip.Key);
            return new XAnimationCompiledSingleState(config, channelIndex, clip);
        }

        private static XAnimationStateConfig CreateTemporaryClipStateConfig(
            XAnimationCompiledClip clip,
            XAnimationCompiledChannel channel)
        {
            return new XAnimationStateConfig
            {
                key = string.Empty,
                stateType = XAnimationStateType.Single,
                clipKey = clip.Key,
                channelName = channel.Name,
                fadeIn = channel.Config.defaultFadeIn,
                fadeOut = channel.Config.defaultFadeOut,
                speed = 1f,
                loop = clip.PlaybackClip.isLooping,
                rootMotionMode = XAnimationClipRootMotionMode.Inherit,
                parameterName = string.Empty,
                parameterXName = string.Empty,
                parameterYName = string.Empty,
                samples = Array.Empty<XAnimationBlend1DSampleConfig>(),
                directionalSamples = Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>(),
            };
        }

        private string CreateTemporaryClipStateKey(string clipKey)
        {
            string stateKey;
            do
            {
                stateKey = $"{TemporaryClipStateKeyPrefix}{clipKey}:{m_NextTemporaryStateId++}";
            }
            while (CompiledAsset.TryGetStateIndex(stateKey, out _));

            return stateKey;
        }

        private static string CreateDirectClipKey(AnimationClip clip)
        {
            string clipName = clip != null && !string.IsNullOrWhiteSpace(clip.name)
                ? clip.name
                : "UnnamedClip";
            return $"{DirectClipKeyPrefix}{clipName}";
        }

        private XAnimationCompiledClip ResolveSingleStateClip(XAnimationCompiledSingleState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.HasDirectClip)
            {
                return state.DirectClip;
            }

            return (XAnimationCompiledClip)CompiledAsset.Clips[state.ClipIndex];
        }

        private XAnimationStatePlaybackInstance CreateStatePlayback(
            int playbackId,
            string channelName,
            XAnimationCompiledState state,
            XAnimationPlaybackRuntimeOptions options)
        {
            bool isTemporaryState = IsTemporaryClipState(state.Key);
            return state switch
            {
                XAnimationCompiledSingleState singleState => CreateSinglePlayback(
                    playbackId,
                    channelName,
                    singleState.Key,
                    ResolveSingleStateClip(singleState),
                    isTemporaryState,
                    options),
                XAnimationCompiledBlend1DState blendState => CreateBlend1DPlayback(
                    playbackId,
                    channelName,
                    blendState,
                    isTemporaryState,
                    options),
                XAnimationCompiledBlend2DSimpleDirectionalState directionalState => CreateBlend2DSimpleDirectionalPlayback(
                    playbackId,
                    channelName,
                    directionalState,
                    isTemporaryState,
                    options),
                XAnimationCompiledBlend2DFreeformDirectionalState freeformState => CreateBlend2DFreeformDirectionalPlayback(
                    playbackId,
                    channelName,
                    freeformState,
                    isTemporaryState,
                    options),
                _ => throw new XAnimationException($"XAnimation state '{state.Key}' has unsupported stateType '{state.StateType}'."),
            };
        }

        private XAnimationSingleStatePlaybackInstance CreateSinglePlayback(
            int playbackId,
            string channelName,
            string stateKey,
            XAnimationCompiledClip clip,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
        {
            AnimationClipPlayable playable = AnimationClipPlayable.Create(m_Graph, clip.PlaybackClip);
            playable.SetApplyFootIK(false);
            playable.SetTime(Mathf.Clamp01(options.NormalizedTime) * Mathf.Max(clip.PlaybackClip.length, 0.0001f));
            return new XAnimationSingleStatePlaybackInstance(playbackId, channelName, stateKey, Animator, clip, playable, isTemporaryState, options);
        }

        private XAnimationBlend1DStatePlaybackInstance CreateBlend1DPlayback(
            int playbackId,
            string channelName,
            XAnimationCompiledBlend1DState state,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
        {
            XAnimationCompiledClip[] clips = new XAnimationCompiledClip[state.Samples.Count];
            for (int i = 0; i < state.Samples.Count; i++)
            {
                clips[i] = (XAnimationCompiledClip)CompiledAsset.Clips[state.Samples[i].ClipIndex];
            }

            return new XAnimationBlend1DStatePlaybackInstance(m_Graph, playbackId, channelName, Animator, state, clips, isTemporaryState, options);
        }

        private XAnimationBlend2DSimpleDirectionalStatePlaybackInstance CreateBlend2DSimpleDirectionalPlayback(
            int playbackId,
            string channelName,
            XAnimationCompiledBlend2DSimpleDirectionalState state,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
        {
            XAnimationCompiledClip[] clips = new XAnimationCompiledClip[state.Samples.Count];
            for (int i = 0; i < state.Samples.Count; i++)
            {
                clips[i] = (XAnimationCompiledClip)CompiledAsset.Clips[state.Samples[i].ClipIndex];
            }

            return new XAnimationBlend2DSimpleDirectionalStatePlaybackInstance(
                m_Graph,
                playbackId,
                channelName,
                Animator,
                state,
                clips,
                isTemporaryState,
                options);
        }

        private XAnimationBlend2DFreeformDirectionalStatePlaybackInstance CreateBlend2DFreeformDirectionalPlayback(
            int playbackId,
            string channelName,
            XAnimationCompiledBlend2DFreeformDirectionalState state,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
        {
            XAnimationCompiledClip[] clips = new XAnimationCompiledClip[state.Samples.Count];
            for (int i = 0; i < state.Samples.Count; i++)
            {
                clips[i] = (XAnimationCompiledClip)CompiledAsset.Clips[state.Samples[i].ClipIndex];
            }

            return new XAnimationBlend2DFreeformDirectionalStatePlaybackInstance(
                m_Graph,
                playbackId,
                channelName,
                Animator,
                state,
                clips,
                isTemporaryState,
                options);
        }

        private XAnimationChannel GetChannel(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                throw new XAnimationException("XAnimation channelName cannot be empty.");
            }

            if (!m_ChannelMap.TryGetValue(channelName, out XAnimationChannel channel))
            {
                throw new XAnimationException($"XAnimation channel '{channelName}' does not exist.");
            }

            return channel;
        }

        private int NextPlaybackId()
        {
            return m_NextPlaybackId++;
        }

        private static bool IsTemporaryClipState(string stateKey)
        {
            return !string.IsNullOrEmpty(stateKey) &&
                   stateKey.StartsWith(TemporaryClipStateKeyPrefix, StringComparison.Ordinal);
        }

        private void ProcessCompletedNonLoopTransitions()
        {
            for (int i = 0; i < m_Channels.Count; i++)
            {
                XAnimationChannel channel = m_Channels[i];
                if (!channel.TryGetCurrentPlayback(out XAnimationStatePlaybackInstance playback) ||
                    playback == null ||
                    playback.IsLooping ||
                    playback.IsTemporaryState ||
                    playback.HasCompletedExitOrTransition ||
                    !CompiledAsset.TryGetStateIndex(playback.StateKey, out int stateIndex))
                {
                    continue;
                }

                XAnimationCompiledState state = (XAnimationCompiledState)CompiledAsset.States[stateIndex];
                bool hasAutoTransition = CompiledAsset.TryGetAutoTransition(state.Key, out XAnimationCompiledAutoTransition autoTransition);
                float exitThreshold = hasAutoTransition && autoTransition.HasNextState
                    ? autoTransition.ExitTime
                    : 1f;
                if (playback.GetTotalNormalizedTime() < exitThreshold)
                {
                    continue;
                }

                if (!hasAutoTransition || !autoTransition.HasNextState)
                {
                    if (channel.TryMarkCompletedExit(out _))
                    {
                        channel.Stop(state.Config.fadeOut, m_CueDispatcher);
                    }

                    continue;
                }

                XAnimationCompiledState nextState = CompiledAsset.GetState(autoTransition.NextStateKey);
                float fadeIn = autoTransition.TransitionDuration > 0f ? autoTransition.TransitionDuration : nextState.Config.fadeIn;
                float fadeOut = autoTransition.TransitionDuration > 0f ? autoTransition.TransitionDuration : nextState.Config.fadeOut;
                XAnimationTransitionRequest request = new(
                    channel.Name,
                    nextState.Key,
                    nextState is XAnimationCompiledSingleState singleState
                        ? ResolveSingleStateClip(singleState).Key
                        : string.Empty,
                    XAnimationTransitionRequestSource.AutoTransition,
                    fadeIn,
                    fadeOut,
                    autoTransition.EnterTime,
                    Mathf.Approximately(nextState.Config.speed, 0f) ? 1f : nextState.Config.speed,
                    nextState.Config.loop,
                    playback.Priority,
                    true,
                    ResolveRootMotion(nextState, channel.CompiledChannel),
                    false);

                if (!CanTransitionFromCurrentPlayback(channel, nextState, out _))
                {
                    continue;
                }

                XAnimationPlaybackStartInfo startInfo = TryPlayCompiledState(nextState, channel.CompiledChannel, request);
                if (startInfo.Started)
                {
                    channel.TryMarkCompletedExit(out _);
                }
            }
        }

        private void OnStateEntered(XAnimationStatePlaybackInstance playback)
        {
            OnStateEnter?.Invoke(BuildStateEvent(playback, null));
        }

        private void OnStateExited(XAnimationStatePlaybackInstance playback, XAnimationStateExitReason reason)
        {
            CompletePlaybackExitAndRaise(BuildStateEvent(playback, reason));
        }

        private static XAnimationStateEvent BuildStateEvent(XAnimationStatePlaybackInstance playback, XAnimationStateExitReason? exitReason)
        {
            return new XAnimationStateEvent
            {
                stateKey = playback?.StateKey ?? string.Empty,
                channelName = playback?.ChannelName ?? string.Empty,
                playbackId = playback?.PlaybackId ?? 0,
                isTemporaryState = playback?.IsTemporaryState ?? false,
                normalizedTime = playback?.GetNormalizedTime() ?? 0f,
                totalNormalizedTime = playback?.GetTotalNormalizedTime() ?? 0f,
                exitReason = exitReason,
            };
        }

        private void ThrowIfDisposed()
        {
            if (!m_RuntimeInitialized)
            {
                throw new XAnimationException("XAnimationDriver is not initialized.");
            }
        }
    }
}
