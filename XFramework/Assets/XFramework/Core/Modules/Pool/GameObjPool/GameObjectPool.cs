using System.Collections.Generic;
using UnityEngine;
using System;

namespace XFramework.Pool
{
    /// <summary>
    /// 对象池
    /// </summary>
    public class GameObjectPool : PoolBase
    {
        /// <summary>
        /// 所有对象池的父物体
        /// </summary>
        private static Transform poolRoot;
        /// <summary>
        /// 当前对象池的父物体
        /// </summary>
        public Transform objParent;
        /// <summary>
        /// 对象池模板
        /// </summary>
        private readonly GameObject template;
        /// <summary>
        /// 对象池链表
        /// </summary>
        private readonly List<GameObjectPoolable> pooledObjects;

        /// <summary>
        /// 当前指向链表位置索引
        /// </summary>
        private int currentIndex = 0;

        public override int CurrentCount
        {
            get
            {
                return pooledObjects.Count;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="_template"></param>
        /// <param name="_initCount"></param>
        /// <param name="_lookPoolSize"></param>
        public GameObjectPool(GameObject _template, int _initCount = 5, int _maxCount = int.MaxValue)
        {
            template = _template;
            initCount = _initCount;
            maxCount = _maxCount;

            poolRoot = poolRoot ?? new GameObject("PoolRoot").transform;
            objParent = new GameObject(template.name + "Pool").transform;
            objParent.SetParent(poolRoot);

            pooledObjects = new List<GameObjectPoolable>();             // 初始化链表
            for (int i = 0; i < CurrentCount; ++i)
            {
                Create(template, false);
            }
        }

        /// <summary>
        /// 将对象池的数量回归5
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < pooledObjects.Count; i++)
            {
                if (i > initCount)
                {
                    GameObject.Destroy(pooledObjects[i].obj);
                    pooledObjects.Remove(pooledObjects[i]);
                }
                else if (pooledObjects[i].obj.activeInHierarchy)
                {
                    pooledObjects[i].obj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 获取对象池中可以使用的对象。
        /// </summary>
        public GameObjectPoolable Allocate()
        {
            for (int i = 0; i < pooledObjects.Count; ++i)
            {
                //每一次遍历都是从上一次被使用的对象的下一个
                int Item = (currentIndex + i) % pooledObjects.Count;
                if (pooledObjects[Item].IsRecycled)
                {
                    currentIndex = (Item + 1) % pooledObjects.Count;
                    //返回第一个未激活的对象
                    pooledObjects[Item].IsRecycled = false;
                    return pooledObjects[Item];
                }
            }

            //如果遍历完一遍对象库发现没有闲置对象且对象池未达到数量限制
            if (CurrentCount < maxCount)
            {
                GameObjectPoolable info = Create(template);
                info.IsRecycled = false;
                return info;
            }

            return null;
        }

        /// <summary>
        /// 回收
        /// </summary>
        public bool Recycle(GameObjectPoolable poolObj)
        {
            if (poolObj == null || poolObj.IsRecycled)
                return false;
            poolObj.OnRecycled();
            return true;
        }

        /// <summary>
        /// 为对象池新增一个对象
        /// </summary>
        private GameObjectPoolable Create(GameObject template,bool isShow = true)
        {
            GameObjectPoolable info = new GameObjectPoolable(GameObject.Instantiate(template, objParent));
            pooledObjects.Add(info);
            if (!isShow)
                info.obj.SetActive(false);
            return info;
        }

        public override int AutoRecycle()
        {
            throw new NotImplementedException();
        }

        public override void OnDestroy()
        {
            throw new NotImplementedException();
        }
    }



    public class GameObjectPoolable : IPoolable
    {
        public GameObject obj;

        public GameObjectPoolable(GameObject _obj)
        {
            obj = _obj;
            IsRecycled = true;
        }

        public bool IsRecycled { get; set; }
        public bool IsLocked { get; set; }

        public void OnRecycled()
        {

        }
    }
}