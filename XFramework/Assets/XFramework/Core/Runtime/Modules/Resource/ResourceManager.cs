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
        /// 构造一个资源管理器
        /// </summary>
        /// <param name="loadHelper">资源加载辅助类</param>
        public ResourceManager(IResourceLoadHelper loadHelper)
        {
            m_LoadHelper = loadHelper;
        }

        /// <summary>
        /// 资源路径
        /// </summary>
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
        /// <returns>资源</returns>
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
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <returns>资源组</returns>
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
        /// <param name="assetName">资源名称</param>
        /// <param name="callBack">回调函数</param>
        /// <returns>加载进度</returns>
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

        public void Update(float elapseSeconds, float realElapseSeconds) { }

        public void Shutdown()
        {
            m_LoadHelper.UnLoadAll();
        }

        #endregion
    }
}