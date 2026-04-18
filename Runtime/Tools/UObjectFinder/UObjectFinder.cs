using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XFramework
{
    /// <summary>
    /// 全局UnityObject查找器，通过key快速查找已注册的UObjectReference
    /// </summary>
    public static class UObjectFinder
    {
        private static readonly Dictionary<string, UObjectReference> references = new();
        private static readonly Dictionary<string, List<UObjectReference>> listReferences = new();

        public static void Register(UObjectReference reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.Path))
            {
                return;
            }

            if (reference.Mode == UObjectReference.RegistrationMode.List)
            {
                if (!listReferences.TryGetValue(reference.Path, out var referenceList))
                {
                    referenceList = new List<UObjectReference>();
                    listReferences[reference.Path] = referenceList;
                }

                if (!referenceList.Contains(reference))
                {
                    referenceList.Add(reference);
                }

                return;
            }

            if (references.ContainsKey(reference.Path))
            {
                Debug.LogWarning($"[UObjectFinder] Path '{reference.Path}' 已存在，将被覆盖");
            }

            references[reference.Path] = reference;
        }

        public static void Unregister(UObjectReference reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.Path))
            {
                return;
            }

            if (reference.Mode == UObjectReference.RegistrationMode.List)
            {
                if (!listReferences.TryGetValue(reference.Path, out var referenceList))
                {
                    return;
                }

                referenceList.Remove(reference);
                if (referenceList.Count == 0)
                {
                    listReferences.Remove(reference.Path);
                }

                return;
            }

            if (references.TryGetValue(reference.Path, out var existing) && existing == reference)
            {
                references.Remove(reference.Path);
            }
        }

        /// <summary>
        /// 通过路径查找GameObject
        /// 不填key时通过name查找，填key时通过key查找
        /// </summary>
        public static GameObject Find(string path)
        {
            if (references.TryGetValue(path, out var reference) && reference != null)
            {
                return reference.gameObject;
            }

            return null;
        }

        /// <summary>
        /// 通过路径查找指定类型的组件
        /// 不填key时通过name查找，填key时通过key查找
        /// </summary>
        public static T Find<T>(string path) where T : Component
        {
            if (references.TryGetValue(path, out var reference) && reference != null)
            {
                return reference.GetComponent<T>();
            }

            return null;
        }

        /// <summary>
        /// 通过列表key查找所有GameObject
        /// </summary>
        public static IReadOnlyList<GameObject> FindList(string key)
        {
            List<UObjectReference> sortedReferences = GetSortedListReferences(key);
            if (sortedReferences.Count == 0)
            {
                return Array.Empty<GameObject>();
            }

            var result = new List<GameObject>(sortedReferences.Count);
            foreach (var reference in sortedReferences)
            {
                if (reference != null)
                {
                    result.Add(reference.gameObject);
                }
            }

            return result;
        }

        /// <summary>
        /// 通过列表key查找所有指定类型的组件
        /// </summary>
        public static IReadOnlyList<T> FindList<T>(string key) where T : Component
        {
            List<UObjectReference> sortedReferences = GetSortedListReferences(key);
            if (sortedReferences.Count == 0)
            {
                return Array.Empty<T>();
            }

            var result = new List<T>(sortedReferences.Count);
            foreach (var reference in sortedReferences)
            {
                if (reference == null)
                {
                    continue;
                }

                T component = reference.GetComponent<T>();
                if (component != null)
                {
                    result.Add(component);
                }
            }

            return result;
        }

        /// <summary>
        /// 清除所有注册
        /// </summary>
        public static void Clear()
        {
            references.Clear();
            listReferences.Clear();
        }

        private static List<UObjectReference> GetSortedListReferences(string key)
        {
            if (!listReferences.TryGetValue(key, out var referenceList) || referenceList.Count == 0)
            {
                return new List<UObjectReference>();
            }

            var result = new List<UObjectReference>(referenceList.Count);
            foreach (var reference in referenceList)
            {
                if (reference != null)
                {
                    result.Add(reference);
                }
            }

            result.Sort(CompareReferencesByHierarchy);
            return result;
        }

        private static int CompareReferencesByHierarchy(UObjectReference left, UObjectReference right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int sceneOrderCompare = GetLoadedSceneOrder(left.gameObject.scene).CompareTo(GetLoadedSceneOrder(right.gameObject.scene));
            if (sceneOrderCompare != 0)
            {
                return sceneOrderCompare;
            }

            var leftIndices = GetSiblingIndexPath(left.transform);
            var rightIndices = GetSiblingIndexPath(right.transform);
            int count = Math.Min(leftIndices.Count, rightIndices.Count);
            for (int i = 0; i < count; i++)
            {
                int compare = leftIndices[i].CompareTo(rightIndices[i]);
                if (compare != 0)
                {
                    return compare;
                }
            }

            if (leftIndices.Count != rightIndices.Count)
            {
                return leftIndices.Count.CompareTo(rightIndices.Count);
            }

            return string.Compare(left.transform.name, right.transform.name, StringComparison.Ordinal);
        }

        private static int GetLoadedSceneOrder(Scene scene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i) == scene)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        private static List<int> GetSiblingIndexPath(Transform transform)
        {
            var indices = new List<int>();
            while (transform != null)
            {
                indices.Add(transform.GetSiblingIndex());
                transform = transform.parent;
            }

            indices.Reverse();
            return indices;
        }
    }
}
