using System.Collections.Generic;

namespace XFramework.Entity
{
    public partial class EntityManager
    {
        /// <summary>
        /// 实体信息
        /// 存储实体的父子关系以及对应实体
        /// </summary>
        private sealed class EntityInfo
        {
            private static readonly Entity[] EmptyArray = new Entity[] { };
            /// <summary>
            /// 对应的实体
            /// </summary>
            private readonly Entity m_Entity;
            /// <summary>
            /// 父实体
            /// </summary>
            private Entity m_ParentEntity;
            /// <summary>
            /// 子实体组
            /// </summary>
            private List<Entity> m_ChildEntities;

            public EntityInfo(Entity entity)
            {
                m_Entity = entity ?? throw new XFrameworkException("Null Entity");
            }

            /// <summary>
            /// 对应的实体
            /// </summary>
            public Entity Entity
            {
                get
                {
                    return m_Entity;
                }
            }

            /// <summary>
            /// 子实体数量
            /// </summary>
            public int ChildCount
            {
                get
                {
                    return m_ChildEntities.Count;
                }
            }

            /// <summary>
            /// 父实体
            /// </summary>
            public Entity Parent
            {
                get
                {
                    return m_ParentEntity;
                }
                set
                {
                    m_ParentEntity = value;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="index"></param>
            /// <returns></returns>
            public Entity this[int index]
            {
                get
                {
                    if (index >= ChildCount || index < 0)
                    {
                        throw new XFrameworkException($"[EntityError] There is no child which index is {index} in {m_Entity.ToString()}");
                    }
                    return m_ChildEntities[index];
                }
            }

            /// <summary>
            /// 获取所有子实体
            /// </summary>
            public Entity[] GetChilds()
            {
                if (m_ChildEntities == null)
                {
                    return EmptyArray;
                }

                return m_ChildEntities.ToArray();
            }

            /// <summary>
            /// 添加子实体
            /// </summary>
            /// <param name="childEntity">子实体</param>
            public void AddChild(Entity childEntity)
            {
                if(m_ChildEntities == null)
                {
                    m_ChildEntities = new List<Entity>();
                }

                if (m_ChildEntities.Contains(childEntity))
                {
                    throw new XFrameworkException("[Entity] Can not add child which is already exist.");
                }
                m_ChildEntities.Add(childEntity);
            }

            /// <summary>
            /// 移除子实体
            /// </summary>
            /// <param name="childEntity">子实体</param>
            public void RemoveChild(Entity childEntity)
            {
                if (m_ChildEntities == null || !m_ChildEntities.Remove(childEntity))
                {
                    throw new XFrameworkException("[Entity] Can not remove child which is not exist.");
                }
            }
        }
    }
}