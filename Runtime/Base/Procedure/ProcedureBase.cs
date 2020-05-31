using System.Collections.Generic;
using XFramework.Fsm;

namespace XFramework
{
    /// <summary>
    /// 流程基类
    /// </summary>
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
    }

    /// <summary>
    /// 子流程基类
    /// </summary>
    public abstract class SubProcedureBase : FsmState { }
}