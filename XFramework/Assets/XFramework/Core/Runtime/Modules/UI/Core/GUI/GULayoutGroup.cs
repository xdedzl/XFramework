using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    [RequireComponent(typeof(LayoutGroup))]
    public class GULayoutGroup : BaseGUI
    {
        public LayoutGroup layoutGroup;

        /// <summary>
        /// 实体模板
        /// </summary>
        private GameObject m_EntityTemplate;

        /// <summary>
        /// 实体回收事件
        /// </summary>
        public EntityEvent onEntityRecycle;
        /// <summary>
        /// 实体创建事件
        /// </summary>
        public EntityEvent onEntityCreate;
        /// <summary>
        /// 实体添加事件
        /// </summary>
        public EntityEvent onEntityAdd;
        /// <summary>
        /// 内容模板
        /// </summary>

        private void Start()
        {
            //entityRecycle.AddListener((entity) =>
            //{
            //    Destroy(entity);
            //});
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
            // TODO之后可能是从对象池种获取
            GameObject obj = CreateEntity();
            onEntityAdd.Invoke(obj);
            return obj;
        }

        /// <summary>
        /// 移除实体
        /// </summary>
        public void RemoveEntity(GameObject gameObject)
        {
            onEntityRecycle.Invoke(gameObject);
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
                    GameObject.Destroy(transform.GetChild(transform.childCount - 1));
                }
            }
        }

        /// <summary>
        /// 创建一个实体并返回，后期改为从对象池中获取
        /// </summary>
        public GameObject CreateEntity()
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
        }

        public class EntityEvent : UnityEvent<GameObject> { }
    }
}