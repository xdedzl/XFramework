using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自定义属性绘制器，将枚举以位掩码的形式显示在Inspector上。
/// </summary>
[CustomPropertyDrawer(typeof(ShowAsFlagsAttribute))]
public class ShowAsFlagsDrawer : PropertyDrawer
{
    private MethodInfo _miIntToEnumFlags;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 如果不是枚举，则按默认显示
        if (property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUI.PropertyField(position, property);
            return;
        }

        if (_miIntToEnumFlags == null)
        {
#if UNITY_2019_1_OR_NEWER
            var assembly = AppDomain.CurrentDomain.GetAssemblies().First(item => item.FullName.StartsWith("UnityEditor"));
            var type = assembly.GetType("UnityEditor.EnumDataUtility");
#else
            var type = typeof(EditorGUI);
#endif
            _miIntToEnumFlags = type.GetMethod("IntToEnumFlags", BindingFlags.Static | BindingFlags.NonPublic);
        }

        // 复杂的转换问题，让Unity来解决（参考EditorGUI.EnumFlagsField()方法的反编译结果）
        Enum currentEnum = (Enum)_miIntToEnumFlags.Invoke(null, new object[] { fieldInfo.FieldType, property.intValue });
        EditorGUI.BeginProperty(position, label, property);
        Enum newEnum = EditorGUI.EnumFlagsField(position, label, currentEnum);
        property.intValue = Convert.ToInt32(newEnum);
        EditorGUI.EndProperty();

        // 备注：
        // 不能使用以下方式获取枚举值：
        // Enum currentEnum = (Enum)fieldInfo.GetValue(property.serializedObject.targetObject);
        // 使用以下方式时，如果ScriptableObject中包含一个某类型的数组，该类型中包含了Flags枚举，将会导致Editor抛出ArgumentException：
        // ArgumentException: Field <enum_flags> defined on type <host_type> is not a field on the target object which is of type <unity_object>.
    }
}
