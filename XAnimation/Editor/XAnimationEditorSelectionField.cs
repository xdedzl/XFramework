#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    internal sealed class XAnimationEditorSelectionField : VisualElement
    {
        private readonly Label m_DropdownArrow;
        private readonly Button m_Button;
        private readonly VisualElement m_InputContainer;
        private readonly VisualElement m_TrailingContainer;
        private readonly Action<XAnimationEditorSelectionField> m_ShowMenu;
        private string m_Value;

        public XAnimationEditorSelectionField(string label, string value, Action<XAnimationEditorSelectionField> showMenu)
        {
            m_ShowMenu = showMenu;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            if (!string.IsNullOrWhiteSpace(label))
            {
                Label caption = new(label);
                caption.style.minWidth = 72;
                caption.style.marginRight = 4;
                caption.style.color = XAnimationEditorUi.TextMuted;
                caption.style.fontSize = XAnimationEditorUi.BodyFontSize;
                Add(caption);
            }

            m_InputContainer = new VisualElement();
            m_InputContainer.style.flexDirection = FlexDirection.Row;
            m_InputContainer.style.alignItems = Align.Center;
            m_InputContainer.style.flexGrow = 1;
            m_InputContainer.style.minWidth = 0;
            m_InputContainer.style.overflow = Overflow.Hidden;
            m_InputContainer.style.backgroundColor = XAnimationEditorUi.ListHeaderBg;
            XAnimationEditorUi.SetBorder(m_InputContainer, XAnimationEditorUi.PaneBorder, 1f, 3f);
            Add(m_InputContainer);

            m_Button = new Button(() => m_ShowMenu?.Invoke(this));
            m_Button.style.flexGrow = 1;
            m_Button.style.minWidth = 0;
            m_Button.style.unityTextAlign = TextAnchor.MiddleLeft;
            m_Button.style.paddingLeft = 6;
            m_Button.style.paddingRight = 6;
            m_Button.style.backgroundColor = Color.clear;
            m_Button.style.borderTopWidth = 0;
            m_Button.style.borderBottomWidth = 0;
            m_Button.style.borderLeftWidth = 0;
            m_Button.style.borderRightWidth = 0;
            m_Button.style.color = XAnimationEditorUi.TextNormal;
            m_InputContainer.Add(m_Button);

            m_TrailingContainer = new VisualElement();
            m_TrailingContainer.style.flexDirection = FlexDirection.Row;
            m_TrailingContainer.style.alignItems = Align.Center;
            m_TrailingContainer.style.flexShrink = 0;
            m_TrailingContainer.style.paddingLeft = 2;
            m_TrailingContainer.style.paddingRight = 2;
            m_TrailingContainer.style.borderLeftWidth = 1;
            m_TrailingContainer.style.borderLeftColor = XAnimationEditorUi.PaneBorder;

            m_DropdownArrow = new Label("▾");
            m_DropdownArrow.pickingMode = PickingMode.Ignore;
            m_DropdownArrow.style.width = 16;
            m_DropdownArrow.style.minWidth = 16;
            m_DropdownArrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_DropdownArrow.style.color = XAnimationEditorUi.TextMuted;
            m_DropdownArrow.style.fontSize = XAnimationEditorUi.BodyFontSize;
            m_DropdownArrow.style.flexShrink = 0;
            m_DropdownArrow.style.marginLeft = 2;
            m_DropdownArrow.style.marginRight = 2;
            m_TrailingContainer.Add(m_DropdownArrow);
            m_TrailingContainer.RegisterCallback<MouseDownEvent>(OnTrailingContainerMouseDown);
            m_InputContainer.Add(m_TrailingContainer);
            SetValueWithoutNotify(value);
        }

        public event Action<string, string> ValueChanged;

        public string value
        {
            get => m_Value;
            set
            {
                string previous = m_Value;
                SetValueWithoutNotify(value);
                if (!string.Equals(previous, m_Value, StringComparison.Ordinal))
                {
                    ValueChanged?.Invoke(previous, m_Value);
                }
            }
        }

        public void SetValueWithoutNotify(string value)
        {
            m_Value = value ?? string.Empty;
            m_Button.text = string.IsNullOrWhiteSpace(m_Value) ? "None" : m_Value;
            m_Button.tooltip = m_Button.text;
        }

        public void RegisterValueChangedCallback(EventCallback<ChangeEvent<string>> callback)
        {
            if (callback == null)
            {
                return;
            }

            ValueChanged += (previous, current) =>
            {
                using ChangeEvent<string> evt = ChangeEvent<string>.GetPooled(previous, current);
                evt.target = this;
                callback(evt);
            };
        }

        public void AddTrailingElement(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.RemoveFromHierarchy();
            if (m_TrailingContainer.childCount > 1)
            {
                VisualElement separator = new VisualElement();
                separator.pickingMode = PickingMode.Ignore;
                separator.style.width = 1;
                separator.style.height = 12;
                separator.style.marginLeft = 2;
                separator.style.marginRight = 2;
                separator.style.backgroundColor = XAnimationEditorUi.PaneBorder;
                m_TrailingContainer.Insert(m_TrailingContainer.childCount - 1, separator);
            }

            if (element is Button button)
            {
                button.style.backgroundColor = Color.clear;
                button.style.borderTopWidth = 0;
                button.style.borderBottomWidth = 0;
                button.style.borderLeftWidth = 0;
                button.style.borderRightWidth = 0;
                button.style.marginLeft = 0;
                button.style.marginRight = 0;
            }

            element.style.flexShrink = 0;
            m_TrailingContainer.Insert(m_TrailingContainer.childCount - 1, element);
        }

        private void OnTrailingContainerMouseDown(MouseDownEvent evt)
        {
            if (evt == null || evt.button != 0 || !enabledInHierarchy)
            {
                return;
            }

            if (evt.target is VisualElement target)
            {
                for (VisualElement current = target; current != null && current != m_TrailingContainer; current = current.hierarchy.parent)
                {
                    if (current is Button)
                    {
                        return;
                    }
                }
            }

            m_ShowMenu?.Invoke(this);
            evt.StopPropagation();
        }

        public new void SetEnabled(bool enabled)
        {
            base.SetEnabled(enabled);
        }
    }
}
#endif
