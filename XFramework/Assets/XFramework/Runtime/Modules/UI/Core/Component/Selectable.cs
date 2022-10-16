using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XFramework.UI
{
    /// <summary>
    /// 可左键触发的UI
    /// </summary>
    public class Selectable : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public PointerEvent OnClick = new PointerEvent();
        public PointerEvent OnEnter = new PointerEvent();
        public PointerEvent OnExit = new PointerEvent();

        public void OnPointerClick(PointerEventData eventData)
        {
            // 左键触发
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnClick.Invoke(transform);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 左键触发
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnEnter.Invoke(transform);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 左键触发
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnExit.Invoke(transform);
            }
        }

        public class PointerEvent : UnityEvent<Transform> { }
    }
}