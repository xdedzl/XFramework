using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace XFramework.Animation
{
    public sealed class XAnimationPlayer : IDisposable
    {
        private readonly List<XAnimationChannel> m_Channels = new();
        private readonly Dictionary<string, XAnimationChannel> m_ChannelMap = new(StringComparer.Ordinal);
        private readonly XAnimationCueDispatcher m_CueDispatcher = new();
        private readonly XAnimationRootMotionResolver m_RootMotionResolver = new();
        private const string TemporaryClipStateKeyPrefix = "__xanimation_temp_clip_state:";

        private PlayableGraph m_Graph;
        private AnimationLayerMixerPlayable m_LayerMixer;
        private AnimationPlayableOutput m_Output;
        private RuntimeAnimatorController m_OriginalController;
        private int m_NextPlaybackId = 1;
        private int m_NextTemporaryStateId = 1;

        public XAnimationPlayer(XAnimationCompiledAsset compiledAsset, Animator animator, XAnimationContext context)
        {
            CompiledAsset = compiledAsset ?? throw new ArgumentNullException(nameof(compiledAsset));
            Animator = animator ? animator : throw new ArgumentNullException(nameof(animator));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            BuildGraph();
        }

        public event Action<XAnimationCueEvent> CueTriggered
        {
            add => m_CueDispatcher.CueTriggered += value;
            remove => m_CueDispatcher.CueTriggered -= value;
        }

        public event Action<XAnimationStateEvent> StateEntered;
        public event Action<XAnimationStateEvent> StateExited;

        public XAnimationCompiledAsset CompiledAsset { get; }
        public Animator Animator { get; }
        public XAnimationContext Context { get; }
        public bool IsDisposed { get; private set; }

        public void PlayClip(string clipKey, string channelName, XAnimationTransitionOptions transition = default)
        {
            ThrowIfDisposed();
            if (!CompiledAsset.TryGetClipIndex(clipKey, out int clipIndex))
            {
                throw new XFrameworkException($"XAnimation clip '{clipKey}' does not exist.");
            }

            XAnimationCompiledClip clip = (XAnimationCompiledClip)CompiledAsset.Clips[clipIndex];
            XAnimationCompiledChannel channel = ResolveClipChannel(clip, channelName);
            if (!CompiledAsset.TryGetChannelIndex(channel.Name, out int channelIndex))
            {
                throw new XFrameworkException($"XAnimation channel '{channel.Name}' does not exist.");
            }

            XAnimationCompiledSingleState temporaryState = CreateTemporaryClipState(clip, clipIndex, channel, channelIndex);
            TryPlayCompiledState(temporaryState, channel, transition);
        }

        public void PlayState(string stateKey, XAnimationTransitionOptions transition = default)
        {
            ThrowIfDisposed();
            XAnimationCompiledState state = CompiledAsset.GetState(stateKey);
            XAnimationCompiledChannel channel = GetStateChannel(state);
            TryPlayCompiledState(state, channel, transition);
        }

        private bool TryPlayCompiledState(
            XAnimationCompiledState state,
            XAnimationCompiledChannel channel,
            XAnimationTransitionOptions transition)
        {
            transition ??= new XAnimationTransitionOptions();

            float fadeIn = transition.fadeIn > 0f ? transition.fadeIn : state.Config.fadeIn;
            float fadeOut = transition.fadeOut > 0f ? transition.fadeOut : state.Config.fadeOut;
            float normalizedTime = Mathf.Clamp01(transition.enterTime);
            float speed = Mathf.Approximately(state.Config.speed, 0f) ? 1f : state.Config.speed;
            bool interruptible = transition.interruptible;
            bool drivesRootMotion = ResolveRootMotion(state, channel);
            bool isLooping = state.Config.loop;

            XAnimationPlaybackRuntimeOptions options = new(
                fadeIn,
                fadeOut,
                1f,
                normalizedTime,
                speed,
                isLooping,
                transition.priority,
                interruptible,
                drivesRootMotion);

            if (!m_ChannelMap[channel.Name].TryPlay(
                (playbackId, actualOptions) => CreateStatePlayback(playbackId, channel.Name, state, actualOptions),
                options,
                m_CueDispatcher))
            {
                return false;
            }

            return true;
        }

        public void Stop(string channelName, float fadeOut = default)
        {
            ThrowIfDisposed();
            XAnimationChannel channel = GetChannel(channelName);
            channel.Stop(fadeOut > 0f ? fadeOut : channel.CompiledChannel.Config.defaultFadeOut, m_CueDispatcher);
        }

        public void StopAll(float fadeOut = default)
        {
            ThrowIfDisposed();
            foreach (XAnimationChannel channel in m_Channels)
            {
                float actualFadeOut = fadeOut > 0f ? fadeOut : channel.CompiledChannel.Config.defaultFadeOut;
                channel.Stop(actualFadeOut, m_CueDispatcher);
            }
        }

        public void SetChannelWeight(string channelName, float weight)
        {
            ThrowIfDisposed();
            GetChannel(channelName).SetChannelWeight(weight);
        }

        public void SetChannelTimeScale(string channelName, float timeScale)
        {
            ThrowIfDisposed();
            GetChannel(channelName).SetChannelTimeScale(timeScale);
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            ThrowIfDisposed();
            m_RootMotionResolver.SetEnabled(enabled);
            m_RootMotionResolver.ApplyToAnimator(Animator);
        }

        public XAnimationChannelState GetChannelState(string channelName)
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

            return state;
        }

        public bool TryGetCurrentState(string channelName, out XAnimationChannelState state)
        {
            ThrowIfDisposed();
            state = GetChannelState(channelName);
            return state != null;
        }

        public bool IsPlaying(string stateKey, string channelName = null)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(channelName))
            {
                XAnimationChannelState state = GetChannelState(channelName);
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

        public float GetStateDuration(string stateKey)
        {
            ThrowIfDisposed();
            return CompiledAsset.GetStateDuration(stateKey);
        }

        public float GetClipDuration(string clipKey)
        {
            ThrowIfDisposed();
            return CompiledAsset.GetClipDuration(clipKey);
        }

        public void Update(float deltaTime)
        {
            ThrowIfDisposed();
            if (deltaTime < 0f)
            {
                throw new XFrameworkException("XAnimation deltaTime cannot be negative.");
            }

            for (int i = 0; i < m_Channels.Count; i++)
            {
                XAnimationChannel channel = m_Channels[i];
                channel.PrepareFrame(deltaTime, Context);
                m_LayerMixer.SetInputWeight(i, channel.HasActivePlayback ? channel.ChannelWeight : 0f);
            }

            m_RootMotionResolver.ResolveSource(m_Channels);
            m_RootMotionResolver.ApplyToAnimator(Animator);

            m_Graph.Evaluate(deltaTime);

            for (int i = 0; i < m_Channels.Count; i++)
            {
                m_Channels[i].FinalizeFrame(m_CueDispatcher);
            }

            ProcessCompletedNonLoopTransitions();

            m_RootMotionResolver.ResolveSource(m_Channels);
            m_RootMotionResolver.ApplyToAnimator(Animator);
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
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

            IsDisposed = true;
        }

        private void BuildGraph()
        {
            m_CueDispatcher.Register(CompiledAsset.CuesByClipKey);
            DisableAnimatorController();

            m_Graph = PlayableGraph.Create($"XAnimationPlayer_{Animator.name}");
            m_Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            m_LayerMixer = AnimationLayerMixerPlayable.Create(m_Graph, CompiledAsset.Channels.Count);
            m_Output = AnimationPlayableOutput.Create(m_Graph, "XAnimationOutput", Animator);
            m_Output.SetSourcePlayable(m_LayerMixer);

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
                m_Graph.Connect(channel.Mixer, 0, m_LayerMixer, i);
                m_LayerMixer.SetInputWeight(i, compiledChannel.Config.defaultWeight);
                m_LayerMixer.SetLayerAdditive((uint)i, compiledChannel.Config.layerType == XAnimationChannelLayerType.Additive);
                if (compiledChannel.Mask != null)
                {
                    m_LayerMixer.SetLayerMaskFromAvatarMask((uint)i, compiledChannel.Mask);
                }
            }

            m_Graph.Play();
            m_RootMotionResolver.ApplyToAnimator(Animator);
        }

        private void DisableAnimatorController()
        {
            m_OriginalController = Animator.runtimeAnimatorController;
            Animator.runtimeAnimatorController = null;
        }

        private void RestoreAnimatorController()
        {
            if (Animator == null || m_OriginalController == null || Animator.runtimeAnimatorController != null)
            {
                return;
            }

            Animator.runtimeAnimatorController = m_OriginalController;
        }

        private XAnimationCompiledChannel ResolveClipChannel(XAnimationCompiledClip clip, string channelName)
        {
            if (!string.IsNullOrWhiteSpace(channelName))
            {
                return CompiledAsset.GetChannel(channelName);
            }

            throw new XFrameworkException($"XAnimation clip '{clip.Key}' direct playback requires an explicit channelName.");
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
                throw new XFrameworkException($"XAnimation state '{state.Key}' cannot drive root motion on channel '{channel.Name}'.");
            }

            return drivesRootMotion;
        }

        private XAnimationCompiledSingleState CreateTemporaryClipState(
            XAnimationCompiledClip clip,
            int clipIndex,
            XAnimationCompiledChannel channel,
            int channelIndex)
        {
            XAnimationStateConfig config = new()
            {
                key = CreateTemporaryClipStateKey(clip.Key),
                stateType = XAnimationStateType.Single,
                clipKey = clip.Key,
                channelName = channel.Name,
                fadeIn = channel.Config.defaultFadeIn,
                fadeOut = channel.Config.defaultFadeOut,
                speed = 1f,
                loop = clip.PlaybackClip.isLooping,
                rootMotionMode = XAnimationClipRootMotionMode.Inherit,
                parameterName = string.Empty,
                samples = Array.Empty<XAnimationBlend1DSampleConfig>(),
            };

            return new XAnimationCompiledSingleState(config, channelIndex, clipIndex);
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
                    (XAnimationCompiledClip)CompiledAsset.Clips[singleState.ClipIndex],
                    isTemporaryState,
                    options),
                XAnimationCompiledBlend1DState blendState => CreateBlend1DPlayback(
                    playbackId,
                    channelName,
                    blendState,
                    isTemporaryState,
                    options),
                _ => throw new XFrameworkException($"XAnimation state '{state.Key}' has unsupported stateType '{state.StateType}'."),
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

        private XAnimationChannel GetChannel(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                throw new XFrameworkException("XAnimation channelName cannot be empty.");
            }

            if (!m_ChannelMap.TryGetValue(channelName, out XAnimationChannel channel))
            {
                throw new XFrameworkException($"XAnimation channel '{channelName}' does not exist.");
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

                if (!channel.TryMarkCompletedExit(out _))
                {
                    continue;
                }

                XAnimationCompiledState nextState = CompiledAsset.GetState(autoTransition.NextStateKey);
                float fadeIn = autoTransition.TransitionDuration > 0f ? autoTransition.TransitionDuration : nextState.Config.fadeIn;
                float fadeOut = autoTransition.TransitionDuration > 0f ? autoTransition.TransitionDuration : nextState.Config.fadeOut;
                bool played = TryPlayCompiledState(
                    nextState,
                    channel.CompiledChannel,
                    new XAnimationTransitionOptions
                    {
                        fadeIn = fadeIn,
                        fadeOut = fadeOut,
                        enterTime = autoTransition.EnterTime,
                        priority = playback.Priority,
                        interruptible = true,
                    });

                if (!played)
                {
                    channel.Stop(state.Config.fadeOut, m_CueDispatcher);
                }
            }
        }

        private void OnStateEntered(XAnimationStatePlaybackInstance playback)
        {
            StateEntered?.Invoke(BuildStateEvent(playback, null));
        }

        private void OnStateExited(XAnimationStatePlaybackInstance playback, XAnimationStateExitReason reason)
        {
            StateExited?.Invoke(BuildStateEvent(playback, reason));
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
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(XAnimationPlayer));
            }
        }
    }
}
