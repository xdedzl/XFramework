using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using XFramework.Resource;
using XFramework.Tasks;

namespace XFramework
{
    public static class XSceneManager
    {
        private sealed class LoadedXScene
        {
            public LoadedXScene(
                string xScenePath,
                XScene xScene,
                XSceneType sceneType,
                List<Scene> unityScenes,
                long loadOrder)
            {
                XScenePath = xScenePath;
                XScene = xScene;
                SceneType = sceneType;
                UnityScenes = unityScenes;
                LoadOrder = loadOrder;
            }

            public string XScenePath { get; }
            public XScene XScene { get; }
            public XSceneType SceneType { get; }
            public List<Scene> UnityScenes { get; }
            public long LoadOrder { get; }
            public bool IsActive { get; set; } = true;
            public Dictionary<GameObject, bool> RootActiveStates { get; } = new();
        }

        private static readonly DirectSceneLoadHelper s_DirectSceneLoadHelper = new();
        private static readonly AssetBundleSceneLoadHelper s_AssetBundleSceneLoadHelper = new();
        private static readonly Dictionary<string, XSceneType> s_SceneTypes = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, XScene> s_XSceneCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, LoadedXScene> s_LoadedXScenes = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> s_UnitySceneOwners = new(StringComparer.Ordinal);
        private static readonly HashSet<string> s_BusyXScenePaths = new(StringComparer.Ordinal);
        private static readonly HashSet<string> s_BusySceneTypes = new(StringComparer.Ordinal);

        private static bool s_SceneTypesInitialized;
        private static bool s_FallbackSceneCaptured;
        private static Scene s_FallbackScene;
        private static long s_LoadOrder;

        private static ISceneLoadHelper LoadHelper
        {
            get
            {
#if UNITY_EDITOR
                return XApplication.Setting.UseABInEditor ? s_AssetBundleSceneLoadHelper : s_DirectSceneLoadHelper;
#else
                return s_AssetBundleSceneLoadHelper;
#endif
            }
        }

        public static Scene ActiveScene => SceneManager.GetActiveScene();

        public static int SceneCount => SceneManager.sceneCount;

        public static event UnityAction<string> xSceneLoaded;

        public static event UnityAction<string> xSceneUnloaded;

        public static event UnityAction<Scene, LoadSceneMode> sceneLoaded
        {
            add => SceneManager.sceneLoaded += value;
            remove => SceneManager.sceneLoaded -= value;
        }

        public static event UnityAction<Scene> sceneUnloaded
        {
            add => SceneManager.sceneUnloaded += value;
            remove => SceneManager.sceneUnloaded -= value;
        }

        public static event UnityAction<Scene, Scene> activeSceneChanged
        {
            add => SceneManager.activeSceneChanged += value;
            remove => SceneManager.activeSceneChanged -= value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            s_SceneTypes.Clear();
            s_XSceneCache.Clear();
            s_LoadedXScenes.Clear();
            s_UnitySceneOwners.Clear();
            s_BusyXScenePaths.Clear();
            s_BusySceneTypes.Clear();
            s_SceneTypesInitialized = false;
            s_FallbackSceneCaptured = false;
            s_FallbackScene = default;
            s_LoadOrder = 0;
            xSceneLoaded = null;
            xSceneUnloaded = null;
        }

