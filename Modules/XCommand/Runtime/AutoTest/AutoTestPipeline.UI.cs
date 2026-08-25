using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XFramework.AutoTest
{
    internal static class AutoTestUI
    {
        [Serializable]
        private sealed class UIElementList
        {
            public long snapshotId;
            public long baseSnapshotId;
            public string contentHash;
            public bool unchanged;
            public int screenWidth;
            public int screenHeight;
            public string eventSystemPath;
            public int totalCount;
            public int matchedCount;
            public int count;
            public bool truncated;
            public List<UIElementInfo> elements = new List<UIElementInfo>();
            public List<string> removedIndexedPaths = new List<string>();
        }

        [Serializable]
        private sealed class UIElementInfo
        {
            public int instanceId;
            public string name;
            public string path;
            public string indexedPath;
            public string scene;
            public string text;
            public int textLength;
            public bool textTruncated;
            public string[] actions;
            public UIActionPoint[] actionPoints;
            public string[] handlerTypes;
            public FloatRect screenRect;
            public FloatRect visibleRect;
            public string canvasPath;
            public string renderMode;
            public int sortingOrder;
            public bool selected;
            public ScrollInfo scroll;
        }

        [Serializable]
        private sealed class FloatRect
        {
            public float x;
            public float y;
            public float width;
            public float height;
        }

        [Serializable]
        private sealed class FloatPoint
        {
            public float x;
            public float y;
        }

        [Serializable]
        private sealed class UIActionPoint
        {
            public string[] actions;
            public FloatPoint screenPoint;
            public string raycastTargetPath;
            public string raycastTargetType;
        }

        [Serializable]
        private sealed class ScrollInfo
        {
            public bool horizontal;
            public bool vertical;
            public float horizontalNormalizedPosition;
            public float verticalNormalizedPosition;
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
            public FloatPoint screenPoint;
            public int stableFrames;
            public int matchCount;
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

        private sealed class UIElementCandidate
        {
            public RectTransform RectTransform;
            public readonly HashSet<string> Actions = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> HandlerTypes = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class UIElementExposure
        {
            public readonly Dictionary<string, UIActionExposure> Actions = new Dictionary<string, UIActionExposure>(StringComparer.Ordinal);
        }

        private sealed class UIActionExposure
        {
            public Vector2 Point;
            public RaycastResult Raycast;
        }

        private sealed class UISnapshot
        {
            public long Id;
            public string ContentHash;
            public List<UIElementInfo> Elements;
            public Dictionary<string, string> Fingerprints;
        }

        private sealed class RenderedUITarget
        {
            public string Path;
            public string IndexedPath;
        }

        private const int SnapshotHistoryLimit = 32;
        private static readonly Dictionary<long, UISnapshot> s_SnapshotHistory = new Dictionary<long, UISnapshot>();
        private static readonly Queue<long> s_SnapshotOrder = new Queue<long>();
        private static long s_NextSnapshotId = 1;

        internal static void ResetSnapshots()
        {
            s_SnapshotHistory.Clear();
            s_SnapshotOrder.Clear();
            s_NextSnapshotId = 1;
        }

        internal static string List(AutoTestUIQuery query)
        {
            AutoTestUISelector selector = query.selector ?? new AutoTestUISelector();
            UISnapshot snapshot = CaptureSnapshot(query.sampleGrid);
            UISnapshot baseSnapshot = null;
            if (query.changedSince > 0 && !s_SnapshotHistory.TryGetValue(query.changedSince, out baseSnapshot))
                throw new ArgumentException($"找不到 UI 快照：{query.changedSince}", nameof(query.changedSince));

            IEnumerable<UIElementInfo> elements = snapshot.Elements;
            if (baseSnapshot != null)
            {
                elements = elements.Where(element => !baseSnapshot.Fingerprints.TryGetValue(element.indexedPath, out string fingerprint) || fingerprint != snapshot.Fingerprints[element.indexedPath]);
            }

            List<UIElementInfo> matched = elements.Where(element => MatchesSelector(element, selector)).ToList();
            int matchedCount = matched.Count;
            if (query.maxResults > 0)
                matched = matched.Take(query.maxResults).ToList();
            var result = new UIElementList {
                snapshotId = snapshot.Id,
                baseSnapshotId = baseSnapshot?.Id ?? 0,
                contentHash = snapshot.ContentHash,
                unchanged = baseSnapshot != null && baseSnapshot.ContentHash == snapshot.ContentHash,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                eventSystemPath = GetGameObjectPath(EventSystem.current?.gameObject),
                totalCount = snapshot.Elements.Count,
                matchedCount = matchedCount,
                count = matched.Count,
                truncated = matched.Count < matchedCount,
                elements = matched.Select(element => CloneForOutput(element, query.textLimit)).ToList(),
            };
            if (baseSnapshot != null)
            {
                result.removedIndexedPaths = baseSnapshot.Elements.Where(element => !snapshot.Fingerprints.ContainsKey(element.indexedPath)).Where(element => MatchesSelector(element, selector)).Select(element => element.indexedPath).ToList();
            }
            return JsonUtility.ToJson(result, !query.compact);
        }

        internal static IEnumerator Act(AutoTestUIActionRequest request, AutoTestOperationContext context)
        {
            AutoTestUISelector selector = request.selector ?? new AutoTestUISelector();
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
            UISnapshot lastSnapshot = null;

            while (Time.realtimeSinceStartup <= deadline)
            {
                lastSnapshot = CaptureSnapshot(request.sampleGrid);
                List<UIElementInfo> matches = lastSnapshot.Elements.Where(element => MatchesSelector(element, selector)).ToList();
                lastMatchCount = matches.Count;
                if (matches.Count == 1)
                {
                    UIElementInfo target = matches[0];
                    string fingerprint = lastSnapshot.Fingerprints[target.indexedPath];
                    observedStableFrames = fingerprint == previousFingerprint ? observedStableFrames + 1 : 1;
                    previousFingerprint = fingerprint;
                    if (observedStableFrames >= stableFrames)
                    {
                        UIActionPoint actionPoint = target.actionPoints.First(point => point.actions.Contains(requiredAction));
                        yield return AutoTestInput.RunPointerAction(request.pointerAction, actionPoint.screenPoint.x, actionPoint.screenPoint.y, request.button, request.scrollX, request.scrollY);
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

        internal static IEnumerator Wait(AutoTestUIWaitRequest request, AutoTestOperationContext context)
        {
            string state = (request.state ?? string.Empty).ToLowerInvariant();
            if (state != "visible" && state != "hidden" && state != "stable")
                throw new ArgumentException($"未知 UI 等待状态：{request.state}");
            AutoTestUISelector selector = request.selector ?? new AutoTestUISelector();
            if (state != "stable" && string.IsNullOrEmpty(selector.name) && string.IsNullOrEmpty(selector.path) && string.IsNullOrEmpty(selector.indexedPath) && string.IsNullOrEmpty(selector.text))
                throw new ArgumentException("等待 UI 出现或消失时至少需要 name、path、indexed-path 或 text 之一。");

            int stableFrames = Mathf.Max(1, request.stableFrames);
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + Mathf.Max(0.1f, request.timeoutSeconds);
            int observedStableFrames = 0;
            string previousHash = null;
            int matchCount = 0;
            long snapshotId = 0;
            string contentHash = string.Empty;
            string[] matchedPaths = Array.Empty<string>();

            while (Time.realtimeSinceStartup <= deadline)
            {
                if (request.interactable || state == "stable")
                {
                    UISnapshot snapshot = CaptureSnapshot(request.sampleGrid);
                    snapshotId = snapshot.Id;
                    List<UIElementInfo> matches = snapshot.Elements.Where(element => MatchesSelector(element, selector)).ToList();
                    matchCount = matches.Count;
                    matchedPaths = matches.Select(element => element.path).Take(16).ToArray();
                    contentHash = ComputeHash(matches.Select(element => snapshot.Fingerprints[element.indexedPath]));
                }
                else
                {
                    List<RenderedUITarget> matches = FindRenderedUITargets(selector);
                    matchCount = matches.Count;
                    matchedPaths = matches.Select(target => target.Path).Take(16).ToArray();
                    contentHash = ComputeHash(matches.Select(target => target.IndexedPath));
                }

                bool conditionMet;
                if (state == "stable")
                {
                    conditionMet = previousHash != null && previousHash == contentHash;
                    previousHash = contentHash;
                }
                else
                {
                    conditionMet = state == "visible" ? matchCount > 0 : matchCount == 0;
                }
                observedStableFrames = conditionMet ? observedStableFrames + 1 : 0;
                if (observedStableFrames >= stableFrames)
                {
                    var receipt = new UIWaitReceipt {
                        success = true,
                        state = state,
                        interactable = request.interactable || state == "stable",
                        elapsedSeconds = Time.realtimeSinceStartup - startedAt,
                        stableFrames = observedStableFrames,
                        matchCount = matchCount,
                        snapshotId = snapshotId,
                        contentHash = contentHash,
                        paths = matchedPaths,
                    };
                    context.SetOutput(JsonUtility.ToJson(receipt));
                    yield break;
                }
                yield return null;
            }

            var failure = new UIWaitReceipt {
                success = false,
                state = state,
                error = "等待 UI 状态超时。",
                interactable = request.interactable || state == "stable",
                elapsedSeconds = Time.realtimeSinceStartup - startedAt,
                stableFrames = observedStableFrames,
                matchCount = matchCount,
                snapshotId = snapshotId,
                contentHash = contentHash,
                paths = matchedPaths,
            };
            context.SetOutput(JsonUtility.ToJson(failure));
            throw new TimeoutException(failure.error);
        }

        private static UISnapshot CaptureSnapshot(int sampleGrid)
        {
            UIElementList captured = CaptureInteractableUI(sampleGrid);
            var fingerprints = captured.elements.ToDictionary(element => element.indexedPath, CreateElementFingerprint, StringComparer.Ordinal);
            var snapshot = new UISnapshot {
                Id = s_NextSnapshotId++,
                Elements = captured.elements,
                Fingerprints = fingerprints,
                ContentHash = ComputeHash(fingerprints.OrderBy(pair => pair.Key).Select(pair => pair.Value)),
            };
            s_SnapshotHistory.Add(snapshot.Id, snapshot);
            s_SnapshotOrder.Enqueue(snapshot.Id);
            while (s_SnapshotOrder.Count > SnapshotHistoryLimit)
                s_SnapshotHistory.Remove(s_SnapshotOrder.Dequeue());
            return snapshot;
        }

        private static UIElementList CaptureInteractableUI(int sampleGrid)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                throw new InvalidOperationException("当前场景没有启用的 EventSystem。");
            sampleGrid = Mathf.Clamp(sampleGrid, 2, 9);
            Canvas.ForceUpdateCanvases();
            var result = new UIElementList {
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                eventSystemPath = GetGameObjectPath(eventSystem.gameObject),
            };
            var raycastResults = new List<RaycastResult>();
            foreach (UIElementCandidate candidate in FindCandidates())
            {
                if (!TryGetScreenRect(candidate.RectTransform, out Rect screenRect))
                    continue;
                Rect visibleRect = IntersectRect(screenRect, new Rect(0f, 0f, Screen.width, Screen.height));
                if (visibleRect.width <= 0f || visibleRect.height <= 0f)
                    continue;
                UIElementExposure exposure = FindExposure(eventSystem, candidate, visibleRect, sampleGrid, raycastResults);
                if (exposure.Actions.Count > 0)
                    result.elements.Add(CreateElementInfo(candidate, exposure, screenRect, visibleRect, eventSystem));
            }
            result.elements = result.elements.OrderByDescending(element => element.sortingOrder).ThenByDescending(element => GetPrimaryActionPoint(element.actionPoints).screenPoint.y).ThenBy(element => GetPrimaryActionPoint(element.actionPoints).screenPoint.x).ToList();
            result.count = result.elements.Count;
            result.totalCount = result.count;
            result.matchedCount = result.count;
            return result;
        }

        private static List<UIElementCandidate> FindCandidates()
        {
            var candidates = new List<UIElementCandidate>();
            foreach (RectTransform rectTransform in UnityEngine.Object.FindObjectsOfType<RectTransform>())
            {
                if (!rectTransform.gameObject.activeInHierarchy)
                    continue;
                var candidate = new UIElementCandidate { RectTransform = rectTransform };
                foreach (Component component in rectTransform.GetComponents<Component>())
                    AddComponentActions(component, candidate);
                if (candidate.Actions.Count > 0)
                    candidates.Add(candidate);
            }
            return candidates;
        }

        private static void AddComponentActions(Component component, UIElementCandidate candidate)
        {
            if (component == null || component is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return;
            if (component is Selectable selectable && !selectable.IsInteractable())
                return;
            AddAction<IPointerClickHandler>(component, candidate, "pointerClick");
            AddAction<IPointerDownHandler>(component, candidate, "pointerDown");
            AddAction<IPointerUpHandler>(component, candidate, "pointerUp");
            AddAction<IPointerEnterHandler>(component, candidate, "pointerEnter");
            AddAction<IPointerExitHandler>(component, candidate, "pointerExit");
            AddAction<IInitializePotentialDragHandler>(component, candidate, "initializeDrag");
            AddAction<IBeginDragHandler>(component, candidate, "beginDrag");
            AddAction<IDragHandler>(component, candidate, "drag");
            AddAction<IEndDragHandler>(component, candidate, "endDrag");
            AddAction<IDropHandler>(component, candidate, "drop");
            AddAction<IScrollHandler>(component, candidate, "scroll");
            AddAction<ISelectHandler>(component, candidate, "select");
            AddAction<IDeselectHandler>(component, candidate, "deselect");
            AddAction<ISubmitHandler>(component, candidate, "submit");
            AddAction<ICancelHandler>(component, candidate, "cancel");
            AddAction<IMoveHandler>(component, candidate, "move");
            AddAction<IUpdateSelectedHandler>(component, candidate, "textInput");
        }

        private static void AddAction<T>(Component component, UIElementCandidate candidate, string action) where T : IEventSystemHandler
        {
            if (!(component is T))
                return;
            candidate.Actions.Add(action);
            candidate.HandlerTypes.Add(component.GetType().FullName ?? component.GetType().Name);
        }

        private static UIElementExposure FindExposure(EventSystem eventSystem, UIElementCandidate candidate, Rect visibleRect, int sampleGrid, List<RaycastResult> raycastResults)
        {
            var exposure = new UIElementExposure();
            foreach (Vector2 point in EnumerateSamplePoints(visibleRect, sampleGrid))
            {
                raycastResults.Clear();
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = point }, raycastResults);
                RaycastResult raycast = FindFirstRaycast(raycastResults);
                if (!raycast.isValid || !(raycast.module is GraphicRaycaster))
                    continue;
                foreach (string action in candidate.Actions)
                {
                    if (!exposure.Actions.ContainsKey(action) && IsActionHandler(raycast.gameObject, candidate.RectTransform.gameObject, action))
                        exposure.Actions.Add(action, new UIActionExposure { Point = point, Raycast = raycast });
                }
                if (exposure.Actions.Count == candidate.Actions.Count)
                    return exposure;
            }
            return exposure;
        }

        private static IEnumerable<Vector2> EnumerateSamplePoints(Rect rect, int sampleGrid)
        {
            yield return rect.center;
            float insetX = Mathf.Min(0.5f, rect.width * 0.25f);
            float insetY = Mathf.Min(0.5f, rect.height * 0.25f);
            float minimumX = rect.xMin + insetX;
            float maximumX = rect.xMax - insetX;
            float minimumY = rect.yMin + insetY;
            float maximumY = rect.yMax - insetY;
            for (int y = 0; y < sampleGrid; y++)
            {
                float pointY = Mathf.Lerp(minimumY, maximumY, (float)y / (sampleGrid - 1));
                for (int x = 0; x < sampleGrid; x++)
                    yield return new Vector2(Mathf.Lerp(minimumX, maximumX, (float)x / (sampleGrid - 1)), pointY);
            }
        }

        private static bool IsActionHandler(GameObject raycastTarget, GameObject candidate, string action)
        {
            switch (action)
            {
                case "pointerClick": return ExecuteEvents.GetEventHandler<IPointerClickHandler>(raycastTarget) == candidate;
                case "pointerDown": return ExecuteEvents.GetEventHandler<IPointerDownHandler>(raycastTarget) == candidate;
                case "pointerUp": return ExecuteEvents.GetEventHandler<IPointerUpHandler>(raycastTarget) == candidate;
                case "pointerEnter": return ExecuteEvents.GetEventHandler<IPointerEnterHandler>(raycastTarget) == candidate;
                case "pointerExit": return ExecuteEvents.GetEventHandler<IPointerExitHandler>(raycastTarget) == candidate;
                case "initializeDrag": return ExecuteEvents.GetEventHandler<IInitializePotentialDragHandler>(raycastTarget) == candidate;
                case "beginDrag": return ExecuteEvents.GetEventHandler<IBeginDragHandler>(raycastTarget) == candidate;
                case "drag": return ExecuteEvents.GetEventHandler<IDragHandler>(raycastTarget) == candidate;
                case "endDrag": return ExecuteEvents.GetEventHandler<IEndDragHandler>(raycastTarget) == candidate;
                case "drop": return ExecuteEvents.GetEventHandler<IDropHandler>(raycastTarget) == candidate;
                case "scroll": return ExecuteEvents.GetEventHandler<IScrollHandler>(raycastTarget) == candidate;
                case "select": return ExecuteEvents.GetEventHandler<ISelectHandler>(raycastTarget) == candidate;
                case "deselect": return ExecuteEvents.GetEventHandler<IDeselectHandler>(raycastTarget) == candidate;
                case "submit": return ExecuteEvents.GetEventHandler<ISubmitHandler>(raycastTarget) == candidate;
                case "cancel": return ExecuteEvents.GetEventHandler<ICancelHandler>(raycastTarget) == candidate;
                case "move": return ExecuteEvents.GetEventHandler<IMoveHandler>(raycastTarget) == candidate;
                case "textInput": return ExecuteEvents.GetEventHandler<IUpdateSelectedHandler>(raycastTarget) == candidate;
                default: return false;
            }
        }

        private static UIElementInfo CreateElementInfo(UIElementCandidate candidate, UIElementExposure exposure, Rect screenRect, Rect visibleRect, EventSystem eventSystem)
        {
            GameObject gameObject = candidate.RectTransform.gameObject;
            Canvas canvas = candidate.RectTransform.GetComponentInParent<Canvas>();
            ScrollRect scrollRect = gameObject.GetComponent<ScrollRect>();
            string text = GetElementText(candidate.RectTransform);
            return new UIElementInfo {
                instanceId = gameObject.GetInstanceID(),
                name = gameObject.name,
                path = GetGameObjectPath(gameObject),
                indexedPath = GetIndexedGameObjectPath(gameObject),
                scene = gameObject.scene.path,
                text = text,
                textLength = text.Length,
                actions = exposure.Actions.Keys.OrderBy(action => action, StringComparer.Ordinal).ToArray(),
                actionPoints = CreateActionPoints(exposure),
                handlerTypes = candidate.HandlerTypes.OrderBy(type => type, StringComparer.Ordinal).ToArray(),
                screenRect = ToFloatRect(screenRect),
                visibleRect = ToFloatRect(visibleRect),
                canvasPath = canvas != null ? GetGameObjectPath(canvas.gameObject) : string.Empty,
                renderMode = canvas != null ? canvas.rootCanvas.renderMode.ToString() : string.Empty,
                sortingOrder = canvas != null ? canvas.rootCanvas.sortingOrder : 0,
                selected = eventSystem.currentSelectedGameObject == gameObject,
                scroll = scrollRect == null ? null : new ScrollInfo {
                    horizontal = scrollRect.horizontal,
                    vertical = scrollRect.vertical,
                    horizontalNormalizedPosition = scrollRect.horizontalNormalizedPosition,
                    verticalNormalizedPosition = scrollRect.verticalNormalizedPosition,
                },
            };
        }

        private static UIActionPoint[] CreateActionPoints(UIElementExposure exposure)
        {
            var points = new List<UIActionPoint>();
            foreach (KeyValuePair<string, UIActionExposure> pair in exposure.Actions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                UIActionPoint point = points.FirstOrDefault(candidate => Mathf.Approximately(candidate.screenPoint.x, pair.Value.Point.x) && Mathf.Approximately(candidate.screenPoint.y, pair.Value.Point.y));
                if (point == null)
                {
                    points.Add(new UIActionPoint {
                        actions = new[] { pair.Key },
                        screenPoint = new FloatPoint { x = pair.Value.Point.x, y = pair.Value.Point.y },
                        raycastTargetPath = GetGameObjectPath(pair.Value.Raycast.gameObject),
                        raycastTargetType = pair.Value.Raycast.gameObject.GetComponent<Graphic>()?.GetType().FullName ?? string.Empty,
                    });
                }
                else
                {
                    int index = point.actions.Length;
                    Array.Resize(ref point.actions, index + 1);
                    point.actions[index] = pair.Key;
                }
            }
            return points.ToArray();
        }

        private static UIActionPoint GetPrimaryActionPoint(UIActionPoint[] actionPoints)
        {
            string[] preferred = { "pointerClick", "pointerDown", "scroll" };
            for (int i = 0; i < preferred.Length; i++)
            {
                UIActionPoint point = actionPoints.FirstOrDefault(candidate => candidate.actions.Contains(preferred[i]));
                if (point != null)
                    return point;
            }
            return actionPoints[0];
        }

        private static string GetElementText(RectTransform rectTransform)
        {
            TMP_InputField tmpInputField = rectTransform.GetComponent<TMP_InputField>();
            if (tmpInputField != null)
                return NormalizeText(tmpInputField.text);
            InputField inputField = rectTransform.GetComponent<InputField>();
            if (inputField != null)
                return NormalizeText(inputField.text);
            IEnumerable<string> legacyTexts = rectTransform.GetComponentsInChildren<Text>(false).Where(text => text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text)).Select(text => text.text);
            IEnumerable<string> tmpTexts = rectTransform.GetComponentsInChildren<TMP_Text>(false).Where(text => text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text)).Select(text => text.text);
            return NormalizeText(string.Join(" | ", legacyTexts.Concat(tmpTexts).Distinct()));
        }

        private static List<RenderedUITarget> FindRenderedUITargets(AutoTestUISelector selector)
        {
            var results = new List<RenderedUITarget>();
            foreach (RectTransform rectTransform in UnityEngine.Object.FindObjectsOfType<RectTransform>())
            {
                GameObject gameObject = rectTransform.gameObject;
                if (!gameObject.activeInHierarchy || !string.IsNullOrEmpty(selector.name) && !string.Equals(gameObject.name, selector.name, StringComparison.Ordinal))
                    continue;
                string path = GetGameObjectPath(gameObject);
                if (!string.IsNullOrEmpty(selector.path) && !string.Equals(path, selector.path, StringComparison.Ordinal))
                    continue;
                string indexedPath = GetIndexedGameObjectPath(gameObject);
                if (!string.IsNullOrEmpty(selector.indexedPath) && !string.Equals(indexedPath, selector.indexedPath, StringComparison.Ordinal))
                    continue;
                if (!string.IsNullOrEmpty(selector.text) && !ContainsIgnoreCase(GetElementText(rectTransform), selector.text))
                    continue;
                if (IsRenderedUI(rectTransform))
                    results.Add(new RenderedUITarget { Path = path, IndexedPath = indexedPath });
            }
            return results;
        }

        private static bool IsRenderedUI(RectTransform rectTransform)
        {
            if (!TryGetScreenRect(rectTransform, out Rect screenRect))
                return false;
            Rect visibleRect = IntersectRect(screenRect, new Rect(0f, 0f, Screen.width, Screen.height));
            if (visibleRect.width <= 0f || visibleRect.height <= 0f)
                return false;
            foreach (Graphic graphic in rectTransform.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.isActiveAndEnabled || graphic.color.a <= 0.001f || graphic.canvasRenderer.cull || graphic.canvasRenderer.GetInheritedAlpha() <= 0.001f)
                    continue;
                if (TryGetScreenRect(graphic.rectTransform, out Rect graphicRect))
                {
                    Rect visibleGraphicRect = IntersectRect(graphicRect, new Rect(0f, 0f, Screen.width, Screen.height));
                    if (visibleGraphicRect.width > 0f && visibleGraphicRect.height > 0f)
                        return true;
                }
            }
            return false;
        }

        private static bool MatchesSelector(UIElementInfo element, AutoTestUISelector selector)
        {
            return (string.IsNullOrEmpty(selector.name) || string.Equals(element.name, selector.name, StringComparison.Ordinal)) &&
                   (string.IsNullOrEmpty(selector.path) || string.Equals(element.path, selector.path, StringComparison.Ordinal)) &&
                   (string.IsNullOrEmpty(selector.indexedPath) || string.Equals(element.indexedPath, selector.indexedPath, StringComparison.Ordinal)) &&
                   (string.IsNullOrEmpty(selector.text) || ContainsIgnoreCase(element.text, selector.text)) &&
                   (string.IsNullOrEmpty(selector.action) || element.actions.Any(action => string.Equals(action, selector.action, StringComparison.OrdinalIgnoreCase)));
        }

        private static string CreateElementFingerprint(UIElementInfo element)
        {
            return string.Join("\n", new[] {
                element.indexedPath,
                element.text,
                string.Join(",", element.actions),
                string.Join(",", element.actionPoints.Select(CreateActionPointFingerprint)),
                element.screenRect.x.ToString("F2", CultureInfo.InvariantCulture),
                element.screenRect.y.ToString("F2", CultureInfo.InvariantCulture),
                element.screenRect.width.ToString("F2", CultureInfo.InvariantCulture),
                element.screenRect.height.ToString("F2", CultureInfo.InvariantCulture),
                element.selected ? "1" : "0",
            });
        }

        private static string CreateActionPointFingerprint(UIActionPoint point)
        {
            return string.Join(":", new[] {
                string.Join(",", point.actions),
                point.screenPoint.x.ToString("F2", CultureInfo.InvariantCulture),
                point.screenPoint.y.ToString("F2", CultureInfo.InvariantCulture),
                point.raycastTargetPath,
                point.raycastTargetType,
            });
        }

        private static string ComputeHash(IEnumerable<string> values)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (string value in values)
            {
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= prime;
                }
                hash ^= 0xff;
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static UIElementInfo CloneForOutput(UIElementInfo source, int textLimit)
        {
            textLimit = Mathf.Clamp(textLimit, 0, 16384);
            string outputText = source.text;
            bool textTruncated = outputText.Length > textLimit;
            if (textTruncated)
                outputText = outputText.Substring(0, textLimit);
            return new UIElementInfo {
                instanceId = source.instanceId,
                name = source.name,
                path = source.path,
                indexedPath = source.indexedPath,
                scene = source.scene,
                text = outputText,
                textLength = source.text.Length,
                textTruncated = textTruncated,
                actions = source.actions,
                actionPoints = source.actionPoints,
                handlerTypes = source.handlerTypes,
                screenRect = source.screenRect,
                visibleRect = source.visibleRect,
                canvasPath = source.canvasPath,
                renderMode = source.renderMode,
                sortingOrder = source.sortingOrder,
                selected = source.selected,
                scroll = source.scroll,
            };
        }

        private static bool TryGetScreenRect(RectTransform rectTransform, out Rect rect)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            Camera camera = rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 maximum = minimum;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            rect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return rect.width > 0f && rect.height > 0f;
        }

        private static Rect IntersectRect(Rect left, Rect right)
        {
            return Rect.MinMaxRect(Mathf.Max(left.xMin, right.xMin), Mathf.Max(left.yMin, right.yMin), Mathf.Min(left.xMax, right.xMax), Mathf.Min(left.yMax, right.yMax));
        }

        private static RaycastResult FindFirstRaycast(List<RaycastResult> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject != null)
                    return results[i];
            }
            return new RaycastResult();
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

        private static string GetGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;
            var names = new Stack<string>();
            for (Transform current = gameObject.transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static string GetIndexedGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;
            var names = new Stack<string>();
            for (Transform current = gameObject.transform; current != null; current = current.parent)
                names.Push($"{current.name}[{current.GetSiblingIndex()}]");
            return string.Join("/", names);
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Replace('\n', ' ').Trim();
        }

        private static bool ContainsIgnoreCase(string value, string expected)
        {
            return (value ?? string.Empty).IndexOf(expected ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static FloatRect ToFloatRect(Rect rect)
        {
            return new FloatRect { x = rect.x, y = rect.y, width = rect.width, height = rect.height };
        }
    }
}
