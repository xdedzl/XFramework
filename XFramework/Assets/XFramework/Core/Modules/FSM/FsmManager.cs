using System;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 状态机管理类
    /// </summary>
    public class FsmManager : IGameModule
    {
        /// <summary>
        /// 存储所有状态机的字典
        /// </summary>
        private Dictionary<string, IFsm> m_FsmDic;

        /// <summary>
        /// 状态机的数量
        /// </summary>
        public int Count
        {
            get
            {
                return m_FsmDic.Count;
            }
        }

        public FsmManager()
        {
            m_FsmDic = new Dictionary<string, IFsm>();
        }

        /// <summary>
        /// 每帧调用处于激活状态的状态机
        /// </summary>
        public void OnUpdate()
        {
            foreach (var fsm in m_FsmDic.Values)
            {
                if (fsm.IsActive)
                    fsm.OnUpdate();
            }
        }

        /// <summary>
        /// 是否包含某种状态机
        /// </summary>
        public bool HasFsm<T>() where T : IFsm
        {
            return HasFsm(typeof(T));
        }

        /// <summary>
        /// 是否包含某种状态机
        /// </summary>
        public bool HasFsm(Type type)
        {
            return m_FsmDic.ContainsKey(type.Name);
        }

        /// <summary>
        /// 获取一个状态机
        /// </summary>
        /// <typeparam name="TFsm"></typeparam>
        public TFsm GetFsm<TFsm>() where TFsm : class, IFsm
        {
            if (!HasFsm<TFsm>())
            {
                CreateFsm<TFsm>();
            }
            return (TFsm)m_FsmDic[typeof(TFsm).Name];
        }

        /// <summary>
        /// 获取对应状态机当前所处的状态
        /// </summary>
        /// <typeparam name="TFsm"></typeparam>
        public FsmState GetCurrentState<TFsm>() where TFsm : IFsm
        {
            if (HasFsm<TFsm>())
            {
                return m_FsmDic[typeof(TFsm).Name].CurrentState;
            }
            else
            {
                return null;
            }
        }

        public TState GetCurrentState<TFsm, TState>() where TFsm : IFsm where TState : FsmState
        {
            return GetCurrentState<TFsm>() as TState;
        }

        /// <summary>
        /// 开启一个状态机
        /// </summary>
        /// <typeparam name="TFsm"></typeparam>
        /// <typeparam name="KState"></typeparam>
        public void StartFsm<TFsm, KState>() where TFsm : class, IFsm where KState : FsmState
        {
            GetFsm<TFsm>()?.StartFsm<KState>();
        }

        /// <summary>
        /// 切换对应状态机到对应状态
        /// </summary>
        /// <typeparam name="TFsm">状态机类型</typeparam>
        /// <typeparam name="KState">目标状态</typeparam>
        public void ChangeState<TFsm, KState>() where TFsm : class, IFsm where KState : FsmState
        {
            if (!HasFsm<TFsm>())
            {
                CreateFsm<TFsm>();
            }
            m_FsmDic[typeof(TFsm).Name].ChangeState<KState>();
        }
        public void ChanegState(Type typeFsm, Type typeState)
        {
            if (!typeFsm.IsSubclassOf(typeof(IFsm)) || !typeState.IsSubclassOf(typeof(FsmState)))
            {
                throw new System.Exception("类型传入错误");
            }

            if (!HasFsm(typeFsm))
            {

            }
        }

        /// <summary>
        /// 根据类型创建一个状态机
        /// </summary>
        public void CreateFsm<T>() where T : class, IFsm
        {
            m_FsmDic.Add(typeof(T).Name, Utility.Reflection.CreateInstance<T>());
        }

        #region 接口实现

        public int Priority { get { return 0; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            OnUpdate();
        }

        public void Shutdown()
        {

        }

        #endregion
    }
}