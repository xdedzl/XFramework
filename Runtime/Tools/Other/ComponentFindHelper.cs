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
        private readonly Dictionary<string, T> m_componentsDic = new Dictionary<string, T>();

        /// <summary>
        /// 构造一个组件查找助手
        /// </summary>
        /// <param name="root">需要查找的根GameObject</param>
        public ComponentFindHelper(GameObject root)
        {
            var uis = root.GetComponentsInChildren<T>(true);
            foreach (var ui in uis)
            {
                // 修改此处：手动向上查找，查到 root 为止
                IComponentFindIgnore ignore = null;
                Transform curr = ui.transform;
                while (curr != null)
                {
                    if (curr == root.transform)
                    {
                        break;
                    }
                    
                    ignore = curr.GetComponent<IComponentFindIgnore>();
                    
                    if (ignore != null)
                    {
                        break;
                    }
                    curr = curr.parent;
                }

                // var ignore = ui.GetComponentInParent<IComponentFindIgnore>();
                if (ignore is MonoBehaviour mb && mb.gameObject != root)
                {
                    continue;
                }

                var keyProvider = ui.GetComponent<IComponentKeyProvider>();
                var key = string.IsNullOrEmpty(keyProvider?.Key) ? ui.name : keyProvider.Key;
                if (!m_componentsDic.TryAdd(key, ui))
                {
                    throw new System.Exception($"{root.name} already have a {typeof(T).Name} component named {key}");
                }
            }
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