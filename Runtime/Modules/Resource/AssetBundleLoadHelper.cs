using System;
using System.Collections.Generic;
using XFramework.Tasks;
using UnityEngine;

namespace XFramework.Resource
{
    /// <summary>
    /// AB包加载助手
    /// </summary>
    public class AssetBundleLoadHelper : IResourceLoadHelper
    {
        /// <summary>
        /// ab包路径
        /// </summary>
        private readonly string m_ABPath;
        /// <summary>
        /// ab包后缀名
        /// </summary>
        private readonly string m_Variant;
        /// <summary>
        /// AB包的缓存
        /// </summary>
        private Dictionary<string, AssetBundle> m_ABDic;
        /// <summary>
        /// 用于获取AB包的依赖关系
        /// </summary>
        private DependenciesData m_DependenceInfo;
        /// <summary>
        /// 正在异步加载中的ab包
        /// </summary>
        private Dictionary<string, AssetBundleCreateRequest> m_LoadingAB;
        /// <summary>
        /// 资源根目录
        /// </summary>
        public string AssetPath => m_ABPath;
        /// <summary>
        /// 资源后缀名
        /// </summary>
        public string Variant => m_Variant;

        public AssetBundleLoadHelper(string abPath = "", string variant = "") : this(abPath, variant, "depenencies")
        {

        }

