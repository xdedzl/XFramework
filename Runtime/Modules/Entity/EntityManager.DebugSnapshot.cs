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
        public EntityManagerDebugSnapshot(IReadOnlyList<EntityContainerDebugSnapshot> containers, IReadOnlyList<EntityDebugSnapshot> entities, int aliasCount)
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
        public EntityDebugSnapshot(Entity entity) : this(entity != null ? entity.Logic : null) { }

        public EntityDebugSnapshot(LogicEntity logic)
        {
            Logic = logic;
            Entity = logic?.View;
            GameObject = Entity != null ? Entity.gameObject : null;
            Id = logic?.Id ?? string.Empty;
            ContainerName = logic?.ContainerName ?? string.Empty;
            Alias = logic?.Alias ?? string.Empty;
            LogicType = logic?.GetType();
            DataType = logic?.Data?.GetType();
            EntityType = Entity != null ? Entity.GetType() : null;
            Name = Entity != null ? Entity.name : string.Empty;
            ActiveSelf = GameObject != null && GameObject.activeSelf;
            ActiveInHierarchy = GameObject != null && GameObject.activeInHierarchy;
            SceneName = GameObject != null && GameObject.scene.IsValid() ? GameObject.scene.name : string.Empty;
        }

        public LogicEntity Logic { get; }
        public Entity Entity { get; }
        public GameObject GameObject { get; }
        public string Id { get; }
        public string ContainerName { get; }
        public string Alias { get; }
        public Type LogicType { get; }
        public Type DataType { get; }
        public Type EntityType { get; }
        public string Name { get; }
        public bool ActiveSelf { get; }
        public bool ActiveInHierarchy { get; }
        public string SceneName { get; }
    }

    public partial class EntityManager
    {
        /// <summary>
        /// Gets a read-only editor debug snapshot of currently valid entities.
        /// </summary>
        public EntityManagerDebugSnapshot GetDebugSnapshot()
        {
            var containers = new List<EntityContainerDebugSnapshot>(m_EntityViewManager.GetDebugSnapshots());
            containers.Sort(CompareContainerSnapshots);

            var entities = new List<EntityDebugSnapshot>(m_LogicEntityDic.Count);
            foreach (LogicEntity logic in m_LogicEntityDic.Values)
            {
                if (!IsDebugLogicValid(logic))
                {
                    continue;
                }

                entities.Add(new EntityDebugSnapshot(logic));
            }

            entities.Sort(CompareEntitySnapshots);
            return new EntityManagerDebugSnapshot(containers, entities, m_EntityAliasDic.Count);
        }

        private bool IsDebugLogicValid(LogicEntity logic)
        {
            Entity view = logic?.View;
            return view != null
                && view.Logic == logic
                && !string.IsNullOrEmpty(logic.Id)
                && m_LogicEntityDic.TryGetValue(logic.Id, out LogicEntity registeredLogic)
                && registeredLogic == logic;
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
