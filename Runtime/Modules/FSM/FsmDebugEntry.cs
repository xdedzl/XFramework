using System;
using UnityEngine;

namespace XFramework.Fsm
{
    public enum FsmScope
    {
        Global = 0,
        Instance = 1
    }

    /// <summary>
    /// 提供给调试窗口与控制台的观察快照。
    /// </summary>
    public readonly struct FsmDebugEntry
    {
        public string Key { get; }
        public FsmScope Scope { get; }
        public UnityEngine.Object Owner { get; }
        public IFsmInspectable Fsm { get; }

        public FsmDebugEntry(string key, FsmScope scope, UnityEngine.Object owner, IFsmInspectable fsm)
        {
            Key = key;
            Scope = scope;
            Owner = owner;
            Fsm = fsm;
        }

        public string ContextTypeName => Fsm != null && Fsm.ContextType != null ? Fsm.ContextType.Name : string.Empty;
        public string CurrentStateName => Fsm != null ? Fsm.CurrentStateName : string.Empty;
        public string PreviousStateName => Fsm != null ? Fsm.PreviousStateName : string.Empty;
        public bool IsRunning => Fsm != null && Fsm.IsRunning;
        public FsmTransition LastTransition => Fsm != null ? Fsm.LastTransition : default;
        public object LastPayload => LastTransition.Payload;
        public string LastPayloadSummary => FormatPayload(LastPayload);

        public static string FormatPayload(object payload)
        {
            if (payload == null)
            {
                return "<null>";
            }

            if (payload is string str)
            {
                return str;
            }

            try
            {
                return payload.ToString();
            }
            catch (Exception)
            {
                return $"<{payload.GetType().Name}>";
            }
        }
    }
}
