using UnityEngine.UIElements;
using UnityEngine;

using System.Collections.Generic;
using System.Linq;

namespace XFramework.UI
{
    public abstract class ExpandableElement : InspectorElement
    {
        private readonly Foldout foldout;
        
        protected VisualElement title;
        protected VisualElement elementsContent;
        
        protected ExpandableElement()
        {
            Remove(variableNameText);
            title = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    right = 24
                },
                focusable = true,
                tabIndex = 0
            };
            foldout = new Foldout
            {
                value = false,
                style =
                {
                    minWidth = 24,
                    maxWidth = 24,
                    left = 8
                }
            };
            foldout.RegisterValueChangedCallback((e) =>
            {
                if (e.newValue)
                {
                    Expand();
                }
                else
                {
                    Collapse();
                }

                title.schedule.Execute(() => title.Focus());
            });
            foldout.RegisterCallback<MouseDownEvent>(OnFoldoutMouseDown);
            foldout.RegisterCallback<KeyDownEvent>(OnTitleKeyDown);
            title.RegisterCallback<MouseDownEvent>(OnTitleMouseDown);
            title.RegisterCallback<KeyDownEvent>(OnTitleKeyDown);
            title.Add(foldout);
            title.Add(variableNameText);

            elementsContent = new VisualElement();

            Add(title);
        }
        
        // 展开
        public void Expand()
        {
            if (!foldout.value)
            {
                foldout.value = true;
            }

            if (elementsContent.parent != this)
            {
                Add(elementsContent);
            }
        }
        
        // 折叠
        public void Collapse()
        {
            if (foldout.value)
            {
                foldout.value = false;
            }

            if (elementsContent.parent == this)
            {
                Remove(elementsContent);
            }
        }

        public override void Refresh()
        {
            base.Refresh();
            ClearElements();
            CreateElements();
        }

        protected virtual void CreateElements()
        {

        }

        protected virtual void ClearElements()
        {

        }

        public void SetArrowActive(bool value)
        {
            if (value && !title.Contains(foldout))
            {
                title.Insert(0, foldout);
            }
            else if(!value && title.Contains(foldout))
            {
                title.Remove(foldout);
            }
        }

        public IEnumerable<InspectorElement> GetChildElements()
        {
            return elementsContent.Children().OfType<InspectorElement>();
        }

        protected override void OnDepthChange(int depth)
        {
            base.OnDepthChange(depth);
            variableNameText.style.translate = new Vector2(Inspector.TabSize * Depth, 0f);
            foldout.style.translate= new Vector2(Inspector.TabSize * Depth, 0f);
        }

        private void OnTitleMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.target is not VisualElement target)
                return;

            if (target.GetFirstAncestorOfType<TextField>() != null
                || target.GetFirstAncestorOfType<Button>() != null)
            {
                return;
            }

            title.Focus();

            if (target == foldout || target.GetFirstAncestorOfType<Foldout>() != null)
            {
                return;
            }

            foldout.value = !foldout.value;
            evt.StopPropagation();
        }

        private void OnTitleKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.LeftArrow)
            {
                Collapse();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                Expand();
                evt.StopPropagation();
            }
        }

        private void OnFoldoutMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 0)
            {
                title.Focus();
            }
        }
    }
}
