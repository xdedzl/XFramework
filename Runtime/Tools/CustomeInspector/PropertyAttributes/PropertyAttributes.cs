using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework
{
    public class AssetFolderPathAttribute : PropertyAttribute { }
    
    public class ReadOnlyAttribute : PropertyAttribute { }
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
}
#endif