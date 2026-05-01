using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace XFramework
{
    public class AssetFolderPathAttribute : PropertyAttribute { }
    
    public class ReadOnlyAttribute : PropertyAttribute { }

    public class TextDropdownAttribute : PropertyAttribute
    {
        public Type ProviderType { get; }
        public string MethodName { get; }
        public bool AllowEmpty { get; }

        public TextDropdownAttribute(string methodName, bool allowEmpty = true)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                throw new ArgumentException("TextDropdownAttribute 需要提供获取文本选项的方法名", nameof(methodName));
            }

            MethodName = methodName;
            AllowEmpty = allowEmpty;
        }

        public TextDropdownAttribute(Type providerType, string methodName, bool allowEmpty = true) : this(methodName, allowEmpty)
        {
            ProviderType = providerType;
        }
    }

    public class AssetPathAttribute : PropertyAttribute
    {
        public Type targetType;
        public AssetPathAttribute(Type assetType = null)
        {
            if (assetType != null && !assetType.IsSubclassOf(typeof(Object)))
            {
                throw new ArgumentException("AssetPathAttribute 只能用于 UnityEngine.Object 的子类");
            }
            targetType = assetType;
        }
    }
}
#if UNITY_EDITOR
namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(AssetFolderPathAttribute))]
    public class FolderPathDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd // 让内容靠右对齐
                }
            };
            var textField = new TextField(property.displayName)
            {
                value = property.stringValue,
                isReadOnly = true,
                style =
                {
                    flexShrink = 0,
                    flexGrow = 1 // 让文本框占据剩余空间
                }
            };
            var button = new Button(() =>
            {
                string path = EditorUtility.OpenFolderPanel("选择文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        path = "Assets" + path[Application.dataPath.Length..];
                    property.stringValue = path;
                    textField.value = path;
                }
            })
            {
                text = "📁"
            };

            container.Add(textField);
            container.Add(button);
            return container;
        }
    }
    
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 禁用GUI的交互能力，使其变为不可编辑状态
            GUI.enabled = false;
            // 绘制属性字段，但此时它已经是灰色的不可编辑状态
            EditorGUI.PropertyField(position, property, label, true);
            // 恢复GUI的交互能力，以免影响后续元素的绘制
            GUI.enabled = true;
        }
        // 确保只读字段的显示高度正确
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    [CustomPropertyDrawer(typeof(TextDropdownAttribute))]
    public class TextDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.HelpBox(position, $"{label.text} 只能在 string 字段上使用 TextDropdown", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            TextDropdownAttribute textDropdownAttribute = (TextDropdownAttribute)attribute;
            if (!TryGetChoices(property, textDropdownAttribute, out List<string> choices, out string error))
            {
                EditorGUI.HelpBox(position, error, MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            if (textDropdownAttribute.AllowEmpty && !choices.Contains(string.Empty))
            {
                choices.Insert(0, string.Empty);
            }

            string currentValue = property.stringValue ?? string.Empty;
            if (!string.IsNullOrEmpty(currentValue) && !choices.Contains(currentValue))
            {
                choices.Insert(0, currentValue);
            }

            if (choices.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.Popup(position, label.text, 0, new[] { "No Options" });
                }

                EditorGUI.EndProperty();
                return;
            }

            int currentIndex = Mathf.Max(0, choices.IndexOf(currentValue));
            string[] displayOptions = CreateDisplayOptions(choices);

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(position, label.text, currentIndex, displayOptions);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = choices[Mathf.Clamp(newIndex, 0, choices.Count - 1)];
            }
            EditorGUI.showMixedValue = false;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        private bool TryGetChoices(
            SerializedProperty property,
            TextDropdownAttribute textDropdownAttribute,
            out List<string> choices,
            out string error)
        {
            choices = new List<string>();
            error = string.Empty;

            object targetObject = property.serializedObject.targetObject;
            Type targetType = textDropdownAttribute.ProviderType ?? targetObject.GetType();
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = targetType.GetMethod(textDropdownAttribute.MethodName, flags);

            if (method == null)
            {
                error = $"{targetType.Name} 未找到无参方法 {textDropdownAttribute.MethodName}";
                return false;
            }

            if (method.GetParameters().Length > 0)
            {
                error = $"{targetType.Name}.{method.Name} 必须是无参方法";
                return false;
            }

            object providerInstance = method.IsStatic ? null : targetObject;
            if (!method.IsStatic && !targetType.IsInstanceOfType(targetObject))
            {
                error = $"{targetType.Name}.{method.Name} 必须是静态方法，或定义在当前目标对象上";
                return false;
            }

            object result = method.Invoke(providerInstance, null);
            if (result == null)
            {
                return true;
            }

            if (result is string singleValue)
            {
                AddChoice(choices, singleValue);
                return true;
            }

            if (result is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item is string value)
                    {
                        AddChoice(choices, value);
                    }
                }

                return true;
            }

            error = $"{targetType.Name}.{method.Name} 需要返回 string 或 IEnumerable<string>";
            return false;
        }

        private static void AddChoice(List<string> choices, string value)
        {
            if (!choices.Contains(value))
            {
                choices.Add(value);
            }
        }

        private static string[] CreateDisplayOptions(IReadOnlyList<string> choices)
        {
            string[] displayOptions = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                displayOptions[i] = string.IsNullOrEmpty(choices[i]) ? "None" : choices[i];
            }

            return displayOptions;
        }
    }

    [CustomPropertyDrawer(typeof(AssetPathAttribute))]
    public class AssetPathDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = fieldInfo.GetCustomAttribute<AssetPathAttribute>();
            var targetType = attr.targetType ?? typeof(Object);
            
            EditorGUI.BeginProperty(position, label, property);
            // 通过路径加载资源
            Object asset = null;
            if (!string.IsNullOrEmpty(property.stringValue))
            {
                asset = AssetDatabase.LoadAssetAtPath<Object>(property.stringValue);
            }
            // 显示ObjectField
            Object newAsset = EditorGUI.ObjectField(
                position,
                label,
                asset,
                targetType,
                false // 禁止场景对象
            );
            // 如果选择了新资源，则更新路径
            if (newAsset != asset)
            {
                string path = newAsset != null ? AssetDatabase.GetAssetPath(newAsset) : string.Empty;
                property.stringValue = path;
            }
            EditorGUI.EndProperty(); 
        }
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
#endif
