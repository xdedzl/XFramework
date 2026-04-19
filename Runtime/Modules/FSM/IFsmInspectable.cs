using System;
using System.Collections.Generic;

namespace XFramework.Fsm
{
    /// <summary>
    /// 仅用于调试观察的状态机只读接口。
    /// </summary>
    public interface IFsmInspectable
    {
        string DebugName { get; }
        Type ContextType { get; }
        bool IsRunning { get; }
        bool IsDisposed { get; }
        string CurrentStateName { get; }
        string PreviousStateName { get; }
        IReadOnlyList<string> RegisteredStateNames { get; }
        FsmTransition LastTransition { get; }
    }
}
