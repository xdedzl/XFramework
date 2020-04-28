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
        private static LinkedList<IGameModule> m_GameModules = new LinkedList<IGameModule>();
        private static LinkedListNode<IGameModule> m_CurrentModule;

        // 模块依赖关系 key：模块, Value：依赖的模块
        private static Dictionary<string, List<string>> m_Dependence = new Dictionary<string, List<string>>();

        /// <summary>
        /// 每帧运行
        /// </summary>
        /// <param name="elapseSeconds">逻辑运行时间</param>
        /// <param name="realElapseSeconds">真实运行时间</param>
        public static void ModuleUpdate(float elapseSeconds, float realElapseSeconds)
        {
            m_CurrentModule = m_GameModules.First;
            m_CurrentModule.Value.Update(elapseSeconds, realElapseSeconds);
            while (m_CurrentModule.Next != null)
            {
                m_CurrentModule = m_CurrentModule.Next;
                m_CurrentModule.Value.Update(elapseSeconds, realElapseSeconds);
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
            foreach (var module in m_GameModules)
            {
                if (module.GetType() == moduleType)
                {
                    return module;
                }
            }
            return CreateModule(moduleType, args);
        }

        /// <summary>
        /// 关闭一个模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        /// <param name="shutdownAllDependentModule">是否卸载其依赖的模块（当没有其它模块也依赖它时）</param>
        public static void ShutdownModule<T>(bool shutdownAllDependentModule = false) where T : IGameModule
        {
            Type moduleType = typeof(T);
            foreach (var item in m_Dependence)
            {
                if (item.Value.Contains(moduleType.Name))
                {
                    throw new FrameworkException($"[Module] {item.Key}依赖于{moduleType.Name}, 请检查卸载时机或顺序");
                }
            }
            IGameModule gameModule = null;
            foreach (var module in m_GameModules)
            {
                if (module.GetType() == moduleType)
                {
                    gameModule = module;
                    break;
                }
            }

            if (gameModule != null)
            {
                gameModule.Shutdown();
            }
            m_GameModules.Remove(gameModule);

            if(m_Dependence.TryGetValue(moduleType.Name, out List<string> dependentModules))
            {
                m_Dependence.Remove(moduleType.Name);

                if (shutdownAllDependentModule)
                {
                    // 卸载依赖模块
                    foreach (var dependentModule in dependentModules)
                    {
                        // TODO
                        //bool canShutdown = true;
                        //foreach (var item in m_Dependence.Values)
                        //{
                        //    if (item.Contains(dependentModule))
                        //    {
                        //        canShutdown = false;
                        //        break;
                        //    }
                        //}
                        //if(canShutdown.)
                    }
                }
            }
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
                if (!m_Dependence.TryGetValue(moduleType.Name, out List<string> modules))
                {
                    modules = new List<string>();
                    m_Dependence.Add(moduleType.Name, modules);
                }
                foreach (var item in dependenceModules)
                {
                    modules.Add(item.moduleType.Name);
                    AddModule(item.moduleType, item.args);
                }
            }

            // 加载模块
            IGameModule module = (IGameModule)Activator.CreateInstance(moduleType, args);
            if (module == null)
            {
                throw new Exception(moduleType.Name + " is not a module");
            }

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
        /// 卸载当前已加载的所有模块
        /// </summary>
        public static void CleraAllModule()
        {
            foreach (var item in m_GameModules)
            {
                item.Shutdown();
            }
        }
    }
}