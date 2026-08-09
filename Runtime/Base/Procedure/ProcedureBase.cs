using System;
using System.Collections.Generic;
using XFramework.Event;

namespace XFramework
{
    #region 流程基类和接口 
    public interface IProcedureEnterRequest
    {
    }

    public interface IProcedureWithRequest
    {
        internal Type RequestType { get; }
        internal void EnterDefaultRequest();
        internal void EnterRequestObject(IProcedureEnterRequest request);
    }

    public interface IProcedureWithRequest<TRequest> : IProcedureWithRequest where TRequest : struct, IProcedureEnterRequest
    {

    }
    
    [Serializable]
    public abstract class ProcedureBase
    {
        private List<SubProcedureBase> m_subProcedureBases;
        private SubProcedureBase m_currentSubProcedure;
        private readonly EventRegisterHelper m_EventRegisterHelper;

        protected ProcedureBase()
        {
            m_EventRegisterHelper = EventRegisterHelper.Create(this);
        }

        public SubProcedureBase CurrentSubProcedure
        {
            get
            {
                return m_currentSubProcedure;
            }
        }

        public virtual void OnInit() { }

        internal void Enter(ProcedureBase preProcedure)
        {
            m_EventRegisterHelper.Register();
            OnEnter(preProcedure);
        }

        internal void Exit()
        {
            OnExit();
            m_EventRegisterHelper.UnRegister();
        }

        public virtual void OnEnter(ProcedureBase preProcedure)
        {

        }

        /// <summary>
        /// 流程进入后的异步准备阶段。
        /// 默认实现直接调用 onReady；子类可重写以执行场景加载等异步操作，完成后再调用 onReady。
        /// onReady 触发后框架才会执行 Module 加载和 UI 打开。
        /// </summary>
        public virtual void OnPrepare(Action onReady)
        {
            onReady?.Invoke();
        }

        public virtual void OnUpdate()
        {
            m_currentSubProcedure?.OnUpdate();
        }

        public virtual void OnExit()
        {
            m_currentSubProcedure?.Exit();
            m_currentSubProcedure = null;
        }

        /// <summary>
        /// 切换子流程
        /// </summary>
        /// <typeparam name="T">子流程类型</typeparam>
        /// <param name="args">参数列表</param>
        public void ChangeSubProcedure<T>(params object[] args) where T : SubProcedureBase, new()
        {
            m_subProcedureBases ??= new List<SubProcedureBase>();

            if(typeof(T) == m_currentSubProcedure?.GetType())
            {
                return;
            }

            m_currentSubProcedure?.Exit();
            foreach (var item in m_subProcedureBases)
            {
                if(item.GetType() == typeof(T))
                {
                    m_currentSubProcedure = item;
                    m_currentSubProcedure.Enter(args);
                    ProcedureManager.Instance.RefreshProcedureState();
                    return;
                }
            }

            m_currentSubProcedure = new T();
            m_currentSubProcedure._parent = this;
            m_currentSubProcedure.OnInit();
            m_currentSubProcedure.Enter(args);
            m_subProcedureBases.Add(m_currentSubProcedure);
            ProcedureManager.Instance.RefreshProcedureState();
        }

        /// <summary>
        /// 将当前子流程置为空
        /// </summary>
        public void ChangeSubProcedure2None()
        {
            m_currentSubProcedure?.Exit();
            m_currentSubProcedure = null;
            ProcedureManager.Instance.RefreshProcedureState();
        }

        internal IReadOnlyList<SubProcedureBase> GetDebugSubProcedures()
        {
            return m_subProcedureBases == null
                ? Array.Empty<SubProcedureBase>()
                : m_subProcedureBases.ToArray();
        }
    }
    #endregion

    #region 主流程
    public abstract class MainProcedure : ProcedureBase
    {
    }

    public abstract class MainProcedure<TRequest> : MainProcedure, IProcedureWithRequest<TRequest> where TRequest : struct, IProcedureEnterRequest
    {
        Type IProcedureWithRequest.RequestType => typeof(TRequest);

        void IProcedureWithRequest.EnterDefaultRequest()
        {
            TRequest request = CreateDefaultEnterRequest();
            OnEnter(in request);
        }

