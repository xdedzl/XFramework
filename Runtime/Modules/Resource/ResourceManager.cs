using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XFramework.Tasks;
using UObject = UnityEngine.Object;

namespace XFramework.Resource
{
    /// <summary>
    /// 资源管理器
    /// </summary>
    [ModuleLifecycle(ModuleLifecycle.EditorPersistent)]
    public partial class ResourceManager : PersistentMonoGameModuleBase<ResourceManager>
    {
        public const string BuildConfigAssetPath = "Assets/Configs/AssetBundleBuildConfig.asset";
        
        private readonly IResourceLoadHelper m_LoadHelper;      // 实际加载资源的处理器
        private readonly Dictionary<string, string> m_PathMap;  // 资源路径映射
        public bool HasPathMap { get; }
        
        private readonly ResourceInstantiateHelper m_InstantiateHelper;
        
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
            m_InstantiateHelper = new ResourceInstantiateHelper(m_LoadHelper);
            
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
        
        public T LoadInResources<T>(string assetName) where T : UObject
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new XFrameworkException("load path is null");
            }
            assetName = assetName.Split('.')[0];
            return Resources.Load<T>(assetName);
        }
        
        public T[] LoadAllInResources<T>(string path, bool isTopOnly = true) where T : UObject
        {
            path = path.Split('.')[0];
            return Resources.LoadAll<T>(path);
        }
        
        public void LoadAsyncInResources<T>(string assetName, Action<T> callBack) where T : UObject
        {
            LoadAsyncInResources<T>(assetName).ContinueWith(callBack).Forget();
        }
        
        public XAwaitableTask<T> LoadAsyncInResources<T>(string assetName) where T : UObject
        {
            return Resources.LoadAsync(assetName).ToXAwaitableTask<T>();
        }
        
        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns>资源</returns>
        public T Load<T>(string assetName) where T : UObject
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
        
        public void LoadAsync<T>(string assetName, LoadAssetDelegate<T> callback) where T : UObject
        {
            m_LoadHelper.LoadAsync<T>(assetName, callback);
        }
        
        public XAwaitableTask<T> LoadAsync<T>(string assetName) where T : UObject
        {
            return m_LoadHelper.LoadAsync<T>(assetName);
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
        
        # region 资源释放
        
        /// <summary>
        /// 释放实例（如果是池化对象则回收到池中）
        /// </summary>
        /// <param name="unityObject">要释放的对象</param>
        public void Release(UObject unityObject)
        {
            if (unityObject == null) return;
            
            if (m_InstantiateHelper.Release(unityObject))
            {
                // 已经被实例化对象池处理了，返回
            }
            else
            {
                // 非池化对象，直接释放
                m_LoadHelper.Release(unityObject);
            }
        }

        # endregion
        
        #region 接口实现

        public override void Shutdown()
        {
            m_LoadHelper.ReleaseAll();
        }

        public override int Priority => 1000;

        public override void Update()
        {
            m_LoadHelper.OnUpdate();
            m_InstantiateHelper.OnUpdate();
        }

        #endregion

    }
}