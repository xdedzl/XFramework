using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 框架入口类
    /// </summary>
    public static class GameEntry
    {
        // 当前已加载的模块
        private static readonly Dictionary<Type, IGameModule> m_GameModules = new Dictionary<Type, IGameModule>();
        private static readonly LinkedList<IMonoGameModule> m_MonoGameModules = new LinkedList<IMonoGameModule>();
        private static LinkedListNode<IMonoGameModule> m_CurrentModule;

        // 模块依赖关系 key：模块, Value：依赖的模块
        private static readonly Dictionary<string, List<Type>> m_DependenceDic = new Dictionary<string, List<Type>>();

        /// <summary>
        /// 每帧运行
        /// </summary>
        public static void ModuleUpdate()
        {
            m_CurrentModule = m_MonoGameModules.First;
            while (m_CurrentModule != null)
            {
                m_CurrentModule.Value.Update();
                m_CurrentModule = m_CurrentModule.Next;
            }
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
            if(m_GameModules.TryGetValue(moduleType, out IGameModule existingModule))
            {
                return existingModule;
            }

            IGameModule module = CreateModule(moduleType, args);

            // Instance 赋值
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

            m_GameModules.Add(moduleType, module);
            
            if (module is IMonoGameModule monoGameModule)
            {
                LinkedListNode<IMonoGameModule> current = m_MonoGameModules.First;
                while (current != null)
                {
                    if (monoGameModule.Priority > current.Value.Priority)
                    {
                        break;
                    }

                    current = current.Next;
                }

                if (current != null)
                {
                    m_MonoGameModules.AddBefore(current, monoGameModule);
                }
                else
                {
                    m_MonoGameModules.AddLast(monoGameModule);
                }
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

            if (m_GameModules.TryGetValue(moduleType, out IGameModule gameModule))
            {
                if (gameModule.IsPersistent)
                {
                    throw new XFrameworkException($"Persistent module {moduleType.Name} can not be shutdown");
                }
                
                gameModule.Shutdown();
                m_GameModules.Remove(moduleType);    
                
                // Instance 赋值
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
                
                // mono模块处理
                if (gameModule is IMonoGameModule monoGameModule)
                {
                    m_MonoGameModules.Remove(monoGameModule);
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
                    throw new XFrameworkException($"[Module] {item.Key} depend on {moduleType.Name}, please check module shutdown order");
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
        public static void ClearAllModule(bool force=false)
        {
            if (force)
            {
                foreach (var item in m_GameModules.Values)
                {
                    item.Shutdown();
                }
                
                m_GameModules.Clear();
                m_CurrentModule = null;
                m_DependenceDic.Clear();
            }
            else
            {
                throw new XFrameworkException("Please use ShutdownModule to shutdown module one by one to ensure the shutdown order");
                var node = m_MonoGameModules.First;
                while (node != null)
                {
                    var next = node.Next;
                    if (!node.Value.IsPersistent)
                    {
                        m_MonoGameModules.Remove(node);
                    }
                    node = next;
                }
            
                var modulesToRemove = new List<Type>();
                foreach (var kvp in m_GameModules)
                {
                    if (!kvp.Value.IsPersistent)
                    {
                        kvp.Value.Shutdown();
                        modulesToRemove.Add(kvp.Key);
                    }
                }
                foreach (var key in modulesToRemove)
                {
                    m_GameModules.Remove(key);
                }
            
                m_CurrentModule = null;
                m_DependenceDic.Clear();
            }
            
        }
    }
}