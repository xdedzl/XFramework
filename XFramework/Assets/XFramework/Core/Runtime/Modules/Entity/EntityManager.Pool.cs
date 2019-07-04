using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public partial class EntityManager
    {
        public class Pool<T>
        {
            /// <summary>
            /// 对象池模板
            /// </summary>
            private readonly GameObject template;
            /// <summary>
            /// 对象池链表
            /// </summary>
            private readonly List<Poolable<T>> pooledObjects;

            /// <summary>
            /// 当前指向链表位置索引
            /// </summary>
            private int currentIndex = 0;
            /// <summary>
            /// 池的最大容量
            /// </summary>
            private int maxCount = 5;

            public int CurrentCount
            {
                get
                {
                    return pooledObjects.Count;
                }
            }

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="template"></param>
            /// <param name="maxCount"></param>
            public Pool(GameObject template,int maxCount)
            {
                this.template = template;
                this.maxCount = maxCount;

                pooledObjects = new List<Poolable<T>>();             // 初始化链表
            }

            /// <summary>
            /// 将对象池的数量回归5
            /// </summary>
            public void Clear()
            {
                for (int i = 0; i < pooledObjects.Count; i++)
                {
                    if (!pooledObjects[i].isLock)
                    {
                        pooledObjects.RemoveAt(i);
                        i--;
                    }
                }
            }

            /// <summary>
            /// 获取对象池中可以使用的对象。
            /// </summary>
            public T Allocate()
            {
                for (int i = 0; i < pooledObjects.Count; ++i)
                {
                    //每一次遍历都是从上一次被使用的对象的下一个
                    int Item = (currentIndex + i) % pooledObjects.Count;
                    if (pooledObjects[Item].isLock)
                    {
                        currentIndex = (Item + 1) % pooledObjects.Count;
                        //返回第一个未激活的对象
                        pooledObjects[Item].isLock = true;
                        return pooledObjects[Item].obj;
                    }
                }

                //如果遍历完一遍对象库发现没有闲置对象且对象池未达到数量限制
                if (CurrentCount < maxCount)
                {
                    Poolable<T> info = Create(template);
                    info.isLock = true;
                    return info.obj;
                }

                return default;
            }

            /// <summary>
            /// 回收
            /// </summary>
            public bool Recycle(Entity poolObj)
            {
                return true;
            }

            /// <summary>
            /// 为对象池新增一个对象
            /// </summary>
            private Poolable<T> Create(GameObject template, bool isShow = true)
            {
                return null;
            }
        }

        public class Poolable<T>
        {
            public T obj;

            public bool isLock;

            public Poolable(T obj)
            {
                this.obj = obj;
            }
        }
    }
}