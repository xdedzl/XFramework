using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XFramework.UI
{
    /// <summary>
    /// 可长按按钮
    /// </summary>
    public class LongOrDoubleBtn : UnityEngine.UI.Button
    {
        /// <summary>
        /// 是否为长按
        /// </summary>
        public bool isLongPressTrigger;
        public LongClickEvent onLongClick = new LongClickEvent();
        public UnityEvent onDoubleClick = new UnityEvent();

        /// <summary>
        /// 长按最大时间，超过后系数为1
        /// 单位 秒
        /// </summary>
        public float maxTime = 1;

        private float startTime;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            startTime = Time.time;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (maxTime <= 0)
                throw new System.Exception("时间初始值不得小于或等于0");
            onLongClick.Invoke(Mathf.Min(1, (Time.time - startTime) / maxTime));
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount == 1)
            {
                onClick.Invoke();
            }
            else if (eventData.clickCount == 2)
            {
                onDoubleClick.Invoke();
            }
        }

        [System.Serializable]
        public class LongClickEvent : UnityEvent<float> { }
    }
}