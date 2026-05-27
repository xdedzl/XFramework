using System;
using UnityEngine;

namespace XFramework.Animation
{
    public struct XAnimationActorInfo
    {
        public TextAsset animationAsset;
        public Animator animator;
        public XAnimationUpdateMode updateMode;
        public bool playOnStart;
        public string startStateKey;
        public bool unityAnimationEventsEnabled;
    }
    
    
    [DisallowMultipleComponent]
    [AddComponentMenu("XAnimation/XAnimation Actor")]
    public sealed class XAnimationActor : MonoBehaviour
    {
        [SerializeField] private TextAsset m_AnimationAsset;
        [SerializeField] private Animator m_Animator;
        [SerializeField] private XAnimationUpdateMode m_UpdateMode = XAnimationUpdateMode.Manual;
        [SerializeField] private bool m_PlayOnStart = true;
        [SerializeField] private string m_StartStateKey = "";
        [SerializeField] private bool m_UnityAnimationEventsEnabled = false;

        private readonly XAnimationDriver m_Driver = new();
        private bool m_PausedByDisable;
        private Action<Animator, Vector3, Quaternion> m_NativeRootMotionApplied;
        private Action<Animator> m_OnAnimatorMove = IgnoreAnimatorMove;
        
        private XAnimationRootMotionBridge m_RootMotionBridge;

        public TextAsset AnimationAsset => m_AnimationAsset;
        public bool IsPaused => m_Driver.IsPaused;
        public event Action<XAnimationCueEvent> CueTriggered
        {
            add => m_Driver.CueTriggered += value;
            remove => m_Driver.CueTriggered -= value;
        }

        public event Action<XAnimationStateEvent> OnStateEnter
        {
            add => m_Driver.OnStateEnter += value;
            remove => m_Driver.OnStateEnter -= value;
        }

        public event Action<XAnimationStateEvent> OnStateExit
        {
            add => m_Driver.OnStateExit += value;
            remove => m_Driver.OnStateExit -= value;
        }

        public event Action<Animator, Vector3, Quaternion> NativeRootMotionApplied
        {
            add
            {
                m_NativeRootMotionApplied += value;
                SyncRootMotionBridge();
            }
            remove
            {
                m_NativeRootMotionApplied -= value;
                SyncRootMotionBridge();
            }
        }

        public float TimeScale
        {
            get => m_Driver.TimeScale;
            set
            {
                m_Driver.SetTimeScale(value);
            }
        }
        
        public XAnimationUpdateMode UpdateMode
        {
            get => m_UpdateMode;
            set
            {
                m_UpdateMode = value;
                m_Driver.SetUpdateMode(m_UpdateMode);
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!m_PausedByDisable)
            {
                return;
            }

            m_PausedByDisable = false;
            m_Driver.Resume();
        }

        private void OnDisable()
        {
            if (m_Driver.IsPaused)
            {
                return;
            }
            
            m_Driver.Pause();
            m_PausedByDisable = true;
        }

        private void OnDestroy()
        {
            UnbindRootMotionBridge();
            m_Driver.Dispose();
            m_PausedByDisable = false;
        }
        
        public void SetInfo(XAnimationActorInfo info)
        {
            m_AnimationAsset = info.animationAsset;
            m_Animator = info.animator;
            m_UpdateMode = info.updateMode;
            m_PlayOnStart = info.playOnStart;
            m_StartStateKey = info.startStateKey;
            m_UnityAnimationEventsEnabled = info.unityAnimationEventsEnabled;
        }

        public void Initialize()
        {
            if (m_Animator == null)
            {
                m_Animator = GetComponentInChildren<Animator>();
            }
            
            if (m_AnimationAsset == null)
            {
                Debug.LogWarning($"{nameof(XAnimationActor)} requires an XAnimation TextAsset.");
            }

            if (m_Animator == null)
            {
                Debug.LogWarning($"{nameof(XAnimationActor)} requires an Animator.");
            }
            
            m_Driver.Initialize(m_AnimationAsset, m_Animator);
            m_Driver.SetUpdateMode(m_UpdateMode);
            m_Driver.SetUnityAnimationEventsEnabled(m_UnityAnimationEventsEnabled);
            if (!isActiveAndEnabled)
            {
                m_Driver.Pause();
                m_PausedByDisable = true;
            }
            if (m_PlayOnStart && !string.IsNullOrWhiteSpace(m_StartStateKey))
            {
                PlayState(m_StartStateKey);
            }
            SyncRootMotionBridge();
        }

        public XAnimationPlaybackHandle PlayClip(string clipName, string channelName, XAnimationTransitionOptions transition = null)
        {
            return m_Driver.PlayClip(clipName, channelName, transition);
        }

        public XAnimationPlaybackHandle PlayClip(AnimationClip clip, string channelName, XAnimationTransitionOptions transition = null)
        {
            return m_Driver.PlayClip(clip, channelName, transition);
        }

        public XAnimationPlaybackHandle PlayState(string stateName)
        {
            return m_Driver.PlayState(stateName);
        }

        public XAnimationPlaybackHandle PlayState(string stateName, bool force)
        {
            return m_Driver.PlayState(stateName, force);
        }

        public XAnimationPlaybackHandle PlayState(string stateName, XAnimationTransitionOptions transition)
        {
            return m_Driver.PlayState(stateName, transition, false);
        }

        public XAnimationPlaybackHandle PlayState(string stateName, XAnimationTransitionOptions transition, bool force)
        {
            return m_Driver.PlayState(stateName, transition, force);
        }

        public void Stop(string channelName, float fadeOut = 0)
        {
            m_Driver.Stop(channelName, fadeOut);
        }

