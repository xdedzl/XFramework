using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XFramework;

namespace XFramework.UI
{
    public class ScreenTouch : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public DragEvent onBeginDrag = new();
        public DragEvent onDrag = new();
        public DragEvent onEndDrag = new();

        private static ScreenTouch _instacne;

        public  static ScreenTouch Instance
        {
            get
            {
                if (_instacne == null)
                {
                    _instacne = CreateScreenTouch();
                }

                return _instacne;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            onBeginDrag.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            onDrag.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            onEndDrag.Invoke(eventData);

        }

        private static ScreenTouch CreateScreenTouch()
        {
            //canvas
            var screenTouchCanvas = new GameObject("ScreenTouchCanvas");
            DontDestroyOnLoad(screenTouchCanvas);

            Canvas canvas = screenTouchCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = -999;

            CanvasScaler cs = screenTouchCanvas.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            cs.scaleFactor = 1;
            cs.referencePixelsPerUnit = 100;

            GraphicRaycaster gr = screenTouchCanvas.AddComponent<GraphicRaycaster>();
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.All;
            gr.ignoreReversedGraphics = true;
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            gr.blockingMask = default;

            //Init the event system if not exist.
            UnityEngine.EventSystems.EventSystem es = GameObject.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject eventsystem = new GameObject("EventSystem");
                eventsystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                UnityEngine.EventSystems.StandaloneInputModule sim = eventsystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            DontDestroyOnLoad(screenTouchCanvas);

            var screenTouchRect = new GameObject("ScreenTouch").AddComponent<RectTransform>();
            screenTouchRect.parent = screenTouchCanvas.transform;
            screenTouchRect.sizeDelta = new Vector2(10000, 10000);
            var screenTouch = screenTouchRect.gameObject.AddComponent<ScreenTouch>();
            var image = screenTouchRect.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            return screenTouch;

        }
        public class DragEvent : UnityEvent<PointerEventData> { }
    }
}
