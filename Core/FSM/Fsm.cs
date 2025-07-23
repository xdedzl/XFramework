using System;
using System.Collections.Generic;

namespace XFramework.Fsm
{
    /// <summary>
    /// 状态机
    /// </summary>
    /// <typeparam name="TState">子类状态机对应的状态基类</typeparam>
    public class Fsm<TState> : IFsm where TState : FsmState
    {
        private TState m_CurrentState;

        /// <summary>
        /// 存储该状态机包含的所有状态
        /// </summary>
        protected Dictionary<string, TState> m_StateDic;

        /// <summary>
        /// 当前状态
        /// </summary>
        public TState CurrentState { get { return m_CurrentState; } }

        public Fsm()
        {
            m_StateDic = new Dictionary<string, TState>();
        }

        /// <summary>
        /// 每帧运行
        /// </summary>
        public virtual void OnUpdate()
        {
            if (CurrentState != null)
            {
                CurrentState.OnUpdate(); 
            }
        }

        /// <summary>
        /// 获取一个状态
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <returns>状态</returns>
        protected FsmState GetState<T>()
        {
            return GetState(typeof(T));
        }

        /// <summary>
        /// 获取一个状态
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <returns>状态</returns>
        protected TState GetState(Type type)
        {
            m_StateDic.TryGetValue(type.Name, out TState state);
            if (state == null)
            {
                state = CreateState(type) as TState;
                m_StateDic.Add(type.Name, state);
            }

            return state;
        }

        /// <summary>
        /// 创建一个状态
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <returns>状态</returns>
        protected FsmState CreateState<T>()
        {
            return CreateState(typeof(T));
        }

        /// <summary>
        /// 创建一个状态
        /// </summary>
        /// <param name="type">状态类型</param>
        protected FsmState CreateState(Type type)
        {
            FsmState state = Utility.Reflection.CreateInstance<TState>(type);

            if (!(state is TState))
                throw new XFrameworkException("[FSM] state type error");

            return state;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        /// <returns></returns>
        public FsmState GetCurrentState()
        {
            return m_CurrentState;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public T GetCurrentState<T>() where T : TState
        {
            return m_CurrentState as T;
        }

        /// <summary>
        /// 状态切换
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <param name="parms">启动参数</param>
        public void ChangeState<T>(params object[] parms) where T : FsmState
        {
            ChangeState(typeof(T), parms);
        }

        /// <summary>
        /// 状态切换
        /// </summary>
        /// <param name="type">状态类型</param>
        /// <param name="parms">启动参数</param>
        public void ChangeState(Type type, params object[] parms)
        {
            TState newState = GetState(type);
            if (m_CurrentState != newState)
            {
                m_CurrentState?.OnExit();
                if (!newState.isInit)
                {
                    newState.OnInit();
                    newState.isInit = true;
                }
                OnStateChange(m_CurrentState, newState);
                m_CurrentState = newState;
                m_CurrentState.OnEnter(parms);
            }
        }

        /// <summary>
        /// 新增一个状态
        /// </summary>
        /// <param name="state">状态</param>
        public void UpdateState(TState state)
        {
            var key = state.GetType().Name;
            m_StateDic[key] = state;

            if (m_CurrentState != null && key == m_CurrentState.GetType().Name)
            {
                m_CurrentState = state;
            }
        }

        protected virtual void OnStateChange(TState oldStart, TState newState) { }

        public void OnDestroy()
        {
            m_CurrentState = null;
            m_StateDic.Clear();
        }
    }
}