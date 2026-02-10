using System;
using System.Collections.Generic;
using XFramework.Tasks;
using UObject = UnityEngine.Object;

namespace XFramework.Resource
{
        
    public delegate void LoadAssetDelegate(bool success, UObject asset);
    public delegate void LoadAssetDelegate<in T>(bool success, T asset) where T : UObject;
    
    /// <summary>
    /// 资源加载辅助类
    /// </summary>
    public interface IResourceLoadHelper
    {
        /// <summary>
        /// 资源路径
        /// </summary>
        string AssetPath { get; }
        bool IsAssetExist(string assetName);
        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <returns>资源</returns>
        T Load<T>(string assetName) where T : UObject;
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <returns>加载任务</returns>
        // IProgressTask<T> LoadAsync<T>(string assetName) where T : UObject;
        
        void LoadAsync<T>(string assetName, LoadAssetDelegate<T> callBack) where T : UObject;
        /// <summary>
        /// 释放资源
        /// </summary>
        void Release(UObject obj);
        /// <summary>
        /// 释放所有资源
        /// </summary>
        void ReleaseAll();

        void OnUpdate();
    }
}