using TMPro;
using UnityEngine.Events;

namespace XFramework.UI
{
    [UnityEngine.RequireComponent(typeof(TMP_InputField))]
    public class XTMPInputField : XUIBase, IUIMultiEventSource
    {
        public static class Events
        {
            public const string ValueChanged = nameof(ValueChanged);
            public const string EndEdit = nameof(EndEdit);
            public const string ValidateInput = nameof(ValidateInput);
        }

        public TMP_InputField inputField;

        public System.Type ListenerType => typeof(UnityAction<string>);

        private void Reset()
        {
            inputField = transform.GetComponent<TMP_InputField>();
        }

        public void AddOnEditorEnd(UnityAction<string> call)
        {
            inputField.onEndEdit.AddListener(call);
        }

        public void AddOnValidateInput(TMP_InputField.OnValidateInput call)
        {
            inputField.onValidateInput += call;
        }

        public void AddOnValueChanged(UnityAction<string> call)
        {
            inputField.onValueChanged.AddListener(call);
        }

        void IUIEventSource.AddListener(System.Delegate listener)
        {
            AddOnValueChanged((UnityAction<string>)listener);
        }

        public System.Type GetListenerType(string eventName)
        {
            return eventName switch
            {
                Events.ValueChanged => typeof(UnityAction<string>),
                Events.EndEdit => typeof(UnityAction<string>),
                Events.ValidateInput => typeof(TMP_InputField.OnValidateInput),
                _ => null
            };
        }

        public void AddListener(string eventName, System.Delegate listener)
        {
            switch (eventName)
            {
                case Events.ValueChanged:
                    AddOnValueChanged((UnityAction<string>)listener);
                    break;
                case Events.EndEdit:
                    AddOnEditorEnd((UnityAction<string>)listener);
                    break;
                case Events.ValidateInput:
                    AddOnValidateInput((TMP_InputField.OnValidateInput)listener);
                    break;
                default:
                    throw new XFrameworkException($"Unsupported XTMPInputField event: {eventName}");
            }
        }
    }
}
