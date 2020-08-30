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
            public static IEnumerable<Type> GetSonClass(Type typeBase, string assemblyName = "Assembly-CSharp")
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
            /// 获取所有实现了typeBase的Type
            /// </summary>
            /// <param name="typeBase">基类或者接口</param>
            /// <param name="assemblyName"></param>
            /// <returns></returns>
            public static IEnumerable<Type> GetAssignableTypes(Type typeBase, string assemblyName = "Assembly-CSharp")
            {
                List<Type> typeNames = new List<Type>();
                Assembly assembly;
                try
                {
                    assembly = Assembly.Load(assemblyName);
                }
                catch
                {
                    return new Type[0];
                }

                if (assembly == null)
                {
                    return new Type[0];
                }

                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if ((type.IsClass || type.IsValueType) && !type.IsAbstract && typeBase.IsAssignableFrom(type))
                    {
                        typeNames.Add(type);
                    }
                }

                return typeNames;
            }

            /// <summary>
            /// 从当前程序域的所有程序集中获取所有类型
            /// </summary>
            /// <returns>所有类型集合</returns>
            public static List<Type> GetTypesInAllAssemblies()
            {
                List<Type> types = new List<Type>();
                Assembly[] assemblys = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblys.Length; i++)
                {
                    types.AddRange(assemblys[i].GetTypes());
                }
                return types;
            }

            /// <summary>
            /// 从当前程序域的所有程序集中获取所有类型
            /// </summary>
            /// <param name="filter">类型筛选器</param>
            /// <returns>所有类型集合</returns>
            public static List<Type> GetTypesInAllAssemblies(Func<Type, bool> filter)
            {
                List<Type> types = new List<Type>();
                Assembly[] assemblys = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblys.Length; i++)
                {
                    Type[] ts = assemblys[i].GetTypes();
                    foreach (var t in ts)
                    {
                        if (filter(t))
                        {
                            types.Add(t);
                        }
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