using System;

namespace XFramework.Fsm
{
    /// <summary>
    /// 向外暴露销毁事件，供运行时管理器做自动注销。
    /// </summary>
    public interface IFsmDisposableNotifier
    {
        event Action Disposed;
    }
}
