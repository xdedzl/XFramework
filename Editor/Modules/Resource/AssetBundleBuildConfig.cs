using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
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

    [Serializable]
    public struct AssetConfig
    {
        public string assetPath;
    }

    [Serializable]
    public struct GroupAssetConfig
    {
        public string abName;
        public AssetConfig[] assetPaths;
    }
    
    [CreateAssetMenu(fileName = "AssetBundleBuildConfig", menuName = "Scriptable Objects/AssetBundleBuildConfig")]
    public class AssetBundleBuildConfig : ScriptableObject
    {
        public PathConfig[] pathConfigs;

        public AssetConfig[] singleAssetConfigs;

        public GroupAssetConfig[] groupAssetConfigs;
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(AssetBundleBuildConfig))]
    public class AssetBundleBuildConfigEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginLeft = -12
                }
            };

            var scriptField = new ObjectField("Script")
            {
                objectType = typeof(MonoScript),
                allowSceneObjects = false,
                value = MonoScript.FromScriptableObject((AssetBundleBuildConfig)target)
            };
            scriptField.SetEnabled(false);
            root.Add(scriptField);

            root.Add(CreateConfigBox("按文件夹打包", serializedObject.FindProperty("pathConfigs"), "文件夹列表"));
            root.Add(CreateConfigBox("单资源独立打包", serializedObject.FindProperty("singleAssetConfigs"), "资源列表"));
            root.Add(CreateConfigBox("多资源合并打包", serializedObject.FindProperty("groupAssetConfigs"), "资源组列表"));
            root.Bind(serializedObject);
            return root;
        }

        private static VisualElement CreateConfigBox(string title, SerializedProperty property, string propertyLabel)
        {
            var box = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginTop = 1,
                    marginBottom = 1,
                    paddingLeft = 6,
                    paddingRight = 8,
                    paddingTop = 7,
                    paddingBottom = 7,
                    backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f),
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 1f),
                    borderRightColor = new Color(0.28f, 0.28f, 0.28f, 1f),
                    borderTopColor = new Color(0.28f, 0.28f, 0.28f, 1f),
                    borderBottomColor = new Color(0.28f, 0.28f, 0.28f, 1f)
                }
            };

            box.Add(new Label(title)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.88f, 0.88f, 0.88f, 1f),
                    marginBottom = 6
                }
            });

            var propertyField = new PropertyField(property, propertyLabel)
            {
                style =
                {
                    marginTop = 2,
                    marginLeft = 6
                }
            };
            box.Add(propertyField);
            return box;
        }
    }

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

            var pathField = new ObjectField
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                value = LoadFolderAsset(pathProperty.stringValue),
                style =
                {
                    width = 180,
                    minWidth = 120,
                    flexShrink = 0
                }
            };
            var pathLabel = new Label(GetPathText(pathProperty.stringValue))
            {
                tooltip = pathProperty.stringValue,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minWidth = 0,
                    marginLeft = 4,
                    marginRight = 4,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    color = new Color(0.75f, 0.75f, 0.75f)
                }
            };
            pathField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == null)
                {
                    SetFolderPath(pathProperty, pathField, pathLabel, string.Empty);
                    return;
                }

                string assetPath = AssetDatabase.GetAssetPath(evt.newValue);
                if (!AssetDatabase.IsValidFolder(assetPath))
                {
                    Debug.LogWarning($"[AssetBundleBuildConfig] PathConfig 只支持 Unity 工程内文件夹: {assetPath}");
                    pathField.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                SetFolderPath(pathProperty, pathField, pathLabel, assetPath);
            });

            var button = new Button(() =>
            {
                string path = EditorUtility.OpenFolderPanel("选择文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        path = "Assets" + path[Application.dataPath.Length..];

                    if (!AssetDatabase.IsValidFolder(path))
                    {
                        Debug.LogWarning($"[AssetBundleBuildConfig] PathConfig 只支持 Unity 工程内文件夹: {path}");
                        return;
                    }

                    SetFolderPath(pathProperty, pathField, pathLabel, path);
                }
            })
            {
                text = "📁",
                style =
                {
                    flexShrink = 0
                }
            };

            var buildTypeField = new EnumField((PackOption)buildTypeProperty.enumValueIndex)
            {
                bindingPath = buildTypeProperty.propertyPath,
                style =
                {
                    marginTop = 4,
                    minWidth = 100,
                    flexShrink = 0
                }
            };
            
            container.Add(pathField);
            container.Add(pathLabel);
            container.Add(button);
            container.Add(buildTypeField);

            return container;
        }

        private static DefaultAsset LoadFolderAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        }

        private static void SetFolderPath(SerializedProperty pathProperty, ObjectField pathField, Label pathLabel, string path)
        {
            pathProperty.stringValue = path;
            pathProperty.serializedObject.ApplyModifiedProperties();
            pathField.SetValueWithoutNotify(LoadFolderAsset(path));
            pathLabel.text = GetPathText(path);
            pathLabel.tooltip = path;
        }

        private static string GetPathText(string path)
        {
            return string.IsNullOrEmpty(path) ? "未选择" : path;
        }
    }

    [CustomPropertyDrawer(typeof(AssetConfig))]
    public class AssetConfigDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var assetPathProperty = property.FindPropertyRelative("assetPath");
            var assetField = new ObjectField
            {
                objectType = typeof(UnityEngine.Object),
                allowSceneObjects = false,
                value = LoadAsset(assetPathProperty.stringValue),
                style =
                {
                    width = 180,
                    minWidth = 120,
                    flexShrink = 0
                }
            };
            var pathLabel = new Label(GetPathText(assetPathProperty.stringValue))
            {
                tooltip = assetPathProperty.stringValue,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minWidth = 0,
                    marginLeft = 4,
                    marginRight = 4,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    color = new Color(0.75f, 0.75f, 0.75f)
                }
            };

            assetField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == null)
                {
                    SetAssetPath(assetPathProperty, assetField, pathLabel, string.Empty);
                    return;
                }

                string assetPath = AssetDatabase.GetAssetPath(evt.newValue);
                if (!IsValidFileAsset(assetPath))
                {
                    Debug.LogWarning($"[AssetBundleBuildConfig] 只支持 Assets 下的文件资源: {assetPath}");
                    assetField.SetValueWithoutNotify(evt.previousValue);
                    return;
                }

                SetAssetPath(assetPathProperty, assetField, pathLabel, assetPath);
            });

            container.Add(assetField);
            container.Add(pathLabel);
            return container;
        }

        private static UnityEngine.Object LoadAsset(string assetPath)
        {
            return IsValidFileAsset(assetPath)
                ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)
                : null;
        }

        private static bool IsValidFileAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.Replace("\\", "/").StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                && !AssetDatabase.IsValidFolder(assetPath)
                && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null;
        }

        private static void SetAssetPath(SerializedProperty assetPathProperty, ObjectField assetField, Label pathLabel, string assetPath)
        {
            assetPathProperty.stringValue = assetPath;
            assetPathProperty.serializedObject.ApplyModifiedProperties();
            assetField.SetValueWithoutNotify(LoadAsset(assetPath));
            pathLabel.text = GetPathText(assetPath);
            pathLabel.tooltip = assetPath;
        }

        private static string GetPathText(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? "未选择" : assetPath;
        }
    }

    [CustomPropertyDrawer(typeof(GroupAssetConfig))]
    public class GroupAssetConfigDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column
                }
            };

            var abNameProperty = property.FindPropertyRelative("abName");
            var assetPathsProperty = property.FindPropertyRelative("assetPaths");
            var abNameField = new TextField("包名")
            {
                bindingPath = abNameProperty.propertyPath,
                style =
                {
                    marginBottom = 4
                }
            };
            container.Add(abNameField);
            container.Add(new PropertyField(assetPathsProperty, "资源列表"));
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
