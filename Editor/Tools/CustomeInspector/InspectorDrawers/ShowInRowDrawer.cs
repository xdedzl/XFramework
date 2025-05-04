using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(ShowInRowAttribute))]
    public class ShowInRowDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = attribute as ShowInRowAttribute;
            var fieldNames = attr.FieldNames;
            var eachFieldWidth = position.width / fieldNames.Length;
            EditorGUI.BeginProperty(position, label, property);

            // 依次绘制每个字段
            for (int i = 0; i < fieldNames.Length; i++)
            {
                var x = position.x + eachFieldWidth * i;
                var rect = new Rect(x, position.y, eachFieldWidth, position.height);
                EditorGUI.PropertyField(rect, property.FindPropertyRelative(fieldNames[i]), GUIContent.none);
            }

            EditorGUI.EndProperty();
        }
    }
}
