using System;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    [DefaultSportTypes(typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(char))]
    public class IntegerElement : InspectorElement
    {
        private NumberParser numberParser;

        private readonly TextField input;

        public IntegerElement()
        {
            style.flexDirection = FlexDirection.Row;
            
            input = new TextField();
            input.AddToClassList("inspector-input");
            this.Add(input);
            input.RegisterValueChangedCallback(OnValueChanged);
        }

        protected override void OnBound()
        {
            base.OnBound();
            numberParser = new NumberParser(BoundVariableType);

            input.value = Value.ToString();
        }

        private void OnValueChanged(ChangeEvent<string> v)
        {
            if (string.IsNullOrEmpty(v.newValue))
            {
                input.value = "0";
            }
            else if (numberParser.TryParse(v.newValue, out object value))
            {
                Value = value;
                input.value = Value.ToString();
            }
            else
            {
                UnityEngine.Debug.LogWarning($"无法将值设为{v.newValue}");
                input.value = v.previousValue;
            }
        }
    }
}