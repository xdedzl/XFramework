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

        private PlayableGraph m_Graph;
        private AnimationLayerMixerPlayable m_LayerMixer;
        private AnimationPlayableOutput m_Output;
        private int m_NextPlaybackId = 1;

        public XAnimationPlayer(XAnimationCompiledAsset compiledAsset, Animator animator)
        {
            CompiledAsset = compiledAsset ?? throw new ArgumentNullException(nameof(compiledAsset));
            Animator = animator ? animator : throw new ArgumentNullException(nameof(animator));
            BuildGraph();
        }

        public event Action<XAnimationCueEvent> CueTriggered
        {
            add => m_CueDispatcher.CueTriggered += value;
            remove => m_CueDispatcher.CueTriggered -= value;
        }

        public XAnimationCompiledAsset CompiledAsset { get; }
        public Animator Animator { get; }
        public bool IsDisposed { get; private set; }

        public void Play(XAnimationPlayRequest request)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(request.clipKey))
            {
                throw new XFrameworkException("XAnimation Play request.clipKey cannot be empty.");
            }

            XAnimationCompiledClip clip = CompiledAsset.GetClip(request.clipKey);
            XAnimationCompiledChannel channel = ResolveChannel(clip, request.channelName);
            bool isLooping = request.loopOverride ?? clip.Config.loop;
            float fadeIn = request.fadeIn > 0f ? request.fadeIn : clip.Config.defaultFadeIn;
            float fadeOut = request.fadeOut > 0f ? request.fadeOut : clip.Config.defaultFadeOut;
            float weight = request.weight > 0f ? request.weight : 1f;
            float normalizedTime = Mathf.Clamp01(request.normalizedTime);
            float speed = Mathf.Approximately(request.speed, 0f) ? 1f : request.speed;
            bool interruptible = request.interruptible;
            bool drivesRootMotion = ResolveRootMotion(clip, channel, request.rootMotionOverride);

            XAnimationPlaybackOptions options = new(
                fadeIn,
                fadeOut,
                weight,
                normalizedTime,
                speed,
                isLooping,
                request.priority,
                interruptible,
                drivesRootMotion);

            if (!m_ChannelMap[channel.Name].TryPlay(clip, options, m_CueDispatcher))
            {
                return;
            }
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
            return GetChannel(channelName).GetState();
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
                channel.PrepareFrame(deltaTime);
                m_LayerMixer.SetInputWeight(i, channel.ChannelWeight);
            }

            m_Graph.Evaluate(deltaTime);

            for (int i = 0; i < m_Channels.Count; i++)
            {
                m_Channels[i].FinalizeFrame(m_CueDispatcher);
            }

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

            IsDisposed = true;
        }

        private void BuildGraph()
        {
            m_CueDispatcher.Register(CompiledAsset.CuesByClipKey);

            m_Graph = PlayableGraph.Create($"XAnimationPlayer_{Animator.name}");
            m_Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            m_LayerMixer = AnimationLayerMixerPlayable.Create(m_Graph, CompiledAsset.Channels.Count);
            m_Output = AnimationPlayableOutput.Create(m_Graph, "XAnimationOutput", Animator);
            m_Output.SetSourcePlayable(m_LayerMixer);

            for (int i = 0; i < CompiledAsset.Channels.Count; i++)
            {
                XAnimationCompiledChannel compiledChannel = (XAnimationCompiledChannel)CompiledAsset.Channels[i];
                XAnimationChannel channel = new(m_Graph, compiledChannel, NextPlaybackId);
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

        private XAnimationCompiledChannel ResolveChannel(XAnimationCompiledClip clip, string channelName)
        {
            if (!string.IsNullOrWhiteSpace(channelName))
            {
                return CompiledAsset.GetChannel(channelName);
            }

            return (XAnimationCompiledChannel)CompiledAsset.Channels[clip.DefaultChannelIndex];
        }

        private bool ResolveRootMotion(XAnimationCompiledClip clip, XAnimationCompiledChannel channel, bool? rootMotionOverride)
        {
            bool drivesRootMotion;
            if (rootMotionOverride.HasValue)
            {
                drivesRootMotion = rootMotionOverride.Value;
            }
            else
            {
                drivesRootMotion = clip.Config.rootMotionMode switch
                {
                    XAnimationClipRootMotionMode.ForceOn => true,
                    XAnimationClipRootMotionMode.ForceOff => false,
                    _ => channel.Config.canDriveRootMotion,
                };
            }

            if (drivesRootMotion && !channel.Config.canDriveRootMotion)
            {
                throw new XFrameworkException($"XAnimation clip '{clip.Key}' cannot drive root motion on channel '{channel.Name}'.");
            }

            return drivesRootMotion;
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

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(XAnimationPlayer));
            }
        }
    }
}
