using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    [RequireComponent(typeof(Slider))]
    public class GUSlider : BaseGUI
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