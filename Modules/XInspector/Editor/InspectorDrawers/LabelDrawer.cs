using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public sealed class LabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.PropertyField(position, property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
        }
    }
}
