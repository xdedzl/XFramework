using TMPro;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(TextMeshProUGUI))]
    public class GUTMPText : GUIBase
    {
        public TextMeshProUGUI text;

        private void Reset()
        {
            text = GetComponent<TextMeshProUGUI>();
        }
    }
}