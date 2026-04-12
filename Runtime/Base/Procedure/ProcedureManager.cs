using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
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
                RefreshProcedureState();
            }
            else
            {
                newProcedure.OnPrepare(() =>
                {
                    // 防止准备期间已经切换到其他流程
                    if (m_CurrentProcedure != newProcedure)
                        return;

                    RefreshProcedureState();
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
        /// 当前激活的相机对象名称
        /// </summary>
        private string m_ActiveCameraName;

        /// <summary>
        /// 强制刷新当前流程（及子流程）的所有自动配置项（模块、UI、相机等）。
        /// 通常在全流程切换或子流程切换后由框架内部自动调用。
        /// </summary>
        internal void RefreshProcedureState()
        {
            if (m_CurrentProcedure != null)
            {
                HandleProcedureModules(m_CurrentProcedure);
                HandleProcedureUI(m_CurrentProcedure);
                HandleProcedureCamera(m_CurrentProcedure);
            }
        }

        /// <summary>
        /// 从当前流程栈中获取指定特性（子流程优先）。
        /// </summary>
        private T GetAttribute<T>(ProcedureBase state) where T : Attribute
        {
            if (state == null) return null;

            // 1. 优先从当前子流程类上查找
            if (state.CurrentSubProcedure != null)
            {
                var attr = state.CurrentSubProcedure.GetType().GetCustomAttribute<T>();
                if (attr != null) return attr;
            }

            // 2. 其次从父流程类上查找
            return state.GetType().GetCustomAttribute<T>();
        }

        /// <summary>
        /// 根据特性配置自动切换相机显隐。
        /// 规则：子流程优先；如果都没有定义则保持现状。
        /// </summary>
        private void HandleProcedureCamera(ProcedureBase newState)
        {
            if (newState == null) return;

            var camAttr = GetAttribute<ProcedureCameraAttribute>(newState);
            string targetCameraName = camAttr?.CameraName;

            // 执行物理切换 (优先组件 Enable，其次 GameObject Active)
            if (!string.IsNullOrEmpty(targetCameraName) && targetCameraName != m_ActiveCameraName)
            {
                // 关闭旧相机
                if (!string.IsNullOrEmpty(m_ActiveCameraName))
                {
                    GameObject oldGo = UObjectFinder.Find(m_ActiveCameraName);
                    ToggleCameraObject(oldGo, false);
                }

                // 开启新相机
                m_ActiveCameraName = targetCameraName;
                GameObject newGo = UObjectFinder.Find(m_ActiveCameraName);
                if (newGo != null)
                {
                    ToggleCameraObject(newGo, true);
                    Debug.Log($"[ProcedureManager] Switch Camera to: {m_ActiveCameraName}");
                }
                else if (!string.IsNullOrEmpty(m_ActiveCameraName))
                {
                    Debug.LogWarning($"[ProcedureManager] Camera not found: {m_ActiveCameraName}");
                }
            }
        }

        private void ToggleCameraObject(GameObject go, bool active)
        {
            if (go == null) return;

            // 寻找镜头组件（适配 Cinemachine 或标准 Camera）
            // 优先通过 behaviour.enabled 控制，这样在 Unity 6 中可以保留物体的生命周期
            var behaviours = go.GetComponents<Behaviour>();
            bool componentFound = false;
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                string typeName = b.GetType().Name;
                
                // 识别 Cinemachine 系列组件 (Unity 6: CinemachineCamera, Legacy: CinemachineVirtualCamera)
                if (typeName.Contains("CinemachineCamera") || 
                    typeName.Contains("CinemachineVirtualCamera") || 
                    typeName.Contains("CinemachineFreeLook"))
                {
                    b.enabled = active;
                    componentFound = true;
                }
                // 也可以适配标准 Camera
                else if (typeName == "Camera")
                {
                    b.enabled = active;
                    componentFound = true;
                }
            }

            // 如果没找到组件，或者明确需要开关物体，则回退到 SetActive
            if (!componentFound)
            {
                go.SetActive(active);
            }
        }

        /// <summary>
        /// 根据新流程或子流程的特性进行UI面板差异打开/关闭。
        /// 规则：优先使用子流程配置（排他性覆盖）；否则沿用父流程配置。
        /// </summary>
        private void HandleProcedureUI(ProcedureBase newState)
        {
            var requiredPanels = new HashSet<string>();
            if (newState != null)
            {
                var uiAttr = GetAttribute<ProcedureUIAttribute>(newState);
                if (uiAttr != null)
                {
                    foreach (var panelName in uiAttr.PanelNames)
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