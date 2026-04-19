namespace XFramework.Fsm
{
    /// <summary>
    /// 一次状态切换的快照。
    /// </summary>
    public readonly struct FsmTransition
    {
        public string FromStateName { get; }
        public string ToStateName { get; }
        public object Payload { get; }
        public int FrameCount { get; }
        public float RealtimeSinceStartup { get; }

        public FsmTransition(string fromStateName, string toStateName, object payload)
        {
            FromStateName = fromStateName;
            ToStateName = toStateName;
            Payload = payload;
            FrameCount = FsmClock.FrameCount;
            RealtimeSinceStartup = FsmClock.RealtimeSinceStartup;
        }

        public override string ToString()
        {
            return $"{FromStateName} -> {ToStateName}";
        }
    }
}
