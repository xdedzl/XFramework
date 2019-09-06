
namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Dropdown))]
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