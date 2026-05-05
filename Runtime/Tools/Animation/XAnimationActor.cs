using System;
using UnityEngine;

namespace XFramework.Animation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/Animation/XAnimation Actor")]
    public sealed class XAnimationActor : MonoBehaviour
    {
        [SerializeField] private TextAsset m_AnimationAsset;
        [SerializeField] private Animator m_Animator;
        [SerializeField] private bool m_InitializeOnAwake = true;
        [SerializeField] private bool m_PlayOnStart = true;
        [SerializeField] private string m_StartStateKey = "";
        [SerializeField] private float m_TimeScale = 1f;

        private readonly XAnimationDriver m_Driver = new();
        private bool m_IsInitialized;
        private bool m_IsPaused;

        public event Action<XAnimationCueEvent> CueTriggered;
        public event Action<XAnimationStateEvent> OnStateEnter;
        public event Action<XAnimationStateEvent> OnStateExit;

        public TextAsset AnimationAsset => m_AnimationAsset;
        public Animator Animator => m_Animator;
        public XAnimationDriver Driver => m_Driver;
        public XAnimationAsset Asset => m_Driver.Asset;
        public XAnimationCompiledAsset CompiledAsset => m_Driver.CompiledAsset;
        public bool IsInitialized => m_IsInitialized;
        public bool IsPaused => m_IsPaused;
        public float TimeScale
        {
            get => m_TimeScale;
            set
            {
                m_TimeScale = Mathf.Max(0f, value);
                if (m_IsInitialized)
                {
                    m_Driver.SetTimeScale(m_TimeScale);
                }
            }
        }

        private void Awake()
        {
            if (m_InitializeOnAwake)
            {
                Initialize();
            }
        }

        private void Start()
        {
            if (!m_PlayOnStart)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(m_StartStateKey))
            {
                PlayState(m_StartStateKey);
            }
        }

        private void OnDestroy()
        {
            DisposeDriver();
        }

        public void SetAnimationAsset(TextAsset animationAsset, bool initializeNow = true)
        {
            m_AnimationAsset = animationAsset;
            RefreshInitialization(initializeNow);
        }

        public void SetAnimator(Animator animator, bool initializeNow = true)
        {
            m_Animator = animator;
            RefreshInitialization(initializeNow);
        }

        public void Initialize()
        {
            Initialize(m_AnimationAsset, ResolveAnimator());
        }

        public void Initialize(TextAsset animationAsset)
        {
            Initialize(animationAsset, ResolveAnimator());
        }

        public void Initialize(TextAsset animationAsset, Animator animator)
        {
            if (animationAsset == null)
            {
                throw new XFrameworkException($"{nameof(XAnimationActor)} requires an XAnimation TextAsset.");
            }

            if (animator == null)
            {
                throw new XFrameworkException($"{nameof(XAnimationActor)} requires an Animator.");
            }

            DisposeDriver();
            m_AnimationAsset = animationAsset;
            m_Animator = animator;
            m_Driver.Initialize(m_AnimationAsset, m_Animator);
            m_Driver.CueTriggered += HandleCueTriggered;
            m_Driver.OnStateEnter += HandleStateEnter;
            m_Driver.OnStateExit += HandleStateExit;
            m_Driver.SetPaused(m_IsPaused);
            m_Driver.SetTimeScale(m_TimeScale);
            m_IsInitialized = true;
        }

        public XAnimationPlaybackHandle PlayClip(string clipName, string channelName, XAnimationTransitionOptions transition = default)
        {
            EnsureInitialized();
            return m_Driver.PlayClip(clipName, channelName, transition);
        }

        public XAnimationPlaybackHandle PlayState(string stateName, XAnimationTransitionOptions transition = default)
        {
            EnsureInitialized();
            return m_Driver.PlayState(stateName, transition);
        }

        public void Play(string clipKey)
        {
            throw new XFrameworkException("XAnimationActor.Play(string clipKey) has been removed. Use PlayClip(clipName, channelName, ...) instead.");
        }

        public XAnimationPlaybackHandle Play(string clipKey, string channelName)
        {
            return PlayClip(clipKey, channelName);
        }

        public XAnimationPlaybackHandle PlayState(string stateKey)
        {
            return PlayState(stateKey, default);
        }

        public void Stop(string channelName, float fadeOut = default)
        {
            EnsureInitialized();
            m_Driver.Stop(channelName, fadeOut);
        }

        public void StopAll(float fadeOut = default)
        {
            EnsureInitialized();
            m_Driver.StopAll(fadeOut);
        }

        public void Pause()
        {
            m_IsPaused = true;
            if (m_IsInitialized)
            {
                m_Driver.Pause();
            }
        }

        public void Resume()
        {
            m_IsPaused = false;
            if (m_IsInitialized)
            {
                m_Driver.Resume();
            }
        }

        public void SetPaused(bool paused)
        {
            m_IsPaused = paused;
            if (m_IsInitialized)
            {
                m_Driver.SetPaused(paused);
            }
        }

        public void Step(float deltaTime)
        {
            EnsureInitialized();
            m_Driver.Step(deltaTime);
        }

        public void SetParameter(string key, float value)
        {
            EnsureInitialized();
            m_Driver.SetParameter(key, value);
        }

        public void SetParameter(string key, bool value)
        {
            EnsureInitialized();
            m_Driver.SetParameter(key, value);
        }

        public void SetParameter(string key, int value)
        {
            EnsureInitialized();
            m_Driver.SetParameter(key, value);
        }

        public void SetTrigger(string key)
        {
            EnsureInitialized();
            m_Driver.SetTrigger(key);
        }

        public void ResetTrigger(string key)
        {
            EnsureInitialized();
            m_Driver.ResetTrigger(key);
        }

        public void SetChannelWeight(string channelName, float weight)
        {
            EnsureInitialized();
            m_Driver.SetChannelWeight(channelName, weight);
        }

        public void SetChannelTimeScale(string channelName, float timeScale)
        {
            EnsureInitialized();
            m_Driver.SetChannelTimeScale(channelName, timeScale);
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            EnsureInitialized();
            m_Driver.SetRootMotionEnabled(enabled);
        }

        public XAnimationChannelState GetChannelState(string channelName)
        {
            EnsureInitialized();
            return m_Driver.GetChannelState(channelName);
        }

        public bool TryGetCurrentState(string channelName, out XAnimationChannelState state)
        {
            EnsureInitialized();
            return m_Driver.TryGetCurrentState(channelName, out state);
        }

        public bool IsPlaying(string stateKey, string channelName = null)
        {
            EnsureInitialized();
            return m_Driver.IsPlaying(stateKey, channelName);
        }

        public float GetStateDuration(string stateKey)
        {
            EnsureInitialized();
            return m_Driver.GetStateDuration(stateKey);
        }

        public float GetClipDuration(string clipKey)
        {
            EnsureInitialized();
            return m_Driver.GetClipDuration(clipKey);
        }

        public bool TryGetRootMotionDelta(out Vector3 deltaPosition, out Quaternion deltaRotation)
        {
            EnsureInitialized();
            return m_Driver.TryGetRootMotionDelta(out deltaPosition, out deltaRotation);
        }

        public void Dispose()
        {
            DisposeDriver();
        }

        private Animator ResolveAnimator()
        {
            if (m_Animator != null)
            {
                return m_Animator;
            }

            m_Animator = GetComponentInChildren<Animator>();
            return m_Animator;
        }

        private void EnsureInitialized()
        {
            if (!m_IsInitialized)
            {
                Initialize();
            }
        }

        private void RefreshInitialization(bool initializeNow)
        {
            if (initializeNow)
            {
                Initialize();
                return;
            }

            DisposeDriver();
        }

        private void DisposeDriver()
        {
            if (!m_IsInitialized)
            {
                return;
            }

            m_Driver.CueTriggered -= HandleCueTriggered;
            m_Driver.OnStateEnter -= HandleStateEnter;
            m_Driver.OnStateExit -= HandleStateExit;
            m_Driver.Dispose();
            m_IsInitialized = false;
        }

        private void HandleCueTriggered(XAnimationCueEvent cueEvent)
        {
            CueTriggered?.Invoke(cueEvent);
        }

        private void HandleStateEnter(XAnimationStateEvent stateEvent)
        {
            OnStateEnter?.Invoke(stateEvent);
        }

        private void HandleStateExit(XAnimationStateEvent stateEvent)
        {
            OnStateExit?.Invoke(stateEvent);
        }
    }
}
