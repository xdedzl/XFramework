using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Command
{
    internal sealed class UIActOperation : IXCommandAsyncOperation
    {
        private readonly XCommandUIActionRequest request;

        public UIActOperation(XCommandUIActionRequest request)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public string Name => "ui-act";

        public IEnumerator Run(XCommandOperationContext context)
        {
            XCommandUISelector selector = request.selector ?? new XCommandUISelector();
            if (string.IsNullOrEmpty(selector.name) && string.IsNullOrEmpty(selector.path) && string.IsNullOrEmpty(selector.indexedPath) && string.IsNullOrEmpty(selector.text))
                throw new ArgumentException("ui-act 至少需要 name、path、indexed-path 或 text 选择器之一。");
            string requiredAction = GetRequiredUIAction(request.pointerAction);
            selector.action = requiredAction;
            int stableFrames = Mathf.Max(1, request.stableFrames);
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + Mathf.Max(0.1f, request.timeoutSeconds);
            int observedStableFrames = 0;
            string previousFingerprint = null;
            int lastMatchCount = 0;
            XCommandUI.UISnapshot lastSnapshot = null;

            while (Time.realtimeSinceStartup <= deadline)
            {
                if (context.IsCancellationRequested)
                    yield break;
                lastSnapshot = XCommandUI.CaptureSnapshot(request.sampleGrid);
                List<XCommandUI.UIElementInfo> matches = lastSnapshot.Elements.Where(element => XCommandUI.MatchesSelector(element, selector)).ToList();
                lastMatchCount = matches.Count;
                if (matches.Count == 1)
                {
                    XCommandUI.UIElementInfo target = matches[0];
                    string fingerprint = lastSnapshot.Fingerprints[target.indexedPath];
                    observedStableFrames = fingerprint == previousFingerprint ? observedStableFrames + 1 : 1;
                    previousFingerprint = fingerprint;
                    if (observedStableFrames >= stableFrames)
                    {
                        XCommandUI.UIActionPoint actionPoint = target.actionPoints.First(point => point.actions.Contains(requiredAction));
                        yield return RunPointerAction(request.pointerAction, actionPoint.screenPoint.x, actionPoint.screenPoint.y, request.button, request.scrollX, request.scrollY);
                        var receipt = new UIActionReceipt {
                            success = true,
                            action = request.pointerAction,
                            snapshotId = lastSnapshot.Id,
                            contentHash = lastSnapshot.ContentHash,
                            path = target.path,
                            indexedPath = target.indexedPath,
                            raycastTargetPath = actionPoint.raycastTargetPath,
                            screenPoint = actionPoint.screenPoint,
                            stableFrames = observedStableFrames,
                            matchCount = 1,
                        };
                        context.SetOutput(JsonUtility.ToJson(receipt));
                        yield break;
                    }
                }
                else
                {
                    observedStableFrames = 0;
                    previousFingerprint = null;
                }
                yield return null;
            }

            string error = lastMatchCount > 1 ? $"UI 选择器匹配到 {lastMatchCount} 个元素，请使用 path 或 indexed-path 消歧。" : "等待可操作 UI 元素超时。";
            var failure = new UIActionReceipt {
                success = false,
                action = request.pointerAction,
                error = error,
                snapshotId = lastSnapshot?.Id ?? 0,
                contentHash = lastSnapshot?.ContentHash ?? string.Empty,
                stableFrames = observedStableFrames,
                matchCount = lastMatchCount,
            };
            context.SetOutput(JsonUtility.ToJson(failure));
            throw new TimeoutException(error);
        }

        private static IEnumerator RunPointerAction(string action, float x, float y, string button, float scrollX, float scrollY)
        {
            var sequence = new XCommandInputSequence {
                steps = new List<XCommandInputStep> {
                    new XCommandInputStep {
                        type = "mouse",
                        action = action,
                        button = button,
                        x = x,
                        y = y,
                        scrollX = scrollX,
                        scrollY = scrollY,
                        frames = 2,
                    },
                },
            };
            return XCommandUtility.Input.Run(sequence, () => false, _ => { });
        }

        private static string GetRequiredUIAction(string pointerAction)
        {
            switch ((pointerAction ?? string.Empty).ToLowerInvariant())
            {
                case "click": return "pointerClick";
                case "down": return "pointerDown";
                case "up": return "pointerUp";
                case "scroll": return "scroll";
                default: throw new ArgumentException($"ui-act 不支持操作：{pointerAction}");
            }
        }

        [Serializable]
        private sealed class UIActionReceipt
        {
            public bool success;
            public string action;
            public string error;
            public long snapshotId;
            public string contentHash;
            public string path;
            public string indexedPath;
            public string raycastTargetPath;
            public XCommandUI.FloatPoint screenPoint;
            public int stableFrames;
            public int matchCount;
        }
    }
}
