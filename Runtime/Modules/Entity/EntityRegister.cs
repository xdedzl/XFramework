using UnityEngine;

namespace XFramework.Entity
{
    [RequireComponent(typeof(Entity))]
    public class EntityRegister : MonoBehaviour
    {
        public bool autoRegister = true;
        public string templateName;
        
        private void Start()
        {
            if (autoRegister)
            {
                Register();
            }
        }

        public void Register()
        {
            var entity = GetComponent<Entity>();
            if (entity == null)
            {
                Debug.LogError($"EntityRegister requires an Entity component. {gameObject.name}");
                return;
            }
            
            EntityManager.Instance.RegisterExistEntity(templateName, gameObject);
            GameObject.Destroy(this);
        }
    }
}