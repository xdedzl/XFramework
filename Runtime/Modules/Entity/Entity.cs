using UnityEngine;

namespace XFramework.Entity
{
    /// <summary>
    /// 单位实体
    /// </summary>
    public abstract class Entity : MonoBehaviour, IEntity
    {
        public int Id { get; private set; }

        /// <summary>
        /// 所属容器名
        /// </summary>
        public string ContainerName { get; private set; }

        public void PreInit(int id, string containerName)
        {
            Id = id;
            ContainerName = containerName;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void OnInit() { }
        /// <summary>
        /// 被分配
        /// </summary>
        public virtual void OnAllocate(EntityData entityData) { }
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
        /// <param name="parentEntity">子实体</param>
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
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds) { }
    }
}