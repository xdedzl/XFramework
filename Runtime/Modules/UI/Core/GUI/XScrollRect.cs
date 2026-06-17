using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(ScrollRect))]
    [UnityEngine.AddComponentMenu("XFramework/XScrollRect")]
    public class XScrollRect : XUIBase, IUIEventSource
    {
        public ScrollRect scrollRect;

        public System.Type ListenerType => typeof(UnityAction<Vector2>);

        private void Reset()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        public void AddOnValueChanged(UnityAction<Vector2> call)
        {
            scrollRect.onValueChanged.AddListener(call);
        }

        void IUIEventSource.AddListener(System.Delegate listener)
        {
            AddOnValueChanged((UnityAction<Vector2>)listener);
        }
    }
}
