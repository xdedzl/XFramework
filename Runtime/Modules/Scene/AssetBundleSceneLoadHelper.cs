using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFramework.Resource;
using XFramework.Tasks;

namespace XFramework
{
    internal class AssetBundleSceneLoadHelper : DirectSceneLoadHelper
    {
        public override string ToString()
        {
            return nameof(AssetBundleSceneLoadHelper);
        }

        public override bool LoadScene(string scenePath, LoadSceneMode mode)
        {
            try
            {
                if (!ResourceManager.Instance.PrepareAsset(scenePath))
                {
                    Debug.LogError($"[XSceneManager] Scene asset bundle is not ready: {scenePath}.");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[XSceneManager] Prepare scene asset bundle failed: {scenePath}. {e}");
                return false;
            }

            return base.LoadScene(scenePath, mode);
        }

        public override XAwaitableTask<bool> LoadSceneAsync(string scenePath, LoadSceneMode mode)
        {
            var resultTask = new XAwaitableTask<bool>();
            XAwaitableTask<bool> prepareTask;
            try
            {
                prepareTask = ResourceManager.Instance.PrepareAssetAsync(scenePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[XSceneManager] Prepare scene asset bundle failed: {scenePath}. {e}");
                resultTask.SetResult(false);
                return resultTask;
            }

            var prepareAwaiter = prepareTask.GetAwaiter();
            if (prepareAwaiter.IsCompleted)
            {
                OnPrepared();
            }
            else
            {
                prepareAwaiter.OnCompleted(OnPrepared);
            }

            return resultTask;

            void OnPrepared()
            {
                if (!prepareAwaiter.GetResult())
                {
                    Debug.LogError($"[XSceneManager] Scene asset bundle is not ready: {scenePath}.");
                    resultTask.SetResult(false);
                    return;
                }

                XAwaitableTask<bool> loadTask = base.LoadSceneAsync(scenePath, mode);
                var loadAwaiter = loadTask.GetAwaiter();
                if (loadAwaiter.IsCompleted)
                {
                    resultTask.SetResult(loadAwaiter.GetResult());
                }
                else
                {
                    loadAwaiter.OnCompleted(() =>
                    {
                        resultTask.SetResult(loadAwaiter.GetResult());
                    });
                }
            }
        }
    }
}
