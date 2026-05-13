using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Tasks;

namespace XFramework.Animation
{
    public sealed class XAnimationDriver : IDisposable
    {
        private readonly XAnimationAssetLoader m_AssetLoader = new();
        private readonly Dictionary<int, PendingPlaybackExit> m_PendingPlaybackExits = new();

        private XAnimationCompiledAsset m_CompiledAsset;
        private XAnimationContext m_Context;
        private XAnimationPlayer m_Player;
        private bool m_IsPaused;
        private float m_TimeScale = 1f;
        private bool m_IsStepping;
        private bool m_IsRegisteredForAutomaticUpdate;

        public event Action<XAnimationCueEvent> CueTriggered;
        public event Action<XAnimationStateEvent> OnStateEnter;
        public event Action<XAnimationStateEvent> OnStateExit;
        public event Action<Animator, Vector3, Quaternion> FrameEvaluated;

        public Animator Animator { get; private set; }
        public XAnimationAsset Asset => m_CompiledAsset?.Asset;
        public XAnimationCompiledAsset CompiledAsset => m_CompiledAsset;
        public bool IsPaused => m_IsPaused;
        public float TimeScale => m_TimeScale;
        internal bool IsRegisteredForAutomaticUpdate => m_IsRegisteredForAutomaticUpdate && m_Player != null;

        private sealed class PendingPlaybackExit
        {
            public PendingPlaybackExit(
                int playbackId,
                string channelName,
                string requestedStateKey,
                string requestedClipKey,
                bool isTemporaryState,
                XAwaitableTask<XAnimationPlaybackExitResult> task)
            {
                PlaybackId = playbackId;
                ChannelName = channelName ?? string.Empty;
                RequestedStateKey = requestedStateKey ?? string.Empty;
                RequestedClipKey = requestedClipKey ?? string.Empty;
                IsTemporaryState = isTemporaryState;
                Task = task ?? throw new ArgumentNullException(nameof(task));
            }

            public int PlaybackId { get; }
            public string ChannelName { get; }
            public string RequestedStateKey { get; }
            public string RequestedClipKey { get; }
            public bool IsTemporaryState { get; }
            public XAwaitableTask<XAnimationPlaybackExitResult> Task { get; }
        }

        public void Initialize(string assetPath, Animator animator)
        {
            ValidateAssetPath(assetPath);
            ValidateAnimator(animator);
            DisposePlayer();
            InitializeLoadedAsset(m_AssetLoader.Load(assetPath), animator);
        }

        public void Initialize(TextAsset animationAsset, Animator animator)
        {
            ValidateAnimationAsset(animationAsset);
            ValidateAnimator(animator);
            DisposePlayer();
            InitializeLoadedAsset(m_AssetLoader.Load(animationAsset), animator);
        }

