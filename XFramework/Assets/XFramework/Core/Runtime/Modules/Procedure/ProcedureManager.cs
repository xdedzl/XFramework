using System;
using XFramework.Fsm;

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
        private ProcedureFsm m_Fsm;

        public ProcedureManager()
        {
            var fsmMoudle = GameEntry.GetModule<FsmManager>();
            if (fsmMoudle != null)
                m_Fsm = fsmMoudle.GetFsm<ProcedureFsm>();
            else
                throw new Exception("初始化Proceedure之前需要初始化FSM");
        }

        /// <summary>
        /// 流程切换
        /// </summary>
        /// <typeparam name="TProcedure"></typeparam>
        public void ChangeProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            m_Fsm.ChangeState<TProcedure>();
        }

        public void ChangeProcedure(Type type)
        {
            m_Fsm.ChangeState(type);
        }

        /// <summary>
        /// 获取当前流程
        /// </summary>
        /// <returns></returns>
        public ProcedureBase GetCurrentProcedure()
        {
            return m_Fsm.GetCurrentState() as ProcedureBase;
        }

        public int Priority { get { return 1; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {

        }

        public void Init()
        {

        }

        public void Shutdown()
        {

        }
    }
}