using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(Draggable))]
    public class GUDragable : BaseGUI
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