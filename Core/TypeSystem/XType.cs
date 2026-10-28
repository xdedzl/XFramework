using System;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework.TypeSystem
{
    /// <summary>
    /// 类型系统唯一对外入口：按类型别名查询类型、创建实例。
    /// 首次使用时扫描所有已加载程序集中标记了 <see cref="XTypeAliasAttribute"/> 的类并建立索引。
    /// </summary>
    public static class XType
    {
        private static readonly object s_Lock = new object();
        private static volatile Dictionary<string, Type> s_AliasToType;

        /// <summary>
        /// 尝试获取别名对应的类型。
        /// </summary>
        /// <param name="alias">类型别名。</param>
        /// <param name="type">别名对应的类型。</param>
        /// <returns>别名存在时返回 true。</returns>
        public static bool TryGet(string alias, out Type type)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                type = null;
                return false;
            }

            EnsureInitialized();
            return s_AliasToType.TryGetValue(alias, out type);
        }

        /// <summary>
        /// 获取别名对应的类型。
        /// </summary>
        /// <param name="alias">类型别名。</param>
        /// <returns>别名对应的类型。</returns>
        /// <exception cref="XFrameworkException">别名为空或不存在时抛出。</exception>
        public static Type Get(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new XFrameworkException("类型别名不能为空。");
            }

            if (!TryGet(alias, out Type type))
            {
                throw new XFrameworkException(
                    $"未找到类型别名 \"{alias}\"。请确认目标类型标记了 [XTypeAlias(\"{alias}\")]，" +
                    "并已加载到当前 AppDomain。");
            }

            return type;
        }

        /// <summary>
        /// 按别名创建实例。
        /// </summary>
        /// <param name="alias">类型别名。</param>
        /// <param name="args">构造函数参数。</param>
        /// <returns>创建好的实例。</returns>
        public static object Create(string alias, params object[] args)
        {
            Type type = Get(alias);
            return CreateInstance(alias, type, args);
        }

        /// <summary>
        /// 按别名创建指定基类或接口的实例。
        /// </summary>
        /// <typeparam name="T">期望的基类或接口。</typeparam>
        /// <param name="alias">类型别名。</param>
        /// <param name="args">构造函数参数。</param>
        /// <returns>创建好的实例。</returns>
        public static T Create<T>(string alias, params object[] args)
        {
            Type type = Get(alias);
            if (!typeof(T).IsAssignableFrom(type))
            {
                throw new XFrameworkException(
                    $"类型别名 \"{alias}\" 指向 {type.FullName}，无法转换为 {typeof(T).FullName}。");
            }

            return (T)CreateInstance(alias, type, args);
        }

        /// <summary>
        /// 获取所有已注册的别名，按名称排序。
        /// </summary>
        /// <returns>别名快照。</returns>
        public static IReadOnlyList<string> GetAliases()
        {
            EnsureInitialized();
            string[] aliases = new string[s_AliasToType.Count];
            s_AliasToType.Keys.CopyTo(aliases, 0);
            Array.Sort(aliases, StringComparer.Ordinal);
            return aliases;
        }

        /// <summary>
        /// 重新扫描所有已加载程序集并重建别名索引。
        /// </summary>
        public static void Refresh()
        {
            lock (s_Lock)
            {
                s_AliasToType = BuildRegistry();
            }
        }

        private static void EnsureInitialized()
        {
            if (s_AliasToType != null)
            {
                return;
            }

            lock (s_Lock)
            {
                if (s_AliasToType == null)
                {
                    s_AliasToType = BuildRegistry();
                }
            }
        }

        private static Dictionary<string, Type> BuildRegistry()
        {
            var aliasToType = new Dictionary<string, Type>(StringComparer.Ordinal);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Array.Sort(assemblies, CompareAssembly);

            foreach (Assembly assembly in assemblies)
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                foreach (Type type in GetLoadableTypes(assembly))
                {
                    XTypeAliasAttribute attribute = type.GetCustomAttribute<XTypeAliasAttribute>(false);
                    if (attribute != null)
                    {
                        Register(aliasToType, type, attribute);
                    }
                }
            }

            return aliasToType;
        }

        private static int CompareAssembly(Assembly left, Assembly right)
        {
            return string.CompareOrdinal(left.GetName().Name, right.GetName().Name);
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var types = new List<Type>(exception.Types.Length);
                foreach (Type type in exception.Types)
                {
                    if (type != null)
                    {
                        types.Add(type);
                    }
                }

                return types.ToArray();
            }
            catch (Exception)
            {
                return Type.EmptyTypes;
            }
        }

        private static void Register(
            Dictionary<string, Type> aliasToType,
            Type type,
            XTypeAliasAttribute attribute)
        {
            if (string.IsNullOrWhiteSpace(attribute.Alias))
            {
                throw new XFrameworkException($"类型 {type.FullName} 的 XTypeAlias 不能为空。");
            }

            if (type.IsAbstract)
            {
                throw new XFrameworkException($"类型 {type.FullName} 是抽象类型，不能声明 XTypeAlias。");
            }

            if (type.ContainsGenericParameters)
            {
                throw new XFrameworkException($"类型 {type.FullName} 是泛型类型定义，不能声明 XTypeAlias。");
            }

            if (aliasToType.TryGetValue(attribute.Alias, out Type existing))
            {
                throw new XFrameworkException(
                    $"类型别名 \"{attribute.Alias}\" 重复：{existing.FullName} 与 {type.FullName}。");
            }

            aliasToType.Add(attribute.Alias, type);
        }

        private static object CreateInstance(string alias, Type type, object[] args)
        {
            try
            {
                return Utility.Reflection.CreateInstance(type, args);
            }
            catch (Exception exception)
            {
                throw new XFrameworkException(
                    $"通过类型别名 \"{alias}\" 创建 {type.FullName} 实例失败：{exception.Message}",
                    exception);
            }
        }
    }
}
