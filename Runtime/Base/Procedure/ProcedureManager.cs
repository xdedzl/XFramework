﻿using System;

namespace XFramework
{
    /// <summary>
    /// 流程的优先级应比状态机低
    /// </summary>
    public class ProcedureManager : MonoSingleton<ProcedureManager>
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
            var current = m_Fsm.GetCurrentState();
            if (current is TProcedure procedure)
            {
                return procedure;
            }
            else
            {
                throw new FrameworkException($"[Procedure] 当前流程不是{typeof(TProcedure).Name}");
            }
        }

        public void Update()
        {
            m_Fsm.OnUpdate();
        }
    }
}