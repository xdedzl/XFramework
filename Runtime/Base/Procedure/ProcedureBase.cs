using System.Collections.Generic;
using XFramework.Event;
using XFramework.Fsm;

namespace XFramework
{
    /// <summary>
    /// 流程基类
    /// </summary>
    [System.Serializable]
    public abstract class ProcedureBase : FsmState 
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

        public virtual void OnEnter(ProcedureBase preProcedure)
        {

        }

        public override void OnUpdate()
        {
            m_currentSubProcedure?.OnUpdate();
        }

        public override void OnExit()
        {
            m_currentSubProcedure?.OnExit();
        }

        /// <summary>
        /// 切换子流程
        /// </summary>
        /// <typeparam name="T">子流程类型</typeparam>
        /// <param name="args">参数列表</param>
        public void ChangeSubProcedure<T>(params object[] args) where T : SubProcedureBase, new()
        {
            m_subProcedureBases ??= new List<SubProcedureBase>();

            m_currentSubProcedure?.OnExit();
            if(typeof(T) == m_currentSubProcedure?.GetType())
            {
                return;
            }

            foreach (var item in m_subProcedureBases)
            {
                if(item.GetType() == typeof(T))
                {
                    m_currentSubProcedure = item;
                    m_currentSubProcedure.OnEnter(args);
                    return;
                }
            }

            m_currentSubProcedure = new T();
            m_currentSubProcedure._parent = this;
            m_currentSubProcedure.OnInit();
            m_currentSubProcedure.OnEnter(args);
            m_subProcedureBases.Add(m_currentSubProcedure);
        }

        /// <summary>
        /// 将当前子流程置为空
        /// </summary>
        public void ChangeSubProcedure2None()
        {
            m_currentSubProcedure?.OnExit();
            m_currentSubProcedure = null;
        }
    }

    public abstract class ProcedureWithEvent : ProcedureBase
    {
        private readonly EventRegisterHelper _registerHelper;

        public ProcedureWithEvent()
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