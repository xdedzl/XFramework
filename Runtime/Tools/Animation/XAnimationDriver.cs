using System;
using UnityEngine;

namespace XFramework.Animation
{
    public sealed class XAnimationDriver : IDisposable
    {
        private readonly XAnimationAssetLoader m_AssetLoader = new();

        private XAnimationCompiledAsset m_CompiledAsset;
        private XAnimationContext m_Context;
        private XAnimationPlayer m_Player;

        public event Action<XAnimationCueEvent> CueTriggered;

        public Animator Animator { get; private set; }
        public XAnimationAsset Asset => m_CompiledAsset?.Asset;
        public XAnimationCompiledAsset CompiledAsset => m_CompiledAsset;

        public void Initialize(string assetPath, Animator animator)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new XFrameworkException("XAnimationDriver assetPath cannot be empty.");
            }

            if (animator == null)
            {
                throw new XFrameworkException("XAnimationDriver animator cannot be null.");
            }

            DisposePlayer();

            Initialize(m_AssetLoader.Load(assetPath), animator);
        }

        public void Initialize(TextAsset animationAsset, Animator animator)
        {
            if (animationAsset == null)
            {
                throw new XFrameworkException("XAnimationDriver animationAsset cannot be null.");
            }

            if (animator == null)
            {
                throw new XFrameworkException("XAnimationDriver animator cannot be null.");
            }

            DisposePlayer();

            Initialize(m_AssetLoader.Load(animationAsset), animator);
        }

        public void Initialize(XAnimationCompiledAsset compiledAsset, Animator animator)
        {
            if (compiledAsset == null)
            {
                throw new XFrameworkException("XAnimationDriver compiledAsset cannot be null.");
            }

            if (animator == null)
            {
                throw new XFrameworkException("XAnimationDriver animator cannot be null.");
            }

            DisposePlayer();

            m_CompiledAsset = compiledAsset;
            if (m_CompiledAsset.Asset.graph != null && m_CompiledAsset.Asset.graph.enabled)
            {
                throw new XFrameworkException("XAnimationStateGraph runtime is not supported in phase 1. Disable graph.enabled before initialization.");
            }

            Animator = animator;
            m_Context = new XAnimationContext(m_CompiledAsset.Parameters);
            m_Player = new XAnimationPlayer(m_CompiledAsset, animator, m_Context);
            m_Player.CueTriggered += OnCueTriggered;
        }

        public void SetParameter(string key, float value)
        {
            EnsureInitialized();
            m_Context.SetFloat(key, value);
        }

        public void SetParameter(string key, bool value)
        {
            EnsureInitialized();
            m_Context.SetBool(key, value);
        }

        public void SetTrigger(string key)
        {
            EnsureInitialized();
            m_Context.SetTrigger(key);
        }

        public void ResetTrigger(string key)
        {
            EnsureInitialized();
            m_Context.ResetTrigger(key);
        }

        public void PlayClip(
            string clipName,
            string channelName,
            XAnimationTransitionOptions transition = default,
            XAnimationPlaybackOptions playback = default)
        {
            EnsureInitialized();
            m_Player.Play(new XAnimationPlayCommand
            {
                target = new XAnimationPlayTarget
                {
                    clipKey = clipName,
                    channelName = channelName,
                },
                transition = NormalizeTransitionOptions(transition),
                playback = NormalizePlaybackOptions(playback),
            });
        }

        public void PlayState(
            string stateName,
            XAnimationTransitionOptions transition = default,
            XAnimationPlaybackOptions playback = default)
        {
            EnsureInitialized();
            m_Player.Play(new XAnimationPlayCommand
            {
                target = new XAnimationPlayTarget
                {
                    stateKey = stateName,
                },
                transition = NormalizeTransitionOptions(transition),
                playback = NormalizePlaybackOptions(playback),
            });
        }

        public void Play(XAnimationPlayCommand command)
        {
            EnsureInitialized();
            m_Player.Play(NormalizeCommand(command));
        }

        public void Stop(string channelName, float fadeOut = default)
        {
            EnsureInitialized();
            m_Player.Stop(channelName, fadeOut);
        }

        public void StopAll(float fadeOut = default)
        {
            EnsureInitialized();
            m_Player.StopAll(fadeOut);
        }

        public void SetChannelWeight(string channelName, float weight)
        {
            EnsureInitialized();
            m_Player.SetChannelWeight(channelName, weight);
        }

        public void SetChannelTimeScale(string channelName, float timeScale)
        {
            EnsureInitialized();
            m_Player.SetChannelTimeScale(channelName, timeScale);
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            EnsureInitialized();
            m_Player.SetRootMotionEnabled(enabled);
        }

        public XAnimationChannelState GetChannelState(string channelName)
        {
            EnsureInitialized();
            return m_Player.GetChannelState(channelName);
        }

        public void Update(float deltaTime)
        {
            EnsureInitialized();
            m_Player.Update(deltaTime);
        }

        public void Dispose()
        {
            DisposePlayer();
            m_Context = null;
            m_CompiledAsset = null;
            Animator = null;
        }

        private void EnsureInitialized()
        {
            if (m_Player == null)
            {
                throw new XFrameworkException("XAnimationDriver is not initialized.");
            }
        }

        private void DisposePlayer()
        {
            if (m_Player == null)
            {
                return;
            }

            m_Player.CueTriggered -= OnCueTriggered;
            m_Player.Dispose();
            m_Player = null;
        }

        private void OnCueTriggered(XAnimationCueEvent cueEvent)
        {
            CueTriggered?.Invoke(cueEvent);
        }

        private static XAnimationPlayCommand NormalizeCommand(XAnimationPlayCommand command)
        {
            return new XAnimationPlayCommand
            {
                target = command?.target ?? new XAnimationPlayTarget(),
                transition = NormalizeTransitionOptions(command?.transition),
                playback = NormalizePlaybackOptions(command?.playback),
            };
        }

        private static XAnimationTransitionOptions NormalizeTransitionOptions(XAnimationTransitionOptions options)
        {
            options ??= new XAnimationTransitionOptions();
            return new XAnimationTransitionOptions
            {
                fadeIn = Mathf.Max(0f, options.fadeIn),
                fadeOut = Mathf.Max(0f, options.fadeOut),
                priority = options.priority,
                interruptible = options.interruptible,
            };
        }

        private static XAnimationPlaybackOptions NormalizePlaybackOptions(XAnimationPlaybackOptions options)
        {
            options ??= new XAnimationPlaybackOptions();
            return new XAnimationPlaybackOptions
            {
                weight = options.weight > 0f ? options.weight : 1f,
                normalizedTime = Mathf.Clamp01(options.normalizedTime),
                speed = Mathf.Approximately(options.speed, 0f) ? 1f : options.speed,
                loopOverride = options.loopOverride,
                rootMotionOverride = options.rootMotionOverride,
            };
        }
    }
}
