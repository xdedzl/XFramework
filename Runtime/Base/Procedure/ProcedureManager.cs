using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 流程的优先级应比状态机低
    /// </summary>
    public partial class ProcedureManager : MonoSingleton<ProcedureManager>
    {
        /// <summary>
        /// 存储所有流程实例
        /// </summary>
        private readonly Dictionary<string, ProcedureBase> m_ProcedureDic = new();

        /// <summary>
        /// 当前流程
        /// </summary>
        private ProcedureBase m_CurrentProcedure;

        private ProcedureOverlayBase m_CurrentOverlay;

        // UI 面板状态已移至 ProcedureUIProcessor 中管理

        /// <summary>
        /// 当前流程
        /// </summary>
        public ProcedureBase CurrentProcedure => m_CurrentProcedure;

        public ProcedureOverlayBase CurrentOverlay => m_CurrentOverlay;

        public bool IsOverlayRunning => m_CurrentOverlay != null;

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
            {
                if (m_CurrentOverlay != null)
                {
                    StopOverlay();
                }
                return;
            }

            StopOverlay();

            var oldProcedure = m_CurrentProcedure;
            oldProcedure?.OnExit();

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

        public bool StartOverlay<TOverlay>(params object[] args) where TOverlay : ProcedureOverlayBase, new()
        {
            if (m_CurrentOverlay != null)
            {
                UnityEngine.Debug.LogWarning($"[Procedure] Cannot start overlay {typeof(TOverlay).Name}. Current overlay is {m_CurrentOverlay.GetType().Name}.");
                return false;
            }

            var overlay = new TOverlay();
            m_CurrentOverlay = overlay;
            overlay.OnInit();
            overlay.OnEnter(args);
            if (m_CurrentOverlay != overlay)
            {
                return false;
            }

            overlay.OnPrepare(() =>
            {
                if (m_CurrentOverlay != overlay)
                {
                    return;
                }

                RefreshProcedureState();
            });
            return true;
        }

        public void StopOverlay()
        {
            if (m_CurrentOverlay == null)
            {
                return;
            }

            var overlay = m_CurrentOverlay;
            m_CurrentOverlay = null;
            overlay.OnExit();
            RefreshProcedureState();
        }

        public void Update()
        {
            m_CurrentProcedure?.OnUpdate();
            m_CurrentOverlay?.OnUpdate();
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
        /// 流程处理器列表
        /// </summary>
        private readonly List<IProcedureProcessor> m_Processors = new List<IProcedureProcessor>()
        {
            new ProcedureModuleProcessor(),
            new ProcedureUIProcessor(),
            new ProcedureCameraProcessor(),
            new ProcedureCursorProcessor(),
            new ProcedureTimeScaleProcessor()
        };

        /// <summary>
        /// 注册自定义流程处理器
        /// </summary>
        public void AddProcessor(IProcedureProcessor processor)
        {
            if (processor != null && !m_Processors.Contains(processor))
            {
                m_Processors.Add(processor);
            }
        }

        /// <summary>
        /// 特性缓存字典
        /// </summary>
        private readonly Dictionary<Type, ProcedureAttributeContext> m_AttributeCache = new();

        /// <summary>
        /// 获取或创建指定类型的特性上下文。
        /// </summary>
        private ProcedureAttributeContext GetContext(Type type)
        {
            if (type == null) return null;
            if (!m_AttributeCache.TryGetValue(type, out var context))
            {
                context = new ProcedureAttributeContext(type);
                m_AttributeCache[type] = context;
            }
            return context;
        }

        /// <summary>
        /// 强制刷新当前流程（及子流程）的所有自动配置项（模块、UI、相机等）。
        /// 通常在全流程切换或子流程切换后由框架内部自动调用。
        /// </summary>
        internal void RefreshProcedureState()
        {
            var subProcedure = m_CurrentProcedure?.CurrentSubProcedure;
            var parentContext = GetContext(m_CurrentProcedure?.GetType());
            var subContext = subProcedure != null ? GetContext(subProcedure.GetType()) : null;
            var overlayContext = m_CurrentOverlay != null ? GetContext(m_CurrentOverlay.GetType()) : null;
            var context = new ProcedureRefreshContext(m_CurrentProcedure, subProcedure, m_CurrentOverlay, parentContext, subContext, overlayContext);

            foreach (var processor in m_Processors)
            {
                processor.OnRefreshProcedureState(context);
            }
        }
    }
}
