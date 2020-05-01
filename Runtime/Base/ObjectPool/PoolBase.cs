namespace XFramework.Pool
{
    public abstract class PoolBase
    {
        public abstract int CurrentCount { get; }

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