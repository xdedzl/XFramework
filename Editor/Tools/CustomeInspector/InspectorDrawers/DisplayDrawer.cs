using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(DisplayAttribute))]
    public sealed class DisplayDrawer : PropertyDrawer
    {
        private const float HiddenHeight = -2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!ShouldDisplay(property))
            {
                return;
            }

            EditorGUI.PropertyField(position, property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!ShouldDisplay(property))
            {
                return HiddenHeight;
            }

            return EditorGUI.GetPropertyHeight(property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
        }

        private bool ShouldDisplay(SerializedProperty property)
        {
            DisplayAttribute displayAttribute = attribute as DisplayAttribute;
            if (displayAttribute == null || string.IsNullOrEmpty(displayAttribute.Condition))
            {
                return true;
            }

            Object targetObject = property.serializedObject.targetObject;
            if (targetObject == null)
            {
                return true;
            }

            BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo condition = targetObject.GetType().GetMethod(displayAttribute.Condition, flags);
            if (condition == null || condition.ReturnType != typeof(bool) || condition.GetParameters().Length != 0)
            {
                return true;
            }

            object owner = condition.IsStatic ? null : targetObject;
            return (bool)condition.Invoke(owner, null);
        }
    }
}
