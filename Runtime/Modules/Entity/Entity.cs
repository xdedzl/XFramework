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

        /// <summary>
        /// 实体别名
        /// </summary>
        public string Alias { get; internal set; }

        public Vector3 position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public bool IsValid => EntityManager.Instance.IsEntityValid(Id);

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
        /// 轮询
        /// </summary>
        public virtual void OnUpdate() { }
        /// <summary>
        /// 释放前，在unity的OnDestroy之前调用
        /// </summary>
        public virtual void OnRelease() { }

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
