using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    internal static class LabelDrawerUtility
    {
        public static GUIContent CreateLabel(FieldInfo fieldInfo, GUIContent fallbackLabel)
        {
            LabelAttribute labelAttribute = fieldInfo?.GetCustomAttribute<LabelAttribute>(true);
            if (labelAttribute == null || string.IsNullOrEmpty(labelAttribute.Text))
            {
                return fallbackLabel;
            }

            return new GUIContent(labelAttribute.Text, fallbackLabel.tooltip);
        }
    }
}
