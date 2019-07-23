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
        /// <summary>
        /// 存储所有在使用的实体字典
        /// </summary>
        private Dictionary<int, Entity> m_EntityDic;
        /// <summary>
        /// 存储所有实体（在使用的，被回收在池中的）
        /// </summary>
        private Dictionary<int, EntityInfo> m_EntityInfoDic;

        public EntityManager()
        {
            m_EntityContainerDic = new Dictionary<string, EntityContainer>();
            m_EntityDic = new Dictionary<int, Entity>();
            m_EntityInfoDic = new Dictionary<int, EntityInfo>();
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
            if(entity != null)
            {
                EntityContainer container = GetContainer(entity.ContainerName);
                if (container != null)
                {
                    m_EntityDic.Remove(entity.Id);
                    return container.Recycle(entity);
                }
                else
                {
                    throw new FrameworkExecption("[Entity] this entity is not created by manager");
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
            if(entity != null)
            {
                return Recycle(entity);
            }
            return false;
        }

        /// <summary>
        /// 附加实体，将child附加到parent上
        /// </summary>
        /// <param name="child"></param>
        /// <param name="parent"></param>
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

            if(childInfo.Parent != null)
            {
                childInfo.Parent.OnDetached(child);
                child.OnDetachFrom(childInfo.Parent);
                childInfo.Parent = null;
            }
        }

        /// <summary>
        /// 获取实体容器
        /// </summary>
        /// <param name="containerName">实体名</param>
        public EntityContainer GetContainer(string containerName)
        {
            m_EntityContainerDic.TryGetValue(containerName, out EntityContainer entityContainer);
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
            List<string> keys = new List<string>();
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
            if(container != null)
            {
                InternalClean(container, count);
            }
            else
            {
                throw new FrameworkExecption("[EntityContainer] null container");
            }
        }

        private void InternalClean(EntityContainer entityContainer, int count)
        {
             entityContainer.Clean(count, (id) => { m_EntityInfoDic.Remove(id); });
        }

        #region 接口实现

        public int Priority => 10000;

        public void Shutdown()
        {
            m_EntityContainerDic = null;
            m_EntityDic = null;
            m_EntityInfoDic = null;
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