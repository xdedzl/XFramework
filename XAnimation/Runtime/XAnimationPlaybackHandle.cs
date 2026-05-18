using System;
using System.Collections.Generic;
using UnityEngine;

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
        private readonly List<Action<XAnimationPlaybackExitResult>> m_ExitCallbacks = new();
        private XAnimationPlaybackExitResult m_ExitResult;

        internal XAnimationPlaybackHandle(
            XAnimationDriver driver,
            bool isValid,
            int playbackId,
            string channelName,
            string requestedStateKey,
            string requestedClipKey,
            bool isTemporaryState)
        {
            m_Driver = driver;
            IsValid = isValid;
            PlaybackId = playbackId;
            ChannelName = channelName ?? string.Empty;
            RequestedStateKey = requestedStateKey ?? string.Empty;
            RequestedClipKey = requestedClipKey ?? string.Empty;
            IsTemporaryState = isTemporaryState;
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

        public XAnimationPlaybackHandle OnExit(Action<XAnimationPlaybackExitResult> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (m_ExitResult != null)
            {
                InvokeExitCallback(callback, m_ExitResult);
                return this;
            }

            m_ExitCallbacks.Add(callback);
            return this;
        }

        internal void CompleteExit(XAnimationPlaybackExitResult result)
        {
            if (result == null || m_ExitResult != null)
            {
                return;
            }

            m_ExitResult = result;
            if (m_ExitCallbacks.Count == 0)
            {
                return;
            }

            var callbacks = m_ExitCallbacks.ToArray();
            m_ExitCallbacks.Clear();
            foreach (var callback in callbacks)
            {
                InvokeExitCallback(callback, result);
            }
        }

        private static void InvokeExitCallback(
            Action<XAnimationPlaybackExitResult> callback,
            XAnimationPlaybackExitResult result)
        {
            try
            {
                callback(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
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
