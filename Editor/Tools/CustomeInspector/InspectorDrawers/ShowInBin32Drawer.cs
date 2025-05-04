using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using XFramework;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(ShowInBin32Attribute))]
    public class ShowInBin32Drawer : PropertyDrawer
    {
        private StringBuilder _sb = new StringBuilder(32 + 3);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 只能处理整数
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                string bin32Value = Convert.ToString(property.intValue, 2);

                _sb.Remove(0, _sb.Length);
                for (int i = 0; i < 32 - bin32Value.Length; i++) { _sb.Append('0'); }
                _sb.Append(bin32Value);
                _sb.Insert(8, ' ');
                _sb.Insert(17, ' ');
                _sb.Insert(26, ' ');

                EditorGUI.TextField(position, label, _sb.ToString());
            }
            else
            {
                EditorGUI.PropertyField(position, property, label);
            }

            EditorGUI.EndProperty();
        }
    }
}
