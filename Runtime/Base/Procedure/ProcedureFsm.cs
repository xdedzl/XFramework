using System;
using System.Collections.Generic;
using System.Reflection;
using XFramework.Fsm;
using XFramework.UI;

namespace XFramework
{
    public class ProcedureFsm : Fsm<ProcedureBase>
    {
        /// <summary>
        /// 当前由流程管理的UI面板名称
        /// </summary>
        private readonly HashSet<string> m_ProcedureManagedPanels = new HashSet<string>();

        protected override void OnStateChange(ProcedureBase oldState, ProcedureBase newState)
        {
            oldState?.CurrentSubProcedure?.OnExit();

            newState?.OnEnter(oldState);

            newState?.OnPrepare(() =>
            {
                // 防止准备期间已经切换到其他流程
                if (CurrentState != newState)
                    return;

                HandleProcedureModules(newState);
                HandleProcedureUI(newState);
            });
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