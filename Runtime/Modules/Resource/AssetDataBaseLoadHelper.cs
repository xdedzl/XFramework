#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XFramework.Resource
{
    public class AssetDataBaseLoadHelper : IResourceLoadHelper
    {
        private readonly string m_AssetPath;

        public AssetDataBaseLoadHelper()
        {
            m_AssetPath = Application.dataPath.Replace("/Assets", "");
        }

        /// <summary>
        /// 资源路径
        /// </summary>
        public string AssetPath => m_AssetPath;

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <returns>资源</returns>
        public T Load<T>(string assetName) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(assetName);
        }

        /// <summary>
        /// 同步加载一组资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="isTopOnly">是否是仅加载本层级的资源</param>
        /// <returns>资源</returns>
        public T[] LoadAll<T>(string path, bool isTopOnly = true) where T : UnityEngine.Object
        {
            if (isTopOnly)
            {
                return LoadAllWithADB<T>(path, SearchOption.TopDirectoryOnly);
            }
            else
            {
                return LoadAllWithADB<T>(path, SearchOption.AllDirectories);
            }
        }

        /// <summary>
        /// 效果同Load<T>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public IProgress LoadAsync<T>(string assetName, System.Action<T> callback) where T : UnityEngine.Object
        {
            T obj = AssetDatabase.LoadAssetAtPath<T>(assetName);
            callback(obj);
            return new DefaultProgress();
        }

        /// <summary>
        /// 同步加载一组资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="isTopOnly">是否是仅加载本层级的资源</param>
        /// <returns>资源</returns>
        public IProgress LoadAllSync<T>(string path, bool isTopOnly, System.Action<T[]> callback) where T : UnityEngine.Object
        {
            var assets = LoadAll<T>(path, isTopOnly);
            callback.Invoke(assets);
            return new DefaultProgress();
        }

        /// <summary>
        /// 加载一个文件夹下的所有资源
        /// </summary>
        private T[] LoadAllWithADB<T>(string path, SearchOption searchOption) where T : Object
        {
            List<T> objs = new List<T>();
            DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "") + path);
            foreach (var item in info.GetFiles("*", searchOption))
            {
                var fullName = item.FullName;
                if (item.Name.EndsWith(".meta"))
                    continue;

                int startIndex = fullName.IndexOf("Assets");
                string assetPath = fullName.Substring(startIndex);
                objs.Add(AssetDatabase.LoadAssetAtPath<T>(assetPath));
            }
            return objs.ToArray();
        }

        public void UnLoad(string name) { }

        public void UnLoadAll() { }
    }
}

#endif