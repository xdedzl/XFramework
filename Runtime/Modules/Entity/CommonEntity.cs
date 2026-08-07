using UnityEngine;
namespace XFramework.Entity
{
    public class CommonEntity : Entity<LogicEntity>
    {
        public override void OnAllocate(IEntityData entityData)
        {
            gameObject.SetActive(true);
        }

        public override void OnRecycle()
        {
            gameObject.SetActive(false);
        }
    }
}
