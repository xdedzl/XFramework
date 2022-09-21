using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(SliderMixInput))]
    [UnityEngine.AddComponentMenu("XFramework/XSliderMixInput")]
    public class XSliderMixInput : XUIBase
    {
        public SliderMixInput mix;

        private void Reset()
        {
            mix = GetComponent<SliderMixInput>();
        }
    }
}