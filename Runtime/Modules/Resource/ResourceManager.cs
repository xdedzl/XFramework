using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XFramework.Tasks;

namespace XFramework.Resource
{
    /// <summary>
    /// 资源管理器
    /// 若加载路径以 Res/ 开头，则会使用unity Resource.xxx 方式加载）
    /// </summary>
    public partial class ResourceManager : PersistentMonoGameModuleBase<ResourceManager>
    {
        public const string BuildConfigAssetPath = "Assets/Configs/AssetBundleBuildConfig.asset";
        
        private readonly IResourceLoadHelper m_LoadHelper;
        /// <summary>
        /// 资源路径映射
        /// </summary>
        private readonly Dictionary<string, string> m_PathMap;
        public bool HasPathMap { get; }

        /// <summary>
        /// 构造一个资源管理器，如果存在路径映射文件则使用文件名加载资源
        /// </summary>
        /// <param name="loadHelper">资源加载辅助类</param>
        /// <param name="mapInfoPath">路径映射文件</param>
        public ResourceManager()
        {
#if UNITY_EDITOR
            if (XApplication.Setting.UseABInEditor)
            {
                m_LoadHelper = new AssetBundleLoadHelper();
            }
            else
            {
                m_LoadHelper = new AssetDataBaseLoadHelper();
            }
#else
            m_LoadHelper = new AssetBundleLoadHelper();
#endif
            var mapInfoPath = "";
            if (File.Exists(mapInfoPath))
            {
                m_PathMap = new Dictionary<string, string>();
                InitPathMapWithText(mapInfoPath);
                HasPathMap = true;
            }
            else
            {
                HasPathMap = false;
            }
        }

        private void InitPathMapWithText(string mapInfoPath)
        {
            string pathMapInfo = File.ReadAllText(mapInfoPath);
            string[] keyValues = pathMapInfo.Split('\n');
            foreach (var item in keyValues)
            {
                string[] keyValue = item.Split(':');
                m_PathMap.Add(keyValue[0], keyValue[1]);
            }
        }

        /// <summary>
        /// 资源路径
        /// </summary>
        public string AssetPath => m_LoadHelper.AssetPath;

        public bool IsAssetExist(string assetName)
        {
            return m_LoadHelper.IsAssetExist(assetName);
        }

        #region 资源加载
        
        public T LoadInResources<T>(string assetName) where T : Object
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new XFrameworkException("load path is null");
            }
            assetName = assetName.Split('.')[0];
            return Resources.Load<T>(assetName);
        }
        
        public T[] LoadAllInResources<T>(string path, bool isTopOnly = true) where T : Object
        {
            path = path.Split('.')[0];
            return Resources.LoadAll<T>(path);
        }
        
        public IProgress LoadAsyncInResources<T>(string assetName, System.Action<T> callBack) where T : Object
        {
            var request = Resources.LoadAsync(assetName);

            var task = XTask.WaitUntil(() => request.isDone);
            task.ContinueWith(() =>
            {
                callBack.Invoke(request.asset as T);
            });
            task.Start();
            return new ResourceRequestProgress(request);
        }
        
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
                throw new XFrameworkException("load path is null");
            }

            assetName = Path2RealPath(assetName);
            return m_LoadHelper.Load<T>(assetName);
        }
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="assetName">资源名称</param>
        /// <returns>加载任务</returns>
        // public IProgressTask<T> LoadAsync<T>(string assetName) where T : Object
        // {
        //     assetName = Path2RealPath(assetName);
        //     return m_LoadHelper.LoadAsync<T>(assetName);
        // }
        
        public void LoadAsync<T>(string assetName, LoadAssetDelegate<T> callback) where T : Object
        {
            m_LoadHelper.LoadAsync<T>(assetName, callback);
        }

        /// <summary>
        /// 将传入路径转为Assets路径
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string Path2RealPath(string path)
        {
            if (HasPathMap)
            {
                if(m_PathMap.TryGetValue(path, out string realPath))
                {
                    return realPath;
                }
                else
                {
                    throw new XFrameworkException($"[Resource] There is no resource which path is {path} or PathMapInfo is obsolete");
                }
            }
            else
            {
                return path;
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

            T asset = Load<T>(assetName);
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
            T asset = Load<T>(assetName);
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
            T asset = Load<T>(assetName);
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
            T asset = Load<T>(assetName);
            T obj = Object.Instantiate<T>(asset, position, quaternion, parent);
            return obj;
        }

        /// <summary>
        /// 异步实例化资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="callBack">实例化完成回调</param>
        public void InstantiateAsync<T>(string assetName, System.Action<T> callBack = null) where T : Object
        {
            LoadAsync<T>(assetName, (success, asset) =>
            {
                if (success)
                {
                    T obj = Object.Instantiate(asset);
                    callBack?.Invoke(obj);
                }
                else
                {
                    Debug.LogError("[Resource] There is no resource which path is " + assetName);
                }
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
            LoadAsync<T>(assetName, (success, asset) =>
            {
                if (success)
                {
                    T obj = Object.Instantiate(asset, parent);
                    callBack?.Invoke(obj);
                }
                else
                {
                    Debug.LogError("[Resource] There is no resource which path is " + assetName);
                }
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
            LoadAsync<T>(assetName, (success, asset) =>
            {
                if (success)
                {
                    T obj = Object.Instantiate(asset, position, quaternion);
                    callBack?.Invoke(obj);
                }
                else
                {
                    Debug.LogError("[Resource] There is no resource which path is " + assetName);
                }
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
            LoadAsync<T>(assetName, (success, asset) =>
            {
                if (success)
                {
                    T obj = Object.Instantiate(asset, position, quaternion, parent);
                    callBack?.Invoke(obj);
                }
                else
                {
                    Debug.LogError("[Resource] There is no resource which path is " + assetName);
                }
            });
        }
        #endregion

        #region 接口实现

        public override void Shutdown()
        {
            m_LoadHelper.ReleaseAll();
        }

        public override int Priority => 1000;

        public override void Update()
        {
            m_LoadHelper.OnUpdate();
        }

        #endregion

    }
}