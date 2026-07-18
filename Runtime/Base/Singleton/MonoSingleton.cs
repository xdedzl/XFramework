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

        static MonoSingleton()
        {
            s_ApplicationIsQuitting = false;
        }

        public static bool IsValid => _instance != null;

        public static T Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    if (!typeof(T).IsDefined(typeof(ExecuteInEditMode), true) &&
                        !typeof(T).IsDefined(typeof(ExecuteAlways), true))
                    {
                        return null;
                    }
                }
#endif
                
                if (_instance == null)
                {
                    // 场景中找不到就创建
                    if (_instance == null)
                    {
                        // 应对关闭DomainReload后
                        s_ApplicationIsQuitting = false;
                    #if UNITY_EDITOR // UNITY_EDITOR会发生Domain Reload，要先查场景中的
                        _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
                        if (_instance != null)
                        {
                            return _instance;
                        }
                        
                    #elif DEVELOPMENT_BUILD
                        var objects = Resources.FindObjectsOfTypeAll<T>();
                        Debug.Assert(objects == null || objects.Length == 0);
                    #endif
                        
                        var singleton = new GameObject();
                        _instance = singleton.AddComponent<T>();
                        singleton.name = "(singleton) " + typeof(T);

                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(singleton);
                        }
                        else
                        {
                            singleton.hideFlags = HideFlags.HideAndDontSave;
                        }
                    }
                }

                return _instance;
            }
        }

        // ReSharper disable once StaticMemberInGenericType
        private static bool s_ApplicationIsQuitting;

        /// <summary>
        /// 当工程运行结束，在退出时机时候，不允许访问单例
        /// </summary>
        //public void OnApplicationQuit()
        //{
        //}

        public void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;
            s_ApplicationIsQuitting = true;
        }
        
        public static bool IsDestroy()
        {
            return s_ApplicationIsQuitting;
        }
    }
}