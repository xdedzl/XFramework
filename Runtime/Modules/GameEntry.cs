using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 框架入口类
    /// </summary>
    public static class GameEntry
    {
#if UNITY_EDITOR
        public readonly struct GameModuleDebugSnapshot
        {
            public GameModuleDebugSnapshot(Type moduleType, int priority, bool isPersistent, bool isMonoModule, int updateOrder, IReadOnlyList<Type> dependenceTypes)
            {
                ModuleType = moduleType;
                Priority = priority;
                IsPersistent = isPersistent;
                IsMonoModule = isMonoModule;
                UpdateOrder = updateOrder;
                DependenceTypes = dependenceTypes;
            }

            public Type ModuleType { get; }
            public int Priority { get; }
            public bool IsPersistent { get; }
            public bool IsMonoModule { get; }
            public int UpdateOrder { get; }
            public IReadOnlyList<Type> DependenceTypes { get; }
        }
#endif

        // 当前已加载的模块
        private static readonly Dictionary<Type, IGameModule> m_GameModules = new Dictionary<Type, IGameModule>();
        private static readonly LinkedList<IMonoGameModule> m_MonoGameModules = new LinkedList<IMonoGameModule>();
        private static LinkedListNode<IMonoGameModule> m_CurrentModule;
        
        /// <summary>
        /// 初始化指定生命周期的模块
        /// </summary>
        public static void InitializeModules(params ModuleLifecycle[] lifecycles)
        {
            var modules = Utility.Reflection.GetAssignableTypes(typeof(IGameModule), "Assembly-CSharp", "XFrameworkRuntime");
            foreach (var type in modules)
            {
                if (type.IsAbstract || !type.IsClass) continue;

                var attr = type.GetCustomAttribute<ModuleLifecycleAttribute>();
                if (attr == null || !lifecycles.Contains(attr.Lifecycle))
                {
                    continue;
                }
                
                AddModule(type);
            }

            string loadedModules = string.Join(", ", m_GameModules.Keys.Select(type =>
            {
                var attr = type.GetCustomAttribute<ModuleLifecycleAttribute>();
                string lifecycle = attr != null ? attr.Lifecycle.ToString() : nameof(ModuleLifecycle.Normal);
                return $"{type.Name}({lifecycle})";
            }));
            Debug.Log($"[XFramework] Initialized modules with lifecycles: {string.Join(", ", lifecycles)}. Loaded modules: {loadedModules}");
        }

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

            SetModuleInstance(moduleType, module);

            m_GameModules.Add(moduleType, module);
            
            if (module is IMonoGameModule monoGameModule)
            {
                LinkedListNode<IMonoGameModule> current = m_MonoGameModules.First;
                while (current != null)
                {
                    // 数值越小，优先级越高，越靠前。
                    if (monoGameModule.Priority < current.Value.Priority)
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

        private static void SetModuleInstance(Type moduleType, IGameModule module)
        {
            var tempType = moduleType.BaseType;
            var genericTypeDefinition = typeof(GameModuleBase<>);
            while (tempType != typeof(object))
            {
                if (tempType.IsGenericType && tempType.GetGenericTypeDefinition() == genericTypeDefinition)
                {
                    var field = tempType.GetField("m_instance", BindingFlags.Static | BindingFlags.NonPublic);
                    field.SetValue(null, module);
                    break;
                }
                tempType = tempType.BaseType;
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
            module.Initialize();
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
        /// <param name="moduleType">模块类型</param>
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
                
                SetModuleInstance(moduleType, null);
                
                // mono模块处理
                if (gameModule is IMonoGameModule monoGameModule)
                {
                    // todo 这里有个潜在问题, 假如在Update中卸载当前模块可能会有一些问题，先不处理，遇到了再说
                    // if (m_CurrentModule.Value == gameModule)
                    // {
                    //     m_CurrentModule = m_CurrentModule.Next;
                    // }
                    m_MonoGameModules.Remove(monoGameModule);
                }
                
                // 依赖模块处理
                if (m_DependenceDic.Remove(moduleType.Name, out var dependentModules))
                {
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
        /// 检查模块是否已加载
        /// </summary>
        public static bool IsModuleLoaded<T>() where T : IGameModule
        {
            return IsModuleLoaded(typeof(T));
        }

        /// <summary>
        /// 检查模块是否已加载
        /// </summary>
        public static bool IsModuleLoaded(Type moduleType)
        {
            return m_GameModules.ContainsKey(moduleType);
        }

        /// <summary>
        /// 获取当前已加载的所有指定生命周期的模块类型
        /// </summary>
        public static List<Type> GetLoadedModuleTypes(ModuleLifecycle lifecycle)
        {
            var result = new List<Type>();
            foreach (var kvp in m_GameModules)
            {
                var attr = kvp.Key.GetCustomAttribute<ModuleLifecycleAttribute>();
                if (attr != null && attr.Lifecycle == lifecycle)
                {
                    result.Add(kvp.Key);
                }
            }
            return result;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 获取当前已加载模块的只读调试快照，仅供编辑器工具展示。
        /// </summary>
        public static List<GameModuleDebugSnapshot> GetDebugModuleSnapshots()
        {
            var monoUpdateOrders = new Dictionary<Type, int>();
            int updateOrder = 0;
            LinkedListNode<IMonoGameModule> current = m_MonoGameModules.First;
            while (current != null)
            {
                monoUpdateOrders[current.Value.GetType()] = updateOrder;
                updateOrder++;
                current = current.Next;
            }

            var result = new List<GameModuleDebugSnapshot>(m_GameModules.Count);
            foreach (var kvp in m_GameModules)
            {
                Type moduleType = kvp.Key;
                IGameModule module = kvp.Value;
                List<Type> dependenceTypes = m_DependenceDic.TryGetValue(moduleType.Name, out var types)
                    ? new List<Type>(types)
                    : new List<Type>();

                result.Add(new GameModuleDebugSnapshot(
                    moduleType,
                    module.Priority,
                    module.IsPersistent,
                    module is IMonoGameModule,
                    monoUpdateOrders.TryGetValue(moduleType, out int order) ? order : -1,
                    dependenceTypes));
            }

            return result;
        }
#endif

        /// <summary>
        /// 卸载当前已加载的所有模块
        /// </summary>
        internal static void ClearAllModule(bool force=false)
        {
            var modules = GetModuleShutdownOrder();
            m_GameModules.Clear();
            if (force)
            {
                // 依赖者先销毁，被依赖模块后销毁；无依赖约束时按优先级降序销毁
                foreach (var item in modules)
                {
                    item.Value.Shutdown();
                    SetModuleInstance(item.Key, null);
                }
                
                m_GameModules.Clear();
                m_CurrentModule = null;
                m_MonoGameModules.Clear();
                m_DependenceDic.Clear();
            }
            else
            {
                foreach (var item in modules)
                {
                    ShutdownModule(item.Key);
                }
            }
        }

        private static List<KeyValuePair<Type, IGameModule>> GetModuleShutdownOrder()
        {
            var remainingModules = new Dictionary<Type, IGameModule>(m_GameModules);
            var result = new List<KeyValuePair<Type, IGameModule>>(remainingModules.Count);
            while (remainingModules.Count > 0)
            {
                var module = remainingModules
                    .Where(item => !remainingModules.Keys.Any(type => IsDependentOn(type, item.Key)))
                    .OrderByDescending(item => item.Value.Priority)
                    .First();

                result.Add(module);
                remainingModules.Remove(module.Key);
            }

            return result;
        }

        private static bool IsDependentOn(Type moduleType, Type dependenceType)
        {
            return m_DependenceDic.TryGetValue(moduleType.Name, out var dependenceTypes)
                && dependenceTypes.Contains(dependenceType);
        }
    }
}
