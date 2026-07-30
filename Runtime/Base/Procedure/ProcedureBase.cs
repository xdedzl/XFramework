using System;
using System.Collections.Generic;
using XFramework.Event;

namespace XFramework
{
    /// <summary>
    /// 流程基类
    /// </summary>
    [System.Serializable]
    public abstract class ProcedureBase
    {
        private List<SubProcedureBase> m_subProcedureBases;
        private SubProcedureBase m_currentSubProcedure;
        public SubProcedureBase CurrentSubProcedure
        {
            get
            {
                return m_currentSubProcedure;
            }
        }

        public virtual void OnInit() { }

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
            m_currentSubProcedure?.OnExit();
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

            m_currentSubProcedure?.OnExit();
            foreach (var item in m_subProcedureBases)
            {
                if(item.GetType() == typeof(T))
                {
                    m_currentSubProcedure = item;
                    m_currentSubProcedure.OnEnter(args);
                    ProcedureManager.Instance.RefreshProcedureState();
                    return;
                }
            }

            m_currentSubProcedure = new T();
            m_currentSubProcedure._parent = this;
            m_currentSubProcedure.OnInit();
            m_currentSubProcedure.OnEnter(args);
            m_subProcedureBases.Add(m_currentSubProcedure);
            ProcedureManager.Instance.RefreshProcedureState();
        }

        /// <summary>
        /// 将当前子流程置为空
        /// </summary>
        public void ChangeSubProcedure2None()
        {
            m_currentSubProcedure?.OnExit();
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

    public abstract class ProcedureWithEvent : ProcedureBase
    {
        private readonly EventRegisterHelper _registerHelper;

        public ProcedureWithEvent()
        {
            _registerHelper = EventRegisterHelper.Create(this);
        }

        public override void OnEnter(ProcedureBase preProcedure)
        {
            _registerHelper.Register();
        }

        public override void OnExit()
        {
            base.OnExit();
            _registerHelper.UnRegister();
        }
    }

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


    /// <summary>
    /// 子流程基类
    /// </summary>
    public abstract class SubProcedureBase
    {
        internal ProcedureBase _parent;

        public virtual void OnInit() { }
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

    public abstract class SubProcedureWithEvent<T> : SubProcedureBase<T> where T : ProcedureBase
    {
        private readonly EventRegisterHelper _registerHelper;

        public SubProcedureWithEvent()
        {
            _registerHelper = EventRegisterHelper.Create(this);
        }

        public override void OnEnter(params object[] parms)
        {
            _registerHelper.Register();
        }

        public override void OnExit()
        {
            _registerHelper.UnRegister();
        }
    }
}
