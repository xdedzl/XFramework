using UnityEngine;
using XFramework.Pool;

namespace XFramework
{
    /// <summary>
    /// 单位实体
    /// </summary>
    public abstract class Entity : MonoBehaviour, IEntity
    {
        public int Id;

        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void OnInit() { }
        /// <summary>
        /// 显示
        /// </summary>
        public virtual void OnShow() { }
        /// <summary>
        /// 隐藏
        /// </summary>
        public virtual void OnHide() { }
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