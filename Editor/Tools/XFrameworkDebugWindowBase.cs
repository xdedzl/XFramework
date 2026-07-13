using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public abstract class XFrameworkDebugWindowBase : EditorWindow, IHasCustomMenu
    {
        protected const double DefaultRefreshInterval = 0.5d;

        private double m_LastRefreshTime;
        private bool m_EnableAutoRefresh = true;
        private Button m_AutoRefreshToggle;

        protected virtual double RefreshInterval => DefaultRefreshInterval;
        protected virtual bool EnableAutoRefresh => m_EnableAutoRefresh;

        protected virtual void OnRefreshClicked()
        {
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("关闭所有Debug窗口"), false, CloseAllDebugWindows);
            menu.AddItem(new GUIContent("关闭其他所有Debug窗口"), false, CloseOtherDebugWindows);
        }

        protected virtual void OnEnable()
        {
            if (m_EnableAutoRefresh)
            {
                EditorApplication.update += HandleEditorUpdate;
            }
        }

        protected virtual void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            XFrameworkInspectorWindow.ClearIfOwner(this);
        }

        private void ToggleAutoRefresh(bool enabled)
        {
            if (m_EnableAutoRefresh == enabled)
            {
                return;
            }

            m_EnableAutoRefresh = enabled;
            if (enabled)
            {
                EditorApplication.update += HandleEditorUpdate;
            }
            else
            {
                EditorApplication.update -= HandleEditorUpdate;
            }

            ApplyAutoRefreshButtonStyle();
        }

        private void ApplyAutoRefreshButtonStyle()
        {
            if (m_AutoRefreshToggle == null)
            {
                return;
            }

            if (m_EnableAutoRefresh)
            {
                m_AutoRefreshToggle.text = "自动刷新";
                m_AutoRefreshToggle.style.color = new Color(0.30f, 0.85f, 0.46f);
            }
            else
            {
                m_AutoRefreshToggle.text = "自动刷新";
                m_AutoRefreshToggle.style.color = new Color(0.55f, 0.55f, 0.55f);
            }
        }

        protected void AddRefreshControls(VisualElement toolbar, string refreshTooltip = null)
        {
            Button refreshButton = new(OnRefreshClicked)
            {
                text = "刷新"
            };
            refreshButton.style.marginLeft = 8f;
            refreshButton.style.width = 64f;
            if (!string.IsNullOrEmpty(refreshTooltip))
            {
                refreshButton.tooltip = refreshTooltip;
            }

            toolbar.Add(refreshButton);

            m_AutoRefreshToggle = new Button(() => ToggleAutoRefresh(!m_EnableAutoRefresh));
            ApplyAutoRefreshButtonStyle();
            m_AutoRefreshToggle.style.marginLeft = 8f;
            m_AutoRefreshToggle.style.width = 80f;
            toolbar.Add(m_AutoRefreshToggle);

            string labelText = $"自动刷新: {RefreshInterval:F1}s";
            Label autoRefreshLabel = new(labelText);
            autoRefreshLabel.style.marginLeft = 4f;
            autoRefreshLabel.style.color = new Color(0.70f, 0.70f, 0.70f);
            toolbar.Add(autoRefreshLabel);
        }

        private void HandleEditorUpdate()
        {
            if (!EnableAutoRefresh)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - m_LastRefreshTime < RefreshInterval)
            {
                return;
            }

            m_LastRefreshTime = now;
            OnAutoRefresh();
        }

        protected virtual void OnAutoRefresh()
        {
        }

        protected void MarkRefreshDirty()
        {
            m_LastRefreshTime = 0d;
        }

        protected static void CloseAllDebugWindows()
        {
            foreach (XFrameworkDebugWindowBase window in GetAllDebugWindows())
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        protected static void CloseOtherDebugWindows()
        {
            XFrameworkDebugWindowBase current = focusedWindow as XFrameworkDebugWindowBase;
            foreach (XFrameworkDebugWindowBase window in GetAllDebugWindows())
            {
                if (window != null && window != current)
                {
                    window.Close();
                }
            }
        }

        private static IEnumerable<XFrameworkDebugWindowBase> GetAllDebugWindows()
        {
            return Resources.FindObjectsOfTypeAll<XFrameworkDebugWindowBase>();
        }

        protected static VisualElement CreateRow(Color backgroundColor, float minHeight)
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = minHeight,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    overflow = Overflow.Hidden,
                    backgroundColor = backgroundColor
                }
            };
        }

        protected static Label CreateHeaderLabel(string text, float width, float marginRight = 0f, bool flexShrink = false)
        {
            Label label = new(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.82f, 0.82f, 0.82f);
            if (width > 0f)
            {
                label.style.width = width;
            }
            if (marginRight > 0f)
            {
                label.style.marginRight = marginRight;
            }
            if (flexShrink)
            {
                label.style.flexShrink = 0f;
            }
            label.style.overflow = Overflow.Hidden;
            return label;
        }

        protected static Label CreateCellLabel(string name, float width, bool bold = false, float marginRight = 0f, bool flexShrink = false, TextOverflow textOverflow = TextOverflow.Clip, bool noWrap = false)
        {
            Label label = new() { name = name };
            if (width > 0f)
            {
                label.style.width = width;
            }
            label.style.overflow = Overflow.Hidden;
            if (bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            if (marginRight > 0f)
            {
                label.style.marginRight = marginRight;
            }
            if (flexShrink)
            {
                label.style.flexShrink = 0f;
            }
            if (textOverflow != TextOverflow.Clip)
            {
                label.style.textOverflow = textOverflow;
            }
            if (noWrap)
            {
                label.style.whiteSpace = WhiteSpace.NoWrap;
            }
            return label;
        }

        protected static Label CreateCellLabel(float width, bool bold = false, float marginRight = 0f, bool flexShrink = false, TextOverflow textOverflow = TextOverflow.Clip, bool noWrap = false)
        {
            return CreateCellLabel(null, width, bold, marginRight, flexShrink, textOverflow, noWrap);
        }

        protected static VisualElement CreatePane()
        {
            return new VisualElement
            {
                style =
                {
                    flexGrow = 1f,
                    flexDirection = FlexDirection.Column,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    minWidth = 0f,
                    overflow = Overflow.Hidden,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f)
                }
            };
        }

        protected static Label CreatePaneTitle(string text, float? height = null, float? marginBottom = null)
        {
            Label label = new(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.86f, 0.86f, 0.86f);
            if (height.HasValue)
            {
                label.style.height = height.Value;
            }
            if (marginBottom.HasValue)
            {
                label.style.marginBottom = marginBottom.Value;
            }
            return label;
        }

        protected static VisualElement CreateSection(string title, float marginBottom = 14f)
        {
            VisualElement section = new()
            {
                style =
                {
                    marginBottom = marginBottom,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    paddingTop = 8f,
                    paddingBottom = 8f,
                    backgroundColor = new Color(0.11f, 0.11f, 0.11f, 0.50f)
                }
            };

            Label titleLabel = new(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 6f;
            section.Add(titleLabel);
            return section;
        }

        protected static VisualElement CreateInfoRow(string labelText, string valueText, float labelWidth = 110f, float marginBottom = 0f)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    minHeight = 22f,
                    alignItems = Align.Center,
                    marginBottom = marginBottom
                }
            };

            Label label = new(labelText);
            label.style.width = labelWidth;
            label.style.color = new Color(0.72f, 0.72f, 0.72f);
            row.Add(label);

            Label value = new(valueText);
            value.style.flexGrow = 1f;
            value.style.whiteSpace = WhiteSpace.Normal;
            row.Add(value);
            return row;
        }

        protected static DropdownField CreateToolbarDropdown(VisualElement toolbar, string labelText, List<string> options, float width)
        {
            VisualElement group = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 0f,
                    marginLeft = 8f
                }
            };

            Label label = new(labelText);
            label.style.marginRight = 4f;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            group.Add(label);

            DropdownField dropdown = new(options, 0);
            dropdown.style.width = width;
            dropdown.style.flexShrink = 0f;
            group.Add(dropdown);

            toolbar.Add(group);
            return dropdown;
        }

        protected static Label CreateMutedLabel(string text, bool wrap = false, float marginTop = 0f, Color? color = null)
        {
            Label label = new(text);
            label.style.color = color ?? new Color(0.72f, 0.72f, 0.72f);
            if (wrap)
            {
                label.style.whiteSpace = WhiteSpace.Normal;
            }
            if (marginTop > 0f)
            {
                label.style.marginTop = marginTop;
            }
            return label;
        }

    }
}
