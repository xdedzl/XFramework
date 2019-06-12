using System;

namespace XFramework
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
        /// 从某一状态开始一个状态机
        /// </summary>
        /// <typeparam name="T"></typeparam>
        void StartFsm<TState>() where TState : FsmState;
        void StartFsm(Type type);

        /// <summary>
        /// 状态切换
        /// </summary>
        void ChangeState<T>() where T : FsmState;
        void ChangeState(Type type);
    }
}