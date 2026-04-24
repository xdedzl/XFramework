using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.Animation
{
    public sealed class XAnimationAssetLoader
    {
        private readonly XAnimationAssetValidator m_Validator = new();

        public XAnimationCompiledAsset Load(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new XFrameworkException("XAnimation assetPath cannot be empty.");
            }

            TextAsset textAsset = ResourceManager.Instance.Load<TextAsset>(assetPath);
            if (textAsset == null)
            {
                throw new XFrameworkException($"XAnimation asset missing at '{assetPath}'.");
            }

            XAnimationAsset asset = textAsset.ToXTextAsset<XAnimationAsset>();
            if (asset == null)
            {
                throw new XFrameworkException($"Failed to deserialize XAnimation asset at '{assetPath}'.");
            }

            m_Validator.Validate(asset);
            return Compile(asset);
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
                    mask = ResourceManager.Instance.Load<AvatarMask>(channelConfig.maskPath);
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
                AnimationClip clip = ResourceManager.Instance.Load<AnimationClip>(clipConfig.clipPath);
                if (clip == null)
                {
                    throw new XFrameworkException($"XAnimation clip '{clipConfig.key}' failed to load AnimationClip at '{clipConfig.clipPath}'.");
                }

                int defaultChannelIndex = channelIndexByName[clipConfig.defaultChannel];
                compiledClips[i] = new XAnimationCompiledClip(clipConfig, clip, defaultChannelIndex);
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
                compiledParameters,
                cuesByClipKey,
                channelIndexByName,
                clipIndexByKey,
                parameterIndexByName);
        }
    }
}
