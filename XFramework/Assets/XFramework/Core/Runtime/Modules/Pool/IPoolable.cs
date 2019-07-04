namespace XFramework.Pool
{
    /// <summary>
    /// 用于泛型约束，要实现SafeObjectPool的类必须继承这个接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 用于表示对象是否被回收
        /// </summary>
        bool IsRecycled { get; set; }
        /// <summary>
        /// 是否可以被回收
        /// </summary>
        bool IsLocked { get; set; }
        /// <summary>
        /// 回收事件
        /// </summary>
        void OnRecycled();
    }
}