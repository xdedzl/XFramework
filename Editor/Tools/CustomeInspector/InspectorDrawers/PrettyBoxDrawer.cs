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

        private static bool IsSupportedObject(SerializedProperty property)
        {
            return property != null
                   && property.propertyType == SerializedPropertyType.Generic
                   && !property.isArray;
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
                style.marginTop = -BorderWidth;

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
                        borderBottomWidth = property.isExpanded ? BorderWidth : 0f,
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
