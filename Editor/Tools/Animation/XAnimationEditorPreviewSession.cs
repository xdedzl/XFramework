#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using XFramework.Animation;
using XFramework.Resource;

namespace XFramework.Editor
{
    internal sealed class XAnimationEditorPreviewSession : IDisposable
    {
        private const int MaxCueLogCount = 64;

        private readonly XAnimationAssetLoader m_AssetLoader = new(new XAnimationEditorAssetResolver());
        private readonly List<string> m_CueLogs = new();
        private readonly Dictionary<AnimationClip, PreviewRootMotionFallbackEvaluator> m_RootMotionFallbackEvaluatorByClip = new();

        private PreviewRenderUtility m_PreviewUtility;
        private RenderTexture m_RenderTexture;
        private GameObject m_Instance;
        private Animator m_Animator;
        private GameObject m_KeyLight;
        private GameObject m_FillLight;
        private GameObject m_RimLight;
        private XAnimationDriver m_Driver;
        private XAnimationCompiledAsset m_CompiledAsset;
        private Vector2Int m_RenderTextureSize;
        private readonly Dictionary<string, string> m_OriginalClipPathByKey = new(StringComparer.Ordinal);

        private Vector3 m_InitialPosition;
        private Quaternion m_InitialRotation;
        private Bounds m_InitialBounds;
        private Vector3 m_CameraPivot;
        private float m_CameraDistance;
        private float m_CameraYaw = 140f;
        private float m_CameraPitch = 18f;
        private Vector3 m_CameraPosition;
        private bool m_CameraInitialized;
        private bool m_RootMotionEnabled;
        private int m_ManualRootMotionPlaybackId;
        private float m_ManualRootMotionNormalizedTime;

        private bool m_GridVisible = true;
        private GameObject m_GridPlane;
        private Material m_GridMaterial;

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

            CacheOriginalClipPaths(assetPath);
            m_CompiledAsset = m_AssetLoader.Load(assetPath);

            m_PreviewUtility = new PreviewRenderUtility();
            ConfigurePreviewCamera();
            ConfigurePreviewLights();

            m_Instance = UnityEngine.Object.Instantiate(prefabAsset);
            m_Instance.transform.position = Vector3.zero;
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
            PrepareGrid();

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

            Transform animatorTransform = m_Animator.transform;
            Vector3 positionBeforeUpdate = animatorTransform.position;
            Quaternion rotationBeforeUpdate = animatorTransform.rotation;

            m_Driver.Update(clampedDeltaTime);
            ApplyPreviewRootMotionFallback(animatorTransform, positionBeforeUpdate, rotationBeforeUpdate);
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

