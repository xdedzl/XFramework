using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    [RequireComponent(typeof(Toggle))]
    [AddComponentMenu("XFramework/GUToggle")]
    public class XToggle : GUIBase
    {
        public Toggle toggle;

        public void AddListener(UnityAction<bool> action)
        {
            toggle.onValueChanged.AddListener(action);
        }

        private void Reset()
        {
            this.toggle = GetComponent<Toggle>();
        }
    }
}