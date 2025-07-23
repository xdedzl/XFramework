using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XFramework.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class Draggable : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public RectTransform targetRect;

        /// <summary>
        /// 鼠标在UI坐标上的位置
        /// </summary>
        private Vector3 globalMousePos;
        /// <summary>
        /// 鼠标落在面板上的位置和面板位置差
        /// </summary>
        private Vector3 differ;
        /// <summary>
        /// 可移动性
        /// </summary>
        public bool isMovable = true;
        /// <summary>
        /// 水平方向
        /// </summary>
        public bool horizontal = true;
        /// <summary>
        /// 竖直方向
        /// </summary>
        public bool vertical = true;
        /// <summary>
        /// 范围限制
        /// </summary>
        public bool limitByParent = true;

        public float Top { get; private set; }
        public float Bottom { get; private set; }
        public float Left { get; private set; }
        public float Right { get; private set; }
        public Padding padding;

        public DragEvent onBeginDrag = new DragEvent();
        public DragEvent onDrag = new DragEvent();
        public DragEvent onEndDrag = new DragEvent();

        public void Start()
        {
            if (targetRect == null)
                targetRect = GetComponent<RectTransform>();
            InitArea();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            InitArea();
            if (isMovable)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(targetRect, eventData.position, eventData.pressEventCamera, out globalMousePos);
                differ = globalMousePos - targetRect.position;
            }
            onBeginDrag.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isMovable)
            {
                //将拖拽时的坐标给予被拖拽对象的代替品
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(targetRect, eventData.position, eventData.pressEventCamera, out globalMousePos))
                {
                    if (horizontal)
                        targetRect.position = targetRect.position.WithX((globalMousePos - differ).x);
                    if (vertical)
                        targetRect.position = targetRect.position.WithY((globalMousePos - differ).y);

                    if (limitByParent)
                    {
                        if (targetRect.position.y > Top)
                            targetRect.position = targetRect.position.WithY(Top);
                        else if (targetRect.position.y < Bottom)
                            targetRect.position = targetRect.position.WithY(Bottom);
                        if (targetRect.position.x < Left)
                            targetRect.position = targetRect.position.WithX(Left);
                        else if (targetRect.position.x > Right)
                            targetRect.position = targetRect.position.WithX(Right);
                    }
                }
            }
            onDrag.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            onEndDrag.Invoke(eventData);
        }

        private void InitArea()
        {
            RectTransform parentRect = targetRect.parent as RectTransform;
            Vector2 pivot = parentRect.pivot;
            Top = parentRect.position.y + parentRect.rect.size.y * (1 - pivot.y) - padding.Top;
            Bottom = parentRect.position.y - parentRect.rect.size.y * pivot.y + padding.Bottom;
            Left = parentRect.position.x - parentRect.rect.size.x * pivot.x + padding.Left;
            Right = parentRect.position.x + parentRect.rect.size.x * (1 - pivot.x) - padding.Right;
        }

        [System.Serializable]
        public struct Padding
        {
            public float Top;
            public float Bottom;
            public float Left;
            public float Right;
        }

        public class DragEvent : UnityEvent<PointerEventData> { }
    }
}