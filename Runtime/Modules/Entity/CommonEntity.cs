using UnityEngine;
namespace XFramework.Entity
{
    public class CommonEntity : Entity
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
