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
            public readonly string name;
            /// <summary>
            /// 容器类型
            /// </summary>
            public readonly Type type;

            /// <summary>
            /// 实体列表
            /// </summary>
            private List<Entity> m_Entities;
            /// <summary>
            /// 对象池
            /// </summary>
            private GameObject m_Template;

            public int Count { get { return m_Entities.Count; } }

            public EntityContainer(Type type, GameObject template)
            {
                if (!type.IsSubclassOf(typeof(Entity)))
                {
                    throw new Exception("类型传入错误，必须是Entity的子类");
                }

                this.type = type;
                m_Template = template;
                m_Entities = new List<Entity>();
            }

            /// <summary>
            /// 实例化物体
            /// </summary>
            /// <param name="pos">位置</param>
            /// <param name="quaternion">角度</param>
            /// <returns></returns>
            public Entity Instantiate(int id, Vector3 pos, Quaternion quaternion)
            {
                GameObject gameObject = UnityEngine.Object.Instantiate(m_Template, pos, quaternion);
                //gameObject.transform.position = pos;
                //gameObject.transform.rotation = quaternion;

                Entity entity = gameObject.AddComponent(type) as Entity;
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

            public void Clean()
            {

                
            }

            /// <summary>
            /// 轮询
            /// </summary>
            /// <param name="elapseSeconds">逻辑运行时间</param>
            /// <param name="realElapseSeconds">实际运行时间</param>
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