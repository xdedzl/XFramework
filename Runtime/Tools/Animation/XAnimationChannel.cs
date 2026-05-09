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
            bool drivesRootMotion,
            XAnimationTransitionRequestSource requestSource)
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
            RequestSource = requestSource;
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
        public XAnimationTransitionRequestSource RequestSource { get; }
    }

    public abstract class XAnimationStatePlaybackInstance
    {
        private float m_FadeElapsed;
        private bool m_HasExitEventBeenRaised;

        protected XAnimationStatePlaybackInstance(
            int playbackId,
            string channelName,
            string stateKey,
            XAnimationStateType stateType,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
        {
            PlaybackId = playbackId;
            ChannelName = channelName;
            StateKey = stateKey;
            StateType = stateType;
            IsTemporaryState = isTemporaryState;
            TargetWeight = Mathf.Max(0f, options.Weight);
            CurrentWeight = options.FadeIn > 0f ? 0f : TargetWeight;
            Speed = options.Speed;
            IsLooping = options.IsLooping;
            Priority = options.Priority;
            Interruptible = options.Interruptible;
            DrivesRootMotion = options.DrivesRootMotion;
            RequestSource = options.RequestSource;
            FadeFrom = CurrentWeight;
            FadeTo = TargetWeight;
            FadeDuration = Mathf.Max(0f, options.FadeIn);
            m_FadeElapsed = 0f;
        }

        public int PlaybackId { get; }
        public string ChannelName { get; }
        public string StateKey { get; }
        public XAnimationStateType StateType { get; }
        public bool IsTemporaryState { get; }
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
        public XAnimationTransitionRequestSource RequestSource { get; }
        public bool SuppressCues { get; set; }
        public bool HasExitEventBeenRaised => m_HasExitEventBeenRaised;
        public bool HasCompletedExitOrTransition { get; private set; }

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

        public void MarkCompletedExitOrTransition()
        {
            HasCompletedExitOrTransition = true;
        }

        public bool TryMarkExitEventRaised()
        {
            if (m_HasExitEventBeenRaised)
            {
                return false;
            }

            m_HasExitEventBeenRaised = true;
            return true;
        }

        public void PrepareFrame(float deltaTime, float channelTimeScale, XAnimationContext context)
        {
            PrepareStateFrame(deltaTime, channelTimeScale, context);
            UpdateFade(deltaTime);
        }

        public abstract void FinalizeFrame(XAnimationCueDispatcher cueDispatcher, float channelWeight);
        public abstract XAnimationChannelState BuildState(float channelWeight, float channelTimeScale);
        public abstract void Dispose(XAnimationCueDispatcher cueDispatcher);
        public abstract float GetNormalizedTime();
        public abstract float GetTotalNormalizedTime();
        public abstract float GetCueWeight(string clipKey);
        public abstract void SeekNormalizedTime(float normalizedTime);

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
        private readonly Animator m_Animator;
        private readonly XAnimationCompiledClip m_Clip;
        private readonly AnimationClipPlayable m_Playable;
        private readonly float m_ClipLength;

        private float m_TotalNormalizedTime;
        private float m_PreviousTotalNormalizedTime;

        internal XAnimationSingleStatePlaybackInstance(
            int playbackId,
            string channelName,
            string stateKey,
            Animator animator,
            XAnimationCompiledClip clip,
            AnimationClipPlayable playable,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
            : base(playbackId, channelName, stateKey, XAnimationStateType.Single, isTemporaryState, options)
        {
            m_Animator = animator ? animator : throw new ArgumentNullException(nameof(animator));
            m_Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            m_Playable = playable;
            m_ClipLength = Mathf.Max(clip.PlaybackClip.length, 0.0001f);
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

        public override void FinalizeFrame(XAnimationCueDispatcher cueDispatcher, float channelWeight)
        {
            if (!m_Playable.IsValid())
            {
                return;
            }

            double playableTime = m_Playable.GetTime();
            if (!IsLooping && playableTime > m_ClipLength)
            {
                m_Playable.SetTime(m_ClipLength);
            }
            else if (!IsLooping && playableTime < 0d)
            {
                m_Playable.SetTime(0d);
            }

            if (!SuppressCues)
            {
                float effectiveWeight = CurrentWeight * channelWeight;
                cueDispatcher?.Update(this, m_Clip.Key, m_PreviousTotalNormalizedTime, m_TotalNormalizedTime, effectiveWeight);
                XAnimationClipEventInvoker.Dispatch(
                    m_Clip.Clip,
                    this,
                    m_PreviousTotalNormalizedTime,
                    m_TotalNormalizedTime,
                    effectiveWeight,
                    cueEvent => cueDispatcher?.Raise(cueEvent));
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
                isTemporaryState = IsTemporaryState,
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

        public override float GetNormalizedTime()
        {
            return NormalizedTime;
        }

        public override float GetTotalNormalizedTime()
        {
            return m_TotalNormalizedTime;
        }

        public override float GetCueWeight(string clipKey)
        {
            return CurrentWeight;
        }

        public override void SeekNormalizedTime(float normalizedTime)
        {
            m_TotalNormalizedTime = Mathf.Clamp01(normalizedTime);
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            if (m_Playable.IsValid())
            {
                m_Playable.SetTime(GetPlayableTime(m_TotalNormalizedTime));
            }
        }

        private double GetPlayableTime(float totalNormalizedTime)
        {
            if (IsLooping)
            {
                return totalNormalizedTime * m_ClipLength;
            }

            return Mathf.Clamp01(totalNormalizedTime) * m_ClipLength;
        }
    }

    public sealed class XAnimationBlend1DStatePlaybackInstance : XAnimationStatePlaybackInstance
    {
        private const float ActiveCueWeightThreshold = 0.0001f;

        private readonly Animator m_Animator;
        private readonly PlayableGraph m_Graph;
        private readonly XAnimationCompiledBlend1DState m_State;
        private readonly XAnimationCompiledClip[] m_Clips;
        private readonly AnimationMixerPlayable m_Mixer;
        private readonly AnimationClipPlayable[] m_Playables;
        private readonly float[] m_ClipLengths;
        private readonly float[] m_SampleWeights;

        private int m_PrimaryClipIndex;
        private float m_TotalNormalizedTime;
        private float m_PreviousTotalNormalizedTime;

        internal XAnimationBlend1DStatePlaybackInstance(
            PlayableGraph graph,
            int playbackId,
            string channelName,
            Animator animator,
            XAnimationCompiledBlend1DState state,
            XAnimationCompiledClip[] clips,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
            : base(playbackId, channelName, state?.Key, XAnimationStateType.Blend1D, isTemporaryState, options)
        {
            m_Animator = animator ? animator : throw new ArgumentNullException(nameof(animator));
            m_Graph = graph;
            m_State = state ?? throw new ArgumentNullException(nameof(state));
            m_Clips = clips ?? throw new ArgumentNullException(nameof(clips));
            m_Mixer = AnimationMixerPlayable.Create(graph, m_Clips.Length, true);
            m_Playables = new AnimationClipPlayable[m_Clips.Length];
            m_ClipLengths = new float[m_Clips.Length];
            m_SampleWeights = new float[m_Clips.Length];

            float normalizedTime = Mathf.Clamp01(options.NormalizedTime);
            m_TotalNormalizedTime = normalizedTime;
            m_PreviousTotalNormalizedTime = normalizedTime;
            for (int i = 0; i < m_Clips.Length; i++)
            {
                XAnimationCompiledClip clip = m_Clips[i];
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip.PlaybackClip);
                playable.SetApplyFootIK(false);
                float clipLength = Mathf.Max(clip.PlaybackClip.length, 0.0001f);
                playable.SetTime(normalizedTime * clipLength);
                m_Playables[i] = playable;
                m_ClipLengths[i] = clipLength;
            }
        }

        public override string PrimaryClipKey => m_Clips[m_PrimaryClipIndex].Key;
        public override Playable OutputPlayable => m_Mixer;

        protected override void PrepareStateFrame(float deltaTime, float channelTimeScale, XAnimationContext context)
        {
            string parameterName = m_State.Config.parameterName;
            if (!context.TryGetFloat(parameterName, out var parameterValue))
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterName}' does not exist.");
            }

            ResolveWeights(parameterValue);
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            float blendedClipLength = GetBlendedClipLength();
            m_TotalNormalizedTime += deltaTime * Speed * channelTimeScale / blendedClipLength;

            for (int i = 0; i < m_Playables.Length; i++)
            {
                bool shouldConnect = m_SampleWeights[i] > 0.0001f;
                EnsureSampleConnection(i, shouldConnect);
                if (shouldConnect)
                {
                    m_Playables[i].SetSpeed(0d);
                    m_Playables[i].SetTime(GetPlayableTime(i, m_TotalNormalizedTime));
                    m_Mixer.SetInputWeight(i, m_SampleWeights[i]);
                }
            }
        }

        public override void FinalizeFrame(XAnimationCueDispatcher cueDispatcher, float channelWeight)
        {
            for (int i = 0; i < m_Playables.Length; i++)
            {
                AnimationClipPlayable playable = m_Playables[i];
                if (!playable.IsValid())
                {
                    continue;
                }

                if (!SuppressCues && m_SampleWeights[i] > ActiveCueWeightThreshold)
                {
                    float effectiveWeight = CurrentWeight * channelWeight * m_SampleWeights[i];
                    cueDispatcher?.Update(
                        this,
                        m_Clips[i].Key,
                        m_PreviousTotalNormalizedTime,
                        m_TotalNormalizedTime,
                        effectiveWeight);
                    XAnimationClipEventInvoker.Dispatch(
                        m_Clips[i].Clip,
                        this,
                        m_PreviousTotalNormalizedTime,
                        m_TotalNormalizedTime,
                        effectiveWeight,
                        cueEvent => cueDispatcher?.Raise(cueEvent));
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
                normalizedTime = GetNormalizedTime(),
                totalNormalizedTime = m_TotalNormalizedTime,
                weight = CurrentWeight,
                channelWeight = channelWeight,
                speed = Speed * channelTimeScale,
                timeScale = channelTimeScale,
                isLooping = IsLooping,
                isFading = IsFading,
                priority = Priority,
                interruptible = Interruptible,
                isTemporaryState = IsTemporaryState,
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

        public override float GetNormalizedTime()
        {
            return IsLooping ? Mathf.Repeat(m_TotalNormalizedTime, 1f) : Mathf.Clamp01(m_TotalNormalizedTime);
        }

        public override float GetTotalNormalizedTime()
        {
            return m_TotalNormalizedTime;
        }

        public override float GetCueWeight(string clipKey)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                return 0f;
            }

            for (int i = 0; i < m_Clips.Length; i++)
            {
                if (string.Equals(m_Clips[i].Key, clipKey, StringComparison.Ordinal))
                {
                    return CurrentWeight * m_SampleWeights[i];
                }
            }

            return 0f;
        }

        public override void SeekNormalizedTime(float normalizedTime)
        {
            m_TotalNormalizedTime = Mathf.Clamp01(normalizedTime);
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            for (int i = 0; i < m_Playables.Length; i++)
            {
                if (m_Playables[i].IsValid())
                {
                    m_Playables[i].SetTime(GetPlayableTime(i, m_TotalNormalizedTime));
                }
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
                    normalizedTime = GetNormalizedTime(),
                    totalNormalizedTime = m_TotalNormalizedTime,
                };
            }

            return states;
        }

        private float GetBlendedClipLength()
        {
            float blendedClipLength = 0f;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                blendedClipLength += m_ClipLengths[i] * m_SampleWeights[i];
            }

            // Keep a single phase for all active samples while preserving the authored speed at either end.
            return blendedClipLength > 0.0001f ? blendedClipLength : m_ClipLengths[Mathf.Clamp(m_PrimaryClipIndex, 0, m_ClipLengths.Length - 1)];
        }

        private double GetPlayableTime(int sampleIndex, float totalNormalizedTime)
        {
            if (IsLooping)
            {
                return totalNormalizedTime * m_ClipLengths[sampleIndex];
            }

            return Mathf.Clamp01(totalNormalizedTime) * m_ClipLengths[sampleIndex];
        }
    }

    public sealed class XAnimationBlend2DSimpleDirectionalStatePlaybackInstance : XAnimationStatePlaybackInstance
    {
        private const float ActiveCueWeightThreshold = 0.0001f;
        private const float DirectionEpsilon = 0.0001f;
        private const float IdleBlendRadius = 1f;

        private readonly PlayableGraph m_Graph;
        private readonly XAnimationCompiledBlend2DSimpleDirectionalState m_State;
        private readonly XAnimationCompiledClip[] m_Clips;
        private readonly AnimationMixerPlayable m_Mixer;
        private readonly AnimationClipPlayable[] m_Playables;
        private readonly float[] m_ClipLengths;
        private readonly float[] m_SampleWeights;
        private readonly Vector2[] m_SamplePositions;
        private readonly int m_IdleSampleIndex = -1;

        private int m_PrimaryClipIndex;
        private float m_TotalNormalizedTime;
        private float m_PreviousTotalNormalizedTime;
        private float m_BlendParameterX;
        private float m_BlendParameterY;

        internal XAnimationBlend2DSimpleDirectionalStatePlaybackInstance(
            PlayableGraph graph,
            int playbackId,
            string channelName,
            Animator animator,
            XAnimationCompiledBlend2DSimpleDirectionalState state,
            XAnimationCompiledClip[] clips,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
            : base(playbackId, channelName, state?.Key, XAnimationStateType.Blend2DSimpleDirectional, isTemporaryState, options)
        {
            _ = animator ? animator : throw new ArgumentNullException(nameof(animator));
            m_Graph = graph;
            m_State = state ?? throw new ArgumentNullException(nameof(state));
            m_Clips = clips ?? throw new ArgumentNullException(nameof(clips));
            m_Mixer = AnimationMixerPlayable.Create(graph, m_Clips.Length, true);
            m_Playables = new AnimationClipPlayable[m_Clips.Length];
            m_ClipLengths = new float[m_Clips.Length];
            m_SampleWeights = new float[m_Clips.Length];
            m_SamplePositions = new Vector2[m_Clips.Length];

            float normalizedTime = Mathf.Clamp01(options.NormalizedTime);
            m_TotalNormalizedTime = normalizedTime;
            m_PreviousTotalNormalizedTime = normalizedTime;
            for (int i = 0; i < m_Clips.Length; i++)
            {
                XAnimationCompiledClip clip = m_Clips[i];
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip.PlaybackClip);
                playable.SetApplyFootIK(false);
                float clipLength = Mathf.Max(clip.PlaybackClip.length, 0.0001f);
                playable.SetTime(normalizedTime * clipLength);
                m_Playables[i] = playable;
                m_ClipLengths[i] = clipLength;
                m_SamplePositions[i] = m_State.Samples[i].Position;
                if (Mathf.Approximately(m_SamplePositions[i].x, 0f) && Mathf.Approximately(m_SamplePositions[i].y, 0f))
                {
                    m_IdleSampleIndex = i;
                }
            }
        }

        public override string PrimaryClipKey => m_Clips[m_PrimaryClipIndex].Key;
        public override Playable OutputPlayable => m_Mixer;

        protected override void PrepareStateFrame(float deltaTime, float channelTimeScale, XAnimationContext context)
        {
            string parameterXName = m_State.Config.parameterXName;
            string parameterYName = m_State.Config.parameterYName;
            if (!context.TryGetFloat(parameterXName, out float parameterXValue))
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterXName}' does not exist.");
            }

            if (!context.TryGetFloat(parameterYName, out float parameterYValue))
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterYName}' does not exist.");
            }

            m_BlendParameterX = parameterXValue;
            m_BlendParameterY = parameterYValue;
            ResolveWeights(new Vector2(parameterXValue, parameterYValue));
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            float blendedClipLength = GetBlendedClipLength();
            m_TotalNormalizedTime += deltaTime * Speed * channelTimeScale / blendedClipLength;

            for (int i = 0; i < m_Playables.Length; i++)
            {
                bool shouldConnect = m_SampleWeights[i] > ActiveCueWeightThreshold;
                EnsureSampleConnection(i, shouldConnect);
                if (shouldConnect)
                {
                    m_Playables[i].SetSpeed(0d);
                    m_Playables[i].SetTime(GetPlayableTime(i, m_TotalNormalizedTime));
                    m_Mixer.SetInputWeight(i, m_SampleWeights[i]);
                }
            }
        }

        public override void FinalizeFrame(XAnimationCueDispatcher cueDispatcher, float channelWeight)
        {
            for (int i = 0; i < m_Playables.Length; i++)
            {
                AnimationClipPlayable playable = m_Playables[i];
                if (!playable.IsValid())
                {
                    continue;
                }

                if (!SuppressCues && m_SampleWeights[i] > ActiveCueWeightThreshold)
                {
                    float effectiveWeight = CurrentWeight * channelWeight * m_SampleWeights[i];
                    cueDispatcher?.Update(
                        this,
                        m_Clips[i].Key,
                        m_PreviousTotalNormalizedTime,
                        m_TotalNormalizedTime,
                        effectiveWeight);
                    XAnimationClipEventInvoker.Dispatch(
                        m_Clips[i].Clip,
                        this,
                        m_PreviousTotalNormalizedTime,
                        m_TotalNormalizedTime,
                        effectiveWeight,
                        cueEvent => cueDispatcher?.Raise(cueEvent));
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
                normalizedTime = GetNormalizedTime(),
                totalNormalizedTime = m_TotalNormalizedTime,
                weight = CurrentWeight,
                channelWeight = channelWeight,
                speed = Speed * channelTimeScale,
                timeScale = channelTimeScale,
                blendParameterX = m_BlendParameterX,
                blendParameterY = m_BlendParameterY,
                isLooping = IsLooping,
                isFading = IsFading,
                priority = Priority,
                interruptible = Interruptible,
                isTemporaryState = IsTemporaryState,
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

        public override float GetNormalizedTime()
        {
            return IsLooping ? Mathf.Repeat(m_TotalNormalizedTime, 1f) : Mathf.Clamp01(m_TotalNormalizedTime);
        }

        public override float GetTotalNormalizedTime()
        {
            return m_TotalNormalizedTime;
        }

        public override float GetCueWeight(string clipKey)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                return 0f;
            }

            for (int i = 0; i < m_Clips.Length; i++)
            {
                if (string.Equals(m_Clips[i].Key, clipKey, StringComparison.Ordinal))
                {
                    return CurrentWeight * m_SampleWeights[i];
                }
            }

            return 0f;
        }

        public override void SeekNormalizedTime(float normalizedTime)
        {
            m_TotalNormalizedTime = Mathf.Clamp01(normalizedTime);
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            for (int i = 0; i < m_Playables.Length; i++)
            {
                if (m_Playables[i].IsValid())
                {
                    m_Playables[i].SetTime(GetPlayableTime(i, m_TotalNormalizedTime));
                }
            }
        }

        private void ResolveWeights(Vector2 input)
        {
            Array.Clear(m_SampleWeights, 0, m_SampleWeights.Length);

            float magnitude = input.magnitude;
            if (magnitude <= DirectionEpsilon)
            {
                if (m_IdleSampleIndex >= 0)
                {
                    m_SampleWeights[m_IdleSampleIndex] = 1f;
                    m_PrimaryClipIndex = m_IdleSampleIndex;
                    return;
                }

                int closestIndex = FindClosestSampleIndex(input);
                m_SampleWeights[closestIndex] = 1f;
                m_PrimaryClipIndex = closestIndex;
                return;
            }

            Vector2 direction = input / magnitude;
            if (!TryResolveDirectionalSampleWeights(
                    direction,
                    out int firstIndex,
                    out float firstWeight,
                    out int secondIndex,
                    out float secondWeight))
            {
                int closestIndex = FindClosestSampleIndex(input);
                m_SampleWeights[closestIndex] = 1f;
                m_PrimaryClipIndex = closestIndex;
                return;
            }

            float idleWeight = 0f;
            if (m_IdleSampleIndex >= 0)
            {
                idleWeight = Mathf.Clamp01(1f - magnitude / IdleBlendRadius);
            }

            float directionalWeight = 1f - idleWeight;
            m_SampleWeights[firstIndex] = directionalWeight * firstWeight;
            if (secondIndex >= 0 && secondWeight > 0f)
            {
                m_SampleWeights[secondIndex] = directionalWeight * secondWeight;
            }

            if (m_IdleSampleIndex >= 0 && idleWeight > 0f)
            {
                m_SampleWeights[m_IdleSampleIndex] = idleWeight;
            }

            m_PrimaryClipIndex = firstIndex;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                if (m_SampleWeights[i] > m_SampleWeights[m_PrimaryClipIndex])
                {
                    m_PrimaryClipIndex = i;
                }
            }
        }

        private bool TryResolveDirectionalSampleWeights(
            Vector2 direction,
            out int firstIndex,
            out float firstWeight,
            out int secondIndex,
            out float secondWeight)
        {
            firstIndex = -1;
            firstWeight = 0f;
            secondIndex = -1;
            secondWeight = 0f;

            int directionalSampleCount = 0;
            int onlyDirectionalSampleIndex = -1;
            int closestDirectionalSampleIndex = -1;
            float closestDot = float.NegativeInfinity;
            int clockwiseIndex = -1;
            int counterClockwiseIndex = -1;
            float clockwiseDistance = float.PositiveInfinity;
            float counterClockwiseDistance = float.PositiveInfinity;
            float inputAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float zeroSampleSqrMagnitude = DirectionEpsilon * DirectionEpsilon;

            for (int i = 0; i < m_SamplePositions.Length; i++)
            {
                Vector2 samplePosition = m_SamplePositions[i];
                if (samplePosition.sqrMagnitude <= zeroSampleSqrMagnitude)
                {
                    continue;
                }

                directionalSampleCount++;
                onlyDirectionalSampleIndex = i;

                Vector2 sampleDirection = samplePosition.normalized;
                float dot = Vector2.Dot(direction, sampleDirection);
                if (dot > closestDot)
                {
                    closestDot = dot;
                    closestDirectionalSampleIndex = i;
                }

                if (dot >= 1f - DirectionEpsilon)
                {
                    firstIndex = i;
                    firstWeight = 1f;
                    return true;
                }

                float sampleAngle = Mathf.Atan2(sampleDirection.y, sampleDirection.x) * Mathf.Rad2Deg;
                float signedDistance = Mathf.DeltaAngle(inputAngle, sampleAngle);
                if (signedDistance < 0f)
                {
                    float distance = -signedDistance;
                    if (distance < clockwiseDistance)
                    {
                        clockwiseDistance = distance;
                        clockwiseIndex = i;
                    }
                }
                else if (signedDistance > 0f && signedDistance < counterClockwiseDistance)
                {
                    counterClockwiseDistance = signedDistance;
                    counterClockwiseIndex = i;
                }
            }

            if (directionalSampleCount <= 0)
            {
                return false;
            }

            if (directionalSampleCount == 1)
            {
                firstIndex = onlyDirectionalSampleIndex;
                firstWeight = 1f;
                return true;
            }

            if (clockwiseIndex < 0 || counterClockwiseIndex < 0)
            {
                firstIndex = closestDirectionalSampleIndex;
                firstWeight = 1f;
                return firstIndex >= 0;
            }

            float totalDistance = clockwiseDistance + counterClockwiseDistance;
            if (totalDistance <= DirectionEpsilon)
            {
                firstIndex = closestDirectionalSampleIndex;
                firstWeight = 1f;
                return firstIndex >= 0;
            }

            firstIndex = clockwiseIndex;
            firstWeight = counterClockwiseDistance / totalDistance;
            secondIndex = counterClockwiseIndex;
            secondWeight = clockwiseDistance / totalDistance;
            return true;
        }

        private int FindClosestSampleIndex(Vector2 input)
        {
            int closestIndex = 0;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < m_SamplePositions.Length; i++)
            {
                float distance = (m_SamplePositions[i] - input).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
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
                    normalizedTime = GetNormalizedTime(),
                    totalNormalizedTime = m_TotalNormalizedTime,
                    positionX = m_SamplePositions[i].x,
                    positionY = m_SamplePositions[i].y,
                };
            }

            return states;
        }

        private float GetBlendedClipLength()
        {
            float blendedClipLength = 0f;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                blendedClipLength += m_ClipLengths[i] * m_SampleWeights[i];
            }

            return blendedClipLength > 0.0001f ? blendedClipLength : m_ClipLengths[Mathf.Clamp(m_PrimaryClipIndex, 0, m_ClipLengths.Length - 1)];
        }

        private double GetPlayableTime(int sampleIndex, float totalNormalizedTime)
        {
            if (IsLooping)
            {
                return totalNormalizedTime * m_ClipLengths[sampleIndex];
            }

            return Mathf.Clamp01(totalNormalizedTime) * m_ClipLengths[sampleIndex];
        }
    }

    public sealed class XAnimationBlend2DFreeformDirectionalStatePlaybackInstance : XAnimationStatePlaybackInstance
    {
        private const float ActiveCueWeightThreshold = 0.0001f;
        private const float DirectionEpsilon = 0.0001f;

        private readonly PlayableGraph m_Graph;
        private readonly XAnimationCompiledBlend2DFreeformDirectionalState m_State;
        private readonly XAnimationCompiledClip[] m_Clips;
        private readonly AnimationMixerPlayable m_Mixer;
        private readonly AnimationClipPlayable[] m_Playables;
        private readonly float[] m_ClipLengths;
        private readonly float[] m_SampleWeights;
        private readonly Vector2[] m_SamplePositions;
        private readonly FreeformDirectionalGroup[] m_DirectionalGroups;
        private readonly int m_IdleSampleIndex = -1;

        private int m_PrimaryClipIndex;
        private float m_TotalNormalizedTime;
        private float m_PreviousTotalNormalizedTime;
        private float m_BlendParameterX;
        private float m_BlendParameterY;

        internal XAnimationBlend2DFreeformDirectionalStatePlaybackInstance(
            PlayableGraph graph,
            int playbackId,
            string channelName,
            Animator animator,
            XAnimationCompiledBlend2DFreeformDirectionalState state,
            XAnimationCompiledClip[] clips,
            bool isTemporaryState,
            XAnimationPlaybackRuntimeOptions options)
            : base(playbackId, channelName, state?.Key, XAnimationStateType.Blend2DFreeformDirectional, isTemporaryState, options)
        {
            _ = animator ? animator : throw new ArgumentNullException(nameof(animator));
            m_Graph = graph;
            m_State = state ?? throw new ArgumentNullException(nameof(state));
            m_Clips = clips ?? throw new ArgumentNullException(nameof(clips));
            m_Mixer = AnimationMixerPlayable.Create(graph, m_Clips.Length, true);
            m_Playables = new AnimationClipPlayable[m_Clips.Length];
            m_ClipLengths = new float[m_Clips.Length];
            m_SampleWeights = new float[m_Clips.Length];
            m_SamplePositions = new Vector2[m_Clips.Length];

            float normalizedTime = Mathf.Clamp01(options.NormalizedTime);
            m_TotalNormalizedTime = normalizedTime;
            m_PreviousTotalNormalizedTime = normalizedTime;
            for (int i = 0; i < m_Clips.Length; i++)
            {
                XAnimationCompiledClip clip = m_Clips[i];
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip.PlaybackClip);
                playable.SetApplyFootIK(false);
                float clipLength = Mathf.Max(clip.PlaybackClip.length, 0.0001f);
                playable.SetTime(normalizedTime * clipLength);
                m_Playables[i] = playable;
                m_ClipLengths[i] = clipLength;
                m_SamplePositions[i] = m_State.Samples[i].Position;
                if (Mathf.Approximately(m_SamplePositions[i].x, 0f) && Mathf.Approximately(m_SamplePositions[i].y, 0f))
                {
                    m_IdleSampleIndex = i;
                }
            }

            m_PrimaryClipIndex = m_IdleSampleIndex >= 0 ? m_IdleSampleIndex : 0;
            m_DirectionalGroups = BuildDirectionalGroups();
        }

        public override string PrimaryClipKey => m_Clips[m_PrimaryClipIndex].Key;
        public override Playable OutputPlayable => m_Mixer;

        protected override void PrepareStateFrame(float deltaTime, float channelTimeScale, XAnimationContext context)
        {
            string parameterXName = m_State.Config.parameterXName;
            string parameterYName = m_State.Config.parameterYName;
            if (!context.TryGetFloat(parameterXName, out float parameterXValue))
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterXName}' does not exist.");
            }

            if (!context.TryGetFloat(parameterYName, out float parameterYValue))
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterYName}' does not exist.");
            }

            m_BlendParameterX = parameterXValue;
            m_BlendParameterY = parameterYValue;
            ResolveWeights(new Vector2(parameterXValue, parameterYValue));
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            float blendedClipLength = GetBlendedClipLength();
            m_TotalNormalizedTime += deltaTime * Speed * channelTimeScale / blendedClipLength;

            for (int i = 0; i < m_Playables.Length; i++)
            {
                bool shouldConnect = m_SampleWeights[i] > ActiveCueWeightThreshold;
                EnsureSampleConnection(i, shouldConnect);
                if (shouldConnect)
                {
                    m_Playables[i].SetSpeed(0d);
                    m_Playables[i].SetTime(GetPlayableTime(i, m_TotalNormalizedTime));
                    m_Mixer.SetInputWeight(i, m_SampleWeights[i]);
                }
            }
        }

        public override void FinalizeFrame(XAnimationCueDispatcher cueDispatcher, float channelWeight)
        {
            for (int i = 0; i < m_Playables.Length; i++)
            {
                AnimationClipPlayable playable = m_Playables[i];
                if (!playable.IsValid())
                {
                    continue;
                }

                if (!SuppressCues && m_SampleWeights[i] > ActiveCueWeightThreshold)
                {
                    float effectiveWeight = CurrentWeight * channelWeight * m_SampleWeights[i];
                    cueDispatcher?.Update(
                        this,
                        m_Clips[i].Key,
                        m_PreviousTotalNormalizedTime,
                        m_TotalNormalizedTime,
                        effectiveWeight);
                    XAnimationClipEventInvoker.Dispatch(
                        m_Clips[i].Clip,
                        this,
                        m_PreviousTotalNormalizedTime,
                        m_TotalNormalizedTime,
                        effectiveWeight,
                        cueEvent => cueDispatcher?.Raise(cueEvent));
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
                normalizedTime = GetNormalizedTime(),
                totalNormalizedTime = m_TotalNormalizedTime,
                weight = CurrentWeight,
                channelWeight = channelWeight,
                speed = Speed * channelTimeScale,
                timeScale = channelTimeScale,
                blendParameterX = m_BlendParameterX,
                blendParameterY = m_BlendParameterY,
                isLooping = IsLooping,
                isFading = IsFading,
                priority = Priority,
                interruptible = Interruptible,
                isTemporaryState = IsTemporaryState,
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

        public override float GetNormalizedTime()
        {
            return IsLooping ? Mathf.Repeat(m_TotalNormalizedTime, 1f) : Mathf.Clamp01(m_TotalNormalizedTime);
        }

        public override float GetTotalNormalizedTime()
        {
            return m_TotalNormalizedTime;
        }

        public override float GetCueWeight(string clipKey)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                return 0f;
            }

            for (int i = 0; i < m_Clips.Length; i++)
            {
                if (string.Equals(m_Clips[i].Key, clipKey, StringComparison.Ordinal))
                {
                    return CurrentWeight * m_SampleWeights[i];
                }
            }

            return 0f;
        }

        public override void SeekNormalizedTime(float normalizedTime)
        {
            m_TotalNormalizedTime = Mathf.Clamp01(normalizedTime);
            m_PreviousTotalNormalizedTime = m_TotalNormalizedTime;
            for (int i = 0; i < m_Playables.Length; i++)
            {
                if (m_Playables[i].IsValid())
                {
                    m_Playables[i].SetTime(GetPlayableTime(i, m_TotalNormalizedTime));
                }
            }
        }

        private void ResolveWeights(Vector2 input)
        {
            Array.Clear(m_SampleWeights, 0, m_SampleWeights.Length);

            float magnitude = input.magnitude;
            if (magnitude <= DirectionEpsilon)
            {
                int idleIndex = m_IdleSampleIndex >= 0 ? m_IdleSampleIndex : FindClosestSampleIndex(input);
                m_SampleWeights[idleIndex] = 1f;
                m_PrimaryClipIndex = idleIndex;
                return;
            }

            if (m_DirectionalGroups.Length == 0)
            {
                int closestIndex = FindClosestSampleIndex(input);
                m_SampleWeights[closestIndex] = 1f;
                m_PrimaryClipIndex = closestIndex;
                return;
            }

            Vector2 direction = input / magnitude;
            if (!TryResolveDirectionalGroupWeights(
                    direction,
                    out int firstGroupIndex,
                    out float firstGroupWeight,
                    out int secondGroupIndex,
                    out float secondGroupWeight))
            {
                int closestGroupIndex = FindClosestDirectionalGroupIndex(direction);
                ApplyGroupRadialWeights(m_DirectionalGroups[closestGroupIndex], magnitude, 1f);
                NormalizeWeights(input);
                return;
            }

            ApplyGroupRadialWeights(m_DirectionalGroups[firstGroupIndex], magnitude, firstGroupWeight);
            if (secondGroupIndex >= 0 && secondGroupWeight > 0f)
            {
                ApplyGroupRadialWeights(m_DirectionalGroups[secondGroupIndex], magnitude, secondGroupWeight);
            }

            NormalizeWeights(input);
        }

        private bool TryResolveDirectionalGroupWeights(
            Vector2 direction,
            out int firstGroupIndex,
            out float firstGroupWeight,
            out int secondGroupIndex,
            out float secondGroupWeight)
        {
            firstGroupIndex = -1;
            firstGroupWeight = 0f;
            secondGroupIndex = -1;
            secondGroupWeight = 0f;

            if (m_DirectionalGroups.Length <= 0)
            {
                return false;
            }

            if (m_DirectionalGroups.Length == 1)
            {
                firstGroupIndex = 0;
                firstGroupWeight = 1f;
                return true;
            }

            int closestGroupIndex = -1;
            float closestDot = float.NegativeInfinity;
            int clockwiseIndex = -1;
            int counterClockwiseIndex = -1;
            float clockwiseDistance = float.PositiveInfinity;
            float counterClockwiseDistance = float.PositiveInfinity;
            float inputAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            for (int i = 0; i < m_DirectionalGroups.Length; i++)
            {
                FreeformDirectionalGroup group = m_DirectionalGroups[i];
                float dot = Vector2.Dot(direction, group.Direction);
                if (dot > closestDot)
                {
                    closestDot = dot;
                    closestGroupIndex = i;
                }

                if (dot >= 1f - DirectionEpsilon)
                {
                    firstGroupIndex = i;
                    firstGroupWeight = 1f;
                    return true;
                }

                float signedDistance = Mathf.DeltaAngle(inputAngle, group.Angle);
                if (signedDistance < 0f)
                {
                    float distance = -signedDistance;
                    if (distance < clockwiseDistance)
                    {
                        clockwiseDistance = distance;
                        clockwiseIndex = i;
                    }
                }
                else if (signedDistance > 0f && signedDistance < counterClockwiseDistance)
                {
                    counterClockwiseDistance = signedDistance;
                    counterClockwiseIndex = i;
                }
            }

            if (clockwiseIndex < 0 || counterClockwiseIndex < 0)
            {
                firstGroupIndex = closestGroupIndex;
                firstGroupWeight = 1f;
                return firstGroupIndex >= 0;
            }

            float totalDistance = clockwiseDistance + counterClockwiseDistance;
            if (totalDistance <= DirectionEpsilon)
            {
                firstGroupIndex = closestGroupIndex;
                firstGroupWeight = 1f;
                return firstGroupIndex >= 0;
            }

            firstGroupIndex = clockwiseIndex;
            firstGroupWeight = counterClockwiseDistance / totalDistance;
            secondGroupIndex = counterClockwiseIndex;
            secondGroupWeight = clockwiseDistance / totalDistance;
            return true;
        }

        private void ApplyGroupRadialWeights(FreeformDirectionalGroup group, float magnitude, float groupWeight)
        {
            if (group == null || groupWeight <= 0f || group.SampleIndices.Length == 0)
            {
                return;
            }

            if (magnitude <= DirectionEpsilon)
            {
                AddIdleWeight(groupWeight);
                return;
            }

            int firstIndex = group.SampleIndices[0];
            float firstRadius = group.Radii[0];
            if (firstRadius <= DirectionEpsilon)
            {
                m_SampleWeights[firstIndex] += groupWeight;
                return;
            }

            if (magnitude <= firstRadius)
            {
                float sampleWeight = Mathf.Clamp01(magnitude / firstRadius);
                AddIdleWeight(groupWeight * (1f - sampleWeight));
                m_SampleWeights[firstIndex] += groupWeight * sampleWeight;
                return;
            }

            for (int i = 1; i < group.SampleIndices.Length; i++)
            {
                float previousRadius = group.Radii[i - 1];
                float nextRadius = group.Radii[i];
                if (magnitude > nextRadius)
                {
                    continue;
                }

                float span = nextRadius - previousRadius;
                if (span <= DirectionEpsilon)
                {
                    m_SampleWeights[group.SampleIndices[i]] += groupWeight;
                    return;
                }

                float nextWeight = Mathf.Clamp01((magnitude - previousRadius) / span);
                m_SampleWeights[group.SampleIndices[i - 1]] += groupWeight * (1f - nextWeight);
                m_SampleWeights[group.SampleIndices[i]] += groupWeight * nextWeight;
                return;
            }

            m_SampleWeights[group.SampleIndices[^1]] += groupWeight;
        }

        private void AddIdleWeight(float weight)
        {
            if (m_IdleSampleIndex >= 0 && weight > 0f)
            {
                m_SampleWeights[m_IdleSampleIndex] += weight;
            }
        }

        private void NormalizeWeights(Vector2 input)
        {
            float totalWeight = 0f;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                totalWeight += m_SampleWeights[i];
            }

            if (totalWeight <= DirectionEpsilon)
            {
                int closestIndex = FindClosestSampleIndex(input);
                m_SampleWeights[closestIndex] = 1f;
                m_PrimaryClipIndex = closestIndex;
                return;
            }

            m_PrimaryClipIndex = 0;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                m_SampleWeights[i] /= totalWeight;
                if (m_SampleWeights[i] > m_SampleWeights[m_PrimaryClipIndex])
                {
                    m_PrimaryClipIndex = i;
                }
            }
        }

        private int FindClosestDirectionalGroupIndex(Vector2 direction)
        {
            int closestIndex = 0;
            float closestDot = float.NegativeInfinity;
            for (int i = 0; i < m_DirectionalGroups.Length; i++)
            {
                float dot = Vector2.Dot(direction, m_DirectionalGroups[i].Direction);
                if (dot > closestDot)
                {
                    closestDot = dot;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private int FindClosestSampleIndex(Vector2 input)
        {
            int closestIndex = 0;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < m_SamplePositions.Length; i++)
            {
                float distance = (m_SamplePositions[i] - input).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private FreeformDirectionalGroup[] BuildDirectionalGroups()
        {
            float zeroSampleSqrMagnitude = DirectionEpsilon * DirectionEpsilon;
            List<Vector2> directions = new();
            List<List<int>> groupSampleIndices = new();
            for (int i = 0; i < m_SamplePositions.Length; i++)
            {
                Vector2 samplePosition = m_SamplePositions[i];
                if (samplePosition.sqrMagnitude <= zeroSampleSqrMagnitude)
                {
                    continue;
                }

                Vector2 sampleDirection = samplePosition.normalized;
                int groupIndex = -1;
                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    if (Vector2.Dot(sampleDirection, directions[directionIndex]) >= 1f - DirectionEpsilon)
                    {
                        groupIndex = directionIndex;
                        break;
                    }
                }

                if (groupIndex < 0)
                {
                    groupIndex = directions.Count;
                    directions.Add(sampleDirection);
                    groupSampleIndices.Add(new List<int>());
                }

                groupSampleIndices[groupIndex].Add(i);
            }

            FreeformDirectionalGroup[] groups = new FreeformDirectionalGroup[groupSampleIndices.Count];
            for (int i = 0; i < groupSampleIndices.Count; i++)
            {
                List<int> indices = groupSampleIndices[i];
                indices.Sort((left, right) => m_SamplePositions[left].magnitude.CompareTo(m_SamplePositions[right].magnitude));
                int[] sampleIndices = indices.ToArray();
                float[] radii = new float[sampleIndices.Length];
                for (int sampleIndex = 0; sampleIndex < sampleIndices.Length; sampleIndex++)
                {
                    radii[sampleIndex] = m_SamplePositions[sampleIndices[sampleIndex]].magnitude;
                }

                Vector2 direction = directions[i];
                groups[i] = new FreeformDirectionalGroup(
                    direction,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg,
                    sampleIndices,
                    radii);
            }

            return groups;
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
                    normalizedTime = GetNormalizedTime(),
                    totalNormalizedTime = m_TotalNormalizedTime,
                    positionX = m_SamplePositions[i].x,
                    positionY = m_SamplePositions[i].y,
                };
            }

            return states;
        }

        private float GetBlendedClipLength()
        {
            float blendedClipLength = 0f;
            for (int i = 0; i < m_SampleWeights.Length; i++)
            {
                blendedClipLength += m_ClipLengths[i] * m_SampleWeights[i];
            }

            return blendedClipLength > 0.0001f ? blendedClipLength : m_ClipLengths[Mathf.Clamp(m_PrimaryClipIndex, 0, m_ClipLengths.Length - 1)];
        }

        private double GetPlayableTime(int sampleIndex, float totalNormalizedTime)
        {
            if (IsLooping)
            {
                return totalNormalizedTime * m_ClipLengths[sampleIndex];
            }

            return Mathf.Clamp01(totalNormalizedTime) * m_ClipLengths[sampleIndex];
        }

        private sealed class FreeformDirectionalGroup
        {
            public FreeformDirectionalGroup(Vector2 direction, float angle, int[] sampleIndices, float[] radii)
            {
                Direction = direction;
                Angle = angle;
                SampleIndices = sampleIndices ?? Array.Empty<int>();
                Radii = radii ?? Array.Empty<float>();
            }

            public Vector2 Direction { get; }
            public float Angle { get; }
            public int[] SampleIndices { get; }
            public float[] Radii { get; }
        }
    }

    public sealed class XAnimationChannel
    {
        private readonly PlayableGraph m_Graph;
        private readonly Func<int> m_NextPlaybackIdProvider;
        private readonly Action<XAnimationStatePlaybackInstance> m_OnStateEnter;
        private readonly Action<XAnimationStatePlaybackInstance, XAnimationStateExitReason> m_OnStateExit;

        private XAnimationStatePlaybackInstance m_Current;
        private XAnimationStatePlaybackInstance m_Previous;
        private float m_ChannelWeight;
        private float m_TimeScale = 1f;
        private XAnimationTransitionRejectReason m_LastRejectReason;
        private string m_LastRejectedStateKey = string.Empty;
        private string m_LastRejectedClipKey = string.Empty;
        private int m_LastRejectedPriority;
        private XAnimationTransitionRequestSource m_LastRejectedSource;

        public XAnimationChannel(
            PlayableGraph graph,
            XAnimationCompiledChannel channel,
            Func<int> nextPlaybackIdProvider,
            Action<XAnimationStatePlaybackInstance> onStateEnter,
            Action<XAnimationStatePlaybackInstance, XAnimationStateExitReason> onStateExit)
        {
            m_Graph = graph;
            CompiledChannel = channel ?? throw new ArgumentNullException(nameof(channel));
            m_NextPlaybackIdProvider = nextPlaybackIdProvider ?? throw new ArgumentNullException(nameof(nextPlaybackIdProvider));
            m_OnStateEnter = onStateEnter;
            m_OnStateExit = onStateExit;
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

        public bool TryGetCurrentPlayback(out XAnimationStatePlaybackInstance playback)
        {
            playback = m_Current;
            return playback != null;
        }

        internal bool TryPlay(
            Func<int, XAnimationPlaybackRuntimeOptions, XAnimationStatePlaybackInstance> playbackFactory,
            XAnimationTransitionRequest request,
            XAnimationCueDispatcher cueDispatcher,
            out XAnimationStatePlaybackInstance playback,
            out XAnimationTransitionRejectReason rejectReason)
        {
            playback = null;
            rejectReason = XAnimationTransitionRejectReason.None;
            if (playbackFactory == null)
            {
                throw new ArgumentNullException(nameof(playbackFactory));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            XAnimationTransitionRejectReason interruptRejectReason = CanInterrupt(request.Priority);
            if (interruptRejectReason != XAnimationTransitionRejectReason.None)
            {
                rejectReason = interruptRejectReason;
                RecordRejectedRequest(request, rejectReason);
                return false;
            }

            ClearRejectedRequest();

            if (m_Previous != null)
            {
                DestroyPlayback(ref m_Previous, cueDispatcher);
            }

            bool hasPlaybackToFadeOut = m_Current != null;
            if (hasPlaybackToFadeOut)
            {
                if (!m_Current.HasExitEventBeenRaised)
                {
                    NotifyStateExit(m_Current, XAnimationStateExitReason.Interrupted);
                }

                m_Current.BeginFade(0f, request.FadeOut);
                SetInputPlayable(1, m_Current.OutputPlayable, m_Current.CurrentWeight);
                m_Previous = m_Current;
                m_Current = null;
            }

            int playbackId = m_NextPlaybackIdProvider();
            XAnimationPlaybackRuntimeOptions options = request.CreateRuntimeOptions(skipFadeIn: !hasPlaybackToFadeOut);

            playback = playbackFactory(playbackId, options);
            cueDispatcher?.ResetForPlayback(playbackId);
            m_Current = playback;
            SetInputPlayable(0, playback.OutputPlayable, playback.CurrentWeight);
            NotifyStateEnter(playback);
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
            ClearRejectedRequest();
            cueDispatcher?.RemovePlayback(m_Current.PlaybackId);
            if (!m_Current.HasExitEventBeenRaised)
            {
                NotifyStateExit(m_Current, XAnimationStateExitReason.Stopped);
            }

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
                m_Current.FinalizeFrame(cueDispatcher, m_ChannelWeight);
            }

            if (m_Previous != null)
            {
                m_Previous.FinalizeFrame(cueDispatcher, m_ChannelWeight);
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

        public bool SeekCurrent(float normalizedTime)
        {
            if (m_Current == null)
            {
                return false;
            }

            m_Current.SeekNormalizedTime(normalizedTime);
            Mixer.SetInputWeight(0, m_Current.CurrentWeight);
            return true;
        }

        public XAnimationChannelState GetState()
        {
            XAnimationChannelState state = m_Current?.BuildState(m_ChannelWeight, m_TimeScale);
            if (state != null && !string.IsNullOrWhiteSpace(state.stateKey))
            {
                state.nextStateKey = string.Empty;
                state.isTransitioning = m_Previous != null;
                state.previousStateKey = m_Previous?.StateKey ?? string.Empty;
                state.previousPlaybackId = m_Previous?.PlaybackId ?? 0;
                state.transitionSource = m_Current.RequestSource;
                state.transitionTargetStateKey = m_Previous != null ? state.stateKey : string.Empty;
                state.lastRejectReason = m_LastRejectReason;
                state.lastRejectedStateKey = m_LastRejectedStateKey;
                state.lastRejectedClipKey = m_LastRejectedClipKey;
                state.lastRejectedPriority = m_LastRejectedPriority;
                state.lastRejectedSource = m_LastRejectedSource;
            }

            return state;
        }

        public bool TryMarkCompletedExit(out XAnimationStatePlaybackInstance playback)
        {
            playback = m_Current;
            if (playback == null || !playback.TryMarkExitEventRaised())
            {
                return false;
            }

            m_OnStateExit?.Invoke(playback, XAnimationStateExitReason.Completed);
            return true;
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
            if (m_Current != null && !m_Current.HasExitEventBeenRaised)
            {
                NotifyStateExit(m_Current, XAnimationStateExitReason.Disposed);
            }

            DestroyPlayback(ref m_Current, cueDispatcher);
            DestroyPlayback(ref m_Previous, cueDispatcher);
            if (Mixer.IsValid())
            {
                Mixer.Destroy();
            }
        }

        private XAnimationTransitionRejectReason CanInterrupt(int requestPriority)
        {
            if (m_Current == null)
            {
                return XAnimationTransitionRejectReason.None;
            }

            if (!CompiledChannel.Config.allowInterrupt)
            {
                return XAnimationTransitionRejectReason.ChannelDisallowInterrupt;
            }

            if (!m_Current.Interruptible)
            {
                return XAnimationTransitionRejectReason.CurrentUninterruptible;
            }

            return requestPriority >= m_Current.Priority
                ? XAnimationTransitionRejectReason.None
                : XAnimationTransitionRejectReason.LowerPriority;
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

        private void NotifyStateEnter(XAnimationStatePlaybackInstance playback)
        {
            m_OnStateEnter?.Invoke(playback);
        }

        private void NotifyStateExit(XAnimationStatePlaybackInstance playback, XAnimationStateExitReason reason)
        {
            playback?.MarkCompletedExitOrTransition();
            m_OnStateExit?.Invoke(playback, reason);
        }

        private void RecordRejectedRequest(XAnimationTransitionRequest request, XAnimationTransitionRejectReason rejectReason)
        {
            m_LastRejectReason = rejectReason;
            m_LastRejectedStateKey = request.TargetStateKey ?? string.Empty;
            m_LastRejectedClipKey = request.TargetClipKey ?? string.Empty;
            m_LastRejectedPriority = request.Priority;
            m_LastRejectedSource = request.Source;
        }

        private void ClearRejectedRequest()
        {
            m_LastRejectReason = XAnimationTransitionRejectReason.None;
            m_LastRejectedStateKey = string.Empty;
            m_LastRejectedClipKey = string.Empty;
            m_LastRejectedPriority = 0;
            m_LastRejectedSource = XAnimationTransitionRequestSource.ExplicitPlay;
        }
    }
}
