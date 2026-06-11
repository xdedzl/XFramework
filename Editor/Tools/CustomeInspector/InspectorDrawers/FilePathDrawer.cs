using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(FilePathAttribute))]
    public sealed class FilePathPropertyDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 24f;
        private const float Spacing = 2f;
        private const string ErrorMessage = "[FilePath] can only be used on string fields.";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                return new HelpBox(ErrorMessage, HelpBoxMessageType.Error);
            }

            var field = new TextField(property.displayName)
            {
                style =
                {
                    flexGrow = 1f
                }
            };
            field.BindProperty(property);

            var button = new Button(() =>
            {
                var filePathAttribute = (FilePathAttribute)attribute;
                string path = EditorUtility.OpenFilePanel("Select File", Application.dataPath, filePathAttribute.Extension);
                ApplySelectedPath(property, path);
            })
            {
                text = "...",
                style =
                {
                    width = ButtonWidth,
                    flexShrink = 0f
                }
            };

            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };
            container.Add(field);
            container.Add(button);
            return container;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, ErrorMessage, MessageType.Error);
                return;
            }

            var buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, EditorGUIUtility.singleLineHeight);
            var fieldRect = new Rect(position.x, position.y, Mathf.Max(0f, position.width - ButtonWidth - Spacing), EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.TextField(fieldRect, label, property.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = value;
            }

            if (GUI.Button(buttonRect, EditorGUIUtility.IconContent("Folder Icon"), EditorStyles.iconButton))
            {
                var filePathAttribute = (FilePathAttribute)attribute;
                string path = EditorUtility.OpenFilePanel("Select File", Application.dataPath, filePathAttribute.Extension);
                ApplySelectedPath(property, path);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType == SerializedPropertyType.String
                ? EditorGUIUtility.singleLineHeight
                : EditorGUIUtility.singleLineHeight * 2f;
        }

        private static void ApplySelectedPath(SerializedProperty property, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            property.serializedObject.Update();
            property.stringValue = ToProjectPath(path);
            property.serializedObject.ApplyModifiedProperties();
        }

        private static string ToProjectPath(string path)
        {
            string normalizedPath = path.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            return normalizedPath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + normalizedPath.Substring(dataPath.Length)
                : normalizedPath;
        }
    }
}
