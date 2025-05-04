using System.Collections.Generic;
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

        /// <summary>
        /// 切换子流程
        /// </summary>
        /// <typeparam name="T">子流程类型</typeparam>
        /// <param name="args">参数列表</param>
        public void ChangeSubProcedure<T>(params object[] args) where T : SubProcedureBase, new()
        {
            m_subProcedureBases = m_subProcedureBases ?? new List<SubProcedureBase>();

            m_currentSubProcedure?.OnExit();

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

    /// <summary>
    /// 子流程基类
    /// </summary>
    public abstract class SubProcedureBase : FsmState { }
}