using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XFramework.AutoTest
{
    [Serializable]
    public sealed class AutoTestGameObjectQuery
    {
        public int instanceId;
        public string name;
        public string path;
        public string indexedPath;
        public string component;
        public bool includeInactive;
        public bool all;
        public int maxResults = 100;
        public bool brief;
        public bool compact;
    }

    [Serializable]
    public sealed class AutoTestGameObjectListQuery
    {
        public string name;
        public string path;
        public string component;
        public string tag;
        public string layer;
        public string active = "any";
        public int maxResults = 100;
        public bool compact;
    }

    internal static class AutoTestGameObjects
    {
        [Serializable]
        private sealed class GameObjectStateResult
        {
            public string timestampUtc;
            public int frameCount;
            public float realtimeSinceStartup;
            public int matchCount;
            public int returnedCount;
            public bool truncated;
            public List<GameObjectState> objects = new List<GameObjectState>();
        }

        [Serializable]
        private sealed class GameObjectBriefStateResult
        {
            public string timestampUtc;
            public int frameCount;
            public float realtimeSinceStartup;
            public int matchCount;
            public int returnedCount;
            public bool truncated;
            public List<GameObjectBriefState> objects = new List<GameObjectBriefState>();
        }

        [Serializable]
        private sealed class GameObjectBriefState
        {
            public int instanceId;
            public string name;
            public string path;
            public string indexedPath;
            public string sceneName;
            public bool activeSelf;
            public bool activeInHierarchy;
            public TransformState transform;
        }

        [Serializable]
        private sealed class GameObjectListResult
        {
            public string timestampUtc;
            public int frameCount;
            public int matchCount;
            public int returnedCount;
            public bool truncated;
            public List<GameObjectListItem> objects = new List<GameObjectListItem>();
        }

        [Serializable]
        private sealed class GameObjectListItem
        {
            public int instanceId;
            public string name;
            public string path;
            public string indexedPath;
            public string sceneName;
            public bool activeSelf;
            public bool activeInHierarchy;
            public string tag;
            public int layer;
            public string layerName;
            public List<string> components = new List<string>();
        }

        [Serializable]
        private sealed class GameObjectWaitResult
        {
            public string state;
            public string timestampUtc;
            public int frameCount;
            public float elapsedSeconds;
            public int stableFrames;
            public int matchCount;
            public List<GameObjectBriefState> objects = new List<GameObjectBriefState>();
        }

        [Serializable]
        private sealed class GameObjectState
        {
            public int instanceId;
            public string name;
            public string path;
            public string indexedPath;
            public string sceneName;
            public string scenePath;
            public bool activeSelf;
            public bool activeInHierarchy;
            public string tag;
            public int layer;
            public string layerName;
            public bool isStatic;
            public int childCount;
            public int missingComponentCount;
            public TransformState transform;
            public RigidbodyState rigidbody;
            public List<ComponentState> components = new List<ComponentState>();
        }

        [Serializable]
        private sealed class TransformState
        {
            public Vector3 position;
            public Vector3 localPosition;
            public Quaternion rotation;
            public Quaternion localRotation;
            public Vector3 eulerAngles;
            public Vector3 localEulerAngles;
            public Vector3 localScale;
            public Vector3 lossyScale;
        }

        [Serializable]
        private sealed class RigidbodyState
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 linearVelocity;
            public Vector3 angularVelocity;
            public bool isKinematic;
            public bool useGravity;
            public bool detectCollisions;
            public bool isSleeping;
            public string constraints;
            public string collisionDetectionMode;
        }

        [Serializable]
        private sealed class ComponentState
        {
            public int instanceId;
            public string type;
            public string fullType;
            public bool hasEnabledState;
            public bool enabled;
            public bool activeAndEnabled;
        }

        internal static string GetState(AutoTestGameObjectQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            EnsureSelector(query, "go-state");
            List<GameObject> matches = FindExact(query, query.includeInactive);

            if (matches.Count == 0)
                throw new InvalidOperationException("没有 GameObject 匹配指定选择器。");
            if (!query.all && matches.Count != 1)
                throw new InvalidOperationException($"go-state 需要唯一匹配，当前匹配 {matches.Count} 个：{string.Join(", ", matches.Take(16).Select(FormatCandidate))}");

            int limit = query.all ? Mathf.Clamp(query.maxResults, 1, 1000) : 1;
            if (query.brief)
            {
                var briefResult = new GameObjectBriefStateResult {
                    timestampUtc = DateTime.UtcNow.ToString("O"),
                    frameCount = Time.frameCount,
                    realtimeSinceStartup = Time.realtimeSinceStartup,
                    matchCount = matches.Count,
                    returnedCount = Mathf.Min(matches.Count, limit),
                    truncated = matches.Count > limit,
                    objects = matches.Take(limit).Select(CreateBriefState).ToList(),
                };
                return JsonUtility.ToJson(briefResult, !query.compact);
            }

            var result = new GameObjectStateResult {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                frameCount = Time.frameCount,
                realtimeSinceStartup = Time.realtimeSinceStartup,
                matchCount = matches.Count,
                returnedCount = Mathf.Min(matches.Count, limit),
                truncated = matches.Count > limit,
                objects = matches.Take(limit).Select(CreateState).ToList(),
            };
            return JsonUtility.ToJson(result, !query.compact);
        }

        internal static string List(AutoTestGameObjectListQuery query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            string active = string.IsNullOrEmpty(query.active) ? "any" : query.active.ToLowerInvariant();
            if (active != "any" && active != "active" && active != "inactive")
                throw new ArgumentException($"未知 active 筛选：{query.active}");
            int? layer = ResolveLayer(query.layer);
            List<GameObject> matches = GetLoadedSceneGameObjects()
                .Where(gameObject => ContainsIgnoreCase(gameObject.name, query.name))
                .Where(gameObject => ContainsIgnoreCase(GetGameObjectPath(gameObject), query.path))
                .Where(gameObject => HasComponent(gameObject, query.component))
                .Where(gameObject => string.IsNullOrEmpty(query.tag) || string.Equals(gameObject.tag, query.tag, StringComparison.OrdinalIgnoreCase))
                .Where(gameObject => !layer.HasValue || gameObject.layer == layer.Value)
                .Where(gameObject => active == "any" || (active == "active") == gameObject.activeInHierarchy)
                .OrderBy(gameObject => gameObject.scene.name, StringComparer.Ordinal)
                .ThenBy(GetIndexedGameObjectPath, StringComparer.Ordinal)
                .ToList();
            int limit = Mathf.Clamp(query.maxResults, 1, 1000);
            var result = new GameObjectListResult {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                frameCount = Time.frameCount,
                matchCount = matches.Count,
                returnedCount = Mathf.Min(matches.Count, limit),
                truncated = matches.Count > limit,
                objects = matches.Take(limit).Select(CreateListItem).ToList(),
            };
            return JsonUtility.ToJson(result, !query.compact);
        }

        internal static IEnumerator Wait(AutoTestGameObjectWaitRequest request, AutoTestOperationContext context)
        {
            if (request.query == null)
                throw new ArgumentException("wait-for go 缺少 query。", nameof(request));
            EnsureSelector(request.query, "wait-for go");
            string state = string.IsNullOrEmpty(request.state) ? "exists" : request.state.ToLowerInvariant();
            if (state != "exists" && state != "missing" && state != "active" && state != "inactive" && state != "stable")
                throw new ArgumentException($"wait-for go 不支持状态：{request.state}");
            int requiredStableFrames = Mathf.Clamp(request.stableFrames, 1, 10000);
            if (request.timeoutSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(request.timeoutSeconds), "timeout 必须大于 0。");
            if (request.positionEpsilon < 0f || request.rotationEpsilon < 0f)
                throw new ArgumentOutOfRangeException(nameof(request), "稳定阈值不能小于 0。");

            float started = Time.realtimeSinceStartup;
            int satisfiedFrames = 0;
            int previousInstanceId = 0;
            Vector3 previousPosition = default;
            Quaternion previousRotation = default;
            Vector3 previousScale = default;
            while (Time.realtimeSinceStartup - started <= request.timeoutSeconds)
            {
                List<GameObject> matches = FindExact(request.query, true);
                if ((state == "active" || state == "inactive" || state == "stable") && matches.Count > 1)
                    throw new InvalidOperationException($"wait-for go --state {state} 需要唯一匹配，当前匹配 {matches.Count} 个：{string.Join(", ", matches.Take(16).Select(FormatCandidate))}");
                bool satisfied;
                if (state == "missing")
                    satisfied = matches.Count == 0;
                else if (state == "exists")
                    satisfied = matches.Count > 0;
                else if (state == "active")
                    satisfied = matches.Count == 1 && matches[0].activeInHierarchy;
                else if (state == "inactive")
                    satisfied = matches.Count == 1 && !matches[0].activeInHierarchy;
                else
                    satisfied = IsStable(matches, request, ref previousInstanceId, ref previousPosition, ref previousRotation, ref previousScale);

                satisfiedFrames = satisfied ? satisfiedFrames + 1 : 0;
                if (satisfiedFrames >= requiredStableFrames)
                {
                    var result = new GameObjectWaitResult {
                        state = state,
                        timestampUtc = DateTime.UtcNow.ToString("O"),
                        frameCount = Time.frameCount,
                        elapsedSeconds = Time.realtimeSinceStartup - started,
                        stableFrames = satisfiedFrames,
                        matchCount = matches.Count,
                        objects = matches.Take(16).Select(CreateBriefState).ToList(),
                    };
                    context.SetOutput(JsonUtility.ToJson(result, !request.compact));
                    yield break;
                }
                yield return null;
            }
            throw new TimeoutException($"wait-for go 等待 {state} 超时（{request.timeoutSeconds.ToString("0.###")} 秒）。");
        }

        private static void EnsureSelector(AutoTestGameObjectQuery query, string command)
        {
            if (query.instanceId == 0 && string.IsNullOrEmpty(query.name) && string.IsNullOrEmpty(query.path) && string.IsNullOrEmpty(query.indexedPath) && string.IsNullOrEmpty(query.component))
                throw new ArgumentException($"{command} 至少需要 instance-id、name、path、indexed-path 或 component 选择器之一。", nameof(query));
        }

        private static List<GameObject> FindExact(AutoTestGameObjectQuery query, bool includeInactive)
        {
            return GetLoadedSceneGameObjects()
                .Where(gameObject => includeInactive || gameObject.activeInHierarchy)
                .Where(gameObject => query.instanceId == 0 || gameObject.GetInstanceID() == query.instanceId)
                .Where(gameObject => string.IsNullOrEmpty(query.name) || string.Equals(gameObject.name, query.name, StringComparison.Ordinal))
                .Where(gameObject => string.IsNullOrEmpty(query.path) || string.Equals(GetGameObjectPath(gameObject), query.path, StringComparison.Ordinal))
                .Where(gameObject => string.IsNullOrEmpty(query.indexedPath) || string.Equals(GetIndexedGameObjectPath(gameObject), query.indexedPath, StringComparison.Ordinal))
                .Where(gameObject => HasComponent(gameObject, query.component))
                .OrderBy(gameObject => gameObject.scene.name, StringComparer.Ordinal)
                .ThenBy(GetIndexedGameObjectPath, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<GameObject> GetLoadedSceneGameObjects()
        {
            HashSet<int> loadedSceneHandles = GetLoadedSceneHandles();
            return Resources.FindObjectsOfTypeAll<GameObject>().Where(gameObject => IsLoadedSceneObject(gameObject, loadedSceneHandles));
        }

        private static bool IsStable(List<GameObject> matches, AutoTestGameObjectWaitRequest request, ref int previousInstanceId, ref Vector3 previousPosition, ref Quaternion previousRotation, ref Vector3 previousScale)
        {
            if (matches.Count == 0)
            {
                previousInstanceId = 0;
                return false;
            }

            Transform transform = matches[0].transform;
            int instanceId = matches[0].GetInstanceID();
            bool stable = previousInstanceId == 0 || (previousInstanceId == instanceId && Vector3.Distance(previousPosition, transform.position) <= request.positionEpsilon && Quaternion.Angle(previousRotation, transform.rotation) <= request.rotationEpsilon && Vector3.Distance(previousScale, transform.localScale) <= request.positionEpsilon);
            previousInstanceId = instanceId;
            previousPosition = transform.position;
            previousRotation = transform.rotation;
            previousScale = transform.localScale;
            return stable;
        }

        private static int? ResolveLayer(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            if (int.TryParse(value, out int layer))
            {
                if (layer < 0 || layer > 31)
                    throw new ArgumentException($"layer 必须在 0 到 31 之间：{value}");
                return layer;
            }
            layer = LayerMask.NameToLayer(value);
            if (layer < 0)
                throw new ArgumentException($"未知 Layer：{value}");
            return layer;
        }

        private static bool ContainsIgnoreCase(string value, string expected)
        {
            return string.IsNullOrEmpty(expected) || value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static GameObjectListItem CreateListItem(GameObject gameObject)
        {
            return new GameObjectListItem {
                instanceId = gameObject.GetInstanceID(),
                name = gameObject.name,
                path = GetGameObjectPath(gameObject),
                indexedPath = GetIndexedGameObjectPath(gameObject),
                sceneName = gameObject.scene.name,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                tag = gameObject.tag,
                layer = gameObject.layer,
                layerName = LayerMask.LayerToName(gameObject.layer),
                components = gameObject.GetComponents<Component>().Where(component => component != null).Select(component => component.GetType().Name).ToList(),
            };
        }

        private static HashSet<int> GetLoadedSceneHandles()
        {
            var handles = new HashSet<int>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                handles.Add(SceneManager.GetSceneAt(i).handle);
            return handles;
        }

        private static bool IsLoadedSceneObject(GameObject gameObject, HashSet<int> loadedSceneHandles)
        {
            return gameObject.scene.IsValid() && gameObject.scene.isLoaded && (loadedSceneHandles.Contains(gameObject.scene.handle) || string.Equals(gameObject.scene.name, "DontDestroyOnLoad", StringComparison.Ordinal));
        }

        private static bool HasComponent(GameObject gameObject, string componentName)
        {
            if (string.IsNullOrEmpty(componentName))
                return true;
            return gameObject.GetComponents<Component>().Any(component => component != null && (string.Equals(component.GetType().Name, componentName, StringComparison.OrdinalIgnoreCase) || string.Equals(component.GetType().FullName, componentName, StringComparison.OrdinalIgnoreCase)));
        }

        private static GameObjectBriefState CreateBriefState(GameObject gameObject)
        {
            return new GameObjectBriefState {
                instanceId = gameObject.GetInstanceID(),
                name = gameObject.name,
                path = GetGameObjectPath(gameObject),
                indexedPath = GetIndexedGameObjectPath(gameObject),
                sceneName = gameObject.scene.name,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                transform = CreateTransformState(gameObject.transform),
            };
        }

        private static GameObjectState CreateState(GameObject gameObject)
        {
            Transform transform = gameObject.transform;
            Component[] components = gameObject.GetComponents<Component>();
            var state = new GameObjectState {
                instanceId = gameObject.GetInstanceID(),
                name = gameObject.name,
                path = GetGameObjectPath(gameObject),
                indexedPath = GetIndexedGameObjectPath(gameObject),
                sceneName = gameObject.scene.name,
                scenePath = gameObject.scene.path,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                tag = gameObject.tag,
                layer = gameObject.layer,
                layerName = LayerMask.LayerToName(gameObject.layer),
                isStatic = gameObject.isStatic,
                childCount = transform.childCount,
                missingComponentCount = components.Count(component => component == null),
                transform = CreateTransformState(transform),
            };

            Rigidbody rigidbody = gameObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                state.rigidbody = new RigidbodyState {
                    position = rigidbody.position,
                    rotation = rigidbody.rotation,
                    linearVelocity = GetLinearVelocity(rigidbody),
                    angularVelocity = rigidbody.angularVelocity,
                    isKinematic = rigidbody.isKinematic,
                    useGravity = rigidbody.useGravity,
                    detectCollisions = rigidbody.detectCollisions,
                    isSleeping = rigidbody.IsSleeping(),
                    constraints = rigidbody.constraints.ToString(),
                    collisionDetectionMode = rigidbody.collisionDetectionMode.ToString(),
                };
            }

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;
                Type type = component.GetType();
                var componentState = new ComponentState {
                    instanceId = component.GetInstanceID(),
                    type = type.Name,
                    fullType = type.FullName,
                };
                if (component is Behaviour behaviour)
                {
                    componentState.hasEnabledState = true;
                    componentState.enabled = behaviour.enabled;
                    componentState.activeAndEnabled = behaviour.isActiveAndEnabled;
                }
                else if (component is Renderer renderer)
                {
                    componentState.hasEnabledState = true;
                    componentState.enabled = renderer.enabled;
                    componentState.activeAndEnabled = renderer.enabled && gameObject.activeInHierarchy;
                }
                else if (component is Collider collider)
                {
                    componentState.hasEnabledState = true;
                    componentState.enabled = collider.enabled;
                    componentState.activeAndEnabled = collider.enabled && gameObject.activeInHierarchy;
                }
                state.components.Add(componentState);
            }
            return state;
        }

        private static TransformState CreateTransformState(Transform transform)
        {
            return new TransformState {
                position = transform.position,
                localPosition = transform.localPosition,
                rotation = transform.rotation,
                localRotation = transform.localRotation,
                eulerAngles = transform.eulerAngles,
                localEulerAngles = transform.localEulerAngles,
                localScale = transform.localScale,
                lossyScale = transform.lossyScale,
            };
        }

        private static Vector3 GetLinearVelocity(Rigidbody rigidbody)
        {
#if UNITY_6000_0_OR_NEWER
            return rigidbody.linearVelocity;
#else
            return rigidbody.velocity;
#endif
        }

        private static string FormatCandidate(GameObject gameObject)
        {
            return $"{gameObject.scene.name}:{GetIndexedGameObjectPath(gameObject)}";
        }

        private static string GetGameObjectPath(GameObject gameObject)
        {
            var names = new Stack<string>();
            for (Transform current = gameObject.transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static string GetIndexedGameObjectPath(GameObject gameObject)
        {
            var names = new Stack<string>();
            for (Transform current = gameObject.transform; current != null; current = current.parent)
                names.Push($"{current.name}[{current.GetSiblingIndex()}]");
            return string.Join("/", names);
        }
    }
}
