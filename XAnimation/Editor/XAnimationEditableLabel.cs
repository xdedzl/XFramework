#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    internal enum EditableLabelEditTrigger
    {
        None,
        DoubleClick,
        ContextMenu,
    }

    internal sealed class EditableLabel : VisualElement
    {
        private readonly TextElement m_Label;
        private readonly TextField m_TextField;
        private ContextualMenuManipulator m_ContextualMenuManipulator;
        private bool m_Editable;
        private EditableLabelEditTrigger m_EditTrigger = EditableLabelEditTrigger.DoubleClick;

        public EditableLabel()
            : this(string.Empty)
        {
        }

        public EditableLabel(string text)
        {
            m_Label = new TextElement
            {
                text = text,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    height = new Length(100, LengthUnit.Percent),
                    width = new Length(100, LengthUnit.Percent),
                    fontSize = 15,
                }
            };

            m_TextField = new TextField
            {
                value = text,
                style =
                {
                    justifyContent = Justify.Center,
                    marginBottom = 5,
                    marginTop = 5,
                }
            };

            VisualElement inputText = m_TextField.Q("unity-text-input");
            if (inputText != null)
            {
                inputText.style.fontSize = 15;
            }

            Add(m_Label);
        }

        public event Action<string, string> ValueCommitted;
        public event Action EditStarted;
        public event Action EditEnded;

        public bool IsEditing => Contains(m_TextField);

        public string text
        {
            get => m_Label.text;
            set
            {
                m_Label.text = value;
                m_TextField.value = value;
            }
        }

        public void BeginEdit()
        {
            if (!m_Editable || IsEditing)
            {
                return;
            }

            Remove(m_Label);
            Add(m_TextField);
            m_TextField.Focus();
            m_TextField.SelectAll();
            EditStarted?.Invoke();
        }

        public void EndEdit()
        {
            if (!IsEditing)
            {
                return;
            }

            string oldText = text;
            Remove(m_TextField);
            Add(m_Label);
            text = m_TextField.value;
            if (!string.Equals(oldText, text, StringComparison.Ordinal))
            {
                ValueCommitted?.Invoke(oldText, text);
            }

            EditEnded?.Invoke();
        }

        public void SetEditable(bool editable)
        {
            SetEditable(editable, m_EditTrigger);
        }

        public void SetEditable(bool editable, EditableLabelEditTrigger editTrigger)
        {
            if (m_Editable)
            {
                UnregisterEditCallbacks();
            }

            m_Editable = editable;
            m_EditTrigger = editTrigger;

            if (editable)
            {
                RegisterEditCallbacks();
            }
        }

        private void RegisterEditCallbacks()
        {
            m_TextField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            m_TextField.RegisterCallback<KeyDownEvent>(OnKeyDown);

            switch (m_EditTrigger)
            {
                case EditableLabelEditTrigger.None:
                    break;
                case EditableLabelEditTrigger.DoubleClick:
                    RegisterCallback<MouseDownEvent>(OnMouseDown);
                    break;
                case EditableLabelEditTrigger.ContextMenu:
                    m_ContextualMenuManipulator ??= new ContextualMenuManipulator(OnContextualMenuPopulate);
                    m_ContextualMenuManipulator.target = this;
                    break;
            }
        }

        private void UnregisterEditCallbacks()
        {
            m_TextField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            m_TextField.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            UnregisterCallback<MouseDownEvent>(OnMouseDown);
            if (m_ContextualMenuManipulator != null)
            {
                m_ContextualMenuManipulator.target = null;
            }
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            EndEdit();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                EndEdit();
            }
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.clickCount == 2)
            {
                BeginEdit();
                evt.StopImmediatePropagation();
            }
        }

        private void OnContextualMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction(
                "Rename",
                _ => BeginEdit(),
                IsEditing ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }
    }
}
#endif
