using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace XFramework.Animation
{
    public readonly struct XAnimationPlaybackRuntimeOptions
    {
        public XAnimationPlaybackRuntimeOptions(
            float fadeIn,
            float fadeOut,
            float weight,
            float normalizedTime,
            float speed,
            bool isLooping,
            int priority,
            bool interruptible,
            bool drivesRootMotion)
        {
            FadeIn = fadeIn;
            FadeOut = fadeOut;
            Weight = weight;
            NormalizedTime = normalizedTime;
            Speed = speed;
            IsLooping = isLooping;
            Priority = priority;
            Interruptible = interruptible;
            DrivesRootMotion = drivesRootMotion;
        }

        public float FadeIn { get; }
        public float FadeOut { get; }
        public float Weight { get; }
        public float NormalizedTime { get; }
        public float Speed { get; }
        public bool IsLooping { get; }
        public int Priority { get; }
        public bool Interruptible { get; }
        public bool DrivesRootMotion { get; }
    }

    public abstract class XAnimationStatePlaybackInstance
    {
        private float m_FadeElapsed;

        protected XAnimationStatePlaybackInstance(
            int playbackId,
            string channelName,
            string stateKey,
            XAnimationStateType stateType,
            XAnimationPlaybackRuntimeOptions options)
        {
            PlaybackId = playbackId;
            ChannelName = channelName;
            StateKey = stateKey;
            StateType = stateType;
            TargetWeight = Mathf.Max(0f, options.Weight);
            CurrentWeight = options.FadeIn > 0f ? 0f : TargetWeight;
            Speed = options.Speed;
            IsLooping = options.IsLooping;
            Priority = options.Priority;
            Interruptible = options.Interruptible;
            DrivesRootMotion = options.DrivesRootMotion;
            FadeFrom = CurrentWeight;
            FadeTo = TargetWeight;
            FadeDuration = Mathf.Max(0f, options.FadeIn);
            m_FadeElapsed = 0f;
        }

        public int PlaybackId { get; }
        public string ChannelName { get; }
        public string StateKey { get; }
        public XAnimationStateType StateType { get; }
        public abstract string PrimaryClipKey { get; }
        public abstract Playable OutputPlayable { get; }
        public float CurrentWeight { get; private set; }
        public float TargetWeight { get; }
        public float Speed { get; }
        public bool IsLooping { get; }
        public bool IsFading => FadeDuration > 0f && m_FadeElapsed < FadeDuration;
        public int Priority { get; }
        public bool Interruptible { get; }
        public bool DrivesRootMotion { get; }
        public bool SuppressCues { get; set; }

        private float FadeFrom { get; set; }
        private float FadeTo { get; set; }
        private float FadeDuration { get; set; }

        public void BeginFade(float targetWeight, float duration)
        {
            FadeFrom = CurrentWeight;
            FadeTo = Mathf.Max(0f, targetWeight);
            FadeDuration = Mathf.Max(0f, duration);
            m_FadeElapsed = 0f;

            if (FadeDuration <= 0f)
            {
                CurrentWeight = FadeTo;
            }
        }

        public void PrepareFrame(float deltaTime, float channelTimeScale, XAnimationContext context)
        {
            PrepareStateFrame(deltaTime, channelTimeScale, context);
            UpdateFade(deltaTime);
        }

        public abstract void FinalizeFrame(XAnimationCueDispatcher cueDispatcher);
        public abstract XAnimationChannelState BuildState(float channelWeight, float channelTimeScale);
        public abstract void Dispose(XAnimationCueDispatcher cueDispatcher);

        public bool HasFinishedFadeOut()
        {
            return CurrentWeight <= 0.0001f && (!IsFading || FadeTo <= 0f);
        }

        protected abstract void PrepareStateFrame(float deltaTime, float channelTimeScale, XAnimationContext context);

        private void UpdateFade(float deltaTime)
        {
            if (FadeDuration <= 0f)
            {
                CurrentWeight = FadeTo;
                return;
            }

            m_FadeElapsed = Mathf.Min(m_FadeElapsed + deltaTime, FadeDuration);
            float t = FadeDuration <= Mathf.Epsilon ? 1f : m_FadeElapsed / FadeDuration;
            CurrentWeight = Mathf.Lerp(FadeFrom, FadeTo, t);
        }
    }

    public sealed class XAnimationSingleStatePlaybackInstance : XAnimationStatePlaybackInstance
    {
        private readonly XAnimationCompiledClip m_Clip;
        private readonly AnimationClipPlayable m_Playable;
        private readonly float m_ClipLength;

        private float m_TotalNormalizedTime;
        private float m_PreviousTotalNormalizedTime;

        internal XAnimationSingleStatePlaybackInstance(
            int playbackId,
            string channelName,
            string stateKey,
            XAnimationCompiledClip clip,
            AnimationClipPlayable playable,
            XAnimationPlaybackRuntimeOptions options)
            : base(playbackId, channelName, stateKey, XAnimationStateType.Single, options)
        {
            m_Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            m_Playable = playable;
            m_ClipLength = Mathf.Max(clip.Clip.length, 0.0001f);
            m_TotalNormalizedTime = Mathf.Clamp01(options.NormalizedTime);
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
        }

        public override string PrimaryClipKey => m_Clip.Key;
        public override Playable OutputPlayable => m_Playable;
        public float TotalNormalizedTime => m_TotalNormalizedTime;
        public float PreviousTotalNormalizedTime => m_PreviousTotalNormalizedTime;
        public float NormalizedTime => IsLooping ? Mathf.Repeat(m_TotalNormalizedTime, 1f) : Mathf.Clamp01(m_TotalNormalizedTime);

        public XAnimationCompiledClip Clip => m_Clip;

        protected override void PrepareStateFrame(float deltaTime, float channelTimeScale, XAnimationContext context)
        {
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            m_Playable.SetSpeed(Speed * channelTimeScale);
            m_TotalNormalizedTime += deltaTime * Speed * channelTimeScale / m_ClipLength;
        }

        public override void FinalizeFrame(XAnimationCueDispatcher cueDispatcher)
        {
            if (!m_Playable.IsValid())
            {
                return;
            }

            double playableTime = m_Playable.GetTime();
            if (IsLooping)
            {
                if (playableTime >= m_ClipLength)
                {
                    m_Playable.SetTime(playableTime % m_ClipLength);
                }
                else if (playableTime < 0d)
                {
                    double wrappedTime = playableTime % m_ClipLength;
                    if (wrappedTime < 0d)
                    {
                        wrappedTime += m_ClipLength;
                    }

                    m_Playable.SetTime(wrappedTime);
                }
            }
            else if (playableTime > m_ClipLength)
            {
                m_Playable.SetTime(m_ClipLength);
            }
            else if (playableTime < 0d)
            {
                m_Playable.SetTime(0d);
            }

            if (!SuppressCues)
            {
                cueDispatcher?.Update(this, m_Clip.Key, m_PreviousTotalNormalizedTime, m_TotalNormalizedTime);
            }
        }

        public override XAnimationChannelState BuildState(float channelWeight, float channelTimeScale)
        {
            return new XAnimationChannelState
            {
                channelName = ChannelName,
                stateKey = StateKey,
                stateType = StateType,
                clipKey = PrimaryClipKey,
                playbackId = PlaybackId,
                normalizedTime = NormalizedTime,
                totalNormalizedTime = m_TotalNormalizedTime,
                weight = CurrentWeight,
                channelWeight = channelWeight,
                speed = Speed * channelTimeScale,
                timeScale = channelTimeScale,
                isLooping = IsLooping,
                isFading = IsFading,
                priority = Priority,
                interruptible = Interruptible,
            };
        }

        public override void Dispose(XAnimationCueDispatcher cueDispatcher)
        {
            cueDispatcher?.RemovePlayback(PlaybackId);
            if (m_Playable.IsValid())
            {
                m_Playable.Destroy();
            }
        }
    }

    public sealed class XAnimationBlend1DStatePlaybackInstance : XAnimationStatePlaybackInstance
    {
        private const float ActiveCueWeightThreshold = 0.0001f;

        private readonly PlayableGraph m_Graph;
        private readonly XAnimationCompiledBlend1DState m_State;
        private readonly XAnimationCompiledClip[] m_Clips;
        private readonly AnimationMixerPlayable m_Mixer;
        private readonly AnimationClipPlayable[] m_Playables;
        private readonly float[] m_ClipLengths;
        private readonly float[] m_TotalNormalizedTimes;
        private readonly float[] m_PreviousTotalNormalizedTimes;
        private readonly float[] m_SampleWeights;

        private int m_PrimaryClipIndex;

        internal XAnimationBlend1DStatePlaybackInstance(
            PlayableGraph graph,
            int playbackId,
            string channelName,
            XAnimationCompiledBlend1DState state,
            XAnimationCompiledClip[] clips,
            XAnimationPlaybackRuntimeOptions options)
            : base(playbackId, channelName, state?.Key, XAnimationStateType.Blend1D, options)
        {
            m_Graph = graph;
            m_State = state ?? throw new ArgumentNullException(nameof(state));
            m_Clips = clips ?? throw new ArgumentNullException(nameof(clips));
            m_Mixer = AnimationMixerPlayable.Create(graph, m_Clips.Length, true);
            m_Playables = new AnimationClipPlayable[m_Clips.Length];
            m_ClipLengths = new float[m_Clips.Length];
            m_TotalNormalizedTimes = new float[m_Clips.Length];
            m_PreviousTotalNormalizedTimes = new float[m_Clips.Length];
            m_SampleWeights = new float[m_Clips.Length];

            float normalizedTime = Mathf.Clamp01(options.NormalizedTime);
            for (int i = 0; i < m_Clips.Length; i++)
            {
                XAnimationCompiledClip clip = m_Clips[i];
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip.Clip);
                playable.SetApplyFootIK(false);
                float clipLength = Mathf.Max(clip.Clip.length, 0.0001f);
                playable.SetTime(normalizedTime * clipLength);
                m_Playables[i] = playable;
                m_ClipLengths[i] = clipLength;
                m_TotalNormalizedTimes[i] = normalizedTime;
                m_PreviousTotalNormalizedTimes[i] = normalizedTime;
            }
        }

        public override string PrimaryClipKey => m_Clips[m_PrimaryClipIndex].Key;
        public override Playable OutputPlayable => m_Mixer;

        protected override void PrepareStateFrame(float deltaTime, float channelTimeScale, XAnimationContext context)
        {
            float parameterValue = 0f;
            string parameterName = m_State.Config.parameterName;
            if (!context.TryGetFloat(parameterName, out parameterValue))
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterName}' does not exist.");
            }

            ResolveWeights(parameterValue);
            for (int i = 0; i < m_Playables.Length; i++)
            {
                m_PreviousTotalNormalizedTimes[i] = m_TotalNormalizedTimes[i];
                m_Playables[i].SetSpeed(Speed * channelTimeScale);
                m_TotalNormalizedTimes[i] += deltaTime * Speed * channelTimeScale / m_ClipLengths[i];

                bool shouldConnect = m_SampleWeights[i] > 0.0001f;
                EnsureSampleConnection(i, shouldConnect);
                if (shouldConnect)
                {
                    m_Mixer.SetInputWeight(i, m_SampleWeights[i]);
                }
            }
        }

        public override void FinalizeFrame(XAnimationCueDispatcher cueDispatcher)
        {
            for (int i = 0; i < m_Playables.Length; i++)
            {
                AnimationClipPlayable playable = m_Playables[i];
                if (!playable.IsValid())
                {
                    continue;
                }

                double playableTime = playable.GetTime();
                if (IsLooping)
                {
                    if (playableTime >= m_ClipLengths[i])
                    {
                        playable.SetTime(playableTime % m_ClipLengths[i]);
                    }
                    else if (playableTime < 0d)
                    {
                        double wrappedTime = playableTime % m_ClipLengths[i];
                        if (wrappedTime < 0d)
                        {
                            wrappedTime += m_ClipLengths[i];
                        }

                        playable.SetTime(wrappedTime);
                    }
                }
                else if (playableTime > m_ClipLengths[i])
                {
                    playable.SetTime(m_ClipLengths[i]);
                }
                else if (playableTime < 0d)
                {
                    playable.SetTime(0d);
                }

                if (!SuppressCues && m_SampleWeights[i] > ActiveCueWeightThreshold)
                {
                    cueDispatcher?.Update(this, m_Clips[i].Key, m_PreviousTotalNormalizedTimes[i], m_TotalNormalizedTimes[i]);
                }
            }
        }

        public override XAnimationChannelState BuildState(float channelWeight, float channelTimeScale)
        {
            return new XAnimationChannelState
            {
                channelName = ChannelName,
                stateKey = StateKey,
                stateType = StateType,
                clipKey = PrimaryClipKey,
                blendClips = BuildBlendClipStates(),
                playbackId = PlaybackId,
                normalizedTime = GetNormalizedTime(m_PrimaryClipIndex),
                totalNormalizedTime = m_TotalNormalizedTimes[m_PrimaryClipIndex],
                weight = CurrentWeight,
                channelWeight = channelWeight,
                speed = Speed * channelTimeScale,
                timeScale = channelTimeScale,
                isLooping = IsLooping,
                isFading = IsFading,
                priority = Priority,
                interruptible = Interruptible,
            };
        }

        public override void Dispose(XAnimationCueDispatcher cueDispatcher)
        {
            cueDispatcher?.RemovePlayback(PlaybackId);
            for (int i = 0; i < m_Playables.Length; i++)
            {
                if (m_Playables[i].IsValid())
                {
                    m_Playables[i].Destroy();
                }
            }

            if (m_Mixer.IsValid())
            {
                m_Mixer.Destroy();
            }
        }

        private void ResolveWeights(float parameterValue)
        {
            Array.Clear(m_SampleWeights, 0, m_SampleWeights.Length);
            IReadOnlyList<XAnimationCompiledBlend1DSample> samples = m_State.Samples;
            if (parameterValue <= samples[0].Threshold)
            {
                m_SampleWeights[0] = 1f;
                m_PrimaryClipIndex = 0;
                return;
            }

            int lastIndex = samples.Count - 1;
            if (parameterValue >= samples[lastIndex].Threshold)
            {
                m_SampleWeights[lastIndex] = 1f;
                m_PrimaryClipIndex = lastIndex;
                return;
            }

            for (int i = 0; i < lastIndex; i++)
            {
                float leftThreshold = samples[i].Threshold;
                float rightThreshold = samples[i + 1].Threshold;
                if (parameterValue < leftThreshold || parameterValue > rightThreshold)
                {
                    continue;
                }

                float range = Mathf.Max(rightThreshold - leftThreshold, 0.0001f);
                float rightWeight = Mathf.Clamp01((parameterValue - leftThreshold) / range);
                float leftWeight = 1f - rightWeight;
                m_SampleWeights[i] = leftWeight;
                m_SampleWeights[i + 1] = rightWeight;
                m_PrimaryClipIndex = rightWeight >= leftWeight ? i + 1 : i;
                return;
            }
        }

        private void EnsureSampleConnection(int inputIndex, bool connected)
        {
            bool isConnected = m_Mixer.GetInput(inputIndex).IsValid();
            if (connected)
            {
                if (!isConnected)
                {
                    m_Graph.Connect(m_Playables[inputIndex], 0, m_Mixer, inputIndex);
                }

                return;
            }

            if (isConnected)
            {
                m_Graph.Disconnect(m_Mixer, inputIndex);
            }

            m_Mixer.SetInputWeight(inputIndex, 0f);
        }

        private XAnimationBlendClipState[] BuildBlendClipStates()
        {
            int activeCount = 0;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                if (m_SampleWeights[i] > ActiveCueWeightThreshold)
                {
                    activeCount++;
                }
            }

            XAnimationBlendClipState[] states = new XAnimationBlendClipState[activeCount];
            int stateIndex = 0;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                if (m_SampleWeights[i] <= ActiveCueWeightThreshold)
                {
                    continue;
                }

                states[stateIndex++] = new XAnimationBlendClipState
                {
                    clipKey = m_Clips[i].Key,
                    weight = m_SampleWeights[i],
                    normalizedTime = GetNormalizedTime(i),
                    totalNormalizedTime = m_TotalNormalizedTimes[i],
                };
            }

            return states;
        }

        private float GetNormalizedTime(int sampleIndex)
        {
            return IsLooping ? Mathf.Repeat(m_TotalNormalizedTimes[sampleIndex], 1f) : Mathf.Clamp01(m_TotalNormalizedTimes[sampleIndex]);
        }
    }

    public sealed class XAnimationChannel
    {
        private readonly PlayableGraph m_Graph;
        private readonly Func<int> m_NextPlaybackIdProvider;

        private XAnimationStatePlaybackInstance m_Current;
        private XAnimationStatePlaybackInstance m_Previous;
        private float m_ChannelWeight;
        private float m_TimeScale = 1f;

        public XAnimationChannel(PlayableGraph graph, XAnimationCompiledChannel channel, Func<int> nextPlaybackIdProvider)
        {
            m_Graph = graph;
            CompiledChannel = channel ?? throw new ArgumentNullException(nameof(channel));
            m_NextPlaybackIdProvider = nextPlaybackIdProvider ?? throw new ArgumentNullException(nameof(nextPlaybackIdProvider));
            Mixer = AnimationMixerPlayable.Create(graph, 2, true);
            m_ChannelWeight = Mathf.Max(0f, channel.Config.defaultWeight);
        }

        public string Name => CompiledChannel.Name;
        public XAnimationChannelLayerType LayerType => CompiledChannel.Config.layerType;
        public bool CanDriveRootMotion => CompiledChannel.Config.canDriveRootMotion;
        public XAnimationCompiledChannel CompiledChannel { get; }
        public AnimationMixerPlayable Mixer { get; }
        public float ChannelWeight => m_ChannelWeight;
        public float TimeScale => m_TimeScale;
        public XAnimationStatePlaybackInstance CurrentPlayback => m_Current;
        public bool HasActivePlayback => m_Current != null || m_Previous != null;

        internal bool TryPlay(Func<int, XAnimationPlaybackRuntimeOptions, XAnimationStatePlaybackInstance> playbackFactory, XAnimationPlaybackRuntimeOptions options, XAnimationCueDispatcher cueDispatcher)
        {
            if (playbackFactory == null)
            {
                throw new ArgumentNullException(nameof(playbackFactory));
            }

            if (!CanInterrupt(options.Priority))
            {
                return false;
            }

            if (m_Previous != null)
            {
                DestroyPlayback(ref m_Previous, cueDispatcher);
            }

            bool hasPlaybackToFadeOut = m_Current != null;
            if (hasPlaybackToFadeOut)
            {
                m_Current.SuppressCues = true;
                cueDispatcher?.RemovePlayback(m_Current.PlaybackId);
                m_Current.BeginFade(0f, Mathf.Max(0f, options.FadeOut));
                SetInputPlayable(1, m_Current.OutputPlayable, m_Current.CurrentWeight);
                m_Previous = m_Current;
                m_Current = null;
            }

            int playbackId = m_NextPlaybackIdProvider();
            if (!hasPlaybackToFadeOut)
            {
                options = new XAnimationPlaybackRuntimeOptions(
                    0f,
                    options.FadeOut,
                    options.Weight,
                    options.NormalizedTime,
                    options.Speed,
                    options.IsLooping,
                    options.Priority,
                    options.Interruptible,
                    options.DrivesRootMotion);
            }

            XAnimationStatePlaybackInstance playback = playbackFactory(playbackId, options);
            cueDispatcher?.ResetForPlayback(playbackId);
            m_Current = playback;
            SetInputPlayable(0, playback.OutputPlayable, playback.CurrentWeight);
            return true;
        }

        public void Stop(float fadeOut, XAnimationCueDispatcher cueDispatcher)
        {
            if (m_Previous != null)
            {
                DestroyPlayback(ref m_Previous, cueDispatcher);
            }

            if (m_Current == null)
            {
                return;
            }

            m_Current.SuppressCues = true;
            cueDispatcher?.RemovePlayback(m_Current.PlaybackId);
            m_Current.BeginFade(0f, Mathf.Max(0f, fadeOut));
            SetInputPlayable(1, m_Current.OutputPlayable, m_Current.CurrentWeight);
            m_Previous = m_Current;
            m_Current = null;
            DisconnectInput(0);
        }

        public void PrepareFrame(float deltaTime, XAnimationContext context)
        {
            if (m_Current != null)
            {
                m_Current.PrepareFrame(deltaTime, m_TimeScale, context);
                Mixer.SetInputWeight(0, m_Current.CurrentWeight);
            }
            else
            {
                Mixer.SetInputWeight(0, 0f);
            }

            if (m_Previous != null)
            {
                m_Previous.PrepareFrame(deltaTime, m_TimeScale, context);
                Mixer.SetInputWeight(1, m_Previous.CurrentWeight);
            }
            else
            {
                Mixer.SetInputWeight(1, 0f);
            }
        }

        public void FinalizeFrame(XAnimationCueDispatcher cueDispatcher)
        {
            if (m_Current != null)
            {
                m_Current.FinalizeFrame(cueDispatcher);
            }

            if (m_Previous != null)
            {
                m_Previous.FinalizeFrame(cueDispatcher);
                if (m_Previous.HasFinishedFadeOut())
                {
                    DestroyPlayback(ref m_Previous, cueDispatcher);
                }
            }
        }

        public void SetChannelWeight(float weight)
        {
            m_ChannelWeight = Mathf.Max(0f, weight);
        }

        public void SetChannelTimeScale(float timeScale)
        {
            m_TimeScale = Mathf.Max(0f, timeScale);
        }

        public XAnimationChannelState GetState()
        {
            return m_Current?.BuildState(m_ChannelWeight, m_TimeScale);
        }

        public bool IsRootMotionSourceCandidate()
        {
            return m_Current != null &&
                   CanDriveRootMotion &&
                   m_Current.DrivesRootMotion &&
                   m_Current.CurrentWeight > 0.0001f;
        }

        public void Dispose(XAnimationCueDispatcher cueDispatcher)
        {
            DestroyPlayback(ref m_Current, cueDispatcher);
            DestroyPlayback(ref m_Previous, cueDispatcher);
            if (Mixer.IsValid())
            {
                Mixer.Destroy();
            }
        }

        private bool CanInterrupt(int requestPriority)
        {
            if (m_Current == null)
            {
                return true;
            }

            if (!CompiledChannel.Config.allowInterrupt || !m_Current.Interruptible)
            {
                return false;
            }

            return requestPriority >= m_Current.Priority;
        }

        private void SetInputPlayable(int inputIndex, Playable playable, float weight)
        {
            if (Mixer.GetInput(inputIndex).IsValid())
            {
                m_Graph.Disconnect(Mixer, inputIndex);
            }

            if (playable.GetOutput(0).IsValid())
            {
                for (int i = 0; i < Mixer.GetInputCount(); i++)
                {
                    if (Mixer.GetInput(i).Equals(playable))
                    {
                        m_Graph.Disconnect(Mixer, i);
                        Mixer.SetInputWeight(i, 0f);
                        break;
                    }
                }
            }

            m_Graph.Connect(playable, 0, Mixer, inputIndex);
            Mixer.SetInputWeight(inputIndex, weight);
        }

        private void DisconnectInput(int inputIndex)
        {
            if (!Mixer.GetInput(inputIndex).IsValid())
            {
                return;
            }

            m_Graph.Disconnect(Mixer, inputIndex);
            Mixer.SetInputWeight(inputIndex, 0f);
        }

        private void DestroyPlayback(ref XAnimationStatePlaybackInstance playback, XAnimationCueDispatcher cueDispatcher)
        {
            if (playback == null)
            {
                return;
            }

            playback.Dispose(cueDispatcher);
            playback = null;
        }
    }
}
