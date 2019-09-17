
namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Dropdown))]
    [UnityEngine.AddComponentMenu("XFramework/GUDropdown")]
    public class GUDropdown : GUIBase
    {
        public UnityEngine.UI.Dropdown dropdown;
        private void Reset()
        {
            dropdown = transform.GetComponent<UnityEngine.UI.Dropdown>();
        }

        public void AddListener(UnityEngine.Events.UnityAction<int> call)
        {
            dropdown.onValueChanged.AddListener(call);
        }
    }
}