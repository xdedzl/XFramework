using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFramework.Tasks;

namespace XFramework
{
    internal class DirectSceneLoadHelper : ISceneLoadHelper
    {
        public virtual XAwaitableTask<bool> LoadSceneAsync(string scenePath)
        {
            return ToBoolTask(
                () => SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive),
                $"Load scene failed: {scenePath}.");
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
