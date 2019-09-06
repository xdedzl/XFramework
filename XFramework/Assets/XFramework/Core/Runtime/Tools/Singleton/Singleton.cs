using System;
using System.Reflection;


namespace XFramework.Singleton
{
    /// <summary>
    /// 不继承mono的单例基类，如果需要Update，可以将方法注册进MonoEvent的事件中
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Singleton<T> where T : Singleton<T>
    {
        private static T _instance;
        private static readonly object objlock = new object();

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (objlock)
                    {
                        if (_instance == null)
                        {
                            ConstructorInfo[] ctors = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
                            ConstructorInfo ctor = Array.Find(ctors, c => c.GetParameters().Length == 0);

                            if (ctor == null)
                            {
                                _instance = Activator.CreateInstance(typeof(T)) as T;
                                UnityEngine.Debug.LogError("Make the constructor private");
                            }

                            else
                                _instance = ctor.Invoke(null) as T;
                        }
                    }
                }
                return _instance;
            }
        }
    }

    public static class SingletonCreator
    {
        public static T CreatSingleton<T>() where T : class
        {
            T instance = Activator.CreateInstance(typeof(T)) as T;
            return instance;
        }
    }
}