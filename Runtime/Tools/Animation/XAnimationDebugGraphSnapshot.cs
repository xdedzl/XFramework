using System;

namespace XFramework.Animation
{
    [Serializable]
    public sealed class XAnimationDebugGraphSnapshot
    {
        public string graphName = string.Empty;
        public bool isValid;
        public bool isPlaying;
        public bool isDisposed;
        public string animatorName = string.Empty;
        public string message = string.Empty;
        public XAnimationDebugChannelSnapshot[] channels = Array.Empty<XAnimationDebugChannelSnapshot>();
        public XAnimationDebugNodeSnapshot[] rootNodes = Array.Empty<XAnimationDebugNodeSnapshot>();

        public static XAnimationDebugGraphSnapshot Invalid(string message, string graphName = "", bool isDisposed = false)
        {
            return new XAnimationDebugGraphSnapshot
            {
                graphName = graphName ?? string.Empty,
                isValid = false,
                isPlaying = false,
                isDisposed = isDisposed,
                message = message ?? string.Empty,
                channels = Array.Empty<XAnimationDebugChannelSnapshot>(),
                rootNodes = Array.Empty<XAnimationDebugNodeSnapshot>(),
            };
        }
    }

    [Serializable]
    public sealed class XAnimationDebugChannelSnapshot
    {
        public string name = string.Empty;
        public int layerIndex;
        public XAnimationChannelLayerType layerType;
        public float layerWeight;
        public float channelWeight;
        public float timeScale;
        public bool hasActivePlayback;
        public bool canDriveRootMotion;
        public bool isRootMotionCandidate;
        public bool hasAvatarMask;
        public string avatarMaskName = string.Empty;
        public string currentStateKey = string.Empty;
        public string previousStateKey = string.Empty;
        public int currentPlaybackId;
        public int previousPlaybackId;
        public XAnimationTransitionRejectReason lastRejectReason;
        public string lastRejectedStateKey = string.Empty;
        public string lastRejectedClipKey = string.Empty;
        public int lastRejectedPriority;
        public XAnimationTransitionRequestSource lastRejectedSource;
    }

    [Serializable]
    public sealed class XAnimationDebugNodeSnapshot
    {
        public int id;
        public int parentId;
        public string displayName = string.Empty;
        public string playableType = string.Empty;
        public int inputIndex = -1;
        public bool isConnected = true;
        public bool isActive;
        public float inputWeight;
        public float effectiveWeight;
        public string channelName = string.Empty;
        public string stateKey = string.Empty;
        public XAnimationStateType stateType;
        public string clipKey = string.Empty;
        public int playbackId;
        public float normalizedTime;
        public float totalNormalizedTime;
        public float speed;
        public float timeScale;
        public float channelWeight;
        public float stateWeight;
        public float blendParameterX;
        public float blendParameterY;
        public bool isLooping;
        public bool isFading;
        public bool isTransitioning;
        public bool isTemporaryState;
        public bool isAdditive;
        public bool canDriveRootMotion;
        public bool drivesRootMotion;
        public bool isRootMotionCandidate;
        public bool hasAvatarMask;
        public string avatarMaskName = string.Empty;
        public XAnimationTransitionRequestSource transitionSource;
        public XAnimationTransitionRejectReason lastRejectReason;
        public string lastRejectedStateKey = string.Empty;
        public string lastRejectedClipKey = string.Empty;
        public int lastRejectedPriority;
        public XAnimationTransitionRequestSource lastRejectedSource;
        public string details = string.Empty;
        public XAnimationDebugNodeSnapshot[] children = Array.Empty<XAnimationDebugNodeSnapshot>();
    }

    internal sealed class XAnimationDebugSnapshotBuilder
    {
        private int m_NextNodeId = 1;

        public XAnimationDebugNodeSnapshot CreateNode(int parentId, string displayName, string playableType)
        {
            return new XAnimationDebugNodeSnapshot
            {
                id = m_NextNodeId++,
                parentId = parentId,
                displayName = displayName ?? string.Empty,
                playableType = playableType ?? string.Empty,
                children = Array.Empty<XAnimationDebugNodeSnapshot>(),
            };
        }
    }
}
