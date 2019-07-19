using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Entity
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

        public void A<T>(object p, Vector3 vector3)
        {
            throw new NotImplementedException();
        }

        public EntityManager()
        {
            m_EntityContainerDic = new Dictionary<string, EntityContainer>();
            m_EntityDic = new Dictionary<int, Entity>();
        }

        private int NextId
        {
            get
            {
                m_Id++;
                if (m_EntityDic.ContainsKey(m_Id))
                    return NextId;
                else
                    return m_Id;
            }
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
        public void AddTemplate(string key, System.Type type, GameObject template)
        {
            if (m_EntityContainerDic.ContainsKey(key))
            {
                Debug.LogWarning("请勿重复添加");
                return;
            }
            EntityContainer container = new EntityContainer(type, key, template);

            m_EntityContainerDic.Add(key, container);
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <returns></returns>
        public T Allocate<T>(Vector3 pos = default, Quaternion quaternion = default) where T : Entity
        {
            return Allocate(typeof(T).Name, NextId, null, pos, quaternion) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="entityData">实体数据</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <returns>实体</returns>
        public T Allocate<T>(EntityData entityData, Vector3 pos = default, Quaternion quaternion = default) where T : Entity
        {
            string key = typeof(T).Name;
            return Allocate(key, NextId, entityData, pos, quaternion) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        public T Allocate<T>(string key, EntityData entityData, Vector3 pos = default, Quaternion quaternion = default) where T : Entity
        {
            return Allocate(key, NextId, entityData, pos, quaternion) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <returns>实体</returns>
        public Entity Allocate(string key, Vector3 pos = default, Quaternion quaternion = default)
        {
            return Allocate(key, NextId, null, pos, quaternion);
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="id">实体Id</param>
        /// <param name="entityData">实体信息</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">角度</param>
        /// <returns></returns>
        public Entity Allocate(string key, int id, EntityData entityData, Vector3 pos, Quaternion quaternion)
        {
            if (!m_EntityContainerDic.ContainsKey(key))
            {
                Debug.LogWarning("没有对应的实体容器");
                return null;
            }
            else
            {
                var entity = m_EntityContainerDic[key].Allocate(id, pos, quaternion, entityData);
                m_EntityDic.Add(entity.Id, entity);
                return entity;
            }
        }

        /// <summary>
        /// 回收实体
        /// </summary>
        /// <param name="entity">目标实体</param>
        public bool Recycle(Entity entity)
        {
            EntityContainer container = GetContainer(entity.ContainerName);
            if (container != null)
            {
                m_EntityDic.Remove(entity.Id);
                return container.Recycle(entity);
            }
            return false;
        }

        /// <summary>
        /// 回收实体
        /// </summary>
        /// <param name="id">目标实体Id</param>
        public bool Recycle(int id)
        {
            Entity entity = GetEntity(id);
            if(entity != null)
            {
                return Recycle(entity);
            }
            return false;
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
            m_EntityContainerDic = null;
            m_EntityDic = null;
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