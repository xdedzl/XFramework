using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace XFramework.UI
{
    [SupportHelper(typeof(UnityObjectSupport))]
    public sealed class UnityObjectElement : XInspectorElement
    {
        private readonly ObjectField m_ObjectField;

        public UnityObjectElement()
        {
            style.flexDirection = FlexDirection.Row;

            m_ObjectField = new ObjectField
            {
                allowSceneObjects = true
            };
            m_ObjectField.AddToClassList("inspector-input");
            m_ObjectField.RegisterValueChangedCallback(evt => Value = evt.newValue);
            Add(m_ObjectField);
        }

        protected override void OnBound()
        {
            base.OnBound();
            m_ObjectField.objectType = BoundVariableType;
            m_ObjectField.SetValueWithoutNotify(Value as Object);
        }

        public override void Refresh()
        {
            base.Refresh();
            m_ObjectField.SetValueWithoutNotify(Value as Object);
        }
    }

    public sealed class UnityObjectSupport : ISupport
    {
        public bool Support(Type type)
        {
            return typeof(Object).IsAssignableFrom(type);
        }
    }
}
