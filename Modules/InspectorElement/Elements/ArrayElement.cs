using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    [SupportHelper(typeof(ArraySupport))]
    public class ArrayElement : ExpandableElement
    {
        private Type elementType;
        private bool IsArray => BoundVariableType.IsArray;

        private readonly CustomerElementAttribute customerAttribute;

        private readonly TextField sizeInput;
        private readonly VisualElement listBox;
        private readonly VisualElement listContainer;
        private readonly VisualElement footerContainer;
        private readonly VisualElement buttonBox;
        private readonly Button addButton;
        private readonly Button removeButton;
        private VisualElement selectedRow;
        private int selectedIndex = -1;
        
        private int Length
        {
            get
            {
                if (Value == null)
                    return 0;
                else if (Value is Array array)
                    return array.Length;
                else if (Value is IList list)
                    return list.Count;

                throw new Exception("类型错误");
            }
        }

        public ArrayElement()
        {
            sizeInput = new TextField();
            listBox = new VisualElement();
            listContainer = new VisualElement();
            footerContainer = new VisualElement();
            buttonBox = new VisualElement();
            addButton = new Button(OnAddButtonClick);
            removeButton = new Button(OnRemoveButtonClick);

            title.Add(sizeInput);

            sizeInput.RegisterValueChangedCallback(OnSizeChange);

            this.AddToClassList("array-element");
            sizeInput.AddToClassList("inspector-input");
            variableNameText.style.flexGrow = 1;
            sizeInput.style.width = 80;
            sizeInput.style.flexShrink = 0;
            sizeInput.style.marginLeft = 8;

            title.RegisterCallback<MouseEnterEvent>(OnTitleMouseEnter);
            title.RegisterCallback<MouseLeaveEvent>(OnTitleMouseLeave);

            elementsContent = new VisualElement();
            elementsContent.style.flexDirection = FlexDirection.Column;
            elementsContent.style.marginTop = 4;

            listBox.style.paddingTop = 4;
            listBox.style.paddingRight = 6;
            listBox.style.paddingBottom = 4;
            listBox.style.paddingLeft = 6;
            listBox.style.borderTopWidth = 1;
            listBox.style.borderRightWidth = 1;
            listBox.style.borderBottomWidth = 1;
            listBox.style.borderLeftWidth = 1;
            listBox.style.borderTopColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            listBox.style.borderRightColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            listBox.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            listBox.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            listBox.style.borderTopLeftRadius = 4;
            listBox.style.borderTopRightRadius = 4;
            listBox.style.borderBottomLeftRadius = 4;
            listBox.style.borderBottomRightRadius = 4;
            listBox.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.20f, 0.20f, 0.20f));

            listContainer.style.flexDirection = FlexDirection.Column;

            footerContainer.style.flexDirection = FlexDirection.Row;
            footerContainer.style.justifyContent = Justify.FlexEnd;
            footerContainer.style.marginTop = -1;
            footerContainer.style.marginRight = 18;

            buttonBox.style.flexDirection = FlexDirection.Row;
            buttonBox.style.paddingTop = 2;
            buttonBox.style.paddingRight = 4;
            buttonBox.style.paddingBottom = 2;
            buttonBox.style.paddingLeft = 4;
            buttonBox.style.borderTopWidth = 1;
            buttonBox.style.borderRightWidth = 1;
            buttonBox.style.borderBottomWidth = 1;
            buttonBox.style.borderLeftWidth = 1;
            buttonBox.style.borderTopColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            buttonBox.style.borderRightColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            buttonBox.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            buttonBox.style.borderLeftColor = new StyleColor(new UnityEngine.Color(0.24f, 0.24f, 0.24f));
            buttonBox.style.borderTopLeftRadius = 4;
            buttonBox.style.borderTopRightRadius = 4;
            buttonBox.style.borderBottomLeftRadius = 4;
            buttonBox.style.borderBottomRightRadius = 4;
            buttonBox.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.20f, 0.20f, 0.20f));

            addButton.text = "+";
            addButton.style.width = 24;
            addButton.style.marginRight = 4;

            removeButton.text = "-";
            removeButton.style.width = 24;

            buttonBox.Add(addButton);
            buttonBox.Add(removeButton);

            listBox.Add(listContainer);
            footerContainer.Add(buttonBox);

            elementsContent.Add(listBox);
            elementsContent.Add(footerContainer);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public ArrayElement(CustomerElementAttribute type) : this()
        {
            customerAttribute = type;
        }

        protected override void OnBound()
        {
            base.OnBound();
            elementType = IsArray ? BoundVariableType.GetElementType() : BoundVariableType.GetGenericArguments()[0];
            sizeInput.value = Length.ToString();
        }

        protected override void OnDepthChange(int depth)
        {
            base.OnDepthChange(depth);
            listBox.style.marginLeft = Inspector.TabSize * (Depth);
        }

        protected override void CreateElements()
        {
            if (Value == null || Length <= 0)
            {
                AddEmptyState();
                return;
            }

            if (IsArray)
            {
                Array array = (Array)Value;
                for (int i = 0; i < array.Length; i++)
                {
                    InspectorElement elementDrawer = CreateDrawerForMemberType();
                    if (elementDrawer == null)
                        break;

                    int index = i;
                    elementDrawer.BindTo(elementType, "Element " + i, () => ((Array)Value).GetValue(index), (value) =>
                    {
                        Array _array = (Array)Value;
                        _array.SetValue(value, index);
                        Value = _array;
                    });
                    elementDrawer.Refresh();

                    AddElementRow(index, elementDrawer);
                }
            }
            else
            {
                IList list = (IList)Value;
                for (int i = 0; i < list.Count; i++)
                {
                    InspectorElement elementDrawer = CreateDrawerForMemberType();
                    if (elementDrawer == null)
                        break;

                    int j = i;
                    elementDrawer.BindTo(elementType, "Element " + i, () => ((IList)Value)[j], (value) =>
                    {
                        IList _list = (IList)Value;
                        _list[j] = value;
                        Value = _list;
                    });
                    elementDrawer.Refresh();

                    AddElementRow(j, elementDrawer);
                }
            }

            if (selectedIndex >= Length)
            {
                ClearSelection();
            }
        }

        protected override void ClearElements()
        {
            listContainer.Clear();
            selectedRow = null;
        }

        private void AddEmptyState()
        {
            Label emptyLabel = new Label("List Is Empty");
            emptyLabel.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
            emptyLabel.style.color = new StyleColor(new UnityEngine.Color(0.7f, 0.7f, 0.7f));
            emptyLabel.style.marginTop = 6;
            emptyLabel.style.marginBottom = 6;
            listContainer.Add(emptyLabel);
        }

        private InspectorElement CreateDrawerForMemberType()
        {
            if(customerAttribute != null)
            {
                return Inspector.CreateDrawerForType(customerAttribute.type, 0, customerAttribute.args);
            }
            else
            {
                return Inspector.CreateDrawerForMemberType(elementType, 0);
            }
        }

        private void OnSizeChange(ChangeEvent<string> input)
        {
            if (int.TryParse(input.newValue, out int size))
            {
                if(size > 100)
                {
                    UnityEngine.Debug.LogWarning("输入的数组长度过大");
                    sizeInput.value = input.previousValue;
                    return;
                }

                if (size != Length && size >= 0)
                {
                    int currLength = Length;
                    if (IsArray)
                    {
                        Array array = (Array)Value;
                        Array newArray = Array.CreateInstance(BoundVariableType.GetElementType(), size);
                        if (size > currLength)
                        {
                            if (array != null)
                                Array.ConstrainedCopy(array, 0, newArray, 0, currLength);
                        }
                        else
                            Array.ConstrainedCopy(array, 0, newArray, 0, size);

                        Value = newArray;
                    }
                    else
                    {
                        IList list = (IList)Value;

                        int differLength = size - currLength;
                        if (differLength > 0)
                        {
                            if (list == null)
                                list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(BoundVariableType.GetGenericArguments()[0]));

                            for (int i = 0; i < differLength; i++)
                                list.Add(CreateDefaultElementValue());
                        }
                        else
                        {
                            for (int i = 0; i > differLength; i--)
                                list.RemoveAt(list.Count - 1);
                        }

                        Value = list;
                    }

                    Refresh();
                }
            }
            else if(string.IsNullOrEmpty(input.newValue))
            {
                sizeInput.value = "0";
            }
            else
            {
                sizeInput.value = input.previousValue;
            }
        }

        private object CreateDefaultElementValue()
        {
            return elementType != null && elementType.IsValueType
                ? Activator.CreateInstance(elementType)
                : null;
        }

        private void AddElementRow(int index, InspectorElement elementDrawer)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;
            row.style.marginBottom = 2;

            row.Add(elementDrawer);
            row.RegisterCallback<MouseDownEvent>((evt) =>
            {
                if (evt.button == 0)
                {
                    SelectRow(row, index);
                }
            });

            listContainer.Add(row);

            if (selectedIndex == index)
            {
                ApplyRowSelection(row, true);
                selectedRow = row;
            }
        }

        private void SelectRow(VisualElement row, int index)
        {
            if (selectedRow == row && selectedIndex == index)
                return;

            ApplyRowSelection(selectedRow, false);
            selectedRow = row;
            selectedIndex = index;
            ApplyRowSelection(selectedRow, true);
        }

        private void ClearSelection()
        {
            ApplyRowSelection(selectedRow, false);
            selectedRow = null;
            selectedIndex = -1;
        }

        private void ApplyRowSelection(VisualElement row, bool selected)
        {
            if (row == null)
                return;

            TextElement titleLabel = row.Q<TextElement>(className: "inspector-label");

            if (selected)
            {
                row.style.backgroundColor = new StyleColor(new UnityEngine.Color(1f, 1f, 1f, 0.06f));
                if (titleLabel != null)
                {
                    titleLabel.style.color = new StyleColor(new UnityEngine.Color(0.35f, 0.65f, 1f));
                }
            }
            else
            {
                row.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                if (titleLabel != null)
                {
                    titleLabel.style.color = new StyleColor(StyleKeyword.Null);
                }
            }
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            evt.destinationPanel?.visualTree.RegisterCallback<MouseDownEvent>(OnPanelMouseDown, TrickleDown.TrickleDown);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            evt.originPanel?.visualTree.UnregisterCallback<MouseDownEvent>(OnPanelMouseDown, TrickleDown.TrickleDown);
        }

        private void OnPanelMouseDown(MouseDownEvent evt)
        {
            if (selectedRow == null || evt.target is not VisualElement target)
                return;

            if (selectedRow.Contains(target) || buttonBox.Contains(target))
                return;

            ClearSelection();
        }

        private void OnAddButtonClick()
        {
            int newIndex = Length;

            if (IsArray)
            {
                Array array = Value as Array;
                Array newArray = Array.CreateInstance(BoundVariableType.GetElementType(), newIndex + 1);
                if (array != null && newIndex > 0)
                {
                    Array.ConstrainedCopy(array, 0, newArray, 0, newIndex);
                }

                newArray.SetValue(CreateDefaultElementValue(), newIndex);
                Value = newArray;
            }
            else
            {
                IList list = Value as IList;
                if (list == null)
                {
                    list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(BoundVariableType.GetGenericArguments()[0]));
                }

                list.Add(CreateDefaultElementValue());
                Value = list;
            }

            selectedIndex = newIndex;
            Refresh();
        }

        private void OnRemoveButtonClick()
        {
            if (selectedIndex < 0 || selectedIndex >= Length)
                return;

            int removedIndex = selectedIndex;

            if (IsArray)
            {
                Array array = Value as Array;
                if (array == null || array.Length == 0)
                    return;

                int newLength = array.Length - 1;
                Array newArray = Array.CreateInstance(BoundVariableType.GetElementType(), newLength);

                if (selectedIndex > 0)
                {
                    Array.ConstrainedCopy(array, 0, newArray, 0, selectedIndex);
                }

                if (selectedIndex < newLength)
                {
                    Array.ConstrainedCopy(array, selectedIndex + 1, newArray, selectedIndex, newLength - selectedIndex);
                }

                Value = newArray;
            }
            else
            {
                IList list = Value as IList;
                if (list == null || selectedIndex >= list.Count)
                    return;

                list.RemoveAt(selectedIndex);
                Value = list;
            }

            int remainingLength = Length;
            if (remainingLength <= 0)
            {
                ClearSelection();
            }
            else
            {
                selectedIndex = Math.Max(0, removedIndex - 1);
                selectedRow = null;
            }

            Refresh();
        }

        private void OnTitleMouseEnter(MouseEnterEvent evt)
        {
            title.style.backgroundColor = new StyleColor(new UnityEngine.Color(1f, 1f, 1f, 0.06f));
            variableNameText.style.color = new StyleColor(new UnityEngine.Color(0.35f, 0.65f, 1f));
        }

        private void OnTitleMouseLeave(MouseLeaveEvent evt)
        {
            title.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            variableNameText.style.color = new StyleColor(StyleKeyword.Null);
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
