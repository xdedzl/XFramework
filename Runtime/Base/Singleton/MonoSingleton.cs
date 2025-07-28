using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 此单例继承于Mono，不需要手动创建
    /// </summary>
    public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;

        private static readonly object _lock = new object();

        protected bool isGlobal = true; // 是否是全局单例

        static MonoSingleton()
        {
            ApplicationIsQuitting = false;
        }

        public static bool IsValid { get { return _instance != null; } }

        public static T Instance
        {
            get
            {
                if (ApplicationIsQuitting)
                {
                    if (Debug.isDebugBuild)
                    {
                        Debug.LogWarning("[Singleton] Instance '" + typeof(T) +
                                                "' already destroyed on application quit." +
                                                " Won't create again - returning null.");
                    }

                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 先在场景中找寻这个单例
                        _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);

                        if (FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 1)
                        {
                            if (Debug.isDebugBuild)
                            {
                                Debug.LogWarning("[Singleton] " + typeof(T) +
                                                        " - there should never be more than 1 singleton!");
                            }

                            return _instance;
                        }

                        // 场景中找不到就创建
                        if (_instance == null)
                        {
                            var singleton = new GameObject();
                            _instance = singleton.AddComponent<T>();
                            singleton.name = "(singleton) " + typeof(T);

                            if (_instance.isGlobal && Application.isPlaying)
                            {
                                DontDestroyOnLoad(singleton);
                            }
                        }
                    }

                    return _instance;
                }
            }
        }

        protected static bool ApplicationIsQuitting { get; private set; }

        /// <summary>
        /// 当工程运行结束，在退出时机时候，不允许访问单例
        /// </summary>
        //public void OnApplicationQuit()
        //{
        //}

        public void OnDestroy()
        {
            ApplicationIsQuitting = true;
        }
    }
}