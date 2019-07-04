using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 实体管理器
    /// </summary>
    public partial class EntityManager : IGameModule
    {
        /// <summary>
        /// 实体id，创建实体时用
        /// </summary>
        private int m_Id;

        /// <summary>
        /// 存储对应实体容器的字典
        /// </summary>
        private Dictionary<string, EntityContainer> m_EntityContainerDic;
        private Dictionary<int, Entity> m_EntityDic;

        public EntityManager()
        {
            m_EntityContainerDic = new Dictionary<string, EntityContainer>();
            m_EntityDic = new Dictionary<int, Entity>();
        }

        /// <summary>
        /// 添加模板
        /// </summary>
        public void AddTemplate<T>(GameObject template) where T : Entity
        {
            AddTemplate(typeof(T).Name, typeof(T), template);
        }

        /// <summary>
        /// 添加模板
        /// </summary>
        public void AddTemplate<T>(string key, GameObject template) where T : Entity
        {
            AddTemplate(key, typeof(T), template);
        }

        /// <summary>
        /// 设置模板
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="type">类型</param>
        /// <param name="template">模板</param>
        private void AddTemplate(string key, System.Type type, GameObject template)
        {
            if (m_EntityContainerDic.ContainsKey(key))
            {
                Debug.LogWarning("请勿重复添加");
                return;
            }
            EntityContainer container = new EntityContainer(type, template);

            m_EntityContainerDic.Add(key, container);
        }

        /// <summary>
        /// 实例化实体
        /// </summary>
        /// <returns></returns>
        public T Instantiate<T>(Vector3 pos = default, Quaternion quaternion = default) where T : Entity
        {
            string key = typeof(T).Name;
            if (!m_EntityContainerDic.ContainsKey(key))
            {
                Debug.LogWarning("没有对应的实体容器");
                return null;
            }
            else
            {
                var entity = m_EntityContainerDic[key].Instantiate(m_Id++, pos, quaternion);
                m_EntityDic.Add(entity.Id, entity);
                return entity as T;
            }
        }

        /// <summary>
        /// 获取实体容器
        /// </summary>
        /// <param name="entityName">实体名</param>
        public EntityContainer GetContainer(string entityName)
        {
            m_EntityContainerDic.TryGetValue(entityName, out EntityContainer entityContainer);
            return entityContainer;
        }

        /// <summary>
        /// 获取实体
        /// </summary>
        public Entity GetEntity(GameObject gameObject)
        {
            Entity entity = null;
            if (gameObject != null)
            {
                entity = gameObject.GetComponent<Entity>();
                if (entity == null)
                    Debug.LogWarning($"{gameObject.name}不是由实体管理器创建的");
            }

            return entity;
        }

        /// <summary>
        /// 获取实体
        /// </summary>
        /// <param name="entityId">实体Id</param>
        public Entity GetEntity(int entityId)
        {
            m_EntityDic.TryGetValue(entityId, out Entity entity);
            return entity;
        }

        /// <summary>
        /// 获取同名的所有实体
        /// </summary>
        /// <param name="entityName"></param>
        public Entity[] GetEntities(string entityName)
        {
            return GetContainer(entityName)?.GetEntities();
        }

        /// <summary>
        /// 清除未在使用的实体
        /// </summary>
        /// <param name="deleteUselessContainer">是否删除不含实体的容器</param>
        public void Clean(bool deleteUselessContainer = false)
        {
            List<string> keys = new List<string>();
            foreach (var item in m_EntityContainerDic.Values)
            {
                item.Clean();
                if (item.Count <= 0 && deleteUselessContainer)
                    keys.Add(item.name);
            }
            if (deleteUselessContainer)
            {
                foreach (var item in keys)
                {
                    m_EntityContainerDic.Remove(item);
                }
            }
        }

        #region 接口实现

        public int Priority => 10000;

        public void Shutdown()
        {
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (var item in m_EntityContainerDic.Values)
            {
                item.OnUpdate(elapseSeconds, realElapseSeconds);
            }
        }

        #endregion
    }
}