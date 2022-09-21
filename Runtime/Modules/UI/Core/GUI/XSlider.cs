using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(Slider))]
    [UnityEngine.AddComponentMenu("XFramework/XSlider")]
    public class XSlider : XUIBase
    {
        public Slider slider;

        private void Reset()
        {
            slider = GetComponent<Slider>();
        }

        public void AddListener(UnityEngine.Events.UnityAction<float> call)
        {
            slider.onValueChanged.AddListener(call);
        }
    }
}