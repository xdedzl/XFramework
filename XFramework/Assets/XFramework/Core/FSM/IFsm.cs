using System;

namespace XFramework.Fsm
{
    /// <summary>
    /// 状态机接口
    /// </summary>
    public interface IFsm
    {
        /// <summary>
        /// 每帧调用
        /// </summary>
        void OnUpdate();
        /// <summary>
        /// 状态切换
        /// </summary>
        /// <typeparam name="T">状态类型</typeparam>
        /// <param name="parms">启动参数</param>
        void ChangeState<T>(params object[] parms) where T : FsmState;
        /// <summary>
        /// 状态切换
        /// </summary>
        /// <param name="type">状态类型</param>
        /// <param name="parms">启动参数</param>
        void ChangeState(Type type, params object[] parms);
        /// <summary>
        /// 获取当前状态
        /// </summary>
        FsmState GetCurrentState();
        /// <summary>
        /// 状态机销毁
        /// </summary>
        void OnDestroy();
    }
}