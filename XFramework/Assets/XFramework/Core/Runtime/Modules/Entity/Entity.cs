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
        public int Id { get; internal set; }

        /// <summary>
        /// 所属容器名
        /// </summary>
        public string ContainerName { get; internal set; }

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
        /// <param name="elapseSeconds">逻辑运行时间</param>
        /// <param name="realElapseSeconds">真实运行时间</param>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds) { }

        /// <summary>
        /// 打印实体的基础信息
        /// </summary>
        public override string ToString()
        {
            return $"(id:{Id}, name:{name}, containerName:{ContainerName})";
        }
    }
}