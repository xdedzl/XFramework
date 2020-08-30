using System;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework
{
    public static partial class Extend
    {
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
    }
}