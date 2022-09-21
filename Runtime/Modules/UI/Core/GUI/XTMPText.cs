using TMPro;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(TextMeshProUGUI))]
    public class XTMPText : XUIBase
    {
        public TextMeshProUGUI text;

        private void Reset()
        {
            text = GetComponent<TextMeshProUGUI>();
        }
    }
}