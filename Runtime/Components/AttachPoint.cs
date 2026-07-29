using UnityEngine;

namespace XFramework
{
    [DisallowMultipleComponent]
    public sealed class AttachPoint : MonoBehaviour
    {
        public string PointName => gameObject.name;
    }
}
