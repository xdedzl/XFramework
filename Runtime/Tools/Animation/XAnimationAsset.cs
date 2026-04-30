using System;
using System.Collections.Generic;
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
        Float,
        Bool,
        Trigger,
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
        public XAnimationBlend1DSampleConfig[] samples = Array.Empty<XAnimationBlend1DSampleConfig>();
    }

    [Serializable]
    public class XAnimationBlend1DSampleConfig
    {
        public string clipKey;
        public float threshold;
    }

    [Serializable]
    public class XAnimationGraphStateConfig
    {
        public string name;
        public string clipKey;
        public string channelName;
    }

    [Serializable]
    public class XAnimationTransitionConfig
    {
        public string fromState;
        public string toState;
        public string parameter;
    }

    [Serializable]
    public class XAnimationStateGraphConfig
    {
        public bool enabled;
        public string entryState;
        public XAnimationGraphStateConfig[] states = Array.Empty<XAnimationGraphStateConfig>();
        public XAnimationTransitionConfig[] transitions = Array.Empty<XAnimationTransitionConfig>();
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
        public XAnimationParameterConfig[] parameters = Array.Empty<XAnimationParameterConfig>();
        public XAnimationCueConfig[] cues = Array.Empty<XAnimationCueConfig>();
        public XAnimationStateGraphConfig graph = new XAnimationStateGraphConfig();
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
        public XAnimationCompiledClip(XAnimationClipConfig config, AnimationClip clip)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Clip = clip ? clip : throw new ArgumentNullException(nameof(clip));
        }

        public XAnimationClipConfig Config { get; }
        public AnimationClip Clip { get; }
        public string Key => Config.key;
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

    public sealed class XAnimationCompiledAsset
    {
        private readonly Dictionary<string, int> m_ChannelIndexByName;
        private readonly Dictionary<string, int> m_ClipIndexByKey;
        private readonly Dictionary<string, int> m_ParameterIndexByName;
        private readonly Dictionary<string, int> m_StateIndexByKey;

        public XAnimationCompiledAsset(
            XAnimationAsset asset,
            XAnimationCompiledChannel[] channels,
            XAnimationCompiledClip[] clips,
            XAnimationCompiledState[] states,
            XAnimationCompiledParameter[] parameters,
            Dictionary<string, List<XAnimationCompiledCue>> cuesByClipKey,
            Dictionary<string, int> channelIndexByName,
            Dictionary<string, int> clipIndexByKey,
            Dictionary<string, int> parameterIndexByName,
            Dictionary<string, int> stateIndexByKey)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            Channels = channels ?? Array.Empty<XAnimationCompiledChannel>();
            Clips = clips ?? Array.Empty<XAnimationCompiledClip>();
            States = states ?? Array.Empty<XAnimationCompiledState>();
            Parameters = parameters ?? Array.Empty<XAnimationCompiledParameter>();
            CuesByClipKey = cuesByClipKey ?? new Dictionary<string, List<XAnimationCompiledCue>>(StringComparer.Ordinal);
            m_ChannelIndexByName = channelIndexByName ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_ClipIndexByKey = clipIndexByKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_ParameterIndexByName = parameterIndexByName ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_StateIndexByKey = stateIndexByKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public XAnimationAsset Asset { get; }
        public IReadOnlyList<XAnimationCompiledChannel> Channels { get; }
        public IReadOnlyList<XAnimationCompiledClip> Clips { get; }
        public IReadOnlyList<XAnimationCompiledState> States { get; }
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
    }

    [Serializable]
    public class XAnimationPlayTarget
    {
        public string stateKey;
        public string clipKey;
        public string channelName;
    }

    [Serializable]
    public class XAnimationTransitionOptions
    {
        public float fadeIn;
        public float fadeOut;
        public int priority;
        public bool interruptible = true;
    }

    [Serializable]
    public class XAnimationPlaybackOptions
    {
        public float weight = 1f;
        public float normalizedTime;
        public float speed = 1f;
        public bool? loopOverride;
        public bool? rootMotionOverride;
    }

    [Serializable]
    public class XAnimationPlayCommand
    {
        public XAnimationPlayTarget target = new();
        public XAnimationTransitionOptions transition = new();
        public XAnimationPlaybackOptions playback = new();
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
        public bool isLooping;
        public bool isFading;
        public int priority;
        public bool interruptible;
    }

    public sealed class XAnimationBlendClipState
    {
        public string clipKey;
        public float weight;
        public float normalizedTime;
        public float totalNormalizedTime;
    }

    public sealed class XAnimationCueEvent
    {
        public int playbackId;
        public string clipKey;
        public string channelName;
        public string eventKey;
        public string payload;
        public float normalizedTime;
        public int loopCount;
    }
}
