// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-12-17 17:23:42
// 版本： V 1.0
// ==========================================
using UnityEngine.UI;
using UnityEngine.Events;

namespace XFramework.UI
{
    public class ProgressBar : Image
    {
        private ProgressEvent onValueChange = new ProgressEvent();
        public Image targetImage;

        public float value = 0;

        public void ChangeValue(float _value)
        {
            if (value != _value)
            {
                value = _value;
                onValueChange.Invoke(_value);
                targetImage.fillAmount = value;
            }
        }

        class ProgressEvent : UnityEvent<float> { }
    }
}