        public void Initialize(XAnimationCompiledAsset compiledAsset, Animator animator)
        {
            ValidateCompiledAsset(compiledAsset);
            ValidateAnimator(animator);
            DisposePlayer();
            InitializeLoadedAsset(compiledAsset, animator);
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

        public void SetParameter(string key, int value)
        {
            EnsureInitialized();
            m_Context.SetInt(key, value);
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

        public XAnimationPlaybackHandle PlayClip(string clipName, string channelName, XAnimationTransitionOptions transition = null)
        {
            EnsureInitialized();
            XAnimationPlaybackStartInfo startInfo = m_Player.PlayClip(clipName, channelName, NormalizeTransitionOptions(transition));
            return CreatePlaybackHandle(startInfo, string.Empty, clipName);
        }

        public XAnimationPlaybackHandle PlayState(string stateName, XAnimationTransitionOptions transition = null)
        {
            EnsureInitialized();
            XAnimationPlaybackStartInfo startInfo = m_Player.PlayState(stateName, NormalizeTransitionOptions(transition));
            return CreatePlaybackHandle(startInfo, stateName, string.Empty);
        }

        public XAnimationPlaybackHandle PlayState(string stateName, bool force)
        {
            return PlayState(stateName, new XAnimationTransitionOptions { force = force });
        }

        public XAnimationPlaybackHandle PlayState(
            string stateName,
            XAnimationTransitionOptions transition,
            bool force)
        {
            transition ??= new XAnimationTransitionOptions();
            transition.force = force;
            return PlayState(stateName, transition);
        }

        public void Stop(string channelName, float fadeOut = 0)
        {
            EnsureInitialized();
            m_Player.Stop(channelName, fadeOut);
        }

        public void StopAll(float fadeOut = 0)
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

        public bool SeekChannel(string channelName, float normalizedTime)
        {
            EnsureInitialized();
            return m_Player.SeekChannel(channelName, normalizedTime);
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            EnsureInitialized();
            m_Player.SetRootMotionEnabled(enabled);
        }

        public void Pause()
        {
            SetPaused(true);
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void SetPaused(bool paused)
        {
            m_IsPaused = paused;
        }

        public void SetTimeScale(float timeScale)
        {
            m_TimeScale = Mathf.Max(0f, timeScale);
        }

        public void Step(float deltaTime)
        {
            EnsureInitialized();
            if (deltaTime <= 0f)
            {
                throw new XFrameworkException("XAnimation step deltaTime must be greater than 0.");
            }

            bool originalPaused = m_IsPaused;
            m_IsStepping = true;
            try
            {
                UpdateInternal(deltaTime);
            }
            finally
            {
                m_IsStepping = false;
                m_IsPaused = originalPaused;
            }
        }

        public XAnimationChannelState GetChannelState(string channelName)
        {
            EnsureInitialized();
            return m_Player.GetChannelState(channelName);
        }

        public bool TryGetCurrentState(string channelName, out XAnimationChannelState state)
        {
            EnsureInitialized();
            return m_Player.TryGetCurrentState(channelName, out state);
        }

        public bool IsPlaying(string stateKey, string channelName = null)
        {
            EnsureInitialized();
            return m_Player.IsPlaying(stateKey, channelName);
        }

        public float GetStateDuration(string stateKey)
        {
            EnsureInitialized();
            return m_Player.GetStateDuration(stateKey);
        }

        public float GetClipDuration(string clipKey)
        {
            EnsureInitialized();
            return m_Player.GetClipDuration(clipKey);
        }

        public bool ShouldApplyNativeRootMotion()
        {
            return m_Player != null && m_Player.ShouldApplyNativeRootMotion();
        }

        public XAnimationDebugGraphSnapshot GetDebugGraphSnapshot()
        {
            if (m_Player == null)
            {
                return XAnimationDebugGraphSnapshot.Invalid("XAnimationDriver is not initialized.");
            }

            return m_Player.GetDebugGraphSnapshot();
        }

        internal void Update(float deltaTime)
        {
            EnsureInitialized();
            UpdateInternal(deltaTime);
        }

        public void SyncFrame()
        {
            EnsureInitialized();
            UpdateInternal(0f);
        }

        public void Dispose()
        {
            UnregisterFromAutomaticUpdate();
            DisposePlayer();
            m_PendingPlaybackExits.Clear();
            m_Context = null;
            m_CompiledAsset = null;
            Animator = null;
        }

        internal bool TryGetPlaybackState(int playbackId, string channelName, out XAnimationChannelState state)
        {
            state = null;
            if (m_Player == null || playbackId <= 0 || string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            if (!m_Player.TryGetCurrentState(channelName, out state) || state == null)
            {
                return false;
            }

            if (state.playbackId != playbackId)
            {
                state = null;
                return false;
            }

            return true;
        }

        private void EnsureInitialized()
        {
            if (m_Player == null)
            {
                throw new XFrameworkException("XAnimationDriver is not initialized.");
            }
        }

        private static void ValidateAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new XFrameworkException("XAnimationDriver assetPath cannot be empty.");
            }
        }

        private static void ValidateAnimationAsset(TextAsset animationAsset)
        {
            if (animationAsset == null)
            {
                throw new XFrameworkException("XAnimationDriver animationAsset cannot be null.");
            }
        }

        private static void ValidateCompiledAsset(XAnimationCompiledAsset compiledAsset)
        {
            if (compiledAsset == null)
            {
                throw new XFrameworkException("XAnimationDriver compiledAsset cannot be null.");
            }
        }

        private static void ValidateAnimator(Animator animator)
        {
            if (animator == null)
            {
                throw new XFrameworkException("XAnimationDriver animator cannot be null.");
            }
        }

        private void InitializeLoadedAsset(XAnimationCompiledAsset compiledAsset, Animator animator)
        {
            m_CompiledAsset = compiledAsset;
            m_PendingPlaybackExits.Clear();
            Animator = animator;
            m_Context = new XAnimationContext(m_CompiledAsset.Parameters);
            m_Player = new XAnimationPlayer(m_CompiledAsset, animator, m_Context);
            m_Player.CueTriggered += OnCueTriggered;
            m_Player.StateEntered += OnPlayerStateEntered;
            m_Player.StateExited += OnPlayerStateExited;
            m_IsPaused = false;
            m_TimeScale = 1f;
            RegisterForAutomaticUpdate();
        }

        private void DisposePlayer()
        {
            if (m_Player == null)
            {
                return;
            }

            m_Player.Dispose();
            m_Player.CueTriggered -= OnCueTriggered;
            m_Player.StateEntered -= OnPlayerStateEntered;
            m_Player.StateExited -= OnPlayerStateExited;
            m_Player = null;
        }

        private void OnCueTriggered(XAnimationCueEvent cueEvent)
        {
            CueTriggered?.Invoke(cueEvent);
        }

        private void OnPlayerStateEntered(XAnimationStateEvent stateEvent)
        {
            OnStateEnter?.Invoke(stateEvent);
        }

        private void OnPlayerStateExited(XAnimationStateEvent stateEvent)
        {
            if (stateEvent != null &&
                stateEvent.playbackId > 0 &&
                m_PendingPlaybackExits.Remove(stateEvent.playbackId, out PendingPlaybackExit pending))
            {
                XAnimationPlaybackExitResult result = new()
                {
                    WasStarted = true,
                    PlaybackId = pending.PlaybackId,
                    ChannelName = pending.ChannelName,
                    RequestedStateKey = pending.RequestedStateKey,
                    RequestedClipKey = pending.RequestedClipKey,
                    IsTemporaryState = pending.IsTemporaryState,
                    ExitReason = stateEvent.exitReason,
                };
                pending.Task.SetResult(result);
            }

            OnStateExit?.Invoke(stateEvent);
        }

        private static XAnimationTransitionOptions NormalizeTransitionOptions(XAnimationTransitionOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return new XAnimationTransitionOptions
            {
                fadeIn = Mathf.Max(0f, options.fadeIn),
                fadeOut = Mathf.Max(0f, options.fadeOut),
                enterTime = Mathf.Clamp01(options.enterTime),
                priority = options.priority,
                interruptible = options.interruptible,
                force = options.force,
            };
        }

        private XAnimationPlaybackHandle CreatePlaybackHandle(
            XAnimationPlaybackStartInfo startInfo,
            string requestedStateKey,
            string requestedClipKey)
        {
            requestedStateKey ??= string.Empty;
            requestedClipKey ??= string.Empty;

            if (!startInfo.Started)
            {
                XAnimationPlaybackExitResult result = new()
                {
                    WasStarted = false,
                    PlaybackId = 0,
                    ChannelName = startInfo.ChannelName,
                    RequestedStateKey = requestedStateKey,
                    RequestedClipKey = requestedClipKey,
                    IsTemporaryState = startInfo.IsTemporaryState,
                    ExitReason = null,
                };
                return new XAnimationPlaybackHandle(
                    this,
                    false,
                    0,
                    startInfo.ChannelName,
                    requestedStateKey,
                    requestedClipKey,
                    startInfo.IsTemporaryState,
                    CreateCompletedExitTask(result));
            }

            XAwaitableTask<XAnimationPlaybackExitResult> task = new();
            PendingPlaybackExit pending = new(
                startInfo.PlaybackId,
                startInfo.ChannelName,
                requestedStateKey,
                requestedClipKey,
                startInfo.IsTemporaryState,
                task);
            m_PendingPlaybackExits[startInfo.PlaybackId] = pending;
            return new XAnimationPlaybackHandle(
                this,
                true,
                startInfo.PlaybackId,
                startInfo.ChannelName,
                requestedStateKey,
                requestedClipKey,
                startInfo.IsTemporaryState,
                task);
        }

        private static XAwaitableTask<XAnimationPlaybackExitResult> CreateCompletedExitTask(XAnimationPlaybackExitResult result)
        {
            XAwaitableTask<XAnimationPlaybackExitResult> task = new();
            task.SetResult(result);
            return task;
        }

        internal void TickFromScheduler(float deltaTime)
        {
            if (m_Player == null || m_IsPaused || m_IsStepping)
            {
                return;
            }

            UpdateInternal(deltaTime * m_TimeScale);
        }

        private void UpdateInternal(float deltaTime)
        {
            if (deltaTime < 0f || m_Player == null)
            {
                return;
            }

            Transform animatorTransform = Animator != null ? Animator.transform : null;
            Vector3 previousPosition = animatorTransform != null ? animatorTransform.position : Vector3.zero;
            Quaternion previousRotation = animatorTransform != null ? animatorTransform.rotation : Quaternion.identity;
            m_Player.Update(deltaTime);
            FrameEvaluated?.Invoke(Animator, previousPosition, previousRotation);
        }

        private void RegisterForAutomaticUpdate()
        {
            if (m_IsRegisteredForAutomaticUpdate)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                XAnimationEditorUpdateRunner.Register(this);
                m_IsRegisteredForAutomaticUpdate = true;
                return;
            }
#endif

            XAnimationRuntimePlayerLoopRunner.Register(this);
            m_IsRegisteredForAutomaticUpdate = true;
        }

        private void UnregisterFromAutomaticUpdate()
        {
            if (!m_IsRegisteredForAutomaticUpdate)
            {
                return;
            }

#if UNITY_EDITOR
            XAnimationEditorUpdateRunner.Unregister(this);
#endif
            XAnimationRuntimePlayerLoopRunner.Unregister(this);
            m_IsRegisteredForAutomaticUpdate = false;
        }
    }
}
