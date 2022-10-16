namespace XFramework.Event
{
    /// <summary>
    /// 观察者数据基类
    /// </summary>
    public abstract class EventData
    {
        protected EventData() { }

        /// <summary>
        /// 这个在派生类中要重写，返回对应的类型
        /// </summary>
        public abstract int dataType
        {
            get;
        }
    }
}