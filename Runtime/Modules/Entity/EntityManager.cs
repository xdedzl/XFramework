using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Entity
{
    /// <summary>
    /// 实体管理器
    /// </summary>
    [ModuleLifecycle(ModuleLifecycle.RuntimePersistent)]
    public partial class EntityManager : MonoGameModuleBase<EntityManager>
    {
        private readonly EntityViewManager m_EntityViewManager = new();
        private readonly Dictionary<string, LogicEntity> m_LogicEntityDic = new();
        private readonly Dictionary<string, LogicEntity> m_EntityAliasDic = new();
        private readonly List<LogicEntity> m_LogicUpdateBuffer = new();

        #region 增删改

        public bool AddAllocator(IEntityViewAllocator allocator)
        {
            return m_EntityViewManager.AddAllocator(allocator);
        }

        public void AddTemplate<T>(GameObject template) where T : Entity
        {
            string key = typeof(T).Name;
            AddAllocator(new GameObjectEntityViewAllocator(key, typeof(T), template, key));
        }

        public void AddTemplate<T>(string key, GameObject template) where T : Entity
        {
            AddAllocator(new GameObjectEntityViewAllocator(key, typeof(T), template, key));
        }

        public void AddTemplate<T>(string key, GameObject template, string entityRootName) where T : Entity
        {
            AddAllocator(new GameObjectEntityViewAllocator(key, typeof(T), template, entityRootName));
        }

        public void AddTemplate(string key, Type type, GameObject template)
        {
            AddAllocator(new GameObjectEntityViewAllocator(key, type, template, key));
        }

        public void AddTemplate(string key, Type type, GameObject template, string entityRootName)
        {
            AddAllocator(new GameObjectEntityViewAllocator(key, type, template, entityRootName));
        }

        public void AddTemplate<T>(string prefabPath) where T : Entity
        {
            AddAllocator(new ResourceEntityViewAllocator(prefabPath, typeof(T)));
        }

        public void RemoveTemplate(string key)
        {
            if (key is null || !m_EntityViewManager.ContainsTemplate(key))
            {
                return;
            }

            Entity[] entities = m_EntityViewManager.GetEntities(key);
            foreach (Entity entity in entities)
            {
                Recycle(entity);
            }

            m_EntityViewManager.RemoveTemplate(key);
        }

        public bool ContainsTemplate(string key)
        {
            return m_EntityViewManager.ContainsTemplate(key);
        }

        #region Allocate

        public T Allocate<T>(Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return Allocate<T>(entityData: null, pos, quaternion, parent);
        }

        public T Allocate<T>(IEntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return AllocateView(typeof(T), typeof(T).Name, null, entityData, pos, quaternion, parent) as T;
        }

        public T Allocate<T>(string key, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return Allocate<T>(key, null, null, pos, quaternion, parent);
        }

        public T Allocate<T>(string key, string alias, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return AllocateView(typeof(T), key, alias, null, pos, quaternion, parent) as T;
        }

        public T Allocate<T>(string key, IEntityData data, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return AllocateView(typeof(T), key, null, data, pos, quaternion, parent) as T;
        }

        public T Allocate<T>(string key, string alias, IEntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return AllocateView(typeof(T), key, alias, entityData, pos, quaternion, parent) as T;
        }

        public Entity Allocate(string key, string alias, IEntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null)
        {
            return AllocateView(null, key, alias, entityData, pos, quaternion, parent);
        }

        private Entity AllocateView(Type entityType, string key, string alias, IEntityData entityData, Vector3 pos, Quaternion quaternion, Transform parent)
        {
            LogicEntity logic = AllocateLogic(entityType, key, alias, entityData, out Type resolvedEntityType);
            try
            {
                return AllocateAndBindView(logic, key, resolvedEntityType, pos, quaternion, parent);
            }
            catch
            {
                Recycle(logic);
                throw;
            }
        }

        #endregion

        #region Allocate With Prefab

        public T AllocateWithPrefab<T>(string prefabPath, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return AllocateWithPrefab<T>(prefabPath, null, null, pos, quaternion, parent);
        }

        public T AllocateWithPrefab<T>(string prefabPath, IEntityData data, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return AllocateWithPrefab<T>(prefabPath, null, data, pos, quaternion, parent);
        }

        private T AllocateWithPrefab<T>(string prefabPath, string alias, IEntityData data, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null) where T : Entity
        {
            return AllocateWithPrefab(prefabPath, typeof(T), alias, data, pos, quaternion, parent) as T;
        }

        public Entity AllocateWithPrefab(string prefabPath, Type entityType, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null)
        {
            return AllocateWithPrefab(prefabPath, entityType, null, pos, quaternion, parent);
        }

        public Entity AllocateWithPrefab(string prefabPath, Type entityType, IEntityData data, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null)
        {
            return AllocateWithPrefab(prefabPath, entityType, null, data, pos, quaternion, parent);
        }

        private Entity AllocateWithPrefab(string prefabPath, Type entityType, string alias, IEntityData entityData, Vector3 pos = default, Quaternion quaternion = default, Transform parent = null)
        {
            LogicEntity logic = AllocateLogicWithPrefab(prefabPath, entityType, alias, entityData, out Type resolvedEntityType);
            try
            {
                return AllocateAndBindView(logic, prefabPath, resolvedEntityType, pos, quaternion, parent);
            }
            catch
            {
                Recycle(logic);
                throw;
            }
        }

        #endregion

        private Entity AllocateAndBindView(LogicEntity logic, string key, Type entityType, Vector3 pos, Quaternion quaternion, Transform parent)
        {
            Entity view = m_EntityViewManager.Allocate(key, entityType, pos, quaternion, parent);

            try
            {
                BindView(logic, view);
                view.gameObject.SetActive(true);
                view.OnAllocate(logic.Data);
                return view;
            }
            catch
            {
                view.gameObject.SetActive(false);
                m_EntityViewManager.Recycle(view);
                if (view.Logic == logic)
                {
                    UnbindView(logic, view);
                }

                throw;
            }
        }

        public bool Recycle(Entity entity)
        {
            return entity != null && Recycle(entity.Logic);
        }

        public bool Recycle(LogicEntity logic)
        {
            if (!IsRegisteredLogic(logic))
            {
                return false;
            }

            if (logic.View != null)
            {
                Entity view = logic.View;
                view.OnRecycle();
                view.gameObject.SetActive(false);
                m_EntityViewManager.Recycle(view);
                UnbindView(logic, view);
            }

            UnregisterLogic(logic);
            logic.OnDestroy();
            return true;
        }

        public bool Recycle(string id)
        {
            return !string.IsNullOrEmpty(id) && m_LogicEntityDic.TryGetValue(id, out LogicEntity logic) && Recycle(logic);
        }

        public void RecycleContainer<T>() where T : Entity
        {
            RecycleContainer(typeof(T).Name);
        }

        public void RecycleContainer(string containerName)
        {
            if (containerName is null || !m_EntityViewManager.ContainsTemplate(containerName))
            {
                return;
            }

            foreach (Entity entity in m_EntityViewManager.GetEntities(containerName))
            {
                Recycle(entity);
            }
        }

        public void RecycleAll()
        {
            foreach (LogicEntity logic in m_LogicEntityDic.Values.ToList())
            {
                Recycle(logic);
            }
        }

        #endregion

        #region 查

        public bool IsEntityValid(string id)
        {
            return !string.IsNullOrEmpty(id) && m_LogicEntityDic.ContainsKey(id);
        }

        public bool IsEntityAliasValid(string alias)
        {
            return !string.IsNullOrEmpty(alias) && m_EntityAliasDic.ContainsKey(alias);
        }

        public Entity GetEntity(GameObject gameObject)
        {
            Entity entity = null;
            if (gameObject != null)
            {
                entity = gameObject.GetComponent<Entity>();
                if (entity == null)
                {
                    Debug.LogWarning($"{gameObject.name}不是由实体管理器创建的");
                }
            }

            return entity;
        }

        public Entity GetEntity(string entityId)
        {
            return GetLogicEntity(entityId).View;
        }

        public LogicEntity GetLogicEntity(string entityId)
        {
            if (!string.IsNullOrEmpty(entityId) && m_LogicEntityDic.TryGetValue(entityId, out LogicEntity logic))
            {
                return logic;
            }

            throw new XFrameworkException($"[Entity] There is no logic entity with an id of {entityId}");
        }

        public TLogic GetLogicEntity<TLogic>(string entityId) where TLogic : LogicEntity
        {
            LogicEntity logic = GetLogicEntity(entityId);
            if (logic is TLogic typedLogic)
            {
                return typedLogic;
            }

            throw new XFrameworkException($"[Entity] logic entity type mismatch. id:{entityId}, expectedType:{typeof(TLogic).FullName}, actualType:{logic.GetType().FullName}");
        }

        public bool TryGetLogicEntity(string entityId, out LogicEntity logic)
        {
            logic = null;
            return !string.IsNullOrEmpty(entityId) && m_LogicEntityDic.TryGetValue(entityId, out logic);
        }

        public Entity GetEntityByAlias(string alias)
        {
            if (TryGetEntityByAlias(alias, out Entity entity))
            {
                return entity;
            }

            throw new XFrameworkException($"[Entity] There is no entity with an alias of {alias}");
        }

        public T GetEntityByAlias<T>(string alias) where T : Entity
        {
            return GetEntityByAlias(alias) as T;
        }

        public LogicEntity GetLogicEntityByAlias(string alias)
        {
            if (TryGetLogicEntityByAlias(alias, out LogicEntity logic))
            {
                return logic;
            }

            throw new XFrameworkException($"[Entity] There is no logic entity with an alias of {alias}");
        }

        public bool TryGetLogicEntityByAlias(string alias, out LogicEntity logic)
        {
            logic = null;
            return !string.IsNullOrEmpty(alias) && m_EntityAliasDic.TryGetValue(alias, out logic);
        }

        public bool TryGetEntityByAlias(string alias, out Entity entity)
        {
            if (string.IsNullOrEmpty(alias))
            {
                entity = null;
                return false;
            }

            if (m_EntityAliasDic.TryGetValue(alias, out LogicEntity logic))
            {
                entity = logic.View;
                return entity != null;
            }

            entity = null;
            return false;
        }

        public Entity[] GetEntities(string entityName)
        {
            return m_EntityViewManager.GetEntities(entityName);
        }

        #endregion

        #region 池的清理

        public void Clean(int count = 0)
        {
            m_EntityViewManager.Clean(count);
        }

        public void Clean(string containerName, int count)
        {
            m_EntityViewManager.Clean(containerName, count);
        }

        #endregion

        #region 接口实现

        public override int Priority => 10000;

        public override void Shutdown()
        {
            RecycleAll();
            m_EntityViewManager.Shutdown();
            m_LogicEntityDic.Clear();
            m_EntityAliasDic.Clear();
        }

        public override void Update()
        {
            m_LogicUpdateBuffer.Clear();
            m_LogicUpdateBuffer.AddRange(m_LogicEntityDic.Values);
            foreach (LogicEntity logic in m_LogicUpdateBuffer)
            {
                if (m_LogicEntityDic.ContainsValue(logic))
                {
                    logic.OnUpdate();
                }
            }

            m_EntityViewManager.Update();
        }

        #endregion

        #region Logic 注册与 View 绑定

        private LogicEntity AllocateLogic(Type entityType, string key, string alias, IEntityData entityData, out Type resolvedEntityType)
        {
            if (m_EntityViewManager.ContainsTemplate(key))
            {
                resolvedEntityType = m_EntityViewManager.ResolveEntityType(key, entityType);
            }
            else
            {
                resolvedEntityType = entityType ?? typeof(CommonEntity);
                EntityViewAllocatorUtility.Validate(key, resolvedEntityType);
                var template = new GameObject(key + "template");
                AddTemplate(key, resolvedEntityType, template);
            }

            return CreateLogic(GetLogicType(resolvedEntityType), key, alias, entityData);
        }

        private LogicEntity AllocateLogicWithPrefab(string prefabPath, Type entityType, string alias, IEntityData entityData, out Type resolvedEntityType)
        {
            resolvedEntityType = m_EntityViewManager.ResolvePrefabEntityType(prefabPath, entityType);
            if (!m_EntityViewManager.ContainsTemplate(prefabPath))
            {
                AddAllocator(new ResourceEntityViewAllocator(prefabPath, resolvedEntityType));
            }

            return CreateLogic(GetLogicType(resolvedEntityType), prefabPath, alias, entityData);
        }

        private LogicEntity CreateLogic(Type logicType, string containerName, string alias, IEntityData entityData)
        {
            LogicEntity logic = Activator.CreateInstance(logicType) as LogicEntity;
            logic.Id = Guid.NewGuid().ToString();
            logic.Data = entityData;
            logic.ContainerName = containerName;
            RegisterLogic(logic, alias);
            try
            {
                logic.OnCreate();
                return logic;
            }
            catch
            {
                UnregisterLogic(logic);
                throw;
            }
        }

        private bool IsRegisteredLogic(LogicEntity logic)
        {
            return logic != null
                && !string.IsNullOrEmpty(logic.Id)
                && m_LogicEntityDic.TryGetValue(logic.Id, out LogicEntity registeredLogic)
                && registeredLogic == logic;
        }

        private static Type GetLogicType(Type entityType)
        {
            Type currentType = entityType;
            while (currentType != null && currentType != typeof(Entity))
            {
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(Entity<>))
                {
                    return currentType.GetGenericArguments()[0];
                }

                currentType = currentType.BaseType;
            }

            return typeof(LogicEntity);
        }

        private void RegisterLogic(LogicEntity logic, string alias)
        {
            if (m_LogicEntityDic.TryGetValue(logic.Id, out LogicEntity existLogic))
            {
                throw new XFrameworkException($"[EntityError] id is already occupied.  Entity {existLogic.View}");
            }

            if (!string.IsNullOrEmpty(alias) && m_EntityAliasDic.TryGetValue(alias, out existLogic))
            {
                throw new XFrameworkException($"[EntityError] alias is already occupied. Alias {alias}, Entity {existLogic.View}");
            }

            logic.Alias = string.IsNullOrEmpty(alias) ? null : alias;
            m_LogicEntityDic.Add(logic.Id, logic);
            if (logic.Alias != null)
            {
                m_EntityAliasDic.Add(logic.Alias, logic);
            }
        }

        private void UnregisterLogic(LogicEntity logic)
        {
            m_LogicEntityDic.Remove(logic.Id);
            if (!string.IsNullOrEmpty(logic.Alias))
            {
                m_EntityAliasDic.Remove(logic.Alias);
            }

            logic.Alias = null;
        }

        private void BindView(LogicEntity logic, Entity view)
        {
            logic.View = view;
            view.Logic = logic;
        }

        private void UnbindView(LogicEntity logic, Entity view)
        {
            logic.View = null;
            view.Logic = null;
        }

        #endregion
    }
}
