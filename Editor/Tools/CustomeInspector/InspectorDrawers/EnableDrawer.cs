using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(EnableAttribute))]
    public sealed class EnableDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(!ShouldEnable(property)))
            {
                EditorGUI.PropertyField(position, property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
        }

        private bool ShouldEnable(SerializedProperty property)
        {
            EnableAttribute enableAttribute = attribute as EnableAttribute;
            if (enableAttribute == null || string.IsNullOrEmpty(enableAttribute.Condition))
            {
                return true;
            }

            Object targetObject = property.serializedObject.targetObject;
            if (targetObject == null)
            {
                return true;
            }

            BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo condition = targetObject.GetType().GetMethod(enableAttribute.Condition, flags);
            if (condition == null || condition.ReturnType != typeof(bool) || condition.GetParameters().Length != 0)
            {
                return true;
            }

            object owner = condition.IsStatic ? null : targetObject;
            return (bool)condition.Invoke(owner, null);
        }
    }
}
