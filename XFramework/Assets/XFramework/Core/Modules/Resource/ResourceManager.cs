#define ABTests
#if!UNITY_EDITOR || ABTest
#define AB
#endif

using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XFramework
{
    public partial class ResourceManager : IGameModule
    {
        private readonly string ABPath = Application.streamingAssetsPath + "/AssetBundles";

        /// <summary>
        /// AB包的缓存
        /// </summary>
        private Dictionary<string, AssetBundle> m_ABDic;
        /// <summary>
        /// 用于获取AB包的依赖关系
        /// </summary>
        private AssetBundleManifest m_Mainfest;

        private TaskPool m_TaskPool;

        public ResourceManager()
        {
            m_TaskPool = new TaskPool();
#if AB
            m_ABDic = new Dictionary<string, AssetBundle>();

            AssetBundle m_MainfestAB = AssetBundle.LoadFromFile(ABPath + "/AssetBundles");
            if (m_MainfestAB != null)
                m_Mainfest = m_MainfestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
#endif
        }

        #region 资源加载

        /// <summary>
        /// 同步加载资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T Load<T>(string path) where T : Object
        {
            
            if (path.Substring(0, 4) == "Res/")
            {
                path = path.Substring(4, path.Length - 4);
                return Resources.Load<T>(path);
            }
#if AB
            var temp = Utility.Text.SplitPathName(path);
            return GetAssetBundle(temp[0]).LoadAsset<T>(temp[1]);
#else
            return LoadWithADB<T>(path);
#endif
        }

        /// <summary>
        /// 加载一个路径下的所有资源
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <returns></returns>
        public T[] LoadAll<T>(string path) where T : Object
        {
            if (path.Substring(0, 4) == "Res/")
            {
                path = path.Substring(4, path.Length - 4);
                return Resources.LoadAll<T>(path);
            }

#if AB
            return GetAssetBundle(path).LoadAllAssets<T>();
#else
            return LoadAllWithADB<T>(path);
#endif
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        /// <param name="callBack"></param>
        public void LoadAsync<T>(string path, System.Action<T> callBack) where T : Object
        {
            if (path.Substring(0, 4) == "Res/")
            {
                path = path.Substring(4, path.Length - 4);
                ResLoadTask<T> resTask = new ResLoadTask<T>(Resources.LoadAsync(path), callBack);
                m_TaskPool.Add(resTask);
            }
#if AB
            var temp = Utility.Text.SplitPathName(path);

            // 同步加载AB包
            //ABLoadTask<T> abTask = new ABLoadTask<T>(GetAssetBundle(temp[0]).LoadAssetAsync(temp[1]), callBack);
            //m_TaskPool.Add(abTask);

            // 异步加载AB包
            GetAssetBundleAsync(temp[0], (ab) =>
            {
                ABLoadTask<T> abTask = new ABLoadTask<T>(ab.LoadAssetAsync(temp[1]), callBack);
                m_TaskPool.Add(abTask);
            });
#else
            T obj = LoadWithADB<T>(path);
            callBack(obj);
#endif
        }

        #endregion

        #region AB包加载

        /// <summary>
        /// 获取一个AB包文件
        /// </summary>
        /// <param name="path">相对路径</param>
        /// <returns></returns>
        public AssetBundle GetAssetBundle(string path)
        {
            m_ABDic.TryGetValue(path, out AssetBundle ab);
            if (ab == null)
            {
                path = path.ToLower();
                string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;

                ab = AssetBundle.LoadFromFile(ABPath + abName + ".ab");
                if (ab == null)
                    Debug.LogError(path + " 为空");
                m_ABDic.Add(path, ab);
            }

            //加载当前AB包的依赖包
            string[] dependencies = m_Mainfest.GetAllDependencies(ab.name);

            foreach (var item in dependencies)
            {
                string key = Name2Key(item);  // 对key去除.ab
                if (!m_ABDic.ContainsKey(key))
                {
                    AssetBundle dependAb = GetAssetBundle(key);
                    m_ABDic.Add(key, dependAb);
                }
            }

            return ab;
        }

        /// <summary>
        /// 异步获取AB包
        /// </summary>
        /// <param name="path"></param>
        public void GetAssetBundleAsync(string path, System.Action<AssetBundle> callBack)
        {
            m_ABDic.TryGetValue(path, out AssetBundle ab);
            if (ab == null)
            {
                path = path.ToLower();
                string abName = string.IsNullOrEmpty(path) ? "" : "/" + path;

                // 添加任务列表
                List<AssetBundleCreateRequest> requests = new List<AssetBundleCreateRequest>();
                requests.Add(AssetBundle.LoadFromFileAsync(ABPath + abName + ".ab"));
                string[] dependencies = m_Mainfest.GetAllDependencies(path + ".ab");
                foreach (var name in dependencies)
                {
                    if (m_ABDic.ContainsKey(Name2Key(name)))
                        continue;
                    requests.Add(AssetBundle.LoadFromFileAsync(ABPath + "/" + name));
                }

                LoadDependenciesTask task = new LoadDependenciesTask(requests, (a)=> 
                {
                    m_ABDic.Add(Name2Key(a.name), a);
                },callBack);

                m_TaskPool.Add(task);
            }
            else
            {
                callBack(ab);
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


#if !AB

        private T LoadWithADB<T>(string path) where T : Object
        {
            string suffix = ""; // 后缀
            switch (typeof(T).Name)
            {
                case "GameObject":
                    suffix = ".prefab";
                    break;
                case "Material":
                    suffix = ".mat";
                    break;
                case "Texture":
                    return LoadTexture<T>("Assets/ResourcesAB/" + path);
                case "Texture2D":
                    return LoadTexture<T>("Assets/ResourcesAB/" + path);
            }
            return AssetDatabase.LoadAssetAtPath<T>("Assets/ResourcesAB/" + path + suffix);
        }

        /// <summary>
        /// 在没有后缀的情况下加载贴图
        /// </summary>
        /// <typeparam name="T">Texture,Texture2D</typeparam>
        private T LoadTexture<T>(string path) where T : Object
        {
            T texture;
            texture = AssetDatabase.LoadAssetAtPath<T>(path + ".png");
            if (!texture)
                texture = AssetDatabase.LoadAssetAtPath<T>(path + "jpg");

            return texture;
        }

        private T[] LoadAllWithADB<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).Convert<T>();
        }

#endif

        #region 接口实现

        public int Priority { get { return 100; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            m_TaskPool.Update();
        }

        public void Shutdown()
        {
            m_ABDic?.Clear();
            m_Mainfest = null;
        }

        #endregion
    }
}