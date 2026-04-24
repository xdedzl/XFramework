#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    [SupportHelper(typeof(FlagsEnumSupport))]
    public class FlagsEnumElement : InspectorElement
    {
        private readonly VisualElement enumsContainer;
        public FlagsEnumElement()
        {
            this.AddToClassList("tagsEnum-element");
            enumsContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                }
            };
            enumsContainer.AddToClassList("inspector-input");
            this.Add(enumsContainer);

            Button addBtn = new Button(OnAddBtnClick)
            {
                text = "+",
            };
            this.Add(enumsContainer);
            this.Add(addBtn);
        }

        protected override void OnBound()
        {
            base.OnBound();
            var values = System.Enum.GetValues(BoundVariableType);

            foreach (var item in values)
            {
                var value = (int)item;
                if (value == 0)
                    continue;
                if ((value & (int)Value) == value)
                {
                    var e = Enum.ToObject(BoundVariableType, value) as Enum;
                    AddEnum(e);
                }
            }
        }

        private void AddEnum(Enum enumValue)
        {
            EnumField enumField = new EnumField();
            enumField.Init(enumValue);
            enumField.RegisterValueChangedCallback((e) =>
            {
                int v = (int)Value ^ Convert.ToInt32(e.previousValue);
                Value = v | Convert.ToInt32(e.newValue);
            });
            enumsContainer.Add(enumField);
        }

        private void OnAddBtnClick()
        {
            Enum v = Activator.CreateInstance(BoundVariableType) as Enum;
            AddEnum(v);
        }

        private struct FlagsEnumSupport : ISupport
        {
            public bool Support(Type type)
            {
                if (type.IsEnum)
                {
                    var attr = type.GetCustomAttribute<FlagsAttribute>();
                    return attr != null;
                }
                return false;
            }
        }
    }
}
#endif