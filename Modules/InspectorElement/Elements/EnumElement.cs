#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    [SupportHelper(typeof(EnumSupport))]
    public class EnumElement : InspectorElement
    {
        private readonly EnumField enumField;

        public EnumElement()
        {
            style.flexDirection = FlexDirection.Row;
            
            enumField = new EnumField();
            enumField.AddToClassList("inspector-input");
            this.Add(enumField);
        }

        protected override void OnBound()
        {
            base.OnBound();
            enumField.Init(Value as Enum);
            enumField.RegisterValueChangedCallback(OnValueChanged);
        }

        private void OnValueChanged(ChangeEvent<Enum> e)
        {
            Value = e.newValue;
        }

        private struct EnumSupport : ISupport
        {
            public bool Support(Type type)
            {
                if (type.IsEnum)
                {
                    var attr = type.GetCustomAttribute<FlagsAttribute>();
                    return attr == null;
                }
                return false;
            }
        }
    }
}

#endif