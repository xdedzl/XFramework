namespace XFramework.Pool
{
    /// <summary>
    /// 池
    /// </summary>
    public interface IPool<T>
    {
        /// <summary>
        /// 分配对象
        /// </summary>
        T Allocate();
        /// <summary>
        /// 回收对象
        /// </summary>
        bool Recycle(T obj);
    }

    /// <summary>
    /// 对象工厂
    /// </summary>
    public interface IObjectFactory<T>
    {
        T Create();
    }

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
        /// 被创建时调用
        /// </summary>
        //void OnInit();
        /// <summary>
        /// 回收事件
        /// </summary>
        void OnRecycled();
    }

    public interface IPoolType
    {
        void Recycle2Cache();
    }
}