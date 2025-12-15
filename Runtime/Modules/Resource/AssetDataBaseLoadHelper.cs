#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework.Tasks;
using UObject = UnityEngine.Object;

namespace XFramework.Resource
{
    public class AssetDataBaseLoadHelper : IResourceLoadHelper
    {
        /// <summary>
        /// 资源路径
        /// </summary>
        public string AssetPath => Application.dataPath.Replace("/Assets", "");

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <returns>资源</returns>
        public T Load<T>(string assetName) where T : UObject
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
        public T[] LoadAll<T>(string path, bool isTopOnly = true) where T : UObject
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
        /// 异步加载资源
        /// </summary>
        /// <param name="assetName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IProgressTask<T> LoadAsync<T>(string assetName) where T : UObject
        {
            T obj = AssetDatabase.LoadAssetAtPath<T>(assetName);
            var progress = new DefaultProgress<T>(obj);
            var xTask = XTask.WaitProgress(progress);
            xTask.Start();
            return xTask;
        }

        /// <summary>
        /// 同步加载一组资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="isTopOnly">是否是仅加载本层级的资源</param>
        /// <returns>资源</returns>
        public IProgress LoadAllSync<T>(string path, bool isTopOnly, System.Action<IList<T>> callback) where T : UObject
        {
            var assets = LoadAll<T>(path, isTopOnly);
            callback.Invoke(assets);
            return new DefaultProgress();
        }

        /// <summary>
        /// 加载一个文件夹下的所有资源
        /// </summary>
        private T[] LoadAllWithADB<T>(string path, SearchOption searchOption) where T : UObject
        {
            var objs = new List<T>();
            DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "") + path);
            foreach (var item in info.GetFiles("*", searchOption))
            {
                var fullName = item.FullName;
                if (item.Name.EndsWith(".meta"))
                    continue;

                int startIndex = fullName.IndexOf("Assets", StringComparison.Ordinal);
                string assetPath = fullName[startIndex..];
                objs.Add(AssetDatabase.LoadAssetAtPath<T>(assetPath));
            }
            return objs.ToArray();
        }

        public void UnLoad(string name) { }

        public void UnLoadAll() { }
    }
}

#endif