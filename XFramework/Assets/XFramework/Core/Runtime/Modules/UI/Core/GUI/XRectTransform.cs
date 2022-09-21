namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.RectTransform))]
    [UnityEngine.AddComponentMenu("XFramework/XRectTransform")]
    public class XRectTransform : XUIBase
    {
        public UnityEngine.RectTransform rect;

        private void Reset()
        {
            rect = transform.GetComponent<UnityEngine.RectTransform>();
        }
    }

}