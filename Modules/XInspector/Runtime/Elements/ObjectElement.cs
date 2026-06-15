using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    public class ObjectElement : XInspectorElement, IExpandableElement
    {
        private const float HeaderHeight = 26f;
        private const float BorderWidth = 1f;
        private const float BodyPadding = 4f;
        private const float ChildSpacing = 2f;
        private const float GroupHeaderHeight = 24f;
        private const float GroupBodyPadding = 4f;

        private readonly VisualElement header;
        private readonly VisualElement elementsContent;
        private readonly Label foldoutLabel;
        private bool arrowActive = true;

        public ObjectElement()
        {
            Remove(variableNameText);
            variableNameText.RemoveFromClassList("inspector-label");
            variableNameText.style.flexGrow = 1;
            variableNameText.style.height = HeaderHeight - 2;
            variableNameText.style.minHeight = HeaderHeight - 2;
            variableNameText.style.marginTop = 1;
            variableNameText.style.unityTextAlign = TextAnchor.MiddleLeft;
            variableNameText.style.unityFontStyleAndWeight = FontStyle.Bold;
            variableNameText.style.fontSize = 12;
            variableNameText.style.color = new Color(0.72f, 0.72f, 0.72f, 1f);

            foldoutLabel = new Label
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

            header = new VisualElement
            {
                style =
                {
                    height = HeaderHeight,
                    minHeight = HeaderHeight,
                    maxHeight = HeaderHeight,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = new Color(0.20f, 0.20f, 0.20f, 1f)
                },
                focusable = true,
                tabIndex = 0
            };
            header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            header.Add(foldoutLabel);
            header.Add(variableNameText);

            elementsContent = new VisualElement
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
            elementsContent.RegisterCallback<AttachToPanelEvent>(_ => UpdateHeaderState());
            elementsContent.RegisterCallback<DetachFromPanelEvent>(_ => UpdateHeaderState());

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

            Add(header);
        }

        public void Expand()
        {
            if (elementsContent.parent != this)
            {
                Add(elementsContent);
            }

            UpdateHeaderState();
        }

        public void Collapse()
        {
            if (elementsContent.parent == this)
            {
                Remove(elementsContent);
            }

            UpdateHeaderState();
        }

        public void SetArrowActive(bool value)
        {
            arrowActive = value;
            if (!value)
            {
                if (header.parent == this)
                {
                    Remove(header);
                }

                Expand();
                return;
            }

            if (header.parent != this)
            {
                Insert(0, header);
            }

            UpdateHeaderState();
        }

        public IEnumerable<VisualElement> GetChildElements()
        {
            return elementsContent.Children();
        }

        public override void Refresh()
        {
            ClearElements();
            CreateElements();
            UpdateHeaderState();
        }

        protected override void OnDepthChange(int depth)
        {
            style.marginLeft = XInspector.TabSize * Math.Max(depth, 0);
            variableNameText.style.translate = Vector2.zero;
        }

        private void CreateElements()
        {
            List<MemberEntry> members = new();
            CollectMembers(members, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField | BindingFlags.GetProperty, false);
            CollectMembers(members, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.GetProperty, true);

            Dictionary<string, PrettyGroupElement> groups = new();
            for (int i = 0; i < members.Count; i++)
            {
                MemberEntry entry = members[i];
                XInspectorElement element = CreateElementForEntry(entry);
                if (element == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.GroupTitle))
                {
                    if (!groups.TryGetValue(entry.GroupTitle, out PrettyGroupElement groupElement))
                    {
                        groupElement = new PrettyGroupElement(entry.GroupTitle);
                        groups[entry.GroupTitle] = groupElement;
                        elementsContent.Add(groupElement);
                    }

                    groupElement.AddChild(element);
                    continue;
                }

                element.style.marginBottom = ChildSpacing;
                elementsContent.Add(element);
            }
        }

        private void ClearElements()
        {
            elementsContent.Clear();
        }

        private void CollectMembers(List<MemberEntry> entries, BindingFlags bindingFlags, bool onlyElementProperty)
        {
            foreach (MemberInfo member in GetMembers(BoundVariableType, bindingFlags))
            {
                if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                {
                    continue;
                }

                if (IsHiddenInInspector(member))
                {
                    continue;
                }

                ElementPropertyAttribute nameAttribute = member.GetCustomAttribute<ElementPropertyAttribute>();
                if (onlyElementProperty && nameAttribute == null)
                {
                    continue;
                }

                string propertyName = nameAttribute != null && !string.IsNullOrEmpty(nameAttribute.propertyName)
                    ? nameAttribute.propertyName
                    : member.Name;
                PrettyGroupAttribute groupAttribute = member.GetCustomAttribute<PrettyGroupAttribute>(true);
                entries.Add(new MemberEntry(member, propertyName, groupAttribute?.Title));
            }
        }

        private XInspectorElement CreateElementForEntry(MemberEntry entry)
        {
            XInspectorElement element = CreateItemForMember(entry.Member, Depth + 1);
            if (element == null)
            {
                return null;
            }

            element.BindTo(this, entry.Member, entry.PropertyName);
            element.Refresh();
            return element;
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (!arrowActive || evt.button != 0)
            {
                return;
            }

            ToggleExpanded();
            evt.StopPropagation();
        }

        private void ToggleExpanded()
        {
            if (elementsContent.parent == this)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        private void UpdateHeaderState()
        {
            bool isExpanded = elementsContent.parent == this;
            foldoutLabel.text = isExpanded ? "▾" : "▸";
            header.style.borderBottomWidth = isExpanded ? BorderWidth : 0f;
            header.style.borderBottomColor = GetDarkLineColor();
        }

        private static Color GetDarkLineColor()
        {
            return new Color(0.07f, 0.07f, 0.07f, 1f);
        }

        /// <summary>
        /// 通过成员变量信息获取UIItem
        /// </summary>
        private XInspectorElement CreateItemForMember(MemberInfo member, int depth)
        {
            if (IsHiddenInInspector(member))
            {
                return null;
            }

            Type variableType = member is FieldInfo fieldInfo ? fieldInfo.FieldType : ((PropertyInfo)member).PropertyType;
            PropertyAttribute propertyAttribute = XInspector.GetPropertyAttribute(member);
            if (propertyAttribute != null && IsArrayPropertyTarget(variableType))
            {
                return XInspector.CreateArrayPropertyElement(propertyAttribute, depth);
            }

            Type propertyDrawerType = XInspector.GetDrawerForPropertyAttribute(propertyAttribute?.GetType());
            if (propertyDrawerType != null)
            {
                return XInspector.CreateDrawerForType(propertyDrawerType, depth);
            }

            return XInspector.CreateDrawerForMemberType(variableType, depth);
        }

        private static bool IsArrayPropertyTarget(Type type)
        {
            return (type.IsArray && type.GetArrayRank() == 1)
                   || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
        }

        private static bool IsHiddenInInspector(MemberInfo member)
        {
            if (Attribute.IsDefined(member, typeof(HideInInspector), true))
            {
                return true;
            }

            if (member is PropertyInfo property)
            {
                FieldInfo backingField = property.DeclaringType?.GetField(
                    $"<{property.Name}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                return backingField != null
                       && Attribute.IsDefined(backingField, typeof(HideInInspector), true);
            }

            return false;
        }

        /// <summary>
        /// 对获得FieldInfos排序
        /// </summary>
        private List<MemberInfo> GetMembers(Type type, BindingFlags bindingFlags)
        {
            List<MemberInfo> result = new List<MemberInfo>();

            do
            {
                var temp = new List<MemberInfo>(type.GetMembers(bindingFlags | BindingFlags.DeclaredOnly));
                temp.AddRange(result);
                result = temp;
                type = type.BaseType;
            }
            while (type != typeof(System.Object) && type != typeof(System.ValueType));

            var aa = new List<MemberInfo>();

            foreach (var item in result)
            {
                if(item is PropertyInfo property)
                {
                    if (property.CanWrite && property.CanRead)
                        aa.Add(item);
                }
                else if(item is FieldInfo field)
                {
                    if (field.IsInitOnly || field.IsLiteral)
                        continue;
                    aa.Add(item);
                }
            }

            return aa;
        }

        private readonly struct MemberEntry
        {
            public readonly MemberInfo Member;
            public readonly string PropertyName;
            public readonly string GroupTitle;

            public MemberEntry(MemberInfo member, string propertyName, string groupTitle)
            {
                Member = member;
                PropertyName = propertyName;
                GroupTitle = groupTitle;
            }
        }

        private sealed class PrettyGroupElement : VisualElement, IExpandableElement
        {
            private readonly string title;
            private readonly VisualElement header;
            private readonly VisualElement content;
            private readonly Label foldoutLabel;

            public PrettyGroupElement(string title)
            {
                this.title = title;
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
                style.marginBottom = 2f;

                foldoutLabel = new Label("▾")
                {
                    style =
                    {
                        width = 18,
                        minWidth = 18,
                        height = GroupHeaderHeight,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 17,
                        color = new Color(0.62f, 0.62f, 0.62f, 1f)
                    }
                };

                Label titleLabel = new Label(title)
                {
                    style =
                    {
                        flexGrow = 1,
                        height = GroupHeaderHeight - 2,
                        minHeight = GroupHeaderHeight - 2,
                        marginTop = 1,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        color = new Color(0.72f, 0.72f, 0.72f, 1f)
                    }
                };

                header = new VisualElement
                {
                    style =
                    {
                        height = GroupHeaderHeight,
                        minHeight = GroupHeaderHeight,
                        maxHeight = GroupHeaderHeight,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        backgroundColor = new Color(0.20f, 0.20f, 0.20f, 1f),
                        borderBottomWidth = BorderWidth,
                        borderBottomColor = GetDarkLineColor()
                    }
                };
                header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
                header.Add(foldoutLabel);
                header.Add(titleLabel);

                content = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        paddingLeft = GroupBodyPadding,
                        paddingRight = GroupBodyPadding,
                        paddingTop = GroupBodyPadding,
                        paddingBottom = GroupBodyPadding
                    }
                };

                Add(header);
                Add(content);
            }

            public void AddChild(XInspectorElement element)
            {
                element.style.marginBottom = ChildSpacing;
                content.Add(element);
            }

            public void Expand()
            {
                if (content.parent != this)
                {
                    Add(content);
                }

                UpdateHeaderState();
            }

            public void Collapse()
            {
                if (content.parent == this)
                {
                    Remove(content);
                }

                UpdateHeaderState();
            }

            public IEnumerable<VisualElement> GetChildElements()
            {
                return content.Children();
            }

            private void OnHeaderPointerDown(PointerDownEvent evt)
            {
                if (evt.button != 0)
                {
                    return;
                }

                if (content.parent == this)
                {
                    Collapse();
                }
                else
                {
                    Expand();
                }

                evt.StopPropagation();
            }

            private void UpdateHeaderState()
            {
                bool isExpanded = content.parent == this;
                foldoutLabel.text = isExpanded ? "▾" : "▸";
                header.style.borderBottomWidth = isExpanded ? BorderWidth : 0f;
                header.style.borderBottomColor = GetDarkLineColor();
                tooltip = title;
            }
        }
    }
}
