using System.Collections.Generic;

namespace XFramework.Pool
{
    public partial class ObjectPoolManager
    {
        /// <summary>
        /// 内联泛型对象池
        /// </summary>
        /// <typeparam name="T">对象池类型</typeparam>
        private class Pool<T> : PoolBase where T : class, IPoolable, new()
        {
            /// <summary>
            /// 可用的对象
            /// </summary>
            private readonly Stack<T> m_AvailableCache;
            /// <summary>
            /// 被占有的对象
            /// </summary>
            private readonly List<T> m_OccupiedCache;

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="initCount"></param>
            public Pool(int initCount)
            {
                m_AvailableCache = new Stack<T>();
                m_OccupiedCache = new List<T>();
                for (int i = 0; i < initCount; ++i)
                {
                    Create().OnRecycled();
                }
            }

            /// <summary>
            /// 获取对象池中可以使用的对象。
            /// </summary>
            public T Allocate()
            {
                T obj;
                if (m_AvailableCache.Count > 0)
                {
                    obj = m_AvailableCache.Pop();
                }
                else
                {
                    obj = Create();
                }
                m_OccupiedCache.Add(obj);

                return obj;
            }

            /// <summary>
            /// 回收
            /// </summary>
            public bool Recycle(T poolObj)
            {
                if (poolObj == null)
                    return false;

                if (m_OccupiedCache.Remove(poolObj))
                {
                    m_AvailableCache.Push(poolObj);
                    poolObj.OnRecycled();
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 自动回收
            /// </summary>
            /// <returns>本次操作回收对象的数量</returns>
            public override void RecycleAllObj()
            {
                foreach (var poolObj in m_OccupiedCache)
                {
                    poolObj.OnRecycled();
                    m_AvailableCache.Push(poolObj);
                }

                m_OccupiedCache.Clear();
            }

            /// <summary>
            /// 销毁自身
            /// </summary>
            public override void OnDestroy()
            {
                m_AvailableCache.Clear();
                m_OccupiedCache.Clear();
            }

            /// <summary>
            /// 创建一个新对象
            /// </summary>
            private T Create()
            {
                return new T();
            }
        }
    }
}