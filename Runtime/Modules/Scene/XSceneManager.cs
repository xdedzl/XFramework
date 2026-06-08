using UnityEngine.Events;
using UnityEngine.SceneManagement;
using XFramework.Tasks;

namespace XFramework
{
    public static class XSceneManager
    {
        private static readonly DirectSceneLoadHelper s_DirectSceneLoadHelper = new();
        private static readonly AssetBundleSceneLoadHelper s_AssetBundleSceneLoadHelper = new();

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

        public static bool LoadScene(string scenePath, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return LoadHelper.LoadScene(scenePath, mode);
        }

        public static bool LoadScene(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return LoadHelper.LoadScene(buildIndex, mode);
        }

        public static XAwaitableTask<bool> LoadSceneAsync(string scenePath, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return LoadHelper.LoadSceneAsync(scenePath, mode);
        }

        public static XAwaitableTask<bool> LoadSceneAsync(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
        {
            return LoadHelper.LoadSceneAsync(buildIndex, mode);
        }

        public static XAwaitableTask<bool> UnloadSceneAsync(string sceneName)
        {
            return LoadHelper.UnloadSceneAsync(sceneName);
        }

        public static XAwaitableTask<bool> UnloadSceneAsync(int buildIndex)
        {
            return LoadHelper.UnloadSceneAsync(buildIndex);
        }

        public static XAwaitableTask<bool> UnloadSceneAsync(Scene scene)
        {
            return LoadHelper.UnloadSceneAsync(scene);
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

        public static bool SetActiveScene(Scene scene)
        {
            return SceneManager.SetActiveScene(scene);
        }

        public static bool IsSceneLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            return SceneManager.GetSceneByName(sceneName).isLoaded;
        }
    }
}
