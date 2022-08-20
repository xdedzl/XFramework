using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(ScrollRect))]
    [UnityEngine.AddComponentMenu("XFramework/XScrollRect")]
    public class XScrollRect : GUIBase
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