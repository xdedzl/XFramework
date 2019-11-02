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
        /// 存储对应实体容器的字典
        /// </summary>
        private Dictionary<string, EntityContainer> m_EntityContainerDic;
        /// <summary>
        /// 存储所有在使用的实体字典
        /// </summary>
        private Dictionary<int, Entity> m_EntityDic;
        /// <summary>
        /// 存储实体父子关系的字典
        /// </summary>
        private Dictionary<int, EntityInfo> m_EntityInfoDic;

        public EntityManager()
        {
            m_EntityContainerDic = new Dictionary<string, EntityContainer>();
            m_EntityDic = new Dictionary<int, Entity>();
            m_EntityInfoDic = new Dictionary<int, EntityInfo>();
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
        /// 移除一个模板
        /// </summary>
        /// <param name="key">key</param>
        public void RemoveTemplate(string key)
        {
            var entitys = GetEntities(key);
            if (entitys != null)
            {
                foreach (var item in entitys)
                {
                    m_EntityDic.Remove(item.Id);
                    m_EntityInfoDic.Remove(item.Id);
                    GameObject.Destroy(item.gameObject);
                }
                m_EntityContainerDic.Remove(key);
            }
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <returns></returns>
        public T Allocate<T>(int id, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return Allocate(id, typeof(T).Name, null, pos, quaternion, parent) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="entityData">实体数据</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <returns>实体</returns>
        public T Allocate<T>(int id, EntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            string key = typeof(T).Name;
            return Allocate(id, key, entityData, pos, quaternion, parent) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        public T Allocate<T>(int id, string key, EntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return Allocate(id, key, entityData, pos, quaternion, parent) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <returns>实体</returns>
        public Entity Allocate(int id, string key, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null)
        {
            return Allocate(id, key, null, pos, quaternion, parent);
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
        public Entity Allocate(int id, string key, EntityData entityData, Vector3 pos, Quaternion quaternion, Transform parent)
        {
            if (!m_EntityContainerDic.ContainsKey(key))
            {
                Debug.LogWarning($"没有名为{key}的实体容器");
                return null;
            }
            else
            {
                var entity = m_EntityContainerDic[key].Allocate(id, pos, quaternion, entityData, parent);
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
            if (entity != null)
            {
                EntityContainer container = GetContainer(entity.ContainerName);
                if (container != null)
                {
                    m_EntityDic.Remove(entity.Id);
                    return container.Recycle(entity);
                }
                else
                {
                    throw new FrameworkException("[Entity] this entity is not created by manager");
                }
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
            if (entity != null)
            {
                Detach(entity);
                m_EntityInfoDic.Remove(id);
                return Recycle(entity);
            }
            return false;
        }

        /// <summary>
        /// 附加实体，将child附加到parent上
        /// </summary>
        /// <param name="child">子实体</param>
        /// <param name="parent">父实体</param>
        public void Attach(Entity child, Entity parent)
        {
            EntityInfo childInfo = GetEntityInfo(child);
            EntityInfo parendInfo = GetEntityInfo(parent);

            childInfo.Parent = parent;
            parendInfo.AddChild(child);

            child.OnAttachTo(parent);
            parent.OnAttached(child);
        }

        /// <summary>
        /// 移除实体，将child从它的父物体上移除
        /// </summary>
        /// <param name="child"></param>
        /// <param name="parent"></param>
        public void Detach(Entity child)
        {
            EntityInfo childInfo = GetEntityInfo(child);

            if (childInfo.Parent != null)
            {
                childInfo.Parent.OnDetached(child);
                child.OnDetachFrom(childInfo.Parent);
                childInfo.Parent = null;
            }
        }

        /// <summary>
        /// 移除父实体上的所有子实体
        /// </summary>
        /// <param name="parent"></param>
        public void DetachChilds(Entity parent)
        {
            EntityInfo parentInfo = GetEntityInfo(parent);

            if (parent != null)
            {
                foreach (var item in parentInfo.GetChilds())
                {
                    Detach(item);
                }
                m_EntityInfoDic.Remove(parent.Id);
            }
        }

        /// <summary>
        /// 获取实体容器
        /// </summary>
        /// <param name="containerName">实体名</param>
        public EntityContainer GetContainer(string containerName)
        {
            if (m_EntityContainerDic.TryGetValue(containerName, out EntityContainer entityContainer))
            {
                return entityContainer;
            }
            else
            {
                throw new FrameworkException($"[EntityError]没有名为{containerName}的实体容器");
            }
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
            if (m_EntityDic.TryGetValue(entityId, out Entity entity))
            {
                return entity;
            }
            else
            {
                throw new FrameworkException($"[Entity]没有id为{entityId}的实体");
            }
        }

        /// <summary>
        /// 获取同名的所有实体
        /// </summary>
        /// <param name="entityName"></param>
        public Entity[] GetEntities(string entityName)
        {
            return GetContainer(entityName).GetEntities();
        }

        /// <summary>
        /// 获取一个实体的父子关系
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private EntityInfo GetEntityInfo(Entity entity)
        {
            if (m_EntityInfoDic.TryGetValue(entity.Id, out EntityInfo entityInfo))
            {
                return entityInfo;
            }
            else
            {
                EntityInfo info = new EntityInfo(entity);
                m_EntityInfoDic.Add(entity.Id, info);
                return info;
            }
        }

        /// <summary>
        /// 清理实体池
        /// </summary>
        /// <param name="count">容器实体池的最大保留量</param>
        public void Clean(int count = 0)
        {
            foreach (var item in m_EntityContainerDic.Values)
            {
                InternalClean(item, count);
            }
        }

        /// <summary>
        /// 清理某个容器实体池
        /// </summary>
        /// <param name="containerName">容器名</param>
        /// <param name="count">容器实体池的最大保留量</param>
        public void Clean(string containerName, int count)
        {
            EntityContainer container = GetContainer(containerName);
            if (container != null)
            {
                InternalClean(container, count);
            }
            else
            {
                throw new FrameworkException("[EntityContainer] null container");
            }
        }

        private void InternalClean(EntityContainer entityContainer, int count)
        {
            entityContainer.Clean(count);
        }

        #region 接口实现

        public int Priority => 10000;

        public void Shutdown()
        {
            m_EntityContainerDic.Clear();
            m_EntityDic.Clear();
            m_EntityInfoDic.Clear();
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