        void IProcedureWithRequest.EnterRequestObject(IProcedureEnterRequest request)
        {
            if (request is not TRequest typedRequest)
            {
                throw new XFrameworkException($"[Procedure] Invalid enter request for {GetType().Name}. Expected: {typeof(TRequest).Name}.");
            }

            OnEnter(in typedRequest);
        }

        protected virtual TRequest CreateDefaultEnterRequest()
        {
            return default;
        }

        protected abstract void OnEnter(in TRequest request);
    }
    #endregion

    #region 并行流程
    /// <summary>
    /// 并行根流程基类。并行根流程与主流程同时运行，但不隶属于主流程。
    /// </summary>
    public abstract class ParallelProcedureBase : ProcedureBase
    {
    }

    /// <summary>
    /// 带 XScene 的并行根流程基类，派生类设置 XScene 资源路径。
    /// 流程启动时自动加载 XScene，停止时自动卸载。
    /// </summary>
    public abstract class SceneParallelProcedureBase : ParallelProcedureBase
    {
        /// <summary>
        /// XScene 资源路径
        /// </summary>
        public abstract string XScenePath { get; }

        /// <summary>
        /// 异步加载 XScene，完成后调用 onReady 以触发 Module/UI 处理
        /// </summary>
        public override void OnPrepare(Action onReady)
        {
            PrepareXSceneAsync(onReady);
        }

        private async void PrepareXSceneAsync(Action onReady)
        {
            bool loaded = await XSceneManager.LoadSceneAsync(XScenePath);
            if (!loaded)
            {
                UnityEngine.Debug.LogError($"[Procedure] Load XScene failed. procedure:{GetType().Name}, xScenePath:{XScenePath}.");
                return;
            }

            if (!ProcedureManager.IsValid || !ProcedureManager.Instance.ContainsParallelProcedure(this))
            {
                return;
            }

            onReady?.Invoke();
            OnXSceneLoaded();
        }

        /// <summary>
        /// 流程停止时卸载关联的 XScene
        /// </summary>
        public override void OnExit()
        {
            base.OnExit();
            UnloadXSceneAsync();
        }

        private async void UnloadXSceneAsync()
        {
            if (!XSceneManager.IsSceneLoaded(XScenePath))
            {
                return;
            }
            await XSceneManager.UnloadSceneAsync(XScenePath);
        }

        /// <summary>
        /// XScene 加载完成后调用，此时 Module 和 UI 已经就绪
        /// </summary>
        public virtual void OnXSceneLoaded() { }
    }

    /// <summary>
    /// 声明并行根流程的合成与更新优先级。优先级必须大于 0，且运行期间必须唯一。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ParallelProcedurePriorityAttribute : Attribute
    {
        public int Priority { get; }

        public ParallelProcedurePriorityAttribute(int priority)
        {
            Priority = priority;
        }
    }
    #endregion

    #region 覆盖流程
    /// <summary>
    /// 流程覆盖层基类，用于在当前主流程之上临时叠加短生命周期玩法或交互。
    /// </summary>
    public abstract class ProcedureOverlayBase
    {
        public virtual void OnInit() { }

        public virtual void OnEnter(params object[] args) { }

        public virtual void OnPrepare(Action onReady)
        {
            onReady?.Invoke();
        }

        public virtual void OnUpdate() { }

        public virtual void OnExit() { }
    }
    #endregion
    
    #region 子流程
    /// <summary>
    /// 子流程基类
    /// </summary>
    public abstract class SubProcedureBase
    {
        internal ProcedureBase _parent;
        private readonly EventRegisterHelper m_EventRegisterHelper;

        protected SubProcedureBase()
        {
            m_EventRegisterHelper = EventRegisterHelper.Create(this);
        }

        public virtual void OnInit() { }

        internal void Enter(params object[] parms)
        {
            m_EventRegisterHelper.Register();
            OnEnter(parms);
        }

        internal void Exit()
        {
            OnExit();
            m_EventRegisterHelper.UnRegister();
        }

        /// <summary>
        /// 进入该状态
        /// </summary>
        /// <param name="parms">启动参数</param>
        public virtual void OnEnter(params object[] parms) { }

        /// <summary>
        /// 每帧运行
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// 离开该状态
        /// </summary>
        public virtual void OnExit() { }
    }

    public abstract class SubProcedureBase<T>: SubProcedureBase where T : ProcedureBase
    {
        public T Parent
        {
            get
            {
                return _parent as T;
            }
        }
    }
    #endregion
}