        public void StopChannel(string channelName)
        {
            if (!IsLoaded)
            {
                return;
            }

            m_Driver.Stop(channelName);
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

        public void SetChannelLayerType(string channelName, XAnimationChannelLayerType layerType)
        {
            EnsureLoaded();
            XAnimationChannelConfig config = m_CompiledAsset.GetChannel(channelName).Config;
            config.layerType = layerType;
            if (layerType == XAnimationChannelLayerType.Additive)
            {
                config.canDriveRootMotion = false;
            }

            RebuildDriverAndSave();
        }

        public void SetChannelMaskPath(string channelName, string maskPath)
        {
            EnsureLoaded();
            m_CompiledAsset.GetChannel(channelName).Config.maskPath = maskPath ?? string.Empty;
            RebuildDriverAndSave();
        }

        public void SetChannelAllowInterrupt(string channelName, bool allowInterrupt)
        {
            EnsureLoaded();
            m_CompiledAsset.GetChannel(channelName).Config.allowInterrupt = allowInterrupt;
            SaveCompiledAsset();
        }

        public void SetChannelDefaultWeight(string channelName, float weight, bool save = true)
        {
            EnsureLoaded();
            float defaultWeight = Mathf.Max(0f, weight);
            m_CompiledAsset.GetChannel(channelName).Config.defaultWeight = defaultWeight;
            m_Driver.SetChannelWeight(channelName, defaultWeight);
            if (save)
            {
                SaveCompiledAsset();
            }
        }

        public void SetChannelFade(string channelName, float fadeIn, float fadeOut, bool save = true)
        {
            EnsureLoaded();
            XAnimationChannelConfig config = m_CompiledAsset.GetChannel(channelName).Config;
            config.defaultFadeIn = Mathf.Max(0f, fadeIn);
            config.defaultFadeOut = Mathf.Max(0f, fadeOut);
            if (save)
            {
                SaveCompiledAsset();
            }
        }

        public void SetChannelCanDriveRootMotion(string channelName, bool canDriveRootMotion)
        {
            EnsureLoaded();
            XAnimationChannelConfig config = m_CompiledAsset.GetChannel(channelName).Config;
            if (canDriveRootMotion && config.layerType == XAnimationChannelLayerType.Additive)
            {
                throw new XFrameworkException($"XAnimation channel '{channelName}' cannot drive root motion when layerType is Additive.");
            }

            config.canDriveRootMotion = canDriveRootMotion;
            RebuildDriverAndSave();
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

        private void ApplyPreviewRootMotionFallback(Transform animatorTransform, Vector3 positionBeforeUpdate, Quaternion rotationBeforeUpdate)
        {
            if (!m_RootMotionEnabled || !m_Animator.applyRootMotion)
            {
                m_ManualRootMotionPlaybackId = 0;
                return;
            }

            bool positionApplied = (animatorTransform.position - positionBeforeUpdate).sqrMagnitude > 0.0000001f;
            bool rotationApplied = Quaternion.Angle(animatorTransform.rotation, rotationBeforeUpdate) > 0.001f;
            if (positionApplied || rotationApplied)
            {
                m_ManualRootMotionPlaybackId = 0;
                return;
            }

            if (TryEvaluateManualRootMotionDelta(out Vector3 deltaPosition, out Quaternion deltaRotation))
            {
                animatorTransform.SetPositionAndRotation(
                    positionBeforeUpdate + deltaPosition,
                    rotationBeforeUpdate * deltaRotation);
                return;
            }

            animatorTransform.SetPositionAndRotation(
                positionBeforeUpdate + m_Animator.deltaPosition,
                rotationBeforeUpdate * m_Animator.deltaRotation);
        }

        private bool TryEvaluateManualRootMotionDelta(out Vector3 deltaPosition, out Quaternion deltaRotation)
        {
            deltaPosition = Vector3.zero;
            deltaRotation = Quaternion.identity;

            if (!TryGetRootMotionSourceState(out XAnimationCompiledClip compiledClip, out XAnimationChannelState state))
            {
                m_ManualRootMotionPlaybackId = 0;
                return false;
            }

            PreviewRootMotionFallbackEvaluator evaluator = GetRootMotionFallbackEvaluator(compiledClip.Clip);
            if (!evaluator.HasPosition && !evaluator.HasRotation)
            {
                return false;
            }

            float currentNormalizedTime = state.totalNormalizedTime;
            if (m_ManualRootMotionPlaybackId != state.playbackId)
            {
                m_ManualRootMotionPlaybackId = state.playbackId;
                m_ManualRootMotionNormalizedTime = currentNormalizedTime;
                return false;
            }

            float previousNormalizedTime = m_ManualRootMotionNormalizedTime;
            Vector3 previousPosition = evaluator.EvaluateAccumulatedPosition(previousNormalizedTime, state.isLooping);
            Vector3 currentPosition = evaluator.EvaluateAccumulatedPosition(currentNormalizedTime, state.isLooping);

            m_ManualRootMotionNormalizedTime = currentNormalizedTime;
            deltaPosition = currentPosition - previousPosition;
            if (state.isLooping)
            {
                Quaternion previousRotation = evaluator.EvaluateRotation(previousNormalizedTime, true);
                Quaternion currentRotation = evaluator.EvaluateRotation(currentNormalizedTime, true);
                deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
            }

            return true;
        }

        private bool TryGetRootMotionSourceState(out XAnimationCompiledClip compiledClip, out XAnimationChannelState state)
        {
            compiledClip = null;
            state = null;

            if (m_CompiledAsset == null)
            {
                return false;
            }

            XAnimationChannelState fallbackState = null;
            XAnimationCompiledClip fallbackClip = null;
            IReadOnlyList<XAnimationCompiledChannel> channels = m_CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = (XAnimationCompiledChannel)channels[i];
                XAnimationChannelState channelState = m_Driver.GetChannelState(channel.Name);
                if (channelState == null || channelState.weight <= 0.0001f)
                {
                    continue;
                }

                XAnimationCompiledClip channelClip = m_CompiledAsset.GetClip(channelState.clipKey);
                bool drivesRootMotion = channelClip.Config.rootMotionMode switch
                {
                    XAnimationClipRootMotionMode.ForceOn => true,
                    XAnimationClipRootMotionMode.ForceOff => false,
                    _ => channel.Config.canDriveRootMotion,
                };

                if (!channel.Config.canDriveRootMotion || !drivesRootMotion)
                {
                    continue;
                }

                PreviewRootMotionFallbackEvaluator evaluator = GetRootMotionFallbackEvaluator(channelClip.Clip);
                if (!evaluator.HasPosition && !evaluator.HasRotation)
                {
                    continue;
                }

                if (channel.Config.layerType == XAnimationChannelLayerType.Base)
                {
                    compiledClip = channelClip;
                    state = channelState;
                    return true;
                }

                fallbackState ??= channelState;
                fallbackClip ??= channelClip;
            }

            if (fallbackState == null)
            {
                return false;
            }

            compiledClip = fallbackClip;
            state = fallbackState;
            return true;
        }

        private PreviewRootMotionFallbackEvaluator GetRootMotionFallbackEvaluator(AnimationClip clip)
        {
            if (m_RootMotionFallbackEvaluatorByClip.TryGetValue(clip, out PreviewRootMotionFallbackEvaluator evaluator))
            {
                return evaluator;
            }

            evaluator = PreviewRootMotionFallbackEvaluator.Create(clip);
            m_RootMotionFallbackEvaluatorByClip[clip] = evaluator;
            return evaluator;
        }

        public void SetGridVisible(bool visible)
        {
            m_GridVisible = visible;
            if (m_GridPlane != null)
            {
                m_GridPlane.SetActive(visible);
            }
            else if (visible && IsLoaded)
            {
                PrepareGrid();
            }
        }

        public bool GetGridVisible()
        {
            return m_GridVisible;
        }

        public void ResetTransform()
        {
            if (m_Instance == null)
            {
                return;
            }

            m_Instance.transform.SetPositionAndRotation(m_InitialPosition, m_InitialRotation);
            CacheInitialBounds();
            UpdateGridTransform();
        }

        public void ResetCamera()
        {
            m_CameraYaw = 140f;
            m_CameraPitch = 18f;
            CacheInitialBounds();
            RecalculateCameraPosition();
        }

        public void Orbit(Vector2 delta)
        {
            m_CameraYaw += delta.x * 0.12f;
            m_CameraPitch = Mathf.Clamp(m_CameraPitch + delta.y * 0.08f, -80f, 80f);
        }

        public void Zoom(float delta)
        {
            Quaternion rotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            float distance = Mathf.Max(m_CameraDistance * 0.08f, 0.05f);
            m_CameraPosition -= rotation * Vector3.forward * delta * distance;
        }

        /// <summary>
        /// Move camera in its local space. x=right, y=up, z=forward.
        /// </summary>
        public void MoveCamera(Vector3 localDelta)
        {
            Quaternion rotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            m_CameraPosition += rotation * localDelta;
            // Keep pivot in sync so orbit still works around the look-at point
            m_CameraPivot = m_CameraPosition + rotation * Vector3.forward * m_CameraDistance;
        }

        private void RecalculateCameraPosition()
        {
            Quaternion rotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            m_CameraPosition = m_CameraPivot - rotation * Vector3.forward * m_CameraDistance;
        }

        public XAnimationChannelState GetChannelState(string channelName)
        {
            return IsLoaded ? m_Driver.GetChannelState(channelName) : null;
        }

        public void SetClipLoop(string clipKey, bool loop)
        {
            EnsureLoaded();
            m_CompiledAsset.GetClip(clipKey).Config.loop = loop;
            SaveCompiledAsset();
        }

        public void SetClipDefaultChannel(string clipKey, string channelName)
        {
            EnsureLoaded();
            m_CompiledAsset.GetChannel(channelName);
            m_CompiledAsset.GetClip(clipKey).Config.defaultChannel = channelName;
            SaveCompiledAsset();
        }

        public void MoveClip(string clipKey, string channelName, string insertBeforeClipKey = null)
        {
            EnsureLoaded();
            m_CompiledAsset.GetChannel(channelName);

            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationClipConfig[] clips = asset.clips ?? Array.Empty<XAnimationClipConfig>();
            XAnimationClipConfig movedClip = null;
            List<XAnimationClipConfig> orderedClips = new(clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip != null && string.Equals(clip.key, clipKey, StringComparison.Ordinal))
                {
                    movedClip = clip;
                    continue;
                }

                orderedClips.Add(clip);
            }

            if (movedClip == null)
            {
                throw new XFrameworkException($"XAnimation clip '{clipKey}' does not exist.");
            }

            movedClip.defaultChannel = channelName;
            int insertIndex = orderedClips.Count;
            if (!string.IsNullOrWhiteSpace(insertBeforeClipKey))
            {
                for (int i = 0; i < orderedClips.Count; i++)
                {
                    XAnimationClipConfig clip = orderedClips[i];
                    if (clip != null && string.Equals(clip.key, insertBeforeClipKey, StringComparison.Ordinal))
                    {
                        insertIndex = i;
                        break;
                    }
                }
            }

            orderedClips.Insert(insertIndex, movedClip);
            asset.clips = orderedClips.ToArray();
            RebuildDriverAndSave();
        }

