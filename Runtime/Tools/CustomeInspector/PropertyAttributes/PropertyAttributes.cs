using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class AssetFolderPathAttribute : PropertyAttribute { }

    #if UNITY_EDITOR
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
    #endif
}