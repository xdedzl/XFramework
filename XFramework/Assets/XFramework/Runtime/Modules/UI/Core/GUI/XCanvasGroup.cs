using UnityEngine;

namespace XFramework.UI
{
    [RequireComponent(typeof(CanvasGroup))]

    public class XCanvasGroup : XUIBase
    {
        public CanvasGroup canvasGroup;

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}