        public void SetClipFade(string clipKey, float fadeIn, float fadeOut, bool save = true)
        {
            EnsureLoaded();
            XAnimationClipConfig config = m_CompiledAsset.GetClip(clipKey).Config;
            config.defaultFadeIn = Mathf.Max(0f, fadeIn);
            config.defaultFadeOut = Mathf.Max(0f, fadeOut);
            if (save)
            {
                SaveCompiledAsset();
            }
        }

        public void SetClipRootMotionMode(string clipKey, XAnimationClipRootMotionMode rootMotionMode)
        {
            EnsureLoaded();
            m_CompiledAsset.GetClip(clipKey).Config.rootMotionMode = rootMotionMode;
            SaveCompiledAsset();
        }

        private void SaveCompiledAsset()
        {
            if (m_CompiledAsset?.Asset == null)
            {
                return;
            }

            m_CompiledAsset.Asset.SaveAsset();
        }

        public void SaveCurrentAsset()
        {
            EnsureLoaded();
            SaveCompiledAsset();
        }

        private void RebuildDriverAndSave()
        {
            m_CompiledAsset = m_AssetLoader.Compile(m_CompiledAsset.Asset);
            if (m_Driver != null && m_Animator != null)
            {
                m_Driver.CueTriggered -= OnCueTriggered;
                m_Driver.Dispose();
                m_Driver = new XAnimationDriver();
                m_Driver.Initialize(m_CompiledAsset, m_Animator);
                m_Driver.CueTriggered += OnCueTriggered;
                m_Driver.SetRootMotionEnabled(m_RootMotionEnabled);
            }

            SaveCompiledAsset();
        }

