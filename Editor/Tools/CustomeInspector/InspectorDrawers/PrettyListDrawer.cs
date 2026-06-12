using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(PrettyListAttribute))]
    public class PrettyListDrawer : PropertyDrawer
    {
        private const float HeaderHeight = 26f;
        private const float RowMinHeight = 22f;
        private const float HandleWidth = 16f;
        private const float FieldLeftPadding = 0f;
        private const float RemoveButtonWidth = 18f;
        private const float AddButtonWidth = 22f;
        private const float BorderWidth = 1f;

        private static readonly Dictionary<string, ListState> States = new Dictionary<string, ListState>();
        private static ClipboardData s_Clipboard;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (!IsSupportedList(property))
            {
                return new HelpBox("[PrettyList] can only be used on List<T> or one-dimensional arrays.", HelpBoxMessageType.Error);
            }

            return new PrettyListElement(property.serializedObject, property.propertyPath);
        }

        private static bool MoveElement(SerializedProperty property, int from, int targetInsertionIndex)
        {
            SerializedProperty current = ResolveProperty(property);
            if (from < 0 || from >= current.arraySize || targetInsertionIndex < 0 || targetInsertionIndex > current.arraySize)
            {
                return false;
            }

            if (targetInsertionIndex == from || targetInsertionIndex == from + 1)
            {
                return false;
            }

            int to = targetInsertionIndex > from ? targetInsertionIndex - 1 : targetInsertionIndex;
            current.MoveArrayElement(from, to);
            current.serializedObject.ApplyModifiedProperties();
            return true;
        }

        private static void AddElement(SerializedProperty property)
        {
            SerializedProperty current = ResolveProperty(property);
            current.serializedObject.Update();
            int index = current.arraySize;
            current.arraySize++;
            ClearPropertyValue(current.GetArrayElementAtIndex(index));
            current.serializedObject.ApplyModifiedProperties();
        }

        private static void InsertDefaultElement(SerializedProperty property, int index)
        {
            SerializedProperty current = ResolveProperty(property);
            current.serializedObject.Update();
            index = Mathf.Clamp(index, 0, current.arraySize);
            current.InsertArrayElementAtIndex(index);
            ClearPropertyValue(current.GetArrayElementAtIndex(index));
            current.serializedObject.ApplyModifiedProperties();
        }

        private static void DuplicateElement(SerializedProperty property, int index)
        {
            SerializedProperty current = ResolveProperty(property);
            current.serializedObject.Update();
            ClipboardData snapshot = ClipboardData.CaptureElement(current.GetArrayElementAtIndex(index));
            current.InsertArrayElementAtIndex(index + 1);
            snapshot.TryApplyTo(current.GetArrayElementAtIndex(index + 1));
            current.serializedObject.ApplyModifiedProperties();
        }

        private static void DeleteElement(SerializedProperty property, int index)
        {
            SerializedProperty current = ResolveProperty(property);
            if (index < 0 || index >= current.arraySize)
            {
                return;
            }

            SerializedProperty element = current.GetArrayElementAtIndex(index);
            bool requiresSecondDelete = element.propertyType == SerializedPropertyType.ObjectReference
                                        && element.objectReferenceValue != null;

            current.serializedObject.Update();
            current.DeleteArrayElementAtIndex(index);
            if (requiresSecondDelete && index < current.arraySize)
            {
                current.DeleteArrayElementAtIndex(index);
            }

            current.serializedObject.ApplyModifiedProperties();
        }

        private static void ClearCollection(SerializedProperty property)
        {
            SerializedProperty current = ResolveProperty(property);
            current.serializedObject.Update();
            current.ClearArray();
            current.serializedObject.ApplyModifiedProperties();
        }

        private static bool TryPasteElement(SerializedProperty property, int index)
        {
            SerializedProperty current = ResolveProperty(property);
            if (s_Clipboard == null || !s_Clipboard.IsElement || index < 0 || index >= current.arraySize)
            {
                return false;
            }

            SerializedProperty element = current.GetArrayElementAtIndex(index);
            if (!s_Clipboard.CanApplyTo(element))
            {
                return false;
            }

            current.serializedObject.Update();
            bool pasted = s_Clipboard.TryApplyTo(element);
            current.serializedObject.ApplyModifiedProperties();
            return pasted;
        }

        private static bool TryPasteCollection(SerializedProperty property)
        {
            SerializedProperty current = ResolveProperty(property);
            if (!CanPasteCollection(current))
            {
                return false;
            }

            current.serializedObject.Update();
            current.arraySize = s_Clipboard.Elements.Count;
            for (int i = 0; i < s_Clipboard.Elements.Count; i++)
            {
                s_Clipboard.Elements[i].TryApplyTo(current.GetArrayElementAtIndex(i));
            }

            current.serializedObject.ApplyModifiedProperties();
            return true;
        }

        private static bool CanPasteElement(SerializedProperty element)
        {
            return s_Clipboard != null && s_Clipboard.IsElement && s_Clipboard.CanApplyTo(element);
        }

        private static bool CanPasteCollection(SerializedProperty property)
        {
            if (s_Clipboard == null || s_Clipboard.IsElement || !IsSupportedList(property))
            {
                return false;
            }

            if (s_Clipboard.CollectionElementType != property.arrayElementType)
            {
                return false;
            }

            if (property.arraySize == 0)
            {
                return true;
            }

            return s_Clipboard.Elements.Count == 0 || s_Clipboard.Elements[0].CanApplyTo(property.GetArrayElementAtIndex(0));
        }

        private static void ClearPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask:
                    property.longValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    property.doubleValue = 0d;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = Color.clear;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = Vector2.zero;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = Vector3.zero;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = Vector4.zero;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = Rect.zero;
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = new Bounds();
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = Quaternion.identity;
                    break;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = Vector2Int.zero;
                    break;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = Vector3Int.zero;
                    break;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = new RectInt();
                    break;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = new BoundsInt();
                    break;
                case SerializedPropertyType.ManagedReference:
                    property.managedReferenceValue = null;
                    break;
                case SerializedPropertyType.Generic:
                    ClearVisibleChildren(property);
                    break;
            }
        }

        private static void ClearVisibleChildren(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                ClearPropertyValue(iterator);
                enterChildren = false;
            }
        }

        private static SerializedProperty ResolveProperty(SerializedProperty property)
        {
            property.serializedObject.Update();
            return property.serializedObject.FindProperty(property.propertyPath);
        }

        private static bool IsSupportedList(SerializedProperty property)
        {
            return property != null && property.isArray && property.propertyType != SerializedPropertyType.String;
        }

        private static string GetCountText(SerializedProperty property)
        {
            return property.arraySize == 0 ? "Empty" : property.arraySize + " items";
        }

        private static ListState GetState(string key)
        {
            if (!States.TryGetValue(key, out ListState state))
            {
                state = new ListState();
                States[key] = state;
            }

            return state;
        }

        private static string GetStateKey(SerializedProperty property)
        {
            int targetId = property.serializedObject.targetObject != null
                ? property.serializedObject.targetObject.GetInstanceID()
                : 0;

            return targetId + ":" + property.propertyPath;
        }

        private static Color GetDarkLineColor()
        {
            return new Color(0.07f, 0.07f, 0.07f, 1f);
        }

        private sealed class PrettyListElement : VisualElement
        {
            private readonly SerializedObject m_SerializedObject;
            private readonly string m_PropertyPath;
            private readonly ListState m_State;
            private readonly List<VisualElement> m_RowElements = new List<VisualElement>();
            private VisualElement m_RowsContainer;
            private VisualElement m_CapturedDragHandle;

            public PrettyListElement(SerializedObject serializedObject, string propertyPath)
            {
                m_SerializedObject = serializedObject;
                m_PropertyPath = propertyPath;
                SerializedProperty property = GetProperty();
                m_State = property == null ? new ListState() : GetState(GetStateKey(property));

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
                m_RowElements.Clear();
                SerializedProperty property = GetProperty();
                if (!IsSupportedList(property))
                {
                    Add(new HelpBox("[PrettyList] can only be used on List<T> or one-dimensional arrays.", HelpBoxMessageType.Error));
                    return;
                }

                Add(CreateHeader(property));
                if (!property.isExpanded)
                {
                    return;
                }

                m_RowsContainer = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column
                    }
                };
                Add(m_RowsContainer);

                if (property.arraySize == 0)
                {
                    m_RowsContainer.Add(CreateEmptyRow());
                    return;
                }

                for (int i = 0; i < property.arraySize; i++)
                {
                    m_RowsContainer.Add(CreateRow(property, i));
                }

                UpdateRowStyles();
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
                header.AddManipulator(new ContextualMenuManipulator(BuildHeaderContextMenu));

                var foldout = new Label(property.isExpanded ? "▾" : "▸")
                {
                    style =
                    {
                        width = 18,
                        minWidth = 18,
                        height = HeaderHeight,
                        minHeight = HeaderHeight,
                        maxHeight = HeaderHeight,
                        marginLeft = 0,
                        marginRight = 0,
                        paddingLeft = 0,
                        paddingRight = 0,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 17,
                        color = new Color(0.62f, 0.62f, 0.62f, 1f)
                    }
                };
                foldout.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    ToggleExpanded();
                    evt.StopPropagation();
                });

                var title = new Label(property.displayName)
                {
                    style =
                    {
                        flexGrow = 1,
                        height = HeaderHeight - 2,
                        minHeight = HeaderHeight - 2,
                        marginTop = 1,
                        marginRight = 4,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12,
                        color = new Color(0.72f, 0.72f, 0.72f, 1f)
                    }
                };
                title.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    ToggleExpanded();
                    evt.StopPropagation();
                });

                var count = new Label(GetCountText(property))
                {
                    style =
                    {
                        width = 68,
                        unityTextAlign = TextAnchor.MiddleRight,
                        color = new Color(0.48f, 0.48f, 0.48f, 1f)
                    }
                };
                count.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    ToggleExpanded();
                    evt.StopPropagation();
                });

                var add = new Label("+")
                {
                    style =
                    {
                        width = AddButtonWidth,
                        height = HeaderHeight,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        fontSize = 18,
                        color = new Color(0.62f, 0.62f, 0.62f, 1f),
                        borderLeftWidth = BorderWidth,
                        borderLeftColor = GetDarkLineColor()
                    }
                };
                add.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    WithProperty(property =>
                    {
                        AddElement(property);
                        m_State.SelectedIndex = ResolveProperty(property).arraySize - 1;
                    });
                    evt.StopPropagation();
                });

                header.Add(foldout);
                header.Add(title);
                header.Add(count);
                header.Add(add);
                return header;
            }

            private VisualElement CreateEmptyRow()
            {
                return new Label("Empty")
                {
                    style =
                    {
                        height = RowMinHeight,
                        minHeight = RowMinHeight,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f),
                        color = new Color(0.45f, 0.45f, 0.45f, 1f)
                    }
                };
            }

            private VisualElement CreateRow(SerializedProperty listProperty, int index)
            {
                SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        minHeight = RowMinHeight
                    }
                };
                m_RowElements.Add(row);
                int rowIndex = index;
                row.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    m_State.SelectedIndex = rowIndex;
                    UpdateRowStyles();
                }, TrickleDown.TrickleDown);
                row.AddManipulator(new ContextualMenuManipulator(evt => BuildElementContextMenu(evt, rowIndex)));

                var handle = new Label("☰")
                {
                    style =
                    {
                        width = HandleWidth,
                        minWidth = HandleWidth,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        color = new Color(0.42f, 0.42f, 0.42f, 1f)
                    }
                };
                handle.RegisterCallback<PointerDownEvent>(evt => BeginDrag(evt, handle, rowIndex));
                handle.RegisterCallback<PointerMoveEvent>(OnDragMove);
                handle.RegisterCallback<PointerUpEvent>(OnDragEnd);

                var field = new PropertyField(element.Copy(), string.Empty)
                {
                    style =
                    {
                        flexGrow = 1,
                        minWidth = 0,
                        marginTop = 2,
                        marginBottom = 2,
                        marginLeft = FieldLeftPadding,
                        marginRight = 4
                    }
                };
                field.Bind(m_SerializedObject);

                var remove = new Button(() =>
                {
                    WithProperty(property =>
                    {
                        DeleteElement(property, rowIndex);
                        m_State.SelectedIndex = Mathf.Clamp(rowIndex - 1, -1, ResolveProperty(property).arraySize - 1);
                    });
                })
                {
                    text = "x",
                    style =
                    {
                        width = RemoveButtonWidth,
                        height = 18,
                        minWidth = RemoveButtonWidth,
                        marginTop = 2,
                        marginBottom = 2,
                        marginRight = 2,
                        paddingLeft = 0,
                        paddingRight = 0,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };

                row.Add(handle);
                row.Add(field);
                row.Add(remove);
                return row;
            }

            private void ToggleExpanded()
            {
                SerializedProperty property = GetProperty();
                if (property == null)
                {
                    return;
                }

                SetExpanded(!property.isExpanded);
            }

            private void SetExpanded(bool expanded)
            {
                WithProperty(property =>
                {
                    property.isExpanded = expanded;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            private void BeginDrag(PointerDownEvent evt, VisualElement handle, int index)
            {
                if (evt.button != 0)
                {
                    return;
                }

                m_State.SelectedIndex = index;
                m_State.DragSourceIndex = index;
                m_State.DropTargetIndex = index;
                m_CapturedDragHandle = handle;
                handle.CapturePointer(evt.pointerId);
                UpdateRowStyles();
                evt.StopPropagation();
            }

            private void OnDragMove(PointerMoveEvent evt)
            {
                if (m_State.DragSourceIndex < 0)
                {
                    return;
                }

                m_State.DropTargetIndex = GetDropTargetIndex(evt.position.y);
                UpdateRowStyles();
                evt.StopPropagation();
            }

            private void OnDragEnd(PointerUpEvent evt)
            {
                if (m_State.DragSourceIndex < 0)
                {
                    return;
                }

                int from = m_State.DragSourceIndex;
                int to = m_State.DropTargetIndex;
                m_State.DragSourceIndex = -1;
                m_State.DropTargetIndex = -1;
                m_CapturedDragHandle?.ReleasePointer(evt.pointerId);
                m_CapturedDragHandle = null;

                WithProperty(property =>
                {
                    if (MoveElement(property, from, to))
                    {
                        m_State.SelectedIndex = Mathf.Clamp(to > from ? to - 1 : to, 0, ResolveProperty(property).arraySize - 1);
                    }
                });
                evt.StopPropagation();
            }

            private int GetDropTargetIndex(float panelY)
            {
                for (int i = 0; i < m_RowElements.Count; i++)
                {
                    if (panelY < m_RowElements[i].worldBound.center.y)
                    {
                        return i;
                    }
                }

                return m_RowElements.Count;
            }

            private void UpdateRowStyles()
            {
                for (int i = 0; i < m_RowElements.Count; i++)
                {
                    VisualElement row = m_RowElements[i];
                    Color background = m_State.SelectedIndex == i
                        ? new Color(0.24f, 0.28f, 0.34f, 1f)
                        : (i % 2 == 0
                            ? new Color(0.16f, 0.16f, 0.16f, 1f)
                            : new Color(0.19f, 0.19f, 0.19f, 1f));
                    row.style.backgroundColor = background;
                    row.style.borderTopWidth = 0;
                    row.style.borderBottomWidth = 0;
                    row.style.borderTopColor = new Color(0.25f, 0.55f, 1f, 1f);
                    row.style.borderBottomColor = new Color(0.25f, 0.55f, 1f, 1f);

                    if (m_State.DragSourceIndex >= 0 && m_State.DropTargetIndex == i)
                    {
                        row.style.borderTopWidth = 2;
                    }
                    else if (m_State.DragSourceIndex >= 0 && m_State.DropTargetIndex == m_RowElements.Count && i == m_RowElements.Count - 1)
                    {
                        row.style.borderBottomWidth = 2;
                    }
                }
            }

            private void BuildHeaderContextMenu(ContextualMenuPopulateEvent evt)
            {
                SerializedProperty property = GetProperty();
                if (!IsSupportedList(property))
                {
                    return;
                }

                evt.menu.AppendAction("Add", _ => WithProperty(AddElement));
                evt.menu.AppendAction(
                    "Clear",
                    _ => WithProperty(property =>
                    {
                        ClearCollection(property);
                        m_State.SelectedIndex = -1;
                    }),
                    _ => property.arraySize > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction(
                    "Copy Collection",
                    _ => s_Clipboard = ClipboardData.CaptureCollection(ResolveProperty(property)),
                    _ => property.arraySize > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction(
                    "Paste Collection",
                    _ => WithProperty(property =>
                    {
                        if (TryPasteCollection(property))
                        {
                            m_State.SelectedIndex = -1;
                        }
                    }),
                    _ => CanPasteCollection(property) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }

            private void BuildElementContextMenu(ContextualMenuPopulateEvent evt, int index)
            {
                SerializedProperty property = GetProperty();
                if (!IsSupportedList(property) || index < 0 || index >= property.arraySize)
                {
                    return;
                }

                SerializedProperty element = property.GetArrayElementAtIndex(index);
                evt.menu.AppendAction("Copy Element", _ =>
                {
                    SerializedProperty current = GetProperty();
                    if (current != null && index < current.arraySize)
                    {
                        s_Clipboard = ClipboardData.CaptureElement(current.GetArrayElementAtIndex(index));
                    }
                });
                evt.menu.AppendAction(
                    "Paste Element",
                    _ => WithProperty(property => TryPasteElement(property, index)),
                    _ => CanPasteElement(element) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                evt.menu.AppendAction("Duplicate Element", _ => WithProperty(property =>
                {
                    DuplicateElement(property, index);
                    m_State.SelectedIndex = index + 1;
                }));
                evt.menu.AppendSeparator(string.Empty);
                evt.menu.AppendAction("Insert Above", _ => WithProperty(property =>
                {
                    InsertDefaultElement(property, index);
                    m_State.SelectedIndex = index;
                }));
                evt.menu.AppendAction("Insert Below", _ => WithProperty(property =>
                {
                    InsertDefaultElement(property, index + 1);
                    m_State.SelectedIndex = index + 1;
                }));
                evt.menu.AppendAction("Delete Element", _ => WithProperty(property =>
                {
                    DeleteElement(property, index);
                    m_State.SelectedIndex = Mathf.Clamp(index - 1, -1, ResolveProperty(property).arraySize - 1);
                }));
            }

            private void WithProperty(Action<SerializedProperty> action)
            {
                SerializedProperty property = GetProperty();
                if (property == null)
                {
                    return;
                }

                action(property);
                Rebuild();
            }
        }

        private sealed class ListState
        {
            public int SelectedIndex = -1;
            public int DragSourceIndex = -1;
            public int DropTargetIndex = -1;
        }

        private sealed class ClipboardData
        {
            public bool IsElement;
            public string CollectionElementType;
            public PropertySnapshot Element;
            public List<PropertySnapshot> Elements;

            public static ClipboardData CaptureElement(SerializedProperty property)
            {
                return new ClipboardData
                {
                    IsElement = true,
                    Element = PropertySnapshot.Capture(property)
                };
            }

            public static ClipboardData CaptureCollection(SerializedProperty property)
            {
                List<PropertySnapshot> elements = new List<PropertySnapshot>();
                for (int i = 0; i < property.arraySize; i++)
                {
                    elements.Add(PropertySnapshot.Capture(property.GetArrayElementAtIndex(i)));
                }

                return new ClipboardData
                {
                    IsElement = false,
                    CollectionElementType = property.arrayElementType,
                    Elements = elements
                };
            }

            public bool CanApplyTo(SerializedProperty property)
            {
                return Element != null && Element.CanApplyTo(property);
            }

            public bool TryApplyTo(SerializedProperty property)
            {
                return Element != null && Element.TryApplyTo(property);
            }
        }

        private sealed class PropertySnapshot
        {
            public SerializedPropertyType PropertyType;
            public string ManagedReferenceFullTypeName;
            public object Value;
            public List<ChildSnapshot> Children;

            public static PropertySnapshot Capture(SerializedProperty property)
            {
                PropertySnapshot snapshot = new PropertySnapshot
                {
                    PropertyType = property.propertyType,
                    ManagedReferenceFullTypeName = property.propertyType == SerializedPropertyType.ManagedReference
                        ? property.managedReferenceFullTypename
                        : null
                };

                if (property.propertyType == SerializedPropertyType.Generic)
                {
                    snapshot.Children = CaptureChildren(property);
                }
                else
                {
                    snapshot.Value = CaptureValue(property);
                }

                return snapshot;
            }

            public bool CanApplyTo(SerializedProperty property)
            {
                return property.propertyType == PropertyType
                       && (PropertyType != SerializedPropertyType.ManagedReference
                           || string.IsNullOrEmpty(ManagedReferenceFullTypeName)
                           || property.managedReferenceFullTypename == ManagedReferenceFullTypeName);
            }

            public bool TryApplyTo(SerializedProperty property)
            {
                if (!CanApplyTo(property))
                {
                    return false;
                }

                if (PropertyType == SerializedPropertyType.Generic)
                {
                    if (Children == null)
                    {
                        return false;
                    }

                    foreach (ChildSnapshot child in Children)
                    {
                        SerializedProperty targetChild = property.FindPropertyRelative(child.RelativePath);
                        if (targetChild == null)
                        {
                            continue;
                        }

                        child.Snapshot.TryApplyTo(targetChild);
                    }

                    return true;
                }

                return ApplyValue(property, Value);
            }

            private static List<ChildSnapshot> CaptureChildren(SerializedProperty property)
            {
                List<ChildSnapshot> children = new List<ChildSnapshot>();
                SerializedProperty iterator = property.Copy();
                SerializedProperty end = iterator.GetEndProperty();
                string prefix = property.propertyPath + ".";
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
                {
                    string relativePath = iterator.propertyPath.StartsWith(prefix, StringComparison.Ordinal)
                        ? iterator.propertyPath.Substring(prefix.Length)
                        : iterator.name;
                    children.Add(new ChildSnapshot(relativePath, Capture(iterator)));
                    enterChildren = false;
                }

                return children;
            }

            private static object CaptureValue(SerializedProperty property)
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.LayerMask:
                        return property.longValue;
                    case SerializedPropertyType.Boolean:
                        return property.boolValue;
                    case SerializedPropertyType.Float:
                        return property.doubleValue;
                    case SerializedPropertyType.String:
                        return property.stringValue;
                    case SerializedPropertyType.Color:
                        return property.colorValue;
                    case SerializedPropertyType.ObjectReference:
                        return property.objectReferenceValue;
                    case SerializedPropertyType.Enum:
                        return property.enumValueIndex;
                    case SerializedPropertyType.Vector2:
                        return property.vector2Value;
                    case SerializedPropertyType.Vector3:
                        return property.vector3Value;
                    case SerializedPropertyType.Vector4:
                        return property.vector4Value;
                    case SerializedPropertyType.Rect:
                        return property.rectValue;
                    case SerializedPropertyType.Bounds:
                        return property.boundsValue;
                    case SerializedPropertyType.Quaternion:
                        return property.quaternionValue;
                    case SerializedPropertyType.Vector2Int:
                        return property.vector2IntValue;
                    case SerializedPropertyType.Vector3Int:
                        return property.vector3IntValue;
                    case SerializedPropertyType.RectInt:
                        return property.rectIntValue;
                    case SerializedPropertyType.BoundsInt:
                        return property.boundsIntValue;
                    case SerializedPropertyType.ManagedReference:
                        return property.managedReferenceValue;
                    case SerializedPropertyType.AnimationCurve:
                        return property.animationCurveValue;
                    case SerializedPropertyType.ExposedReference:
                        return property.exposedReferenceValue;
                    default:
                        return null;
                }
            }

            private static bool ApplyValue(SerializedProperty property, object value)
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.LayerMask:
                        property.longValue = value is long longValue ? longValue : 0;
                        return true;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = value is bool boolValue && boolValue;
                        return true;
                    case SerializedPropertyType.Float:
                        property.doubleValue = value is double doubleValue ? doubleValue : 0d;
                        return true;
                    case SerializedPropertyType.String:
                        property.stringValue = value as string ?? string.Empty;
                        return true;
                    case SerializedPropertyType.Color:
                        property.colorValue = value is Color colorValue ? colorValue : Color.clear;
                        return true;
                    case SerializedPropertyType.ObjectReference:
                        property.objectReferenceValue = value as UnityEngine.Object;
                        return true;
                    case SerializedPropertyType.Enum:
                        property.enumValueIndex = value is int enumValue ? enumValue : 0;
                        return true;
                    case SerializedPropertyType.Vector2:
                        property.vector2Value = value is Vector2 vector2Value ? vector2Value : Vector2.zero;
                        return true;
                    case SerializedPropertyType.Vector3:
                        property.vector3Value = value is Vector3 vector3Value ? vector3Value : Vector3.zero;
                        return true;
                    case SerializedPropertyType.Vector4:
                        property.vector4Value = value is Vector4 vector4Value ? vector4Value : Vector4.zero;
                        return true;
                    case SerializedPropertyType.Rect:
                        property.rectValue = value is Rect rectValue ? rectValue : Rect.zero;
                        return true;
                    case SerializedPropertyType.Bounds:
                        property.boundsValue = value is Bounds boundsValue ? boundsValue : new Bounds();
                        return true;
                    case SerializedPropertyType.Quaternion:
                        property.quaternionValue = value is Quaternion quaternionValue ? quaternionValue : Quaternion.identity;
                        return true;
                    case SerializedPropertyType.Vector2Int:
                        property.vector2IntValue = value is Vector2Int vector2IntValue ? vector2IntValue : Vector2Int.zero;
                        return true;
                    case SerializedPropertyType.Vector3Int:
                        property.vector3IntValue = value is Vector3Int vector3IntValue ? vector3IntValue : Vector3Int.zero;
                        return true;
                    case SerializedPropertyType.RectInt:
                        property.rectIntValue = value is RectInt rectIntValue ? rectIntValue : new RectInt();
                        return true;
                    case SerializedPropertyType.BoundsInt:
                        property.boundsIntValue = value is BoundsInt boundsIntValue ? boundsIntValue : new BoundsInt();
                        return true;
                    case SerializedPropertyType.ManagedReference:
                        property.managedReferenceValue = value;
                        return true;
                    case SerializedPropertyType.AnimationCurve:
                        property.animationCurveValue = value as AnimationCurve;
                        return true;
                    case SerializedPropertyType.ExposedReference:
                        property.exposedReferenceValue = value as UnityEngine.Object;
                        return true;
                    default:
                        return false;
                }
            }
        }

        private readonly struct ChildSnapshot
        {
            public readonly string RelativePath;
            public readonly PropertySnapshot Snapshot;

            public ChildSnapshot(string relativePath, PropertySnapshot snapshot)
            {
                RelativePath = relativePath;
                Snapshot = snapshot;
            }
        }
    }
}
