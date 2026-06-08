using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    [DefaultSportTypes(typeof(string))]
    public class StringElement : XInspectorElement
    {
        protected readonly TextField input;

        public StringElement()
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
            input.value = Value?.ToString();
        }

        private void OnValueChanged(ChangeEvent<string> v)
        {
            Value = v.newValue;
        }
    }

    
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(TextAreaAttribute))]
    public class TextAreaElement : StringElement
    {
        public TextAreaElement()
        {
            input.AddToClassList("text-area-element");
            input.multiline = true;
            input.style.whiteSpace = WhiteSpace.Normal;
            input.ElementAt(0).style.unityTextAlign = UnityEngine.TextAnchor.UpperLeft;
        }

        public TextAreaElement(int minHeight) : this()
        {
            if (minHeight > 0)
            {
                input.style.minHeight = minHeight;
            }
        }
    }
#endif
}