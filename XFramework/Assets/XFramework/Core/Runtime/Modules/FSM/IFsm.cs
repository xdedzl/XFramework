using System;

namespace XFramework.Fsm
{
    /// <summary>
    /// 状态机接口
    /// </summary>
    public interface IFsm
    {
        /// <summary>
        /// 状态机当前状态
        /// </summary>
        FsmState CurrentState { get; }
        /// <summary>
        /// 状态机是否激活
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 每帧调用
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// 状态切换
        /// </summary>
        void ChangeState<T>(params object[] parms) where T : FsmState;
        void ChangeState(Type type, params object[] parms);

        void OnDestroy();
    }
}