using UnityEngine.UIElements;

namespace XFramework.UI
{
    [DefaultSportTypes(typeof(bool))]
    public class BooleanElement : InspectorElement
    {
        private readonly Toggle toggle;

        public BooleanElement()
        {
            style.flexDirection = FlexDirection.Row;
            
            toggle = new Toggle();
            this.Add(toggle);
        }

        protected override void OnBound()
        {
            base.OnBound();
            toggle.value = (bool)Value;
            toggle.RegisterValueChangedCallback(OnValueChanged);
        }

        private void OnValueChanged(ChangeEvent<bool> e)
        {
            Value = e.newValue;
        }
    }
}