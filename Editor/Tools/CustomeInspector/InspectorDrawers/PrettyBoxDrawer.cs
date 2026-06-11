using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(PrettyBoxAttribute))]
    public class PrettyBoxDrawer : PropertyDrawer
    {
        private const float HeaderHeight = 26f;
        private const float BorderWidth = 1f;
        private const float BodyPadding = 4f;
        private const float ChildSpacing = 2f;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (!IsSupportedObject(property))
            {
                return new HelpBox("[PrettyBox] can only be used on serializable class or struct fields.", HelpBoxMessageType.Error);
            }

            return new PrettyBoxElement(property.serializedObject, property.propertyPath);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!IsSupportedObject(property))
            {
                EditorGUI.HelpBox(position, "[PrettyBox] can only be used on serializable class or struct fields.", MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            DrawBoxFrame(position);

            Rect contentRect = new Rect(
                position.x + BorderWidth,
                position.y + BorderWidth,
                position.width - BorderWidth * 2f,
                position.height - BorderWidth * 2f);
            Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, HeaderHeight);
            DrawHeader(headerRect, property, label);

            if (property.isExpanded)
            {
                float y = headerRect.yMax + BodyPadding;
                ForEachDirectChild(property, child =>
                {
                    float childHeight = EditorGUI.GetPropertyHeight(child, true);
                    Rect childRect = new Rect(contentRect.x + BodyPadding, y, contentRect.width - BodyPadding * 2f, childHeight);
                    EditorGUI.PropertyField(childRect, child, true);
                    y += childHeight + ChildSpacing;
                });
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!IsSupportedObject(property))
            {
                return EditorGUIUtility.singleLineHeight * 2f;
            }

            float height = BorderWidth * 2f + HeaderHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            float childrenHeight = 0f;
            int childCount = 0;
            ForEachDirectChild(property, child =>
            {
                childrenHeight += EditorGUI.GetPropertyHeight(child, true);
                childCount++;
            });

            if (childCount > 0)
            {
                childrenHeight += ChildSpacing * (childCount - 1);
            }

            return height + BodyPadding * 2f + childrenHeight;
        }

        private static bool IsSupportedObject(SerializedProperty property)
        {
            return property != null
                   && property.propertyType == SerializedPropertyType.Generic
                   && !property.isArray;
        }

        private static void DrawBoxFrame(Rect rect)
        {
            EditorGUI.DrawRect(rect, GetDarkLineColor());
        }

        private static void DrawHeader(Rect rect, SerializedProperty property, GUIContent label)
        {
            UnityEngine.Event evt = UnityEngine.Event.current;
            EditorGUI.DrawRect(rect, new Color(0.20f, 0.20f, 0.20f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.28f, 0.28f, 0.28f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), GetDarkLineColor());

            Rect foldoutRect = new Rect(rect.x + 2f, rect.y + 1f, 16f, rect.height - 2f);
            Rect labelRect = new Rect(foldoutRect.xMax, rect.y + 1f, rect.width - foldoutRect.width - 4f, rect.height - 2f);

            bool expanded = GUI.Toggle(foldoutRect, property.isExpanded, GUIContent.none, EditorStyles.foldout);
            if (expanded != property.isExpanded)
            {
                property.isExpanded = expanded;
                property.serializedObject.ApplyModifiedProperties();
                evt.Use();
            }

            GUI.Label(labelRect, label, GetHeaderLabelStyle());
            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                property.isExpanded = !property.isExpanded;
                property.serializedObject.ApplyModifiedProperties();
                evt.Use();
            }
        }

        private static void ForEachDirectChild(SerializedProperty property, System.Action<SerializedProperty> action)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            int childDepth = property.depth + 1;
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != childDepth)
                {
                    continue;
                }

                action(iterator.Copy());
            }
        }

        private static GUIStyle GetHeaderLabelStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.alignment = TextAnchor.MiddleLeft;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 12;
            style.normal.textColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            return style;
        }

        private static Color GetDarkLineColor()
        {
            return new Color(0.07f, 0.07f, 0.07f, 1f);
        }

        private sealed class PrettyBoxElement : VisualElement
        {
            private readonly SerializedObject m_SerializedObject;
            private readonly string m_PropertyPath;
            private Label m_Foldout;

            public PrettyBoxElement(SerializedObject serializedObject, string propertyPath)
            {
                m_SerializedObject = serializedObject;
                m_PropertyPath = propertyPath;

                style.flexDirection = FlexDirection.Column;
                style.borderLeftWidth = BorderWidth;
                style.borderRightWidth = BorderWidth;
                style.borderTopWidth = BorderWidth;
                style.borderBottomWidth = BorderWidth;
                style.borderLeftColor = GetDarkLineColor();
                style.borderRightColor = GetDarkLineColor();
                style.borderTopColor = GetDarkLineColor();
                style.borderBottomColor = GetDarkLineColor();

                Rebuild();
            }

            private SerializedProperty GetProperty()
            {
                m_SerializedObject.Update();
                return m_SerializedObject.FindProperty(m_PropertyPath);
            }

            private void Rebuild()
            {
                Clear();
                SerializedProperty property = GetProperty();
                if (!IsSupportedObject(property))
                {
                    Add(new HelpBox("[PrettyBox] can only be used on serializable class or struct fields.", HelpBoxMessageType.Error));
                    return;
                }

                Add(CreateHeader(property));
                if (!property.isExpanded)
                {
                    return;
                }

                VisualElement children = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        paddingLeft = BodyPadding,
                        paddingRight = BodyPadding,
                        paddingTop = BodyPadding,
                        paddingBottom = BodyPadding
                    }
                };

                ForEachDirectChild(property, child =>
                {
                    var field = new PropertyField(child.Copy())
                    {
                        style =
                        {
                            marginBottom = ChildSpacing
                        }
                    };
                    field.Bind(m_SerializedObject);
                    children.Add(field);
                });

                Add(children);
            }

            private VisualElement CreateHeader(SerializedProperty property)
            {
                var header = new VisualElement
                {
                    style =
                    {
                        height = HeaderHeight,
                        minHeight = HeaderHeight,
                        maxHeight = HeaderHeight,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        backgroundColor = new Color(0.20f, 0.20f, 0.20f, 1f),
                        borderBottomWidth = BorderWidth,
                        borderBottomColor = GetDarkLineColor()
                    }
                };

                m_Foldout = new Label(property.isExpanded ? "▾" : "▸")
                {
                    style =
                    {
                        width = 18,
                        minWidth = 18,
                        height = HeaderHeight,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 17,
                        color = new Color(0.62f, 0.62f, 0.62f, 1f)
                    }
                };

                var title = new Label(property.displayName)
                {
                    style =
                    {
                        flexGrow = 1,
                        height = HeaderHeight - 2,
                        minHeight = HeaderHeight - 2,
                        marginTop = 1,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        color = new Color(0.72f, 0.72f, 0.72f, 1f)
                    }
                };

                header.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    ToggleExpanded();
                    evt.StopPropagation();
                });

                header.Add(m_Foldout);
                header.Add(title);
                return header;
            }

            private void ToggleExpanded()
            {
                SerializedProperty property = GetProperty();
                if (property == null)
                {
                    return;
                }

                property.isExpanded = !property.isExpanded;
                property.serializedObject.ApplyModifiedProperties();
                if (m_Foldout != null)
                {
                    m_Foldout.text = property.isExpanded ? "▾" : "▸";
                }

                Rebuild();
            }
        }
    }
}
