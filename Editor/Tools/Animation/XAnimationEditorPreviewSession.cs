#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XFramework.Animation;

namespace XFramework.Editor
{
    internal sealed class XAnimationEditorPreviewSession : IDisposable
    {
        private const int MaxCueLogCount = 64;

        private readonly XAnimationAssetLoader m_AssetLoader = new(new XAnimationEditorAssetResolver());
        private readonly List<string> m_CueLogs = new();

        private PreviewRenderUtility m_PreviewUtility;
        private RenderTexture m_RenderTexture;
        private GameObject m_Instance;
        private Animator m_Animator;
        private XAnimationDriver m_Driver;
        private XAnimationCompiledAsset m_CompiledAsset;
        private Vector2Int m_RenderTextureSize;

        private Vector3 m_InitialPosition;
        private Quaternion m_InitialRotation;
        private Bounds m_InitialBounds;
        private Vector3 m_CameraPivot;
        private float m_CameraDistance;
        private float m_CameraYaw = 140f;
        private float m_CameraPitch = 18f;
        private bool m_RootMotionEnabled;

        public IReadOnlyList<string> CueLogs => m_CueLogs;
        public XAnimationCompiledAsset CompiledAsset => m_CompiledAsset;
        public Texture PreviewTexture => m_RenderTexture;
        public bool IsLoaded => m_Driver != null && m_Animator != null;

        public void Load(GameObject prefabAsset, string assetPath)
        {
            if (prefabAsset == null)
            {
                throw new XFrameworkException("XAnimation preview prefab cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new XFrameworkException("XAnimation preview assetPath cannot be empty.");
            }

            DisposePreview();

            m_CompiledAsset = m_AssetLoader.Load(assetPath);

            m_PreviewUtility = new PreviewRenderUtility();
            ConfigurePreviewCamera();
            ConfigurePreviewLights();

            m_Instance = UnityEngine.Object.Instantiate(prefabAsset);
            ApplyHideFlags(m_Instance);

            m_Animator = m_Instance.GetComponent<Animator>();
            if (m_Animator == null)
            {
                m_Animator = m_Instance.GetComponentInChildren<Animator>(true);
            }

            if (m_Animator == null)
            {
                throw new XFrameworkException("XAnimation preview prefab does not contain an Animator.");
            }

            SanitizePreviewInstance();
            m_Animator.runtimeAnimatorController = null;
            m_Animator.enabled = true;
            m_Animator.applyRootMotion = false;
            m_Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            m_PreviewUtility.AddSingleGO(m_Instance);

            CacheInitialTransform();
            CacheInitialBounds();

            m_Driver = new XAnimationDriver();
            m_Driver.Initialize(m_CompiledAsset, m_Animator);
            m_Driver.CueTriggered += OnCueTriggered;
            SetRootMotionEnabled(false);
        }

        public void Update(float deltaTime)
        {
            if (!IsLoaded)
            {
                return;
            }

            float clampedDeltaTime = Mathf.Clamp(deltaTime, 0f, 0.1f);
            if (clampedDeltaTime <= 0f)
            {
                return;
            }

            m_Driver.Update(clampedDeltaTime);
        }

        public void Render(Vector2 size)
        {
            if (!IsLoaded)
            {
                return;
            }

            int width = Mathf.Max(1, Mathf.RoundToInt(size.x));
            int height = Mathf.Max(1, Mathf.RoundToInt(size.y));
            EnsureRenderTexture(width, height);
            UpdateCameraTransform();

            Camera camera = m_PreviewUtility.camera;
            camera.targetTexture = m_RenderTexture;
            camera.Render();
            camera.targetTexture = null;
        }

        public void Play(string clipKey, float speed, string channelName = null)
        {
            EnsureLoaded();
            m_Driver.Play(new XAnimationPlayRequest
            {
                clipKey = clipKey,
                channelName = channelName,
                speed = Mathf.Approximately(speed, 0f) ? 1f : speed,
                weight = 1f,
                interruptible = true,
            });
        }

        public void StopAll()
        {
            if (!IsLoaded)
            {
                return;
            }

            m_Driver.StopAll();
        }

        public void SetChannelWeight(string channelName, float weight)
        {
            EnsureLoaded();
            m_Driver.SetChannelWeight(channelName, weight);
        }

        public void SetChannelTimeScale(string channelName, float timeScale)
        {
            EnsureLoaded();
            m_Driver.SetChannelTimeScale(channelName, timeScale);
        }

        public void SetRootMotionEnabled(bool enabled)
        {
            EnsureLoaded();
            m_RootMotionEnabled = enabled;
            m_Driver.SetRootMotionEnabled(enabled);
            if (!enabled)
            {
                ResetTransform();
            }
        }

        public bool GetRootMotionEnabled()
        {
            return m_RootMotionEnabled;
        }

        public void ResetTransform()
        {
            if (m_Instance == null)
            {
                return;
            }

            m_Instance.transform.SetPositionAndRotation(m_InitialPosition, m_InitialRotation);
            CacheInitialBounds();
        }

        public void ResetCamera()
        {
            m_CameraYaw = 140f;
            m_CameraPitch = 18f;
            CacheInitialBounds();
        }

        public void Orbit(Vector2 delta)
        {
            m_CameraYaw += delta.x * 0.35f;
            m_CameraPitch = Mathf.Clamp(m_CameraPitch - delta.y * 0.25f, -80f, 80f);
        }

        public void Zoom(float delta)
        {
            float zoomFactor = 1f + delta * 0.03f;
            if (zoomFactor <= 0f)
            {
                zoomFactor = 0.1f;
            }

            m_CameraDistance = Mathf.Clamp(m_CameraDistance * zoomFactor, 0.5f, 50f);
        }

        public XAnimationChannelState GetChannelState(string channelName)
        {
            return IsLoaded ? m_Driver.GetChannelState(channelName) : null;
        }

        public void Dispose()
        {
            DisposePreview();
        }

        private void DisposePreview()
        {
            if (m_Driver != null)
            {
                m_Driver.CueTriggered -= OnCueTriggered;
                m_Driver.Dispose();
                m_Driver = null;
            }

            if (m_PreviewUtility != null)
            {
                m_PreviewUtility.Cleanup();
                m_PreviewUtility = null;
            }

            if (m_Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(m_Instance);
                m_Instance = null;
            }

            if (m_RenderTexture != null)
            {
                m_RenderTexture.Release();
                UnityEngine.Object.DestroyImmediate(m_RenderTexture);
                m_RenderTexture = null;
            }

            m_Animator = null;
            m_CompiledAsset = null;
            m_CueLogs.Clear();
            m_RenderTextureSize = Vector2Int.zero;
        }

        private void EnsureLoaded()
        {
            if (!IsLoaded)
            {
                throw new XFrameworkException("XAnimation preview session is not loaded.");
            }
        }

        private void ConfigurePreviewCamera()
        {
            Camera camera = m_PreviewUtility.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.16f, 0.16f, 0.18f, 1f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.allowMSAA = false;
            camera.allowHDR = false;
        }

        private void ConfigurePreviewLights()
        {
            Light[] lights = m_PreviewUtility.lights;
            if (lights == null || lights.Length < 2)
            {
                return;
            }

            lights[0].intensity = 1.2f;
            lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            lights[1].intensity = 1f;
            lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 177f);
        }

