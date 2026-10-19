using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Command
{
    [Serializable]
    internal sealed class XCommandGameObjectWaitRequest : IXCommandWaitRequest
    {
        public string state = "exists";
        public XCommandGameObjectQuery query = new XCommandGameObjectQuery();
        public int stableFrames = 2;
        public float positionEpsilon = 0.001f;
        public float rotationEpsilon = 0.1f;
        public float timeoutSeconds = 30f;
        public bool compact;

        private string normalizedState;
        private int requiredStableFrames;
        private int satisfiedFrames;
        private int previousInstanceId;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector3 previousScale;
        private int matchCount;
        private List<GameObject> matches = new List<GameObject>();

        float IXCommandWaitRequest.TimeoutSeconds => timeoutSeconds;
        bool IXCommandWaitRequest.Compact => compact;

        string IXCommandWaitRequest.TimeoutMessage => $"wait-for-go 等待 {normalizedState} 超时（{timeoutSeconds.ToString("0.###")} 秒）。";

        void IXCommandWaitRequest.Validate()
        {
            if (query == null)
                throw new ArgumentException("wait-for-go 缺少 query。", nameof(query));
            XCommandUtility.GameObjects.EnsureSelector(query, "wait-for-go");
            normalizedState = string.IsNullOrEmpty(state) ? "exists" : state.ToLowerInvariant();
            if (normalizedState != "exists" && normalizedState != "missing" && normalizedState != "active" && normalizedState != "inactive" && normalizedState != "stable")
                throw new ArgumentException($"wait-for-go 不支持状态：{state}");
            if (positionEpsilon < 0f || rotationEpsilon < 0f)
                throw new ArgumentOutOfRangeException(nameof(positionEpsilon), "稳定阈值不能小于 0。");

            requiredStableFrames = Mathf.Clamp(stableFrames, 1, 10000);
            satisfiedFrames = 0;
            previousInstanceId = 0;
            previousPosition = default;
            previousRotation = default;
            previousScale = default;
            matchCount = 0;
            matches = new List<GameObject>();
        }

        bool IXCommandWaitRequest.Evaluate()
        {
            matches = XCommandUtility.GameObjects.FindExact(query, true);
            if ((normalizedState == "active" || normalizedState == "inactive" || normalizedState == "stable") && matches.Count > 1)
                throw new InvalidOperationException($"wait-for-go --state {normalizedState} 需要唯一匹配，当前匹配 {matches.Count} 个：{string.Join(", ", matches.Take(16).Select(XCommandUtility.GameObjects.FormatCandidate))}");
            bool satisfied;
            if (normalizedState == "missing")
                satisfied = matches.Count == 0;
            else if (normalizedState == "exists")
                satisfied = matches.Count > 0;
            else if (normalizedState == "active")
                satisfied = matches.Count == 1 && matches[0].activeInHierarchy;
            else if (normalizedState == "inactive")
                satisfied = matches.Count == 1 && !matches[0].activeInHierarchy;
            else
                satisfied = XCommandUtility.GameObjects.IsStable(matches, this, ref previousInstanceId, ref previousPosition, ref previousRotation, ref previousScale);

            matchCount = matches.Count;
            satisfiedFrames = satisfied ? satisfiedFrames + 1 : 0;
            return satisfiedFrames >= requiredStableFrames;
        }

        string IXCommandWaitRequest.CreateOutput(bool success, float elapsedSeconds)
        {
            var result = new GameObjectWaitResult {
                success = success,
                error = success ? string.Empty : ((IXCommandWaitRequest)this).TimeoutMessage,
                state = normalizedState,
                timestampUtc = DateTime.UtcNow.ToString("O"),
                frameCount = Time.frameCount,
                elapsedSeconds = elapsedSeconds,
                stableFrames = satisfiedFrames,
                matchCount = matchCount,
                objects = matches.Take(16).Select(XCommandUtility.GameObjects.CreateBriefState).ToList(),
            };
            return JsonUtility.ToJson(result, !compact);
        }

        [Serializable]
        private sealed class GameObjectWaitResult
        {
            public bool success;
            public string error;
            public string state;
            public string timestampUtc;
            public int frameCount;
            public float elapsedSeconds;
            public int stableFrames;
            public int matchCount;
            public List<XCommandUtility.GameObjects.GameObjectBriefState> objects = new List<XCommandUtility.GameObjects.GameObjectBriefState>();
        }
    }
}
