using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(PasswordAttribute))]
    public sealed class PasswordDrawer : PropertyDrawer
    {
        private const string ErrorMessage = "[Password] can only be used on string fields.";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new HelpBox(ErrorMessage, HelpBoxMessageType.Error);
            }

            var field = new TextField(property.displayName)
            {
                isPasswordField = true
            };
            field.BindProperty(property);
            return field;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, ErrorMessage, MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.PasswordField(position, label, property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = value;
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
