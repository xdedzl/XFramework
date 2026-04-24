using System;
using System.Collections.Generic;

namespace XFramework.Animation
{
    public sealed class XAnimationAssetValidator
    {
        public void Validate(XAnimationAsset asset)
        {
            if (asset == null)
            {
                throw new XFrameworkException("XAnimation asset is null.");
            }

            ValidateChannels(asset.channels);
            ValidateClips(asset.channels, asset.clips);
            ValidateParameters(asset.parameters);
            ValidateCues(asset.clips, asset.cues);
            ValidateGraph(asset.graph);
        }

        private static void ValidateChannels(IReadOnlyList<XAnimationChannelConfig> channels)
        {
            if (channels == null || channels.Count == 0)
            {
                throw new XFrameworkException("XAnimation asset must contain at least one channel.");
            }

            bool hasBaseChannel = false;
            HashSet<string> channelNames = new(StringComparer.Ordinal);
            foreach (XAnimationChannelConfig channel in channels)
            {
                if (channel == null)
                {
                    throw new XFrameworkException("XAnimation channel config is null.");
                }

                if (string.IsNullOrWhiteSpace(channel.name))
                {
                    throw new XFrameworkException("XAnimation channel name cannot be empty.");
                }

                if (!channelNames.Add(channel.name))
                {
                    throw new XFrameworkException($"XAnimation channel '{channel.name}' is duplicated.");
                }

                if (channel.defaultWeight < 0f)
                {
                    throw new XFrameworkException($"XAnimation channel '{channel.name}' has negative defaultWeight.");
                }

                if (channel.defaultFadeIn < 0f || channel.defaultFadeOut < 0f)
                {
                    throw new XFrameworkException($"XAnimation channel '{channel.name}' has negative fade settings.");
                }

                if (channel.layerType == XAnimationChannelLayerType.Base)
                {
                    hasBaseChannel = true;
                }

                if (channel.canDriveRootMotion && channel.layerType == XAnimationChannelLayerType.Additive)
                {
                    throw new XFrameworkException($"XAnimation channel '{channel.name}' cannot drive root motion when layerType is Additive.");
                }
            }

            if (!hasBaseChannel)
            {
                throw new XFrameworkException("XAnimation asset must contain at least one Base channel.");
            }
        }

        private static void ValidateClips(IReadOnlyList<XAnimationChannelConfig> channels, IReadOnlyList<XAnimationClipConfig> clips)
        {
            if (clips == null || clips.Count == 0)
            {
                throw new XFrameworkException("XAnimation asset must contain at least one clip.");
            }

            Dictionary<string, XAnimationChannelConfig> channelMap = new(StringComparer.Ordinal);
            foreach (XAnimationChannelConfig channel in channels)
            {
                channelMap[channel.name] = channel;
            }

            HashSet<string> clipKeys = new(StringComparer.Ordinal);
            foreach (XAnimationClipConfig clip in clips)
            {
                if (clip == null)
                {
                    throw new XFrameworkException("XAnimation clip config is null.");
                }

                if (string.IsNullOrWhiteSpace(clip.key))
                {
                    throw new XFrameworkException("XAnimation clip key cannot be empty.");
                }

                if (!clipKeys.Add(clip.key))
                {
                    throw new XFrameworkException($"XAnimation clip '{clip.key}' is duplicated.");
                }

                if (string.IsNullOrWhiteSpace(clip.clipPath))
                {
                    throw new XFrameworkException($"XAnimation clip '{clip.key}' has an empty clipPath.");
                }

                if (string.IsNullOrWhiteSpace(clip.defaultChannel))
                {
                    throw new XFrameworkException($"XAnimation clip '{clip.key}' has an empty defaultChannel.");
                }

                if (!channelMap.TryGetValue(clip.defaultChannel, out XAnimationChannelConfig defaultChannel))
                {
                    throw new XFrameworkException($"XAnimation clip '{clip.key}' references unknown channel '{clip.defaultChannel}'.");
                }

                if (clip.defaultFadeIn < 0f || clip.defaultFadeOut < 0f)
                {
                    throw new XFrameworkException($"XAnimation clip '{clip.key}' has negative fade settings.");
                }

                if (clip.rootMotionMode == XAnimationClipRootMotionMode.ForceOn && !defaultChannel.canDriveRootMotion)
                {
                    throw new XFrameworkException($"XAnimation clip '{clip.key}' forces root motion, but default channel '{clip.defaultChannel}' cannot drive root motion.");
                }
            }
        }