        public string GetOriginalClipPath(string clipKey)
        {
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                return string.Empty;
            }

            return m_OriginalClipPathByKey.TryGetValue(clipKey, out string clipPath) ? clipPath : string.Empty;
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

            DestroyGrid();
            DestroyLight(ref m_KeyLight);
            DestroyLight(ref m_FillLight);
            DestroyLight(ref m_RimLight);

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
            m_OriginalClipPathByKey.Clear();
            m_RootMotionFallbackEvaluatorByClip.Clear();
            m_RenderTextureSize = Vector2Int.zero;
            m_ManualRootMotionPlaybackId = 0;
            m_ManualRootMotionNormalizedTime = 0f;
        }

        private void CacheOriginalClipPaths(string assetPath)
        {
            m_OriginalClipPathByKey.Clear();

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (textAsset == null)
            {
                return;
            }

            XAnimationOverrideAsset overrideAsset = textAsset.ToXTextAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                TextAsset baseTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(overrideAsset.baseAssetPath);
                if (baseTextAsset == null)
                {
                    return;
                }

                CacheOriginalClipPaths(baseTextAsset.ToXTextAsset<XAnimationAsset>());
                return;
            }

            CacheOriginalClipPaths(textAsset.ToXTextAsset<XAnimationAsset>());
        }

        private void CacheOriginalClipPaths(XAnimationAsset asset)
        {
            if (asset?.clips == null)
            {
                return;
            }

            for (int i = 0; i < asset.clips.Length; i++)
            {
                XAnimationClipConfig clip = asset.clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.key))
                {
                    continue;
                }

                m_OriginalClipPathByKey[clip.key] = clip.clipPath;
            }
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
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.allowMSAA = true;
            camera.allowHDR = true;

            // Use the editor default skybox, matching prefab preview appearance
            Material skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (skybox != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                Skybox skyboxComponent = camera.gameObject.GetComponent<Skybox>();
                if (skyboxComponent == null)
                {
                    skyboxComponent = camera.gameObject.AddComponent<Skybox>();
                }
                skyboxComponent.material = skybox;
            }
            else
            {
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(0.22f, 0.22f, 0.24f, 1f);
            }
        }

        private void ConfigurePreviewLights()
        {
            // Disable built-in PreviewRenderUtility lights (not used by SRP)
            Light[] builtinLights = m_PreviewUtility.lights;
            if (builtinLights != null)
            {
                for (int i = 0; i < builtinLights.Length; i++)
                {
                    if (builtinLights[i] != null)
                    {
                        builtinLights[i].intensity = 0f;
                        builtinLights[i].enabled = false;
                    }
                }
            }

            // Create real directional lights as GameObjects so URP/SRP renders them
            m_KeyLight = CreateDirectionalLight("__PreviewKeyLight__",
                Quaternion.Euler(50f, 120f, 0f), new Color(1f, 0.97f, 0.92f), 1.5f);
            m_PreviewUtility.AddSingleGO(m_KeyLight);

            m_FillLight = CreateDirectionalLight("__PreviewFillLight__",
                Quaternion.Euler(340f, 300f, 0f), new Color(0.82f, 0.87f, 1f), 0.8f);
            m_PreviewUtility.AddSingleGO(m_FillLight);

            m_RimLight = CreateDirectionalLight("__PreviewRimLight__",
                Quaternion.Euler(10f, 220f, 0f), new Color(0.9f, 0.9f, 0.95f), 0.5f);
            m_PreviewUtility.AddSingleGO(m_RimLight);

            // Set ambient for the preview scene
            m_PreviewUtility.ambientColor = new Color(0.45f, 0.45f, 0.50f, 1f);
        }

        private static GameObject CreateDirectionalLight(string name, Quaternion rotation, Color color, float intensity)
        {
            GameObject go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.rotation = rotation;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return go;
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
            if (!m_CameraInitialized)
            {
                RecalculateCameraPosition();
                m_CameraInitialized = true;
            }

            Camera camera = m_PreviewUtility.camera;
            Quaternion rotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            camera.transform.position = m_CameraPosition;
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

        private void PrepareGrid()
        {
            DestroyGrid();

            float modelExtent = Mathf.Max(m_InitialBounds.extents.magnitude, 0.5f);
            float gridHalf = Mathf.Max(10f, Mathf.Ceil(modelExtent * 6f));
            float cellSize = Mathf.Max(0.25f, Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(modelExtent * 0.5f))));
            int cellCount = Mathf.RoundToInt(gridHalf / cellSize);
            gridHalf = cellCount * cellSize;
            float gridSize = gridHalf * 2f;

            // Create material from URP grid shader
            Shader shader = Shader.Find("Hidden/XFramework/AnimationPreviewGrid");
            if (shader == null)
            {
                Debug.LogWarning("XAnimation preview grid shader not found (Hidden/XFramework/AnimationPreviewGrid).");
                return;
            }

            m_GridMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            m_GridMaterial.SetColor("_BGColor", new Color(0.039216f, 0.101961f, 0.180392f, 1f));
            m_GridMaterial.SetColor("_GridColor", new Color(0.301961f, 0.670588f, 0.968627f, 1f));
            m_GridMaterial.SetColor("_MajorGridColor", new Color(0.301961f, 0.670588f, 0.968627f, 1f));
            m_GridMaterial.SetColor("_CenterLineColor", new Color(0.518077f, 0.684173f, 0.974843f, 1f));
            m_GridMaterial.SetFloat("_GridWidth", 0.01f);
            m_GridMaterial.SetFloat("_MajorGridWidth", 0.01f);
            m_GridMaterial.SetFloat("_CenterLineWidth", 0.1f);
            m_GridMaterial.SetFloat("_GridSpacing", cellSize);
            m_GridMaterial.SetFloat("_MajorGridInterval", 5f);
            m_GridMaterial.SetFloat("_GridSize", gridSize);

            // Create a Plane GameObject as the grid surface
            // Unity built-in Plane is 10x10 units, so scale to match gridSize
            m_GridPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            m_GridPlane.name = "__PreviewGridPlane__";
            m_GridPlane.hideFlags = HideFlags.HideAndDontSave;

            // Remove collider (not needed in preview)
            Collider collider = m_GridPlane.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = m_GridPlane.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = m_GridMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            float planeScale = gridSize / 10f; // built-in Plane is 10 units wide
            m_GridPlane.transform.localScale = new Vector3(planeScale, 1f, planeScale);
            UpdateGridTransform();

            m_GridPlane.SetActive(m_GridVisible);
            m_PreviewUtility.AddSingleGO(m_GridPlane);
        }

        private void DestroyGrid()
        {
            if (m_GridPlane != null)
            {
                UnityEngine.Object.DestroyImmediate(m_GridPlane);
                m_GridPlane = null;
            }

            if (m_GridMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(m_GridMaterial);
                m_GridMaterial = null;
            }
        }

        private void UpdateGridTransform()
        {
            if (m_GridPlane == null)
            {
                return;
            }

            m_GridPlane.transform.position = Vector3.zero;
        }

        private static void DestroyLight(ref GameObject lightGo)
        {
            if (lightGo != null)
            {
                UnityEngine.Object.DestroyImmediate(lightGo);
                lightGo = null;
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

        private sealed class PreviewRootMotionFallbackEvaluator
        {
            private readonly AnimationClip m_Clip;
            private AnimationCurve m_RootTX;
            private AnimationCurve m_RootTY;
            private AnimationCurve m_RootTZ;
            private AnimationCurve m_RootQX;
            private AnimationCurve m_RootQY;
            private AnimationCurve m_RootQZ;
            private AnimationCurve m_RootQW;

            public bool HasPosition => m_RootTX != null || m_RootTY != null || m_RootTZ != null;
            public bool HasRotation => m_RootQX != null || m_RootQY != null || m_RootQZ != null || m_RootQW != null;

            private PreviewRootMotionFallbackEvaluator(AnimationClip clip)
            {
                m_Clip = clip;
            }

            public static PreviewRootMotionFallbackEvaluator Create(AnimationClip clip)
            {
                PreviewRootMotionFallbackEvaluator evaluator = new(clip);
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                for (int i = 0; i < bindings.Length; i++)
                {
                    EditorCurveBinding binding = bindings[i];
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    switch (binding.propertyName)
                    {
                        case "RootT.x":
                            evaluator.m_RootTX = curve;
                            break;
                        case "RootT.y":
                            evaluator.m_RootTY = curve;
                            break;
                        case "RootT.z":
                            evaluator.m_RootTZ = curve;
                            break;
                        case "RootQ.x":
                            evaluator.m_RootQX = curve;
                            break;
                        case "RootQ.y":
                            evaluator.m_RootQY = curve;
                            break;
                        case "RootQ.z":
                            evaluator.m_RootQZ = curve;
                            break;
                        case "RootQ.w":
                            evaluator.m_RootQW = curve;
                            break;
                    }
                }

                return evaluator;
            }

            public Vector3 EvaluateAccumulatedPosition(float totalNormalizedTime, bool isLooping)
            {
                if (!isLooping)
                {
                    return EvaluatePosition(Mathf.Clamp01(totalNormalizedTime));
                }

                int loopCount = Mathf.FloorToInt(totalNormalizedTime);
                float normalizedTime = Mathf.Repeat(totalNormalizedTime, 1f);
                Vector3 loopDelta = EvaluatePosition(1f) - EvaluatePosition(0f);
                return loopDelta * loopCount + EvaluatePosition(normalizedTime);
            }

            public Vector3 EvaluatePosition(float normalizedTime)
            {
                float time = Mathf.Clamp01(normalizedTime) * Mathf.Max(m_Clip.length, 0.0001f);
                return new Vector3(
                    m_RootTX != null ? m_RootTX.Evaluate(time) : 0f,
                    m_RootTY != null ? m_RootTY.Evaluate(time) : 0f,
                    m_RootTZ != null ? m_RootTZ.Evaluate(time) : 0f);
            }

            public Quaternion EvaluateRotation(float totalNormalizedTime, bool isLooping)
            {
                float normalizedTime = isLooping ? Mathf.Repeat(totalNormalizedTime, 1f) : Mathf.Clamp01(totalNormalizedTime);
                float time = normalizedTime * Mathf.Max(m_Clip.length, 0.0001f);
                Quaternion rotation = new(
                    m_RootQX != null ? m_RootQX.Evaluate(time) : 0f,
                    m_RootQY != null ? m_RootQY.Evaluate(time) : 0f,
                    m_RootQZ != null ? m_RootQZ.Evaluate(time) : 0f,
                    m_RootQW != null ? m_RootQW.Evaluate(time) : 1f);

                return Normalize(rotation);
            }

            private static Quaternion Normalize(Quaternion rotation)
            {
                float magnitude = Mathf.Sqrt(
                    rotation.x * rotation.x +
                    rotation.y * rotation.y +
                    rotation.z * rotation.z +
                    rotation.w * rotation.w);

                if (magnitude <= 0.0001f)
                {
                    return Quaternion.identity;
                }

                return new Quaternion(
                    rotation.x / magnitude,
                    rotation.y / magnitude,
                    rotation.z / magnitude,
                    rotation.w / magnitude);
            }
        }
    }

}
#endif
