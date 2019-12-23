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

        public AssetBundleLoadHelper(string abPath = "", string variant = "") :this(abPath,variant,"depenencies")
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

        public T Load<T>(string assetName) where T : Object
        {
            var temp = Utility.Text.SplitPathName(assetName);
            return GetAssetBundle(temp[0])?.LoadAsset<T>(temp[1]);
        }

        public T[] LoadAll<T>(string path) where T : Object
        {
            return GetAssetBundle(path).LoadAllAssets<T>();
        }

        public IProgress LoadAsync<T>(string assetName, System.Action<T> callback) where T : Object
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
                GameEntry.GetModule<TaskManager>().StartTask(task);

                SingleResProgress resProgress = new SingleResProgress(request);
                progress.Add(resProgress);
            });
            progress.Add(abProgress);

            return progress;
        }


        #region AB包加载

        /// <summary>
        /// 获取一个AB包文件
        /// </summary>
        /// <param name="path">相对路径</param>
        /// <returns></returns>
        public AssetBundle GetAssetBundle(string path)
        {
            path = Path2Key(path);
            path = path.Replace('\\', '/');
            if (!m_ABDic.TryGetValue(path, out AssetBundle ab))
            {
                string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;
                ab = AssetBundle.LoadFromFile(m_ABPath + abName + m_Variant);

                m_ABDic.Add(path, ab);
            }

            //加载当前AB包的依赖包
            //string[] dependencies = m_Mainfest.GetAllDependencies(ab.name);
            string[] dependencies = m_DependenceInfo.GetAllDependencies(ab.name);

            foreach (var item in dependencies)
            {
                string key = Name2Key(item);  // 对key去除.ab
                if (!m_ABDic.ContainsKey(key))
                {
                    AssetBundle dependAb = GetAssetBundle(key);
                }
            }

            return ab;
        }

        /// <summary>
        /// 异步获取AB包
        /// </summary>
        /// <param name="path"></param>
        private IProgress GetAssetBundleAsync(string path, System.Action<AssetBundle> callBack)
        {
            path = Path2Key(path);
            path = path.Replace('\\', '/');
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
                GameEntry.GetModule<TaskManager>().StartTask(abTask);

                return new ResProgress(requests.ToArray());
            }
            else
            {
                callBack(ab);
                return new DefaultProgress();
            }
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

        #endregion

        private string Name2Key(string abName)
        {
            return abName.Substring(0, abName.Length - (m_Variant.Length));
        }

        private string Path2Key(string path)
        {
            return path.ToLower();
        }

        public void UnLoad(string name)
        {
            m_ABDic.TryGetValue(name, out AssetBundle ab);
            if (ab != null)
            {
                ab.Unload(true);
                m_ABDic.Remove(name);
            }
        }

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