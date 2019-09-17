using System;

namespace XFramework.Event
{
    /// <summary>
    /// 事件参数
    /// </summary>
    public class EventArgs
    {
        public object[] data;                    // 事件参数
        public int eventType;      // 事件类型

        public EventArgs(Enum eventType, params object[] data)
        {
            this.data = data;
            this.eventType = Convert.ToInt32(eventType);
        }
    }
}