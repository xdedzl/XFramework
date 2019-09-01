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

        public string AssetPath => m_ABPath;

        public AssetBundleLoadHelper(string abPath = "", string variant = "")
        {
            m_ABPath = string.IsNullOrEmpty(abPath) ? Application.streamingAssetsPath + "/AssetBundles" : abPath;
            m_Variant = string.IsNullOrEmpty(variant) ? ".ab" : "." + variant;

            m_ABDic = new Dictionary<string, AssetBundle>();

            string dependencePath = m_ABPath + "/depenencies.json";
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
            return GetAssetBundle(temp[0]).LoadAsset<T>(temp[1]);
        }

        public T[] LoadAll<T>(string path) where T : Object
        {
            return GetAssetBundle(path).LoadAllAssets<T>();
        }

        public IProgress LoadAsync<T>(string assetName, System.Action<T> callback) where T : Object
        {
            var temp = Utility.Text.SplitPathName(assetName);

            return GetAssetBundleAsync(temp[0], (ab) =>
            {
                var request = ab.LoadAssetAsync(temp[1]);
                SingleTask task = new SingleTask(() => { return request.isDone; });
                task.Then(() => { callback(request.asset as T); return true; });
                GameEntry.GetModule<TaskManager>().StartTask(task);
            });
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

            if(!m_ABDic.TryGetValue(path, out AssetBundle ab))
            {
                string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;

                ab = AssetBundle.LoadFromFile(m_ABPath + abName + m_Variant);
                if (ab == null)
                    Debug.LogError(path + " 为空");
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
            if(!m_ABDic.TryGetValue(path, out AssetBundle ab))
            {
                string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;

                AssetBundleCreateRequest mainRequest = AssetBundle.LoadFromFileAsync(m_ABPath + abName + ".ab");
                // 添加任务列表
                List<AssetBundleCreateRequest> requests = new List<AssetBundleCreateRequest>
                {
                    mainRequest
                };

                //string[] dependencies = m_Mainfest.GetAllDependencies(path + ".ab");
                string[] dependencies = m_DependenceInfo.GetAllDependencies(path + ".ab");
                foreach (var name in dependencies)
                {
                    if (m_ABDic.ContainsKey(Name2Key(name)))
                        continue;
                    requests.Add(AssetBundle.LoadFromFileAsync(m_ABPath + "/" + name));
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
                        m_ABDic.Add(Name2Key(requests[index].assetBundle.name), requests[index].assetBundle);
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
                return null;
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
            return abName.Substring(0, abName.Length - (m_Variant.Length + 1));
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
            m_ABDic.Clear();
        }
    }
}