using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace XFramework.Animation
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class XAnimationAssetAliasAttribute : Attribute
    {
        public XAnimationAssetAliasAttribute(string alias)
        {
            Alias = alias;
        }

        public string Alias { get; }
    }

    public struct XAnimationMetaInfo
    {
        public string typeAlias;
        public string assetPath;
    }

    public class XAnimationAssetBase
    {
        [JsonProperty]
        private XAnimationMetaInfo m_MetaInfo;

        public string Serialize()
        {
            XAnimationAssetAliasAttribute aliasAttribute = GetType().GetCustomAttribute<XAnimationAssetAliasAttribute>(true);
            m_MetaInfo = new XAnimationMetaInfo
            {
                typeAlias = aliasAttribute?.Alias ?? m_MetaInfo.typeAlias,
                assetPath = m_MetaInfo.assetPath
            };
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

#if UNITY_EDITOR
        public void SetAssetPath(string path)
        {
            m_MetaInfo = new XAnimationMetaInfo
            {
                typeAlias = m_MetaInfo.typeAlias,
                assetPath = path
            };
        }

        public void SaveAsset()
        {
            string json = Serialize();
            string path = m_MetaInfo.assetPath;
            if (string.IsNullOrEmpty(path))
            {
                throw new Exception("Asset path is not set. Please call SetAssetPath before saving.");
            }

            System.IO.File.WriteAllText(path, json);
            UnityEditor.AssetDatabase.Refresh();
        }
#endif
    }

    public static class XAnimationAssetUtility
    {
        public const string AnimationAssetAlias = "xframework.animation.asset";
        public const string AnimationOverrideAlias = "xframework.animation.override";
        public const string AnimationAssetExtension = ".xanimation";
        public const string AnimationOverrideExtension = ".xanimationoverride";

        public static bool TryReadMetaInfo(string text, out XAnimationMetaInfo metaInfo)
        {
            metaInfo = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                JObject root = JObject.Parse(text);
                JToken token = root["m_MetaInfo"];
                if (token == null || token.Type != JTokenType.Object)
                {
                    return false;
                }

                XAnimationMetaInfo parsedInfo = token.ToObject<XAnimationMetaInfo>();
                metaInfo = parsedInfo;
                return !string.IsNullOrWhiteSpace(parsedInfo.typeAlias);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static T ToXAnimationAsset<T>(this TextAsset textAsset) where T : XAnimationAssetBase
        {
            T asset = JsonConvert.DeserializeObject<T>(textAsset.text);
#if UNITY_EDITOR
            if (asset != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(textAsset);
                asset.SetAssetPath(path);
            }
#endif
            return asset;
        }

        public static T ToXAnimationAsset<T>(this TextAsset textAsset, Type type) where T : XAnimationAssetBase
        {
            object deserialized = JsonConvert.DeserializeObject(textAsset.text, type);
            T asset = deserialized as T;
#if UNITY_EDITOR
            if (asset != null)
            {
                string path = UnityEditor.AssetDatabase.GetAssetPath(textAsset);
                asset.SetAssetPath(path);
            }
#endif
            return asset;
        }

        public static bool IsAnimationAssetExtension(string assetPath)
        {
            return HasExtension(assetPath, AnimationAssetExtension) ||
                   HasExtension(assetPath, AnimationOverrideExtension);
        }

        public static bool HasExtension(string assetPath, string extension)
        {
            return string.Equals(
                System.IO.Path.GetExtension(assetPath),
                extension,
                StringComparison.OrdinalIgnoreCase);
        }
    }

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
        public string editorGroupName;
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
        public string editorGroupName;
        public XAnimationStateType stateType = XAnimationStateType.Single;
        public string clipKey;
        public string channelName;
        public string[] allowedNextStateKeys = Array.Empty<string>();
        public string[] allowedPreviousStateKeys = Array.Empty<string>();
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
    public class XAnimationTransitionPairConfig
    {
        public string preStateKey;
        public string nextStateKey;
    }

    [Serializable]
    public class XAnimationDefaultTransitionConfig
    {
        public string editorName;
        public XAnimationTransitionPairConfig[] pairs = Array.Empty<XAnimationTransitionPairConfig>();
        public float fadeIn;
        public float fadeOut;
        public float enterTime;
        public int priority;
        public bool interruptible = true;
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
    [XAnimationAssetAlias(XAnimationAssetUtility.AnimationAssetAlias)]
    public class XAnimationAsset : XAnimationAssetBase
    {
        public string alias;
        public string DefaultPrefabPath;
        public bool preload;
        public bool rootMotion;
        public XAnimationChannelConfig[] channels = Array.Empty<XAnimationChannelConfig>();
        public XAnimationClipConfig[] clips = Array.Empty<XAnimationClipConfig>();
        public XAnimationStateConfig[] states = Array.Empty<XAnimationStateConfig>();
        public XAnimationAutoTransitionConfig[] autoTransitions = Array.Empty<XAnimationAutoTransitionConfig>();
        public XAnimationDefaultTransitionConfig[] defaultTransitions = Array.Empty<XAnimationDefaultTransitionConfig>();
        public XAnimationParameterConfig[] parameters = Array.Empty<XAnimationParameterConfig>();
        public XAnimationCueConfig[] cues = Array.Empty<XAnimationCueConfig>();
    }

    [Serializable]
    public class XAnimationOverrideClipConfig
    {
        public string key;
        public string clipPath;
    }

    [Serializable]
    [XAnimationAssetAlias(XAnimationAssetUtility.AnimationOverrideAlias)]
    public class XAnimationOverrideAsset : XAnimationAssetBase
    {
        public string baseAssetPath;
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
        private static readonly Dictionary<int, AnimationClip> s_RuntimePlaybackClipCache = new();

        private readonly IXAnimationAssetResolver m_Resolver;
        private readonly bool m_ClearUnityAnimationEvents;
        private AnimationClip m_Clip;
        private AnimationClip m_PlaybackClip;

        public XAnimationCompiledClip(XAnimationClipConfig config, IXAnimationAssetResolver resolver)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            m_Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            m_ClearUnityAnimationEvents = true;
        }

        public XAnimationCompiledClip(
            XAnimationClipConfig config,
            AnimationClip clip,
            AnimationClip playbackClip = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            m_Clip = clip ? clip : throw new ArgumentNullException(nameof(clip));
            m_PlaybackClip = playbackClip ? playbackClip : m_Clip;
            m_ClearUnityAnimationEvents = false;
        }

        public XAnimationClipConfig Config { get; }
        public AnimationClip Clip => LoadClip();
        public AnimationClip PlaybackClip => LoadPlaybackClip();
        public string Key => Config.key;
        public string ClipPath => Config.clipPath;

        public void Preload()
        {
            _ = PlaybackClip;
        }

        private AnimationClip LoadClip()
        {
            if (m_Clip)
            {
                return m_Clip;
            }

            if (m_Resolver == null)
            {
                throw new XAnimationException($"XAnimation clip '{Key}' has no asset resolver.");
            }

            AnimationClip clip = m_Resolver.LoadAnimationClip(Config.clipPath);
            if (clip == null)
            {
                string message = $"XAnimation clip '{Key}' failed to load AnimationClip at '{Config.clipPath}'.";
                Debug.LogError($"[XFramework] {message}");
                throw new XAnimationException(message);
            }

            m_Clip = clip;
            return m_Clip;
        }

        private AnimationClip LoadPlaybackClip()
        {
            if (m_PlaybackClip)
            {
                return m_PlaybackClip;
            }

            AnimationClip clip = LoadClip();
            m_PlaybackClip = m_ClearUnityAnimationEvents ? CreatePlaybackClip(clip) : clip;
            return m_PlaybackClip;
        }

        private static AnimationClip CreatePlaybackClip(AnimationClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            AnimationEvent[] events = clip.events;
            if (events == null || events.Length == 0)
            {
                return clip;
            }

            int instanceId = clip.GetInstanceID();
            if (s_RuntimePlaybackClipCache.TryGetValue(instanceId, out AnimationClip cachedClip) && cachedClip != null)
            {
                return cachedClip;
            }

            AnimationClip playbackClip = UnityEngine.Object.Instantiate(clip);
            playbackClip.name = $"{clip.name}_XAnimationRuntime";
            ClearAnimationEvents(playbackClip);
            playbackClip.hideFlags = HideFlags.HideAndDontSave;
            s_RuntimePlaybackClipCache[instanceId] = playbackClip;
            return playbackClip;
        }

        private static void ClearAnimationEvents(AnimationClip clip)
        {
            if (clip == null)
            {
                return;
            }

#if UNITY_EDITOR
            Type animationUtilityType = Type.GetType("UnityEditor.AnimationUtility, UnityEditor");
            MethodInfo setAnimationEventsMethod = animationUtilityType?.GetMethod(
                "SetAnimationEvents",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(AnimationClip), typeof(AnimationEvent[]) },
                null);
            if (setAnimationEventsMethod != null)
            {
                setAnimationEventsMethod.Invoke(null, new object[] { clip, Array.Empty<AnimationEvent>() });
                return;
            }
#endif

            clip.events = Array.Empty<AnimationEvent>();
        }
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

    public sealed class XAnimationCompiledDefaultTransition
    {
        public XAnimationCompiledDefaultTransition(XAnimationDefaultTransitionConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public XAnimationDefaultTransitionConfig Config { get; }
        public string EditorName => Config.editorName;
        public IReadOnlyList<XAnimationTransitionPairConfig> Pairs => Config.pairs ?? Array.Empty<XAnimationTransitionPairConfig>();

        public XAnimationTransitionOptions CreateTransitionOptions()
        {
            return new XAnimationTransitionOptions
            {
                fadeIn = Config.fadeIn,
                fadeOut = Config.fadeOut,
                enterTime = Config.enterTime,
                priority = Config.priority,
                interruptible = Config.interruptible,
            };
        }
    }

    public enum XAnimationTransitionRequestSource
    {
        ExplicitPlay,
        DefaultTransition,
        AutoTransition,
    }

    public enum XAnimationTransitionRejectReason
    {
        None = 0,
        ChannelDisallowInterrupt = 1,
        CurrentUninterruptible = 2,
        LowerPriority = 3,
        SourceStateDisallowTarget = 4,
        TargetStateDisallowSource = 5,
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
        public virtual XAnimationCompiledClip DirectClip => null;
        public abstract IReadOnlyList<int> ClipIndices { get; }
    }

    public sealed class XAnimationCompiledSingleState : XAnimationCompiledState
    {
        private readonly int[] m_ClipIndices;

        public XAnimationCompiledSingleState(XAnimationStateConfig config, int defaultChannelIndex, int clipIndex)
            : base(config, defaultChannelIndex)
        {
            ClipIndex = clipIndex;
            m_ClipIndices = new[] { clipIndex };
        }

        public XAnimationCompiledSingleState(XAnimationStateConfig config, int defaultChannelIndex, XAnimationCompiledClip directClip)
            : base(config, defaultChannelIndex)
        {
            DirectClip = directClip ?? throw new ArgumentNullException(nameof(directClip));
            ClipIndex = -1;
            m_ClipIndices = Array.Empty<int>();
        }

        public int ClipIndex { get; }
        public override XAnimationCompiledClip DirectClip { get; }
        public override IReadOnlyList<int> ClipIndices => m_ClipIndices;
        public bool HasDirectClip => DirectClip != null;
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
        private readonly int[] m_ClipIndices;

        public XAnimationCompiledBlend1DState(
            XAnimationStateConfig config,
            int defaultChannelIndex,
            int parameterIndex,
            XAnimationCompiledBlend1DSample[] samples)
            : base(config, defaultChannelIndex)
        {
            ParameterIndex = parameterIndex;
            Samples = samples ?? Array.Empty<XAnimationCompiledBlend1DSample>();
            m_ClipIndices = new int[Samples.Count];
            for (int i = 0; i < Samples.Count; i++)
            {
                m_ClipIndices[i] = Samples[i].ClipIndex;
            }
        }

        public int ParameterIndex { get; }
        public IReadOnlyList<XAnimationCompiledBlend1DSample> Samples { get; }
        public override IReadOnlyList<int> ClipIndices => m_ClipIndices;
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
        private readonly int[] m_ClipIndices;

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
            m_ClipIndices = new int[Samples.Count];
            for (int i = 0; i < Samples.Count; i++)
            {
                m_ClipIndices[i] = Samples[i].ClipIndex;
            }
        }

        public int ParameterXIndex { get; }
        public int ParameterYIndex { get; }
        public IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> Samples { get; }
        public override IReadOnlyList<int> ClipIndices => m_ClipIndices;
    }

    public sealed class XAnimationCompiledBlend2DFreeformDirectionalState : XAnimationCompiledState
    {
        private readonly int[] m_ClipIndices;

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
            m_ClipIndices = new int[Samples.Count];
            for (int i = 0; i < Samples.Count; i++)
            {
                m_ClipIndices[i] = Samples[i].ClipIndex;
            }
        }

        public int ParameterXIndex { get; }
        public int ParameterYIndex { get; }
        public IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> Samples { get; }
        public override IReadOnlyList<int> ClipIndices => m_ClipIndices;
    }

    public sealed class XAnimationCompiledAsset
    {
        private readonly Dictionary<string, int> m_ChannelIndexByName;
        private readonly Dictionary<string, int> m_ClipIndexByKey;
        private readonly Dictionary<string, int> m_ParameterIndexByName;
        private readonly Dictionary<string, int> m_StateIndexByKey;
        private readonly Dictionary<string, int> m_AutoTransitionIndexByPreStateKey;
        private readonly Dictionary<string, int> m_DefaultTransitionIndexByPairKey;

        public XAnimationCompiledAsset(
            XAnimationAsset asset,
            XAnimationCompiledChannel[] channels,
            XAnimationCompiledClip[] clips,
            XAnimationCompiledState[] states,
            XAnimationCompiledAutoTransition[] autoTransitions,
            XAnimationCompiledDefaultTransition[] defaultTransitions,
            XAnimationCompiledParameter[] parameters,
            Dictionary<string, List<XAnimationCompiledCue>> cuesByClipKey,
            Dictionary<string, int> channelIndexByName,
            Dictionary<string, int> clipIndexByKey,
            Dictionary<string, int> parameterIndexByName,
            Dictionary<string, int> stateIndexByKey,
            Dictionary<string, int> autoTransitionIndexByPreStateKey,
            Dictionary<string, int> defaultTransitionIndexByPairKey)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            Channels = channels ?? Array.Empty<XAnimationCompiledChannel>();
            Clips = clips ?? Array.Empty<XAnimationCompiledClip>();
            States = states ?? Array.Empty<XAnimationCompiledState>();
            AutoTransitions = autoTransitions ?? Array.Empty<XAnimationCompiledAutoTransition>();
            DefaultTransitions = defaultTransitions ?? Array.Empty<XAnimationCompiledDefaultTransition>();
            Parameters = parameters ?? Array.Empty<XAnimationCompiledParameter>();
            CuesByClipKey = cuesByClipKey ?? new Dictionary<string, List<XAnimationCompiledCue>>(StringComparer.Ordinal);
            m_ChannelIndexByName = channelIndexByName ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_ClipIndexByKey = clipIndexByKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_ParameterIndexByName = parameterIndexByName ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_StateIndexByKey = stateIndexByKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_AutoTransitionIndexByPreStateKey = autoTransitionIndexByPreStateKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
            m_DefaultTransitionIndexByPairKey = defaultTransitionIndexByPairKey ?? new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public XAnimationAsset Asset { get; }
        public IReadOnlyList<XAnimationCompiledChannel> Channels { get; }
        public IReadOnlyList<XAnimationCompiledClip> Clips { get; }
        public IReadOnlyList<XAnimationCompiledState> States { get; }
        public IReadOnlyList<XAnimationCompiledAutoTransition> AutoTransitions { get; }
        public IReadOnlyList<XAnimationCompiledDefaultTransition> DefaultTransitions { get; }
        public IReadOnlyList<XAnimationCompiledParameter> Parameters { get; }
        public IReadOnlyDictionary<string, List<XAnimationCompiledCue>> CuesByClipKey { get; }
        public bool RootMotionEnabled => Asset.rootMotion;

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

        public bool TryGetDefaultTransition(
            string preStateKey,
            string nextStateKey,
            out XAnimationCompiledDefaultTransition transition)
        {
            if (string.IsNullOrWhiteSpace(preStateKey) ||
                string.IsNullOrWhiteSpace(nextStateKey) ||
                !m_DefaultTransitionIndexByPairKey.TryGetValue(BuildTransitionPairKey(preStateKey, nextStateKey), out int transitionIndex))
            {
                transition = null;
                return false;
            }

            transition = DefaultTransitions[transitionIndex];
            return true;
        }

        public XAnimationCompiledChannel GetChannel(string channelName)
        {
            if (!TryGetChannelIndex(channelName, out int channelIndex))
            {
                throw new XAnimationException($"XAnimation channel '{channelName}' does not exist.");
            }

            return Channels[channelIndex];
        }

        public XAnimationCompiledClip GetClip(string clipKey)
        {
            if (!TryGetClipIndex(clipKey, out int clipIndex))
            {
                throw new XAnimationException($"XAnimation clip '{clipKey}' does not exist.");
            }

            return Clips[clipIndex];
        }

        public void PreloadAll()
        {
            for (int i = 0; i < Clips.Count; i++)
            {
                PreloadClipAtIndex(i);
            }
        }

        public void PreloadState(string stateKey)
        {
            XAnimationCompiledState state = GetState(stateKey);
            if (state.DirectClip != null)
            {
                state.DirectClip.Preload();
                return;
            }

            IReadOnlyList<int> clipIndices = state.ClipIndices;
            for (int i = 0; i < clipIndices.Count; i++)
            {
                PreloadClipAtIndex(clipIndices[i]);
            }
        }

        public XAnimationCompiledParameter GetParameter(string parameterName)
        {
            if (!TryGetParameterIndex(parameterName, out int parameterIndex))
            {
                throw new XAnimationException($"XAnimation parameter '{parameterName}' does not exist.");
            }

            return Parameters[parameterIndex];
        }

        public XAnimationCompiledState GetState(string stateKey)
        {
            if (!TryGetStateIndex(stateKey, out int stateIndex))
            {
                throw new XAnimationException($"XAnimation state '{stateKey}' does not exist.");
            }

            return States[stateIndex];
        }

        public XAnimationCompiledAutoTransition GetAutoTransition(string preStateKey)
        {
            if (!TryGetAutoTransition(preStateKey, out XAnimationCompiledAutoTransition transition))
            {
                throw new XAnimationException($"XAnimation auto transition for state '{preStateKey}' does not exist.");
            }

            return transition;
        }

        public static string BuildTransitionPairKey(string preStateKey, string nextStateKey)
        {
            return $"{preStateKey}\u001F{nextStateKey}";
        }

        public float GetStateDuration(string stateKey)
        {
            if (!TryGetStateDuration(stateKey, out float duration))
            {
                throw new XAnimationException($"XAnimation state '{stateKey}' does not provide a fixed duration.");
            }

            return duration;
        }

        public bool TryGetStateDuration(string stateKey, out float duration)
        {
            XAnimationCompiledState state = GetState(stateKey);
            float speed = Mathf.Approximately(state.Config.speed, 0f) ? 1f : state.Config.speed;
            float maxClipLength = 0f;

            if (state.DirectClip != null)
            {
                maxClipLength = state.DirectClip.Clip.length;
            }
            else
            {
                IReadOnlyList<int> clipIndices = state.ClipIndices;
                for (int i = 0; i < clipIndices.Count; i++)
                {
                    XAnimationCompiledClip clip = (XAnimationCompiledClip)Clips[clipIndices[i]];
                    maxClipLength = Mathf.Max(maxClipLength, clip.Clip.length);
                }
            }

            if (maxClipLength <= 0f)
            {
                duration = 0f;
                return false;
            }

            duration = maxClipLength / speed;
            return true;
        }

        public float GetClipDuration(string clipKey)
        {
            if (!TryGetClipDuration(clipKey, out float duration))
            {
                throw new XAnimationException($"XAnimation clip '{clipKey}' does not exist.");
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

        private void PreloadClipAtIndex(int clipIndex)
        {
            XAnimationCompiledClip clip = (XAnimationCompiledClip)Clips[clipIndex];
            clip.Preload();
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

    internal sealed class XAnimationTransitionRequest
    {
        public XAnimationTransitionRequest(
            string channelName,
            string targetStateKey,
            string targetClipKey,
            XAnimationTransitionRequestSource source,
            float fadeIn,
            float fadeOut,
            float enterTime,
            float speed,
            bool isLooping,
            int priority,
            bool interruptible,
            bool drivesRootMotion,
            bool force)
        {
            ChannelName = channelName ?? string.Empty;
            TargetStateKey = targetStateKey ?? string.Empty;
            TargetClipKey = targetClipKey ?? string.Empty;
            Source = source;
            FadeIn = Mathf.Max(0f, fadeIn);
            FadeOut = Mathf.Max(0f, fadeOut);
            EnterTime = Mathf.Clamp01(enterTime);
            Speed = speed;
            IsLooping = isLooping;
            Priority = priority;
            Interruptible = interruptible;
            DrivesRootMotion = drivesRootMotion;
            Force = force;
        }

        public string ChannelName { get; }
        public string TargetStateKey { get; }
        public string TargetClipKey { get; }
        public XAnimationTransitionRequestSource Source { get; }
        public float FadeIn { get; }
        public float FadeOut { get; }
        public float EnterTime { get; }
        public float Speed { get; }
        public bool IsLooping { get; }
        public int Priority { get; }
        public bool Interruptible { get; }
        public bool DrivesRootMotion { get; }
        public bool Force { get; }

        public XAnimationPlaybackRuntimeOptions CreateRuntimeOptions(bool skipFadeIn)
        {
            return new XAnimationPlaybackRuntimeOptions(
                skipFadeIn ? 0f : FadeIn,
                FadeOut,
                1f,
                EnterTime,
                Speed,
                IsLooping,
                Priority,
                Interruptible,
                DrivesRootMotion,
                Source);
        }
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
        public bool isTransitioning;
        public string previousStateKey;
        public int previousPlaybackId;
        public XAnimationTransitionRequestSource transitionSource;
        public string transitionTargetStateKey;
        public XAnimationTransitionRejectReason lastRejectReason;
        public string lastRejectedStateKey;
        public string lastRejectedClipKey;
        public int lastRejectedPriority;
        public XAnimationTransitionRequestSource lastRejectedSource;
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

    public sealed class XAnimationRootMotionEvent
    {
        public string channelName;
        public int playbackId;
        public string stateKey;
        public Vector3 deltaPosition;
        public Quaternion deltaRotation = Quaternion.identity;
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
