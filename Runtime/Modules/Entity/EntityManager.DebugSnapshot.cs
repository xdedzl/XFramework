using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Entity
{
    /// <summary>
    /// EntityManager runtime snapshot for editor debugging.
    /// </summary>
    public readonly struct EntityManagerDebugSnapshot
    {
        public EntityManagerDebugSnapshot(
            IReadOnlyList<EntityContainerDebugSnapshot> containers,
            IReadOnlyList<EntityDebugSnapshot> entities,
            int aliasCount)
        {
            Containers = containers ?? Array.Empty<EntityContainerDebugSnapshot>();
            Entities = entities ?? Array.Empty<EntityDebugSnapshot>();
            AliasCount = aliasCount;
        }

        public IReadOnlyList<EntityContainerDebugSnapshot> Containers { get; }
        public IReadOnlyList<EntityDebugSnapshot> Entities { get; }
        public int AliasCount { get; }
    }

    /// <summary>
    /// Runtime container state excluding recycled entities.
    /// </summary>
    public readonly struct EntityContainerDebugSnapshot
    {
        public EntityContainerDebugSnapshot(string name, Type entityType, GameObject template, int activeEntityCount)
        {
            Name = name ?? string.Empty;
            EntityType = entityType;
            Template = template;
            ActiveEntityCount = activeEntityCount;
        }

        public string Name { get; }
        public Type EntityType { get; }
        public GameObject Template { get; }
        public int ActiveEntityCount { get; }
    }

    /// <summary>
    /// Runtime entity state while the entity is still registered in EntityManager.
    /// </summary>
    public readonly struct EntityDebugSnapshot
    {
        public EntityDebugSnapshot(Entity entity, Entity parent, IReadOnlyList<Entity> children)
        {
            Entity = entity;
            GameObject = entity != null ? entity.gameObject : null;
            Id = entity != null ? entity.Id : string.Empty;
            ContainerName = entity != null ? entity.ContainerName : string.Empty;
            Alias = entity != null ? entity.Alias : string.Empty;
            EntityType = entity != null ? entity.GetType() : null;
            Name = entity != null ? entity.name : string.Empty;
            ActiveSelf = GameObject != null && GameObject.activeSelf;
            ActiveInHierarchy = GameObject != null && GameObject.activeInHierarchy;
            SceneName = GameObject != null && GameObject.scene.IsValid() ? GameObject.scene.name : "<No Scene>";
            Parent = parent;
            Children = children ?? Array.Empty<Entity>();
            ChildCount = Children.Count;
        }

        public Entity Entity { get; }
        public GameObject GameObject { get; }
        public string Id { get; }
        public string ContainerName { get; }
        public string Alias { get; }
        public Type EntityType { get; }
        public string Name { get; }
        public bool ActiveSelf { get; }
        public bool ActiveInHierarchy { get; }
        public string SceneName { get; }
        public Entity Parent { get; }
        public IReadOnlyList<Entity> Children { get; }
        public int ChildCount { get; }
    }

    public partial class EntityManager
    {
        /// <summary>
        /// Gets a read-only editor debug snapshot of currently valid entities.
        /// </summary>
        public EntityManagerDebugSnapshot GetDebugSnapshot()
        {
            var containers = new List<EntityContainerDebugSnapshot>(m_EntityContainerDic.Count);
            foreach (KeyValuePair<string, EntityContainer> pair in m_EntityContainerDic)
            {
                EntityContainer container = pair.Value;
                containers.Add(new EntityContainerDebugSnapshot(
                    pair.Key,
                    container?.EntityType,
                    container?.Template,
                    container?.Count ?? 0));
            }

            containers.Sort(CompareContainerSnapshots);

            var entities = new List<EntityDebugSnapshot>(m_EntityDic.Count);
            foreach (Entity entity in m_EntityDic.Values)
            {
                if (!IsDebugEntityValid(entity))
                {
                    continue;
                }

                m_EntityInfoDic.TryGetValue(entity.Id, out EntityInfo info);
                Entity parent = IsDebugEntityValid(info?.Parent) ? info.Parent : null;
                IReadOnlyList<Entity> children = GetDebugChildren(info);
                entities.Add(new EntityDebugSnapshot(entity, parent, children));
            }

            entities.Sort(CompareEntitySnapshots);
            return new EntityManagerDebugSnapshot(containers, entities, m_EntityAliasDic.Count);
        }

        private IReadOnlyList<Entity> GetDebugChildren(EntityInfo info)
        {
            if (info == null)
            {
                return Array.Empty<Entity>();
            }

            Entity[] children = info.GetChilds();
            if (children.Length == 0)
            {
                return Array.Empty<Entity>();
            }

            var validChildren = new List<Entity>(children.Length);
            for (int i = 0; i < children.Length; i++)
            {
                Entity child = children[i];
                if (IsDebugEntityValid(child))
                {
                    validChildren.Add(child);
                }
            }

            return validChildren;
        }

        private bool IsDebugEntityValid(Entity entity)
        {
            return entity != null
                && !string.IsNullOrEmpty(entity.Id)
                && m_EntityDic.TryGetValue(entity.Id, out Entity registeredEntity)
                && registeredEntity == entity;
        }

        private static int CompareContainerSnapshots(EntityContainerDebugSnapshot left, EntityContainerDebugSnapshot right)
        {
            return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }

        private static int CompareEntitySnapshots(EntityDebugSnapshot left, EntityDebugSnapshot right)
        {
            int containerResult = string.Compare(left.ContainerName, right.ContainerName, StringComparison.Ordinal);
            if (containerResult != 0)
            {
                return containerResult;
            }

            int nameResult = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            return nameResult != 0
                ? nameResult
                : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }
    }
}
