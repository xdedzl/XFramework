using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(Slider))]
    [UnityEngine.AddComponentMenu("XFramework/XSlider")]
    public class XSlider : XUIBase, IUIEventSource
    {
        public Slider slider;

        public Type ListenerType => typeof(UnityAction<float>);

        private void Reset()
        {
            slider = GetComponent<Slider>();
        }

        public void AddListener(UnityAction<float> call)
        {
            slider.onValueChanged.AddListener(call);
        }

        void IUIEventSource.AddListener(Delegate listener)
        {
            AddListener((UnityAction<float>)listener);
        }
    }
}
