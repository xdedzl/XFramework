namespace XFramework.Event
{
    ///<summary>
    ///观察者接口
    ///<summary>
    public interface IObserver
    {
        /// <summary>
        /// 有数据产生变化时触发
        /// </summary>
        /// <param name="eventData"></param>
        /// <param name="type"></param>
        /// <param name="obj"></param>
        void OnDataChange(EventData eventData, int type, object obj);
    }
}