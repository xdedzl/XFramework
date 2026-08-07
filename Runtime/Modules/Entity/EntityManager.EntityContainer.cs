using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.Entity
{
    internal static class EntityViewAllocatorUtility
    {
        internal static void Validate(string key, Type entityType)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new XFrameworkException("[EntityError] allocator key is null");
            }

            if (entityType == null)
            {
                throw new XFrameworkException($"[EntityError] entity type is null. key:{key}");
            }

            if (!typeof(Entity).IsAssignableFrom(entityType) || entityType.IsAbstract)
            {
                throw new XFrameworkException($"[EntityError] entity type must be a non-abstract Entity subtype. key:{key}, type:{entityType.FullName}");
            }
        }

        internal static Entity GetOrAddEntity(GameObject gameObject, string key, Type entityType)
        {
            Entity[] entities = gameObject.GetComponents<Entity>();
            if (entities.Length == 0)
            {
                Entity addedEntity = gameObject.AddComponent(entityType) as Entity;
                if (addedEntity == null)
                {
                    throw new XFrameworkException($"[EntityError] failed to add entity component. key:{key}, expectedType:{entityType.FullName}, gameObject:{gameObject.name}");
                }

                return addedEntity;
            }

            if (entities.Length == 1 && entities[0].GetType() == entityType)
            {
                return entities[0];
            }

            string actualTypes = string.Join(", ", entities.Select(entity => entity.GetType().FullName));
            throw new XFrameworkException($"[EntityError] entity component type mismatch. key:{key}, expectedType:{entityType.FullName}, actualTypes:{actualTypes}, gameObject:{gameObject.name}");
        }

        internal static Type GetExistingEntityType(GameObject gameObject, string key)
        {
            Entity[] entities = gameObject.GetComponents<Entity>();
            if (entities.Length == 1)
            {
                return entities[0].GetType();
            }

            string actualTypes = entities.Length == 0
                ? "<none>"
                : string.Join(", ", entities.Select(entity => entity.GetType().FullName));
            throw new XFrameworkException($"[EntityError] prefab root must contain exactly one Entity component when entity type is not specified. key:{key}, actualTypes:{actualTypes}, gameObject:{gameObject.name}");
        }
    }

    public interface IEntityViewAllocator
    {
        string Key { get; }
        Type EntityType { get; }
        int Count { get; }
        Entity Allocate(Vector3 position, Quaternion rotation, Transform parent);
        bool Recycle(Entity entity);
        Entity[] GetEntities();
        void OnUpdate();
        void ReleaseAll();
    }

    public sealed class GameObjectEntityViewAllocator : IEntityViewAllocator
    {
        private readonly string m_Key;
        private readonly Type m_EntityType;
        private readonly HashSet<Entity> m_Entities = new();
        private readonly List<Entity> m_UpdateBuffer = new();
        private readonly GameObject m_Template;
        private readonly Stack<Entity> m_Pool = new();
        private readonly Transform m_EntityRoot;
        private readonly bool m_OwnsEntityRoot;

        public GameObjectEntityViewAllocator(string key, Type entityType, GameObject template, string entityRootName) : this(key, entityType, template, entityRootName, false) { }
        
        internal GameObjectEntityViewAllocator(string key, Type entityType, GameObject template, string entityRootName, bool ownsTemplate)
        {
            EntityViewAllocatorUtility.Validate(key, entityType);
            m_Key = key;
            m_EntityType = entityType;
            m_Template = template;
            if (!string.IsNullOrEmpty(entityRootName))
            {
                GameObject obj = GameObject.Find(entityRootName);
                if (obj)
                {
                    m_EntityRoot = obj.transform;
                }
                else
                {
                    m_EntityRoot = new GameObject(entityRootName).transform;
                    m_OwnsEntityRoot = true;
                    GameObject.DontDestroyOnLoad(m_EntityRoot.gameObject);
                }
            }
        }

        public string Key => m_Key;
        public Type EntityType => m_EntityType;
        public int Count => m_Entities.Count;
        internal GameObject Template => m_Template;

        public Entity Allocate(Vector3 position, Quaternion rotation, Transform parent)
        {
            Entity entity;
            if (m_Pool.Count > 0)
            {
                entity = m_Pool.Pop();
                entity.transform.position = position;
                entity.transform.rotation = rotation;
            }
            else
            {
                entity = InstantiateFromTemplate(position, rotation, parent);
            }

            m_Entities.Add(entity);
            return entity;
        }

        public bool Recycle(Entity entity)
        {
            if (!m_Entities.Contains(entity))
            {
                return false;
            }

            m_Entities.Remove(entity);
            if (m_EntityRoot != null && m_EntityRoot != entity.transform.parent)
            {
                entity.transform.parent = m_EntityRoot;
            }

            m_Pool.Push(entity);
            return true;
        }

        public Entity[] GetEntities()
        {
            return m_Entities.ToArray();
        }

        public void OnUpdate()
        {
            m_UpdateBuffer.Clear();
            m_UpdateBuffer.AddRange(m_Entities);
            foreach (Entity entity in m_UpdateBuffer)
            {
                if (m_Entities.Contains(entity))
                {
                    entity.OnUpdate();
                }
            }
        }

        public void ReleaseAll()
        {
            foreach (Entity entity in m_Entities.ToArray())
            {
                Recycle(entity);
            }

            Clean(0);
            if (m_OwnsEntityRoot)
            {
                GameObject.Destroy(m_EntityRoot.gameObject);
            }
        }

        internal void Clean(int count)
        {
            while (count < m_Pool.Count)
            {
                Entity entity = m_Pool.Pop();
                entity.OnRelease();
                GameObject.Destroy(entity.gameObject);
            }
        }

        private Entity InstantiateFromTemplate(Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject gameObject = GameObject.Instantiate(m_Template, position, rotation, parent);
            try
            {
                if (m_EntityRoot)
                {
                    gameObject.transform.SetParent(m_EntityRoot);
                }

                return InitializeEntity(gameObject);
            }
            catch
            {
                GameObject.Destroy(gameObject);
                throw;
            }
        }

        private Entity InitializeEntity(GameObject gameObject)
        {
            Entity entity = EntityViewAllocatorUtility.GetOrAddEntity(gameObject, m_Key, m_EntityType);
            entity.name = m_Key;
            entity.OnInit();
            return entity;
        }

        private static GameObject LoadTemplate(string key, Type entityType, string prefabPath)
        {
            EntityViewAllocatorUtility.Validate(key, entityType);
            GameObject template = ResourceManager.Instance.Load<GameObject>(prefabPath);
            if (template == null)
            {
                throw new XFrameworkException($"[EntityError] prefab path is not found. prefabPath:{prefabPath}");
            }

            return template;
        }
    }

    public sealed class ResourceEntityViewAllocator : IEntityViewAllocator
    {
        private readonly string m_Key;
        private readonly Type m_EntityType;
        private readonly HashSet<Entity> m_Entities = new();
        private readonly List<Entity> m_UpdateBuffer = new();
        private readonly string m_PrefabPath;

        public ResourceEntityViewAllocator(string prefabPath, Type entityType) : this(prefabPath, prefabPath, entityType) { }

        public ResourceEntityViewAllocator(string key, string prefabPath, Type entityType)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new XFrameworkException("[EntityError] prefab path is null");
            }

            EntityViewAllocatorUtility.Validate(key, entityType);
            m_Key = key;
            m_PrefabPath = prefabPath;
            m_EntityType = entityType;
        }

        public string Key => m_Key;
        public Type EntityType => m_EntityType;
        public int Count => m_Entities.Count;

        public Entity Allocate(Vector3 position, Quaternion rotation, Transform parent)
        {
            Entity entity = InstantiateFromResource(position, rotation, parent);
            m_Entities.Add(entity);
            return entity;
        }

        public bool Recycle(Entity entity)
        {
            if (!m_Entities.Remove(entity))
            {
                return false;  
            }

            entity.OnRelease();
            ReleaseResourceEntity(entity);
            return true;
        }

        public Entity[] GetEntities()
        {
            return m_Entities.ToArray();
        }

        public void OnUpdate()
        {
            m_UpdateBuffer.Clear();
            m_UpdateBuffer.AddRange(m_Entities);
            foreach (Entity entity in m_UpdateBuffer)
            {
                if (m_Entities.Contains(entity))
                {
                    entity.OnUpdate();
                }
            }
        }

        public void ReleaseAll()
        {
            foreach (Entity entity in m_Entities.ToArray())
            {
                Recycle(entity);
            }
        }

        private Entity InstantiateFromResource(Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject gameObject = ResourceManager.Instance.InstantiateByPool<GameObject>(m_PrefabPath, position, rotation, parent);
            if (gameObject == null)
            {
                throw new XFrameworkException($"[EntityError] prefab instantiate failed. prefabPath:{m_PrefabPath}");
            }

            try
            {
                Entity entity = EntityViewAllocatorUtility.GetOrAddEntity(gameObject, m_Key, m_EntityType);
                entity.name = m_Key;
                entity.OnInit();
                return entity;
            }
            catch
            {
                ResourceManager.Instance.Release(gameObject);
                throw;
            }
        }

        private static void ReleaseResourceEntity(Entity entity)
        {
            ResourceManager.Instance.Release(entity.gameObject);
        }
    }

    internal sealed class EntityViewManager
    {
        private readonly Dictionary<string, IEntityViewAllocator> m_EntityViewAllocatorDic = new();

        internal bool AddAllocator(IEntityViewAllocator allocator)
        {
            if (allocator == null)
            {
                throw new ArgumentNullException(nameof(allocator));
            }

            EntityViewAllocatorUtility.Validate(allocator.Key, allocator.EntityType);

            if (m_EntityViewAllocatorDic.ContainsKey(allocator.Key))
            {
                Debug.LogWarning("请勿重复添加");
                allocator.ReleaseAll();
                return false;
            }

            m_EntityViewAllocatorDic.Add(allocator.Key, allocator);
            return true;
        }

        internal bool ContainsTemplate(string key)
        {
            return m_EntityViewAllocatorDic.ContainsKey(key);
        }

        internal Entity[] GetEntities(string key)
        {
            return GetAllocator(key).GetEntities();
        }

        internal Type ResolveEntityType(string key, Type entityType)
        {
            IEntityViewAllocator allocator = GetAllocator(key);
            Type resolvedType = entityType ?? allocator.EntityType;
            ValidateTemplateEntityType(key, resolvedType, allocator);
            return resolvedType;
        }

        internal Entity Allocate(string key, Type entityType, Vector3 position, Quaternion rotation, Transform parent)
        {
            IEntityViewAllocator allocator = GetAllocator(key);
            ValidateTemplateEntityType(key, entityType, allocator);
            Entity entity = allocator.Allocate(position, rotation, parent);
            if (entity == null)
            {
                throw new XFrameworkException($"[EntityError] allocator returned null. key:{key}, expectedType:{entityType.FullName}");
            }

            try
            {
                Entity resolvedEntity = EntityViewAllocatorUtility.GetOrAddEntity(entity.gameObject, key, entityType);
                if (resolvedEntity != entity)
                {
                    throw new XFrameworkException($"[EntityError] allocator returned an unexpected Entity component. key:{key}, expectedType:{entityType.FullName}, actualType:{entity.GetType().FullName}, gameObject:{entity.gameObject.name}");
                }

                return entity;
            }
            catch
            {
                allocator.Recycle(entity);
                throw;
            }
        }

        internal Type ResolvePrefabEntityType(string prefabPath, Type entityType)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                throw new XFrameworkException("[EntityError] prefab path is null");
            }

            if (TryGetAllocator(prefabPath, out IEntityViewAllocator allocator))
            {
                Type resolvedType = entityType ?? allocator.EntityType;
                ValidateTemplateEntityType(prefabPath, resolvedType, allocator);
                return resolvedType;
            }

            if (entityType != null)
            {
                EntityViewAllocatorUtility.Validate(prefabPath, entityType);
                return entityType;
            }

            GameObject prefab = ResourceManager.Instance.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new XFrameworkException($"[EntityError] prefab path is not found. prefabPath:{prefabPath}");
            }

            try
            {
                return EntityViewAllocatorUtility.GetExistingEntityType(prefab, prefabPath);
            }
            finally
            {
                ResourceManager.Instance.Release(prefab);
            }
        }

        internal bool Recycle(Entity entity)
        {
            return GetAllocator(entity.ContainerName).Recycle(entity);
        }

        internal void RemoveTemplate(string key)
        {
            if (!TryGetAllocator(key, out IEntityViewAllocator allocator))
            {
                return;
            }

            allocator.ReleaseAll();
            m_EntityViewAllocatorDic.Remove(key);
        }

        internal void Clean(int count)
        {
            foreach (IEntityViewAllocator allocator in m_EntityViewAllocatorDic.Values)
            {
                if (allocator is GameObjectEntityViewAllocator pooledAllocator)
                {
                    pooledAllocator.Clean(count);
                }
            }
        }

        internal void Clean(string key, int count)
        {
            if (GetAllocator(key) is GameObjectEntityViewAllocator goAllocator)
            {
                goAllocator.Clean(count);
            }
        }

        internal void Update()
        {
            foreach (IEntityViewAllocator allocator in m_EntityViewAllocatorDic.Values)
            {
                allocator.OnUpdate();
            }
        }

        internal void Shutdown()
        {
            foreach (IEntityViewAllocator allocator in m_EntityViewAllocatorDic.Values)
            {
                allocator.ReleaseAll();
            }

            m_EntityViewAllocatorDic.Clear();
        }

        internal EntityContainerDebugSnapshot[] GetDebugSnapshots()
        {
            var snapshots = new EntityContainerDebugSnapshot[m_EntityViewAllocatorDic.Count];
            int index = 0;
            foreach (KeyValuePair<string, IEntityViewAllocator> pair in m_EntityViewAllocatorDic)
            {
                IEntityViewAllocator allocator = pair.Value;
                GameObject template = allocator is GameObjectEntityViewAllocator pooledAllocator ? pooledAllocator.Template : null;
                snapshots[index++] = new EntityContainerDebugSnapshot(pair.Key, allocator.EntityType, template, allocator.Count);
            }

            return snapshots;
        }

        private IEntityViewAllocator GetAllocator(string key)
        {
            if (TryGetAllocator(key, out IEntityViewAllocator allocator))
            {
                return allocator;
            }

            throw new XFrameworkException($"[EntityError] There is no entity allocator named {key}");
        }

        private bool TryGetAllocator(string key, out IEntityViewAllocator allocator)
        {
            if (key is null)
            {
                allocator = null;
                return false;
            }

            return m_EntityViewAllocatorDic.TryGetValue(key, out allocator);
        }

        private static void ValidateTemplateEntityType(string key, Type requestType, IEntityViewAllocator allocator)
        {
            EntityViewAllocatorUtility.Validate(key, requestType);
            if (allocator.EntityType != requestType)
            {
                throw new XFrameworkException($"[EntityError] template {key} is already registered with entity type {allocator.EntityType.FullName}, but requested {requestType.FullName}");
            }
        }

    }
}
