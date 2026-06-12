using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(ColorAttribute))]
    public sealed class ColorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            ColorAttribute colorAttribute = attribute as ColorAttribute;
            Color oldColor = GUI.color;
            if (colorAttribute != null)
            {
                GUI.color = new Color(colorAttribute.R, colorAttribute.G, colorAttribute.B, colorAttribute.A);
            }

            EditorGUI.PropertyField(position, property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
            GUI.color = oldColor;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, LabelDrawerUtility.CreateLabel(fieldInfo, label), true);
        }
    }
}
