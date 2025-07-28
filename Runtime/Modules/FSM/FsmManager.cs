using System;
using System.Collections.Generic;

namespace XFramework.Fsm
{
    /// <summary>
    /// 状态机管理类
    /// </summary>
    public class FsmManager : GameModuleBase<FsmManager>
    {
        /// <summary>
        /// 存储所有状态机的字典
        /// </summary>
        private readonly Dictionary<string, IFsm> m_FsmDic = new Dictionary<string, IFsm>();

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
        /// <typeparam name="TFsm">状态机类型</typeparam>
        public FsmState GetCurrentState<TFsm>() where TFsm : IFsm
        {
            if (HasFsm<TFsm>())
            {
                return m_FsmDic[typeof(TFsm).Name].GetCurrentState();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 获取对应状态机当前所处的状态
        /// </summary>
        /// <typeparam name="TFsm">状态机类型</typeparam>
        /// <typeparam name="TState">目标状态</typeparam>
        /// <returns></returns>
        public TState GetCurrentState<TFsm, TState>() where TFsm : IFsm where TState : FsmState
        {
            return GetCurrentState<TFsm>() as TState;
        }

        /// <summary>
        /// 切换对应状态机到对应状态
        /// </summary>
        /// <typeparam name="TFsm">状态机类型</typeparam>
        /// <typeparam name="KState">目标状态</typeparam>
        /// <param name="parms">参数</param>
        public void ChangeState<TFsm, KState>(params object[] parms) where TFsm : class, IFsm where KState : FsmState
        {
            if (!HasFsm<TFsm>())
            {
                CreateFsm<TFsm>();
            }
            m_FsmDic[typeof(TFsm).Name].ChangeState<KState>(parms);
        }

        /// <summary>
        /// 切换对应状态机到对应状态
        /// </summary>
        /// <param name="typeFsm">状态机类型</param>
        /// <param name="typeState">目标状态</param>
        /// <param name="parms">参数</param>
        public void ChangeState(Type typeFsm, Type typeState, params object[] parms)
        {
            if (!typeFsm.IsSubclassOf(typeof(IFsm)) || !typeState.IsSubclassOf(typeof(FsmState)))
            {
                throw new System.Exception("[FSM] type error");
            }

            if (!HasFsm(typeFsm))
            {
                CreateFsm(typeFsm);
            }
            m_FsmDic[typeFsm.Name].ChangeState(typeState, parms);
        }

        /// <summary>
        /// 根据类型创建一个状态机
        /// </summary>
        public T CreateFsm<T>() where T : class, IFsm
        {
            return CreateFsm(typeof(T)) as T;
        }

        /// <summary>
        /// 创建一个状态机
        /// </summary>
        /// <param name="type">状态机类型</param>
        /// <returns>状态机</returns>
        public IFsm CreateFsm(Type type)
        {
            if (type.IsSubclassOf(type.GetType()))
            {
                throw new Exception($"{type.Name}不继承IFsm");
            }
            IFsm fsm = Activator.CreateInstance(type) as IFsm;
            m_FsmDic.Add(type.Name, fsm);
            return fsm;
        }

        /// <summary>
        /// 删除一个状态机
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void DestroyFsm<T>() where T : class, IFsm
        {
            DestroyFsm(typeof(T));
        }

        /// <summary>
        /// 删除一个状态机
        /// </summary>
        /// <param name="type">状态机类型</param>
        public void DestroyFsm(Type type)
        {
            if (HasFsm(type))
            {
                m_FsmDic.Remove(type.Name);
            }
        }

        #region 接口实现

        public override int Priority { get { return 0; } }

        public override void Update()
        {
            foreach (var fsm in m_FsmDic.Values)
            {
                fsm.OnUpdate();
            }
        }

        #endregion
    }
}