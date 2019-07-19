using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Entity
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
            /// 模板
            /// </summary>
            private GameObject m_Template;
            /// <summary>
            /// 实体池
            /// </summary>
            private Stack<Entity> m_Pool;

            public int Count { get { return m_Entities.Count; } }

            public EntityContainer(Type type, string name, GameObject template)
            {
                if (!type.IsSubclassOf(typeof(Entity)))
                {
                    throw new Exception($"[{type.Name}]类型传入错误，必须是Entity的子类");
                }

                this.type = type;
                this.name = name;
                m_Template = template;
                m_Entities = new List<Entity>();
                m_Pool = new Stack<Entity>();
            }

            /// <summary>
            /// 实体实例化及初始化
            /// </summary>
            /// <param name="id">唯一标识符</param>
            /// <param name="pos">位置</param>
            /// <param name="quaternion">朝向</param>
            /// <returns></returns>
            private Entity Instantiate(int id, Vector3 pos, Quaternion quaternion)
            {
                GameObject gameObject = UnityEngine.Object.Instantiate(m_Template, pos, quaternion);

                Entity entity = gameObject.AddComponent(type) as Entity;
                entity.PreInit(id, name);
                entity.OnInit();
                return entity;
            }

            /// <summary>
            /// 实例化物体
            /// </summary>
            /// <param name="pos">位置</param>
            /// <param name="quaternion">角度</param>
            /// <returns></returns>
            public Entity Allocate(int id, Vector3 pos, Quaternion quaternion, EntityData entityData)
            {
                Entity entity;
                if (m_Pool.Count > 0)
                {
                    entity = m_Pool.Pop();
                    entity.transform.position = pos;
                    entity.transform.rotation = quaternion;
                }
                else
                {
                    entity = Instantiate(id, pos, quaternion);
                }
                entity.OnAllocate(entityData);
                m_Entities.Add(entity);
                return entity;
            }

            /// <summary>
            /// 回收实体
            /// </summary>
            /// <param name="entity">实体</param>
            /// <returns>是否回收成功</returns>
            public bool Recycle(Entity entity)
            {
                if (m_Entities.Contains(entity))
                {
                    m_Entities.Remove(entity);
                    m_Pool.Push(entity);
                    entity.OnRecycle();
                    return true;
                }
                return false;
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