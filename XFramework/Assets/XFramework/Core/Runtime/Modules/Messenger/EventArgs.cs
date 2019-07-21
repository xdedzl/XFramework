namespace XFramework.Event
{
    /// <summary>
    /// 事件参数
    /// </summary>
    public class EventArgs
    {
        public object[] data;                    // 事件参数
        public EventDispatchType eventType;      // 事件类型

        public EventArgs(EventDispatchType eventType, params object[] data)
        {
            this.data = data;
            this.eventType = eventType;
        }
    }
}