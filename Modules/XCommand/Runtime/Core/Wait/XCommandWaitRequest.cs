using System;
using UnityEngine;

namespace XFramework.Command
{
    [Serializable]
    internal sealed class XCommandWaitRequest : IXCommandWaitRequest
    {
        private const string ModeError = "wait 必须且只能指定 --real-seconds、--game-seconds 或 --frames 之一。";

        public float realSeconds;
        public float gameSeconds;
        public int frames;
        public bool compact;

        private string mode;
        private float startedRealTime;
        private float startedGameTime;
        private int startedFrame;

        float IXCommandWaitRequest.TimeoutSeconds => float.PositiveInfinity;
        bool IXCommandWaitRequest.Compact => compact;
        string IXCommandWaitRequest.TimeoutMessage => "wait 不会超时。";

        void IXCommandWaitRequest.Validate()
        {
            if (realSeconds < 0f || gameSeconds < 0f || frames < 0)
                throw new ArgumentOutOfRangeException(nameof(realSeconds), "wait 的等待值不能小于 0。");
            int modeCount = (realSeconds > 0f ? 1 : 0) + (gameSeconds > 0f ? 1 : 0) + (frames > 0 ? 1 : 0);
            if (modeCount != 1)
                throw new ArgumentException(ModeError, nameof(realSeconds));

            startedRealTime = Time.realtimeSinceStartup;
            startedGameTime = Time.time;
            startedFrame = Time.frameCount;
            mode = realSeconds > 0f ? "real" : gameSeconds > 0f ? "game" : "frames";
        }

        bool IXCommandWaitRequest.Evaluate()
        {
            if (mode == "real")
                return Time.realtimeSinceStartup - startedRealTime >= realSeconds;
            if (mode == "game")
                return Time.time - startedGameTime >= gameSeconds;
            return Time.frameCount - startedFrame >= frames;
        }

        string IXCommandWaitRequest.CreateOutput(bool success, float elapsedSeconds)
        {
            var result = new WaitResult {
                success = success,
                error = success ? string.Empty : ((IXCommandWaitRequest)this).TimeoutMessage,
                mode = mode,
                requestedSeconds = realSeconds > 0f ? realSeconds : gameSeconds,
                requestedFrames = frames,
                elapsedRealSeconds = Time.realtimeSinceStartup - startedRealTime,
                elapsedGameSeconds = Time.time - startedGameTime,
                elapsedFrames = Time.frameCount - startedFrame,
                completedFrame = Time.frameCount,
                completedTimeUtc = DateTime.UtcNow.ToString("O"),
            };
            return JsonUtility.ToJson(result, !compact);
        }

        [Serializable]
        private sealed class WaitResult
        {
            public bool success;
            public string error;
            public string mode;
            public float requestedSeconds;
            public int requestedFrames;
            public float elapsedRealSeconds;
            public float elapsedGameSeconds;
            public int elapsedFrames;
            public int completedFrame;
            public string completedTimeUtc;
        }
    }
}
