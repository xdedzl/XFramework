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

        public void OnBind(IBindableDataCell<string> bindableData)
        {
            text.text = bindableData.Value;
        }
    }
}