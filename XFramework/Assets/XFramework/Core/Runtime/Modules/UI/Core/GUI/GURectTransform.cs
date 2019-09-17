namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.RectTransform))]
    [UnityEngine.AddComponentMenu("XFramework/GURectTransform")]
    public class GURectTransform : GUIBase
    {
        public UnityEngine.RectTransform rect;

        private void Reset()
        {
            rect = transform.GetComponent<UnityEngine.RectTransform>();
        }
    }

}