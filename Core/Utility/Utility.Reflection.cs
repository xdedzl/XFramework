using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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
            public static IEnumerable<Type> GetSonTypes(Type typeBase, string assemblyName = "Assembly-CSharp")
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
                    Type[] ts;
                    try
                    {
                        ts = assemblys[i].GetTypes();
                    }
                    catch (Exception)
                    {
                        continue;
                    }
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

            public static T GetStaticFiled<T>(Type type, string filedName)
            {
                var filed = type.GetField(filedName, BindingFlags.NonPublic | BindingFlags.Static);
                return (T)filed.GetValue(null);
            }

            #region 反射性能优化

            public static PropertyWrapper<T> PropertyWrapper<T>(object target, PropertyInfo propertyInfo)
            {
                return new PropertyWrapper<T>(target, propertyInfo);
            }

            public static Action MethodWrapperAction(object target, MethodInfo methodInfo)
            {
                return (Action)Delegate.CreateDelegate(typeof(Action), target, methodInfo);
            }

            public static Action<T> MethodWrapperAction<T>(object target, MethodInfo methodInfo)
            {
                return (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), target, methodInfo);
            }

            public static Action<T1, T2> MethodWrapperAction<T1, T2>(object target, MethodInfo methodInfo)
            {
                return (Action<T1, T2>)Delegate.CreateDelegate(typeof(Action<T1, T2>), target, methodInfo);
            }

            public static Action<T1, T2, T3> MethodWrapperAction<T1, T2, T3>(object target, MethodInfo methodInfo)
            {
                return (Action<T1, T2, T3>)Delegate.CreateDelegate(typeof(Action<T1, T2, T3>), target, methodInfo);
            }

            public static Func<TResult> MethodWrapperFunc<TResult>(object target, MethodInfo methodInfo)
            {
                return (Func<TResult>)Delegate.CreateDelegate(typeof(Func<TResult>), target, methodInfo);
            }

            public static Func<T1, TResult> MethodWrapperFunc<T1, TResult>(object target, MethodInfo methodInfo)
            {
                return (Func<T1, TResult>)Delegate.CreateDelegate(typeof(Func<T1, TResult>), target, methodInfo);
            }

            public static Func<T1, T2, TResult> MethodWrapperFunc<T1, T2, TResult>(object target, MethodInfo methodInfo)
            {
                return (Func<T1, T2, TResult>)Delegate.CreateDelegate(typeof(Func<T1, T2, TResult>), target, methodInfo);
            }

            public static Func<T1, T2, T3, TResult> MethodWrapperFunc<T1, T2, T3, TResult>(object target, MethodInfo methodInfo)
            {
                return (Func<T1, T2, T3, TResult>)Delegate.CreateDelegate(typeof(Func<T1, T2, T3, TResult>), target, methodInfo);
            }

            #endregion
        }

        /// <summary>
        /// PropertyInfo优化适配器
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        public class PropertyWrapper<T>
        {
            private Action<T> setter;
            private Func<T> getter;

            /// <summary>
            /// 属性的值
            /// </summary>
            public T Value
            {
                get
                {
                    return getter();
                }
                set
                {
                    setter(value);
                }
            }

            /// <summary>
            /// 构造一个用于优化PropertyInfo的适配器
            /// </summary>
            /// <param name="target">propertyInfo属于的对应</param>
            /// <param name="propertyInfo">propertyInfo属于的对应</param>
            public PropertyWrapper(object target, PropertyInfo propertyInfo)
            {
                var methodInfo = propertyInfo.GetSetMethod();
                var @delegate = Delegate.CreateDelegate(typeof(Action<T>), target, methodInfo);
                setter = (Action<T>)@delegate;

                methodInfo = propertyInfo.GetGetMethod();
                @delegate = Delegate.CreateDelegate(typeof(Func<T>), target, methodInfo);
                getter = (Func<T>)@delegate;
            }
        }


        #region Extend
        /// <summary>
        /// 通过反射和函数名调用非公有方法
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="methodName">函数名</param>
        /// <param name="objs">参数数组</param>
        public static void Invoke(this object obj, string methodName, params object[] objs)
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = obj.GetType();
            MethodInfo m = type.GetMethod(methodName, flags);
            m.Invoke(obj, objs);
        }

        public static string[] GetSonNames(this Type typeBase, string assemblyName = "Assembly-CSharp")
        {
            List<string> typeNames = new List<string>();
            Assembly assembly;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch
            {
                return new string[0];
            }

            if (assembly == null)
            {
                return new string[0];
            }

            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeBase))
                {
                    typeNames.Add(type.FullName);
                }
            }
            typeNames.Sort();
            return typeNames.ToArray();
        }


        /// <summary>
        /// 从当前类型中获取所有字段
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="filter">字段筛选器</param>
        /// <returns>所有字段集合</returns>
        public static List<FieldInfo> GetFields(this Type type, Func<FieldInfo, bool> filter)
        {
            List<FieldInfo> fields = new List<FieldInfo>();
            FieldInfo[] infos = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < infos.Length; i++)
            {
                if (filter(infos[i]))
                {
                    fields.Add(infos[i]);
                }
            }
            return fields;
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
        /// 从当前类型中获取所有方法
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="filter">方法筛选器</param>
        /// <returns>所有方法集合</returns>
        public static List<MethodInfo> GetMethods(this Type type, Func<MethodInfo, bool> filter)
        {
            List<MethodInfo> methods = new List<MethodInfo>();
            MethodInfo[] infos = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < infos.Length; i++)
            {
                if (filter(infos[i]))
                {
                    methods.Add(infos[i]);
                }
            }
            return methods;
        }
        #endregion
    }
}