        public static async XAwaitableTask<bool> LoadSceneAsync(string xScenePath)
        {
            if (string.IsNullOrWhiteSpace(xScenePath))
            {
                Debug.LogError("[XSceneManager] XScene path is empty.");
                return false;
            }

            if (IsSceneLoaded(xScenePath))
            {
                return true;
            }

            if (!s_BusyXScenePaths.Add(xScenePath))
            {
                Debug.LogError($"[XSceneManager] XScene is busy: {xScenePath}.");
                return false;
            }

            string lockedSceneType = null;
            bool scenePathsReserved = false;
            try
            {
                CaptureFallbackScene();

                if (!s_XSceneCache.TryGetValue(xScenePath, out XScene xScene))
                {
                    xScene = await ResourceManager.Instance.LoadAsync<XScene>(xScenePath);
                    if (xScene == null)
                    {
                        Debug.LogError($"[XSceneManager] Load XScene asset failed: {xScenePath}.");
                        return false;
                    }

                    ValidateXScene(xScenePath, xScene);
                    s_XSceneCache.Add(xScenePath, xScene);
                }

                XSceneType sceneType = GetSceneType(xScene.SceneType);
                if (!s_BusySceneTypes.Add(sceneType.Name))
                {
                    Debug.LogError(
                        $"[XSceneManager] Scene type is busy. xScenePath:{xScenePath}, sceneType:{sceneType.Name}.");
                    return false;
                }

                lockedSceneType = sceneType.Name;
                if (sceneType.Name == XSceneType.MainName &&
                    GetLoadedSceneCount(XSceneType.MainName) > 0 &&
                    !await UnloadScenesForMainSwitchAsync())
                {
                    return false;
                }

                if (!await MakeRoomForSceneAsync(sceneType))
                {
                    return false;
                }

                if (!TryReserveUnityScenePaths(xScenePath, xScene))
                {
                    return false;
                }

                scenePathsReserved = true;
                var unityScenes = new List<Scene>(xScene.ScenePaths.Count);
                for (int i = 0; i < xScene.ScenePaths.Count; i++)
                {
                    string unityScenePath = xScene.ScenePaths[i];
                    if (!await LoadHelper.LoadSceneAsync(unityScenePath))
                    {
                        Debug.LogError(
                            $"[XSceneManager] Load Unity scene failed. xScenePath:{xScenePath}, scenePath:{unityScenePath}.");
                        await RollbackLoadedScenesAsync(xScenePath, xScene, unityScenes);
                        return false;
                    }

                    Scene unityScene = SceneManager.GetSceneByPath(unityScenePath);
                    if (!unityScene.IsValid() || !unityScene.isLoaded)
                    {
                        Debug.LogError(
                            $"[XSceneManager] Loaded Unity scene is unavailable. xScenePath:{xScenePath}, scenePath:{unityScenePath}.");
                        await RollbackLoadedScenesAsync(xScenePath, xScene, unityScenes);
                        return false;
                    }

                    unityScenes.Add(unityScene);
                }

                var loadedXScene = new LoadedXScene(
                    xScenePath,
                    xScene,
                    sceneType,
                    unityScenes,
                    ++s_LoadOrder);
                s_LoadedXScenes.Add(xScenePath, loadedXScene);
                scenePathsReserved = false;

                RefreshActiveScene();
                InvokeXSceneLoaded(xScenePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[XSceneManager] Load XScene failed: {xScenePath}. {exception}");
                return false;
            }
            finally
            {
                if (scenePathsReserved && s_XSceneCache.TryGetValue(xScenePath, out XScene xScene))
                {
                    ReleaseUnityScenePaths(xScenePath, xScene);
                }

                if (lockedSceneType != null)
                {
                    s_BusySceneTypes.Remove(lockedSceneType);
                }

                s_BusyXScenePaths.Remove(xScenePath);
            }
        }

        public static async XAwaitableTask<bool> UnloadSceneAsync(string xScenePath)
        {
            if (string.IsNullOrWhiteSpace(xScenePath))
            {
                Debug.LogError("[XSceneManager] XScene path is empty.");
                return false;
            }

            if (!s_LoadedXScenes.TryGetValue(xScenePath, out LoadedXScene loadedXScene))
            {
                Debug.LogError($"[XSceneManager] XScene is not loaded: {xScenePath}.");
                return false;
            }

            if (!s_BusyXScenePaths.Add(xScenePath))
            {
                Debug.LogError($"[XSceneManager] XScene is busy: {xScenePath}.");
                return false;
            }

            string sceneTypeName = loadedXScene.SceneType.Name;
            if (!s_BusySceneTypes.Add(sceneTypeName))
            {
                s_BusyXScenePaths.Remove(xScenePath);
                Debug.LogError(
                    $"[XSceneManager] Scene type is busy. xScenePath:{xScenePath}, sceneType:{sceneTypeName}.");
                return false;
            }

            try
            {
                return await UnloadLoadedXSceneAsync(loadedXScene);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[XSceneManager] Unload XScene failed: {xScenePath}. {exception}");
                return false;
            }
            finally
            {
                s_BusySceneTypes.Remove(sceneTypeName);
                s_BusyXScenePaths.Remove(xScenePath);
            }
        }

        public static bool IsSceneLoaded(string xScenePath)
        {
            if (!s_LoadedXScenes.TryGetValue(xScenePath, out LoadedXScene loadedXScene))
            {
                return false;
            }

            if (loadedXScene.UnityScenes.Count != loadedXScene.XScene.ScenePaths.Count)
            {
                return false;
            }

            foreach (Scene unityScene in loadedXScene.UnityScenes)
            {
                if (!unityScene.isLoaded)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool SetActive(string xScenePath, bool active)
        {
            if (string.IsNullOrWhiteSpace(xScenePath))
            {
                Debug.LogError("[XSceneManager] XScene path is empty.");
                return false;
            }

            if (!s_LoadedXScenes.TryGetValue(xScenePath, out LoadedXScene loadedXScene))
            {
                Debug.LogError($"[XSceneManager] XScene is not loaded: {xScenePath}.");
                return false;
            }

            if (loadedXScene.IsActive == active)
            {
                return true;
            }

            if (IsSceneBusy(xScenePath))
            {
                Debug.LogError($"[XSceneManager] XScene is busy: {xScenePath}.");
                return false;
            }

            if (active)
            {
                ActivateLoadedXScene(loadedXScene);
            }
            else
            {
                DeactivateLoadedXScene(loadedXScene);
            }

            return true;
        }

        public static bool IsSceneBusy(string xScenePath)
        {
            if (s_BusyXScenePaths.Contains(xScenePath))
            {
                return true;
            }

            if (s_LoadedXScenes.TryGetValue(xScenePath, out LoadedXScene loadedXScene))
            {
                return s_BusySceneTypes.Contains(loadedXScene.SceneType.Name);
            }

            return s_XSceneCache.TryGetValue(xScenePath, out XScene xScene) &&
                   s_BusySceneTypes.Contains(xScene.SceneType);
        }

        public static IReadOnlyList<Scene> GetUnityScenes(string xScenePath)
        {
            return s_LoadedXScenes.TryGetValue(xScenePath, out LoadedXScene loadedXScene)
                ? loadedXScene.UnityScenes.ToArray()
                : Array.Empty<Scene>();
        }

        public static IReadOnlyList<string> GetLoadedXScenePaths(string sceneTypeName)
        {
            XSceneType sceneType = GetSceneType(sceneTypeName);
            var loadedScenes = new List<LoadedXScene>();
            foreach (LoadedXScene loadedXScene in s_LoadedXScenes.Values)
            {
                if (loadedXScene.SceneType.Name == sceneType.Name)
                {
                    loadedScenes.Add(loadedXScene);
                }
            }

            loadedScenes.Sort((left, right) => left.LoadOrder.CompareTo(right.LoadOrder));
            var paths = new string[loadedScenes.Count];
            for (int i = 0; i < loadedScenes.Count; i++)
            {
                paths[i] = loadedScenes[i].XScenePath;
            }

            return paths;
        }

        public static bool TryGetLoadedXScene(string xScenePath, out XScene xScene)
        {
            if (s_LoadedXScenes.TryGetValue(xScenePath, out LoadedXScene loadedXScene))
            {
                xScene = loadedXScene.XScene;
                return true;
            }

            xScene = null;
            return false;
        }

        public static Scene GetSceneAt(int index)
        {
            return SceneManager.GetSceneAt(index);
        }

        public static Scene GetSceneByName(string name)
        {
            return SceneManager.GetSceneByName(name);
        }

        public static Scene GetSceneByPath(string scenePath)
        {
            return SceneManager.GetSceneByPath(scenePath);
        }

        public static Scene GetSceneByBuildIndex(int buildIndex)
        {
            return SceneManager.GetSceneByBuildIndex(buildIndex);
        }

        private static void CaptureFallbackScene()
        {
            if (s_FallbackSceneCaptured)
            {
                return;
            }

            s_FallbackScene = SceneManager.GetActiveScene();
            s_FallbackSceneCaptured = true;
        }

        private static XSceneType GetSceneType(string sceneTypeName)
        {
            InitializeSceneTypes();
            if (!s_SceneTypes.TryGetValue(sceneTypeName, out XSceneType sceneType))
            {
                throw new XFrameworkException($"[XSceneManager] Scene type is not configured: {sceneTypeName}.");
            }

            return sceneType;
        }

        private static void InitializeSceneTypes()
        {
            if (s_SceneTypesInitialized)
            {
                return;
            }

            s_SceneTypes.Clear();
            foreach (XSceneType sceneType in XSceneType.BuiltIn)
            {
                AddSceneType(sceneType);
            }

            IReadOnlyList<XSceneType> sceneTypes = XApplication.Setting.SceneTypes;
            if (sceneTypes != null)
            {
                foreach (XSceneType sceneType in sceneTypes)
                {
                    AddSceneType(sceneType);
                }
            }

            s_SceneTypesInitialized = true;
        }

        private static void AddSceneType(XSceneType sceneType)
        {
            if (sceneType == null || string.IsNullOrWhiteSpace(sceneType.Name))
            {
                throw new XFrameworkException("[XSceneManager] Scene type name is empty.");
            }

            if (sceneType.MaxLoadedSceneCount < 1)
            {
                throw new XFrameworkException(
                    $"[XSceneManager] Scene type capacity must be greater than zero: {sceneType.Name}.");
            }

            if (!s_SceneTypes.TryAdd(sceneType.Name, sceneType))
            {
                throw new XFrameworkException($"[XSceneManager] Duplicate scene type: {sceneType.Name}.");
            }
        }

        private static void ValidateXScene(string xScenePath, XScene xScene)
        {
            if (!xScenePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new XFrameworkException($"[XSceneManager] XScene path must be an asset path: {xScenePath}.");
            }

            GetSceneType(xScene.SceneType);
            if (xScene.ScenePaths == null || xScene.ScenePaths.Count == 0)
            {
                throw new XFrameworkException($"[XSceneManager] XScene has no Unity scene paths: {xScenePath}.");
            }

            var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string unityScenePath in xScene.ScenePaths)
            {
                if (string.IsNullOrWhiteSpace(unityScenePath) ||
                    !unityScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    throw new XFrameworkException(
                        $"[XSceneManager] Invalid Unity scene path. xScenePath:{xScenePath}, scenePath:{unityScenePath}.");
                }

                if (!uniquePaths.Add(unityScenePath))
                {
                    throw new XFrameworkException(
                        $"[XSceneManager] Duplicate Unity scene path. xScenePath:{xScenePath}, scenePath:{unityScenePath}.");
                }
            }
        }

        private static async XAwaitableTask<bool> MakeRoomForSceneAsync(XSceneType sceneType)
        {
            while (GetLoadedSceneCount(sceneType.Name) >= sceneType.MaxLoadedSceneCount)
            {
                LoadedXScene oldestScene = GetOldestLoadedScene(sceneType.Name);
                if (!await UnloadLoadedXSceneAsync(oldestScene))
                {
                    Debug.LogError(
                        $"[XSceneManager] Unload oldest XScene failed. xScenePath:{oldestScene.XScenePath}, sceneType:{sceneType.Name}.");
                    return false;
                }
            }

            return true;
        }

        private static async XAwaitableTask<bool> UnloadScenesForMainSwitchAsync()
        {
            var scenesToUnload = new List<LoadedXScene>();
            var sceneTypesToLock = new HashSet<string>(StringComparer.Ordinal);
            foreach (LoadedXScene loadedXScene in s_LoadedXScenes.Values)
            {
                if (loadedXScene.SceneType.Name != XSceneType.MainName &&
                    loadedXScene.SceneType.UnloadOnMainSceneChanged)
                {
                    scenesToUnload.Add(loadedXScene);
                    sceneTypesToLock.Add(loadedXScene.SceneType.Name);
                }
            }

            scenesToUnload.Sort((left, right) => left.LoadOrder.CompareTo(right.LoadOrder));
            var lockedSceneTypes = new List<string>(sceneTypesToLock.Count);
            foreach (string sceneTypeName in sceneTypesToLock)
            {
                if (!s_BusySceneTypes.Add(sceneTypeName))
                {
                    foreach (string lockedSceneType in lockedSceneTypes)
                    {
                        s_BusySceneTypes.Remove(lockedSceneType);
                    }

                    Debug.LogError(
                        $"[XSceneManager] Cannot switch main scene because scene type is busy: {sceneTypeName}.");
                    return false;
                }

                lockedSceneTypes.Add(sceneTypeName);
            }

            try
            {
                foreach (LoadedXScene loadedXScene in scenesToUnload)
                {
                    if (!await UnloadLoadedXSceneAsync(loadedXScene))
                    {
                        return false;
                    }
                }
            }
            finally
            {
                foreach (string sceneTypeName in lockedSceneTypes)
                {
                    s_BusySceneTypes.Remove(sceneTypeName);
                }
            }

            return true;
        }

        private static int GetLoadedSceneCount(string sceneTypeName)
        {
            int count = 0;
            foreach (LoadedXScene loadedXScene in s_LoadedXScenes.Values)
            {
                if (loadedXScene.SceneType.Name == sceneTypeName)
                {
                    count++;
                }
            }

            return count;
        }

        private static LoadedXScene GetOldestLoadedScene(string sceneTypeName)
        {
            LoadedXScene oldestScene = null;
            foreach (LoadedXScene loadedXScene in s_LoadedXScenes.Values)
            {
                if (loadedXScene.SceneType.Name != sceneTypeName)
                {
                    continue;
                }

                if (oldestScene == null || loadedXScene.LoadOrder < oldestScene.LoadOrder)
                {
                    oldestScene = loadedXScene;
                }
            }

            return oldestScene;
        }

        private static bool TryReserveUnityScenePaths(string xScenePath, XScene xScene)
        {
            foreach (string unityScenePath in xScene.ScenePaths)
            {
                if (s_UnitySceneOwners.TryGetValue(unityScenePath, out string ownerPath))
                {
                    Debug.LogError(
                        $"[XSceneManager] Unity scene is already owned. scenePath:{unityScenePath}, owner:{ownerPath}, requester:{xScenePath}.");
                    return false;
                }

                Scene loadedScene = SceneManager.GetSceneByPath(unityScenePath);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                {
                    Debug.LogError(
                        $"[XSceneManager] Unity scene is already loaded outside XSceneManager: {unityScenePath}.");
                    return false;
                }
            }

            foreach (string unityScenePath in xScene.ScenePaths)
            {
                s_UnitySceneOwners.Add(unityScenePath, xScenePath);
            }

            return true;
        }

        private static void ReleaseUnityScenePaths(string xScenePath, XScene xScene)
        {
            foreach (string unityScenePath in xScene.ScenePaths)
            {
                if (s_UnitySceneOwners.TryGetValue(unityScenePath, out string ownerPath) && ownerPath == xScenePath)
                {
                    s_UnitySceneOwners.Remove(unityScenePath);
                }
            }
        }

        private static async XAwaitableTask RollbackLoadedScenesAsync(
            string xScenePath,
            XScene xScene,
            List<Scene> unityScenes)
        {
            for (int i = unityScenes.Count - 1; i >= 0; i--)
            {
                Scene unityScene = unityScenes[i];
                string unityScenePath = unityScene.path;
                if (!await LoadHelper.UnloadSceneAsync(unityScene))
                {
                    Debug.LogError(
                        $"[XSceneManager] Rollback Unity scene failed. xScenePath:{xScenePath}, scenePath:{unityScenePath}.");
                }
            }

            ReleaseUnityScenePaths(xScenePath, xScene);
        }

        private static async XAwaitableTask<bool> UnloadLoadedXSceneAsync(LoadedXScene loadedXScene)
        {
            for (int i = loadedXScene.UnityScenes.Count - 1; i >= 0; i--)
            {
                Scene unityScene = loadedXScene.UnityScenes[i];
                string unityScenePath = unityScene.path;
                if (!await LoadHelper.UnloadSceneAsync(unityScene))
                {
                    Debug.LogError(
                        $"[XSceneManager] Unload Unity scene failed. xScenePath:{loadedXScene.XScenePath}, scenePath:{unityScenePath}.");
                    return false;
                }

                loadedXScene.UnityScenes.RemoveAt(i);
                s_UnitySceneOwners.Remove(unityScenePath);
            }

            s_LoadedXScenes.Remove(loadedXScene.XScenePath);
            RefreshActiveScene();
            InvokeXSceneUnloaded(loadedXScene.XScenePath);
            return true;
        }

        private static void ActivateLoadedXScene(LoadedXScene loadedXScene)
        {
            loadedXScene.IsActive = true;
            RefreshActiveScene();

            foreach (KeyValuePair<GameObject, bool> rootActiveState in loadedXScene.RootActiveStates)
            {
                if (rootActiveState.Key != null)
                {
                    rootActiveState.Key.SetActive(rootActiveState.Value);
                }
            }

            loadedXScene.RootActiveStates.Clear();
        }

        private static void DeactivateLoadedXScene(LoadedXScene loadedXScene)
        {
            foreach (Scene unityScene in loadedXScene.UnityScenes)
            {
                foreach (GameObject rootGameObject in unityScene.GetRootGameObjects())
                {
                    loadedXScene.RootActiveStates.Add(rootGameObject, rootGameObject.activeSelf);
                }
            }

            loadedXScene.IsActive = false;
            RefreshActiveScene();

            foreach (GameObject rootGameObject in loadedXScene.RootActiveStates.Keys)
            {
                if (rootGameObject != null)
                {
                    rootGameObject.SetActive(false);
                }
            }
        }

        private static void InvokeXSceneLoaded(string xScenePath)
        {
            try
            {
                xSceneLoaded?.Invoke(xScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[XSceneManager] xSceneLoaded listener failed: {xScenePath}. {exception}");
            }
        }

        private static void InvokeXSceneUnloaded(string xScenePath)
        {
            try
            {
                xSceneUnloaded?.Invoke(xScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[XSceneManager] xSceneUnloaded listener failed: {xScenePath}. {exception}");
            }
        }

        private static void RefreshActiveScene()
        {
            LoadedXScene targetXScene = null;
            foreach (LoadedXScene loadedXScene in s_LoadedXScenes.Values)
            {
                if (!loadedXScene.IsActive || loadedXScene.UnityScenes.Count == 0)
                {
                    continue;
                }

                if (targetXScene == null ||
                    loadedXScene.SceneType.ActivePriority > targetXScene.SceneType.ActivePriority ||
                    loadedXScene.SceneType.ActivePriority == targetXScene.SceneType.ActivePriority &&
                    loadedXScene.LoadOrder > targetXScene.LoadOrder)
                {
                    targetXScene = loadedXScene;
                }
            }

            Scene targetScene = targetXScene != null
                ? targetXScene.UnityScenes[0]
                : s_FallbackScene;
            if (targetScene.IsValid() && targetScene.isLoaded && SceneManager.GetActiveScene() != targetScene)
            {
                if (!SceneManager.SetActiveScene(targetScene))
                {
                    Debug.LogError($"[XSceneManager] Set active scene failed: {targetScene.path}.");
                }
            }
        }
    }
}
