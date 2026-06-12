using System;
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
        [InspectorName("资源列表")]
        [PrettyList]
        public AssetConfig[] assetPaths;
    }
    
    [CreateAssetMenu(fileName = "AssetBundleBuildConfig", menuName = "Scriptable Objects/AssetBundleBuildConfig")]
    public class AssetBundleBuildConfig : ScriptableObject
    {
        [InspectorName("按文件夹打包")]
        [PrettyList]
        public PathConfig[] pathConfigs;

        [InspectorName("单资源独立打包")]
        [PrettyList]
        public AssetConfig[] singleAssetConfigs;

        [InspectorName("多资源合并打包")]
        [PrettyList]
        public GroupAssetConfig[] groupAssetConfigs;
    }

    [CustomPropertyDrawer(typeof(PathConfig))]
    public class PathConfigDrawer : PropertyDrawer
    {
        private const float ObjectFieldPreferredWidth = 160f;
        private const float ObjectFieldMinWidth = 110f;
        private const float BuildTypeWidth = 120f;
        private const float PathLabelMinWidth = 80f;
        private const float Spacing = 4f;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty pathProperty = property.FindPropertyRelative(nameof(PathConfig.path));
            SerializedProperty buildTypeProperty = property.FindPropertyRelative(nameof(PathConfig.buildType));
            if (pathProperty == null || buildTypeProperty == null)
            {
                return new Label("Invalid PathConfig");
            }

            SerializedProperty pathPropertyCopy = pathProperty.Copy();
            VisualElement row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 20f
                }
            };

            ObjectField folderField = new ObjectField
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                tooltip = "选择打包文件夹",
                style =
                {
                    width = ObjectFieldPreferredWidth,
                    minWidth = ObjectFieldMinWidth,
                    flexShrink = 0f,
                    marginRight = Spacing
                }
            };

            Label pathLabel = new Label
            {
                style =
                {
                    minWidth = PathLabelMinWidth,
                    flexGrow = 1f,
                    flexShrink = 1f,
                    marginRight = Spacing,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            PropertyField buildTypeField = new PropertyField(buildTypeProperty.Copy())
            {
                label = string.Empty,
                style =
                {
                    width = BuildTypeWidth,
                    minWidth = BuildTypeWidth,
                    flexShrink = 0f
                }
            };

            row.Add(folderField);
            row.Add(pathLabel);
            row.Add(buildTypeField);

            void RefreshPath()
            {
                property.serializedObject.Update();
                if (pathPropertyCopy.hasMultipleDifferentValues)
                {
                    folderField.SetValueWithoutNotify(null);
                    pathLabel.text = "多个不同路径";
                    pathLabel.tooltip = "多个对象的路径不同";
                    return;
                }

                string path = pathPropertyCopy.stringValue;
                DefaultAsset currentFolder = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                folderField.SetValueWithoutNotify(currentFolder);
                pathLabel.text = path;
                pathLabel.tooltip = path;
            }

            folderField.RegisterValueChangedCallback(evt =>
            {
                property.serializedObject.Update();
                DefaultAsset selectedFolder = evt.newValue as DefaultAsset;
                if (selectedFolder == null)
                {
                    pathPropertyCopy.stringValue = string.Empty;
                }
                else
                {
                    string selectedPath = AssetDatabase.GetAssetPath(selectedFolder);
                    if (!AssetDatabase.IsValidFolder(selectedPath))
                    {
                        RefreshPath();
                        return;
                    }

                    pathPropertyCopy.stringValue = selectedPath;
                }

                property.serializedObject.ApplyModifiedProperties();
                RefreshPath();
            });

            row.TrackPropertyValue(pathPropertyCopy, _ => RefreshPath());
            RefreshPath();
            return row;
        }
    }

    [CustomPropertyDrawer(typeof(AssetConfig))]
    public class AssetConfigDrawer : PropertyDrawer
    {
        private const float ObjectFieldPreferredWidth = 180f;
        private const float ObjectFieldMinWidth = 110f;
        private const float PathLabelMinWidth = 80f;
        private const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty assetPathProperty = property.FindPropertyRelative(nameof(AssetConfig.assetPath));
            if (assetPathProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "Invalid AssetConfig");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            float objectFieldWidth = Mathf.Clamp(lineRect.width - PathLabelMinWidth - Spacing, ObjectFieldMinWidth, ObjectFieldPreferredWidth);
            if (lineRect.width < ObjectFieldMinWidth + Spacing)
            {
                objectFieldWidth = Mathf.Max(0f, lineRect.width);
            }

            Rect objectFieldRect = new Rect(lineRect.x, lineRect.y, objectFieldWidth, lineRect.height);
            Rect pathLabelRect = new Rect(
                objectFieldRect.xMax + Spacing,
                lineRect.y,
                Mathf.Max(0f, lineRect.xMax - objectFieldRect.xMax - Spacing),
                lineRect.height);

            UnityEngine.Object currentAsset = string.IsNullOrEmpty(assetPathProperty.stringValue)
                ? null
                : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPathProperty.stringValue);
            UnityEngine.Object selectedAsset = EditorGUI.ObjectField(objectFieldRect, currentAsset, typeof(UnityEngine.Object), false);
            if (selectedAsset != currentAsset)
            {
                if (selectedAsset == null)
                {
                    assetPathProperty.stringValue = string.Empty;
                }
                else
                {
                    string selectedPath = AssetDatabase.GetAssetPath(selectedAsset);
                    if (AssetBundleBuildConfigDrawerGUI.IsValidAssetPath(selectedPath))
                    {
                        assetPathProperty.stringValue = selectedPath;
                    }
                }
            }

            AssetBundleBuildConfigDrawerGUI.DrawPathLabel(pathLabelRect, assetPathProperty.stringValue);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }

    [CustomPropertyDrawer(typeof(GroupAssetConfig))]
    public class GroupAssetConfigDrawer : PropertyDrawer
    {
        private const float FoldoutWidth = 14f;
        private const float CountWidth = 68f;
        private const float ChildIndent = 16f;
        private const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty abNameProperty = property.FindPropertyRelative(nameof(GroupAssetConfig.abName));
            SerializedProperty assetPathsProperty = property.FindPropertyRelative(nameof(GroupAssetConfig.assetPaths));
            if (abNameProperty == null || assetPathsProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "Invalid GroupAssetConfig");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldoutRect = new Rect(lineRect.x, lineRect.y, FoldoutWidth, lineRect.height);
            Rect countRect = new Rect(lineRect.xMax - CountWidth, lineRect.y, CountWidth, lineRect.height);
            Rect abNameRect = new Rect(
                foldoutRect.xMax + Spacing,
                lineRect.y,
                Mathf.Max(0f, countRect.x - foldoutRect.xMax - Spacing * 2f),
                lineRect.height);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);
            abNameProperty.stringValue = EditorGUI.TextField(abNameRect, abNameProperty.stringValue);
            GUI.Label(countRect, AssetBundleBuildConfigDrawerGUI.GetCountText(assetPathsProperty), AssetBundleBuildConfigDrawerGUI.CountLabelStyle);

            if (property.isExpanded)
            {
                Rect childRect = new Rect(
                    position.x + ChildIndent,
                    lineRect.yMax + Spacing,
                    Mathf.Max(0f, position.width - ChildIndent),
                    EditorGUI.GetPropertyHeight(assetPathsProperty, GUIContent.none, true));
                EditorGUI.PropertyField(childRect, assetPathsProperty, GUIContent.none, true);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
            {
                SerializedProperty assetPathsProperty = property.FindPropertyRelative(nameof(GroupAssetConfig.assetPaths));
                if (assetPathsProperty != null)
                {
                    height += Spacing + EditorGUI.GetPropertyHeight(assetPathsProperty, GUIContent.none, true);
                }
            }

            return height;
        }
    }

    internal static class AssetBundleBuildConfigDrawerGUI
    {
        public static readonly GUIStyle CountLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip
        };

        private static readonly GUIStyle PathLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip
        };

        public static void DrawPathLabel(Rect rect, string path)
        {
            GUI.Label(rect, GetEllipsizedText(path, rect.width, PathLabelStyle), PathLabelStyle);
        }

        public static string GetCountText(SerializedProperty arrayProperty)
        {
            return arrayProperty.arraySize == 0 ? "Empty" : $"{arrayProperty.arraySize} items";
        }

        public static bool IsValidAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                   && !AssetDatabase.IsValidFolder(path)
                   && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null;
        }

        private static string GetEllipsizedText(string text, float width, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f)
            {
                return string.Empty;
            }

            if (style.CalcSize(new GUIContent(text)).x <= width)
            {
                return text;
            }

            const string ellipsis = "...";
            if (style.CalcSize(new GUIContent(ellipsis)).x > width)
            {
                return string.Empty;
            }

            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid) + ellipsis;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return text.Substring(0, low) + ellipsis;
        }
    }
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
