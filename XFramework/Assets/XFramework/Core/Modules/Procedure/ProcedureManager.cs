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
        /// 开启一个流程
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        public void StartProcedure<TState>() where TState : ProcedureBase
        {
            m_Fsm.StartFsm<TState>();
        }

        public void StartProcedure(Type type)
        {
            m_Fsm.StartFsm(type);
        }

        /// <summary>
        /// 流程切换
        /// </summary>
        /// <typeparam name="TProcedure"></typeparam>
        public void ChangeProcedure<TProcedure>() where TProcedure : ProcedureBase
        {
            m_Fsm.ChangeState<TProcedure>();
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