using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XFramework.UI
{
    public class ColorPicker : MonoBehaviour
    {
        [Range(0, 1)]
        public float hue;

        public Knob knob;
        public Knob hueKnob;

        private Material m_ColorPickerMaterial;

        public ColorEvent onValueChange = new ColorEvent();

        public Color color
        {
            get
            {
                return Color.HSVToRGB(hue, knob.value.x, knob.value.y);
            }
            set
            {
                Color.RGBToHSV(value, out float h, out float s, out float v);
                hueKnob.value = new Vector2(h, 0.5f);
                knob.value = new Vector2(s, v);
            }
        }

        public void Start()
        {
            m_ColorPickerMaterial = GetComponent<RawImage>().material;

            knob.onValueChange.AddListener(OnKonbValueChange);
            hueKnob.onValueChange.AddListener(OnHueKonbValueChange);
        }

        public void SetHue(float hue)
        {
            if (hue > 1 || hue < 0)
                return;
            this.hue = hue;
            m_ColorPickerMaterial.SetFloat("_Hue", hue);
            RefreshColor();
        }

        private void OnKonbValueChange(Vector2 pos)
        {
            RefreshColor();
        }

        private void OnHueKonbValueChange(Vector2 pos)
        {
            SetHue(pos.x);
        }

        private void RefreshColor()
        {
            onValueChange.Invoke(color);
        }
    }

    public class ColorEvent : UnityEvent<Color> { }
}

