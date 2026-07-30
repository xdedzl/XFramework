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
#if UNITY_EDITOR
            // 编辑器 Play Mode 下用 EditorSceneManager 绕过 Build Profile 限制，
            // 可加载未加入 Build Settings 的场景。
            return ToBoolTask(
                () => UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    scenePath, new LoadSceneParameters(LoadSceneMode.Additive)),
                $"Load scene failed: {scenePath}.");
#else
            return LoadSceneBySceneManager(scenePath);
#endif
        }

        public virtual XAwaitableTask<bool> UnloadSceneAsync(Scene scene)
        {
            return ToBoolTask(() => SceneManager.UnloadSceneAsync(scene), $"Unload scene failed: {scene.path}.");
        }

        /// <summary>
        /// 通过运行时 SceneManager 加载场景。AB 模式下场景从已加载的 AssetBundle 中加载，必须走此路径。
        /// </summary>
        protected XAwaitableTask<bool> LoadSceneBySceneManager(string scenePath)
        {
            return ToBoolTask(
                () => SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive),
                $"Load scene failed: {scenePath}.");
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
