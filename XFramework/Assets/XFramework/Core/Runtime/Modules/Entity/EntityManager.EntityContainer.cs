using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Pool;

namespace XFramework
{
    public partial class EntityManager
    {
        /// <summary>
        /// 实体容器
        /// </summary>
        public class EntityContainer
        {
            /// <summary>
            /// 容器名
            /// </summary>
            private string m_Name;
            /// <summary>
            /// 容器类型
            /// </summary>
            private Type m_Type;
            /// <summary>
            /// 实体列表
            /// </summary>
            private List<Entity> m_Entities;
            /// <summary>
            /// 模板
            /// </summary>
            //private GameObject m_Template;
            /// <summary>
            /// 对象池
            /// </summary>
            private GameObjectPool m_Pool;

            public EntityContainer(Type type, GameObject template)
            {
                if (!type.IsSubclassOf(typeof(Entity)))
                {
                    throw new Exception("类型传入错误，必须是Entity的子类");
                }

                m_Type = type;
                m_Entities = new List<Entity>();
                //m_Template = template;
                m_Pool = new GameObjectPool(template, 0);
            }

            /// <summary>
            /// 实例化物体
            /// </summary>
            /// <param name="pos">位置</param>
            /// <param name="quaternion">角度</param>
            /// <returns></returns>
            public Entity Instantiate(int id, Vector3 pos, Quaternion quaternion)
            {
                GameObject gameObject = m_Pool.Allocate().obj;
                gameObject.transform.position = pos;
                gameObject.transform.rotation = quaternion;

                //Entity entity = Activator.CreateInstance(m_Type, (object)gameObject) as Entity;
                Entity entity = gameObject.AddComponent(m_Type) as Entity;
                entity.Id = id;
                entity.OnInit();
                m_Entities.Add(entity);
                return entity;
            }

            /// <summary>
            /// 获取容器中的所有实体
            /// </summary>
            public Entity[] GetEntities()
            {
                return m_Entities.ToArray();
            }

            public void OnUpdate(float elapseSeconds, float realElapseSeconds)
            {
                foreach (var item in m_Entities)
                {
                    item.OnUpdate(elapseSeconds, realElapseSeconds);
                }
            }
        }
    }
}