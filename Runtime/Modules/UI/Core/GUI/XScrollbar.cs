using UnityEngine.UI;
using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(Scrollbar))]
    [UnityEngine.AddComponentMenu("XFramework/XScrollbar")]
    public class XScrollbar : XUIBase
    {
        public Scrollbar scrollbar;

        private void Reset()
        {
            scrollbar = GetComponent<Scrollbar>();
        }

        public void AddListener(UnityEngine.Events.UnityAction<float> call)
        {
            scrollbar.onValueChanged.AddListener(call);
        }
    }
}
