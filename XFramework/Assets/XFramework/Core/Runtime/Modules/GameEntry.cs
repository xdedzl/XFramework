using System;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework
{
    /// <summary>
    /// 框架入口类
    /// </summary>
    public static class GameEntry
    {
        // 当前已加载的模块
        private static readonly LinkedList<IGameModule> m_GameModules = new LinkedList<IGameModule>();
        private static LinkedListNode<IGameModule> m_CurrentModule;

        // 模块依赖关系 key：模块, Value：依赖的模块
        private static readonly Dictionary<string, List<Type>> m_DependenceDic = new Dictionary<string, List<Type>>();

        /// <summary>
        /// 每帧运行
        /// </summary>
        /// <param name="elapseSeconds">逻辑运行时间</param>
        /// <param name="realElapseSeconds">真实运行时间</param>
        public static void ModuleUpdate(float elapseSeconds, float realElapseSeconds)
        {
            m_CurrentModule = m_GameModules.First;
            while (m_CurrentModule != null)
            {
                m_CurrentModule.Value.Update(elapseSeconds, realElapseSeconds);
                m_CurrentModule = m_CurrentModule.Next;
            }
        }

        /// <summary>
        /// 获取一个模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <returns>模块</returns>
        public static T GetModule<T>() where T : class, IGameModule
        {
            Type moduleType = typeof(T);
            foreach (var module in m_GameModules)
            {
                if (module.GetType() == moduleType)
                {
                    return (T)module;
                }
            }

            return null;
        }

        /// <summary>
        /// 开启一个模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <param name="args">对应模块构造函数需要用到的参数</param>
        /// <returns>模块</returns>
        public static T AddModule<T>(params object[] args) where T : IGameModule
        {
            Type moduleType = typeof(T);

            return (T)AddModule(moduleType, args);
        }

        /// <summary>
        /// 开启一个模块
        /// </summary>
        /// <param name="moduleType">模块类型</param>
        /// <param name="args">对应模块构造函数需要用到的参数</param>
        /// <returns>模块</returns>
        public static IGameModule AddModule(Type moduleType, params object[] args)
        {
            foreach (var item in m_GameModules)
            {
                if (item.GetType() == moduleType)
                {
                    return item;
                }
            }

            IGameModule module = CreateModule(moduleType, args);

            // 特殊处理
            var tempType = moduleType.BaseType;
            var genericTypeDefinition = typeof(GameModuleBase<>);
            while(tempType != typeof(object))
            {
                if (tempType.IsGenericType && tempType.GetGenericTypeDefinition() == genericTypeDefinition)
                {
                    var field = tempType.GetField("m_instance", BindingFlags.Static | BindingFlags.NonPublic);
                    field.SetValue(null, module);
                    break;
                }
                tempType = tempType.BaseType;
            }

            // 将模块添加到链表中
            LinkedListNode<IGameModule> current = m_GameModules.First;
            while (current != null)
            {
                if (module.Priority > current.Value.Priority)
                {
                    break;
                }

                current = current.Next;
            }

            if (current != null)
            {
                m_GameModules.AddBefore(current, module);
            }
            else
            {
                m_GameModules.AddLast(module);
            }

            return module;
        }

        /// <summary>
        /// 创建一个模块
        /// </summary>
        /// <param name="moduleType">模块类型</param>
        /// <param name="args">模块启动参数</param>
        /// <returns>模块</returns>
        private static IGameModule CreateModule(Type moduleType, params object[] args)
        {
            // 加载依赖模块
            var dependenceModules = moduleType.GetCustomAttributes<DependenceModuleAttribute>();
            if (dependenceModules != null)
            {
                if (!m_DependenceDic.TryGetValue(moduleType.Name, out List<Type> modules))
                {
                    modules = new List<Type>();
                    m_DependenceDic.Add(moduleType.Name, modules);
                }
                foreach (var item in dependenceModules)
                {
                    modules.Add(item.moduleType);
                    AddModule(item.moduleType, item.args);
                }
            }

            // 加载模块
            IGameModule module = (IGameModule)Activator.CreateInstance(moduleType, args);
            if (module == null)
            {
                throw new Exception(moduleType.Name + " is not a module");
            }

            return module;
        }

        /// <summary>
        /// 关闭一个模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <param name="shutdownAllDependentModule">是否卸载其依赖的模块（当没有其它模块也依赖它时）</param>
        public static void ShutdownModule<T>(bool shutdownAllDependentModule = false) where T : IGameModule
        {
            Type moduleType = typeof(T);

            ShutdownModule(moduleType, shutdownAllDependentModule);
        }

        /// <summary>
        /// 关闭一个模块
        /// </summary>
        /// <param name="moduleType">模块类型</typeparam>
        /// <param name="shutdownAllDependentModule">是否卸载其依赖的模块（当没有其它模块也依赖它时）</param>
        public static void ShutdownModule(Type moduleType, bool shutdownAllDependentModule = false)
        {
            CheckShutdownSafe(moduleType);

            IGameModule gameModule = null;
            foreach (var module in m_GameModules)
            {
                if (module.GetType() == moduleType)
                {
                    gameModule = module;
                    gameModule.Shutdown();
                    break;
                }
            }

            m_GameModules.Remove(gameModule);

            // 特殊处理
            var tempType = moduleType.BaseType;
            var genericTypeDefinition = typeof(GameModuleBase<>);
            while (tempType != typeof(object))
            {
                if (tempType.IsGenericType && tempType.GetGenericTypeDefinition() == genericTypeDefinition)
                {
                    var field = tempType.GetField("m_instance", BindingFlags.Static | BindingFlags.NonPublic);
                    field.SetValue(null, null);
                    break;
                }
                tempType = tempType.BaseType;
            }

            // 依赖模块处理
            if (m_DependenceDic.TryGetValue(moduleType.Name, out List<Type> dependentModules))
            {
                m_DependenceDic.Remove(moduleType.Name);

                if (shutdownAllDependentModule)
                {
                    // 卸载依赖模块
                    foreach (var dependentModule in dependentModules)
                    {
                        if (!HaveModuleDependent(dependentModule))
                        {
                            ShutdownModule(dependentModule, shutdownAllDependentModule);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 检测删除对应模块安全性
        /// </summary>
        /// <param name="moduleType"></param>
        private static void CheckShutdownSafe(Type moduleType)
        {
            foreach (var item in m_DependenceDic)
            {
                if (item.Value.Contains(moduleType))
                {
                    throw new XFrameworkException($"[Module] {item.Key}依赖于{moduleType.Name}, 请检查卸载时机或顺序");
                }
            }
        }

        /// <summary>
        /// 是否还有别的模块依赖于此模块
        /// </summary>
        /// <param name="moduleType">模块类型</param>
        /// <returns></returns>
        private static bool HaveModuleDependent(Type moduleType)
        {
            foreach (var item in m_DependenceDic)
            {
                if (item.Value.Contains(moduleType))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 卸载当前已加载的所有模块
        /// </summary>
        public static void CleraAllModule()
        {
            foreach (var item in m_GameModules)
            {
                item.Shutdown();
            }
            m_GameModules.Clear();
            m_CurrentModule = null;
            m_DependenceDic.Clear();
        }
    }
}