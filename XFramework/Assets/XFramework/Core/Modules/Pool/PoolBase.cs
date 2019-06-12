using System.Collections.Generic;

namespace XFramework.Pool
{
    public abstract class PoolBase 
    {
        /// <summary>
        /// 初始化大小
        /// </summary>
        protected int initCount;
        /// <summary>
        /// 对象池最大数量
        /// </summary>
        protected int maxCount;

        public abstract int CurrentCount
        {
            get;
        }

        /// <summary>
        /// 自动回收对象池中的对象
        /// </summary>
        public abstract int AutoRecycle();

        /// <summary>
        /// 销毁对象池
        /// </summary>
        public abstract void OnDestroy();
    }
}