        public void StopAll(float fadeOut = 0)
        {
            m_Driver.StopAll(fadeOut);
        }

        public void Pause()
        {
            m_PausedByDisable = false;
            m_Driver.Pause();
        }

        public void Resume()
        {
            m_PausedByDisable = false;
            m_Driver.Resume();
        }

        public void Step(float deltaTime)
        {
            m_Driver.Step(deltaTime);
        }

        public void SetParameter(string key, float value)
        {
            m_Driver.SetParameter(key, value);
        }

        public void SetParameter(string key, bool value)
        {
            m_Driver.SetParameter(key, value);
        }

        public void SetParameter(string key, int value)
        {
            m_Driver.SetParameter(key, value);
        }

        public void SetTrigger(string key)
        {
            m_Driver.SetTrigger(key);
        }

        public void ResetTrigger(string key)
        {
            m_Driver.ResetTrigger(key);
        }

        public bool TryGetParameter(string key, out float value)
        {
            return m_Driver.TryGetParameter(key, out value);
        }

        public bool TryGetParameter(string key, out bool value)
        {
            return m_Driver.TryGetParameter(key, out value);
        }

        public bool TryGetParameter(string key, out int value)
        {
            return m_Driver.TryGetParameter(key, out value);
        }

        public bool TryGetTrigger(string key, out bool value)
        {
            return m_Driver.TryGetTrigger(key, out value);
        }

        public void SetChannelWeight(string channelName, float weight)
        {
            m_Driver.SetChannelWeight(channelName, weight);
        }

        public void SetChannelTimeScale(string channelName, float timeScale)
        {
            m_Driver.SetChannelTimeScale(channelName, timeScale);
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            m_Driver.SetRootMotionEnabled(enabled);
            SyncRootMotionBridge();
        }

        public XAnimationChannelState GetChannelState(string channelName)
        {
            return m_Driver.GetChannelState(channelName);
        }

        public bool TryGetCurrentState(string channelName, out XAnimationChannelState state)
        {
            return m_Driver.TryGetCurrentState(channelName, out state);
        }

        public bool IsPlaying(string stateKey, string channelName = null)
        {
            return m_Driver.IsPlaying(stateKey, channelName);
        }

        public float GetStateDuration(string stateKey)
        {
            return m_Driver.GetStateDuration(stateKey);
        }

        public float GetClipDuration(string clipKey)
        {
            return m_Driver.GetClipDuration(clipKey);
        }

        public void PreloadAll()
        {
            m_Driver.PreloadAll();
        }

        public void PreloadState(string stateKey)
        {
            m_Driver.PreloadState(stateKey);
        }

        public XAnimationDebugGraphSnapshot GetDebugGraphSnapshot()
        {
            return m_Driver.GetDebugGraphSnapshot();
        }

        internal void HandleNativeRootMotion(Animator animator)
        {
            m_OnAnimatorMove.Invoke(animator);
        }

        private void ApplyNativeRootMotion(Animator animator)
        {
            m_NativeRootMotionApplied.Invoke(animator, animator.deltaPosition, animator.deltaRotation);
        }

        private void ApplyDefaultNativeRootMotion(Animator animator)
        {
            Transform target = transform;
            target.position += animator.deltaPosition;
            target.rotation = animator.deltaRotation * target.rotation;
        }

        private void SyncRootMotionBridge()
        {
            if (!m_Driver.ShouldApplyNativeRootMotion())
            {
                UnbindRootMotionBridge();
                return;
            }

            m_OnAnimatorMove = m_NativeRootMotionApplied == null
                ? ApplyDefaultNativeRootMotion
                : ApplyNativeRootMotion;
            BindRootMotionBridge();
        }

        private void BindRootMotionBridge()
        {
            if (m_Animator == null)
            {
                return;
            }

            m_RootMotionBridge = m_Animator.GetComponent<XAnimationRootMotionBridge>();
            if (m_RootMotionBridge == null)
            {
                m_RootMotionBridge = m_Animator.gameObject.AddComponent<XAnimationRootMotionBridge>();
            }

            m_RootMotionBridge.Bind(this, m_Animator);
        }

        private void UnbindRootMotionBridge()
        {
            XAnimationRootMotionBridge bridge = m_RootMotionBridge;
            if (bridge == null && m_Animator != null)
            {
                bridge = m_Animator.GetComponent<XAnimationRootMotionBridge>();
            }

            if (bridge == null)
            {
                return;
            }

            bridge.Unbind(this);
            if (!bridge.IsBound)
            {
                if (Application.isPlaying)
                {
                    Destroy(bridge);
                }
                else
                {
                    DestroyImmediate(bridge);
                }
            }

            m_RootMotionBridge = null;
            m_OnAnimatorMove = IgnoreAnimatorMove;
        }

        private static void IgnoreAnimatorMove(Animator animator)
        {
        }
    }

    [DisallowMultipleComponent]
    internal sealed class XAnimationRootMotionBridge : MonoBehaviour
    {
        private XAnimationActor m_Actor;
        private Animator m_Animator;

        internal void Bind(XAnimationActor actor, Animator animator)
        {
            m_Actor = actor;
            m_Animator = animator;
        }

        internal void Unbind(XAnimationActor actor)
        {
            if (m_Actor != actor)
            {
                return;
            }

            m_Actor = null;
            m_Animator = null;
        }

        internal bool IsBound => m_Actor != null;

        private void OnAnimatorMove()
        {
            if (m_Actor == null || m_Animator == null)
            {
                return;
            }

            m_Actor.HandleNativeRootMotion(m_Animator);
        }
    }
}
