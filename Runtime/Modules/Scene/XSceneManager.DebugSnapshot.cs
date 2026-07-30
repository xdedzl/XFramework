using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace XFramework
{
    /// <summary>
    /// XSceneManager 运行时快照，供编辑器调试使用。
    /// </summary>
    public readonly struct XSceneManagerDebugSnapshot
    {
        public XSceneManagerDebugSnapshot(
            IReadOnlyList<LoadedXSceneDebugSnapshot> loadedXScenes,
            IReadOnlyList<XSceneTypeDebugSnapshot> sceneTypes,
            IReadOnlyList<UnitySceneOwnerDebugSnapshot> unitySceneOwners,
            IReadOnlyList<string> busyXScenePaths,
            IReadOnlyList<string> busySceneTypes,
            Scene fallbackScene,
            bool fallbackSceneCaptured,
            long loadOrder,
            Scene activeScene,
            int sceneCount)
        {
            LoadedXScenes = loadedXScenes ?? Array.Empty<LoadedXSceneDebugSnapshot>();
            SceneTypes = sceneTypes ?? Array.Empty<XSceneTypeDebugSnapshot>();
            UnitySceneOwners = unitySceneOwners ?? Array.Empty<UnitySceneOwnerDebugSnapshot>();
            BusyXScenePaths = busyXScenePaths ?? Array.Empty<string>();
            BusySceneTypes = busySceneTypes ?? Array.Empty<string>();
            FallbackScene = fallbackScene;
            FallbackSceneCaptured = fallbackSceneCaptured;
            LoadOrder = loadOrder;
            ActiveScene = activeScene;
            SceneCount = sceneCount;
        }

        public IReadOnlyList<LoadedXSceneDebugSnapshot> LoadedXScenes { get; }
        public IReadOnlyList<XSceneTypeDebugSnapshot> SceneTypes { get; }
        public IReadOnlyList<UnitySceneOwnerDebugSnapshot> UnitySceneOwners { get; }
        public IReadOnlyList<string> BusyXScenePaths { get; }
        public IReadOnlyList<string> BusySceneTypes { get; }
        public Scene FallbackScene { get; }
        public bool FallbackSceneCaptured { get; }
        public long LoadOrder { get; }
        public Scene ActiveScene { get; }
        public int SceneCount { get; }
    }

    /// <summary>
    /// 已加载 XScene 的运行时状态快照。
    /// </summary>
    public readonly struct LoadedXSceneDebugSnapshot
    {
        public LoadedXSceneDebugSnapshot(
            string xScenePath,
            XScene xScene,
            string sceneTypeName,
            int activePriority,
            bool unloadOnMainSceneChanged,
            long loadOrder,
            bool isActive,
            bool isBusy,
            IReadOnlyList<UnitySceneDebugSnapshot> unityScenes,
            int rootGameObjectCount)
        {
            XScenePath = xScenePath ?? string.Empty;
            XScene = xScene;
            SceneTypeName = sceneTypeName ?? string.Empty;
            ActivePriority = activePriority;
            UnloadOnMainSceneChanged = unloadOnMainSceneChanged;
            LoadOrder = loadOrder;
            IsActive = isActive;
            IsBusy = isBusy;
            UnityScenes = unityScenes ?? Array.Empty<UnitySceneDebugSnapshot>();
            RootGameObjectCount = rootGameObjectCount;
        }

        public string XScenePath { get; }
        public XScene XScene { get; }
        public string SceneTypeName { get; }
        public int ActivePriority { get; }
        public bool UnloadOnMainSceneChanged { get; }
        public long LoadOrder { get; }
        public bool IsActive { get; }
        public bool IsBusy { get; }
        public IReadOnlyList<UnitySceneDebugSnapshot> UnityScenes { get; }
        public int RootGameObjectCount { get; }
    }

    /// <summary>
    /// XScene 内包含的 Unity 场景运行时状态快照。
    /// </summary>
    public readonly struct UnitySceneDebugSnapshot
    {
        public UnitySceneDebugSnapshot(string path, string name, bool isValid, bool isLoaded, int rootCount, Scene scene)
        {
            Path = path ?? string.Empty;
            Name = name ?? string.Empty;
            IsValid = isValid;
            IsLoaded = isLoaded;
            RootCount = rootCount;
            Scene = scene;
        }

        public string Path { get; }
        public string Name { get; }
        public bool IsValid { get; }
        public bool IsLoaded { get; }
        public int RootCount { get; }
        public Scene Scene { get; }
    }

    /// <summary>
    /// 场景类型注册表快照。
    /// </summary>
    public readonly struct XSceneTypeDebugSnapshot
    {
        public XSceneTypeDebugSnapshot(
            string name,
            int maxLoadedSceneCount,
            int activePriority,
            bool unloadOnMainSceneChanged,
            int loadedCount)
        {
            Name = name ?? string.Empty;
            MaxLoadedSceneCount = maxLoadedSceneCount;
            ActivePriority = activePriority;
            UnloadOnMainSceneChanged = unloadOnMainSceneChanged;
            LoadedCount = loadedCount;
        }

        public string Name { get; }
        public int MaxLoadedSceneCount { get; }
        public int ActivePriority { get; }
        public bool UnloadOnMainSceneChanged { get; }
        public int LoadedCount { get; }
    }

    /// <summary>
    /// Unity 场景路径归属映射快照（.unity 路径 → 拥有者 XScene 路径）。
    /// </summary>
    public readonly struct UnitySceneOwnerDebugSnapshot
    {
        public UnitySceneOwnerDebugSnapshot(string unityScenePath, string ownerXScenePath)
        {
            UnityScenePath = unityScenePath ?? string.Empty;
            OwnerXScenePath = ownerXScenePath ?? string.Empty;
        }

        public string UnityScenePath { get; }
        public string OwnerXScenePath { get; }
    }

    public static partial class XSceneManager
    {
        /// <summary>
        /// 获取 XSceneManager 当前运行时状态的只读快照，供编辑器调试使用。
        /// </summary>
        public static XSceneManagerDebugSnapshot GetDebugSnapshot()
        {
            var loadedXScenes = new List<LoadedXSceneDebugSnapshot>(s_LoadedXScenes.Count);
            foreach (LoadedXScene loadedXScene in s_LoadedXScenes.Values)
            {
                var unityScenes = new List<UnitySceneDebugSnapshot>(loadedXScene.UnityScenes.Count);
                int rootGameObjectCount = 0;
                foreach (Scene unityScene in loadedXScene.UnityScenes)
                {
                    int rootCount = unityScene.IsValid() && unityScene.isLoaded
                        ? unityScene.rootCount
                        : 0;
                    rootGameObjectCount += rootCount;
                    unityScenes.Add(new UnitySceneDebugSnapshot(
                        unityScene.path,
                        unityScene.name,
                        unityScene.IsValid(),
                        unityScene.isLoaded,
                        rootCount,
                        unityScene));
                }

                bool isBusy = s_BusyXScenePaths.Contains(loadedXScene.XScenePath) ||
                              s_BusySceneTypes.Contains(loadedXScene.SceneType.Name);
                loadedXScenes.Add(new LoadedXSceneDebugSnapshot(
                    loadedXScene.XScenePath,
                    loadedXScene.XScene,
                    loadedXScene.SceneType.Name,
                    loadedXScene.SceneType.ActivePriority,
                    loadedXScene.SceneType.UnloadOnMainSceneChanged,
                    loadedXScene.LoadOrder,
                    loadedXScene.IsActive,
                    isBusy,
                    unityScenes,
                    rootGameObjectCount));
            }

            loadedXScenes.Sort((left, right) => left.LoadOrder.CompareTo(right.LoadOrder));

            var sceneTypes = new List<XSceneTypeDebugSnapshot>(s_SceneTypes.Count);
            foreach (XSceneType sceneType in s_SceneTypes.Values)
            {
                sceneTypes.Add(new XSceneTypeDebugSnapshot(
                    sceneType.Name,
                    sceneType.MaxLoadedSceneCount,
                    sceneType.ActivePriority,
                    sceneType.UnloadOnMainSceneChanged,
                    GetLoadedSceneCount(sceneType.Name)));
            }

            sceneTypes.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));

            var unitySceneOwners = new List<UnitySceneOwnerDebugSnapshot>(s_UnitySceneOwners.Count);
            foreach (KeyValuePair<string, string> pair in s_UnitySceneOwners)
            {
                unitySceneOwners.Add(new UnitySceneOwnerDebugSnapshot(pair.Key, pair.Value));
            }

            unitySceneOwners.Sort((left, right) =>
                string.Compare(left.UnityScenePath, right.UnityScenePath, StringComparison.Ordinal));

            return new XSceneManagerDebugSnapshot(
                loadedXScenes,
                sceneTypes,
                unitySceneOwners,
                new List<string>(s_BusyXScenePaths),
                new List<string>(s_BusySceneTypes),
                s_FallbackScene,
                s_FallbackSceneCaptured,
                s_LoadOrder,
                SceneManager.GetActiveScene(),
                SceneManager.sceneCount);
        }
    }
}
