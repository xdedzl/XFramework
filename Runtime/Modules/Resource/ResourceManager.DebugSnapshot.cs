using System.Collections.Generic;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace XFramework.Resource
{
    public enum ResourceInstanceDebugState
    {
        NormalActive,
        PooledActive,
        PooledFree
    }

    public readonly struct ResourceManagerDebugSnapshot
    {
        public ResourceManagerDebugSnapshot(
            string loadHelperName,
            string assetPath,
            bool usesAssetBundles,
            IReadOnlyList<ResourceAssetDebugSnapshot> assets,
            IReadOnlyList<ResourceBundleDebugSnapshot> bundles,
            IReadOnlyList<ResourceInstanceGroupDebugSnapshot> instanceGroups,
            IReadOnlyList<ResourceInstanceDebugSnapshot> instances)
        {
            LoadHelperName = loadHelperName;
            AssetPath = assetPath;
            UsesAssetBundles = usesAssetBundles;
            Assets = assets;
            Bundles = bundles;
            InstanceGroups = instanceGroups;
            Instances = instances;
        }

        public string LoadHelperName { get; }
        public string AssetPath { get; }
        public bool UsesAssetBundles { get; }
        public IReadOnlyList<ResourceAssetDebugSnapshot> Assets { get; }
        public IReadOnlyList<ResourceBundleDebugSnapshot> Bundles { get; }
        public IReadOnlyList<ResourceInstanceGroupDebugSnapshot> InstanceGroups { get; }
        public IReadOnlyList<ResourceInstanceDebugSnapshot> Instances { get; }
    }

    public readonly struct ResourceAssetDebugSnapshot
    {
        public ResourceAssetDebugSnapshot(
            string cacheKey,
            string assetPath,
            string subAssetName,
            string bundlePath,
            UObject asset,
            bool isLoaded,
            bool isLoading)
        {
            CacheKey = cacheKey;
            AssetPath = assetPath;
            SubAssetName = subAssetName;
            BundlePath = bundlePath;
            Asset = asset;
            IsLoaded = isLoaded;
            IsLoading = isLoading;
        }

        public string CacheKey { get; }
        public string AssetPath { get; }
        public string SubAssetName { get; }
        public string BundlePath { get; }
        public UObject Asset { get; }
        public bool IsLoaded { get; }
        public bool IsLoading { get; }
    }

    public readonly struct ResourceBundleDebugSnapshot
    {
        public ResourceBundleDebugSnapshot(
            string bundlePath,
            AssetBundle bundle,
            IReadOnlyList<string> directDependencies,
            bool isLoaded,
            bool isLoading)
        {
            BundlePath = bundlePath;
            Bundle = bundle;
            DirectDependencies = directDependencies;
            IsLoaded = isLoaded;
            IsLoading = isLoading;
        }

        public string BundlePath { get; }
        public AssetBundle Bundle { get; }
        public IReadOnlyList<string> DirectDependencies { get; }
        public bool IsLoaded { get; }
        public bool IsLoading { get; }
    }

    public readonly struct ResourceInstanceGroupDebugSnapshot
    {
        public ResourceInstanceGroupDebugSnapshot(
            string assetName,
            int normalActiveCount,
            int pooledActiveCount,
            int freeCount)
        {
            AssetName = assetName;
            NormalActiveCount = normalActiveCount;
            PooledActiveCount = pooledActiveCount;
            FreeCount = freeCount;
        }

        public string AssetName { get; }
        public int NormalActiveCount { get; }
        public int PooledActiveCount { get; }
        public int FreeCount { get; }
        public int TotalCount => NormalActiveCount + PooledActiveCount + FreeCount;
    }

    public readonly struct ResourceInstanceDebugSnapshot
    {
        public ResourceInstanceDebugSnapshot(
            int instanceId,
            string assetName,
            UObject instance,
            UObject asset,
            ResourceInstanceDebugState state,
            float freeDuration,
            int freeQueueOrder,
            int lruOrder)
        {
            InstanceId = instanceId;
            AssetName = assetName;
            Instance = instance;
            Asset = asset;
            State = state;
            FreeDuration = freeDuration;
            FreeQueueOrder = freeQueueOrder;
            LruOrder = lruOrder;
        }

        public int InstanceId { get; }
        public string AssetName { get; }
        public UObject Instance { get; }
        public UObject Asset { get; }
        public ResourceInstanceDebugState State { get; }
        public float FreeDuration { get; }
        public int FreeQueueOrder { get; }
        public int LruOrder { get; }
    }

    public partial class ResourceManager
    {
        public ResourceManagerDebugSnapshot GetDebugSnapshot()
        {
            var assets = new List<ResourceAssetDebugSnapshot>();
            var bundles = new List<ResourceBundleDebugSnapshot>();
            bool usesAssetBundles = m_LoadHelper is AssetBundleLoadHelper;
            if (usesAssetBundles)
            {
                ((AssetBundleLoadHelper)m_LoadHelper).CollectDebugSnapshots(assets, bundles);
            }

            var instanceGroups = new List<ResourceInstanceGroupDebugSnapshot>();
            var instances = new List<ResourceInstanceDebugSnapshot>();
            m_InstantiateHelper.CollectDebugSnapshots(instanceGroups, instances);

            return new ResourceManagerDebugSnapshot(
                m_LoadHelper.GetType().Name,
                m_LoadHelper.AssetPath,
                usesAssetBundles,
                assets,
                bundles,
                instanceGroups,
                instances);
        }
    }
}
