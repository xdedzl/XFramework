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
        private readonly string m_ABPath = Application.streamingAssetsPath + "/AssetBundles";
        /// <summary>
        /// AB包的缓存
        /// </summary>
        private Dictionary<string, AssetBundle> m_ABDic;
        /// <summary>
        /// 用于获取AB包的依赖关系
        /// </summary>
        //private AssetBundleManifest m_Mainfest;

        private DependenciesData dependenceInfo;

        public string AssetPath => m_ABPath;

        public AssetBundleLoadHelper()
        {
            m_ABDic = new Dictionary<string, AssetBundle>();

            //AssetBundle m_MainfestAB = AssetBundle.LoadFromFile(m_ABPath + "/AssetBundles");
            //if (m_MainfestAB != null)
            //    m_Mainfest = m_MainfestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            string dependencePath = m_ABPath + "/depenencies.json";
            if (System.IO.File.Exists(dependencePath))
            {
                string json = System.IO.File.ReadAllText(dependencePath);
                dependenceInfo = JsonUtility.FromJson<DependenciesData>(json);
            }
            else
            {
                throw new FrameworkException("[Resource]AB包依赖关系文件丢失");
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

                ab = AssetBundle.LoadFromFile(m_ABPath + abName + ".ab");
                if (ab == null)
                    Debug.LogError(path + " 为空");
                m_ABDic.Add(path, ab);
            }

            //加载当前AB包的依赖包
            //string[] dependencies = m_Mainfest.GetAllDependencies(ab.name);
            string[] dependencies = dependenceInfo.GetAllDependencies(ab.name);

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
                string[] dependencies = dependenceInfo.GetAllDependencies(path + ".ab");
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
            return abName.Substring(0, abName.Length - 3);
        }

        private string Path2Key(string path)
        {
            return path.ToLower();
        }
    }
}