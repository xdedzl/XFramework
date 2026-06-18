using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    [SupportHelper(typeof(ArraySupport))]
    public class ArrayElement : XInspectorElement, IExpandableElement
    {
        private const float HeaderHeight = 26f;
        private const float RowMinHeight = 22f;
        private const float HandleWidth = 16f;
        private const float FieldLeftPadding = 0f;
        private const float RemoveButtonWidth = 18f;
        private const float AddButtonWidth = 22f;
        private const float BorderWidth = 1f;

        private static ClipboardData s_Clipboard;

        private Type elementType;
        private bool IsArray => BoundVariableType.IsArray;

        private readonly PropertyAttribute itemPropertyAttribute;
        private readonly VisualElement title;
        private readonly VisualElement elementsContent;
        private readonly Label foldoutLabel;
        private readonly Label countLabel;
        private readonly Label addLabel;
        private readonly List<VisualElement> rowElements = new();
        private readonly List<XInspectorElement> elementDrawers = new();
        private VisualElement capturedDragHandle;
        private int selectedIndex = -1;
        private int dragSourceIndex = -1;
        private int dropTargetIndex = -1;

        private int Length
        {
            get
            {
                if (Value == null)
                    return 0;
                if (Value is Array array)
                    return array.Length;
                if (Value is IList list)
                    return list.Count;

                throw new Exception("类型错误");
            }
        }

        public ArrayElement()
        {
            Remove(variableNameText);
            variableNameText.RemoveFromClassList("inspector-label");
            variableNameText.style.flexGrow = 1;
            variableNameText.style.height = HeaderHeight - 2;
            variableNameText.style.minHeight = HeaderHeight - 2;
            variableNameText.style.marginTop = 1;
            variableNameText.style.marginRight = 4;
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
            foldoutLabel.RegisterCallback<PointerDownEvent>(OnTogglePointerDown);

            countLabel = new Label
            {
                style =
                {
                    width = 68,
                    unityTextAlign = TextAnchor.MiddleRight,
                    color = new Color(0.48f, 0.48f, 0.48f, 1f)
                }
            };
            countLabel.RegisterCallback<PointerDownEvent>(OnTogglePointerDown);

            addLabel = new Label("+")
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
            addLabel.RegisterCallback<PointerDownEvent>(OnAddPointerDown);

            title = new VisualElement
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
            title.AddManipulator(new ContextualMenuManipulator(BuildHeaderContextMenu));
            variableNameText.RegisterCallback<PointerDownEvent>(OnTogglePointerDown);
            title.Add(foldoutLabel);
            title.Add(variableNameText);
            title.Add(countLabel);
            title.Add(addLabel);

            elementsContent = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column
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

            AddToClassList("array-element");
            Add(title);
        }

        public ArrayElement(PropertyAttribute itemPropertyAttribute) : this()
        {
            this.itemPropertyAttribute = itemPropertyAttribute;
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

        public IEnumerable<VisualElement> GetChildElements()
        {
            return elementDrawers;
        }

        public override void Refresh()
        {
            ClearElements();
            CreateElements();
            UpdateHeaderState();
        }

        protected override void OnBound()
        {
            base.OnBound();
            elementType = IsArray ? BoundVariableType.GetElementType() : BoundVariableType.GetGenericArguments()[0];
            UpdateHeaderState();
        }

        protected override void OnDepthChange(int depth)
        {
            style.marginLeft = XInspector.TabSize * Math.Max(depth, 0);
            variableNameText.style.translate = Vector2.zero;
        }

        private void CreateElements()
        {
            rowElements.Clear();
            elementDrawers.Clear();
            if (selectedIndex >= Length)
            {
                selectedIndex = Length - 1;
            }

            if (Value == null || Length <= 0)
            {
                selectedIndex = -1;
                elementsContent.Add(CreateEmptyRow());
                return;
            }

            for (int i = 0; i < Length; i++)
            {
                VisualElement row = CreateRow(i);
                if (row != null)
                {
                    elementsContent.Add(row);
                }
            }

            UpdateRowStyles();
        }

        private void ClearElements()
        {
            elementsContent.Clear();
            rowElements.Clear();
            elementDrawers.Clear();
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

        private VisualElement CreateRow(int index)
        {
            XInspectorElement elementDrawer = CreateDrawerForMemberType();
            if (elementDrawer == null)
            {
                return null;
            }

            int rowIndex = index;
            elementDrawer.BindTo(elementType, "Element " + index, () => GetElementValue(rowIndex), value => SetElementValue(rowIndex, value));
            elementDrawer.SetVariableNameTextRowHeight(RowMinHeight - 4f);
            elementDrawer.Refresh();
            elementDrawer.style.flexGrow = 1;
            elementDrawer.style.minWidth = 0;
            elementDrawers.Add(elementDrawer);

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    minHeight = RowMinHeight
                }
            };
            rowElements.Add(row);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                selectedIndex = rowIndex;
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

            var field = new VisualElement
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
            field.Add(elementDrawer);

            var remove = new Button(() =>
            {
                DeleteElement(rowIndex);
                selectedIndex = ClampSelection(rowIndex - 1);
                Refresh();
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

        private XInspectorElement CreateDrawerForMemberType()
        {
            if (itemPropertyAttribute != null)
            {
                Type drawerType = XInspector.GetDrawerForPropertyAttribute(itemPropertyAttribute.GetType());
                if (drawerType != null)
                {
                    XInspectorElement element = XInspector.CreateDrawerForType(drawerType, 0);
                    if (element is IPropertyAttributeElement propertyAttributeElement)
                    {
                        propertyAttributeElement.SetPropertyAttribute(itemPropertyAttribute);
                    }

                    return element;
                }
            }

            return XInspector.CreateDrawerForMemberType(elementType, 0);
        }

        private void OnTogglePointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            ToggleExpanded();
            evt.StopPropagation();
        }

        private void OnAddPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            AddElement();
            selectedIndex = Length - 1;
            Refresh();
            evt.StopPropagation();
        }

        private void ToggleExpanded()
        {
            if (IsExpanded())
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        private bool IsExpanded()
        {
            return elementsContent.parent == this;
        }

        private void UpdateHeaderState()
        {
            bool isExpanded = IsExpanded();
            foldoutLabel.text = isExpanded ? "▾" : "▸";
            countLabel.text = GetCountText();
            title.style.borderBottomWidth = isExpanded ? BorderWidth : 0f;
            title.style.borderBottomColor = GetDarkLineColor();
        }

        private string GetCountText()
        {
            return Length == 0 ? "Empty" : Length + " items";
        }

        private static Color GetDarkLineColor()
        {
            return new Color(0.07f, 0.07f, 0.07f, 1f);
        }

        private object GetElementValue(int index)
        {
            if (Value is Array array)
            {
                return array.GetValue(index);
            }

            return ((IList)Value)[index];
        }

        private void SetElementValue(int index, object value)
        {
            if (IsArray)
            {
                Array array = (Array)Value;
                array.SetValue(value, index);
                Value = array;
                return;
            }

            IList list = (IList)Value;
            list[index] = value;
            Value = list;
        }

        private void AddElement()
        {
            InsertDefaultElement(Length);
        }

        private void InsertDefaultElement(int index)
        {
            InsertElement(index, CreateDefaultElementValue());
        }

        private void InsertElement(int index, object elementValue)
        {
            int length = Length;
            index = Mathf.Clamp(index, 0, length);

            if (IsArray)
            {
                Array source = Value as Array;
                Array target = Array.CreateInstance(elementType, length + 1);
                if (source != null && index > 0)
                {
                    Array.ConstrainedCopy(source, 0, target, 0, index);
                }

                target.SetValue(elementValue, index);
                if (source != null && index < length)
                {
                    Array.ConstrainedCopy(source, index, target, index + 1, length - index);
                }

                Value = target;
                return;
            }

            IList list = Value as IList ?? CreateListInstance();
            list.Insert(index, elementValue);
            Value = list;
        }

        private void DuplicateElement(int index)
        {
            if (index < 0 || index >= Length)
            {
                return;
            }

            InsertElement(index + 1, CloneElementValue(GetElementValue(index)));
        }

        private void DeleteElement(int index)
        {
            if (index < 0 || index >= Length)
            {
                return;
            }

            if (IsArray)
            {
                Array source = Value as Array;
                int length = Length;
                Array target = Array.CreateInstance(elementType, length - 1);
                if (source != null && index > 0)
                {
                    Array.ConstrainedCopy(source, 0, target, 0, index);
                }

                if (source != null && index < length - 1)
                {
                    Array.ConstrainedCopy(source, index + 1, target, index, length - index - 1);
                }

                Value = target;
                return;
            }

            IList list = (IList)Value;
            list.RemoveAt(index);
            Value = list;
        }

        private void ClearCollection()
        {
            if (IsArray)
            {
                Value = Array.CreateInstance(elementType, 0);
                return;
            }

            IList list = Value as IList ?? CreateListInstance();
            list.Clear();
            Value = list;
        }

        private bool MoveElement(int from, int targetInsertionIndex)
        {
            if (from < 0 || from >= Length || targetInsertionIndex < 0 || targetInsertionIndex > Length)
            {
                return false;
            }

            if (targetInsertionIndex == from || targetInsertionIndex == from + 1)
            {
                return false;
            }

            int to = targetInsertionIndex > from ? targetInsertionIndex - 1 : targetInsertionIndex;
            if (IsArray)
            {
                List<object> values = new();
                for (int i = 0; i < Length; i++)
                {
                    values.Add(GetElementValue(i));
                }

                object movingValue = values[from];
                values.RemoveAt(from);
                values.Insert(to, movingValue);

                Array target = Array.CreateInstance(elementType, values.Count);
                for (int i = 0; i < values.Count; i++)
                {
                    target.SetValue(values[i], i);
                }

                Value = target;
                return true;
            }

            IList list = (IList)Value;
            object value = list[from];
            list.RemoveAt(from);
            list.Insert(to, value);
            Value = list;
            return true;
        }

        private object CreateDefaultElementValue()
        {
            if (elementType == typeof(string))
            {
                return string.Empty;
            }

            return elementType != null && elementType.IsValueType
                ? Activator.CreateInstance(elementType)
                : null;
        }

        private IList CreateListInstance()
        {
            return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
        }

        private int ClampSelection(int value)
        {
            if (Length <= 0)
            {
                return -1;
            }

            return Mathf.Clamp(value, 0, Length - 1);
        }

        private static object CloneElementValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            Type valueType = value.GetType();
            if (valueType.IsValueType
                || valueType == typeof(string)
                || typeof(UnityEngine.Object).IsAssignableFrom(valueType))
            {
                return value;
            }

            if (value is Array sourceArray)
            {
                Type arrayElementType = valueType.GetElementType();
                Array targetArray = Array.CreateInstance(arrayElementType, sourceArray.Length);
                for (int i = 0; i < sourceArray.Length; i++)
                {
                    targetArray.SetValue(CloneElementValue(sourceArray.GetValue(i)), i);
                }

                return targetArray;
            }

            if (value is IList sourceList && valueType.IsGenericType)
            {
                if (valueType.GetConstructor(Type.EmptyTypes) == null)
                {
                    return value;
                }

                IList targetList = (IList)Activator.CreateInstance(valueType);
                foreach (object item in sourceList)
                {
                    targetList.Add(CloneElementValue(item));
                }

                return targetList;
            }

            if (valueType.GetConstructor(Type.EmptyTypes) == null)
            {
                return value;
            }

            object clone = Activator.CreateInstance(valueType);
            foreach (FieldInfo field in valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsInitOnly || field.IsLiteral)
                {
                    continue;
                }

                field.SetValue(clone, CloneElementValue(field.GetValue(value)));
            }

            foreach (PropertyInfo property in valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                try
                {
                    property.SetValue(clone, CloneElementValue(property.GetValue(value)));
                }
                catch
                {
                    // Ignore properties whose accessors are not safe for editor-side snapshots.
                }
            }

            return clone;
        }

        private void BeginDrag(PointerDownEvent evt, VisualElement handle, int index)
        {
            if (evt.button != 0)
            {
                return;
            }

            selectedIndex = index;
            dragSourceIndex = index;
            dropTargetIndex = index;
            capturedDragHandle = handle;
            handle.CapturePointer(evt.pointerId);
            UpdateRowStyles();
            evt.StopPropagation();
        }

        private void OnDragMove(PointerMoveEvent evt)
        {
            if (dragSourceIndex < 0)
            {
                return;
            }

            dropTargetIndex = GetDropTargetIndex(evt.position.y);
            UpdateRowStyles();
            evt.StopPropagation();
        }

        private void OnDragEnd(PointerUpEvent evt)
        {
            if (dragSourceIndex < 0)
            {
                return;
            }

            int from = dragSourceIndex;
            int to = dropTargetIndex;
            dragSourceIndex = -1;
            dropTargetIndex = -1;
            capturedDragHandle?.ReleasePointer(evt.pointerId);
            capturedDragHandle = null;

            if (MoveElement(from, to))
            {
                selectedIndex = ClampSelection(to > from ? to - 1 : to);
            }

            Refresh();
            evt.StopPropagation();
        }

        private int GetDropTargetIndex(float panelY)
        {
            for (int i = 0; i < rowElements.Count; i++)
            {
                if (panelY < rowElements[i].worldBound.center.y)
                {
                    return i;
                }
            }

            return rowElements.Count;
        }

        private void UpdateRowStyles()
        {
            for (int i = 0; i < rowElements.Count; i++)
            {
                VisualElement row = rowElements[i];
                Color background = selectedIndex == i
                    ? new Color(0.24f, 0.28f, 0.34f, 1f)
                    : (i % 2 == 0
                        ? new Color(0.16f, 0.16f, 0.16f, 1f)
                        : new Color(0.19f, 0.19f, 0.19f, 1f));
                row.style.backgroundColor = background;
                row.style.borderTopWidth = 0;
                row.style.borderBottomWidth = 0;
                row.style.borderTopColor = new Color(0.25f, 0.55f, 1f, 1f);
                row.style.borderBottomColor = new Color(0.25f, 0.55f, 1f, 1f);

                if (dragSourceIndex >= 0 && dropTargetIndex == i)
                {
                    row.style.borderTopWidth = 2;
                }
                else if (dragSourceIndex >= 0 && dropTargetIndex == rowElements.Count && i == rowElements.Count - 1)
                {
                    row.style.borderBottomWidth = 2;
                }
            }
        }

        private void BuildHeaderContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Add", _ =>
            {
                AddElement();
                selectedIndex = Length - 1;
                Refresh();
            });
            evt.menu.AppendAction(
                "Clear",
                _ =>
                {
                    ClearCollection();
                    selectedIndex = -1;
                    Refresh();
                },
                _ => Length > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction(
                "Copy Collection",
                _ => s_Clipboard = ClipboardData.CaptureCollection(this),
                _ => Length > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction(
                "Paste Collection",
                _ =>
                {
                    if (TryPasteCollection())
                    {
                        selectedIndex = -1;
                        Refresh();
                    }
                },
                _ => CanPasteCollection() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void BuildElementContextMenu(ContextualMenuPopulateEvent evt, int index)
        {
            if (index < 0 || index >= Length)
            {
                return;
            }

            evt.menu.AppendAction("Copy Element", _ => s_Clipboard = ClipboardData.CaptureElement(this, index));
            evt.menu.AppendAction(
                "Paste Element",
                _ =>
                {
                    if (TryPasteElement(index))
                    {
                        Refresh();
                    }
                },
                _ => CanPasteElement() ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Duplicate Element", _ =>
            {
                DuplicateElement(index);
                selectedIndex = index + 1;
                Refresh();
            });
            evt.menu.AppendSeparator(string.Empty);
            evt.menu.AppendAction("Insert Above", _ =>
            {
                InsertDefaultElement(index);
                selectedIndex = index;
                Refresh();
            });
            evt.menu.AppendAction("Insert Below", _ =>
            {
                InsertDefaultElement(index + 1);
                selectedIndex = index + 1;
                Refresh();
            });
            evt.menu.AppendAction("Delete Element", _ =>
            {
                DeleteElement(index);
                selectedIndex = ClampSelection(index - 1);
                Refresh();
            });
        }

        private bool CanPasteElement()
        {
            return s_Clipboard != null
                   && s_Clipboard.IsElement
                   && IsCompatibleClipboardType(s_Clipboard.CollectionElementType);
        }

        private bool TryPasteElement(int index)
        {
            if (!CanPasteElement() || index < 0 || index >= Length)
            {
                return false;
            }

            SetElementValue(index, CloneElementValue(s_Clipboard.Element));
            return true;
        }

        private bool CanPasteCollection()
        {
            return s_Clipboard != null
                   && !s_Clipboard.IsElement
                   && IsCompatibleClipboardType(s_Clipboard.CollectionElementType);
        }

        private bool TryPasteCollection()
        {
            if (!CanPasteCollection())
            {
                return false;
            }

            if (IsArray)
            {
                Array target = Array.CreateInstance(elementType, s_Clipboard.Elements.Count);
                for (int i = 0; i < s_Clipboard.Elements.Count; i++)
                {
                    target.SetValue(CloneElementValue(s_Clipboard.Elements[i]), i);
                }

                Value = target;
                return true;
            }

            IList list = CreateListInstance();
            for (int i = 0; i < s_Clipboard.Elements.Count; i++)
            {
                list.Add(CloneElementValue(s_Clipboard.Elements[i]));
            }

            Value = list;
            return true;
        }

        private bool IsCompatibleClipboardType(Type clipboardElementType)
        {
            return clipboardElementType == elementType
                   || clipboardElementType != null && elementType.IsAssignableFrom(clipboardElementType);
        }

        private sealed class ClipboardData
        {
            public bool IsElement;
            public Type CollectionElementType;
            public object Element;
            public List<object> Elements;

            public static ClipboardData CaptureElement(ArrayElement owner, int index)
            {
                return new ClipboardData
                {
                    IsElement = true,
                    CollectionElementType = owner.elementType,
                    Element = CloneElementValue(owner.GetElementValue(index))
                };
            }

            public static ClipboardData CaptureCollection(ArrayElement owner)
            {
                List<object> elements = new();
                for (int i = 0; i < owner.Length; i++)
                {
                    elements.Add(CloneElementValue(owner.GetElementValue(i)));
                }

                return new ClipboardData
                {
                    IsElement = false,
                    CollectionElementType = owner.elementType,
                    Elements = elements
                };
            }
        }

        private struct ArraySupport : ISupport
        {
            public bool Support(Type type)
            {
                return (type.IsArray && type.GetArrayRank() == 1) ||
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
            }
        }
    }
}
