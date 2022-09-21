using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(ToggleGroup))]
    [AddComponentMenu("XFramework/XToggleGroup")]
    public class XToggleGroup : XUIBase
    {
        public ToggleGroup toggleGroup;
        private void Reset()
        {
            toggleGroup = GetComponent<ToggleGroup>();
        }

        public void SetAllOff()
        {
            toggleGroup.SetAllTogglesOff();
        }
    }
}