using System;

namespace XFramework
{
    /// <summary>
    /// 流程的优先级应比状态机低
    /// </summary>
    public class ProcedureManager : IGameModule
    {
        /// <summary>
        /// 流程状态机
        /// </summary>
        private readonly ProcedureFsm m_Fsm;

        public ProcedureManager()
        {
            m_Fsm = new ProcedureFsm();
        }

        /// <summary>
        /// 切换流程
        /// </summary>
        /// <typeparam name="TProcedure">流程类型</typeparam>
        /// <param name="parms">参数</param>
        public void ChangeProcedure<TProcedure>(params object[] parms) where TProcedure : ProcedureBase
        {
            m_Fsm.ChangeState<TProcedure>(parms);
        }

        /// <summary>
        /// 切换流程
        /// </summary>
        /// <param name="type">流程类型</param>
        /// <param name="parms">参数</param>
        public void ChangeProcedure(Type type, params object[] parms)
        {
            m_Fsm.ChangeState(type, parms);
        }

        /// <summary>
        /// 获取当前流程
        /// </summary>
        /// <returns>当前流程</returns>
        public ProcedureBase GetCurrentProcedure()
        {
            return m_Fsm.GetCurrentState() as ProcedureBase;
        }

        /// <summary>
        /// 获取当前流程
        /// </summary>
        /// <typeparam name="TProcedure">流程类型</typeparam>
        /// <returns>当前流程</returns>
        public TProcedure GetCurrentProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            return m_Fsm.GetCurrentState() as TProcedure;
        }

        #region 接口实现

        public int Priority { get { return 1; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            m_Fsm.OnUpdate();
        }

        public void Shutdown()
        {

        }

        #endregion
    }
}