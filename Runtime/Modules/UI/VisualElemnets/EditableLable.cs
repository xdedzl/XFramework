using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    public class EditableLabel: VisualElement
    {
        private readonly TextElement m_label;
        private readonly TextField m_textField;
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
        
        private void BeginEditTitle()
        {
            if (IsEditing)
            {
                return;
            }

            Remove(m_label);
            Add(m_textField);
            m_textField.Focus();
            m_textField.SelectAll();
            EditStarted?.Invoke();
        }

        private void EndEditTitle()
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
            if (editable)
            {
                m_textField.RegisterCallback<FocusOutEvent>(OnFocusOut);
                m_textField.RegisterCallback<KeyDownEvent>(OnKeyDown);
                
                // 监听 title 区域的双击事件
                RegisterCallback<MouseDownEvent>(OnMouseDown);
            }
            else
            {
                m_textField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
                m_textField.UnregisterCallback<KeyDownEvent>(OnKeyDown);
                UnregisterCallback<MouseDownEvent>(OnMouseDown);
            }   
            
            return;

            void OnFocusOut(FocusOutEvent e)
            {
                EndEditTitle();
            }

            void OnKeyDown(KeyDownEvent e)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    EndEditTitle();
                }
            }

            void OnMouseDown(MouseDownEvent e)
            {
                if (e.clickCount == 2)
                {
                    BeginEditTitle();
                    e.StopImmediatePropagation();
                }
            }
        }
    }
}
