using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Pool
{
    public class GameObjectPoolManager : IGameModule
    {
        /// <summary>
        /// 利用GameObject的int型Hash值，索引对应的Pool对象
        /// </summary>
        private readonly Dictionary<string, GameObjectPool> poolDic;
        /// <summary>
        /// 所有模型字典
        /// </summary>
        public Dictionary<string, GameObject> PoolTemplateDic { get; private set; }

        public GameObjectPoolManager()
        {
            PoolTemplateDic = new Dictionary<string, GameObject>();
            poolDic = new Dictionary<string, GameObjectPool>();
        }

        /// <summary>
        /// 创建一个对象池
        /// </summary>
        /// <param name="template"></param>
        public GameObjectPool CreatPool(GameObject template)
        {
            PoolTemplateDic.Add(template.name, template);
            GameObjectPool _newPol = new GameObjectPool(template);
            poolDic.Add(template.name, _newPol);
            return _newPol;
        }

        /// <summary>
        /// 通过名字实例化gameobj方法
        /// </summary>
        public GameObject Instantiate(string name, Vector3 pos = default, Quaternion quaternion = default)
        {
            if (!PoolTemplateDic.ContainsKey(name))
            {
                throw new System.Exception("已有名为" + name + "的对象池");
            }

            if (!poolDic.TryGetValue(name, out GameObjectPool pool))
            {
                pool = CreatPool(PoolTemplateDic[name]);
            }

            GameObject obj = pool.Allocate().obj;
            obj.transform.position = pos;
            obj.transform.rotation = quaternion;
            obj.SetActive(true);

            return obj;
        }

        public int Priority => 1000;

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            // TODO 定时回收对象
        }

        public void Shutdown()
        {
            // 清空所有对象池
        }
    }
}