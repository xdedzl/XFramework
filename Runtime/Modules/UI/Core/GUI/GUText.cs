using UnityEngine.UI;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(Text))]
    [UnityEngine.AddComponentMenu("XFramework/GUText")]
    public class GUText : GUIBase
    {
        public Text text;
        private void Reset()
        {
            text = transform.GetComponent<Text>();
        }
    }
}