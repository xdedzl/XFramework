using System;
using XFramework.Tasks;

namespace XFramework.Animation
{
    public sealed class XAnimationPlaybackExitResult
    {
        public bool WasStarted { get; internal set; }
        public int PlaybackId { get; internal set; }
        public string ChannelName { get; internal set; }
        public string RequestedStateKey { get; internal set; }
        public string RequestedClipKey { get; internal set; }
        public bool IsTemporaryState { get; internal set; }
        public XAnimationStateExitReason? ExitReason { get; internal set; }
    }

    public sealed class XAnimationPlaybackHandle
    {
        private readonly XAnimationDriver m_Driver;
        private readonly XAwaitableTask<XAnimationPlaybackExitResult> m_ExitTask;

        internal XAnimationPlaybackHandle(
            XAnimationDriver driver,
            bool isValid,
            int playbackId,
            string channelName,
            string requestedStateKey,
            string requestedClipKey,
            bool isTemporaryState,
            XAwaitableTask<XAnimationPlaybackExitResult> exitTask)
        {
            m_Driver = driver;
            IsValid = isValid;
            PlaybackId = playbackId;
            ChannelName = channelName ?? string.Empty;
            RequestedStateKey = requestedStateKey ?? string.Empty;
            RequestedClipKey = requestedClipKey ?? string.Empty;
            IsTemporaryState = isTemporaryState;
            m_ExitTask = exitTask ?? throw new ArgumentNullException(nameof(exitTask));
        }

        public bool IsValid { get; }
        public int PlaybackId { get; }
        public string ChannelName { get; }
        public string RequestedStateKey { get; }
        public string RequestedClipKey { get; }
        public bool IsTemporaryState { get; }

        public bool IsPlaying
        {
            get
            {
                if (!IsValid || m_Driver == null)
                {
                    return false;
                }

                return m_Driver.TryGetPlaybackState(PlaybackId, ChannelName, out _);
            }
        }

        public bool TryGetState(out XAnimationChannelState state)
        {
            state = null;
            if (!IsValid || m_Driver == null)
            {
                return false;
            }

            return m_Driver.TryGetPlaybackState(PlaybackId, ChannelName, out state);
        }

        public XAwaitableTask<XAnimationPlaybackExitResult> WaitForExitAsync()
        {
            return m_ExitTask;
        }
    }

    internal readonly struct XAnimationPlaybackStartInfo
    {
        public XAnimationPlaybackStartInfo(
            bool started,
            int playbackId,
            string channelName,
            string stateKey,
            string clipKey,
            bool isTemporaryState,
            XAnimationTransitionRejectReason rejectReason)
        {
            Started = started;
            PlaybackId = playbackId;
            ChannelName = channelName ?? string.Empty;
            StateKey = stateKey ?? string.Empty;
            ClipKey = clipKey ?? string.Empty;
            IsTemporaryState = isTemporaryState;
            RejectReason = rejectReason;
        }

        public bool Started { get; }
        public int PlaybackId { get; }
        public string ChannelName { get; }
        public string StateKey { get; }
        public string ClipKey { get; }
        public bool IsTemporaryState { get; }
        public XAnimationTransitionRejectReason RejectReason { get; }

        public static XAnimationPlaybackStartInfo CreateFailed(
            string channelName,
            string stateKey,
            string clipKey,
            bool isTemporaryState,
            XAnimationTransitionRejectReason rejectReason)
        {
            return new XAnimationPlaybackStartInfo(false, 0, channelName, stateKey, clipKey, isTemporaryState, rejectReason);
        }

        public static XAnimationPlaybackStartInfo CreateStarted(XAnimationStatePlaybackInstance playback)
        {
            if (playback == null)
            {
                throw new ArgumentNullException(nameof(playback));
            }

            return new XAnimationPlaybackStartInfo(
                true,
                playback.PlaybackId,
                playback.ChannelName,
                playback.StateKey,
                playback.PrimaryClipKey,
                playback.IsTemporaryState,
                XAnimationTransitionRejectReason.None);
        }
    }
}
