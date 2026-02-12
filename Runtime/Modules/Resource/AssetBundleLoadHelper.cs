using System;
using System.Collections.Generic;
using XFramework.Tasks;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace XFramework.Resource
{
    public delegate void LoadAssetBundleDelegate(bool success, AssetBundle assetBundle);
    
    internal class AssetObject
    {
        public string assetPath;
        public UObject asset;
        // public int refCount;
    }
    
    internal class AssetLoadTask
    {
        public AssetObject assetObject;
        public AssetBundleRequest assetBundleRequest;
        public LoadAssetDelegate callback;
    }
    
    internal class AssetBundleObject
    {
        public string abPath;
        public AssetBundle assetBundle;
        // public int refCount;
    }

    internal class AssetBundleLoadTask
    {
        public AssetBundleCreateRequest assetBundleCreateRequest; 
        public AssetBundleObject assetBundleObject;
        public LoadAssetBundleDelegate callback;
        
        public string abPath => assetBundleObject.abPath;
    }

    
    
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
        
        private readonly Dictionary<string, AssetBundleObject> m_LoadedAB = new();          // 已加载的AB包
        private readonly Dictionary<string, AssetBundleLoadTask> m_LoadingAB = new ();      // 正在异步加载中的ab包
        private readonly Dictionary<string, AssetBundleObject> m_UnLoadAB = new ();         // 待卸载的AB包
        
        private readonly Dictionary<string, AssetObject> m_LoadedAssets = new ();           // 已加载的资源
        private readonly Dictionary<string, AssetLoadTask> m_LoadingAssets = new ();        // 正在异步加载的资源
        
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
        public T Load<T>(string assetPath) where T : UObject
        {
            if (!IsAssetExist(assetPath))
            {
                Debug.LogError($"[AssetBundleLoadHelper] 资源 {assetPath} 不存在");
                return null;
            }
            
            if (m_LoadedAssets.TryGetValue(assetPath, out AssetObject _assetObject))
            {
                return _assetObject.asset as T;
            }
            
            var abPath = Asset2Ab(assetPath);
            var assetBundle = LoadAssetBundle(abPath);
            if (assetBundle == null)
            {
                return null;
            }
            var asset = assetBundle?.LoadAsset(assetPath);
            if (asset == null)
            {
                Debug.LogError($"[AssetBundleLoadHelper] 资源 {assetPath} 在AB包 {abPath} 中未找到");
                return null;
            }
            
            var assetObject = new AssetObject();
            assetObject.asset = asset;
            assetObject.assetPath = assetPath;
            m_LoadedAssets.Add(assetPath, assetObject);
            return asset as T;
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="assetPath">资源名称（完整名称，加路径及后缀名）</param>
        /// <param name="callback">回调函数</param>
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
            
            var abPath = Asset2Ab(assetPath);
            LoadAssetBundleAsync(abPath, (success, assetBundle) =>
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
            
            m_LoadingAssets[assetPath] = assetLoadTask;
        }
        
        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="assetPath">资源名称（完整名称，加路径及后缀名）</param>
        /// <param name="callback">回调函数</param>
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
        
        public void Release(UObject obj)
        {
            // todo 资源卸载逻辑， 待实现
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
        
        #endregion
        
        #region AB包加载/卸载
        private bool IsAbExist(string abName)
        {
            return m_DependenceInfo.IsAbExist(abName);
        }
        
        private string Asset2Ab(string assetPath)
        {
            return m_Asset2Ab[assetPath];
        }
        
        private bool AbIsLoaded(string abName)
        {
            return m_LoadedAB.ContainsKey(abName);
        }

        private bool AssetIsLoaded(string assetPath)
        {
            return m_LoadedAssets.ContainsKey(assetPath);
        }

        /// <summary>
        /// 获取一个AB包文件
        /// </summary>
        /// <param name="path">相对路径</param>
        /// <returns></returns>
        private AssetBundle LoadAssetBundle(string abPath)
        {
            abPath = GetUnityAbPath(abPath);
            if (!IsAbExist(abPath))
            {
                Debug.LogError($"[AssetBundleLoadHelper] AB包 {abPath} 不存在");
                return null;
            }
            
            if (m_LoadedAB.TryGetValue(abPath, out AssetBundleObject _abObject))
            {
                return _abObject.assetBundle;
            }
            
            // 正在异步加载的，转同步加载
            if (m_LoadingAB.TryGetValue(abPath, out AssetBundleLoadTask abLoadTask))
            {
                // 先把依赖项也转同步
                foreach (var dependency in m_DependenceInfo.GetDirectDependencies(abPath))
                {
                    LoadAssetBundle(dependency);
                }
                
                abLoadTask.assetBundleObject.assetBundle = abLoadTask.assetBundleCreateRequest.assetBundle;  // 异步转同步
                return abLoadTask.assetBundleObject.assetBundle;
            }
            
            
            // 先加载依赖项
            foreach (var dependency in m_DependenceInfo.GetDirectDependencies(abPath))
            {
                LoadAssetBundle(dependency);
            }
            
            // 加载自身
            var assetBundle = AssetBundle.LoadFromFile(GetAbAbsolutePath(abPath));
            if (assetBundle == null)
            {
                throw new Exception($"[AssetBundleLoadHelper] 加载AB包 {abPath} 失败, 需要实现相关逻辑");
            }
            
            var abObject = new AssetBundleObject();
            abObject.abPath = abPath;
            abObject.assetBundle = assetBundle;
            DoAssetBundleLoaded(abObject);
            return abObject.assetBundle;
        }

        /// <summary>
        /// 异步获取AB包
        /// </summary>
        /// <param name="path"></param>

        private void LoadAssetBundleAsync(string abPath, LoadAssetBundleDelegate callBack)
        {
            abPath = GetUnityAbPath(abPath);
            
            if (!IsAbExist(abPath))
            {
                Debug.LogError($"[AssetBundleLoadHelper] AB包 {abPath} 不存在");
                callBack.Invoke(false, null);
                return;
            }
            
            if (m_LoadedAB.TryGetValue(abPath, out AssetBundleObject abObject))
            {
                callBack.Invoke(true, abObject.assetBundle);
                return;
            }

            if (m_LoadingAB.TryGetValue(abPath, out AssetBundleLoadTask abLoadTask))
            {
                abLoadTask.callback += callBack;
                return;
            }
            
            //加载依赖项
            foreach (var dependency in m_DependenceInfo.GetDirectDependencies(abPath))
            {
                LoadAssetBundleAsync(dependency, (success, assetBundle) =>
                {
                    
                });
            }
            
            //加载自身  todo 这里可以加个数量限制，如果当前正在加载的数量超过限制，先放到等待队列里
            var abTask = new AssetBundleLoadTask();
            
            AssetBundleCreateRequest mainRequest = AssetBundle.LoadFromFileAsync(GetAbAbsolutePath(abPath));
            abTask.assetBundleCreateRequest = mainRequest;
            abTask.callback += callBack;
            abTask.assetBundleObject = new AssetBundleObject();
            abTask.assetBundleObject.abPath = abPath;
            mainRequest.completed += (asyncOperation) =>
            {
                UpdateLoadingAb();
            };
            
            m_LoadingAB[abPath] = abTask;
        }
        
        private void UpdateLoadingAb()
        {
            var m_TempList = new List<string>();
            
            foreach (var item in m_LoadingAB)
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
                DoAssetBundleLoaded(m_LoadingAB[key]);
            }
        }
        
        private void DoAssetBundleLoaded(AssetBundleLoadTask loadTask)
        {
            if(!m_LoadingAB.Remove(loadTask.abPath))
            {
                throw new Exception($"[AssetBundleLoadHelper] 加载AB包 {loadTask.abPath} 时，未在正在加载的AB包列表中找到对应项");
            }
                
            var success = loadTask.assetBundleCreateRequest.assetBundle != null;
            if (success)
            {
                var abObject = loadTask.assetBundleObject;
                abObject.assetBundle = loadTask.assetBundleCreateRequest.assetBundle;
                m_LoadedAB.Add(abObject.abPath, abObject);
                loadTask.callback.Invoke(success, abObject.assetBundle);
            }
            else
            {
                throw new Exception($"[AssetBundleLoadHelper] 加载AB包 {loadTask.abPath} 失败, 需要实现相关逻辑");
            }
        }

        private void DoAssetBundleLoaded(AssetBundleObject assetBundleObject)
        {
            if (!m_LoadedAB.TryAdd(assetBundleObject.abPath, assetBundleObject))
            {
                throw new Exception($"[AssetBundleLoadHelper] AB包 {assetBundleObject.abPath} 已存在于已加载的AB包列表中");
            }
        }

        /// <summary>
        /// 卸载AB包
        /// </summary>
        /// <param name="path"></param>
        /// <param name="unLoadAllObjects"></param>
        private void UnLoad(string abPath, bool unLoadAllObjects)
        {
            if (!IsAbExist(abPath))
            {
                Debug.LogError($"[AssetBundleLoadHelper] AB包 {abPath} 不存在");
                return;
            }
            m_LoadedAB[abPath].assetBundle.Unload(unLoadAllObjects);
            m_LoadedAB.Remove(abPath);
        }
        
        #endregion


        #region Obsolete
        [Obsolete]
        private IEnumerable<AssetBundle> LoadAssetBundles(string path)
        {
            List<AssetBundle> assetBundles = new List<AssetBundle>();
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
                    var abPath = item.FullName[startIndex..];
                    var ab = LoadAssetBundle(abPath);
                    if (ab !=null)
                    {
                        assetBundles.Add(ab);
                    }
                }
            }
        
            return assetBundles;
        }
        
        [Obsolete]
        private T[] LoadAll<T>(string path, bool isTopOnly = true) where T : UObject
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
        #endregion
        
        // 将路径转换成Unity资源路径的格式（小写，斜杠分隔）
        private string GetUnityAbPath(string path)
        {
            path = path.ToLower();
            return path.Replace('\\', '/');
        }
        
        // 获取AB包在磁盘上的绝对路径
        private string GetAbAbsolutePath(string path)
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
        /// 卸载所有ab包
        /// </summary>
        public void ReleaseAll()
        {
            foreach (var item in m_LoadedAB.Values)
            {
                item.assetBundle.Unload(true);
            }
            foreach (var item in m_LoadingAB.Values)
            {
                item.assetBundleCreateRequest.assetBundle.Unload(true);
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