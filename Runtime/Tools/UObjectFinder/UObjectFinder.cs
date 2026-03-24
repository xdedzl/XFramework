using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 全局UnityObject查找器，通过key快速查找已注册的UObjectReference
    /// </summary>
    public static class UObjectFinder
    {
        private static readonly Dictionary<string, UObjectReference> references = new();

        public static void Register(UObjectReference reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.Path))
                return;

            if (references.ContainsKey(reference.Path))
            {
                Debug.LogWarning($"[UObjectFinder] Path '{reference.Path}' 已存在，将被覆盖");
            }

            references[reference.Path] = reference;
        }

        public static void Unregister(UObjectReference reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.Path))
                return;

            if (references.TryGetValue(reference.Path, out var existing) && existing == reference)
            {
                references.Remove(reference.Path);
            }
        }

        /// <summary>
        /// 通过路径查找GameObject
        /// 不填key时通过name查找，填key时通过key/name查找
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
        /// 不填key时通过name查找，填key时通过key/name查找
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
        /// 清除所有注册
        /// </summary>
        public static void Clear()
        {
            references.Clear();
        }
    }
}