        private static void ValidateParameters(IReadOnlyList<XAnimationParameterConfig> parameters)
        {
            if (parameters == null)
            {
                return;
            }

            HashSet<string> parameterNames = new(StringComparer.Ordinal);
            foreach (XAnimationParameterConfig parameter in parameters)
            {
                if (parameter == null)
                {
                    throw new XFrameworkException("XAnimation parameter config is null.");
                }

                if (string.IsNullOrWhiteSpace(parameter.name))
                {
                    throw new XFrameworkException("XAnimation parameter name cannot be empty.");
                }

                if (!parameterNames.Add(parameter.name))
                {
                    throw new XFrameworkException($"XAnimation parameter '{parameter.name}' is duplicated.");
                }
            }
        }

        private static void ValidateCues(IReadOnlyList<XAnimationClipConfig> clips, IReadOnlyList<XAnimationCueConfig> cues)
        {
            if (cues == null)
            {
                return;
            }

            HashSet<string> clipKeys = new(StringComparer.Ordinal);
            foreach (XAnimationClipConfig clip in clips)
            {
                clipKeys.Add(clip.key);
            }

            foreach (XAnimationCueConfig cue in cues)
            {
                if (cue == null)
                {
                    throw new XFrameworkException("XAnimation cue config is null.");
                }

                if (string.IsNullOrWhiteSpace(cue.clipKey))
                {
                    throw new XFrameworkException("XAnimation cue clipKey cannot be empty.");
                }

                if (!clipKeys.Contains(cue.clipKey))
                {
                    throw new XFrameworkException($"XAnimation cue references unknown clip '{cue.clipKey}'.");
                }

                if (cue.time < 0f || cue.time > 1f)
                {
                    throw new XFrameworkException($"XAnimation cue '{cue.eventKey}' on clip '{cue.clipKey}' has time outside [0, 1].");
                }

                if (string.IsNullOrWhiteSpace(cue.eventKey))
                {
                    throw new XFrameworkException($"XAnimation cue on clip '{cue.clipKey}' has an empty eventKey.");
                }
            }
        }

        private static void ValidateGraph(XAnimationStateGraphConfig graph)
        {
            if (graph == null || !graph.enabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(graph.entryState))
            {
                throw new XFrameworkException("XAnimation graph is enabled but entryState is empty.");
            }

            HashSet<string> stateNames = new(StringComparer.Ordinal);
            if (graph.states != null)
            {
                foreach (XAnimationStateConfig state in graph.states)
                {
                    if (state == null)
                    {
                        throw new XFrameworkException("XAnimation graph state config is null.");
                    }

                    if (string.IsNullOrWhiteSpace(state.name))
                    {
                        throw new XFrameworkException("XAnimation graph state name cannot be empty.");
                    }

                    if (!stateNames.Add(state.name))
                    {
                        throw new XFrameworkException($"XAnimation graph state '{state.name}' is duplicated.");
                    }
                }
            }

            if (!stateNames.Contains(graph.entryState))
            {
                throw new XFrameworkException($"XAnimation graph entryState '{graph.entryState}' does not exist.");
            }

            if (graph.transitions == null)
            {
                return;
            }

            foreach (XAnimationTransitionConfig transition in graph.transitions)
            {
                if (transition == null)
                {
                    throw new XFrameworkException("XAnimation graph transition config is null.");
                }

                if (string.IsNullOrWhiteSpace(transition.fromState) || !stateNames.Contains(transition.fromState))
                {
                    throw new XFrameworkException($"XAnimation graph transition references unknown fromState '{transition?.fromState}'.");
                }

                if (string.IsNullOrWhiteSpace(transition.toState) || !stateNames.Contains(transition.toState))
                {
                    throw new XFrameworkException($"XAnimation graph transition references unknown toState '{transition?.toState}'.");
                }
            }
        }
    }
}
