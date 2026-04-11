using System;
using System.Collections.Generic;
using System.Reflection;
using XFramework.UI;

namespace XFramework
{
    /// <summary>
    /// 流程的优先级应比状态机低
    /// </summary>
    public class ProcedureManager : MonoSingleton<ProcedureManager>
    {
        /// <summary>
        /// 存储所有流程实例
        /// </summary>
        private readonly Dictionary<string, ProcedureBase> m_ProcedureDic = new();

        /// <summary>
        /// 当前流程
        /// </summary>
        private ProcedureBase m_CurrentProcedure;

        /// <summary>
        /// 当前由流程管理的UI面板名称
        /// </summary>
        private readonly HashSet<string> m_ProcedureManagedPanels = new();

        /// <summary>
        /// 当前流程
        /// </summary>
        public ProcedureBase CurrentProcedure => m_CurrentProcedure;

        /// <summary>
        /// 当前的子流程
        /// </summary>
        public SubProcedureBase CurrenSubProcedure => m_CurrentProcedure?.CurrentSubProcedure;

        /// <summary>
        /// 切换流程
        /// </summary>
        /// <typeparam name="TProcedure">流程类型</typeparam>
        public void ChangeProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            ChangeProcedure(typeof(TProcedure));
        }

        /// <summary>
        /// 切换流程
        /// </summary>
        /// <param name="type">流程类型</param>
        public void ChangeProcedure(Type type)
        {
            ProcedureBase newProcedure = type is null ? null : GetOrCreateProcedure(type);
            if (m_CurrentProcedure == newProcedure)
                return;

            var oldProcedure = m_CurrentProcedure;
            oldProcedure?.OnExit();
            oldProcedure?.CurrentSubProcedure?.OnExit();

            m_CurrentProcedure = newProcedure;

            newProcedure?.OnEnter(oldProcedure);
            if (newProcedure == null)
            {
                HandleProcedureModules(null);
                HandleProcedureUI(null);
            }
            else
            {
                newProcedure.OnPrepare(() =>
                {
                    // 防止准备期间已经切换到其他流程
                    if (m_CurrentProcedure != newProcedure)
                        return;

                    HandleProcedureModules(newProcedure);
                    HandleProcedureUI(newProcedure);
                });
            }
        }

        /// <summary>
        /// 新增或更新一个流程实例
        /// </summary>
        /// <param name="procedure">流程</param>
        public void UpdateProcedure(ProcedureBase procedure)
        {
            var key = procedure.GetType().Name;
            m_ProcedureDic[key] = procedure;

            if (m_CurrentProcedure != null && key == m_CurrentProcedure.GetType().Name)
            {
                m_CurrentProcedure = procedure;
            }
        }

        /// <summary>
        /// 获取当前流程
        /// </summary>
        /// <typeparam name="TProcedure">流程类型</typeparam>
        /// <returns>当前流程</returns>
        public TProcedure GetCurrentProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            if (TryGetCurrentProcedure<TProcedure>(out var procedure))
            {
                return procedure;
            }
            else
            {
                throw new XFrameworkException($"[Procedure] current procedure is not {typeof(TProcedure).Name}, Please Use TryGetCurrentProcedure");
            }
        }

        /// <summary>
        /// 获取当前流程
        /// </summary>
        public bool TryGetCurrentProcedure<TProcedure>(out TProcedure procedure) where TProcedure : ProcedureBase
        {
            if (m_CurrentProcedure is TProcedure p)
            {
                procedure = p;
                return true;
            }
            else
            {
                procedure = null;
                return false;
            }
        }

        public void Update()
        {
            m_CurrentProcedure?.OnUpdate();
        }

        private ProcedureBase GetOrCreateProcedure(Type type)
        {
            if (!m_ProcedureDic.TryGetValue(type.Name, out var procedure))
            {
                procedure = Utility.Reflection.CreateInstance<ProcedureBase>(type);
                m_ProcedureDic[type.Name] = procedure;
                procedure.OnInit();
            }
            return procedure;
        }

        /// <summary>
        /// 根据新流程的 ProcedureModuleAttribute 进行模块差异加载/卸载
        /// </summary>
        private void HandleProcedureModules(ProcedureBase newState)
        {
            var requiredTypes = new HashSet<Type>();
            if (newState != null)
            {
                var attr = newState.GetType().GetCustomAttribute<ProcedureModuleAttribute>();
                if (attr != null)
                {
                    foreach (var moduleType in attr.ModuleTypes)
                    {
                        var lifecycleAttr = moduleType.GetCustomAttribute<ModuleLifecycleAttribute>();
                        if (lifecycleAttr == null || lifecycleAttr.Lifecycle != ModuleLifecycle.Procedure)
                        {
                            throw new XFrameworkException(
                                $"[Procedure] Module {moduleType.Name} declared in ProcedureModuleAttribute on {newState.GetType().Name} " +
                                $"must have [ModuleLifecycle(ModuleLifecycle.Procedure)] attribute");
                        }
                        requiredTypes.Add(moduleType);
                    }
                }
            }

            var loadedProcedureModules = GameEntry.GetLoadedModuleTypes(ModuleLifecycle.Procedure);

            foreach (var loadedType in loadedProcedureModules)
            {
                if (!requiredTypes.Contains(loadedType))
                {
                    GameEntry.ShutdownModule(loadedType);
                }
            }

            foreach (var requiredType in requiredTypes)
            {
                if (!GameEntry.IsModuleLoaded(requiredType))
                {
                    GameEntry.AddModule(requiredType);
                }
            }
        }

        /// <summary>
        /// 根据新流程的 ProcedureUIAttribute 进行UI面板差异打开/关闭
        /// </summary>
        private void HandleProcedureUI(ProcedureBase newState)
        {
            var requiredPanels = new HashSet<string>();
            if (newState != null)
            {
                var attr = newState.GetType().GetCustomAttribute<ProcedureUIAttribute>();
                if (attr != null)
                {
                    foreach (var panelName in attr.PanelNames)
                    {
                        requiredPanels.Add(panelName);
                    }
                }
            }

            // 关闭不再需要的面板
            foreach (var panelName in m_ProcedureManagedPanels)
            {
                if (!requiredPanels.Contains(panelName))
                {
                    UIManager.Instance.ClosePanel(panelName);
                }
            }

            // 打开需要的面板
            foreach (var panelName in requiredPanels)
            {
                if (!m_ProcedureManagedPanels.Contains(panelName))
                {
                    UIManager.Instance.OpenPanel(panelName);
                }
            }

            m_ProcedureManagedPanels.Clear();
            foreach (var panelName in requiredPanels)
            {
                m_ProcedureManagedPanels.Add(panelName);
            }
        }
    }
}