using UnityEngine.UI;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(Text))]
    [UnityEngine.AddComponentMenu("XFramework/XText")]
    public class XText : GUIBase
    {
        public Text text;
        private void Reset()
        {
            text = transform.GetComponent<Text>();
        }
    }
}