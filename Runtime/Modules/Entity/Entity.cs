using UnityEngine;

namespace XFramework.Entity
{
    /// <summary>
    /// 单位实体
    /// </summary>
    public abstract class Entity : MonoBehaviour, IEntity
    {
        /// <summary>
        /// 实体编号
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// 所属容器名
        /// </summary>
        public string ContainerName { get; internal set; }

        public Vector3 position
        {
            get
            {
                return transform.position;
            }
            set
            {
                transform.position = value;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void OnInit() { }
        /// <summary>
        /// 被分配
        /// </summary>
        public virtual void OnAllocate(IEntityData entityData) { }
        /// <summary>
        /// 被回收
        /// </summary>
        public virtual void OnRecycle() { }
        /// <summary>
        /// 附加子实体
        /// </summary>
        /// <param name="childEntity">子实体</param>
        public virtual void OnAttached(Entity childEntity) { }
        /// <summary>
        /// 移除子实体
        /// </summary>
        /// <param name="childEntity">子实体</param>
        public virtual void OnDetached(Entity childEntity) { }
        /// <summary>
        /// 附加到别的实体上
        /// </summary>
        /// <param name="parentEntity">父实体</param>
        public virtual void OnAttachTo(Entity parentEntity) { }
        /// <summary>
        /// 被别的实体移除
        /// </summary>
        /// <param name="parentEntity">父实体</param>
        public virtual void OnDetachFrom(Entity parentEntity) { }
        /// <summary>
        /// 轮询
        /// </summary>
        public virtual void OnUpdate() { }
        /// <summary>
        /// 销毁
        /// </summary>
        public virtual void OnDestroy() { }

        /// <summary>
        /// 附加实体，将child附加到自身上
        /// </summary>
        /// <param name="child">子实体</param>
        public void Aattch(Entity child)
        {
            EntityManager.Instance.Attach(child, this);
        }

        /// <summary>
        /// 移除实体，将自身从父物体上移除
        /// </summary>
        public void Dettch()
        {
            EntityManager.Instance.Detach(this);
        }

        /// <summary>
        /// 移除自身上所有子实体
        /// </summary>
        public void DetachChilds()
        {
            EntityManager.Instance.DetachChilds(this);
        }

        /// <summary>
        /// 回收
        /// </summary>
        public void Recycle()
        {
            EntityManager.Instance.Recycle(this);
        }

        /// <summary>
        /// 打印实体的基础信息
        /// </summary>
        public override string ToString()
        {
            return $"(id:{Id}, name:{name}, containerName:{ContainerName})";
        }
    }
}