using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(Draggable))]
    [UnityEngine.AddComponentMenu("XFramework/GUDraggable")]
    public class GUDragable : GUIBase
    {
        public Draggable draggable;

        private void Reset()
        {
            draggable = GetComponent<Draggable>();
        }

        public void AddOnBegainDrag(UnityAction<PointerEventData> call)
        {
            draggable.onBeginDrag.AddListener(call);
        }

        public void AddOnDrag(UnityAction<PointerEventData> call)
        {
            draggable.onDrag.AddListener(call);
        }

        public void AddOnEndDrag(UnityAction<PointerEventData> call)
        {
            draggable.onEndDrag.AddListener(call);
        }
    }
}