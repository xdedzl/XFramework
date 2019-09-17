using System.Collections.Generic;
using XFramework.Tasks;
using UnityEngine;

namespace XFramework.Resource
{
    /// <summary>
    /// 资源管理器
    /// </summary>
    public class ResourceManager : IGameModule
    {
        private IResourceLoadHelper m_LoadHelper;
        /// <summary>
        /// 需要实例化的资源
        /// </summary>
        private Dictionary<string, Object> m_AssetDic;

        /// <summary>
        /// 构造一个资源管理器
        /// </summary>
        /// <param name="loadHelper">资源加载辅助类</param>
        public ResourceManager(IResourceLoadHelper loadHelper)
        {
            m_LoadHelper = loadHelper;
            m_AssetDic = new Dictionary<string, Object>();
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

        /// <summary>
        /// 是否通过Resources内加载
        /// </summary>
        /// <param name="assetName"></param>
        /// <returns></returns>
        private bool IsResources(string assetName)
        {
            return assetName.Substring(0, 3).Equals("Res");
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
            if (string.IsNullOrEmpty(assetName))
            {
                throw new System.Exception("加载资源路径不能为空");
            }
            if (IsResources(assetName))
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
            if (IsResources(path))
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
            if (IsResources(assetName))
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

        #region 资源实例化

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <returns>资源的拷贝</returns>
        public T Instantiate<T>(string assetName) where T : Object
        {

            T asset = GetAsset<T>(assetName);
            T obj = Object.Instantiate<T>(asset);
            return obj;
        }

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="parent">父物体</param>
        /// <returns>资源的拷贝</returns>
        public T Instantiate<T>(string assetName, Transform parent) where T : Object
        {
            T asset = GetAsset<T>(assetName);
            T obj = Object.Instantiate<T>(asset, parent);
            return obj;
        }

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <returns>资源的拷贝</returns>
        public T Instantiate<T>(string assetName, Vector3 position, Quaternion quaternion) where T : Object
        {
            T asset = GetAsset<T>(assetName);
            T obj = Object.Instantiate<T>(asset, position, quaternion);
            return obj;
        }

        /// <summary>
        /// 实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <param name="parent">父物体</param>
        /// <returns>资源的拷贝</returns>
        public T Instantiate<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent) where T : Object
        {
            T asset = GetAsset<T>(assetName);
            T obj = Object.Instantiate<T>(asset, position, quaternion, parent);
            return obj;
        }

        /// <summary>
        /// 获取一个资源
        /// </summary>
        private T GetAsset<T>(string assetName) where T : Object
        {
            m_AssetDic.TryGetValue(assetName, out Object asset);
            if (asset == null)
            {
                asset = Load<T>(assetName);
            }
            return asset as T;
        }

        /// <summary>
        /// 异步实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="callBack">实例化完成回调</param>
        public void InstantiateAsync<T>(string assetName, System.Action<T> callBack = null) where T : Object
        {
            GetAssetAsync<T>(assetName, (asset) =>
            {
                T obj = Object.Instantiate(asset);
                callBack?.Invoke(obj);
            });
        }

        /// <summary>
        /// 异步实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="parent">父物体</param>
        /// <param name="callBack">实例化完成回调</param>
        public void InstantiateAsync<T>(string assetName, Transform parent, System.Action<T> callBack = null) where T : Object
        {
            GetAssetAsync<T>(assetName, (asset) =>
            {
                T obj = Object.Instantiate(asset, parent);
                callBack?.Invoke(obj);
            });
        }

        /// <summary>
        /// 异步实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <param name="callBack">实例化完成回调</param>
        public void InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, System.Action<T> callBack = null) where T : Object
        {
            GetAssetAsync<T>(assetName, (asset) =>
            {
                T obj = Object.Instantiate(asset, position, quaternion);
                callBack?.Invoke(obj);
            });
        }

        /// <summary>
        /// 异步实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="position">位置</param>
        /// <param name="quaternion">方向</param>
        /// <param name="parent">父物体</param>
        /// <param name="callBack">实例化完成回调</param>
        public void InstantiateAsync<T>(string assetName, Vector3 position, Quaternion quaternion, Transform parent, System.Action<T> callBack = null) where T : Object
        {
            GetAssetAsync<T>(assetName, (asset) =>
            {
                T obj = Object.Instantiate(asset, position, quaternion, parent);
                callBack?.Invoke(obj);
            });
        }

        /// <summary>
        /// 异步获取一个资源
        /// </summary>
        private void GetAssetAsync<T>(string assetName, System.Action<T> callBack) where T : Object
        {
            m_AssetDic.TryGetValue(assetName, out Object asset);
            if (asset == null)
            {
                LoadAsync<T>(assetName, callBack);
            }
            else
            {
                callBack(asset as T);
            }
        }

        #endregion

        #region 接口实现

        public int Priority { get { return 100; } }

        public void Update(float elapseSeconds, float realElapseSeconds) { }

        public void Shutdown()
        {
            m_LoadHelper.UnLoadAll();
            m_AssetDic.Clear();
        }

        #endregion
    }
}