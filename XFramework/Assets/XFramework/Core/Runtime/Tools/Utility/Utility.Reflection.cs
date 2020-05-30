using System;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework
{
    public static partial class Utility
    {
        /// <summary>
        /// 反射相关的工具
        /// </summary>
        public static class Reflection
        {
            /// <summary>
            /// 创建一个对象
            /// </summary>
            /// <typeparam name="T">类型</typeparam>
            /// <param name="objs">参数</param>
            public static T CreateInstance<T>(params object[] objs) where T : class
            {
                T instance;
                if (objs != null)
                    instance = Activator.CreateInstance(typeof(T), objs) as T;
                else
                    instance = Activator.CreateInstance(typeof(T)) as T;
                return instance;
            }

            /// <summary>
            /// 创建一个对象
            /// </summary>
            /// <typeparam name="T">对象类型</typeparam>
            /// <param name="type">类型</param>
            /// <param name="objs">参数数组</param>
            /// <returns></returns>
            public static T CreateInstance<T>(Type type, params object[] objs) where T : class
            {
                T instance;
                if (objs != null)
                    instance = Activator.CreateInstance(type, objs) as T;
                else
                    instance = Activator.CreateInstance(type) as T;
                return instance;
            }

            /// <summary>
            /// 获取一个类型的所有派生类
            /// </summary>
            /// <param name="typeBase">基类型</param>
            /// <param name="assemblyName">程序集</param>
            /// <returns></returns>
            public static List<Type> GetSonClass(Type typeBase, string assemblyName = "Assembly-CSharp")
            {
                List<Type> types = new List<Type>();
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly == null)
                {
                    throw new System.Exception("没有找到程序集");
                }

                Type[] allType = assembly.GetTypes();
                foreach (Type type in allType)
                {
                    if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeBase))
                    {
                        types.Add(type);
                    }
                }
                return types;
            }

            /// <summary>
            /// 判断一个类是否继承自一个泛型类型
            /// </summary>
            /// <param name="sonType">子类类型</param>
            /// <param name="genericType">泛型类型</param>
            /// <returns></returns>
            public static bool IsSubclassOfGeneric(Type sonType, Type genericType)
            {
                while (sonType != null && sonType != typeof(object))
                {
                    var cur = sonType.IsGenericType ? sonType.GetGenericTypeDefinition() : sonType;
                    if (genericType == cur)
                    {
                        return true;
                    }
                    sonType = sonType.BaseType;
                }
                return false;
            }
        }
    }
}