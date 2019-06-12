using System.Collections.Generic;

namespace XFramework.Pool
{
    /// <summary>
    /// 对象池管理
    /// </summary>
    public partial class ObjectPoolManager : IGameModule
    {
        private Dictionary<string, PoolBase> m_ObjectPools;

        public ObjectPoolManager()
        {
            m_ObjectPools = new Dictionary<string, PoolBase>();
        }

        /// <summary>
        /// 是否有T类型的对象池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public bool HasPool<T>() where T : IPoolable, new()
        {
            return HasPool(typeof(T).Name);
        }

        public bool HasPool(string poolName)
        {
            if (m_ObjectPools.ContainsKey(poolName))
                return true;
            return false;
        }

        /// <summary>
        /// 创建一个T类型的对象池
        /// </summary>
        /// <typeparam name="T">对象池类型</typeparam>
        /// <param name="initCount">初始数量</param>
        /// <param name="maxCount">最大数量</param>
        public void CreatePool<T>(int initCount = 0, int maxCount = int.MaxValue) where T : IPoolable, new()
        {
            if (HasPool<T>())
                return;

            Pool<T> pool = new Pool<T>(initCount, maxCount);
            m_ObjectPools.Add(typeof(T).Name, pool);
        }

        /// <summary>
        /// 销毁一个对象池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>是否成功销毁</returns>
        public bool DestoryPool<T>() where T : IPoolable, new()
        {
            if (HasPool<T>())
            {
                m_ObjectPools[typeof(T).Name].OnDestroy();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取一个对象池
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public Pool<T> GetPool<T>() where T : IPoolable, new()
        {
            return (Pool<T>)GetPool(typeof(T).Name);
        }

        public PoolBase GetPool(string poolName)
        {
            if (HasPool(poolName))
            {
                return m_ObjectPools[poolName];
            }
            return null;
        }

        /// <summary>
        /// 分配一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public T Allocate<T>() where T : IPoolable, new()
        {
            if (!HasPool<T>())
            {
                CreatePool<T>();
            }

            return ((Pool<T>)m_ObjectPools[typeof(T).Name]).Allocate();
        }

        /// <summary>
        /// 回收一个对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>是否回收成功</returns>
        public bool Recycle<T>(T obj) where T : IPoolable, new()
        {
            if (HasPool<T>())
            {
                return ((Pool<T>)m_ObjectPools[typeof(T).Name]).Recycle(obj);
            }
            return false;
        }

        #region 接口实现

        public int Priority { get { return 2; } }

        public void Shutdown()
        {
            foreach (var pool in m_ObjectPools.Values)
            {
                pool.OnDestroy();
            }
            m_ObjectPools.Clear();
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            // TODO每隔一定时间清理一次对象池
        }

        #endregion
    }
}