using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(SliderMixInput))]
    [UnityEngine.AddComponentMenu("XFramework/GUSliderMixInput")]
    public class GUSliderMixInput : GUIBase
    {
        public SliderMixInput mix;

        private void Reset()
        {
            mix = GetComponent<SliderMixInput>();
        }
    }
}