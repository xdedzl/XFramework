using UnityEngine.UIElements;

namespace XFramework.UI
{
    [DefaultSportTypes(typeof(bool))]
    public class BooleanElement : XInspectorElement
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

        public override void Refresh()
        {
            base.Refresh();
            toggle.SetValueWithoutNotify((bool)Value);
        }

        private void OnValueChanged(ChangeEvent<bool> e)
        {
            Value = e.newValue;
        }
    }
}
