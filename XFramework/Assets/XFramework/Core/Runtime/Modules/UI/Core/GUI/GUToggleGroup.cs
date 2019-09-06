using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(ToggleGroup))]
    public class GUToggleGroup : GUIBase
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