using UnityEngine.UI;
using UnityEngine.Events;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(Image))]
    public class GUInputField : GUIBase
    {
        public InputField inputField;

        private void Reset()
        {
            inputField = transform.GetComponent<InputField>();
        }

        public void AddOnEditorEnd(UnityAction<string> call)
        {
            inputField.onEndEdit.AddListener(call);
        }

        public void AddOnValidateInput(InputField.OnValidateInput call)
        {
            inputField.onValidateInput = call;
        }

        public void AddOnValueChanged(UnityAction<string> call)
        {
            inputField.onValueChanged.AddListener(call);
        }
    }
}