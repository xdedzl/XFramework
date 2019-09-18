using System;
using System.Collections.Generic;

namespace XFramework
{
    public static class GameEntry
    {
        private static LinkedList<IGameModule> m_GameModules = new LinkedList<IGameModule>();

        private static LinkedListNode<IGameModule> m_CurrentModule;

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
            foreach (var module in m_GameModules)
            {
                if (module.GetType() == moduleType)
                {
                    return (T)module;
                }
            }
            return (T)CreateModule(moduleType, args);
        }

        /// <summary>
        /// 关闭一个模块
        /// </summary>
        /// <typeparam name="T">模块类型</typeparam>
        public static void ShutdownModule<T>() where T : IGameModule
        {
            Type moduleType = typeof(T);
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
        }

        /// <summary>
        /// 创建一个模块
        /// </summary>
        /// <param name="moduleType"></param>
        /// <returns></returns>
        private static IGameModule CreateModule(Type moduleType, params object[] args)
        {
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