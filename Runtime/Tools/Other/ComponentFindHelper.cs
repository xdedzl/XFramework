using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public interface IComponentKeyProvider
    {
        string Key { get; }
    }
    
    public interface IComponentFindIgnore
    {
        
    }
    
    
    /// <summary>
    /// unity组件查找助手
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    public class ComponentFindHelper<T> where T : MonoBehaviour
    {
        public readonly struct ComponentInfo
        {
            public readonly string Key;
            public readonly T Component;

            public ComponentInfo(string key, T component)
            {
                Key = key;
                Component = component;
            }
        }

        private readonly Dictionary<string, T> m_componentsDic = new Dictionary<string, T>();

        /// <summary>
        /// 构造一个组件查找助手
        /// </summary>
        /// <param name="root">需要查找的根GameObject</param>
        public ComponentFindHelper(GameObject root)
        {
            List<ComponentInfo> components = CollectComponents(root);
            for (int i = 0; i < components.Count; i++)
            {
                ComponentInfo component = components[i];
                if (!m_componentsDic.TryAdd(component.Key, component.Component))
                {
                    throw new System.Exception($"{root.name} already have a {typeof(T).Name} component named {component.Key}");
                }
            }
        }

        /// <summary>
        /// 按运行时查找规则收集组件。遇到 root 以下的 IComponentFindIgnore 边界时跳过该组件，
        /// 例如 PanelBase 收集时会忽略 UINodeBase 子树中的 XUIBase。
        /// </summary>
        public static List<ComponentInfo> CollectComponents(GameObject root)
        {
            List<ComponentInfo> result = new List<ComponentInfo>();
            var uis = root.GetComponentsInChildren<T>(true);
            foreach (var ui in uis)
            {
                if (HasFindIgnoreBoundary(root, ui))
                {
                    continue;
                }

                result.Add(new ComponentInfo(GetKey(ui), ui));
            }

            return result;
        }

        private static bool HasFindIgnoreBoundary(GameObject root, T component)
        {
            Transform current = component.transform;
            while (current != null && current != root.transform)
            {
                IComponentFindIgnore ignore = current.GetComponent<IComponentFindIgnore>();
                if (ignore is MonoBehaviour monoBehaviour && monoBehaviour.gameObject != root)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static string GetKey(T component)
        {
            var keyProvider = component.GetComponent<IComponentKeyProvider>();
            return string.IsNullOrEmpty(keyProvider?.Key) ? component.name : keyProvider.Key;
        }

        /// <summary>
        /// 获取对应名称的组件
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public T this[string key]
        {
            get
            {
                if (m_componentsDic.ContainsKey(key))
                    return m_componentsDic[key];
                else
                {
                    throw new System.Exception($"there is no ui component named '{key}' in {this}");
                }
            }
        }

        /// <summary>
        /// 创建一个组件查找助手
        /// </summary>
        /// <param name="root">需要查找的根GameObject</param>
        /// <returns></returns>
        public static ComponentFindHelper<T> CreateHelper(GameObject root)
        {
            return new ComponentFindHelper<T>(root);
        }
    }
}
