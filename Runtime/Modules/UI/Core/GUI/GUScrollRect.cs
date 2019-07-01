using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public class GUScrollRect : BaseGUI
    {
        public ScrollRect scrollRect;

        private void Reset()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        public void AddOnValueChanged(UnityAction<Vector2> call)
        {
            scrollRect.onValueChanged.AddListener(call);
        }
    }
}