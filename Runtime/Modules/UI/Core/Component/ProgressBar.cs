// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-12-17 17:23:42
// 版本： V 1.0
// ==========================================
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine;

namespace XFramework.UI
{
    public enum ProgressBarType
    {
        FillImage,
        ModifyRect
    }

    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class ProgressBar : MonoBehaviour
    {
        [SerializeField]
        private ProgressBarType progressBarType = ProgressBarType.ModifyRect;

        [SerializeField]
        private Image targetImage;
        
        [SerializeField]
        [Range(0f, 1f)]
        private float m_Value;


        public readonly ProgressEvent onValueChange = new();

        public float value
        {
            get
            {
                return m_Value;
            }
            set
            {
                if(m_Value != value)
                {
                    m_Value = value;
                    OnValueChange();
                }
            }
        }

        private void OnValueChange()
        {
            onValueChange.Invoke(value);

            Refresh();
        }

        private void Refresh()
        {
            if (progressBarType == ProgressBarType.FillImage)
            {
                targetImage.fillAmount = value;
            }
            else
            {
                targetImage.rectTransform.anchorMin = Vector2.zero;
                targetImage.rectTransform.anchorMax = new Vector2(value, 1);
                targetImage.rectTransform.offsetMin = Vector2.zero;
                targetImage.rectTransform.offsetMax = Vector2.zero;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Refresh();
        }
#endif



        public class ProgressEvent : UnityEvent<float> { }

    }
}