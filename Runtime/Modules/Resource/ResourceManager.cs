using UnityEngine;
using XFramework.Tasks;

namespace XFramework.Resource
{
    /// <summary>
    /// 资源管理器
    /// </summary>
    public class ResourceManager : IGameModule
    {
        private IResourceLoadHelper m_LoadHelper;

        /// <summary>
        /// 资源加载方式
        /// </summary>
        private ResMode m_ResMode;
        public ResMode ResMode { get { return m_ResMode; } }

        public ResourceManager(IResourceLoadHelper loadHelper)
        {
            m_LoadHelper = loadHelper;
            if(m_LoadHelper is AssetBundleLoadHelper)
                m_ResMode = ResMode.AssetBundle;
            else
                m_ResMode = ResMode.AssetDataBase;
        }

        public string AssetPath
        {
            get
            {
                return m_LoadHelper.AssetPath;
            }
        }

        #region 资源加载

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public T Load<T>(string assetName) where T : Object
        {

            if (assetName.Substring(0, 4) == "Res/")
            {
                assetName = assetName.Substring(4, assetName.Length - 4);
                return Resources.Load<T>(assetName);
            }
            return m_LoadHelper.Load<T>(assetName);
        }

        /// <summary>
        /// 加载一个路径下的所有资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T[] LoadAll<T>(string path) where T : Object
        {
            if (path.Substring(0, 4) == "Res/")
            {
                path = path.Substring(4, path.Length - 4);
                return Resources.LoadAll<T>(path);
            }

            return m_LoadHelper.LoadAll<T>(path);
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="assetName"></param>
        /// <param name="name"></param>
        /// <param name="callBack"></param>
        public IProgress LoadAsync<T>(string assetName, System.Action<T> callBack) where T : Object
        {
            if (assetName.Substring(0, 4) == "Res/")
            {
                assetName = assetName.Substring(4, assetName.Length - 4);
                var request = Resources.LoadAsync(assetName);

                SingleTask task = new SingleTask(() =>
                {
                    return request.isDone;
                });
                task.Then(() =>
                {
                    callBack.Invoke(request.asset as T);
                    return true;
                });
                GameEntry.GetModule<TaskManager>().StartTask(task);
                return null;
            }

            else
            {
                return m_LoadHelper.LoadAsync<T>(assetName, callBack);
            }

        }

        #endregion

        #region 接口实现

        public int Priority { get { return 100; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            //m_TaskPool.Update();
        }

        public void Shutdown()
        {
            AssetBundle.UnloadAllAssetBundles(true);
        }

        #endregion
    }

    public enum ResMode
    {
        AssetBundle,
        AssetDataBase,
    }
}