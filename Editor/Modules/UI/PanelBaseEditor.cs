using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework;
using XFramework.UI;
using UEditor = UnityEditor.Editor;

namespace XFramework.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(PanelBase), true)]
    public sealed class PanelBaseEditor : UEditor
    {
        private const string ScriptPropertyName = "m_Script";
        private const float KeyLabelWidth = 160f;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1f
                }
            };

            CreateDefaultInspector(root);
            CreatePanelXUIList(root);

            return root;
        }

        private void CreateDefaultInspector(VisualElement root)
        {
            using SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                SerializedProperty property = iterator.Copy();
                PropertyField field = new PropertyField(property);
                field.Bind(serializedObject);
                if (property.name == ScriptPropertyName)
                {
                    field.SetEnabled(false);
                }

                root.Add(field);
                enterChildren = false;
            }
        }

        private void CreatePanelXUIList(VisualElement root)
        {
            PanelBase panel = target as PanelBase;
            if (panel == null)
            {
                return;
            }

            List<ComponentFindHelper<XUIBase>.ComponentInfo> components = ComponentFindHelper<XUIBase>.CollectComponents(panel.gameObject);
            Dictionary<string, int> keyCounts = CountKeys(components);
            VisualElement section = CreateSection();
            section.Add(CreateTitle($"Panel XUIBase：{components.Count}"));

            for (int i = 0; i < components.Count; i++)
            {
                ComponentFindHelper<XUIBase>.ComponentInfo componentInfo = components[i];
                section.Add(CreateItem(
                    componentInfo.Component,
                    componentInfo.Key,
                    keyCounts[componentInfo.Key] > 1));
            }

            root.Add(section);
        }

        private static VisualElement CreateSection()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginTop = 8f,
                    marginLeft = 3f,
                    marginRight = 3f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    borderTopWidth = 1f,
                    borderTopColor = new Color(0.16f, 0.16f, 0.16f, 1f)
                }
            };
        }

        private static Label CreateTitle(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 4f
                }
            };
        }

        private static VisualElement CreateItem(XUIBase component, string key, bool isDuplicatedKey)
        {
            VisualElement row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 1f,
                    marginBottom = 1f
                }
            };

            Image errorIcon = CreateDuplicateKeyIcon(key, isDuplicatedKey);

            Label keyLabel = new Label(key)
            {
                tooltip = "运行时查找 Key",
                style =
                {
                    width = KeyLabelWidth,
                    minWidth = KeyLabelWidth,
                    maxWidth = KeyLabelWidth,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    overflow = Overflow.Hidden
                }
            };

            ObjectField objectField = new ObjectField
            {
                objectType = typeof(XUIBase),
                value = component,
                allowSceneObjects = true,
                style =
                {
                    flexGrow = 1f
                }
            };
            objectField.SetEnabled(false);

            row.Add(errorIcon);
            row.Add(keyLabel);
            row.Add(objectField);
            return row;
        }

        private static Image CreateDuplicateKeyIcon(string key, bool isDuplicatedKey)
        {
            Image icon = new Image
            {
                image = isDuplicatedKey ? EditorGUIUtility.IconContent("console.erroricon").image : null,
                tooltip = isDuplicatedKey ? $"重复的 UI 查找 Key：{key}" : string.Empty,
                style =
                {
                    width = 16f,
                    minWidth = 16f,
                    maxWidth = 16f,
                    height = 16f,
                    marginRight = 4f,
                    unityBackgroundImageTintColor = Color.red
                }
            };

            return icon;
        }

        private static Dictionary<string, int> CountKeys(List<ComponentFindHelper<XUIBase>.ComponentInfo> components)
        {
            Dictionary<string, int> keyCounts = new Dictionary<string, int>();
            for (int i = 0; i < components.Count; i++)
            {
                string key = components[i].Key;
                if (keyCounts.ContainsKey(key))
                {
                    keyCounts[key]++;
                }
                else
                {
                    keyCounts.Add(key, 1);
                }
            }

            return keyCounts;
        }
    }
}
