using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace XFramework.Animation
{
    public sealed class XAnimationAssetLoader
    {
        private readonly XAnimationAssetValidator m_Validator = new();
        private readonly IXAnimationAssetResolver m_Resolver;

        public XAnimationAssetLoader()
            : this(new XAnimationRuntimeAssetResolver())
        {
        }

        public XAnimationAssetLoader(IXAnimationAssetResolver resolver)
        {
            m_Resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public XAnimationCompiledAsset Load(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new XAnimationException("XAnimation assetPath cannot be empty.");
            }

            TextAsset textAsset = m_Resolver.LoadTextAsset(assetPath);
            if (textAsset == null)
            {
                throw new XAnimationException($"XAnimation asset missing at '{assetPath}'.");
            }

            XAnimationAsset asset = LoadAsset(textAsset, assetPath);
            return Compile(asset);
        }

        public XAnimationCompiledAsset Load(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                throw new XAnimationException("XAnimation TextAsset cannot be null.");
            }

            XAnimationAsset asset = LoadAsset(textAsset, textAsset.name);
            return Compile(asset);
        }

        public static bool IsXAnimationAssetText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                JObject json = JObject.Parse(text);
                if (json["baseAssetPath"] != null)
                {
                    return true;
                }

                return json["channels"] is JArray && json["clips"] is JArray;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public XAnimationCompiledAsset Compile(XAnimationAsset asset)
        {
            NormalizeStateTransitionGateValues(asset);
            NormalizeAutoTransitionValues(asset);
            NormalizeDefaultTransitionValues(asset);
            m_Validator.Validate(asset);

            XAnimationCompiledChannel[] compiledChannels = new XAnimationCompiledChannel[asset.channels.Length];
            Dictionary<string, int> channelIndexByName = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channelConfig = asset.channels[i];
                AvatarMask mask = null;
                if (!string.IsNullOrWhiteSpace(channelConfig.maskPath))
                {
                    mask = m_Resolver.LoadAvatarMask(channelConfig.maskPath);
                    if (mask == null)
                    {
                        throw new XAnimationException($"XAnimation channel '{channelConfig.name}' failed to load AvatarMask at '{channelConfig.maskPath}'.");
                    }
                }

                compiledChannels[i] = new XAnimationCompiledChannel(channelConfig, mask, i);
                channelIndexByName[channelConfig.name] = i;
            }

            XAnimationCompiledClip[] compiledClips = new XAnimationCompiledClip[asset.clips.Length];
            Dictionary<string, int> clipIndexByKey = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.clips.Length; i++)
            {
                XAnimationClipConfig clipConfig = asset.clips[i];
                compiledClips[i] = new XAnimationCompiledClip(clipConfig, m_Resolver);
                clipIndexByKey[clipConfig.key] = i;
            }

            XAnimationCompiledParameter[] compiledParameters = new XAnimationCompiledParameter[asset.parameters.Length];
            Dictionary<string, int> parameterIndexByName = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.parameters.Length; i++)
            {
                XAnimationCompiledParameter parameter = new(asset.parameters[i], i);
                compiledParameters[i] = parameter;
                parameterIndexByName[parameter.Name] = i;
            }

            XAnimationCompiledState[] compiledStates = new XAnimationCompiledState[asset.states.Length];
            Dictionary<string, int> stateIndexByKey = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig stateConfig = asset.states[i];
                int defaultChannelIndex = channelIndexByName[stateConfig.channelName];
                compiledStates[i] = stateConfig.stateType switch
                {
                    XAnimationStateType.Single => new XAnimationCompiledSingleState(
                        stateConfig,
                        defaultChannelIndex,
                        clipIndexByKey[stateConfig.clipKey]),
                    XAnimationStateType.Blend1D => CompileBlend1DState(
                        stateConfig,
                        defaultChannelIndex,
                        clipIndexByKey,
                        parameterIndexByName),
                    XAnimationStateType.Blend2DSimpleDirectional => CompileBlend2DSimpleDirectionalState(
                        stateConfig,
                        defaultChannelIndex,
                        clipIndexByKey,
                        parameterIndexByName),
                    XAnimationStateType.Blend2DFreeformDirectional => CompileBlend2DFreeformDirectionalState(
                        stateConfig,
                        defaultChannelIndex,
                        clipIndexByKey,
                        parameterIndexByName),
                    _ => throw new XAnimationException($"XAnimation state '{stateConfig.key}' has unsupported stateType '{stateConfig.stateType}'."),
                };
                stateIndexByKey[stateConfig.key] = i;
            }

            XAnimationAutoTransitionConfig[] autoTransitionConfigs = asset.autoTransitions ?? Array.Empty<XAnimationAutoTransitionConfig>();
            XAnimationCompiledAutoTransition[] compiledAutoTransitions = new XAnimationCompiledAutoTransition[autoTransitionConfigs.Length];
            Dictionary<string, int> autoTransitionIndexByPreStateKey = new(StringComparer.Ordinal);
            for (int i = 0; i < autoTransitionConfigs.Length; i++)
            {
                XAnimationAutoTransitionConfig autoTransitionConfig = autoTransitionConfigs[i];
                compiledAutoTransitions[i] = new XAnimationCompiledAutoTransition(autoTransitionConfig);
                autoTransitionIndexByPreStateKey[autoTransitionConfig.preStateKey] = i;
            }

            XAnimationDefaultTransitionConfig[] defaultTransitionConfigs = asset.defaultTransitions ?? Array.Empty<XAnimationDefaultTransitionConfig>();
            XAnimationCompiledDefaultTransition[] compiledDefaultTransitions = new XAnimationCompiledDefaultTransition[defaultTransitionConfigs.Length];
            Dictionary<string, int> defaultTransitionIndexByPairKey = new(StringComparer.Ordinal);
            for (int i = 0; i < defaultTransitionConfigs.Length; i++)
            {
                XAnimationDefaultTransitionConfig defaultTransitionConfig = defaultTransitionConfigs[i];
                compiledDefaultTransitions[i] = new XAnimationCompiledDefaultTransition(defaultTransitionConfig);
                XAnimationTransitionPairConfig[] pairs = defaultTransitionConfig.pairs ?? Array.Empty<XAnimationTransitionPairConfig>();
                for (int pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
                {
                    XAnimationTransitionPairConfig pair = pairs[pairIndex];
                    string pairKey = XAnimationCompiledAsset.BuildTransitionPairKey(pair.preStateKey, pair.nextStateKey);
                    defaultTransitionIndexByPairKey[pairKey] = i;
                }
            }

            Dictionary<string, List<XAnimationCompiledCue>> cuesByClipKey = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.cues.Length; i++)
            {
                XAnimationCueConfig cueConfig = asset.cues[i];
                if (!cuesByClipKey.TryGetValue(cueConfig.clipKey, out List<XAnimationCompiledCue> compiledCues))
                {
                    compiledCues = new List<XAnimationCompiledCue>();
                    cuesByClipKey.Add(cueConfig.clipKey, compiledCues);
                }

                compiledCues.Add(new XAnimationCompiledCue(cueConfig, i));
            }

            foreach (List<XAnimationCompiledCue> cueList in cuesByClipKey.Values)
            {
                cueList.Sort((left, right) => left.Config.time.CompareTo(right.Config.time));
            }

            return new XAnimationCompiledAsset(
                asset,
                compiledChannels,
                compiledClips,
                compiledStates,
                compiledAutoTransitions,
                compiledDefaultTransitions,
                compiledParameters,
                cuesByClipKey,
                channelIndexByName,
                clipIndexByKey,
                parameterIndexByName,
                stateIndexByKey,
                autoTransitionIndexByPreStateKey,
                defaultTransitionIndexByPairKey);
        }

        private static XAnimationCompiledBlend1DState CompileBlend1DState(
            XAnimationStateConfig stateConfig,
            int defaultChannelIndex,
            IReadOnlyDictionary<string, int> clipIndexByKey,
            IReadOnlyDictionary<string, int> parameterIndexByName)
        {
            XAnimationBlend1DSampleConfig[] samples = stateConfig.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
            XAnimationCompiledBlend1DSample[] compiledSamples = new XAnimationCompiledBlend1DSample[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                compiledSamples[i] = new XAnimationCompiledBlend1DSample(samples[i], clipIndexByKey[samples[i].clipKey]);
            }

            return new XAnimationCompiledBlend1DState(
                stateConfig,
                defaultChannelIndex,
                parameterIndexByName[stateConfig.parameterName],
                compiledSamples);
        }

        private static XAnimationCompiledBlend2DSimpleDirectionalState CompileBlend2DSimpleDirectionalState(
            XAnimationStateConfig stateConfig,
            int defaultChannelIndex,
            IReadOnlyDictionary<string, int> clipIndexByKey,
            IReadOnlyDictionary<string, int> parameterIndexByName)
        {
            XAnimationBlend2DSimpleDirectionalSampleConfig[] samples =
                stateConfig.directionalSamples ?? Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            XAnimationCompiledBlend2DSimpleDirectionalSample[] compiledSamples =
                new XAnimationCompiledBlend2DSimpleDirectionalSample[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                compiledSamples[i] = new XAnimationCompiledBlend2DSimpleDirectionalSample(
                    samples[i],
                    clipIndexByKey[samples[i].clipKey]);
            }

            return new XAnimationCompiledBlend2DSimpleDirectionalState(
                stateConfig,
                defaultChannelIndex,
                parameterIndexByName[stateConfig.parameterXName],
                parameterIndexByName[stateConfig.parameterYName],
                compiledSamples);
        }

        private static XAnimationCompiledBlend2DFreeformDirectionalState CompileBlend2DFreeformDirectionalState(
            XAnimationStateConfig stateConfig,
            int defaultChannelIndex,
            IReadOnlyDictionary<string, int> clipIndexByKey,
            IReadOnlyDictionary<string, int> parameterIndexByName)
        {
            XAnimationBlend2DSimpleDirectionalSampleConfig[] samples =
                stateConfig.directionalSamples ?? Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            XAnimationCompiledBlend2DSimpleDirectionalSample[] compiledSamples =
                new XAnimationCompiledBlend2DSimpleDirectionalSample[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                compiledSamples[i] = new XAnimationCompiledBlend2DSimpleDirectionalSample(
                    samples[i],
                    clipIndexByKey[samples[i].clipKey]);
            }

            return new XAnimationCompiledBlend2DFreeformDirectionalState(
                stateConfig,
                defaultChannelIndex,
                parameterIndexByName[stateConfig.parameterXName],
                parameterIndexByName[stateConfig.parameterYName],
                compiledSamples);
        }

        private XAnimationAsset LoadAsset(TextAsset textAsset, string assetPath)
        {
            if (IsOverrideAssetText(textAsset.text))
            {
                return LoadOverrideAsset(textAsset, assetPath);
            }

            XAnimationAsset asset = textAsset.ToXAnimationAsset<XAnimationAsset>();
            if (asset == null)
            {
                throw new XAnimationException($"Failed to deserialize XAnimation asset at '{assetPath}'.");
            }

            return asset;
        }

        private XAnimationAsset LoadOverrideAsset(TextAsset textAsset, string assetPath)
        {
            XAnimationOverrideAsset overrideAsset = textAsset.ToXAnimationAsset<XAnimationOverrideAsset>();
            if (overrideAsset == null)
            {
                throw new XAnimationException($"Failed to deserialize XAnimation override asset at '{assetPath}'.");
            }

            ValidateOverrideAsset(overrideAsset, assetPath);

            TextAsset baseTextAsset = m_Resolver.LoadTextAsset(overrideAsset.baseAssetPath);
            if (baseTextAsset == null)
            {
                throw new XAnimationException($"XAnimation override '{assetPath}' base asset missing at '{overrideAsset.baseAssetPath}'.");
            }

            if (IsOverrideAssetText(baseTextAsset.text))
            {
                throw new XAnimationException($"XAnimation override '{assetPath}' baseAssetPath must reference a base XAnimationAsset, not another override asset.");
            }

            XAnimationAsset baseAsset = baseTextAsset.ToXAnimationAsset<XAnimationAsset>();
            if (baseAsset == null)
            {
                throw new XAnimationException($"Failed to deserialize XAnimation override base asset at '{overrideAsset.baseAssetPath}'.");
            }

            m_Validator.Validate(baseAsset);
            XAnimationAsset mergedAsset = CloneAsset(baseAsset);
            if (mergedAsset == null)
            {
                throw new XAnimationException($"Failed to clone XAnimation override base asset at '{overrideAsset.baseAssetPath}'.");
            }

            ApplyOverrideClips(mergedAsset, overrideAsset, assetPath);
            return mergedAsset;
        }

        private static bool IsOverrideAssetText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                JObject json = JObject.Parse(text);
                return json["baseAssetPath"] != null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static XAnimationAsset CloneAsset(XAnimationAsset asset)
        {
            string json = JsonConvert.SerializeObject(asset);
            return JsonConvert.DeserializeObject<XAnimationAsset>(json);
        }

        private static void NormalizeAutoTransitionValues(XAnimationAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            asset.autoTransitions ??= Array.Empty<XAnimationAutoTransitionConfig>();
            for (int i = 0; i < asset.autoTransitions.Length; i++)
            {
                XAnimationAutoTransitionConfig transition = asset.autoTransitions[i];
                if (transition == null)
                {
                    continue;
                }

                transition.preStateKey = transition.preStateKey?.Trim();
                transition.nextStateKey = string.IsNullOrWhiteSpace(transition.nextStateKey)
                    ? string.Empty
                    : transition.nextStateKey.Trim();
                transition.transitionDuration = Mathf.Max(0f, transition.transitionDuration);
            }
        }

        private static void NormalizeStateTransitionGateValues(XAnimationAsset asset)
        {
            if (asset?.states == null)
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

                state.allowedNextStateKeys = NormalizeStateKeyList(state.allowedNextStateKeys);
                state.allowedPreviousStateKeys = NormalizeStateKeyList(state.allowedPreviousStateKeys);
            }
        }

        private static string[] NormalizeStateKeyList(string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            List<string> normalized = new(values.Length);
            HashSet<string> unique = new(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i]?.Trim();
                if (string.IsNullOrWhiteSpace(value) || !unique.Add(value))
                {
                    continue;
                }

                normalized.Add(value);
            }

            return normalized.Count == 0 ? Array.Empty<string>() : normalized.ToArray();
        }

        private static void NormalizeDefaultTransitionValues(XAnimationAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            asset.defaultTransitions ??= Array.Empty<XAnimationDefaultTransitionConfig>();
            for (int i = 0; i < asset.defaultTransitions.Length; i++)
            {
                XAnimationDefaultTransitionConfig transition = asset.defaultTransitions[i];
                if (transition == null)
                {
                    continue;
                }

                transition.editorName = transition.editorName?.Trim() ?? string.Empty;
                transition.pairs ??= Array.Empty<XAnimationTransitionPairConfig>();
                transition.fadeIn = Mathf.Max(0f, transition.fadeIn);
                transition.fadeOut = Mathf.Max(0f, transition.fadeOut);
                transition.enterTime = Mathf.Clamp01(transition.enterTime);
                for (int pairIndex = 0; pairIndex < transition.pairs.Length; pairIndex++)
                {
                    XAnimationTransitionPairConfig pair = transition.pairs[pairIndex];
                    if (pair == null)
                    {
                        continue;
                    }

                    pair.preStateKey = pair.preStateKey?.Trim();
                    pair.nextStateKey = pair.nextStateKey?.Trim();
                }
            }
        }

        private static void ValidateOverrideAsset(XAnimationOverrideAsset overrideAsset, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                throw new XAnimationException($"XAnimation override '{assetPath}' baseAssetPath cannot be empty.");
            }

            HashSet<string> overrideKeys = new(StringComparer.Ordinal);
            XAnimationOverrideClipConfig[] clips = overrideAsset.clips ?? Array.Empty<XAnimationOverrideClipConfig>();
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationOverrideClipConfig clip = clips[i];
                if (clip == null)
                {
                    throw new XAnimationException($"XAnimation override '{assetPath}' clip config at index {i} is null.");
                }

                if (string.IsNullOrWhiteSpace(clip.key))
                {
                    throw new XAnimationException($"XAnimation override '{assetPath}' clip key at index {i} cannot be empty.");
                }

                if (!overrideKeys.Add(clip.key))
                {
                    throw new XAnimationException($"XAnimation override '{assetPath}' clip key '{clip.key}' is duplicated.");
                }

                if (string.IsNullOrWhiteSpace(clip.clipPath))
                {
                    throw new XAnimationException($"XAnimation override '{assetPath}' clip '{clip.key}' clipPath cannot be empty.");
                }
            }
        }

        private static void ApplyOverrideClips(
            XAnimationAsset baseAsset,
            XAnimationOverrideAsset overrideAsset,
            string assetPath)
        {
            Dictionary<string, XAnimationClipConfig> baseClipMap = new(StringComparer.Ordinal);
            for (int i = 0; i < baseAsset.clips.Length; i++)
            {
                XAnimationClipConfig clip = baseAsset.clips[i];
                if (clip != null && !string.IsNullOrWhiteSpace(clip.key))
                {
                    baseClipMap[clip.key] = clip;
                }
            }

            XAnimationOverrideClipConfig[] overrideClips = overrideAsset.clips ?? Array.Empty<XAnimationOverrideClipConfig>();
            for (int i = 0; i < overrideClips.Length; i++)
            {
                XAnimationOverrideClipConfig overrideClip = overrideClips[i];
                if (!baseClipMap.TryGetValue(overrideClip.key, out XAnimationClipConfig baseClip))
                {
                    throw new XAnimationException($"XAnimation override '{assetPath}' clip '{overrideClip.key}' does not exist in base asset '{overrideAsset.baseAssetPath}'.");
                }

                baseClip.clipPath = overrideClip.clipPath;
            }
        }
    }
}
