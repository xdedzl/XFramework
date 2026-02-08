using UnityEngine;

namespace XFramework.Bind
{
    [AddComponentMenu("XFramework/Bind/BindGameObjectActive")]
    public class BindGameObjectActive : MonoBehaviour, IBindObject<bool>
    {
        public bool reverse;
        
        public void OnBind(IBindableDataCell<bool> bindableData)
        {
            gameObject.SetActive(bindableData.Value ^ reverse);
        }
    }
}