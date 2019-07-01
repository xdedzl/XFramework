using System.Collections.Generic;

namespace XFramework.Pool
{
    public partial class ObjectPoolManager : IGameModule
    {
        /// <summary>
        /// 内联泛型对象池
        /// </summary>
        /// <typeparam name="T">对象池类型</typeparam>
        public class Pool<T> : PoolBase where T : IPoolable, new()
        {
            /// <summary>
            /// 可用的对象
            /// </summary>
            private Stack<T> m_AvailableCache;
            /// <summary>
            /// 被占有的对象
            /// </summary>
            private List<T> m_OccupiedCache;

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="_initCount"></param>
            /// <param name="_lookPoolSize"></param>
            public Pool(int _initCount = 5, int _maxCount = int.MaxValue)
            {
                if (_initCount > _maxCount)
                    throw new System.Exception("error");

                initCount = _initCount;
                maxCount = _maxCount;

                m_AvailableCache = new Stack<T>();
                m_OccupiedCache = new List<T>();
                for (int i = 0; i < initCount; ++i)
                {
                    Create().OnRecycled();
                }
            }

            public override int CurrentCount
            {
                get
                {
                    return m_AvailableCache.Count + m_OccupiedCache.Count;
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
                    m_OccupiedCache.Add(obj);
                    return obj;
                }

                //如果遍历完一遍对象库发现没有闲置对象且对象池未达到数量限制
                if (CurrentCount < maxCount)
                {
                    T info = Create();
                    info.IsRecycled = false;
                    return info;
                }

                return default;
            }

            /// <summary>
            /// 回收
            /// </summary>
            public bool Recycle(T poolObj)
            {
                if (poolObj == null || poolObj.IsRecycled)
                    return false;
                m_AvailableCache.Push(poolObj);
                m_OccupiedCache.Remove(poolObj);
                poolObj.OnRecycled();
                return true;
            }

            /// <summary>
            /// 自动回收
            /// </summary>
            /// <returns>本次操作回收对象的数量</returns>
            public override int AutoRecycle()
            {
                List<int> temp = new List<int>();
                foreach (var poolObj in m_OccupiedCache)
                {
                    if (poolObj.IsLocked == false)
                    {
                        m_AvailableCache.Push(poolObj);
                        m_OccupiedCache.Remove(poolObj);
                        poolObj.OnRecycled();
                    }
                }
                return temp.Count;
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