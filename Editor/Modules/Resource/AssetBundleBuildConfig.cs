using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public enum PackOption
    {
        AllFiles,       // 所有文件一个包 
        TopDirectory,   // 一级子文件夹单独打包
        AllDirectory,   // 所有子文件夹单独打包
        TopFileOnly,    // 只打包当前文件夹的文件
    }
    
    [Serializable]
    public struct PathConfig
    {
        public string path;                
        public PackOption buildType;
    }
    
    [CreateAssetMenu(fileName = "AssetBundleBuildConfig", menuName = "Scriptable Objects/AssetBundleBuildConfig")]
    public class AssetBundleBuildConfig : ScriptableObject
    {
        public PathConfig[] pathConfigs;
    }
    
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(PathConfig))]
    public class PathConfigDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd // 让内容靠右对齐
                }
            };

            var pathProperty = property.FindPropertyRelative("path");
            var buildTypeProperty = property.FindPropertyRelative("buildType");

            var pathField = new TextField
            {
                bindingPath = pathProperty.propertyPath,
                isReadOnly = true,
                style =
                {
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
                    pathProperty.stringValue = path;
                    pathProperty.serializedObject.ApplyModifiedProperties(); // 应用更改
                    pathField.value = path;
                }
            })
            {
                text = "📁"
            };

            var buildTypeField = new EnumField((PackOption)buildTypeProperty.enumValueIndex)
            {
                bindingPath = buildTypeProperty.propertyPath,
                style =
                {
                    marginTop = 4,
                    minWidth = 100
                }
            };
            
            container.Add(pathField);
            container.Add(button);
            container.Add(buildTypeField);

            return container;
        }
    }
#endif
}



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
                property.serializedObject.ApplyModifiedProperties(); // 应用更改
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
