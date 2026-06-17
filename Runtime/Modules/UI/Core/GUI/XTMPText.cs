using TMPro;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(TextMeshProUGUI))]
    public class XTMPText : XUIBase, IBindObject<string>
    {
        public TextMeshProUGUI text;

        private void Reset()
        {
            text = GetComponent<TextMeshProUGUI>();
        }

        public void SetText(string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        public void OnBind(IBindableDataCell<string> bindableData)
        {
            SetText(bindableData.Value);
        }
    }
}
