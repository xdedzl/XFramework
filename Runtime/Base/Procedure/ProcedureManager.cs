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
        private MainProcedure m_CurrentProcedure;

        private ProcedureOverlayBase m_CurrentOverlay;

        private readonly List<ParallelProcedureBase> m_ParallelProcedures = new();
        private readonly List<ParallelProcedureBase> m_ParallelUpdateBuffer = new();

        // UI 面板状态已移至 ProcedureUIProcessor 中管理

        /// <summary>
        /// 当前流程
        /// </summary>
        public MainProcedure CurrentProcedure => m_CurrentProcedure;

        public ProcedureOverlayBase CurrentOverlay => m_CurrentOverlay;

        public bool IsOverlayRunning => m_CurrentOverlay != null;

        public IReadOnlyList<ParallelProcedureBase> CurrentParallelProcedures => m_ParallelProcedures;

        /// <summary>
        /// 当前的子流程
        /// </summary>
        public SubProcedureBase CurrenSubProcedure => m_CurrentProcedure?.CurrentSubProcedure;

        /// <summary>
        /// 切换流程
        /// </summary>
        /// <typeparam name="TProcedure">流程类型</typeparam>
        public void ChangeProcedure<TProcedure>() where TProcedure : MainProcedure
        {
            ChangeProcedure(typeof(TProcedure));
        }

        /// <summary>
        /// 使用强类型 Request 切换流程
        /// </summary>
        public void ChangeProcedure<TProcedure>(IProcedureEnterRequest request) where TProcedure : MainProcedure
        {
            ChangeProcedure(typeof(TProcedure), request);
        }

        /// <summary>
        /// 切换流程
        /// </summary>
        /// <param name="type">流程类型</param>
        public void ChangeProcedure(Type type)
        {
            ChangeProcedure(type, null, false);
        }

        /// <summary>
        /// 使用强类型 Request 切换流程
        /// </summary>
        public void ChangeProcedure(Type type, IProcedureEnterRequest request)
        {
            ChangeProcedure(type, request, true);
        }

        private void ChangeProcedure(Type type, IProcedureEnterRequest request, bool hasRequest)
        {
            if (hasRequest && type == null)
            {
                throw new XFrameworkException("[Procedure] Cannot enter a null procedure with a request.");
            }

            if (hasRequest && request == null)
            {
                throw new XFrameworkException($"[Procedure] Invalid enter request. Procedure: {type.Name}, Request is null.");
            }

            if (type != null && !typeof(MainProcedure).IsAssignableFrom(type))
            {
                throw new XFrameworkException($"[Procedure] {type.Name} is not a MainProcedure and cannot be used as the main procedure.");
            }

            MainProcedure newProcedure = type is null ? null : (MainProcedure)GetOrCreateProcedure(type);
            IProcedureWithRequest requestProcedure = newProcedure as IProcedureWithRequest;
            if (hasRequest && requestProcedure == null)
            {
                throw new XFrameworkException($"[Procedure] Invalid enter request for {type.Name}. Expected procedure implementing IProcedureWithRequest.");
            }

            if (hasRequest && requestProcedure.RequestType != request.GetType())
            {
                throw new XFrameworkException($"[Procedure] Invalid enter request for {type.Name}. Expected: {requestProcedure.RequestType.Name}, Actual: {request.GetType().Name}.");
            }

            if (m_CurrentProcedure == newProcedure)
            {
                return;
            }

            StopOverlay(false);
            StopAllParallelProcedures(false);

            var oldProcedure = m_CurrentProcedure;
            oldProcedure?.Exit();

            m_CurrentProcedure = newProcedure;

            newProcedure?.Enter(oldProcedure);
            if (requestProcedure != null)
            {
                if (hasRequest)
                {
                    requestProcedure.EnterRequestObject(request);
                }
                else
                {
                    requestProcedure.EnterDefaultRequest();
                }
            }

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
                if (procedure is not MainProcedure mainProcedure)
                {
                    throw new XFrameworkException($"[Procedure] {key} is active as the main procedure and must be updated with a MainProcedure instance.");
                }

                m_CurrentProcedure = mainProcedure;
            }

            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                if (key != m_ParallelProcedures[i].GetType().Name)
                {
                    continue;
                }

                if (procedure is not ParallelProcedureBase parallelProcedure)
                {
                    throw new XFrameworkException($"[Procedure] {key} is active as a parallel procedure and must be updated with a parallel procedure instance.");
                }

                m_ParallelProcedures[i] = parallelProcedure;
                break;
            }
        }

        /// <summary>
        /// 获取当前流程
        /// </summary>
        /// <typeparam name="TProcedure">流程类型</typeparam>
        /// <returns>当前流程</returns>
        public TProcedure GetCurrentProcedure<TProcedure>() where TProcedure : MainProcedure
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
        public bool TryGetCurrentProcedure<TProcedure>(out TProcedure procedure) where TProcedure : MainProcedure
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
            StopOverlay(true);
        }

        private void StopOverlay(bool refreshState)
        {
            if (m_CurrentOverlay == null)
            {
                return;
            }

            var overlay = m_CurrentOverlay;
            m_CurrentOverlay = null;
            overlay.OnExit();
            if (refreshState)
            {
                RefreshProcedureState();
            }
        }

        public bool StartParallelProcedure<TProcedure>() where TProcedure : ParallelProcedureBase
        {
            if (TryGetParallelProcedure<TProcedure>(out _))
            {
                return false;
            }

            int priority = GetParallelProcedurePriority(typeof(TProcedure));
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                int activePriority = GetParallelProcedurePriority(m_ParallelProcedures[i].GetType());
                if (activePriority == priority)
                {
                    throw new XFrameworkException($"[Procedure] Parallel priority {priority} is already used by {m_ParallelProcedures[i].GetType().Name}; cannot start {typeof(TProcedure).Name}.");
                }
            }

            var procedure = (TProcedure)GetOrCreateProcedure(typeof(TProcedure));

            int insertIndex = 0;
            while (insertIndex < m_ParallelProcedures.Count &&
                   GetParallelProcedurePriority(m_ParallelProcedures[insertIndex].GetType()) < priority)
            {
                insertIndex++;
            }

            m_ParallelProcedures.Insert(insertIndex, procedure);
            procedure.Enter(null);
            if (!m_ParallelProcedures.Contains(procedure))
            {
                return false;
            }

            procedure.OnPrepare(() =>
            {
                if (!m_ParallelProcedures.Contains(procedure))
                {
                    return;
                }

                RefreshProcedureState();
            });
            return true;
        }

        public bool StopParallelProcedure<TProcedure>() where TProcedure : ParallelProcedureBase
        {
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                if (m_ParallelProcedures[i] is not TProcedure procedure)
                {
                    continue;
                }

                m_ParallelProcedures.RemoveAt(i);
                procedure.Exit();
                RefreshProcedureState();
                return true;
            }

            return false;
        }

        public bool StartParallelProcedure(Type type)
        {
            if (!typeof(ParallelProcedureBase).IsAssignableFrom(type))
            {
                UnityEngine.Debug.LogError($"[Procedure] Type {type.Name} is not a ParallelProcedureBase.");
                return false;
            }

            if (TryGetParallelProcedure(type, out _))
            {
                return false;
            }

            int priority = GetParallelProcedurePriority(type);
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                int activePriority = GetParallelProcedurePriority(m_ParallelProcedures[i].GetType());
                if (activePriority == priority)
                {
                    throw new XFrameworkException($"[Procedure] Parallel priority {priority} is already used by {m_ParallelProcedures[i].GetType().Name}; cannot start {type.Name}.");
                }
            }

            var procedure = (ParallelProcedureBase)GetOrCreateProcedure(type);

            int insertIndex = 0;
            while (insertIndex < m_ParallelProcedures.Count &&
                   GetParallelProcedurePriority(m_ParallelProcedures[insertIndex].GetType()) < priority)
            {
                insertIndex++;
            }

            m_ParallelProcedures.Insert(insertIndex, procedure);
            procedure.Enter(null);
            if (!m_ParallelProcedures.Contains(procedure))
            {
                return false;
            }

            procedure.OnPrepare(() =>
            {
                if (!m_ParallelProcedures.Contains(procedure))
                {
                    return;
                }

                RefreshProcedureState();
            });
            return true;
        }

        public bool StopParallelProcedure(Type type)
        {
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                if (m_ParallelProcedures[i].GetType() != type)
                {
                    continue;
                }

                var procedure = m_ParallelProcedures[i];
                m_ParallelProcedures.RemoveAt(i);
                procedure.Exit();
                RefreshProcedureState();
                return true;
            }

            return false;
        }

        public bool TryGetParallelProcedure(Type type, out ParallelProcedureBase procedure)
        {
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                if (m_ParallelProcedures[i].GetType() == type)
                {
                    procedure = m_ParallelProcedures[i];
                    return true;
                }
            }

            procedure = null;
            return false;
        }

        public bool ContainsParallelProcedure(ParallelProcedureBase procedure)
        {
            return m_ParallelProcedures.Contains(procedure);
        }

        public bool TryGetParallelProcedure<TProcedure>(out TProcedure procedure) where TProcedure : ParallelProcedureBase
        {
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                if (m_ParallelProcedures[i] is TProcedure activeProcedure)
                {
                    procedure = activeProcedure;
                    return true;
                }
            }

            procedure = null;
            return false;
        }

        public void StopAllParallelProcedures()
        {
            StopAllParallelProcedures(true);
        }

        private void StopAllParallelProcedures(bool refreshState)
        {
            for (int i = m_ParallelProcedures.Count - 1; i >= 0; i--)
            {
                var procedure = m_ParallelProcedures[i];
                m_ParallelProcedures.RemoveAt(i);
                procedure.Exit();
            }

            if (refreshState)
            {
                RefreshProcedureState();
            }
        }

        public void Update()
        {
            m_CurrentProcedure?.OnUpdate();

            m_ParallelUpdateBuffer.Clear();
            m_ParallelUpdateBuffer.AddRange(m_ParallelProcedures);
            for (int i = 0; i < m_ParallelUpdateBuffer.Count; i++)
            {
                var procedure = m_ParallelUpdateBuffer[i];
                if (m_ParallelProcedures.Contains(procedure))
                {
                    procedure.OnUpdate();
                }
            }
            m_ParallelUpdateBuffer.Clear();

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

        private int GetParallelProcedurePriority(Type type)
        {
            var priorityAttribute = GetContext(type).ParallelPriorityAttr;
            if (priorityAttribute == null || priorityAttribute.Priority <= 0)
            {
                throw new XFrameworkException($"[Procedure] Parallel procedure {type.Name} must declare a positive ParallelProcedurePriorityAttribute.");
            }

            return priorityAttribute.Priority;
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
            var parallelBranches = new List<ProcedureBranchContext>(m_ParallelProcedures.Count);
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                var parallelProcedure = m_ParallelProcedures[i];
                var parallelSubProcedure = parallelProcedure.CurrentSubProcedure;
                parallelBranches.Add(new ProcedureBranchContext(
                    parallelProcedure,
                    parallelSubProcedure,
                    GetContext(parallelProcedure.GetType()),
                    parallelSubProcedure != null ? GetContext(parallelSubProcedure.GetType()) : null,
                    GetParallelProcedurePriority(parallelProcedure.GetType())));
            }

            var context = new ProcedureRefreshContext(
                m_CurrentProcedure,
                subProcedure,
                m_CurrentOverlay,
                parentContext,
                subContext,
                overlayContext,
                parallelBranches);

            foreach (var processor in m_Processors)
            {
                processor.OnRefreshProcedureState(context);
            }
        }
    }
}
