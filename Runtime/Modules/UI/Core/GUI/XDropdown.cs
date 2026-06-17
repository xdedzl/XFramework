
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(Dropdown))]
    [AddComponentMenu("XFramework/XDropdown")]
    public class XDropdown : XUIBase, IUIEventSource
    {
        public Dropdown dropdown;

        public Type ListenerType => typeof(UnityAction<int>);

        private void Reset()
        {
            dropdown = transform.GetComponent<Dropdown>();
        }

        public void AddListener(UnityAction<int> call)
        {
            dropdown.onValueChanged.AddListener(call);
        }

        void IUIEventSource.AddListener(Delegate listener)
        {
            AddListener((UnityAction<int>)listener);
        }
    }
}
