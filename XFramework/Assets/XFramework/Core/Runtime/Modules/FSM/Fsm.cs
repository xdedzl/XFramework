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
        private bool m_IsActive;
        private FsmState m_CurrentState;

        /// <summary>
        /// 存储该状态机包含的所有状态
        /// </summary>
        protected Dictionary<string, FsmState> m_StateDic;

        public bool IsActive { get { return m_IsActive; } }
        public FsmState CurrentState { get { return m_CurrentState; } }

        public Fsm()
        {
            m_StateDic = new Dictionary<string, FsmState>();
            m_IsActive = false;
        }

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
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected FsmState GetState<T>()
        {
            return GetState(typeof(T));
        }

        /// <summary>
        /// 获取一个状态
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        protected FsmState GetState(Type type)
        {
            m_StateDic.TryGetValue(type.Name, out FsmState state);

            if (state == null)
            {
                state = CreateState(type);
                m_StateDic.Add(type.Name, state);
            }

            return state;
        }

        /// <summary>
        /// 创建一个状态
        /// </summary>
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
            FsmState state = Utility.Reflection.CreateInstance<FsmState>(type);

            if (!(state is TState))
                throw new System.Exception("状态类型设置错误");

            state.Init();
            return state;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public FsmState GetCurrentState()
        {
            return CurrentState;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public T GetCurrentState<T>() where T : TState
        {
            return CurrentState as T;
        }

        /// <summary>
        /// 开启状态机
        /// </summary>
        private void StartFsm(Type type)
        {
            if (!IsActive)
            {
                m_CurrentState = GetState(type);
                m_CurrentState.OnEnter();
                m_IsActive = true;
            }
        }

        /// <summary>
        /// 状态切换
        /// </summary>
        /// <typeparam name="KState">目标状态</typeparam>
        public void ChangeState<KState>() where KState : FsmState
        {
            ChangeState(typeof(KState));
        }

        /// <summary>
        /// 状态切换
        /// </summary>
        /// <param name="type">目标状态</param>
        public void ChangeState(Type type)
        {
            if (IsActive)
            {
                FsmState tempstate = GetState(type);

                if (m_CurrentState != tempstate)
                {
                    m_CurrentState?.OnExit();
                    m_CurrentState = tempstate;
                    m_CurrentState.OnEnter();
                }
            }
            else
            {
                StartFsm(type);
            }
        }

        public void OnDestroy()
        {
            m_CurrentState = null;
            m_StateDic.Clear();
        }
    }
}