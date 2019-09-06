using UnityEngine;

namespace XFramework.Resource
{
    /// <summary>
    /// 资源加载辅助类
    /// </summary>
    public interface IResourceLoadHelper
    {
        /// <summary>
        /// 资源路径
        /// </summary>
        string AssetPath { get; }
        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <returns></returns>
        T Load<T>(string assetName) where T : Object;
        /// <summary>
        /// 同步加载一组资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <returns></returns>
        T[] LoadAll<T>(string path) where T : Object;
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="callback">回调函数</param>
        /// <returns></returns>
        IProgress LoadAsync<T>(string assetName, System.Action<T> callback) where T : Object;
        /// <summary>
        /// 卸载某个资源
        /// </summary>
        /// <param name="name">资源名称</param>
        void UnLoad(string name);
        /// <summary>
        /// 卸载所有资源
        /// </summary>
        void UnLoadAll();
    }
}