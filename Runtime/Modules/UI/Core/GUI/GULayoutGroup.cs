using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace XFramework.UI
{
    [RequireComponent(typeof(LayoutGroup))]
    [UnityEngine.AddComponentMenu("XFramework/GULayoutGroup")]
    public class GULayoutGroup : GUIBase
    {
        public LayoutGroup layoutGroup;

        /// <summary>
        /// 实体模板
        /// </summary>
        private GameObject m_EntityTemplate;
        private Transform m_PoolParnet;
        /// <summary>
        /// 实体池
        /// </summary>
        private Stack<GameObject> m_EntityPool;

        /// <summary>
        /// 实体回收事件
        /// </summary>
        [HideInInspector] public EntityEvent onEntityRecycle;
        /// <summary>
        /// 实体创建事件
        /// </summary>
        [HideInInspector] public EntityEvent onEntityCreate;
        /// <summary>
        /// 实体添加事件
        /// </summary>
        [HideInInspector] public EntityEvent onEntityAdd;

        private void Awake()
        {
            onEntityRecycle = new EntityEvent();
            onEntityCreate = new EntityEvent();
            onEntityAdd = new EntityEvent();
            m_EntityPool = new Stack<GameObject>();
        }

        private void Reset()
        {
            layoutGroup = transform.GetComponent<LayoutGroup>();
        }

        /// <summary>
        /// 添加实体
        /// </summary>
        public GameObject AddEntity()
        {
            GameObject obj = GetEntity();
            onEntityAdd.Invoke(obj);        // 触发添加事件
            return obj;
        }

        public GameObject AddEntity(int index)
        {
            return null;
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        public void RemoveEntity(GameObject gameObject)
        {
            onEntityRecycle.Invoke(gameObject);
            m_EntityPool.Push(gameObject);
            gameObject.transform.SetParent(m_PoolParnet);
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        /// <param name="index"></param>
        public void RemoveEntity(int index)
        {
            RemoveEntity(layoutGroup.transform.GetChild(index).gameObject);
        }

        /// <summary>
        /// 配置实体数量
        /// </summary>
        public void ConfigEntity(int count)
        {
            int differ = count - transform.childCount;

            if (differ > 0)         // 当目标Item数量大于现有Item数量时补充Item
            {
                for (int i = 0; i < differ; i++)
                {
                    AddEntity();
                }
            }
            else if (differ < 0)    // 当目标Item数量小于现有Item数量时删除Item
            {
                for (int i = 0; i < -differ; i++)
                {
                    // 暂时写销毁，后期改为对象池回收
                    RemoveEntity(transform.childCount - 1);
                }
            }
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
            onEntityCreate.Invoke(obj);
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

        /// <summary>
        /// 设置实体模板
        /// </summary>
        /// <param name="template">模板</param>
        public void SetEntity(GameObject template)
        {
            m_EntityTemplate = template;
            m_EntityTemplate.transform.position = Vector3.up * 100000;
            m_PoolParnet = new GameObject("Layout" + template.name).transform;
        }

        public class EntityEvent : UnityEvent<GameObject> { }
    }
}