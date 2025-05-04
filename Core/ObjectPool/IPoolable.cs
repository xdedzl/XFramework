namespace XFramework.Pool
{
    /// <summary>
    /// 可使用对象池管理的
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 回收事件
        /// </summary>
        void OnRecycled();
    }
}