using System.Collections.Generic;

namespace XFramework.Pool
{
    /// <summary>
    /// 对象池管理
    /// </summary>
    public partial class ObjectPoolManager : Singleton<ObjectPoolManager>
    {
        private readonly Dictionary<string, PoolBase> m_ObjectPools;

        private ObjectPoolManager()
        {
            m_ObjectPools = new Dictionary<string, PoolBase>();
        }

        /// <summary>
        /// 分配一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public T Allocate<T>() where T : class, IPoolable, new()
        {
            return GetPool<T>().Allocate();
        }

        /// <summary>
        /// 回收一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>是否回收成功</returns>
        public bool Recycle<T>(T obj) where T : class, IPoolable, new()
        {
            if (TryGetPool<T>(out Pool<T> pool))
            {
                pool.Recycle(obj);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 销毁一个对象池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>是否成功销毁</returns>
        public bool DestoryPool<T>() where T : class, IPoolable, new()
        {
            string typeName = typeof(T).Name;
            if (m_ObjectPools.TryGetValue(typeName, out PoolBase pool))
            {
                pool.OnDestroy();
                m_ObjectPools.Remove(typeName);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取一个对象池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private Pool<T> GetPool<T>() where T : class, IPoolable, new()
        {
            if (TryGetPool<T>(out Pool<T> pool))
            {
                return pool;
            }
            return CreatePool<T>();
        }

        private bool TryGetPool<T>(out Pool<T> pool) where T : class, IPoolable, new()
        {
            string typeName = typeof(T).Name;

            if (m_ObjectPools.TryGetValue(typeName, out PoolBase poolBase))
            {
                pool = (Pool<T>)poolBase;
                return true;
            }
            pool = null;
            return false;
        }

        private Pool<T> CreatePool<T>(int initCount = 0) where T : class, IPoolable, new()
        {
            string typeName = typeof(T).Name;
            Pool<T> pool = new Pool<T>(initCount);
            m_ObjectPools.Add(typeName, pool);
            return pool;
        }
    }
}