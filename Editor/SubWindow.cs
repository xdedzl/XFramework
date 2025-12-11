using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class SubWindow
    {
        public virtual void Awake() { }
        public virtual void OnEnable() { }
        public virtual void OnGUI() { }
        public virtual void OnDisable() { }
        public virtual VisualElement BuildUI() { return null; }
    }
}