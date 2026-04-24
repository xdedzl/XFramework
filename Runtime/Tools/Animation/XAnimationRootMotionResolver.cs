using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Animation
{
    public sealed class XAnimationRootMotionResolver
    {
        public bool Enabled { get; private set; } = true;
        public string SourceChannelName { get; private set; }
        public int SourcePlaybackId { get; private set; }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            if (!enabled)
            {
                SourceChannelName = null;
                SourcePlaybackId = 0;
            }
        }

        public XAnimationChannel ResolveSource(IReadOnlyList<XAnimationChannel> channels)
        {
            SourceChannelName = null;
            SourcePlaybackId = 0;
            if (!Enabled || channels == null)
            {
                return null;
            }

            XAnimationChannel fallbackChannel = null;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationChannel channel = channels[i];
                if (channel == null || !channel.IsRootMotionSourceCandidate())
                {
                    continue;
                }

                if (channel.LayerType == XAnimationChannelLayerType.Base)
                {
                    SourceChannelName = channel.Name;
                    SourcePlaybackId = channel.CurrentPlayback.PlaybackId;
                    return channel;
                }

                fallbackChannel ??= channel;
            }

            if (fallbackChannel != null)
            {
                SourceChannelName = fallbackChannel.Name;
                SourcePlaybackId = fallbackChannel.CurrentPlayback.PlaybackId;
            }

            return fallbackChannel;
        }

        public void ApplyToAnimator(Animator animator)
        {
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = Enabled && !string.IsNullOrEmpty(SourceChannelName);
        }
    }
}
