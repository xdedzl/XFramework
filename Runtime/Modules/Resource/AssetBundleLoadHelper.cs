using System;
using System.Collections.Generic;
using XFramework.Tasks;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace XFramework.Resource
{
    class AssetObject
    {
        public string assetPath;
        public UObject asset;
        // public int refCount;
    }
    
    class AssetLoadTask
    {
        public AssetObject assetObject;
        public AssetBundleRequest assetBundleRequest;
        public LoadAssetDelegate callback;
    }
    
    class AssetBundleObject
    {
        public string abPath;
        public AssetBundle assetBundle;
        // public int refCount;
    }

    class AssetBundleLoadTask
    {
        public AssetBundleCreateRequest assetBundleCreateRequest; 
        public AssetBundleObject assetBundleObject;
        public LoadAssetBundleDelegate callback;
    }

    public delegate void LoadAssetBundleDelegate(bool success, AssetBundle assetBundle);
    
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
        /// 用于获取AB包的依赖关系
        /// </summary>
        private readonly DependenciesData m_DependenceInfo;
        private readonly Dictionary<string, string> m_Asset2Ab = new ();                    // 资源名称到AB包名称的映射
        
        private readonly Dictionary<string, AssetBundle> m_LoadedAB = new ();               // 已加载的AB包
        private readonly Dictionary<string, AssetBundleCreateRequest> m_LoadingAB = new (); // 正在异步加载中的ab包
        
        
        private readonly Dictionary<string, AssetBundleObject> m_LoadedABNew = new();       // 已加载的AB包
        private readonly Dictionary<string, AssetBundleLoadTask> m_LoadingABNew = new ();   // 正在异步加载中的ab包
        private readonly Dictionary<string, AssetBundleObject> m_UnLoadAB = new ();         // 待卸载的AB包
        
        private readonly Dictionary<string, AssetObject> m_LoadedAssets = new ();           // 已加载的资源
        private readonly Dictionary<string, AssetLoadTask> m_LoadingAssets = new ();          // 正在异步加载的资源
        
        /// <summary>
        /// 资源根目录
        /// </summary>
        public string AssetPath => m_ABPath;
        /// <summary>
        /// 资源后缀名
        /// </summary>
        public string Variant => m_Variant;

        public AssetBundleLoadHelper(string abPath = "", string variant = "") : this(abPath, variant, "AssetManifest") {}

        public AssetBundleLoadHelper(string abPath, string variant, string assetManifestFileName)
        {
            m_ABPath = string.IsNullOrEmpty(abPath) ? Application.streamingAssetsPath + "/AssetBundles" : abPath;
            m_Variant = string.IsNullOrEmpty(variant) ? ".ab" : "." + variant;

            string assetManifestPath = $"{m_ABPath}/{assetManifestFileName}.json";
            AssetManifest assetManifest; 
            if (System.IO.File.Exists(assetManifestPath))
            {
                string json = System.IO.File.ReadAllText(assetManifestPath);
                assetManifest = JsonUtility.FromJson<AssetManifest>(json);
            }
            else
            {
                assetManifest = new AssetManifest
                {
                    dependencies = Array.Empty<SingleDependenciesData>(),
                    asset2Abs = Array.Empty<Asset2AB>()
                };
                Debug.LogWarning($"[AssetBundleHelper] 路径 {m_ABPath} 下无资源打包数据");
            }

            m_DependenceInfo = new DependenciesData(assetManifest.dependencies);
            foreach (var asset2Ab in assetManifest.asset2Abs)
            {
                if (!m_Asset2Ab.TryAdd(asset2Ab.assetPath, asset2Ab.abName))
                {
                    Debug.LogWarning($"[AssetBundleHelper] 资源 {asset2Ab.assetPath} 在资源打包数据中重复了");
                }
            }
        }
        
        #region 状态查询
        public bool IsAssetExist(string assetName)
        {
            return m_Asset2Ab.ContainsKey(assetName);
        }
        #endregion
        
        #region 资源加载/卸载
        /// <summary>
        /// 加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称（完整名称，加路径及后缀名）</param>
        /// <returns>资源</returns>
        public T Load<T>(string assetName) where T : UObject
        {
            var temp = Utility.Text.SplitPathName(assetName);
            return LoadAssetBundle(temp[0])?.LoadAsset<T>(temp[1]);
        }
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetName">资源名称（完整名称，加路径及后缀名）</param>
        /// <param name="callback">回调函数</param>
        /// <returns>加载进度</returns>

        public void LoadAsync(string assetPath, LoadAssetDelegate callback)
        {
            if (!IsAssetExist(assetPath))
            {
                callback.Invoke(false, null);
                return;
            }
            
            if (m_LoadedAssets.TryGetValue(assetPath, out AssetObject assetObject))
            {
                callback.Invoke(true, assetObject.asset);
                return;
            }

            if (m_LoadingAssets.TryGetValue(assetPath, out AssetLoadTask _assetLoadTask))
            {
                _assetLoadTask.callback += callback;
                return;
            }
            
            var assetLoadTask = new AssetLoadTask();
            assetLoadTask.callback += callback;
            assetLoadTask.assetObject = new AssetObject();
            assetLoadTask.assetObject.assetPath = assetPath;
            
            var abPath = GetAssetAb(assetPath);
            LoadAssetBundleAsyncNew(abPath, (success, assetBundle) =>
            {
                if (success)
                {
                    //加载自身  todo 这里可以加个数量限制，如果当前正在加载的数量超过限制，先放到等待队列里
                    var assetRequest = assetBundle.LoadAssetAsync(assetPath);
                    assetLoadTask.assetBundleRequest = assetRequest;
                    assetRequest.completed += (asyncOperation) =>
                    {
                        UpdateLoadingAsset();
                    };
                }
                else
                {
                    throw new Exception($"[AssetBundleLoadHelper] 加载AB包 {abPath} 失败, 需要实现相关逻辑");
                }
            });
            
            m_LoadingAssets[abPath] = assetLoadTask;
        }
        
        public void LoadAsync<T>(string assetPath, LoadAssetDelegate<T> callback) where T : UObject
        {
            LoadAsync(assetPath, (success, asset) =>
            {
                if (success)
                {
                    callback.Invoke(true, asset as T);
                }
                else
                {
                    callback.Invoke(false, null);
                }
            });
        }
        
        private void UpdateLoadingAsset()
        {
            var m_TempList = new List<string>();
            
            foreach (var item in m_LoadingAssets)
            {
                var loadTask = item.Value;
                
                if (loadTask.assetBundleRequest != null && loadTask.assetBundleRequest.isDone)
                {
                    m_TempList.Add(item.Key);
                }
            }
            
            foreach (var key in m_TempList)
            {
                // todo 封裝成OnAssetLoaded
                if(!m_LoadingAssets.Remove(key, out AssetLoadTask loadTask))
                {
                    throw new Exception($"[AssetBundleLoadHelper] 加载资源 {key} 时，未在正在加载的资源列表中找到对应项");
                }
                
                var success = loadTask.assetBundleRequest.asset != null;
                if (success)
                {
                    var assetObject = loadTask.assetObject;
                    assetObject.asset = loadTask.assetBundleRequest.asset;
                    m_LoadedAssets.Add(key, assetObject);
                    loadTask.callback.Invoke(success, assetObject.asset);
                }
                else
                {
                    throw new Exception($"[AssetBundleLoadHelper] 加载资源 {key} 失败, 需要实现相关逻辑");
                }
            }
        }

        /// <summary>
        /// 加载一个路径下的所有资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="isTopOnly">是否是仅加载本层级的资源</param>
        /// <returns></returns>
        // public T[] LoadAll<T>(string path, bool isTopOnly = true) where T : UObject
        // {
        //     if (isTopOnly)
        //     {
        //         return LoadAssetBundle(path).LoadAllAssets<T>();
        //     }
        //     else
        //     {
        //         var abs = LoadAssetBundles(path);
        //         List<T> assets = new List<T>();
        //         foreach (var item in abs)
        //         {
        //             assets.AddRange(item.LoadAllAssets().Convert<T>());
        //         }
        //         return assets.ToArray();
        //     }
        // }
        
        /// <summary>
        /// 同步加载一组资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径</param>
        /// <param name="isTopOnly">是否是仅加载本层级的资源</param>
        /// <returns>资源</returns>
        // public IProgress LoadAllSync<T>(string path, bool isTopOnly, Action<IList<T>> callback) where T : UObject
        // {
        //     if (isTopOnly)
        //     {
        //         return LoadAssetBundleAsync(path, (ab) =>
        //         {
        //             AssetBundleRequest request = ab.LoadAllAssetsAsync<T>();
        //             
        //             var task = XTask.WaitUntil(() => request.isDone);
        //             task.ContinueWith(() => { callback(request.allAssets.Convert<T>()); });
        //             task.Start();
        //         });
        //     }
        //     else
        //     {
        //         List<T> assets = new List<T>();
        //
        //         DynamicMultiProgress dynamicProgress = new DynamicMultiProgress(2);
        //         var assetBundlesProgress = LoadAssetBundlesAsync(path, OnAssetBundleLoadComplete);
        //
        //         return dynamicProgress;
        //
        //         void OnAssetBundleLoadComplete(IEnumerable<AssetBundle> assetBundles)
        //         {
        //             List<IProgress> progresses = new List<IProgress>();
        //             var startTask = XTask.WaitUntil(() => true);
        //             var currentEndTask = startTask;
        //             foreach (var ab in assetBundles)
        //             {
        //                 var request = ab.LoadAllAssetsAsync<T>();
        //                 var task = XTask.WaitUntil(() => request.isDone);
        //                 var endTask = task.ContinueWith(() => { assets.AddRange(request.allAssets.Convert<T>());});
        //                 progresses.Add(new AssetBundleRequestProgress(request));
        //
        //                 if(currentEndTask != null)
        //                 {
        //                     currentEndTask.ContinueWith(task);
        //                 }
        //                 currentEndTask = endTask;
        //             }
        //             dynamicProgress.Add(new MultiProgress(progresses.ToArray()));
        //
        //             currentEndTask.ContinueWith(() => { callback(assets); });
        //             startTask.Start();
        //         }
        //     }
        // }
        #endregion
        
        #region AB包加载/卸载
        private bool IsAbExist(string abName)
        {
            return m_DependenceInfo.IsAbExist(abName);
        }
        
        private string GetAssetAb(string assetPath)
        {
            return m_Asset2Ab[assetPath];
        }
        
        private bool AbIsLoaded(string abName)
        {
            return m_LoadedABNew.ContainsKey(abName);
        }

        /// <summary>
        /// 获取一个AB包文件
        /// </summary>
        /// <param name="path">相对路径</param>
        /// <returns></returns>
        private AssetBundle LoadAssetBundle(string path)
        {
            path = Path2Key(path);
            if (!m_LoadedAB.TryGetValue(path, out AssetBundle ab))
            {
                string fullName = Convert2FullPath(path);
                ab = AssetBundle.LoadFromFile(fullName);
                m_LoadedAB.Add(path, ab);

                //加载当前AB包的依赖包
                //string[] dependencies = m_Manifest.GetAllDependencies(ab.name);
                string[] dependencies = m_DependenceInfo.GetAllDependencies(ab.name);

                foreach (var item in dependencies)
                {
                    string key = GetNoVariantName(item);  // 对key去除.ab
                    if (!m_LoadedAB.ContainsKey(key))
                    {
                        LoadAssetBundle(key);
                    }
                }
            }

            return ab;
        }

        /// <summary>
        /// 异步获取AB包
        /// </summary>
        /// <param name="path"></param>

        private void LoadAssetBundleAsyncNew(string abPath, LoadAssetBundleDelegate callBack)
        {
            abPath = Path2Key(abPath);
            
            if (m_LoadedAB.TryGetValue(abPath, out AssetBundle _ab))
            {
                callBack.Invoke(true, _ab);
                return;
            }

            if (m_LoadingABNew.TryGetValue(abPath, out AssetBundleLoadTask abLoadTask))
            {
                abLoadTask.callback += callBack;
                return;
            }
            
            //加载依赖项
            foreach (var dependency in m_DependenceInfo.GetDirectDependencies(abPath))
            {
                LoadAssetBundleAsyncNew(dependency, (success, assetBundle) =>
                {
                    
                });
            }
            
            //加载自身  todo 这里可以加个数量限制，如果当前正在加载的数量超过限制，先放到等待队列里
            var abTask = new AssetBundleLoadTask();
            
            AssetBundleCreateRequest mainRequest = AssetBundle.LoadFromFileAsync(Convert2FullPath(abPath));
            abTask.assetBundleCreateRequest = mainRequest;
            abTask.callback += callBack;
            abTask.assetBundleObject = new AssetBundleObject();
            abTask.assetBundleObject.abPath = abPath;
            mainRequest.completed += (asyncOperation) =>
            {
                UpdateLoadingAb();
            };
            
            m_LoadingABNew[abPath] = abTask;
        }
        
        private void UpdateLoadingAb()
        {
            var m_TempList = new List<string>();
            
            foreach (var item in m_LoadingABNew)
            {
                var loadTask = item.Value;
                var dependencies = m_DependenceInfo.GetDirectDependencies(item.Key);
                var isAllDependencyLoaded = true;
                foreach (var dp in dependencies)              
                {
                    if (!AbIsLoaded(dp))              
                    {
                        isAllDependencyLoaded = false;
                        break;
                    }
                }
                
                if (loadTask.assetBundleCreateRequest.isDone && isAllDependencyLoaded)
                {
                    m_TempList.Add(item.Key);
                }
            }

            foreach (var key in m_TempList)
            {
                // todo 封裝成OnAbLoaded
                if(!m_LoadingABNew.Remove(key, out AssetBundleLoadTask loadTask))
                {
                    throw new Exception($"[AssetBundleLoadHelper] 加载AB包 {key} 时，未在正在加载的AB包列表中找到对应项");
                }
                
                var success = loadTask.assetBundleCreateRequest.assetBundle != null;
                if (success)
                {
                    var abObject = loadTask.assetBundleObject;
                    abObject.assetBundle = loadTask.assetBundleCreateRequest.assetBundle;
                    m_LoadedABNew.Add(key, abObject);
                    loadTask.callback.Invoke(success, abObject.assetBundle);
                }
                else
                {
                    throw new Exception($"[AssetBundleLoadHelper] 加载AB包 {key} 失败, 需要实现相关逻辑");
                }
            }
        }


        /// <summary>
        /// 获取一组ab包
        /// </summary>
        /// <param name="path"></param>
        // private IEnumerable<AssetBundle> LoadAssetBundles(string path)
        // {
        //     List<AssetBundle> assetBundles = new List<AssetBundle>();
        //
        //     path = Path2Key(path);
        //
        //     var topAb = LoadAssetBundle(path);
        //     if (topAb != null)
        //     {
        //         assetBundles.Add(topAb);
        //     }
        //
        //     string dirPath = m_ABPath + "/" + path;
        //     var subDirectory = new System.IO.DirectoryInfo(dirPath);
        //     if (subDirectory.Exists)
        //     {
        //         var abFiles = subDirectory.GetFiles($"*{m_Variant}", System.IO.SearchOption.AllDirectories);
        //         int startIndex = m_ABPath.Length + 1;
        //
        //         foreach (var item in abFiles)
        //         {
        //             var abPath = item.FullName.Substring(startIndex);
        //             var ab = LoadAssetBundle(abPath);
        //             if (ab != null)
        //             {
        //                 assetBundles.Add(ab);
        //             }
        //         }
        //     }
        //
        //     return assetBundles;
        // }

        /// <summary>
        /// 异步获取一组ab包
        /// </summary>
        /// <param name="path"></param>
        /// <param name="callback"></param>
        // private IProgress LoadAssetBundlesAsync(string path, Action<IEnumerable<AssetBundle>> callback)
        // {
        //     List<IProgress> progresses = new List<IProgress>();
        //
        //     path = Path2Key(path);
        //
        //     List<AssetBundle> assetBundles = new List<AssetBundle>();
        //
        //     // top层
        //     var abProgress = LoadAssetBundleAsync(path, (ab) =>
        //     {
        //         assetBundles.Add(ab);
        //     });
        //     progresses.Add(abProgress);
        //     int abCount = 1;
        //
        //     // 其它层
        //     string directoryPath = m_ABPath + "/" + path;
        //     var subDirectory = new System.IO.DirectoryInfo(directoryPath);
        //     if (subDirectory.Exists)
        //     {
        //         var abFiles = subDirectory.GetFiles($"*{m_Variant}", System.IO.SearchOption.AllDirectories);
        //
        //         int startIndex = m_ABPath.Length + 1;
        //         abCount += abFiles.Length;
        //         foreach (var item in abFiles)
        //         {
        //             var abPath = item.FullName.Substring(startIndex);
        //
        //             var progress = LoadAssetBundleAsync(abPath, (ab) =>
        //             {
        //                 assetBundles.Add(ab);
        //             });
        //             progresses.Add(abProgress);
        //         }
        //     }
        //
        //     var singleTask = XTask.WaitUntil(() => assetBundles.Count == abCount);
        //     singleTask.ContinueWith(() => { callback(assetBundles); });
        //     singleTask.Start();
        //
        //     return new MultiProgress(progresses.ToArray());
        // }

        #endregion
        
        private string GetNoVariantName(string abName)
        {
            return abName[..^m_Variant.Length];
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
        private void UnLoad(string path, bool unLoadAllObjects = true)
        {
            LoadAssetBundle(path).Unload(unLoadAllObjects);
            m_LoadedAB.Remove(path);
        }

        /// <summary>
        /// 卸载对应ab包
        /// </summary>
        /// <param name="name">ab包名称</param>
        private void UnLoad(string name)
        {
            m_LoadedAB.TryGetValue(name, out AssetBundle ab);
            if (ab != null)
            {
                ab.Unload(true);
                m_LoadedAB.Remove(name);
            }
        }

        public void Release(UObject obj)
        {
            
        }

        /// <summary>
        /// 卸载所有ab包
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var item in m_LoadedAB.Values)
            {
                item.Unload(true);
            }
            foreach (var item in m_LoadingAB.Values)
            {
                item.assetBundle.Unload(true);
            }
            m_LoadedAB.Clear();
        }

        public void OnUpdate()
        {
            UpdateLoadingAb();
            // var a = m_LoadedABNew;
            // var b = m_LoadingABNew;
            // var c = 1;
        }
    }
}