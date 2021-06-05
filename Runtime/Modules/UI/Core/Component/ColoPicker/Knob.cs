using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XFramework.UI
{
    [RequireComponent(typeof(Image))]
    public class Knob : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private RectTransform rectTransform;
        /// <summary>
        /// 水平方向
        /// </summary>
        public bool horizontal = true;
        /// <summary>
        /// 竖直方向
        /// </summary>
        public bool vertical = true;

        private Vector3 globalMousePos;
        /// <summary>
        /// 鼠标落在knob上的位置和面板位置差
        /// </summary>
        private Vector3 differ;

        public float top { get; private set; }
        public float bottom { get; private set; }
        public float left { get; private set; }
        public float right { get; private set; }
        private Vector2 m_Value;
        public Vector2 value
        {
            get
            {
                return m_Value;
            }
            set
            {
                float x = Mathf.Min(value.x, 1);
                float y = Mathf.Min(value.y, 1);

                Vector2 newPos = new Vector2(x * width + left, y * height + bottom);
                //rectTransform.position = newPos;

                m_Value = value;
                onValueChange.Invoke(m_Value);
            }
        }

        public float width
        {
            get
            {
                return right - left;
            }
        }

        public float height
        {
            get
            {
                return top - bottom;
            }
        }

        public Padding padding;

        public DragEvent onValueChange = new DragEvent();

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ResetArea();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out globalMousePos);
            differ = globalMousePos - rectTransform.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            //将拖拽时的坐标给予被拖拽对象的代替品
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out globalMousePos))
            {
                var rect = rectTransform.rect;
                //rect.size *= rectTransform.lossyScale;
                rect.center = (Vector2)rectTransform.position;

                Vector3 newPos = rectTransform.position;

                if (horizontal)
                    newPos.x = (globalMousePos - differ).x;
                if (vertical)
                    newPos.y = (globalMousePos - differ).y;

                if (newPos.y > top)
                    newPos.y = top;
                else if (newPos.y < bottom)
                    newPos.y = bottom;
                if (newPos.x < left)
                    newPos.x = left;
                else if (newPos.x > right)
                    newPos.x = right;
                rectTransform.position = newPos;

                float x = (newPos.x - left) / width;
                float y = (newPos.y - bottom) / height;

                m_Value = new Vector2(x, y);
                onValueChange.Invoke(m_Value);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
           
        }

        private void ResetArea()
        {
            RectTransform parentRect = rectTransform.parent as RectTransform;
            Vector2 pivot = parentRect.pivot;
            top = parentRect.position.y + parentRect.rect.size.y * (1 - pivot.y) - padding.top;
            bottom = parentRect.position.y - parentRect.rect.size.y * pivot.y + padding.bottom;
            left = parentRect.position.x - parentRect.rect.size.x * pivot.x + padding.left;
            right = parentRect.position.x + parentRect.rect.size.x * (1 - pivot.x) - padding.right;
        }

        [System.Serializable]
        public struct Padding
        {
            public float top;
            public float bottom;
            public float left;
            public float right;
        }

        public class DragEvent : UnityEvent<Vector2> { }
    }
}
