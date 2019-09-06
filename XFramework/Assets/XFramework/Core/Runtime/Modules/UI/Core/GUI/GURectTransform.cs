namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(UnityEngine.RectTransform))]
    public class GURectTransform : GUIBase
    {
        public UnityEngine.RectTransform rect;

        private void Reset()
        {
            rect = transform.GetComponent<UnityEngine.RectTransform>();
        }
    }

}