using UnityEngine.UI;
using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(Scrollbar))]
    [UnityEngine.AddComponentMenu("XFramework/XScrollbar")]
    public class XScrollbar : XUIBase, IUIEventSource
    {
        public Scrollbar scrollbar;

        public System.Type ListenerType => typeof(UnityEngine.Events.UnityAction<float>);

        private void Reset()
        {
            scrollbar = GetComponent<Scrollbar>();
        }

        public void AddListener(UnityEngine.Events.UnityAction<float> call)
        {
            scrollbar.onValueChanged.AddListener(call);
        }

        void IUIEventSource.AddListener(System.Delegate listener)
        {
            AddListener((UnityEngine.Events.UnityAction<float>)listener);
        }
    }
}
