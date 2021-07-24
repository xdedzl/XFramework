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
            return LoadAssetBundle(temp[0])?.LoadAsset<T>(temp[1]);
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
                return LoadAssetBundle(path).LoadAllAssets<T>();
            }
            else
            {
                var abs = LoadAssetBundles(path);
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

            var abProgress = LoadAssetBundleAsync(temp[0], (ab) =>
            {
                var request = ab.LoadAssetAsync(temp[1]);
                SingleTask task = new SingleTask(() =>
                {
                    return request.isDone;
                });
                task.Then(() => { callback(request.asset as T); return true; });
                task.Start();

                AsyncOperationProgress resProgress = new AsyncOperationProgress(request);
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
        public IProgress LoadAllSync<T>(string path, bool isTopOnly, Action<IList<T>> callback) where T : UnityEngine.Object
        {
            if (isTopOnly)
            {
                return LoadAssetBundleAsync(path, (ab) =>
                {
                    AssetBundleRequest request = ab.LoadAllAssetsAsync<T>();
                    SingleTask task = new SingleTask(() =>
                    {
                        return request.isDone;
                    });
                    task.Then(() => { callback(request.allAssets.Convert<T>()); return true; });
                    task.Start();
                });
            }
            else
            {
                List<T> assets = new List<T>();

                DynamicMultiProgress dynamicProgress = new DynamicMultiProgress(2);
                var assetBundlesProgress = LoadAssetBundlesAsync(path, OnAssetBundleLoadComplate);

                return dynamicProgress;

                void OnAssetBundleLoadComplate(IEnumerable<AssetBundle> assetBundles)
                {
                    List<IProgress> progresses = new List<IProgress>();
                    SingleTask startTask = SingleTask.Create(() => true);
                    ITask currentEndTask = startTask;
                    foreach (var ab in assetBundles)
                    {
                        var request = ab.LoadAllAssetsAsync<T>();
                        SingleTask task = new SingleTask(() =>
                        {
                            return request.isDone;
                        });
                        var endTask = task.Then(() => { assets.AddRange(request.allAssets.Convert<T>()); return true; });
                        progresses.Add(new AsyncOperationProgress(request));

                        if(currentEndTask != null)
                        {
                            currentEndTask.Then(task);
                        }
                        currentEndTask = endTask;
                    }
                    dynamicProgress.Add(new MultiProgress(progresses.ToArray()));

                    currentEndTask.Then(() => { callback(assets); });
                    startTask.Start();
                }
            }
        }

        #region AB包加载

        /// <summary>
        /// 获取一个AB包文件
        /// </summary>
        /// <param name="path">相对路径</param>
        /// <returns></returns>
        private AssetBundle LoadAssetBundle(string path)
        {
            path = Path2Key(path);
            if (!m_ABDic.TryGetValue(path, out AssetBundle ab))
            {
                string fullName = Convert2FullPath(path);
                ab = AssetBundle.LoadFromFile(fullName);
                m_ABDic.Add(path, ab);

                //加载当前AB包的依赖包
                //string[] dependencies = m_Mainfest.GetAllDependencies(ab.name);
                string[] dependencies = m_DependenceInfo.GetAllDependencies(ab.name);

                foreach (var item in dependencies)
                {
                    string key = GetNoVariantName(item);  // 对key去除.ab
                    if (!m_ABDic.ContainsKey(key))
                    {
                        LoadAssetBundle(key);
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
        private IEnumerable<AssetBundle> LoadAssetBundles(string path)
        {
            List<AssetBundle> assetBundles = new List<AssetBundle>();

            path = Path2Key(path);

            var topAb = LoadAssetBundle(path);
            if (topAb != null)
            {
                assetBundles.Add(topAb);
            }

            string dirPath = m_ABPath + "/" + path;
            var subDirectory = new System.IO.DirectoryInfo(dirPath);
            if (subDirectory.Exists)
            {
                var abFiles = subDirectory.GetFiles($"*{m_Variant}", System.IO.SearchOption.AllDirectories);
                int startIndex = m_ABPath.Length + 1;

                foreach (var item in abFiles)
                {
                    var abPath = item.FullName.Substring(startIndex);
                    var ab = LoadAssetBundle(abPath);
                    if (ab != null)
                    {
                        assetBundles.Add(ab);
                    }
                }
            }

            return assetBundles;
        }

        /// <summary>
        /// 异步获取AB包
        /// </summary>
        /// <param name="path"></param>
        private IProgress LoadAssetBundleAsync(string path, Action<AssetBundle> callBack)
        {
            string abKey = Path2Key(path);
            if (!m_ABDic.TryGetValue(abKey, out AssetBundle ab))
            {
                AssetBundleCreateRequest mainRequest = AssetBundle.LoadFromFileAsync(Convert2FullPath(abKey));
                m_LoadingAB.Add(abKey, mainRequest);

                // 添加任务列表
                List<AssetBundleCreateRequest> requests = new List<AssetBundleCreateRequest>
                {
                    mainRequest
                };

                //string[] dependencies = m_Mainfest.GetAllDependencies(path + ".ab");
                string[] dependencies = m_DependenceInfo.GetAllDependencies(abKey + m_Variant);
                foreach (var name in dependencies)
                {
                    string key = GetNoVariantName(name);
                    if (m_ABDic.ContainsKey(key))
                        continue;
                    var request = AssetBundle.LoadFromFileAsync(Convert2FullPath(name));
                    requests.Add(request);
                    m_LoadingAB.Add(key, request);
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
                        string key = GetNoVariantName(requests[index].assetBundle.name);
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
                abTask.Start();

                return new AsyncOperationsProgress(requests.ToArray());
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
        private IProgress LoadAssetBundlesAsync(string path, Action<IEnumerable<AssetBundle>> callback)
        {
            List<IProgress> progresses = new List<IProgress>();

            path = Path2Key(path);

            List<AssetBundle> assetBundles = new List<AssetBundle>();

            // top层
            var abProgress = LoadAssetBundleAsync(path, (ab) =>
            {
                assetBundles.Add(ab);
            });
            progresses.Add(abProgress);
            int abCount = 1;

            // 其它层
            string directoryPath = m_ABPath + "/" + path;
            var subDirectory = new System.IO.DirectoryInfo(directoryPath);
            if (subDirectory.Exists)
            {
                var abFiles = subDirectory.GetFiles($"*{m_Variant}", System.IO.SearchOption.AllDirectories);

                int startIndex = m_ABPath.Length + 1;
                abCount += abFiles.Length;
                foreach (var item in abFiles)
                {
                    var abPath = item.FullName.Substring(startIndex);

                    var progress = LoadAssetBundleAsync(abPath, (ab) =>
                    {
                        assetBundles.Add(ab);
                    });
                    progresses.Add(abProgress);
                }
            }

            SingleTask singleTask = new SingleTask(() => { return assetBundles.Count == abCount; });
            singleTask.Then(() => { callback(assetBundles); return true; });
            singleTask.Start();

            return new MultiProgress(progresses.ToArray());
        }

        #endregion

        private string GetNoVariantName(string abName)
        {
            return abName.Substring(0, abName.Length - (m_Variant.Length));
        }

        private string Path2Key(string path)
        {
            path = path.ToLower();
            return path.Replace('\\', '/');
        }

        private string Convert2FullPath(string path)
        {
            string fullPath = string.IsNullOrEmpty(path) ? "" : "/" + path;
            fullPath = m_ABPath + fullPath;

            if (!fullPath.EndsWith(m_Variant))
            {
                fullPath += m_Variant;
            }

            return fullPath;
        }

        /// <summary>
        /// 卸载AB包
        /// </summary>
        /// <param name="path"></param>
        /// <param name="unLoadAllObjects"></param>
        public void UnLoad(string path, bool unLoadAllObjects = true)
        {
            LoadAssetBundle(path).Unload(unLoadAllObjects);
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