using System;

namespace XFramework.Fsm
{
    /// <summary>
    /// 为纯 C# 核心提供时间与帧信息。
    /// </summary>
    public static class FsmClock
    {
        private static Func<int> s_FrameProvider = () => 0;
        private static Func<float> s_RealtimeProvider = () => 0f;

        public static int FrameCount => s_FrameProvider != null ? s_FrameProvider.Invoke() : 0;
        public static float RealtimeSinceStartup => s_RealtimeProvider != null ? s_RealtimeProvider.Invoke() : 0f;

        public static void Configure(Func<int> frameProvider, Func<float> realtimeProvider)
        {
            s_FrameProvider = frameProvider ?? (() => 0);
            s_RealtimeProvider = realtimeProvider ?? (() => 0f);
        }
    }
}
