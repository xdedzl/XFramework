using UnityEngine;

namespace XFramework.UI
{
    public abstract class XUIBase : MonoBehaviour, IComponentKeyProvider
    {
        [SerializeField] private string searchKey = "";
        public string Key => searchKey;
    }
}