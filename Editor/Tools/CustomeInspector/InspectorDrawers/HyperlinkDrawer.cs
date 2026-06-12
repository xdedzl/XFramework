using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(HyperlinkAttribute))]
    public sealed class HyperlinkDrawer : PropertyDrawer
    {
        private const string ErrorMessage = "[Hyperlink] can only be used on string fields.";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new HelpBox(ErrorMessage, HelpBoxMessageType.Error);
            }

            var hyperlinkAttribute = (HyperlinkAttribute)attribute;
            var button = new Button(() =>
            {
                string url = property.stringValue;
                if (!string.IsNullOrEmpty(url))
                {
                    Application.OpenURL(url);
                }
            })
            {
                text = string.IsNullOrEmpty(hyperlinkAttribute.Name)
                    ? property.stringValue
                    : hyperlinkAttribute.Name,
                tooltip = property.stringValue
            };

            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            button.style.color = new Color(0.25f, 0.5f, 1f);
            button.SetEnabled(!string.IsNullOrEmpty(property.stringValue));

            return button;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, ErrorMessage, MessageType.Error);
                return;
            }

            var hyperlinkAttribute = (HyperlinkAttribute)attribute;
            string text = string.IsNullOrEmpty(hyperlinkAttribute.Name)
                ? property.stringValue
                : hyperlinkAttribute.Name;

            EditorGUI.BeginProperty(position, label, property);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(property.stringValue)))
            {
                if (GUI.Button(position, text, EditorStyles.linkLabel))
                {
                    Application.OpenURL(property.stringValue);
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType == SerializedPropertyType.String
                ? EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight * 2f;
        }
    }
}
