using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    public enum EditableLabelEditTrigger
    {
        DoubleClick,
        ContextMenu
    }

    public class EditableLabel: VisualElement
    {
        private readonly TextElement m_label;
        private readonly TextField m_textField;
        private ContextualMenuManipulator m_ContextualMenuManipulator;
        private bool m_Editable;
        private EditableLabelEditTrigger m_EditTrigger = EditableLabelEditTrigger.DoubleClick;
        public event Action<string, string> ValueCommitted;
        public event Action EditStarted;
        public event Action EditEnded;
        public bool IsEditing => Contains(m_textField);
        
        public EditableLabel() : this(string.Empty)
        {
        }
        
        public EditableLabel(string text)
        {
            m_label = new TextElement()
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

            m_textField = new TextField
            {
                value = text,
                style =
                {
                    justifyContent = Justify.Center,
                    // alignItems = Align.Center,
                    // height = new Length(100, LengthUnit.Percent),
                    // width = new Length(100, LengthUnit.Percent),
                    marginBottom = 5,
                    marginTop = 5,
                }
            };

            var inputText = m_textField.Q("unity-text-input").ElementAt(0);
            if (inputText != null)
            {
                inputText.style.fontSize = 15;  
            }
            
            Add(m_label);
        }
        
        public string text
        {
            get => m_label.text;
            set
            {
                m_label.text = value;
                m_textField.value = value;
            }
        }
        
        public void BeginEdit()
        {
            if (!m_Editable || IsEditing)
            {
                return;
            }

            Remove(m_label);
            Add(m_textField);
            m_textField.Focus();
            m_textField.SelectAll();
            EditStarted?.Invoke();
        }

        public void EndEdit()
        {
            if (!IsEditing)
            {
                return;
            }

            string oldText = text;
            Remove(m_textField);
            Add(m_label);
            text = m_textField.value;
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
            m_textField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            m_textField.RegisterCallback<KeyDownEvent>(OnKeyDown);

            switch (m_EditTrigger)
            {
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
            m_textField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            m_textField.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            UnregisterCallback<MouseDownEvent>(OnMouseDown);
            if (m_ContextualMenuManipulator != null)
            {
                m_ContextualMenuManipulator.target = null;
            }
        }

        private void OnFocusOut(FocusOutEvent e)
        {
            EndEdit();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                EndEdit();
            }
        }

        private void OnMouseDown(MouseDownEvent e)
        {
            if (e.clickCount == 2)
            {
                BeginEdit();
                e.StopImmediatePropagation();
            }
        }

        private void OnContextualMenuPopulate(ContextualMenuPopulateEvent e)
        {
            e.menu.AppendAction("Rename", _ => BeginEdit(), IsEditing ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
        }
    }
}
