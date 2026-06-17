using System;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    [RequireComponent(typeof(Toggle))]
    [AddComponentMenu("XFramework/XToggle")]
    public class XToggle : XUIBase, IUIEventSource
    {
        public Toggle toggle;

        public Type ListenerType => typeof(UnityAction<bool>);

        public void AddListener(UnityAction<bool> action)
        {
            toggle.onValueChanged.AddListener(action);
        }

        void IUIEventSource.AddListener(Delegate listener)
        {
            AddListener((UnityAction<bool>)listener);
        }

        private void Reset()
        {
            this.toggle = GetComponent<Toggle>();
        }
    }
}
