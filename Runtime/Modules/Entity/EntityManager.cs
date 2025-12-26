using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Entity
{
    /// <summary>
    /// 实体管理器
    /// </summary>
    public partial class EntityManager : PersistentMonoGameModuleBase<EntityManager>
    {
        /// <summary>
        /// 存储对应实体容器的字典
        /// </summary>
        private readonly Dictionary<string, EntityContainer> m_EntityContainerDic = new();
        /// <summary>
        /// 存储所有在使用的实体字典
        /// </summary>
        private readonly Dictionary<string, Entity> m_EntityDic = new();
        /// <summary>
        /// 存储实体父子关系的字典(从实际经验看大多数entity都不需要attach/detach,所以不直接存在Entity中)
        /// </summary>
        private readonly Dictionary<string, EntityInfo> m_EntityInfoDic = new();

        #region 增删改

        /// <summary>
        /// 添加模板
        /// </summary>
        public void AddTemplate<T>(GameObject template) where T : Entity
        {
            AddTemplate<T>(typeof(T).Name, template);
        }

        /// <summary>
        /// 添加模板
        /// </summary>
        public void AddTemplate<T>(string key, GameObject template) where T : Entity
        {
            AddTemplate<T>(key, template, key);
        }

        /// <summary>
        /// 添加模板
        /// </summary>
        public void AddTemplate<T>(string key, GameObject template, string entityRootName) where T : Entity
        {
            AddTemplate(key, typeof(T), template, entityRootName);
        }

        /// <summary>
        /// 设置模板
        /// </summary>
        public void AddTemplate(string key, Type type, GameObject template)
        {
            AddTemplate(key, type, template, key);
        }

        /// <summary>
        /// 设置模板
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="type">类型</param>
        /// <param name="template">模板</param>
        public void AddTemplate(string key, Type type, GameObject template, string entityRootName)
        {
            if (m_EntityContainerDic.ContainsKey(key))
            {
                Debug.LogWarning("请勿重复添加");
                return;
            }
            EntityContainer container = new(type, key, template, entityRootName);

            m_EntityContainerDic.Add(key, container);
        }

        /// <summary>
        /// 移除一个模板
        /// </summary>
        /// <param name="key">key</param>
        public void RemoveTemplate(string key)
        {
            if(TryGetContainer(key, out var container))
            {
                container.Clean(0);
                var entities = container.GetEntities();
                if (entities != null)
                {
                    foreach (var item in entities)
                    {
                        m_EntityDic.Remove(item.Id);
                        m_EntityInfoDic.Remove(item.Id);
                        UnityEngine.Object.Destroy(item.gameObject);
                    }
                }
                m_EntityContainerDic.Remove(key);
            }
        }

        /// <summary>
        /// 是否包含一个模板
        /// </summary>
        /// <param name="key"></param>
        public bool ContainsTemplate(string key)
        {
            return m_EntityContainerDic.ContainsKey(key);
        }


        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="id">实体编号</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <param name="parent">实体父物体</param>
        /// <returns>实体</returns>
        public T Allocate<T>(Vector3 pos = default, Quaternion quaternion = default, Transform parent = null, string id = null) where T : Entity
        {
            return Allocate<T>(entityData:null, pos, quaternion, parent, id);
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="id">实体编号</param>
        /// <param name="entityData">实体数据</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <param name="parent">实体父物体</param>
        /// <returns>实体</returns>
        public T Allocate<T>(IEntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null, string id = null) where T : Entity
        {
            string key = typeof(T).Name;
            if(!TryGetContainer(key, out EntityContainer _))
            {
                var obj = new GameObject(key + "template");
                AddTemplate<T>(obj);
            }
            return Allocate(key, entityData, pos, quaternion, parent, id) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="id">实体编号</param>
        /// <param name="key">键值</param>
        /// <param name="entityData">实体数据</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <param name="parent">实体父物体</param>
        /// <returns>实体</returns>
        public T Allocate<T>(string key, IEntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null, string id = null) where T : Entity
        {
            return Allocate(key, entityData, pos, quaternion, parent, id) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <param name="id">实体编号</param>
        /// <returns>实体</returns>
        public Entity Allocate(string key)
        {
            return Allocate(key, null);
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <param name="key">键值</param>
        /// <returns>实体</returns>
        public T Allocate<T>(string key) where T : Entity
        {
            return Allocate(key, null) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <typeparam name="T">实体子类型</typeparam>
        /// <param name="id">实体编号</param>
        /// <param name="key">键值</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <param name="parent">实体父物体</param>
        /// <returns>实体</returns>
        public T Allocate<T>(string key, Vector3 pos, Quaternion quaternion = default, Transform parent = null, string id = null) where T : Entity
        {
            return Allocate(key, null, pos, quaternion, parent, id) as T;
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <param name="id">实体编号</param>
        /// <param name="key">键值</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">朝向</param>
        /// <param name="parent">实体父物体</param>
        /// <returns>实体</returns>
        public Entity Allocate(string key, Vector3 pos, Quaternion quaternion = default, Transform parent = null, string id = null)
        {
            return Allocate(key, null, pos, quaternion, parent, id);
        }

        /// <summary>
        /// 分配实体
        /// </summary>
        /// <param name="id">实体Id</param>
        /// <param name="key">键值</param>
        /// <param name="entityData">实体数据</param>
        /// <param name="pos">位置</param>
        /// <param name="quaternion">角度</param>
        /// <param name="parent">实体父物体</param>
        /// <returns></returns>
        public Entity Allocate(string key, IEntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null, string id = null)
        {
            if (!TryGetContainer(key, out EntityContainer _))
            {
                var obj = new GameObject(key + "template");
                AddTemplate<CommonEntity>(obj);
            }
            var entityContainer = GetContainer(key);
            id ??= Guid.NewGuid().ToString();

            if (m_EntityDic.ContainsKey(id))
            {
                Entity e = m_EntityDic[id];
                throw new XFrameworkException($"[EntityError] id is already occupied.  Entity {e}");
            }

            var entity = entityContainer.Allocate(id, pos, quaternion, entityData, parent);
            m_EntityDic.Add(entity.Id, entity);
            return entity;
        }
        
        internal void RegisterExistEntity(string templateKey, GameObject entityObj)
        {
            var container = GetContainer(templateKey);
            var id = Guid.NewGuid().ToString();
            var entity = container.RegisterExistEntity(id, entityObj);
            m_EntityDic.Add(entity.Id, entity);
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
                    Detach(entity);
                    m_EntityInfoDic.Remove(entity.Id);
                    m_EntityDic.Remove(entity.Id);
                    return container.Recycle(entity);
                }
                else
                {
                    throw new XFrameworkException("[Entity] this entity is not created by manager");
                }
            }
            return false;
        }

        /// <summary>
        /// 回收实体
        /// </summary>
        /// <param name="id">目标实体Id</param>
        public bool Recycle(string id)
        {
            Entity entity = GetEntity(id);
            if (entity != null)
            {
                return Recycle(entity);
            }
            return false;
        }

        public void RecycleContainer<T>() where T : Entity
        {
            RecycleContainer(typeof(T).Name);
        }

        public void RecycleContainer(string containerName)
        {
            if (TryGetContainer(containerName, out var container))
            {
                foreach (var entity in container.GetEntities())
                {
                    Recycle(entity);
                }
            }
        }

        /// <summary>
        /// 回收所有实体
        /// </summary>
        public void RecycleAll()
        {
            foreach (var item in m_EntityDic.Values.ToList())
            {
                Recycle(item);
            }
        }


        /// <summary>
        /// 附加实体，将child附加到parent上
        /// </summary>
        /// <param name="child">子实体</param>
        /// <param name="parent">父实体</param>
        public void Attach(Entity child, Entity parent)
        {
            EntityInfo childInfo = GetEntityInfo(child);
            EntityInfo parentInfo = GetEntityInfo(parent);

            childInfo.Parent = parent;
            parentInfo.AddChild(child);

            child.OnAttachTo(parent);
            parent.OnAttached(child);
        }

        /// <summary>
        /// 移除实体，将child从它的父物体上移除
        /// </summary>
        /// <param name="child">子实体</param>
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
        /// <param name="parent">父实体</param>
        public void DetachChildren(Entity parent)
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

        #endregion

        #region 查

        /// <summary>
        /// 获取实体容器
        /// </summary>
        /// <param name="containerName">实体名</param>
        private EntityContainer GetContainer(string containerName)
        {
            if (TryGetContainer(containerName, out EntityContainer entityContainer))
            {
                return entityContainer;
            }
            else
            {
                throw new XFrameworkException($"[EntityError] There is no entity container named {containerName}");
            }
        }

        private bool TryGetContainer(string containerName, out EntityContainer entityContainer)
        {
            if (containerName is null)
            {
                entityContainer = null;
                return false;
            }
            
            if (m_EntityContainerDic.TryGetValue(containerName, out entityContainer))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsEntityValid(string id)
        {
            return m_EntityDic.ContainsKey(id);
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
        public Entity GetEntity(string entityId)
        {
            if (m_EntityDic.TryGetValue(entityId, out Entity entity))
            {
                return entity;
            }
            else
            {
                throw new XFrameworkException($"[Entity] There is no entity with an id of {entityId}");
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
            return GetEntityInfo(entity.Id);
        }

        private EntityInfo GetEntityInfo(string entityId)
        {
            if (m_EntityInfoDic.TryGetValue(entityId, out EntityInfo entityInfo))
            {
                return entityInfo;
            }
            else
            {
                EntityInfo info = new EntityInfo(GetEntity(entityId));
                m_EntityInfoDic.Add(entityId, info);
                return info;
            }
        }
        
        /// <summary>
        /// 获取一个实体的子实体
        /// </summary>
        /// <param name="entityId">父实体编号</param>
        /// <param name="index">子实体索引</param>
        /// <returns>子实体</returns>
        public Entity GetChildEntity(string entityId, int index)
        {
            Entity entity = GetEntity(entityId);
            return GetChildEntity(entity, index);
        }

        /// <summary>
        /// 获取一个实体的子实体
        /// </summary>
        /// <param name="entity">父实体</param>
        /// <param name="index">子实体索引</param>
        /// <returns>子实体</returns>
        public Entity GetChildEntity(Entity entity, int index)
        {
            return GetEntityInfo(entity)[index];
        }

        /// <summary>
        /// 获取一个实体的父实体
        /// </summary>
        /// <param name="entityId">子实体编号</param>
        /// <returns>父实体</returns>
        public Entity GetParentEntity(string entityId)
        {
            return GetEntityInfo(entityId).Parent;
        }

        /// <summary>
        /// 获取一个实体的父实体
        /// </summary>
        /// <param name="entity">子实体</param>
        /// <returns>父实体</returns>
        public Entity GetParentEntity(Entity entity)
        {
            return GetParentEntity(entity.Id);
        }

        #endregion

        #region 池的清理

        /// <summary>
        /// 清理实体池
        /// </summary>
        /// <param name="count">容器实体池的最大保留量</param>
        public void Clean(int count = 0)
        {
            foreach (var item in m_EntityContainerDic.Values)
            {
                item.Clean(count);
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
                container.Clean(count);
            }
            else
            {
                throw new XFrameworkException("[EntityContainer] null container");
            }
        }

        /// <summary>
        /// 移除一个模板
        /// </summary>
        /// <param name="key">key</param>
        public void RecycleAllTemplate(string key)
        {
            var container = GetContainer(key);
            container.Clean(0);
            var entities = container.GetEntities();
            if (entities != null)
            {
                foreach (var item in entities)
                {
                    m_EntityDic.Remove(item.Id);
                    m_EntityInfoDic.Remove(item.Id);
                    GameObject.Destroy(item.gameObject);
                }
                m_EntityContainerDic.Remove(key);
            }
        }

        #endregion

        #region 接口实现

        public override int Priority => 10000;

        public override void Shutdown()
        {
            m_EntityContainerDic.Clear();
            m_EntityDic.Clear();
            m_EntityInfoDic.Clear();
        }

        public override void Update()
        {
            foreach (var item in m_EntityContainerDic.Values)
            {
                item.OnUpdate();
            }
        }

        #endregion
    }
}