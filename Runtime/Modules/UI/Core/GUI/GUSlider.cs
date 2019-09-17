using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(Slider))]
    [UnityEngine.AddComponentMenu("XFramework/GUSlider")]
    public class GUSlider : GUIBase
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