using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.Animation
{
    public enum XAnimationChannelLayerType
    {
        Base,
        Override,
        Additive,
    }

    public enum XAnimationParameterType
    {
        Float = 0,
        Bool = 1,
        Trigger = 2,
        Int = 3,
    }

    public enum XAnimationClipRootMotionMode
    {
        Inherit,
        ForceOn,
        ForceOff,
    }

    public enum XAnimationStateType
    {
        Single,
        Blend1D,
        Blend2DSimpleDirectional,
        Blend2DFreeformDirectional,
    }

    [Serializable]
    public class XAnimationChannelConfig
    {
        public string name;
        public XAnimationChannelLayerType layerType = XAnimationChannelLayerType.Base;
        public float defaultWeight = 1f;
        [AssetPath(typeof(AvatarMask))]
        public string maskPath;
        public bool allowInterrupt = true;
        public float defaultFadeIn = 0.15f;
        public float defaultFadeOut = 0.15f;
        public bool canDriveRootMotion;
    }

    [Serializable]
    public class XAnimationClipConfig
    {
        public string key;
        [AssetPath(typeof(AnimationClip))]
        public string clipPath;
        public XAnimationRootMotionTrackConfig rootMotionTrack;
    }

    [Serializable]
    public sealed class XAnimationCurveKeyframe
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
        public float inWeight;
        public float outWeight;
        public WeightedMode weightedMode;
    }

    [Serializable]
    public sealed class XAnimationRootMotionTrackConfig
    {
        public float clipLength;
        public Vector3 loopDeltaPosition;
        public XAnimationCurveKeyframe[] positionX = Array.Empty<XAnimationCurveKeyframe>();
        public XAnimationCurveKeyframe[] positionY = Array.Empty<XAnimationCurveKeyframe>();
        public XAnimationCurveKeyframe[] positionZ = Array.Empty<XAnimationCurveKeyframe>();
        public XAnimationCurveKeyframe[] rotationX = Array.Empty<XAnimationCurveKeyframe>();
        public XAnimationCurveKeyframe[] rotationY = Array.Empty<XAnimationCurveKeyframe>();
        public XAnimationCurveKeyframe[] rotationZ = Array.Empty<XAnimationCurveKeyframe>();
        public XAnimationCurveKeyframe[] rotationW = Array.Empty<XAnimationCurveKeyframe>();

        public bool HasPosition =>
            (positionX?.Length ?? 0) > 0 ||
            (positionY?.Length ?? 0) > 0 ||
            (positionZ?.Length ?? 0) > 0;

        public bool HasRotation =>
            (rotationX?.Length ?? 0) > 0 ||
            (rotationY?.Length ?? 0) > 0 ||
            (rotationZ?.Length ?? 0) > 0 ||
            (rotationW?.Length ?? 0) > 0;
    }

    [Serializable]
    public class XAnimationParameterConfig
    {
        public string name;
        public XAnimationParameterType type;
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public object defaultValue;
    }

    [Serializable]
    public class XAnimationCueConfig
    {
        public string clipKey;
        [Range(0f, 1f)]
        public float time;
        public string eventKey;
        public string payload;
    }

    [Serializable]
    public class XAnimationStateConfig
    {
        public string key;
        public XAnimationStateType stateType = XAnimationStateType.Single;
        public string clipKey;
        public string channelName;
        public float fadeIn = 0.15f;
        public float fadeOut = 0.15f;
        public float speed = 1f;
        public bool loop = true;
        public XAnimationClipRootMotionMode rootMotionMode = XAnimationClipRootMotionMode.Inherit;
        public string parameterName;
        public string parameterXName;
        public string parameterYName;
        public XAnimationBlend1DSampleConfig[] samples = Array.Empty<XAnimationBlend1DSampleConfig>();
        public XAnimationBlend2DSimpleDirectionalSampleConfig[] directionalSamples = Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
    }

    [Serializable]
    public class XAnimationAutoTransitionConfig
    {
        public string preStateKey;
        public string nextStateKey;
        [JsonProperty("ExitTime")]
        public float exitTime = 1f;
        [JsonProperty("TransitionDuration")]
        public float transitionDuration;
        [JsonProperty("EnterTime")]
        public float enterTime;
    }

    [Serializable]
    public class XAnimationBlend1DSampleConfig
    {
        public string clipKey;
        public float threshold;
    }

    [Serializable]
    public class XAnimationBlend2DSimpleDirectionalSampleConfig
    {
        public string clipKey;
        public float positionX;
        public float positionY;
    }

    [Serializable]
    [XTextAssetAlias("xframework.animation.asset")]
    public class XAnimationAsset : XTextAsset
    {
        public string alias;
        [AssetPath(typeof(GameObject))]
        public string DefaultPrefabPath;
        public XAnimationChannelConfig[] channels = Array.Empty<XAnimationChannelConfig>();
        public XAnimationClipConfig[] clips = Array.Empty<XAnimationClipConfig>();
        public XAnimationStateConfig[] states = Array.Empty<XAnimationStateConfig>();
        public XAnimationAutoTransitionConfig[] autoTransitions = Array.Empty<XAnimationAutoTransitionConfig>();
        public XAnimationParameterConfig[] parameters = Array.Empty<XAnimationParameterConfig>();
        public XAnimationCueConfig[] cues = Array.Empty<XAnimationCueConfig>();
    }

    [Serializable]
    public class XAnimationOverrideClipConfig
    {
        public string key;
        [AssetPath(typeof(AnimationClip))]
        public string clipPath;
    }

    [Serializable]
    [XTextAssetAlias("xframework.animation.override")]
    public class XAnimationOverrideAsset : XTextAsset
    {
        [AssetPath(typeof(TextAsset))]
        public string baseAssetPath;
        [AssetPath(typeof(GameObject))]
        public string DefaultPrefabPath;
        public XAnimationOverrideClipConfig[] clips = Array.Empty<XAnimationOverrideClipConfig>();
    }

    public sealed class XAnimationCompiledChannel
    {
        public XAnimationCompiledChannel(XAnimationChannelConfig config, AvatarMask mask, int layerIndex)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Mask = mask;
            LayerIndex = layerIndex;
        }

        public XAnimationChannelConfig Config { get; }
        public AvatarMask Mask { get; }
        public int LayerIndex { get; }
        public string Name => Config.name;
    }

    public sealed class XAnimationCompiledClip
    {
        public XAnimationCompiledClip(
            XAnimationClipConfig config,
            AnimationClip clip,
            AnimationClip playbackClip = null,
            XAnimationRootMotionTrack rootMotionTrack = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Clip = clip ? clip : throw new ArgumentNullException(nameof(clip));
            PlaybackClip = playbackClip ? playbackClip : Clip;
            RootMotionTrack = rootMotionTrack;
        }

        public XAnimationClipConfig Config { get; }
        public AnimationClip Clip { get; }
        public AnimationClip PlaybackClip { get; }
        public XAnimationRootMotionTrack RootMotionTrack { get; }
        public string Key => Config.key;
    }

    public sealed class XAnimationRootMotionTrack
    {
        private readonly float m_ClipLength;
        private readonly AnimationCurve m_PositionX;
        private readonly AnimationCurve m_PositionY;
        private readonly AnimationCurve m_PositionZ;
        private readonly AnimationCurve m_RotationX;
        private readonly AnimationCurve m_RotationY;
        private readonly AnimationCurve m_RotationZ;
        private readonly AnimationCurve m_RotationW;

        private XAnimationRootMotionTrack(
            float clipLength,
            Vector3 loopDeltaPosition,
            AnimationCurve positionX,
            AnimationCurve positionY,
            AnimationCurve positionZ,
            AnimationCurve rotationX,
            AnimationCurve rotationY,
            AnimationCurve rotationZ,
            AnimationCurve rotationW)
        {
            m_ClipLength = Mathf.Max(clipLength, 0.0001f);
            LoopDeltaPosition = loopDeltaPosition;
            m_PositionX = positionX;
            m_PositionY = positionY;
            m_PositionZ = positionZ;
            m_RotationX = rotationX;
            m_RotationY = rotationY;
            m_RotationZ = rotationZ;
            m_RotationW = rotationW;
        }

        public bool HasPosition => m_PositionX != null || m_PositionY != null || m_PositionZ != null;
        public bool HasRotation => m_RotationX != null || m_RotationY != null || m_RotationZ != null || m_RotationW != null;
        public Vector3 LoopDeltaPosition { get; }

        public static XAnimationRootMotionTrack Create(XAnimationRootMotionTrackConfig config)
        {
            if (config == null || (!config.HasPosition && !config.HasRotation))
            {
                return null;
            }

            return new XAnimationRootMotionTrack(
                config.clipLength,
                config.loopDeltaPosition,
                BuildCurve(config.positionX),
                BuildCurve(config.positionY),
                BuildCurve(config.positionZ),
                BuildCurve(config.rotationX),
                BuildCurve(config.rotationY),
                BuildCurve(config.rotationZ),
                BuildCurve(config.rotationW));
        }

        public Vector3 EvaluateAccumulatedPosition(float totalNormalizedTime, bool isLooping)
        {
            if (!HasPosition)
            {
                return Vector3.zero;
            }

            if (!isLooping)
            {
                return EvaluatePosition(Mathf.Clamp01(totalNormalizedTime));
            }

            int loopCount = Mathf.FloorToInt(totalNormalizedTime);
            float normalizedTime = Mathf.Repeat(totalNormalizedTime, 1f);
            return LoopDeltaPosition * loopCount + EvaluatePosition(normalizedTime);
        }

        public Quaternion EvaluateRotation(float totalNormalizedTime, bool isLooping)
        {
            if (!HasRotation)
            {
                return Quaternion.identity;
            }

            float normalizedTime = isLooping
                ? Mathf.Repeat(totalNormalizedTime, 1f)
                : Mathf.Clamp01(totalNormalizedTime);
            float time = normalizedTime * m_ClipLength;
            Quaternion rotation = new(
                m_RotationX != null ? m_RotationX.Evaluate(time) : 0f,
                m_RotationY != null ? m_RotationY.Evaluate(time) : 0f,
                m_RotationZ != null ? m_RotationZ.Evaluate(time) : 0f,
                m_RotationW != null ? m_RotationW.Evaluate(time) : 1f);
            return Normalize(rotation);
        }

        private Vector3 EvaluatePosition(float normalizedTime)
        {
            float time = Mathf.Clamp01(normalizedTime) * m_ClipLength;
            return new Vector3(
                m_PositionX != null ? m_PositionX.Evaluate(time) : 0f,
                m_PositionY != null ? m_PositionY.Evaluate(time) : 0f,
                m_PositionZ != null ? m_PositionZ.Evaluate(time) : 0f);
        }

        private static AnimationCurve BuildCurve(XAnimationCurveKeyframe[] source)
        {
            if (source == null || source.Length == 0)
            {
                return null;
            }

            Keyframe[] keys = new Keyframe[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                XAnimationCurveKeyframe keyframe = source[i];
                Keyframe runtimeKeyframe = new(
                    keyframe.time,
                    keyframe.value,
                    keyframe.inTangent,
                    keyframe.outTangent,
                    keyframe.inWeight,
                    keyframe.outWeight)
                {
                    weightedMode = keyframe.weightedMode,
                };
                keys[i] = runtimeKeyframe;
            }

            return new AnimationCurve(keys);
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

    internal sealed class XAnimationRootMotionEvaluator
    {
        private const float ActiveSampleWeightThreshold = 0.0001f;

        private bool m_HasPreviousSourceSample;
        private string m_PreviousSourceChannelName = string.Empty;
        private int m_PreviousSourcePlaybackId;
        private float m_PreviousSourceTotalNormalizedTime;

        public void Reset()
        {
            m_HasPreviousSourceSample = false;
            m_PreviousSourceChannelName = string.Empty;
            m_PreviousSourcePlaybackId = 0;
            m_PreviousSourceTotalNormalizedTime = 0f;
        }

        public bool TryEvaluate(
            XAnimationCompiledAsset compiledAsset,
            XAnimationChannel sourceChannel,
            out Vector3 deltaPosition,
            out Quaternion deltaRotation)
        {
            deltaPosition = Vector3.zero;
            deltaRotation = Quaternion.identity;

            if (compiledAsset == null || sourceChannel == null)
            {
                Reset();
                return false;
            }

            XAnimationChannelState state = sourceChannel.GetState();
            if (state == null || state.playbackId <= 0)
            {
                Reset();
                return false;
            }

            bool playbackChanged = !m_HasPreviousSourceSample ||
                m_PreviousSourcePlaybackId != state.playbackId ||
                !string.Equals(m_PreviousSourceChannelName, state.channelName, StringComparison.Ordinal);

            float previousTotalNormalizedTime = m_PreviousSourceTotalNormalizedTime;
            m_HasPreviousSourceSample = true;
            m_PreviousSourceChannelName = state.channelName ?? string.Empty;
            m_PreviousSourcePlaybackId = state.playbackId;
            m_PreviousSourceTotalNormalizedTime = state.totalNormalizedTime;

            if (playbackChanged)
            {
                return true;
            }

            if (!TryEvaluateStateTransform(compiledAsset, state, previousTotalNormalizedTime, out Vector3 previousPosition, out Quaternion previousRotation, out bool hasPosition, out bool hasRotation) ||
                !TryEvaluateStateTransform(compiledAsset, state, state.totalNormalizedTime, out Vector3 currentPosition, out Quaternion currentRotation, out _, out _))
            {
                return false;
            }

            if (hasPosition)
            {
                deltaPosition = currentPosition - previousPosition;
            }

            if (hasRotation)
            {
                deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
            }

            return true;
        }

        private static bool TryEvaluateStateTransform(
            XAnimationCompiledAsset compiledAsset,
            XAnimationChannelState state,
            float totalNormalizedTime,
            out Vector3 position,
            out Quaternion rotation,
            out bool hasPosition,
            out bool hasRotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            hasPosition = false;
            hasRotation = false;

            if (compiledAsset == null || state == null)
            {
                return false;
            }

            if (state.stateType == XAnimationStateType.Single)
            {
                return TryEvaluateClipTransform(
                    compiledAsset,
                    state.clipKey,
                    totalNormalizedTime,
                    state.isLooping,
                    out position,
                    out rotation,
                    out hasPosition,
                    out hasRotation);
            }

            XAnimationBlendClipState[] blendClips = state.blendClips;
            if (blendClips == null || blendClips.Length == 0)
            {
                return false;
            }

            Vector4 blendedRotation = Vector4.zero;
            Quaternion referenceRotation = Quaternion.identity;
            bool hasReferenceRotation = false;
            bool hasAnySample = false;

            for (int i = 0; i < blendClips.Length; i++)
            {
                XAnimationBlendClipState blendClip = blendClips[i];
                if (blendClip == null || blendClip.weight <= ActiveSampleWeightThreshold)
                {
                    continue;
                }

                if (!TryEvaluateClipTransform(
                        compiledAsset,
                        blendClip.clipKey,
                        totalNormalizedTime,
                        state.isLooping,
                        out Vector3 clipPosition,
                        out Quaternion clipRotation,
                        out bool clipHasPosition,
                        out bool clipHasRotation))
                {
                    return false;
                }

                hasAnySample = true;
                if (clipHasPosition)
                {
                    position += clipPosition * blendClip.weight;
                    hasPosition = true;
                }

                if (clipHasRotation)
                {
                    AccumulateRotation(ref blendedRotation, ref referenceRotation, ref hasReferenceRotation, clipRotation, blendClip.weight);
                    hasRotation = true;
                }
            }

            if (!hasAnySample)
            {
                return false;
            }

            if (hasRotation)
            {
                rotation = Normalize(new Quaternion(blendedRotation.x, blendedRotation.y, blendedRotation.z, blendedRotation.w));
            }

            return hasPosition || hasRotation;
        }

        private static bool TryEvaluateClipTransform(
            XAnimationCompiledAsset compiledAsset,
            string clipKey,
            float totalNormalizedTime,
            bool isLooping,
            out Vector3 position,
            out Quaternion rotation,
            out bool hasPosition,
            out bool hasRotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            hasPosition = false;
            hasRotation = false;

            if (compiledAsset == null ||
                string.IsNullOrWhiteSpace(clipKey) ||
                !compiledAsset.TryGetClipIndex(clipKey, out int clipIndex))
            {
                return false;
            }

            XAnimationCompiledClip clip = (XAnimationCompiledClip)compiledAsset.Clips[clipIndex];
            XAnimationRootMotionTrack track = clip.RootMotionTrack;
            if (track == null || (!track.HasPosition && !track.HasRotation))
            {
                return false;
            }

            hasPosition = track.HasPosition;
            hasRotation = track.HasRotation;
            if (hasPosition)
            {
                position = track.EvaluateAccumulatedPosition(totalNormalizedTime, isLooping);
            }

            if (hasRotation)
            {
                rotation = track.EvaluateRotation(totalNormalizedTime, isLooping);
            }

            return true;
        }

        private static void AccumulateRotation(
            ref Vector4 accumulator,
            ref Quaternion referenceRotation,
            ref bool hasReferenceRotation,
            Quaternion rotation,
            float weight)
        {
            if (!hasReferenceRotation)
            {
                referenceRotation = rotation;
                hasReferenceRotation = true;
            }
            else if (Quaternion.Dot(referenceRotation, rotation) < 0f)
            {
                rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
            }

            accumulator.x += rotation.x * weight;
            accumulator.y += rotation.y * weight;
            accumulator.z += rotation.z * weight;
            accumulator.w += rotation.w * weight;
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

    internal static class XAnimationRootMotionTrackBuilder
    {
#if UNITY_EDITOR
        private static readonly Type s_AnimationUtilityType = Type.GetType("UnityEditor.AnimationUtility, UnityEditor");
#endif

        public static XAnimationRootMotionTrackConfig BuildConfig(AnimationClip clip)
        {
#if UNITY_EDITOR
            if (clip == null || s_AnimationUtilityType == null)
            {
                return null;
            }

            MethodInfo getCurveBindingsMethod = s_AnimationUtilityType.GetMethod(
                "GetCurveBindings",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(AnimationClip) },
                null);
            if (getCurveBindingsMethod == null)
            {
                return null;
            }

            Array bindings = getCurveBindingsMethod.Invoke(null, new object[] { clip }) as Array;
            if (bindings == null)
            {
                return null;
            }

            Type bindingType = bindings.GetType().GetElementType();
            MethodInfo getEditorCurveMethod = s_AnimationUtilityType.GetMethod(
                "GetEditorCurve",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(AnimationClip), bindingType },
                null);
            if (getEditorCurveMethod == null)
            {
                return null;
            }

            AnimationCurve rootTX = null;
            AnimationCurve rootTY = null;
            AnimationCurve rootTZ = null;
            AnimationCurve rootQX = null;
            AnimationCurve rootQY = null;
            AnimationCurve rootQZ = null;
            AnimationCurve rootQW = null;

            for (int i = 0; i < bindings.Length; i++)
            {
                object binding = bindings.GetValue(i);
                if (!TryGetBindingPropertyName(binding, out string propertyName))
                {
                    continue;
                }

                AnimationCurve curve = getEditorCurveMethod.Invoke(null, new[] { clip, binding }) as AnimationCurve;
                switch (propertyName)
                {
                    case "RootT.x":
                        rootTX = curve;
                        break;
                    case "RootT.y":
                        rootTY = curve;
                        break;
                    case "RootT.z":
                        rootTZ = curve;
                        break;
                    case "RootQ.x":
                        rootQX = curve;
                        break;
                    case "RootQ.y":
                        rootQY = curve;
                        break;
                    case "RootQ.z":
                        rootQZ = curve;
                        break;
                    case "RootQ.w":
                        rootQW = curve;
                        break;
                }
            }

            bool hasPosition = rootTX != null || rootTY != null || rootTZ != null;
            bool hasRotation = rootQX != null || rootQY != null || rootQZ != null || rootQW != null;
            if (!hasPosition && !hasRotation)
            {
                return null;
            }

            float clipLength = Mathf.Max(clip.length, 0.0001f);
            return new XAnimationRootMotionTrackConfig
            {
                clipLength = clipLength,
                loopDeltaPosition = EvaluatePosition(rootTX, rootTY, rootTZ, clipLength) - EvaluatePosition(rootTX, rootTY, rootTZ, 0f),
                positionX = ExportCurve(rootTX),
                positionY = ExportCurve(rootTY),
                positionZ = ExportCurve(rootTZ),
                rotationX = ExportCurve(rootQX),
                rotationY = ExportCurve(rootQY),
                rotationZ = ExportCurve(rootQZ),
                rotationW = ExportCurve(rootQW),
            };
#else
            _ = clip;
            return null;
#endif
        }

#if UNITY_EDITOR
        private static bool TryGetBindingPropertyName(object binding, out string propertyName)
        {
            propertyName = string.Empty;
            if (binding == null)
            {
                return false;
            }

            Type bindingType = binding.GetType();
            FieldInfo propertyNameField = bindingType.GetField("propertyName");
            if (propertyNameField != null)
            {
                propertyName = propertyNameField.GetValue(binding) as string;
                return !string.IsNullOrWhiteSpace(propertyName);
            }

            PropertyInfo propertyNameProperty = bindingType.GetProperty("propertyName", BindingFlags.Public | BindingFlags.Instance);
            if (propertyNameProperty == null)
            {
                return false;
            }

            propertyName = propertyNameProperty.GetValue(binding) as string;
            return !string.IsNullOrWhiteSpace(propertyName);
        }

        private static XAnimationCurveKeyframe[] ExportCurve(AnimationCurve curve)
        {
            if (curve == null || curve.keys == null || curve.keys.Length == 0)
            {
                return Array.Empty<XAnimationCurveKeyframe>();
            }

            Keyframe[] source = curve.keys;
            XAnimationCurveKeyframe[] output = new XAnimationCurveKeyframe[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Keyframe keyframe = source[i];
                output[i] = new XAnimationCurveKeyframe
                {
                    time = keyframe.time,
                    value = keyframe.value,
                    inTangent = keyframe.inTangent,
                    outTangent = keyframe.outTangent,
                    inWeight = keyframe.inWeight,
                    outWeight = keyframe.outWeight,
                    weightedMode = keyframe.weightedMode,
                };
            }

            return output;
        }

        private static Vector3 EvaluatePosition(AnimationCurve x, AnimationCurve y, AnimationCurve z, float time)
        {
            return new Vector3(
                x != null ? x.Evaluate(time) : 0f,
                y != null ? y.Evaluate(time) : 0f,
                z != null ? z.Evaluate(time) : 0f);
        }
#endif
    }

    public sealed class XAnimationCompiledParameter
    {
        public XAnimationCompiledParameter(XAnimationParameterConfig config, int index)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Index = index;
        }

        public XAnimationParameterConfig Config { get; }
        public int Index { get; }
        public string Name => Config.name;
        public XAnimationParameterType Type => Config.type;
    }

    public sealed class XAnimationCompiledCue
    {
        public XAnimationCompiledCue(XAnimationCueConfig config, int cueIndex)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            CueIndex = cueIndex;
        }

        public XAnimationCueConfig Config { get; }
        public int CueIndex { get; }
    }

    public sealed class XAnimationCompiledAutoTransition
    {
        public XAnimationCompiledAutoTransition(XAnimationAutoTransitionConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public XAnimationAutoTransitionConfig Config { get; }
        public string PreStateKey => Config.preStateKey;
        public string NextStateKey => Config.nextStateKey;
        public float ExitTime => Config.exitTime;
        public float TransitionDuration => Config.transitionDuration;
        public float EnterTime => Config.enterTime;
        public bool HasNextState => !string.IsNullOrWhiteSpace(Config.nextStateKey);
    }

    public abstract class XAnimationCompiledState
    {
        protected XAnimationCompiledState(XAnimationStateConfig config, int defaultChannelIndex)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            DefaultChannelIndex = defaultChannelIndex;
        }

        public XAnimationStateConfig Config { get; }
        public int DefaultChannelIndex { get; }
        public string Key => Config.key;
        public XAnimationStateType StateType => Config.stateType;
    }

    public sealed class XAnimationCompiledSingleState : XAnimationCompiledState
    {
        public XAnimationCompiledSingleState(XAnimationStateConfig config, int defaultChannelIndex, int clipIndex)
            : base(config, defaultChannelIndex)
        {
            ClipIndex = clipIndex;
        }

        public int ClipIndex { get; }
    }

    public sealed class XAnimationCompiledBlend1DSample
    {
        public XAnimationCompiledBlend1DSample(XAnimationBlend1DSampleConfig config, int clipIndex)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            ClipIndex = clipIndex;
        }

        public XAnimationBlend1DSampleConfig Config { get; }
        public int ClipIndex { get; }
        public float Threshold => Config.threshold;
    }

    public sealed class XAnimationCompiledBlend1DState : XAnimationCompiledState
    {
        public XAnimationCompiledBlend1DState(
            XAnimationStateConfig config,
            int defaultChannelIndex,
            int parameterIndex,
            XAnimationCompiledBlend1DSample[] samples)
            : base(config, defaultChannelIndex)
        {
            ParameterIndex = parameterIndex;
            Samples = samples ?? Array.Empty<XAnimationCompiledBlend1DSample>();
        }

        public int ParameterIndex { get; }
        public IReadOnlyList<XAnimationCompiledBlend1DSample> Samples { get; }
    }

    public sealed class XAnimationCompiledBlend2DSimpleDirectionalSample
    {
        public XAnimationCompiledBlend2DSimpleDirectionalSample(XAnimationBlend2DSimpleDirectionalSampleConfig config, int clipIndex)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            ClipIndex = clipIndex;
        }

        public XAnimationBlend2DSimpleDirectionalSampleConfig Config { get; }
        public int ClipIndex { get; }
        public Vector2 Position => new(Config.positionX, Config.positionY);
    }

    public sealed class XAnimationCompiledBlend2DSimpleDirectionalState : XAnimationCompiledState
    {
        public XAnimationCompiledBlend2DSimpleDirectionalState(
            XAnimationStateConfig config,
            int defaultChannelIndex,
            int parameterXIndex,
            int parameterYIndex,
            XAnimationCompiledBlend2DSimpleDirectionalSample[] samples)
            : base(config, defaultChannelIndex)
        {
            ParameterXIndex = parameterXIndex;
            ParameterYIndex = parameterYIndex;
            Samples = samples ?? Array.Empty<XAnimationCompiledBlend2DSimpleDirectionalSample>();
        }

        public int ParameterXIndex { get; }
        public int ParameterYIndex { get; }
        public IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> Samples { get; }
    }

    public sealed class XAnimationCompiledBlend2DFreeformDirectionalState : XAnimationCompiledState
    {
        public XAnimationCompiledBlend2DFreeformDirectionalState(
            XAnimationStateConfig config,
            int defaultChannelIndex,
            int parameterXIndex,
            int parameterYIndex,
            XAnimationCompiledBlend2DSimpleDirectionalSample[] samples)
            : base(config, defaultChannelIndex)
        {
            ParameterXIndex = parameterXIndex;
            ParameterYIndex = parameterYIndex;
            Samples = samples ?? Array.Empty<XAnimationCompiledBlend2DSimpleDirectionalSample>();
        }

        public int ParameterXIndex { get; }
        public int ParameterYIndex { get; }
        public IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> Samples { get; }
    }

    public sealed class XAnimationCompiledAsset
    {
        private readonly Dictionary<string, int> m_ChannelIndexByName;
        private readonly Dictionary<string, int> m_ClipIndexByKey;
        private readonly Dictionary<string, int> m_ParameterIndexByName;
        private readonly Dictionary<string, int> m_StateIndexByKey;
        private readonly Dictionary<string, int> m_AutoTransitionIndexByPreStateKey;

        public XAnimationCompiledAsset(
            XAnimationAsset asset,
            XAnimationCompiledChannel[] channels,
            XAnimationCompiledClip[] clips,
            XAnimationCompiledState[] states,
            XAnimationCompiledAutoTransition[] autoTransitions,
            XAnimationCompiledParameter[] parameters,
            Dictionary<string, List<XAnimationCompiledCue>> cuesByClipKey,
            Dictionary<string, int> channelIndexByName,
            Dictionary<string, int> clipIndexByKey,
            Dictionary<string, int> parameterIndexByName,
            Dictionary<string, int> stateIndexByKey,
            Dictionary<string, int> autoTransitionIndexByPreStateKey)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            Channels = channels ?? Array.Empty<XAnimationCompiledChannel>();
            Clips = clips ?? Array.Empty<XAnimationCompiledClip>();
            States = states ?? Array.Empty<XAnimationCompiledState>();
            AutoTransitions = autoTransitions ?? Array.Empty<XAnimationCompiledAutoTransition>();
            Parameters = parameters ?? Array.Empty<XAnimationCompiledParameter>();
            CuesByClipKey = cuesByClipKey ?? new Dictionary<string, List<XAnimationCompiledCue>>(StringComparer.Ordinal);
            m_ChannelIndexByName = channelIndexByName ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_ClipIndexByKey = clipIndexByKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_ParameterIndexByName = parameterIndexByName ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_StateIndexByKey = stateIndexByKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_AutoTransitionIndexByPreStateKey = autoTransitionIndexByPreStateKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public XAnimationAsset Asset { get; }
        public IReadOnlyList<XAnimationCompiledChannel> Channels { get; }
        public IReadOnlyList<XAnimationCompiledClip> Clips { get; }
        public IReadOnlyList<XAnimationCompiledState> States { get; }
        public IReadOnlyList<XAnimationCompiledAutoTransition> AutoTransitions { get; }
        public IReadOnlyList<XAnimationCompiledParameter> Parameters { get; }
        public IReadOnlyDictionary<string, List<XAnimationCompiledCue>> CuesByClipKey { get; }

        public bool TryGetChannelIndex(string channelName, out int channelIndex)
        {
            return m_ChannelIndexByName.TryGetValue(channelName, out channelIndex);
        }

        public bool TryGetClipIndex(string clipKey, out int clipIndex)
        {
            return m_ClipIndexByKey.TryGetValue(clipKey, out clipIndex);
        }

        public bool TryGetParameterIndex(string parameterName, out int parameterIndex)
        {
            return m_ParameterIndexByName.TryGetValue(parameterName, out parameterIndex);
        }

        public bool TryGetStateIndex(string stateKey, out int stateIndex)
        {
            return m_StateIndexByKey.TryGetValue(stateKey, out stateIndex);
        }

        public bool TryGetAutoTransition(string preStateKey, out XAnimationCompiledAutoTransition transition)
        {
            if (string.IsNullOrWhiteSpace(preStateKey) ||
                !m_AutoTransitionIndexByPreStateKey.TryGetValue(preStateKey, out int transitionIndex))
            {
                transition = null;
                return false;
            }

            transition = AutoTransitions[transitionIndex];
            return true;
        }

        public XAnimationCompiledChannel GetChannel(string channelName)
        {
            if (!TryGetChannelIndex(channelName, out int channelIndex))
            {
                throw new XFrameworkException($"XAnimation channel '{channelName}' does not exist.");
            }

            return Channels[channelIndex];
        }

        public XAnimationCompiledClip GetClip(string clipKey)
        {
            if (!TryGetClipIndex(clipKey, out int clipIndex))
            {
                throw new XFrameworkException($"XAnimation clip '{clipKey}' does not exist.");
            }

            return Clips[clipIndex];
        }

        public XAnimationCompiledParameter GetParameter(string parameterName)
        {
            if (!TryGetParameterIndex(parameterName, out int parameterIndex))
            {
                throw new XFrameworkException($"XAnimation parameter '{parameterName}' does not exist.");
            }

            return Parameters[parameterIndex];
        }

        public XAnimationCompiledState GetState(string stateKey)
        {
            if (!TryGetStateIndex(stateKey, out int stateIndex))
            {
                throw new XFrameworkException($"XAnimation state '{stateKey}' does not exist.");
            }

            return States[stateIndex];
        }

        public XAnimationCompiledAutoTransition GetAutoTransition(string preStateKey)
        {
            if (!TryGetAutoTransition(preStateKey, out XAnimationCompiledAutoTransition transition))
            {
                throw new XFrameworkException($"XAnimation auto transition for state '{preStateKey}' does not exist.");
            }

            return transition;
        }

        public float GetStateDuration(string stateKey)
        {
            if (!TryGetStateDuration(stateKey, out float duration))
            {
                throw new XFrameworkException($"XAnimation state '{stateKey}' does not provide a fixed duration.");
            }

            return duration;
        }

        public bool TryGetStateDuration(string stateKey, out float duration)
        {
            XAnimationCompiledState state = GetState(stateKey);
            float speed = Mathf.Approximately(state.Config.speed, 0f) ? 1f : state.Config.speed;
            switch (state)
            {
                case XAnimationCompiledSingleState singleState:
                {
                    XAnimationCompiledClip clip = (XAnimationCompiledClip)Clips[singleState.ClipIndex];
                    duration = clip.Clip.length / speed;
                    return true;
                }
                case XAnimationCompiledBlend1DState blend1DState:
                {
                    float maxClipLength = 0f;
                    IReadOnlyList<XAnimationCompiledBlend1DSample> samples = blend1DState.Samples;
                    for (int i = 0; i < samples.Count; i++)
                    {
                        XAnimationCompiledBlend1DSample sample = samples[i];
                        XAnimationCompiledClip clip = (XAnimationCompiledClip)Clips[sample.ClipIndex];
                        maxClipLength = Mathf.Max(maxClipLength, clip.Clip.length);
                    }

                    if (maxClipLength <= 0f)
                    {
                        duration = 0f;
                        return false;
                    }

                    duration = maxClipLength / speed;
                    return true;
                }
                case XAnimationCompiledBlend2DSimpleDirectionalState directionalState:
                {
                    float maxClipLength = 0f;
                    IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> samples = directionalState.Samples;
                    for (int i = 0; i < samples.Count; i++)
                    {
                        XAnimationCompiledBlend2DSimpleDirectionalSample sample = samples[i];
                        XAnimationCompiledClip clip = (XAnimationCompiledClip)Clips[sample.ClipIndex];
                        maxClipLength = Mathf.Max(maxClipLength, clip.Clip.length);
                    }

                    if (maxClipLength <= 0f)
                    {
                        duration = 0f;
                        return false;
                    }

                    duration = maxClipLength / speed;
                    return true;
                }
                case XAnimationCompiledBlend2DFreeformDirectionalState directionalState:
                {
                    float maxClipLength = 0f;
                    IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> samples = directionalState.Samples;
                    for (int i = 0; i < samples.Count; i++)
                    {
                        XAnimationCompiledBlend2DSimpleDirectionalSample sample = samples[i];
                        XAnimationCompiledClip clip = (XAnimationCompiledClip)Clips[sample.ClipIndex];
                        maxClipLength = Mathf.Max(maxClipLength, clip.Clip.length);
                    }

                    if (maxClipLength <= 0f)
                    {
                        duration = 0f;
                        return false;
                    }

                    duration = maxClipLength / speed;
                    return true;
                }
                default:
                    duration = 0f;
                    return false;
            }
        }

        public float GetClipDuration(string clipKey)
        {
            if (!TryGetClipDuration(clipKey, out float duration))
            {
                throw new XFrameworkException($"XAnimation clip '{clipKey}' does not exist.");
            }

            return duration;
        }

        public bool TryGetClipDuration(string clipKey, out float duration)
        {
            if (!TryGetClipIndex(clipKey, out int clipIndex))
            {
                duration = 0f;
                return false;
            }

            XAnimationCompiledClip clip = (XAnimationCompiledClip)Clips[clipIndex];
            duration = clip.Clip.length;
            return true;
        }
    }

    [Serializable]
    public class XAnimationTransitionOptions
    {
        public float fadeIn;
        public float fadeOut;
        public float enterTime;
        public int priority;
        public bool interruptible = true;
    }

    public sealed class XAnimationChannelState
    {
        public string channelName;
        public string stateKey;
        public XAnimationStateType stateType;
        public string clipKey;
        public XAnimationBlendClipState[] blendClips;
        public int playbackId;
        public float normalizedTime;
        public float totalNormalizedTime;
        public float weight;
        public float channelWeight;
        public float speed;
        public float timeScale;
        public float blendParameterX;
        public float blendParameterY;
        public bool isLooping;
        public bool isFading;
        public int priority;
        public bool interruptible;
        public bool isTemporaryState;
        public string nextStateKey;
    }

    public enum XAnimationStateExitReason
    {
        Interrupted,
        Completed,
        Stopped,
        Disposed,
    }

    public sealed class XAnimationStateEvent
    {
        public string stateKey;
        public string channelName;
        public int playbackId;
        public bool isTemporaryState;
        public float normalizedTime;
        public float totalNormalizedTime;
        public XAnimationStateExitReason? exitReason;
    }

    public sealed class XAnimationBlendClipState
    {
        public string clipKey;
        public float weight;
        public float normalizedTime;
        public float totalNormalizedTime;
        public float positionX;
        public float positionY;
    }

    public sealed class XAnimationCueEvent
    {
        public int playbackId;
        public string clipKey;
        public string channelName;
        public string eventKey;
        public string payload;
        public float weight;
        public float normalizedTime;
        public int loopCount;
    }
}
