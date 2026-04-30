using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using XFramework.Resource;

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
                throw new XFrameworkException("XAnimation assetPath cannot be empty.");
            }

            TextAsset textAsset = m_Resolver.LoadTextAsset(assetPath);
            if (textAsset == null)
            {
                throw new XFrameworkException($"XAnimation asset missing at '{assetPath}'.");
            }

            XAnimationAsset asset = LoadAsset(textAsset, assetPath);
            m_Validator.Validate(asset);
            return Compile(asset);
        }

        public XAnimationCompiledAsset Load(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                throw new XFrameworkException("XAnimation TextAsset cannot be null.");
            }

            XAnimationAsset asset = LoadAsset(textAsset, textAsset.name);
            m_Validator.Validate(asset);
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
                        throw new XFrameworkException($"XAnimation channel '{channelConfig.name}' failed to load AvatarMask at '{channelConfig.maskPath}'.");
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
                AnimationClip clip = m_Resolver.LoadAnimationClip(clipConfig.clipPath);
                if (clip == null)
                {
                    throw new XFrameworkException($"XAnimation clip '{clipConfig.key}' failed to load AnimationClip at '{clipConfig.clipPath}'.");
                }

                compiledClips[i] = new XAnimationCompiledClip(clipConfig, clip);
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
                    _ => throw new XFrameworkException($"XAnimation state '{stateConfig.key}' has unsupported stateType '{stateConfig.stateType}'."),
                };
                stateIndexByKey[stateConfig.key] = i;
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
                compiledParameters,
                cuesByClipKey,
                channelIndexByName,
                clipIndexByKey,
                parameterIndexByName,
                stateIndexByKey);
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

        private XAnimationAsset LoadAsset(TextAsset textAsset, string assetPath)
        {
            if (IsOverrideAssetText(textAsset.text))
            {
                return LoadOverrideAsset(textAsset, assetPath);
            }

            XAnimationAsset asset = textAsset.ToXTextAsset<XAnimationAsset>();
            if (asset == null)
            {
                throw new XFrameworkException($"Failed to deserialize XAnimation asset at '{assetPath}'.");
            }

            return asset;
        }

        private XAnimationAsset LoadOverrideAsset(TextAsset textAsset, string assetPath)
        {
            XAnimationOverrideAsset overrideAsset = textAsset.ToXTextAsset<XAnimationOverrideAsset>();
            if (overrideAsset == null)
            {
                throw new XFrameworkException($"Failed to deserialize XAnimation override asset at '{assetPath}'.");
            }

            ValidateOverrideAsset(overrideAsset, assetPath);

            TextAsset baseTextAsset = m_Resolver.LoadTextAsset(overrideAsset.baseAssetPath);
            if (baseTextAsset == null)
            {
                throw new XFrameworkException($"XAnimation override '{assetPath}' base asset missing at '{overrideAsset.baseAssetPath}'.");
            }

            if (IsOverrideAssetText(baseTextAsset.text))
            {
                throw new XFrameworkException($"XAnimation override '{assetPath}' baseAssetPath must reference a base XAnimationAsset, not another override asset.");
            }

            XAnimationAsset baseAsset = baseTextAsset.ToXTextAsset<XAnimationAsset>();
            if (baseAsset == null)
            {
                throw new XFrameworkException($"Failed to deserialize XAnimation override base asset at '{overrideAsset.baseAssetPath}'.");
            }

            m_Validator.Validate(baseAsset);
            XAnimationAsset mergedAsset = CloneAsset(baseAsset);
            if (mergedAsset == null)
            {
                throw new XFrameworkException($"Failed to clone XAnimation override base asset at '{overrideAsset.baseAssetPath}'.");
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

        private static void ValidateOverrideAsset(XAnimationOverrideAsset overrideAsset, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                throw new XFrameworkException($"XAnimation override '{assetPath}' baseAssetPath cannot be empty.");
            }

            HashSet<string> overrideKeys = new(StringComparer.Ordinal);
            XAnimationOverrideClipConfig[] clips = overrideAsset.clips ?? Array.Empty<XAnimationOverrideClipConfig>();
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationOverrideClipConfig clip = clips[i];
                if (clip == null)
                {
                    throw new XFrameworkException($"XAnimation override '{assetPath}' clip config at index {i} is null.");
                }

                if (string.IsNullOrWhiteSpace(clip.key))
                {
                    throw new XFrameworkException($"XAnimation override '{assetPath}' clip key at index {i} cannot be empty.");
                }

                if (!overrideKeys.Add(clip.key))
                {
                    throw new XFrameworkException($"XAnimation override '{assetPath}' clip key '{clip.key}' is duplicated.");
                }

                if (string.IsNullOrWhiteSpace(clip.clipPath))
                {
                    throw new XFrameworkException($"XAnimation override '{assetPath}' clip '{clip.key}' clipPath cannot be empty.");
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
                    throw new XFrameworkException($"XAnimation override '{assetPath}' clip '{overrideClip.key}' does not exist in base asset '{overrideAsset.baseAssetPath}'.");
                }

                baseClip.clipPath = overrideClip.clipPath;
            }
        }
    }
}
