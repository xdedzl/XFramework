using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFramework.Tasks;

namespace XFramework
{
    internal class DirectSceneLoadHelper : ISceneLoadHelper
    {
        public virtual bool LoadScene(string scenePath, LoadSceneMode mode)
        {
            try
            {
                SceneManager.LoadScene(scenePath, mode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[XSceneManager] Load scene failed: {scenePath}. {e}");
                return false;
            }
        }

        public virtual bool LoadScene(int buildIndex, LoadSceneMode mode)
        {
            try
            {
                SceneManager.LoadScene(buildIndex, mode);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[XSceneManager] Load scene failed: {buildIndex}. {e}");
                return false;
            }
        }

        public virtual XAwaitableTask<bool> LoadSceneAsync(string scenePath, LoadSceneMode mode)
        {
            return ToBoolTask(() => SceneManager.LoadSceneAsync(scenePath, mode), $"Load scene failed: {scenePath}.");
        }

        public virtual XAwaitableTask<bool> LoadSceneAsync(int buildIndex, LoadSceneMode mode)
        {
            return ToBoolTask(() => SceneManager.LoadSceneAsync(buildIndex, mode), $"Load scene failed: {buildIndex}.");
        }

        public virtual XAwaitableTask<bool> UnloadSceneAsync(string sceneName)
        {
            return ToBoolTask(() => SceneManager.UnloadSceneAsync(sceneName), $"Unload scene failed: {sceneName}.");
        }

        public virtual XAwaitableTask<bool> UnloadSceneAsync(int buildIndex)
        {
            return ToBoolTask(() => SceneManager.UnloadSceneAsync(buildIndex), $"Unload scene failed: {buildIndex}.");
        }

        public virtual XAwaitableTask<bool> UnloadSceneAsync(Scene scene)
        {
            return ToBoolTask(() => SceneManager.UnloadSceneAsync(scene), $"Unload scene failed: {scene.path}.");
        }

        protected static XAwaitableTask<bool> ToBoolTask(Func<AsyncOperation> createOperation, string errorMessage)
        {
            try
            {
                return ToBoolTask(createOperation(), errorMessage);
            }
            catch (Exception e)
            {
                Debug.LogError($"[XSceneManager] {errorMessage} {e}");
                var task = new XAwaitableTask<bool>();
                task.SetResult(false);
                return task;
            }
        }

        protected static XAwaitableTask<bool> ToBoolTask(AsyncOperation operation, string errorMessage)
        {
            var task = new XAwaitableTask<bool>();
            if (operation == null)
            {
                Debug.LogError($"[XSceneManager] {errorMessage}");
                task.SetResult(false);
                return task;
            }

            operation.completed += _ =>
            {
                task.SetResult(true);
            };
            return task;
        }
    }
}
