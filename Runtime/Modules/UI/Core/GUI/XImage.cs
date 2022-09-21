using UnityEngine;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.UI.Image))]
    [UnityEngine.AddComponentMenu("XFramework/XImage")]
    public class XImage : XUIBase
    {
        public UnityEngine.UI.Image image;

        private void Reset()
        {
            image = transform.GetComponent<UnityEngine.UI.Image>();
        }
    }
}