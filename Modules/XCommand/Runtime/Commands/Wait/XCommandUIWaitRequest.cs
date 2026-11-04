using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Command
{
    [Serializable]
    internal sealed class XCommandUIWaitRequest : IXCommandWaitRequest
    {
        private const string TimeoutError = "等待 UI 状态超时。";

        public string state = "visible";
        public bool interactable;
        public XCommandUISelector selector = new XCommandUISelector();
        public int sampleGrid = 5;
        public int stableFrames = 2;
        public float timeoutSeconds = 30f;
        public bool compact;

        private string normalizedState;
        private XCommandUISelector actualSelector;
        private int requiredStableFrames;
        private string previousHash;
        private int observedStableFrames;
        private int matchCount;
        private long snapshotId;
        private string contentHash = string.Empty;
        private string[] matchedPaths = Array.Empty<string>();

        float IXCommandWaitRequest.TimeoutSeconds => timeoutSeconds;
        bool IXCommandWaitRequest.Compact => compact;
        string IXCommandWaitRequest.TimeoutMessage => TimeoutError;

        void IXCommandWaitRequest.Validate()
        {
            normalizedState = (state ?? string.Empty).ToLowerInvariant();
            if (normalizedState != "visible" && normalizedState != "hidden" && normalizedState != "stable")
                throw new ArgumentException($"未知 UI 等待状态：{state}");
            actualSelector = selector ?? new XCommandUISelector();
            if (normalizedState != "stable" && string.IsNullOrEmpty(actualSelector.name) && string.IsNullOrEmpty(actualSelector.path) && string.IsNullOrEmpty(actualSelector.indexedPath) && string.IsNullOrEmpty(actualSelector.text))
                throw new ArgumentException("等待 UI 出现或消失时至少需要 name、path、indexed-path 或 text 之一。");

            requiredStableFrames = Mathf.Max(1, stableFrames);
            previousHash = null;
            observedStableFrames = 0;
            matchCount = 0;
            snapshotId = 0;
            contentHash = string.Empty;
            matchedPaths = Array.Empty<string>();
        }

        bool IXCommandWaitRequest.Evaluate()
        {
            if (interactable || normalizedState == "stable")
            {
                XCommandUI.UISnapshot snapshot = XCommandUI.CaptureSnapshot(sampleGrid);
                snapshotId = snapshot.Id;
                List<XCommandUI.UIElementInfo> matches = snapshot.Elements.Where(element => XCommandUI.MatchesSelector(element, actualSelector)).ToList();
                matchCount = matches.Count;
                matchedPaths = matches.Select(element => element.path).Take(16).ToArray();
                contentHash = XCommandUI.ComputeHash(matches.Select(element => snapshot.Fingerprints[element.indexedPath]));
            }
            else
            {
                List<XCommandUI.RenderedUITarget> matches = XCommandUI.FindRenderedUITargets(actualSelector);
                matchCount = matches.Count;
                matchedPaths = matches.Select(target => target.Path).Take(16).ToArray();
                contentHash = XCommandUI.ComputeHash(matches.Select(target => target.IndexedPath));
            }

            bool conditionMet;
            if (normalizedState == "stable")
            {
                conditionMet = previousHash != null && previousHash == contentHash;
                previousHash = contentHash;
            }
            else
            {
                conditionMet = normalizedState == "visible" ? matchCount > 0 : matchCount == 0;
            }
            observedStableFrames = conditionMet ? observedStableFrames + 1 : 0;
            return observedStableFrames >= requiredStableFrames;
        }

        string IXCommandWaitRequest.CreateOutput(bool success, float elapsedSeconds)
        {
            var receipt = new UIWaitReceipt {
                success = success,
                state = normalizedState,
                error = success ? string.Empty : TimeoutError,
                interactable = interactable || normalizedState == "stable",
                elapsedSeconds = elapsedSeconds,
                stableFrames = observedStableFrames,
                matchCount = matchCount,
                snapshotId = snapshotId,
                contentHash = contentHash,
                paths = matchedPaths,
            };
            return JsonUtility.ToJson(receipt, !compact);
        }

        [Serializable]
        private sealed class UIWaitReceipt
        {
            public bool success;
            public string state;
            public string error;
            public bool interactable;
            public float elapsedSeconds;
            public int stableFrames;
            public int matchCount;
            public long snapshotId;
            public string contentHash;
            public string[] paths;
        }
    }
}
