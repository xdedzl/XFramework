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
            private readonly List<Entity> m_Entities;
            /// <summary>
            /// 模板
            /// </summary>
            private readonly GameObject m_Template;
            /// <summary>
            /// 实体池
            /// </summary>
            private readonly Stack<Entity> m_Pool;

            /// <summary>
            /// 构造一个实体容器
            /// </summary>
            /// <param name="type">实体类型</param>
            /// <param name="name">容器名</param>
            /// <param name="template">实体模板</param>
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
            /// 实体数量（不包括池中的）
            /// </summary>
            public int Count
            {
                get
                {
                    return m_Entities.Count;
                }
            }

            /// <summary>
            /// 实体实例化及初始化
            /// </summary>
            /// <param name="id">唯一标识符</param>
            /// <param name="pos">位置</param>
            /// <param name="quaternion">朝向</param>
            /// <returns></returns>
            private Entity Instantiate(Vector3 pos, Quaternion quaternion, Transform parent)
            {
                GameObject gameObject = GameObject.Instantiate(m_Template, pos, quaternion, parent);

                Entity entity = gameObject.AddComponent(type) as Entity;
                entity.name = name;
                entity.ContainerName = this.name;
                entity.OnInit();
                return entity;
            }

            /// <summary>
            /// 实例化物体
            /// </summary>
            /// <param name="pos">位置</param>
            /// <param name="quaternion">角度</param>
            /// <returns></returns>
            internal Entity Allocate(int id, Vector3 pos, Quaternion quaternion, EntityData entityData, Transform parent)
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
                    entity = Instantiate(pos, quaternion, parent);
                }
                entity.Id = id;
                entity.OnAllocate(entityData);
                m_Entities.Add(entity);

                return entity;
            }

            /// <summary>
            /// 回收实体
            /// </summary>
            /// <param name="entity">实体</param>
            /// <returns>是否回收成功</returns>
            internal bool Recycle(Entity entity)
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

            /// <summary>
            /// 清理实体池
            /// </summary>
            /// <param name="count">清理后实体池的最大数量</param>
            /// <param name="callBack"></param>
            internal void Clean(int count)
            {
                while (count < m_Pool.Count)
                {
                    GameObject obj = m_Pool.Pop().gameObject;
                    GameObject.Destroy(obj);
                }
            }

            /// <summary>
            /// 轮询
            /// </summary>
            /// <param name="elapseSeconds">逻辑运行时间</param>
            /// <param name="realElapseSeconds">实际运行时间</param>
            internal void OnUpdate(float elapseSeconds, float realElapseSeconds)
            {
                for (int i = 0; i < m_Entities.Count; i++)
                {
                    m_Entities[i].OnUpdate(elapseSeconds, realElapseSeconds);
                }
            }
        }
    }
}