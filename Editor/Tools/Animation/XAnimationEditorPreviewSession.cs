#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const float CloseGridSpacing = 1f;
        private const float FarGridSpacing = 10f;
        private const float SwitchToFarGridCellPixels = 8f;
        private const float SwitchToCloseGridCellPixels = 12f;
        private const float MinGridHalfSize = 250f;
        private const float PreviewFarClipPlane = 500f;

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
        private string m_AssetPath;
        private bool m_IsOverrideAsset;
        private XAnimationOverrideAsset m_OverrideAsset;

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
        private float m_GridSpacing = CloseGridSpacing;

        public IReadOnlyList<string> CueLogs => m_CueLogs;
        public XAnimationCompiledAsset CompiledAsset => m_CompiledAsset;
        public Texture PreviewTexture => m_RenderTexture;
        public bool IsLoaded => m_Driver != null && m_Animator != null;
        public bool IsOverrideAsset => m_IsOverrideAsset;

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

            m_AssetPath = assetPath;
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

        public void Play(XAnimationPlayCommand command)
        {
            EnsureLoaded();
            m_Driver.Play(command);
        }

        public void PlayClip(
            string clipKey,
            string channelName,
            XAnimationTransitionOptions transition = default,
            XAnimationPlaybackOptions playback = default)
        {
            transition ??= new XAnimationTransitionOptions();
            playback ??= new XAnimationPlaybackOptions();
            playback.speed = Mathf.Approximately(playback.speed, 0f) ? 1f : playback.speed;
            if (playback.weight <= 0f)
            {
                playback.weight = 1f;
            }

            Play(new XAnimationPlayCommand
            {
                target = new XAnimationPlayTarget
                {
                    clipKey = clipKey,
                    channelName = channelName,
                },
                transition = transition,
                playback = playback,
            });
        }

        public void PlayState(
            string stateKey,
            XAnimationTransitionOptions transition = default,
            XAnimationPlaybackOptions playback = default)
        {
            transition ??= new XAnimationTransitionOptions();
            playback ??= new XAnimationPlaybackOptions();
            playback.speed = Mathf.Approximately(playback.speed, 0f) ? 1f : playback.speed;
            if (playback.weight <= 0f)
            {
                playback.weight = 1f;
            }

            Play(new XAnimationPlayCommand
            {
                target = new XAnimationPlayTarget
                {
                    stateKey = stateKey,
                },
                transition = transition,
                playback = playback,
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

        public void StopChannel(string channelName, float fadeOut = default)
        {
            if (!IsLoaded)
            {
                return;
            }

            m_Driver.Stop(channelName, fadeOut);
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

        public void SetPreviewParameter(string key, float value)
        {
            EnsureLoaded();
            m_Driver.SetParameter(key, value);
        }

        public void SetPreviewParameter(string key, bool value)
        {
            EnsureLoaded();
            m_Driver.SetParameter(key, value);
        }

        public string AddParameter()
        {
            EnsureBaseAssetEditable();
            XAnimationAsset asset = m_CompiledAsset.Asset;
            string parameterName = CreateUniqueParameterName("NewParameter");
            List<XAnimationParameterConfig> parameters = new(asset.parameters ?? Array.Empty<XAnimationParameterConfig>())
            {
                new XAnimationParameterConfig
                {
                    name = parameterName,
                    type = XAnimationParameterType.Float,
                    defaultValue = 0f,
                }
            };
            asset.parameters = parameters.ToArray();
            RebuildDriverAndSave();
            return parameterName;
        }

        public void DeleteParameter(string parameterName)
        {
            EnsureBaseAssetEditable();
            parameterName = parameterName?.Trim();
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return;
            }

            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationParameterConfig[] parameters = asset.parameters ?? Array.Empty<XAnimationParameterConfig>();
            bool hasReference = HasStateParameterReference(asset, parameterName);
            bool removed = false;
            List<XAnimationParameterConfig> orderedParameters = new(parameters.Length);
            for (int i = 0; i < parameters.Length; i++)
            {
                XAnimationParameterConfig parameter = parameters[i];
                if (parameter != null && string.Equals(parameter.name, parameterName, StringComparison.Ordinal))
                {
                    removed = true;
                    continue;
                }

                orderedParameters.Add(parameter);
            }

            if (!removed)
            {
                return;
            }

            asset.parameters = orderedParameters.ToArray();
            string fallbackParameterName = hasReference ? EnsureFloatParameter() : null;
            RemoveStateParameterReferences(asset, parameterName, fallbackParameterName);
            RebuildDriverAndSave();
        }

        public void RenameParameter(string oldName, string newName)
        {
            EnsureLoaded();
            newName = newName?.Trim();
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new XFrameworkException("XAnimation parameter name cannot be empty.");
            }

            if (m_CompiledAsset.TryGetParameterIndex(newName, out _))
            {
                throw new XFrameworkException($"XAnimation parameter '{newName}' is duplicated.");
            }

            XAnimationParameterConfig config = m_CompiledAsset.GetParameter(oldName).Config;
            config.name = newName;
            RenameStateParameterReferences(m_CompiledAsset.Asset, oldName, newName);
            RebuildDriverAndSave();
        }

        public void SetParameterType(string parameterName, XAnimationParameterType type)
        {
            EnsureLoaded();
            XAnimationParameterConfig config = m_CompiledAsset.GetParameter(parameterName).Config;
            config.type = type;
            config.defaultValue = type switch
            {
                XAnimationParameterType.Float => ConvertParameterDefaultToFloat(config.defaultValue),
                XAnimationParameterType.Bool => ConvertParameterDefaultToBool(config.defaultValue),
                XAnimationParameterType.Trigger => null,
                _ => config.defaultValue,
            };

            if (type != XAnimationParameterType.Float)
            {
                XAnimationAsset asset = m_CompiledAsset.Asset;
                string fallbackParameterName = HasStateParameterReference(asset, parameterName)
                    ? EnsureFloatParameter()
                    : null;
                RemoveStateParameterReferences(asset, parameterName, fallbackParameterName);
            }

            RebuildDriverAndSave();
        }

        public void SetParameterDefaultValue(string parameterName, object defaultValue)
        {
            EnsureLoaded();
            XAnimationParameterConfig config = m_CompiledAsset.GetParameter(parameterName).Config;
            config.defaultValue = config.type switch
            {
                XAnimationParameterType.Float => Convert.ToSingle(defaultValue),
                XAnimationParameterType.Bool => Convert.ToBoolean(defaultValue),
                XAnimationParameterType.Trigger => null,
                _ => defaultValue,
            };
            RebuildDriverAndSave();
        }

        public void RenameChannel(string oldName, string newName)
        {
            EnsureLoaded();
            newName = newName?.Trim();
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                throw new XFrameworkException("XAnimation channel name cannot be empty.");
            }

            if (m_CompiledAsset.TryGetChannelIndex(newName, out _))
            {
                throw new XFrameworkException($"XAnimation channel '{newName}' is duplicated.");
            }

            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationChannelConfig channel = m_CompiledAsset.GetChannel(oldName).Config;
            channel.name = newName;

            if (asset.graph?.states != null)
            {
                for (int i = 0; i < asset.graph.states.Length; i++)
                {
                    XAnimationGraphStateConfig state = asset.graph.states[i];
                    if (state != null && string.Equals(state.channelName, oldName, StringComparison.Ordinal))
                    {
                        state.channelName = newName;
                    }
                }
            }

            RenameStateChannelReferences(asset, oldName, newName);

            RebuildDriverAndSave();
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
                bool drivesRootMotion = ResolvePreviewRootMotion(channel, channelState);

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

        private bool ResolvePreviewRootMotion(XAnimationCompiledChannel channel, XAnimationChannelState channelState)
        {
            if (!string.IsNullOrWhiteSpace(channelState.stateKey) &&
                m_CompiledAsset.TryGetStateIndex(channelState.stateKey, out int stateIndex))
            {
                XAnimationCompiledState state = m_CompiledAsset.States[stateIndex];
                return state.Config.rootMotionMode switch
                {
                    XAnimationClipRootMotionMode.ForceOn => true,
                    XAnimationClipRootMotionMode.ForceOff => false,
                    _ => channel.Config.canDriveRootMotion,
                };
            }

            return channel.Config.canDriveRootMotion;
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
            ResetManualRootMotionPreviewState();
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
            m_CameraDistance = Mathf.Max(Vector3.Distance(m_CameraPosition, m_CameraPivot), 0.05f);
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

        public string AddChannel()
        {
            EnsureBaseAssetEditable();
            XAnimationAsset asset = m_CompiledAsset.Asset;
            string channelName = CreateUniqueChannelName("NewChannel");
            List<XAnimationChannelConfig> channels = new(asset.channels ?? Array.Empty<XAnimationChannelConfig>());
            channels.Add(new XAnimationChannelConfig
            {
                name = channelName,
                layerType = XAnimationChannelLayerType.Override,
                defaultWeight = 1f,
                allowInterrupt = true,
                defaultFadeIn = 0.15f,
                defaultFadeOut = 0.15f,
            });
            asset.channels = channels.ToArray();
            RebuildDriverAndSave();
            return channelName;
        }

        public void DeleteChannel(string channelName)
        {
            EnsureBaseAssetEditable();
            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationChannelConfig[] channels = asset.channels ?? Array.Empty<XAnimationChannelConfig>();
            if (channels.Length <= 1)
            {
                throw new XFrameworkException("XAnimation asset must contain at least one channel.");
            }

            XAnimationChannelConfig channel = m_CompiledAsset.GetChannel(channelName).Config;
            bool hasOtherBaseChannel = false;
            for (int i = 0; i < channels.Length; i++)
            {
                XAnimationChannelConfig item = channels[i];
                if (!ReferenceEquals(item, channel) && item != null && item.layerType == XAnimationChannelLayerType.Base)
                {
                    hasOtherBaseChannel = true;
                    break;
                }
            }

            if (channel.layerType == XAnimationChannelLayerType.Base && !hasOtherBaseChannel)
            {
                throw new XFrameworkException("XAnimation asset must contain at least one Base channel.");
            }

            List<XAnimationChannelConfig> orderedChannels = new(channels.Length - 1);
            for (int i = 0; i < channels.Length; i++)
            {
                if (!ReferenceEquals(channels[i], channel))
                {
                    orderedChannels.Add(channels[i]);
                }
            }

            asset.channels = orderedChannels.ToArray();
            RemoveStatesInChannel(asset, channelName);

            RebuildDriverAndSave();
        }

        public string AddClip()
        {
            EnsureBaseAssetEditable();
            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationClipConfig[] clips = asset.clips ?? Array.Empty<XAnimationClipConfig>();
            string clipPath = FindTemplateClipPath(clips);
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                throw new XFrameworkException("Cannot add clip because no template AnimationClip exists.");
            }

            string clipKey = CreateUniqueClipKey("NewClip");
            List<XAnimationClipConfig> orderedClips = new(clips)
            {
                new XAnimationClipConfig
                {
                    key = clipKey,
                    clipPath = clipPath,
                }
            };
            asset.clips = orderedClips.ToArray();
            m_OriginalClipPathByKey[clipKey] = clipPath;
            RebuildDriverAndSave();
            return clipKey;
        }

        public void DeleteClip(string clipKey)
        {
            EnsureBaseAssetEditable();
            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationClipConfig[] clips = asset.clips ?? Array.Empty<XAnimationClipConfig>();
            if (clips.Length <= 1)
            {
                throw new XFrameworkException("XAnimation asset must contain at least one clip.");
            }

            m_CompiledAsset.GetClip(clipKey);
            List<XAnimationClipConfig> orderedClips = new(clips.Length - 1);
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip != null && string.Equals(clip.key, clipKey, StringComparison.Ordinal))
                {
                    continue;
                }

                orderedClips.Add(clip);
            }

            asset.clips = orderedClips.ToArray();
            RemoveCueReferences(asset, new HashSet<string>(StringComparer.Ordinal) { clipKey });
            RemoveStateReferences(asset, new HashSet<string>(StringComparer.Ordinal) { clipKey });
            m_OriginalClipPathByKey.Remove(clipKey);
            RebuildDriverAndSave();
        }

        public void RenameClip(string oldKey, string newKey)
        {
            EnsureLoaded();
            newKey = newKey?.Trim();
            if (string.Equals(oldKey, newKey, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(newKey))
            {
                throw new XFrameworkException("XAnimation clip key cannot be empty.");
            }

            if (m_CompiledAsset.TryGetClipIndex(newKey, out _))
            {
                throw new XFrameworkException($"XAnimation clip '{newKey}' is duplicated.");
            }

            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationClipConfig clipConfig = m_CompiledAsset.GetClip(oldKey).Config;
            clipConfig.key = newKey;

            if (asset.cues != null)
            {
                for (int i = 0; i < asset.cues.Length; i++)
                {
                    XAnimationCueConfig cue = asset.cues[i];
                    if (cue != null && string.Equals(cue.clipKey, oldKey, StringComparison.Ordinal))
                    {
                        cue.clipKey = newKey;
                    }
                }
            }

            if (asset.graph?.states != null)
            {
                for (int i = 0; i < asset.graph.states.Length; i++)
                {
                    XAnimationGraphStateConfig state = asset.graph.states[i];
                    if (state != null && string.Equals(state.clipKey, oldKey, StringComparison.Ordinal))
                    {
                        state.clipKey = newKey;
                    }
                }
            }

            RenameStateClipReferences(asset, oldKey, newKey);

            if (m_OriginalClipPathByKey.Remove(oldKey, out string originalClipPath))
            {
                m_OriginalClipPathByKey[newKey] = originalClipPath;
            }

            RebuildDriverAndSave();
        }

        public void SetClipPath(string clipKey, string clipPath)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(clipPath))
            {
                throw new XFrameworkException("XAnimation clipPath cannot be empty.");
            }

            m_CompiledAsset.GetClip(clipKey);
            if (m_IsOverrideAsset)
            {
                SetOverrideClipPath(clipKey, clipPath);
                return;
            }

            XAnimationClipConfig config = m_CompiledAsset.GetClip(clipKey).Config;
            if (string.Equals(config.clipPath, clipPath, StringComparison.Ordinal))
            {
                return;
            }

            config.clipPath = clipPath;
            m_OriginalClipPathByKey[clipKey] = clipPath;
            RebuildDriverAndSave();
        }

        public string AddState(string channelName)
        {
            EnsureBaseAssetEditable();
            m_CompiledAsset.GetChannel(channelName);
            XAnimationAsset asset = m_CompiledAsset.Asset;
            string clipKey = FindTemplateClipKey(asset.clips ?? Array.Empty<XAnimationClipConfig>());
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                throw new XFrameworkException("Cannot add state because no template clip exists.");
            }

            string stateKey = CreateUniqueStateKey("NewState");
            List<XAnimationStateConfig> states = new(asset.states ?? Array.Empty<XAnimationStateConfig>())
            {
                new XAnimationStateConfig
                {
                    key = stateKey,
                    stateType = XAnimationStateType.Single,
                    clipKey = clipKey,
                    channelName = channelName,
                    fadeIn = 0.15f,
                    fadeOut = 0.15f,
                    speed = 1f,
                    loop = true,
                    rootMotionMode = XAnimationClipRootMotionMode.Inherit,
                    parameterName = string.Empty,
                    samples = Array.Empty<XAnimationBlend1DSampleConfig>(),
                }
            };
            asset.states = states.ToArray();
            RebuildDriverAndSave();
            return stateKey;
        }

        public void DeleteState(string stateKey)
        {
            EnsureBaseAssetEditable();
            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationStateConfig[] states = asset.states ?? Array.Empty<XAnimationStateConfig>();
            if (states.Length <= 1)
            {
                throw new XFrameworkException("XAnimation asset must contain at least one state.");
            }

            m_CompiledAsset.GetState(stateKey);
            List<XAnimationStateConfig> orderedStates = new(states.Length - 1);
            for (int i = 0; i < states.Length; i++)
            {
                XAnimationStateConfig state = states[i];
                if (state != null && string.Equals(state.key, stateKey, StringComparison.Ordinal))
                {
                    continue;
                }

                orderedStates.Add(state);
            }

            asset.states = orderedStates.ToArray();
            RebuildDriverAndSave();
        }

        public void RenameState(string oldKey, string newKey)
        {
            EnsureLoaded();
            newKey = newKey?.Trim();
            if (string.Equals(oldKey, newKey, StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(newKey))
            {
                throw new XFrameworkException("XAnimation state key cannot be empty.");
            }

            if (m_CompiledAsset.TryGetStateIndex(newKey, out _))
            {
                throw new XFrameworkException($"XAnimation state '{newKey}' is duplicated.");
            }

            m_CompiledAsset.GetState(oldKey).Config.key = newKey;
            RebuildDriverAndSave();
        }

        public void MoveState(string stateKey, string channelName, string insertBeforeStateKey = null)
        {
            EnsureLoaded();
            m_CompiledAsset.GetChannel(channelName);

            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationStateConfig[] states = asset.states ?? Array.Empty<XAnimationStateConfig>();
            XAnimationStateConfig movedState = null;
            List<XAnimationStateConfig> orderedStates = new(states.Length);
            for (int i = 0; i < states.Length; i++)
            {
                XAnimationStateConfig state = states[i];
                if (state != null && string.Equals(state.key, stateKey, StringComparison.Ordinal))
                {
                    movedState = state;
                    continue;
                }

                orderedStates.Add(state);
            }

            if (movedState == null)
            {
                throw new XFrameworkException($"XAnimation state '{stateKey}' does not exist.");
            }

            movedState.channelName = channelName;
            int insertIndex = orderedStates.Count;
            if (!string.IsNullOrWhiteSpace(insertBeforeStateKey))
            {
                for (int i = 0; i < orderedStates.Count; i++)
                {
                    XAnimationStateConfig state = orderedStates[i];
                    if (state != null && string.Equals(state.key, insertBeforeStateKey, StringComparison.Ordinal))
                    {
                        insertIndex = i;
                        break;
                    }
                }
            }

            orderedStates.Insert(insertIndex, movedState);
            asset.states = orderedStates.ToArray();
            RebuildDriverAndSave();
        }

        public void SetStateType(string stateKey, XAnimationStateType stateType)
        {
            EnsureLoaded();
            XAnimationStateConfig config = m_CompiledAsset.GetState(stateKey).Config;
            if (config.stateType == stateType)
            {
                return;
            }

            config.stateType = stateType;
            if (stateType == XAnimationStateType.Single)
            {
                config.clipKey = string.IsNullOrWhiteSpace(config.clipKey)
                    ? FindTemplateClipKey(m_CompiledAsset.Asset.clips ?? Array.Empty<XAnimationClipConfig>())
                    : config.clipKey;
                config.parameterName = string.Empty;
                config.samples = Array.Empty<XAnimationBlend1DSampleConfig>();
            }
            else
            {
                config.clipKey = string.Empty;
                config.parameterName = EnsureFloatParameter();
                config.samples = CreateDefaultBlendSamples(config.channelName);
            }

            RebuildDriverAndSave();
        }

        public void SetStateChannel(string stateKey, string channelName)
        {
            EnsureLoaded();
            m_CompiledAsset.GetChannel(channelName);
            m_CompiledAsset.GetState(stateKey).Config.channelName = channelName;
            RebuildDriverAndSave();
        }

        public void SetStateClipKey(string stateKey, string clipKey)
        {
            EnsureLoaded();
            clipKey = clipKey?.Trim();
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                throw new XFrameworkException("XAnimation state clipKey cannot be empty.");
            }

            m_CompiledAsset.GetClip(clipKey);
            XAnimationStateConfig config = m_CompiledAsset.GetState(stateKey).Config;
            config.clipKey = clipKey;
            RebuildDriverAndSave();
        }

        public void SetStateBlendParameter(string stateKey, string parameterName)
        {
            EnsureLoaded();
            parameterName = parameterName?.Trim();
            XAnimationCompiledParameter parameter = m_CompiledAsset.GetParameter(parameterName);
            if (parameter.Type != XAnimationParameterType.Float)
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterName}' must be Float for Blend1D.");
            }

            m_CompiledAsset.GetState(stateKey).Config.parameterName = parameterName;
            RebuildDriverAndSave();
        }

        public void SetStateLoop(string stateKey, bool loop, bool save = true)
        {
            EnsureLoaded();
            m_CompiledAsset.GetState(stateKey).Config.loop = loop;
            if (save)
            {
                SaveCompiledAsset();
            }
        }

        public void SetStateFade(string stateKey, float fadeIn, float fadeOut, bool save = true)
        {
            EnsureLoaded();
            XAnimationStateConfig config = m_CompiledAsset.GetState(stateKey).Config;
            config.fadeIn = Mathf.Max(0f, fadeIn);
            config.fadeOut = Mathf.Max(0f, fadeOut);
            if (save)
            {
                SaveCompiledAsset();
            }
        }

        public void SetStateSpeed(string stateKey, float speed, bool save = true)
        {
            EnsureLoaded();
            m_CompiledAsset.GetState(stateKey).Config.speed = Mathf.Approximately(speed, 0f) ? 1f : speed;
            if (save)
            {
                SaveCompiledAsset();
            }
        }

        public void SetStateRootMotionMode(string stateKey, XAnimationClipRootMotionMode rootMotionMode)
        {
            EnsureLoaded();
            m_CompiledAsset.GetState(stateKey).Config.rootMotionMode = rootMotionMode;
            RebuildDriverAndSave();
        }

        public void AddBlendSample(string stateKey)
        {
            EnsureLoaded();
            XAnimationStateConfig config = m_CompiledAsset.GetState(stateKey).Config;
            if (config.stateType != XAnimationStateType.Blend1D)
            {
                throw new XFrameworkException($"XAnimation state '{stateKey}' is not Blend1D.");
            }

            List<XAnimationBlend1DSampleConfig> samples = new(config.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>());
            string clipKey = FindTemplateClipKey(m_CompiledAsset.Asset.clips ?? Array.Empty<XAnimationClipConfig>());
            float threshold = samples.Count == 0 ? 0f : samples[^1].threshold + 1f;
            samples.Add(new XAnimationBlend1DSampleConfig
            {
                clipKey = clipKey,
                threshold = threshold,
            });
            config.samples = samples.ToArray();
            RebuildDriverAndSave();
        }

        public void DeleteBlendSample(string stateKey, int sampleIndex)
        {
            EnsureLoaded();
            XAnimationStateConfig config = m_CompiledAsset.GetState(stateKey).Config;
            XAnimationBlend1DSampleConfig[] samples = config.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
            if (sampleIndex < 0 || sampleIndex >= samples.Length)
            {
                throw new XFrameworkException($"XAnimation Blend1D sample index '{sampleIndex}' does not exist.");
            }

            if (samples.Length <= 2)
            {
                throw new XFrameworkException("XAnimation Blend1D state must contain at least two samples.");
            }

            List<XAnimationBlend1DSampleConfig> orderedSamples = new(samples.Length - 1);
            for (int i = 0; i < samples.Length; i++)
            {
                if (i != sampleIndex)
                {
                    orderedSamples.Add(samples[i]);
                }
            }

            config.samples = orderedSamples.ToArray();
            RebuildDriverAndSave();
        }

        public void SetBlendSampleClipKey(string stateKey, int sampleIndex, string clipKey)
        {
            EnsureLoaded();
            clipKey = clipKey?.Trim();
            m_CompiledAsset.GetClip(clipKey);
            XAnimationBlend1DSampleConfig sample = GetBlendSampleConfig(stateKey, sampleIndex);
            sample.clipKey = clipKey;
            RebuildDriverAndSave();
        }

        public void SetBlendSampleThreshold(string stateKey, int sampleIndex, float threshold)
        {
            EnsureLoaded();
            XAnimationBlend1DSampleConfig sample = GetBlendSampleConfig(stateKey, sampleIndex);
            sample.threshold = threshold;
            RebuildDriverAndSave();
        }

        public int AddCue(string clipKey)
        {
            EnsureBaseAssetEditable();
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                if (m_CompiledAsset.Clips.Count == 0)
                {
                    throw new XFrameworkException("Cannot add cue because no clip exists.");
                }

                clipKey = m_CompiledAsset.Clips[0].Key;
            }

            m_CompiledAsset.GetClip(clipKey);
            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationCueConfig[] cues = asset.cues ?? Array.Empty<XAnimationCueConfig>();
            List<XAnimationCueConfig> orderedCues = new(cues)
            {
                new XAnimationCueConfig
                {
                    clipKey = clipKey,
                    time = 0f,
                    eventKey = CreateUniqueCueEventKey("NewCue"),
                    payload = string.Empty,
                }
            };

            asset.cues = orderedCues.ToArray();
            RebuildDriverAndSave();
            return asset.cues.Length - 1;
        }

        public void DeleteCue(int cueIndex)
        {
            EnsureBaseAssetEditable();
            XAnimationAsset asset = m_CompiledAsset.Asset;
            XAnimationCueConfig[] cues = asset.cues ?? Array.Empty<XAnimationCueConfig>();
            if (cueIndex < 0 || cueIndex >= cues.Length)
            {
                throw new XFrameworkException($"XAnimation cue index '{cueIndex}' does not exist.");
            }

            List<XAnimationCueConfig> orderedCues = new(cues.Length - 1);
            for (int i = 0; i < cues.Length; i++)
            {
                if (i != cueIndex)
                {
                    orderedCues.Add(cues[i]);
                }
            }

            asset.cues = orderedCues.ToArray();
            RebuildDriverAndSave();
        }

        public void SetCueClipKey(int cueIndex, string clipKey)
        {
            EnsureBaseAssetEditable();
            clipKey = clipKey?.Trim();
            if (string.IsNullOrWhiteSpace(clipKey))
            {
                throw new XFrameworkException("XAnimation cue clipKey cannot be empty.");
            }

            m_CompiledAsset.GetClip(clipKey);
            XAnimationCueConfig cue = GetCueConfig(cueIndex);
            if (string.Equals(cue.clipKey, clipKey, StringComparison.Ordinal))
            {
                return;
            }

            cue.clipKey = clipKey;
            RebuildDriverAndSave();
        }

        public void SetCueTime(int cueIndex, float time, bool save = true)
        {
            EnsureBaseAssetEditable();
            XAnimationCueConfig cue = GetCueConfig(cueIndex);
            cue.time = Mathf.Clamp01(time);
            if (save)
            {
                SaveCompiledAsset();
            }
        }

        public void SetCueEventKey(int cueIndex, string eventKey)
        {
            EnsureBaseAssetEditable();
            eventKey = eventKey?.Trim();
            if (string.IsNullOrWhiteSpace(eventKey))
            {
                throw new XFrameworkException("XAnimation cue eventKey cannot be empty.");
            }

            XAnimationCueConfig cue = GetCueConfig(cueIndex);
            cue.eventKey = eventKey;
            SaveCompiledAsset();
        }

        public void SetCuePayload(int cueIndex, string payload)
        {
            EnsureBaseAssetEditable();
            XAnimationCueConfig cue = GetCueConfig(cueIndex);
            cue.payload = payload ?? string.Empty;
            SaveCompiledAsset();
        }

        private void SetOverrideClipPath(string clipKey, string clipPath)
        {
            if (m_OverrideAsset == null)
            {
                throw new XFrameworkException("XAnimation override asset is not loaded.");
            }

            string originalClipPath = GetOriginalClipPath(clipKey);
            List<XAnimationOverrideClipConfig> overrideClips = new(m_OverrideAsset.clips ?? Array.Empty<XAnimationOverrideClipConfig>());
            int index = overrideClips.FindIndex(item => item != null && string.Equals(item.key, clipKey, StringComparison.Ordinal));
            if (string.Equals(originalClipPath, clipPath, StringComparison.Ordinal))
            {
                if (index >= 0)
                {
                    overrideClips.RemoveAt(index);
                }
            }
            else if (index >= 0)
            {
                overrideClips[index].clipPath = clipPath;
            }
            else
            {
                overrideClips.Add(new XAnimationOverrideClipConfig
                {
                    key = clipKey,
                    clipPath = clipPath,
                });
            }

            m_OverrideAsset.clips = overrideClips.ToArray();
            m_OverrideAsset.SaveAsset();
            m_CompiledAsset = m_AssetLoader.Load(m_AssetPath);
            RebuildDriver();
        }

        private void EnsureBaseAssetEditable()
        {
            EnsureLoaded();
            if (m_IsOverrideAsset)
            {
                throw new XFrameworkException("XAnimation override asset cannot edit channels or clip structure.");
            }
        }

        private string CreateUniqueChannelName(string prefix)
        {
            return CreateUniqueName(prefix, name => m_CompiledAsset.TryGetChannelIndex(name, out _));
        }

        private string CreateUniqueClipKey(string prefix)
        {
            return CreateUniqueName(prefix, key => m_CompiledAsset.TryGetClipIndex(key, out _));
        }

        private string CreateUniqueStateKey(string prefix)
        {
            return CreateUniqueName(prefix, key => m_CompiledAsset.TryGetStateIndex(key, out _));
        }

        private string CreateUniqueCueEventKey(string prefix)
        {
            return CreateUniqueName(prefix, key =>
            {
                XAnimationCueConfig[] cues = m_CompiledAsset.Asset.cues ?? Array.Empty<XAnimationCueConfig>();
                for (int i = 0; i < cues.Length; i++)
                {
                    XAnimationCueConfig cue = cues[i];
                    if (cue != null && string.Equals(cue.eventKey, key, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        private XAnimationCueConfig GetCueConfig(int cueIndex)
        {
            XAnimationCueConfig[] cues = m_CompiledAsset.Asset.cues ?? Array.Empty<XAnimationCueConfig>();
            if (cueIndex < 0 || cueIndex >= cues.Length || cues[cueIndex] == null)
            {
                throw new XFrameworkException($"XAnimation cue index '{cueIndex}' does not exist.");
            }

            return cues[cueIndex];
        }

        private XAnimationBlend1DSampleConfig GetBlendSampleConfig(string stateKey, int sampleIndex)
        {
            XAnimationStateConfig state = m_CompiledAsset.GetState(stateKey).Config;
            XAnimationBlend1DSampleConfig[] samples = state.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
            if (sampleIndex < 0 || sampleIndex >= samples.Length || samples[sampleIndex] == null)
            {
                throw new XFrameworkException($"XAnimation Blend1D sample index '{sampleIndex}' does not exist.");
            }

            return samples[sampleIndex];
        }

        private static string CreateUniqueName(string prefix, Predicate<string> exists)
        {
            if (!exists(prefix))
            {
                return prefix;
            }

            for (int i = 1; i < 10000; i++)
            {
                string name = $"{prefix}{i}";
                if (!exists(name))
                {
                    return name;
                }
            }

            throw new XFrameworkException($"Unable to create unique name with prefix '{prefix}'.");
        }

        private static string FindTemplateClipPath(XAnimationClipConfig[] clips)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip != null && !string.IsNullOrWhiteSpace(clip.clipPath))
                {
                    return clip.clipPath;
                }
            }

            return string.Empty;
        }

        private static string FindTemplateClipKey(XAnimationClipConfig[] clips)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip != null && !string.IsNullOrWhiteSpace(clip.key))
                {
                    return clip.key;
                }
            }

            return string.Empty;
        }

        private string EnsureFloatParameter()
        {
            XAnimationParameterConfig[] parameters = m_CompiledAsset.Asset.parameters ?? Array.Empty<XAnimationParameterConfig>();
            for (int i = 0; i < parameters.Length; i++)
            {
                XAnimationParameterConfig parameter = parameters[i];
                if (parameter != null && parameter.type == XAnimationParameterType.Float && !string.IsNullOrWhiteSpace(parameter.name))
                {
                    return parameter.name;
                }
            }

            string parameterName = CreateUniqueParameterName("blend");
            List<XAnimationParameterConfig> orderedParameters = new(parameters)
            {
                new XAnimationParameterConfig
                {
                    name = parameterName,
                    type = XAnimationParameterType.Float,
                    defaultValue = 0f,
                }
            };
            m_CompiledAsset.Asset.parameters = orderedParameters.ToArray();
            return parameterName;
        }

        private string CreateUniqueParameterName(string prefix)
        {
            return CreateUniqueName(prefix, name =>
            {
                XAnimationParameterConfig[] parameters = m_CompiledAsset.Asset.parameters ?? Array.Empty<XAnimationParameterConfig>();
                for (int i = 0; i < parameters.Length; i++)
                {
                    XAnimationParameterConfig parameter = parameters[i];
                    if (parameter != null && string.Equals(parameter.name, name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        private XAnimationBlend1DSampleConfig[] CreateDefaultBlendSamples(string channelName)
        {
            XAnimationClipConfig[] clips = m_CompiledAsset.Asset.clips ?? Array.Empty<XAnimationClipConfig>();
            List<string> clipKeys = new(2);
            for (int i = 0; i < clips.Length && clipKeys.Count < 2; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip != null && !string.IsNullOrWhiteSpace(clip.key) && !clipKeys.Contains(clip.key))
                {
                    clipKeys.Add(clip.key);
                }
            }

            if (clipKeys.Count < 2)
            {
                throw new XFrameworkException("Cannot create Blend1D state because at least two clips are required.");
            }

            return new[]
            {
                new XAnimationBlend1DSampleConfig
                {
                    clipKey = clipKeys[0],
                    threshold = 0f,
                },
                new XAnimationBlend1DSampleConfig
                {
                    clipKey = clipKeys[1],
                    threshold = 1f,
                }
            };
        }

        private static void RemoveCueReferences(XAnimationAsset asset, HashSet<string> removedClipKeys)
        {
            if (asset.cues == null || removedClipKeys == null || removedClipKeys.Count == 0)
            {
                return;
            }

            List<XAnimationCueConfig> cues = new(asset.cues.Length);
            for (int i = 0; i < asset.cues.Length; i++)
            {
                XAnimationCueConfig cue = asset.cues[i];
                if (cue == null || !removedClipKeys.Contains(cue.clipKey))
                {
                    cues.Add(cue);
                }
            }

            asset.cues = cues.ToArray();
        }

        private static void RemoveStateReferences(XAnimationAsset asset, HashSet<string> removedClipKeys)
        {
            if (asset.states == null || removedClipKeys == null || removedClipKeys.Count == 0)
            {
                return;
            }

            List<XAnimationStateConfig> states = new(asset.states.Length);
            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state == null)
                {
                    continue;
                }

                if (state.stateType == XAnimationStateType.Single && removedClipKeys.Contains(state.clipKey))
                {
                    continue;
                }

                if (state.stateType == XAnimationStateType.Blend1D && HasRemovedBlendSample(state, removedClipKeys))
                {
                    continue;
                }

                states.Add(state);
            }

            asset.states = states.ToArray();
        }

        private static void RemoveStatesInChannel(XAnimationAsset asset, string channelName)
        {
            if (asset.states == null)
            {
                return;
            }

            List<XAnimationStateConfig> states = new(asset.states.Length);
            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state == null || string.Equals(state.channelName, channelName, StringComparison.Ordinal))
                {
                    continue;
                }

                states.Add(state);
            }

            asset.states = states.ToArray();
        }

        private static bool HasRemovedBlendSample(XAnimationStateConfig state, HashSet<string> removedClipKeys)
        {
            XAnimationBlend1DSampleConfig[] samples = state.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
            for (int i = 0; i < samples.Length; i++)
            {
                XAnimationBlend1DSampleConfig sample = samples[i];
                if (sample != null && removedClipKeys.Contains(sample.clipKey))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RenameStateChannelReferences(XAnimationAsset asset, string oldName, string newName)
        {
            if (asset.states == null)
            {
                return;
            }

            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state != null && string.Equals(state.channelName, oldName, StringComparison.Ordinal))
                {
                    state.channelName = newName;
                }
            }
        }

        private static void RenameStateClipReferences(XAnimationAsset asset, string oldKey, string newKey)
        {
            if (asset.states == null)
            {
                return;
            }

            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state == null)
                {
                    continue;
                }

                if (string.Equals(state.clipKey, oldKey, StringComparison.Ordinal))
                {
                    state.clipKey = newKey;
                }

                XAnimationBlend1DSampleConfig[] samples = state.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
                for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
                {
                    XAnimationBlend1DSampleConfig sample = samples[sampleIndex];
                    if (sample != null && string.Equals(sample.clipKey, oldKey, StringComparison.Ordinal))
                    {
                        sample.clipKey = newKey;
                    }
                }
            }
        }

        private static void RenameStateParameterReferences(XAnimationAsset asset, string oldName, string newName)
        {
            if (asset.states == null)
            {
                return;
            }

            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state != null && string.Equals(state.parameterName, oldName, StringComparison.Ordinal))
                {
                    state.parameterName = newName;
                }
            }
        }

        private static bool HasStateParameterReference(XAnimationAsset asset, string parameterName)
        {
            if (asset.states == null)
            {
                return false;
            }

            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state != null && string.Equals(state.parameterName, parameterName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveStateParameterReferences(XAnimationAsset asset, string parameterName, string fallbackParameterName)
        {
            if (asset.states == null)
            {
                return;
            }

            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state != null && string.Equals(state.parameterName, parameterName, StringComparison.Ordinal))
                {
                    state.parameterName = fallbackParameterName ?? string.Empty;
                }
            }
        }

        private static float ConvertParameterDefaultToFloat(object value)
        {
            if (value == null)
            {
                return 0f;
            }

            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private static bool ConvertParameterDefaultToBool(object value)
        {
            if (value == null)
            {
                return false;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
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
            RebuildDriver();
            SaveCompiledAsset();
        }

        private void RebuildDriver()
        {
            if (m_Driver != null && m_Animator != null)
            {
                m_Driver.CueTriggered -= OnCueTriggered;
                m_Driver.Dispose();
                m_Driver = new XAnimationDriver();
                m_Driver.Initialize(m_CompiledAsset, m_Animator);
                m_Driver.CueTriggered += OnCueTriggered;
                m_Driver.SetRootMotionEnabled(m_RootMotionEnabled);
            }
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
            m_AssetPath = null;
            m_IsOverrideAsset = false;
            m_OverrideAsset = null;
            m_CueLogs.Clear();
            m_OriginalClipPathByKey.Clear();
            m_RootMotionFallbackEvaluatorByClip.Clear();
            m_RenderTextureSize = Vector2Int.zero;
            ResetManualRootMotionPreviewState();
        }

        private void ResetManualRootMotionPreviewState()
        {
            m_ManualRootMotionPlaybackId = 0;
            m_ManualRootMotionNormalizedTime = 0f;
        }

        private void CacheOriginalClipPaths(string assetPath)
        {
            m_OriginalClipPathByKey.Clear();
            m_IsOverrideAsset = false;
            m_OverrideAsset = null;

            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (textAsset == null)
            {
                return;
            }

            XAnimationOverrideAsset overrideAsset = textAsset.ToXTextAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                m_IsOverrideAsset = true;
                m_OverrideAsset = overrideAsset;
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
            camera.farClipPlane = PreviewFarClipPlane;
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
            UpdateGridMaterialForCamera();
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
            float gridHalf = Mathf.Max(MinGridHalfSize, Mathf.Ceil(modelExtent * 12f));
            int cellCount = Mathf.RoundToInt(gridHalf / FarGridSpacing);
            gridHalf = Mathf.Max(FarGridSpacing, cellCount * FarGridSpacing);
            float gridSize = gridHalf * 2f;
            m_GridSpacing = CloseGridSpacing;

            // Create material from URP grid shader
            Shader shader = Shader.Find("Hidden/XFramework/AnimationPreviewGrid");
            if (shader == null)
            {
                Debug.LogWarning("XAnimation preview grid shader not found (Hidden/XFramework/AnimationPreviewGrid).");
                return;
            }

            m_GridMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            m_GridMaterial.SetColor("_BGColor", new Color(0.075f, 0.082f, 0.09f, 0.58f));
            m_GridMaterial.SetColor("_GridColor", new Color(0.58f, 0.64f, 0.68f, 0.22f));
            m_GridMaterial.SetColor("_MajorGridColor", new Color(0.74f, 0.80f, 0.86f, 0.42f));
            m_GridMaterial.SetColor("_CenterLineColor", new Color(0.42f, 0.66f, 0.95f, 0.60f));
            m_GridMaterial.SetFloat("_GridWidth", 0.015f);
            m_GridMaterial.SetFloat("_MajorGridWidth", 0.035f);
            m_GridMaterial.SetFloat("_CenterLineWidth", 0.05f);
            m_GridMaterial.SetFloat("_GridSpacing", m_GridSpacing);
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

        private void UpdateGridMaterialForCamera()
        {
            if (m_GridMaterial == null)
            {
                return;
            }

            float closeGridCellPixels = GetCloseGridCellPixelSize();
            float targetSpacing = m_GridSpacing;
            if (m_GridSpacing < FarGridSpacing && closeGridCellPixels <= SwitchToFarGridCellPixels)
            {
                targetSpacing = FarGridSpacing;
            }
            else if (m_GridSpacing > CloseGridSpacing && closeGridCellPixels >= SwitchToCloseGridCellPixels)
            {
                targetSpacing = CloseGridSpacing;
            }

            if (Mathf.Approximately(targetSpacing, m_GridSpacing))
            {
                return;
            }

            m_GridSpacing = targetSpacing;
            float widthScale = Mathf.Sqrt(m_GridSpacing);
            m_GridMaterial.SetFloat("_GridSpacing", m_GridSpacing);
            m_GridMaterial.SetFloat("_GridWidth", 0.015f * widthScale);
            m_GridMaterial.SetFloat("_MajorGridWidth", 0.035f * widthScale);
            m_GridMaterial.SetFloat("_CenterLineWidth", 0.05f * widthScale);
        }

        private float GetCloseGridCellPixelSize()
        {
            float distance = GetGridViewDistance();
            int pixelHeight = Mathf.Max(m_RenderTextureSize.y, 1);
            float fieldOfView = m_PreviewUtility?.camera != null ? m_PreviewUtility.camera.fieldOfView : 30f;
            float viewHeightMeters = 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            return CloseGridSpacing * pixelHeight / Mathf.Max(viewHeightMeters, 0.0001f);
        }

        private float GetGridViewDistance()
        {
            Quaternion rotation = Quaternion.Euler(m_CameraPitch, m_CameraYaw, 0f);
            Vector3 forward = rotation * Vector3.forward;
            float gridY = m_GridPlane != null ? m_GridPlane.transform.position.y : 0f;
            if (Mathf.Abs(forward.y) > 0.0001f)
            {
                float hitDistance = (gridY - m_CameraPosition.y) / forward.y;
                if (hitDistance > 0f)
                {
                    return Mathf.Max(hitDistance, 0.05f);
                }
            }

            float heightFromGrid = Mathf.Abs(m_CameraPosition.y - gridY);
            float pitchRadians = Mathf.Max(Mathf.Abs(m_CameraPitch) * Mathf.Deg2Rad, 5f * Mathf.Deg2Rad);
            return Mathf.Max(heightFromGrid / Mathf.Sin(pitchRadians), 0.05f);
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
