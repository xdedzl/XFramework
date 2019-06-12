// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-11-26 09:00:47
// 版本： V 1.0
// ==========================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace XFramework.UI
{
    public class SliderMixInput : MonoBehaviour
    {
        public Slider slider;         // 滑动器
        public InputField input;      // 输入框
        public float Value { get; private set; }        // 控制的值  

        public int minValue;
        public int maxValue;

        public SliderMixInputEvent onValueChange = new SliderMixInputEvent();

        private void Reset()
        {
            slider = transform.Find("Slider").GetComponent<Slider>();
            input = transform.Find("InputField").GetComponent<InputField>();
        }


        public void Awake()
        {
            if (slider == null || input == null)
            {
                Debug.LogError("滑动条和输入框没设置");
                return;
            }

            // 监听滑动器值变动
            slider.onValueChanged.AddListener(a =>
            {
                input.text = a.ToString();
                Value = a;
                onValueChange.Invoke(a);
            });

            //监听输入框输入
            input.onValidateInput += (text, charIndex, addedChar) =>
            {
                float temp;
                try
                {
                    temp = float.Parse(text + addedChar);
                }
                catch (System.Exception)
                {
                    return default(char);
                }

                return addedChar;
            };

            // 监听输入框编辑结束事件
            input.onEndEdit.AddListener((str) =>
            {
                float temp = float.Parse(str);

                temp = Mathf.Clamp(temp, slider.minValue, slider.maxValue);
                input.text = temp.ToString();
                slider.value = temp;
                Value = temp;
            });

            SetMinMax(minValue, maxValue);
        }

        public void SetMinMax(float min, float max)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = min;
            input.text = min.ToString();
            Value = min;
        }

        public class SliderMixInputEvent : UnityEvent<float>
        {
            public SliderMixInputEvent() { }
        }
    }
}