using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// unity组件查找助手
    /// </summary>
    /// <typeparam name="T">组件类型</typeparam>
    public class ComponentFindHelper<T> where T : MonoBehaviour
    {
        private Dictionary<string, T> m_componentsDic = new Dictionary<string, T>();

        /// <summary>
        /// 构造一个组件查找助手
        /// </summary>
        /// <param name="root">需要查找的根GameObject</param>
        public ComponentFindHelper(GameObject root)
        {
            T[] uis = root.GetComponentsInChildren<T>();
            for (int i = 0; i < uis.Length; i++)
            {
                if (m_componentsDic.ContainsKey(uis[i].name))
                {
                    throw new System.Exception($"{root.name} already have a {typeof(T).Name} component named {uis[i].name}");
                }
                m_componentsDic.Add(uis[i].name, uis[i]);
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