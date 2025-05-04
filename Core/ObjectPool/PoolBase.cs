namespace XFramework.Pool
{
    /// <summary>
    /// 对象池基类
    /// </summary>
    public abstract class PoolBase
    {
        /// <summary>
        /// 回收所有对象
        /// </summary>
        public abstract void RecycleAllObj();

        /// <summary>
        /// 销毁对象池
        /// </summary>
        public abstract void OnDestroy();
    }
}