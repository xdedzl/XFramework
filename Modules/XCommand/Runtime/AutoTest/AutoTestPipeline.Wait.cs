using System;
using System.Collections;
using UnityEngine;

namespace XFramework.AutoTest
{
    internal static class AutoTestWait
    {
        [Serializable]
        private sealed class WaitResult
        {
            public string mode;
            public float requestedSeconds;
            public int requestedFrames;
            public float elapsedRealSeconds;
            public float elapsedGameSeconds;
            public int elapsedFrames;
            public int completedFrame;
            public string completedTimeUtc;
        }

        internal static IEnumerator Run(AutoTestWaitRequest request, AutoTestOperationContext context)
        {
            if (request.realSeconds < 0f || request.gameSeconds < 0f || request.frames < 0)
                throw new ArgumentOutOfRangeException(nameof(request), "wait 的等待值不能小于 0。");
            int modeCount = (request.realSeconds > 0f ? 1 : 0) + (request.gameSeconds > 0f ? 1 : 0) + (request.frames > 0 ? 1 : 0);
            if (modeCount != 1)
                throw new ArgumentException("wait 必须且只能指定 --real-seconds、--game-seconds 或 --frames 之一。", nameof(request));

            float startedRealTime = Time.realtimeSinceStartup;
            float startedGameTime = Time.time;
            int startedFrame = Time.frameCount;
            string mode;
            if (request.realSeconds > 0f)
            {
                mode = "real";
                while (Time.realtimeSinceStartup - startedRealTime < request.realSeconds)
                    yield return null;
            }
            else if (request.gameSeconds > 0f)
            {
                mode = "game";
                while (Time.time - startedGameTime < request.gameSeconds)
                    yield return null;
            }
            else
            {
                mode = "frames";
                while (Time.frameCount - startedFrame < request.frames)
                    yield return null;
            }

            var result = new WaitResult {
                mode = mode,
                requestedSeconds = request.realSeconds > 0f ? request.realSeconds : request.gameSeconds,
                requestedFrames = request.frames,
                elapsedRealSeconds = Time.realtimeSinceStartup - startedRealTime,
                elapsedGameSeconds = Time.time - startedGameTime,
                elapsedFrames = Time.frameCount - startedFrame,
                completedFrame = Time.frameCount,
                completedTimeUtc = DateTime.UtcNow.ToString("O"),
            };
            context.SetOutput(JsonUtility.ToJson(result, !request.compact));
        }
    }
}
