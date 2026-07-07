using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using XFramework;

namespace XFramework.Entity
{
    [DisallowMultipleComponent]
    public class EntitySpawn : MonoBehaviour
    {
        [AssetPath(typeof(GameObject))]
        public string prefabPath;

        [TextDropdown(nameof(GetEntityTypeOptions))]
        public string entityType;

        public bool spawnOnStart = true;

        public Entity SpawnedEntity { get; private set; }

        private void Start()
        {
            if (!Application.isPlaying || !spawnOnStart || string.IsNullOrWhiteSpace(prefabPath))
            {
                return;
            }

            StartCoroutine(SpawnWhenEntityManagerReady());
        }

        public Entity SpawnEntity()
        {
            if (SpawnedEntity != null)
            {
                return SpawnedEntity;
            }

            if (!Application.isPlaying)
            {
                Debug.LogError("[EntitySpawn] SpawnEntity only works in play mode.", this);
                return null;
            }

            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                Debug.LogError("[EntitySpawn] prefabPath is empty.", this);
                return null;
            }

            if (!GameEntry.IsModuleLoaded<EntityManager>())
            {
                Debug.LogError("[EntitySpawn] EntityManager is not loaded.", this);
                return null;
            }

            if (!TryResolveEntityType(out Type resolvedEntityType))
            {
                return null;
            }

            try
            {
                SpawnedEntity = EntityManager.Instance.AllocateWithPrefab(
                    prefabPath,
                    resolvedEntityType,
                    transform.position,
                    transform.rotation,
                    null);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[EntitySpawn] Spawn entity failed. prefabPath:{prefabPath}, entityType:{entityType}\n{exception}", this);
                return null;
            }

            if (SpawnedEntity == null)
            {
                Debug.LogError($"[EntitySpawn] Spawn entity returned null. prefabPath:{prefabPath}, entityType:{entityType}", this);
            }

            return SpawnedEntity;
        }

        private IEnumerator SpawnWhenEntityManagerReady()
        {
            while (!GameEntry.IsModuleLoaded<EntityManager>())
            {
                yield return null;
            }

            SpawnEntity();
        }

        private bool TryResolveEntityType(out Type resolvedEntityType)
        {
            resolvedEntityType = null;
            if (string.IsNullOrWhiteSpace(entityType))
            {
                return true;
            }

            resolvedEntityType = Utility.Reflection.GetType(entityType, "Assembly-CSharp", "XFrameworkRuntime") ?? Type.GetType(entityType);
            if (resolvedEntityType == null)
            {
                Debug.LogError($"[EntitySpawn] entityType is not found. entityType:{entityType}", this);
                return false;
            }

            if (!typeof(Entity).IsAssignableFrom(resolvedEntityType) || resolvedEntityType.IsAbstract)
            {
                Debug.LogError($"[EntitySpawn] entityType must be a non-abstract Entity subtype. entityType:{entityType}", this);
                resolvedEntityType = null;
                return false;
            }

            return true;
        }

        private static string[] GetEntityTypeOptions()
        {
            return Utility.Reflection.GetAssignableTypes(typeof(Entity), "Assembly-CSharp", "XFrameworkRuntime")
                .Where(type => type != null && !type.IsAbstract)
                .Select(type => type.FullName)
                .Where(typeName => !string.IsNullOrEmpty(typeName))
                .Distinct()
                .OrderBy(typeName => typeName)
                .ToArray();
        }
    }
}
