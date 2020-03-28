using System;

namespace XFramework.Event
{
    /// <summary>
    /// 事件参数
    /// </summary>
    public class EventArgs
    {
        /// <summary>
        /// 事件参数
        /// </summary>
        public object[] data;
        /// <summary>
        /// 事件类型
        /// </summary>
        public int eventType;

        public EventArgs(Enum eventType, params object[] data)
        {
            this.data = data;
            this.eventType = Convert.ToInt32(eventType);
        }
    }
}