        private void CacheInitialTransform()
        {
            m_InitialPosition = m_Instance.transform.position;
            m_InitialRotation = m_Instance.transform.rotation;
        }

        private void CacheInitialBounds()
        {
            Renderer[] renderers = m_Instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                m_InitialBounds = new Bounds(m_Instance.transform.position, Vector3.one);
            }
            else
            {
                m_InitialBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    m_InitialBounds.Encapsulate(renderers[i].bounds);
                }
            }

            m_CameraPivot = m_InitialBounds.center;
            float extentsMagnitude = Mathf.Max(m_InitialBounds.extents.magnitude, 0.5f);
            m_CameraDistance = extentsMagnitude * 2.8f;
        }

        private void EnsureRenderTexture(int width, int height)
        {
            if (m_RenderTexture != null && m_RenderTextureSize.x == width && m_RenderTextureSize.y == height)
            {
                return;
            }

            if (m_RenderTexture != null)
            {
                m_RenderTexture.Release();
                UnityEngine.Object.DestroyImmediate(m_RenderTexture);
            }

            RenderTextureDescriptor descriptor = new(width, height, RenderTextureFormat.ARGB32, 24)
            {
                msaaSamples = 1,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                useMipMap = false,
                autoGenerateMips = false,
            };
            m_RenderTexture = new RenderTexture(descriptor)
            {
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            m_RenderTexture.Create();
            m_RenderTextureSize = new Vector2Int(width, height);
        }

        private void UpdateCameraTransform()
        {
            Camera camera = m_PreviewUtility.camera;
            Quaternion rotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            Vector3 direction = rotation * Vector3.forward;
            camera.transform.position = m_CameraPivot - direction * m_CameraDistance;
            camera.transform.rotation = rotation;
        }

        private void ApplyHideFlags(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void SanitizePreviewInstance()
        {
            if (m_Instance == null)
            {
                return;
            }

            AudioSource[] audioSources = m_Instance.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
            {
                AudioSource audioSource = audioSources[i];
                audioSource.playOnAwake = false;
                audioSource.Stop();
                audioSource.enabled = false;
            }

            Behaviour[] behaviours = m_Instance.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == m_Animator)
                {
                    continue;
                }

                behaviour.enabled = false;
            }

            Rigidbody[] rigidbodies = m_Instance.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rigidbody = rigidbodies[i];
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = false;
            }

            Collider[] colliders = m_Instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            ParticleSystem[] particleSystems = m_Instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.gameObject.SetActive(false);
            }
        }

        private void OnCueTriggered(XAnimationCueEvent cueEvent)
        {
            string message = $"[{cueEvent.channelName}] {cueEvent.clipKey} -> {cueEvent.eventKey} @ {cueEvent.normalizedTime:0.00}";
            m_CueLogs.Insert(0, message);
            if (m_CueLogs.Count > MaxCueLogCount)
            {
                m_CueLogs.RemoveAt(m_CueLogs.Count - 1);
            }
        }
    }
}
#endif
