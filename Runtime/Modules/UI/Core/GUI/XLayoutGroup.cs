using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace XFramework.UI
{
    [RequireComponent(typeof(LayoutGroup))]
    [UnityEngine.AddComponentMenu("XFramework/XLayoutGroup")]
    public class XLayoutGroup : XUIBase
    {
        public LayoutGroup layoutGroup;

        /// <summary>
        /// 实体模板
        /// </summary>
        private GameObject m_EntityTemplate;
        private Transform m_PoolParent;
        /// <summary>
        /// 实体池
        /// </summary>
        private Stack<GameObject> m_EntityPool;
        
        [HideInInspector] public EntityEvent onEntityChange;
        

        public int itemCount { get; private set; } = 0;

        private void Awake()
        {
            onEntityChange = new EntityEvent();
            m_EntityPool = new Stack<GameObject>();

            if (m_EntityTemplate == null)
            {
                m_EntityTemplate = transform.GetChild(0).gameObject;
            }

            if (m_EntityTemplate == null)
            {
                throw new System.Exception("XLayoutGroup需要一个实体模板，请在编辑器中指定，或在运行时将一个子物体作为模板");
            }
            
            m_EntityTemplate.name += "(Template)";
            m_EntityTemplate.SetActive(false);

            if (transform.childCount > 1)
            {
                for (int i = transform.childCount - 1; i > 0; i--)
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }
        }

        private void Reset()
        {
            layoutGroup = transform.GetComponent<LayoutGroup>();
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        private GameObject AddEntity()
        {
            GameObject obj = GetEntity();
            return obj;
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        private void RemoveEntity(GameObject gameObject)
        {
            m_EntityPool.Push(gameObject);

            if(m_PoolParent == null)
                m_PoolParent = new GameObject(name + "_Pool").transform;

            gameObject.transform.SetParent(m_PoolParent);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="index"></param>
        private void RemoveEntity(int index)
        {
            RemoveEntity(layoutGroup.transform.GetChild(index).gameObject);
        }

        /// <summary>
        /// 配置实体数量
        /// </summary>
        public void SetItemCount(int targetCount)
        {
            int currentCount = itemCount;
            int differ = targetCount - currentCount;

            // 先处理数量变多的情况
            if (differ > 0)
            {
                // 先激活已有的inactive对象（从第2个开始）
                int activated = 0;
                for (int i = 1; i < transform.childCount && activated < differ; i++)
                {
                    var go = transform.GetChild(i).gameObject;
                    if (!go.activeSelf)
                    {
                        go.SetActive(true);
                        activated++;
                    }
                    
                    onEntityChange.Invoke(i - 1, go);
                }
                // 不够则创建
                for (int i = activated; i < differ; i++)
                {
                    var go = AddEntity();
                    go.SetActive(true);
                    onEntityChange.Invoke(i, go);
                }
            }
            // 数量变少时，仅将多余的active对象设为false（从后往前，跳过模板）
            else if (differ < 0)
            {
                int toDisable = -differ;
                for (int i = transform.childCount - 1; i > 0 && toDisable > 0; i--)
                {
                    var go = transform.GetChild(i).gameObject;
                    if (go.activeSelf)
                    {
                        go.SetActive(false);
                        toDisable--;
                    }
                }
            }
            
            itemCount = targetCount;
        }

        /// <summary>
        /// 获取一个实体并返回
        /// </summary>
        private GameObject GetEntity()
        {
            if (m_EntityPool.Count > 0)
            {
                GameObject obj = m_EntityPool.Pop();
                obj.transform.SetParent(this.transform);
                return obj;
            }

            return CreateEntity();
        }

        /// <summary>
        /// 新创建一个实体并返回
        /// </summary>
        /// <returns></returns>
        private GameObject CreateEntity()
        {
            GameObject obj = Instantiate(m_EntityTemplate, transform);
            return obj;
        }

        /// <summary>
        /// 删除所有子物体
        /// </summary>
        public void Clear()
        {
            for (int i = layoutGroup.transform.childCount; i > 0; i--)
            {
                Destroy(layoutGroup.transform.GetChild(i - 1).gameObject);
            }
        }

        public class EntityEvent : UnityEvent<int, GameObject> { }
    }
}