        public AssetBundleLoadHelper(string abPath, string variant, string depenenciesFileName)
        {
            m_ABPath = string.IsNullOrEmpty(abPath) ? Application.streamingAssetsPath + "/AssetBundles" : abPath;
            m_Variant = string.IsNullOrEmpty(variant) ? ".ab" : "." + variant;

            m_ABDic = new Dictionary<string, AssetBundle>();
            m_LoadingAB = new Dictionary<string, AssetBundleCreateRequest>();

            string dependencePath = $"{m_ABPath}/{depenenciesFileName}.json";
            if (System.IO.File.Exists(dependencePath))
            {
                string json = System.IO.File.ReadAllText(dependencePath);
                m_DependenceInfo = JsonUtility.FromJson<DependenciesData>(json);
            }
            else
            {
                m_DependenceInfo = new DependenciesData(new SingleDepenciesData[0]);
                Debug.LogWarning($"[AssetBundleHelper] 路径 {m_ABPath} 下无依赖关系");
            }
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称（完整名称，加路径及后缀名）</param>
        /// <returns>资源</returns>
        public T Load<T>(string assetName) where T : UnityEngine.Object
        {
            var temp = Utility.Text.SplitPathName(assetName);
            return GetAssetBundle(temp[0])?.LoadAsset<T>(temp[1]);
        }

        /// <summary>
        /// 加载一个路径下的所有资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="isTopOnly">是否是仅加载本层级的资源</param>
        /// <returns></returns>
        public T[] LoadAll<T>(string path, bool isTopOnly = true) where T : UnityEngine.Object
        {
            if (isTopOnly)
            {
                return GetAssetBundle(path).LoadAllAssets<T>();
            }
            else
            {
                var abs = GetAssetBundles(path);
                List<T> assets = new List<T>();
                foreach (var item in abs)
                {
                    assets.AddRange(item.LoadAllAssets().Convert<T>());
                }
                return assets.ToArray();
            }
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称（完整名称，加路径及后缀名）</param>
        /// <param name="callback">回调函数</param>
        /// <returns>加载进度</returns>
        public IProgress LoadAsync<T>(string assetName, Action<T> callback) where T : UnityEngine.Object
        {
            var temp = Utility.Text.SplitPathName(assetName);

            DynamicMultiProgress progress = new DynamicMultiProgress(2, 0.9f, 0.1f);

            var abProgress = GetAssetBundleAsync(temp[0], (ab) =>
            {
                var request = ab.LoadAssetAsync(temp[1]);
                SingleTask task = new SingleTask(() =>
                {
                    return request.isDone;
                });
                task.Then(() => { callback(request.asset as T); return true; });
                TaskManager.Instance.StartTask(task);

                SingleResProgress resProgress = new SingleResProgress(request);
                progress.Add(resProgress);
            });
            progress.Add(abProgress);

            return progress;
        }

        /// <summary>
        /// 同步加载一组资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="isTopOnly">是否是仅加载本层级的资源</param>
        /// <returns>资源</returns>
        public IProgress LoadAllSync<T>(string path, bool isTopOnly, Action<T[]> callback) where T : UnityEngine.Object
        {
            if (isTopOnly)
            {
                return GetAssetBundleAsync(path, (ab) =>
                {
                    AssetBundleRequest request = ab.LoadAllAssetsAsync<T>();
                    SingleTask task = new SingleTask(() =>
                    {
                        return request.isDone;
                    });
                    task.Then(() => { callback(request.allAssets.Convert<T>()); return true; });
                });
            }
            else
            {
                List<T> assets = new List<T>();

                DynamicMultiProgress dynamicProgress = new DynamicMultiProgress(2);
                var assetBundlesProgress = GetAssetBundlesAsync(path, OnAssetBundleLoadComplate);

                return dynamicProgress;

                void OnAssetBundleLoadComplate(IEnumerable<AssetBundle> assetBundles)
                {
                    List<IProgress> progresses = new List<IProgress>();
                    foreach (var ab in assetBundles)
                    {
                        var request = ab.LoadAllAssetsAsync<T>();
                        SingleTask task = new SingleTask(() =>
                        {
                            return request.isDone;
                        });
                        task.Then(() => { assets.AddRange(request.allAssets.Convert<T>()); return true; });
                        progresses.Add(new SingleResProgress(request));
                    }
                    dynamicProgress.Add(new MultiProgress(progresses.ToArray()));
                }
            }
        }

        #region AB包加载

        /// <summary>
        /// 获取一个AB包文件
        /// </summary>
        /// <param name="path">相对路径</param>
        /// <returns></returns>
        private AssetBundle GetAssetBundle(string path)
        {
            path = Path2Key(path);
            if (!m_ABDic.TryGetValue(path, out AssetBundle ab))
            {
                string fullName = ABPath2FullPath(path);
                ab = AssetBundle.LoadFromFile(fullName);

                m_ABDic.Add(path, ab);

                //加载当前AB包的依赖包
                //string[] dependencies = m_Mainfest.GetAllDependencies(ab.name);
                string[] dependencies = m_DependenceInfo.GetAllDependencies(ab.name);

                foreach (var item in dependencies)
                {
                    string key = Name2Key(item);  // 对key去除.ab
                    if (!m_ABDic.ContainsKey(key))
                    {
                        GetAssetBundle(key);
                    }
                }
            }

            return ab;
        }

        /// <summary>
        /// 获取一组ab包
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private IEnumerable<AssetBundle> GetAssetBundles(string path)
        {
            List<AssetBundle> assetBundles = new List<AssetBundle>();

            path = Path2Key(path);

            var topAb = GetAssetBundle(path);
            if (topAb != null)
            {
                assetBundles.Add(topAb);
            }

            string dirPath = m_ABPath + "/" + path;
            var a = new System.IO.DirectoryInfo(dirPath);
            var abFiles = a.GetFiles($"*{m_Variant}", System.IO.SearchOption.AllDirectories);

            int startIndex = m_ABPath.Length + 1;

            foreach (var item in abFiles)
            {
                var abPath = item.FullName.Substring(startIndex);
                var ab = GetAssetBundle(abPath);
                if (ab != null)
                {
                    assetBundles.Add(ab);
                }
            }

            return assetBundles;
        }

        /// <summary>
        /// 异步获取AB包
        /// </summary>
        /// <param name="path"></param>
        private IProgress GetAssetBundleAsync(string path, Action<AssetBundle> callBack)
        {
            path = Path2Key(path);
            if (!m_ABDic.TryGetValue(path, out AssetBundle ab))
            {
                string abName = path;
                //string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;

                AssetBundleCreateRequest mainRequest = AssetBundle.LoadFromFileAsync(m_ABPath + "/" + abName + m_Variant);
                m_LoadingAB.Add(abName, mainRequest);

                // 添加任务列表
                List<AssetBundleCreateRequest> requests = new List<AssetBundleCreateRequest>
                {
                    mainRequest
                };

                //string[] dependencies = m_Mainfest.GetAllDependencies(path + ".ab");
                string[] dependencies = m_DependenceInfo.GetAllDependencies(path + m_Variant);
                foreach (var name in dependencies)
                {
                    if (m_ABDic.ContainsKey(Name2Key(name)))
                        continue;
                    var request = AssetBundle.LoadFromFileAsync(m_ABPath + "/" + name);
                    requests.Add(request);
                    m_LoadingAB.Add(Name2Key(name), request);
                }

                ITask[] tasks = new ITask[requests.Count];

                for (int i = 0; i < tasks.Length; i++)
                {
                    int index = i;
                    tasks[index] = new SingleTask(() =>
                    {
                        return requests[index].isDone;
                    });
                    tasks[index].Then(new SingleTask(() =>
                    {
                        string key = Name2Key(requests[index].assetBundle.name);
                        m_ABDic.Add(key, requests[index].assetBundle);
                        m_LoadingAB.Remove(key);
                        return true;
                    }));
                }

                AllTask abTask = new AllTask(tasks);
                abTask.Then(new SingleTask(() =>
                {
                    callBack.Invoke(mainRequest.assetBundle);
                    return true;
                }));
                TaskManager.Instance.StartTask(abTask);

                return new ResProgress(requests.ToArray());
            }
            else
            {
                callBack(ab);
                return new DefaultProgress();
            }
        }

        /// <summary>
        /// 异步获取一组ab包
        /// </summary>
        /// <param name="path"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IProgress GetAssetBundlesAsync(string path, Action<IEnumerable<AssetBundle>> callback)
        {
            List<IProgress> progresses = new List<IProgress>();

            path = Path2Key(path);

            List<AssetBundle> assetBundles = new List<AssetBundle>();

            // top层
            var abProgress = GetAssetBundleAsync(path, (ab) =>
            {
                assetBundles.Add(ab);
            });
            progresses.Add(abProgress);

            // 其它层
            string directoryPath = m_ABPath + "/" + path;
            var a = new System.IO.DirectoryInfo(directoryPath);
            var abFiles = a.GetFiles($"*{m_Variant}", System.IO.SearchOption.AllDirectories);

            int startIndex = m_ABPath.Length + 1;
            int abCount = abFiles.Length + 1;
            foreach (var item in abFiles)
            {
                var abPath = item.FullName.Substring(startIndex);

                var progress = GetAssetBundleAsync(abPath, (ab) =>
                {
                    assetBundles.Add(ab);
                });
                progresses.Add(abProgress);
            }

            SingleTask singleTask = new SingleTask(() => { return assetBundles.Count == abCount; });
            singleTask.Then(() => { callback(assetBundles); return true; });
            TaskManager.Instance.StartTask(singleTask);

            return new MultiProgress(progresses.ToArray());
        }

        #endregion

        private string Name2Key(string abName)
        {
            return abName.Substring(0, abName.Length - (m_Variant.Length));
        }

        private string Path2Key(string path)
        {
            path = path.ToLower();
            return path.Replace('\\', '/');
        }

        private string ABPath2FullPath(string path)
        {
            string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;
            return m_ABPath + abName + m_Variant;
        }

        /// <summary>
        /// 卸载AB包
        /// </summary>
        /// <param name="path"></param>
        /// <param name="unLoadAllObjects"></param>
        public void UnLoad(string path, bool unLoadAllObjects = true)
        {
            GetAssetBundle(path).Unload(unLoadAllObjects);
            m_ABDic.Remove(path);
        }

        /// <summary>
        /// 卸载对应ab包
        /// </summary>
        /// <param name="name">ab包名称</param>
        public void UnLoad(string name)
        {
            m_ABDic.TryGetValue(name, out AssetBundle ab);
            if (ab != null)
            {
                ab.Unload(true);
                m_ABDic.Remove(name);
            }
        }

        /// <summary>
        /// 卸载所有ab包
        /// </summary>
        public void UnLoadAll()
        {
            foreach (var item in m_ABDic.Values)
            {
                item.Unload(true);
            }
            foreach (var item in m_LoadingAB.Values)
            {
                item.assetBundle.Unload(true);
            }
            m_ABDic.Clear();
        }
    }
}