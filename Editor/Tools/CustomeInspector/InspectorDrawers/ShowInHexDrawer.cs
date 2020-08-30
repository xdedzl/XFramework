using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(ShowInHexAttribute))]
    public class ShowInHexDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = attribute as ShowInHexAttribute;

            EditorGUI.BeginProperty(position, label, property);

            // 只能处理整数
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                string hexValue = System.Convert.ToString(property.intValue, 16).ToUpper();
                int placeCount = attr.places - hexValue.Length;
                switch (placeCount)
                {
                    case 1: hexValue = "0x0" + hexValue; break;
                    case 2: hexValue = "0x00" + hexValue; break;
                    case 3: hexValue = "0x000" + hexValue; break;
                    case 4: hexValue = "0x0000" + hexValue; break;
                    case 5: hexValue = "0x00000" + hexValue; break;
                    case 6: hexValue = "0x000000" + hexValue; break;
                    case 7: hexValue = "0x0000000" + hexValue; break;
                    default: hexValue = "0x" + hexValue; break;
                }
                EditorGUI.TextField(position, label, hexValue);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }

            EditorGUI.EndProperty();
        }
    }
}
