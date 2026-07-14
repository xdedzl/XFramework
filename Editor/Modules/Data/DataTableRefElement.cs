using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UEvent = UnityEngine.Event;
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

    [CustomPropertyDrawer(typeof(DataTableRefAttribute))]
    public sealed class DataTableRefDrawer : PropertyDrawer
    {
        private const float PickerButtonWidth = 24f;
        private const float ControlSpacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var dataTableRefAttribute = (DataTableRefAttribute)attribute;
            XDataTableRefMeta meta = XDataTableRefResolver.Resolve(fieldInfo, dataTableRefAttribute, out string resolveError);
            if (!string.IsNullOrEmpty(resolveError))
            {
                EditorGUI.HelpBox(position, resolveError, MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            Type fieldType = fieldInfo.FieldType;
            if (!TryGetPropertyValue(property, fieldType, out object currentValue))
            {
                EditorGUI.HelpBox(position, $"{label.text} 的字段类型 {fieldType.Name} 不受 DataTableRefDrawer 支持。", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            Rect controlRect = EditorGUI.PrefixLabel(position, label);
            Rect pickerRect = new(
                controlRect.xMax - PickerButtonWidth,
                controlRect.y,
                PickerButtonWidth,
                controlRect.height);
            Rect valueRect = new(
                controlRect.x,
                controlRect.y,
                controlRect.width - PickerButtonWidth - ControlSpacing,
                controlRect.height);

            string displayText = property.hasMultipleDifferentValues
                ? "—"
                : XDataTableRefResolver.GetDisplayText(meta, currentValue, fieldType);
            var displayContent = new GUIContent(displayText, displayText);

            if (GUI.Button(valueRect, displayContent, EditorStyles.textField))
            {
                if (UEvent.current.clickCount >= 2)
                {
                    XDataTableRefResolver.OpenReferencedTable(meta, currentValue, fieldType);
                }
                else
                {
                    XDataTableRefResolver.PingReferencedTable(meta);
                }
            }

            if (GUI.Button(pickerRect, new GUIContent("◉", "选择数据表引用")))
            {
                ShowPicker(property, label.text, meta, fieldType, currentValue, pickerRect);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        private static void ShowPicker(
            SerializedProperty property,
            string title,
            XDataTableRefMeta meta,
            Type fieldType,
            object currentValue,
            Rect pickerRect)
        {
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;
            pickerRect.position = GUIUtility.GUIToScreenPoint(pickerRect.position);

            XDataTableRefPickerWindow.ShowWindow(
                title,
                meta,
                fieldType,
                pickedValue => SetPropertyValue(serializedObject, propertyPath, fieldType, pickedValue),
                pickerRect,
                currentValue);
        }

        private static void SetPropertyValue(
            SerializedObject serializedObject,
            string propertyPath,
            Type fieldType,
            object pickedValue)
        {
            if (!XDataTableRefResolver.TryConvertReferenceValue(fieldType, pickedValue, out object converted))
            {
                throw new InvalidOperationException($"无法将数据表主键转换为 {fieldType.Name}。");
            }

            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            SetPropertyValue(property, fieldType, converted);
            serializedObject.ApplyModifiedProperties();
        }

        private static bool TryGetPropertyValue(SerializedProperty property, Type fieldType, out object value)
        {
            if (fieldType == typeof(string) && property.propertyType == SerializedPropertyType.String)
            {
                value = property.stringValue;
                return true;
            }

            if (property.propertyType != SerializedPropertyType.Integer)
            {
                value = null;
                return false;
            }

            if (fieldType == typeof(ulong))
            {
                value = property.ulongValue;
                return true;
            }

            long number = property.longValue;
            value = fieldType == typeof(long) ? number
                : fieldType == typeof(uint) ? (uint)number
                : fieldType == typeof(int) ? (int)number
                : fieldType == typeof(ushort) ? (ushort)number
                : fieldType == typeof(short) ? (short)number
                : fieldType == typeof(byte) ? (byte)number
                : fieldType == typeof(sbyte) ? (sbyte)number
                : fieldType == typeof(char) ? (char)number
                : null;
            return value != null;
        }

        private static void SetPropertyValue(SerializedProperty property, Type fieldType, object value)
        {
            if (fieldType == typeof(string))
            {
                property.stringValue = (string)value;
                return;
            }

            if (fieldType == typeof(ulong))
            {
                property.ulongValue = (ulong)value;
                return;
            }

            property.longValue = Convert.ToInt64(value);
        }
    }
}
