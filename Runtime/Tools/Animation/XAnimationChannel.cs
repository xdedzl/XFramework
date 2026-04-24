using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace XFramework.Animation
{
    internal readonly struct XAnimationPlaybackOptions
    {
        public XAnimationPlaybackOptions(
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

    public sealed class XAnimationPlaybackInstance
    {
        private float m_FadeElapsed;
        private readonly float m_ClipLength;

        internal XAnimationPlaybackInstance(
            int playbackId,
            string channelName,
            XAnimationCompiledClip clip,
            AnimationClipPlayable playable,
            XAnimationPlaybackOptions options)
        {
            PlaybackId = playbackId;
            ChannelName = channelName;
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            Playable = playable;
            m_ClipLength = Mathf.Max(clip.Clip.length, 0.0001f);
            TargetWeight = Mathf.Max(0f, options.Weight);
            CurrentWeight = options.FadeIn > 0f ? 0f : TargetWeight;
            Speed = options.Speed;
            IsLooping = options.IsLooping;
            Priority = options.Priority;
            Interruptible = options.Interruptible;
            DrivesRootMotion = options.DrivesRootMotion;
            TotalNormalizedTime = Mathf.Clamp01(options.NormalizedTime);
            PreviousTotalNormalizedTime = TotalNormalizedTime;
            FadeFrom = CurrentWeight;
            FadeTo = TargetWeight;
            FadeDuration = Mathf.Max(0f, options.FadeIn);
            m_FadeElapsed = 0f;
        }

        internal XAnimationCompiledClip Clip { get; }
        internal AnimationClipPlayable Playable { get; }

        public int PlaybackId { get; }
        public string ClipKey => Clip.Key;
        public string ChannelName { get; }
        public float TotalNormalizedTime { get; private set; }
        public float PreviousTotalNormalizedTime { get; private set; }
        public float NormalizedTime => IsLooping ? Mathf.Repeat(TotalNormalizedTime, 1f) : Mathf.Clamp01(TotalNormalizedTime);
        public float CurrentWeight { get; private set; }
        public float TargetWeight { get; private set; }
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

        public void PrepareFrame(float deltaTime, float channelTimeScale)
        {
            PreviousTotalNormalizedTime = TotalNormalizedTime;
            Playable.SetSpeed(Speed * channelTimeScale);
            TotalNormalizedTime += deltaTime * Speed * channelTimeScale / m_ClipLength;

            if (FadeDuration <= 0f)
            {
                CurrentWeight = FadeTo;
                return;
            }

            m_FadeElapsed = Mathf.Min(m_FadeElapsed + deltaTime, FadeDuration);
            float t = FadeDuration <= Mathf.Epsilon ? 1f : m_FadeElapsed / FadeDuration;
            CurrentWeight = Mathf.Lerp(FadeFrom, FadeTo, t);
        }

        public void FinalizeFrame()
        {
            if (!Playable.IsValid())
            {
                return;
            }

            double playableTime = Playable.GetTime();
            if (IsLooping)
            {
                if (playableTime >= m_ClipLength)
                {
                    Playable.SetTime(playableTime % m_ClipLength);
                }
                else if (playableTime < 0d)
                {
                    double wrappedTime = playableTime % m_ClipLength;
                    if (wrappedTime < 0d)
                    {
                        wrappedTime += m_ClipLength;
                    }
                    Playable.SetTime(wrappedTime);
                }

                return;
            }

            if (playableTime > m_ClipLength)
            {
                Playable.SetTime(m_ClipLength);
            }
            else if (playableTime < 0d)
            {
                Playable.SetTime(0d);
            }
        }

        public bool HasFinishedFadeOut()
        {
            return CurrentWeight <= 0.0001f && (!IsFading || FadeTo <= 0f);
        }
    }

    public sealed class XAnimationChannel
    {
        private readonly PlayableGraph m_Graph;
        private readonly Func<int> m_NextPlaybackIdProvider;

        private XAnimationPlaybackInstance m_Current;
        private XAnimationPlaybackInstance m_Previous;
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
        public XAnimationPlaybackInstance CurrentPlayback => m_Current;

        internal bool TryPlay(XAnimationCompiledClip clip, XAnimationPlaybackOptions options, XAnimationCueDispatcher cueDispatcher)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (!CanInterrupt(options.Priority))
            {
                return false;
            }

            if (m_Previous != null)
            {
                DestroyPlayback(ref m_Previous, cueDispatcher);
            }

            if (m_Current != null)
            {
                m_Current.SuppressCues = true;
                cueDispatcher?.RemovePlayback(m_Current.PlaybackId);
                m_Current.BeginFade(0f, Mathf.Max(0f, options.FadeOut));
                SetInputPlayable(1, m_Current.Playable, m_Current.CurrentWeight);
                m_Previous = m_Current;
                m_Current = null;
            }

            int playbackId = m_NextPlaybackIdProvider();
            AnimationClipPlayable playable = AnimationClipPlayable.Create(m_Graph, clip.Clip);
            playable.SetApplyFootIK(false);
            playable.SetTime(Mathf.Clamp01(options.NormalizedTime) * Mathf.Max(clip.Clip.length, 0.0001f));
            XAnimationPlaybackInstance playback = new(playbackId, Name, clip, playable, options);
            cueDispatcher?.ResetForPlayback(playbackId);
            m_Current = playback;
            SetInputPlayable(0, playable, playback.CurrentWeight);
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
            SetInputPlayable(1, m_Current.Playable, m_Current.CurrentWeight);
            m_Previous = m_Current;
            m_Current = null;
            DisconnectInput(0);
        }

        public void PrepareFrame(float deltaTime)
        {
            if (m_Current != null)
            {
                m_Current.PrepareFrame(deltaTime, m_TimeScale);
                Mixer.SetInputWeight(0, m_Current.CurrentWeight);
            }
            else
            {
                Mixer.SetInputWeight(0, 0f);
            }

            if (m_Previous != null)
            {
                m_Previous.PrepareFrame(deltaTime, m_TimeScale);
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
                m_Current.FinalizeFrame();
                cueDispatcher?.Update(m_Current, m_Current.PreviousTotalNormalizedTime, m_Current.TotalNormalizedTime);
            }

            if (m_Previous != null)
            {
                m_Previous.FinalizeFrame();
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
            if (m_Current == null)
            {
                return null;
            }

            return new XAnimationChannelState
            {
                channelName = Name,
                clipKey = m_Current.ClipKey,
                playbackId = m_Current.PlaybackId,
                normalizedTime = m_Current.NormalizedTime,
                totalNormalizedTime = m_Current.TotalNormalizedTime,
                weight = m_Current.CurrentWeight,
                speed = m_Current.Speed * m_TimeScale,
                isLooping = m_Current.IsLooping,
                isFading = m_Current.IsFading,
                priority = m_Current.Priority,
                interruptible = m_Current.Interruptible,
            };
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

        private void SetInputPlayable(int inputIndex, AnimationClipPlayable playable, float weight)
        {
            if (Mixer.GetInput(inputIndex).IsValid())
            {
                m_Graph.Disconnect(Mixer, inputIndex);
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

        private void DestroyPlayback(ref XAnimationPlaybackInstance playback, XAnimationCueDispatcher cueDispatcher)
        {
            if (playback == null)
            {
                return;
            }

            cueDispatcher?.RemovePlayback(playback.PlaybackId);
            if (playback.Playable.IsValid())
            {
                playback.Playable.Destroy();
            }

            playback = null;
        }
    }
}
