using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.UI;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(DataTableRefAttribute))]
    public sealed class DataTableRefElement : XInspectorElement, IPropertyAttributeElement
    {
        private DataTableRefAttribute m_Attribute;
        private readonly VisualElement m_ValueField;
        private readonly Label m_ValueLabel;

        public DataTableRefElement()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            m_ValueField = new VisualElement();
            m_ValueField.AddToClassList("inspector-input");
            m_ValueField.style.flexGrow = 1f;
            m_ValueField.style.minHeight = 22f;
            m_ValueField.style.justifyContent = Justify.Center;
            m_ValueField.style.paddingLeft = 6f;
            m_ValueField.style.paddingRight = 6f;
            m_ValueField.style.borderTopWidth = 1f;
            m_ValueField.style.borderBottomWidth = 1f;
            m_ValueField.style.borderLeftWidth = 1f;
            m_ValueField.style.borderRightWidth = 1f;
            m_ValueField.style.borderTopColor = new Color(0.32f, 0.32f, 0.32f, 0.95f);
            m_ValueField.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f, 0.95f);
            m_ValueField.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 0.95f);
            m_ValueField.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f, 0.95f);
            m_ValueField.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.85f);

            m_ValueLabel = new Label
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    flexGrow = 1f
                }
            };
            m_ValueField.Add(m_ValueLabel);

            var mPickerButton = new Button(OpenPicker)
            {
                text = "◉",
                style =
                {
                    width = 24f,
                    minWidth = 24f,
                    marginLeft = 4f,
                    flexShrink = 0f
                },
                tooltip = "选择引用"
            };

            Add(m_ValueField);
            Add(mPickerButton);
            m_ValueField.RegisterCallback<MouseDownEvent>(OnValueFieldMouseDown, TrickleDown.TrickleDown);
        }

        public void SetPropertyAttribute(PropertyAttribute attribute)
        {
            m_Attribute = attribute as DataTableRefAttribute;
        }

        public override void Refresh()
        {
            base.Refresh();
            UpdateDisplay();
        }

        protected override void OnBound()
        {
            base.OnBound();
            m_Attribute ??= BoundMemberInfo?.GetCustomAttribute<DataTableRefAttribute>();
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            XDataTableRefMeta meta = ResolveMeta(out string resolveError);
            string displayText = !string.IsNullOrEmpty(resolveError)
                ? resolveError
                : XDataTableRefResolver.GetDisplayText(meta, Value, BoundVariableType);

            if (string.IsNullOrEmpty(displayText))
            {
                displayText = "None";
            }

            m_ValueLabel.text = displayText;
            m_ValueField.tooltip = displayText;
        }

        private XDataTableRefMeta ResolveMeta(out string resolveError)
        {
            return XDataTableRefResolver.Resolve(BoundVariableType, Name, m_Attribute?.tableType, out resolveError);
        }

        private void OnValueFieldMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            XDataTableRefMeta meta = ResolveMeta(out _);
            XDataTableRefResolver.PingReferencedTable(meta);
            if (evt.clickCount >= 2)
            {
                XDataTableRefResolver.OpenReferencedTable(meta, Value, BoundVariableType);
            }

            evt.StopImmediatePropagation();
        }

        private void OpenPicker()
        {
            XDataTableRefMeta meta = ResolveMeta(out string resolveError);
            if (!string.IsNullOrEmpty(resolveError))
            {
                Debug.LogWarning(resolveError);
                return;
            }

            Rect anchorRect = worldBound;
            if (panel?.visualTree != null)
            {
                Vector2 screenPosition = GUIUtility.GUIToScreenPoint(anchorRect.position);
                anchorRect.position = screenPosition;
            }

            XDataTableRefPickerWindow.ShowWindow(
                Name,
                meta,
                BoundVariableType,
                pickedValue =>
                {
                    if (XDataTableRefResolver.TryConvertReferenceValue(BoundVariableType, pickedValue, out object converted))
                    {
                        Value = converted;
                        UpdateDisplay();
                    }
                },
                anchorRect,
                Value);
        }
    }
}
