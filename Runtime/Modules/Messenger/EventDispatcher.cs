using System;

namespace XFramework.Event
{
    /// <summary>
    /// 事件分发处理类
    /// 这是个基类
    /// </summary>
    public class EventDispatcher
    {
        private event Action<object, EventArgs> EventListener;

        /// <summary>
        /// 分发消息
        /// </summary>    
        /// <param name="eventType"></param>
        /// <param name="data"></param>
        public void DispatchEvent(Enum eventType, object data = null)
        {
            EventListener?.Invoke(this, new EventArgs(eventType, data));
        }

        /// <summary>
        /// 注册监听
        /// </summary>
        public void RegistEvent(Action<object, EventArgs> fuc)
        {
            // 防止重复添加事件
            EventListener -= fuc;
            EventListener += fuc;
        }

        /// <summary>
        /// 注销监听
        /// </summary>
        public void UnRegistEvent(Action<object, EventArgs> fuc)
        {
            EventListener -= fuc;
        }
    }
}