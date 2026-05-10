using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.UI;

namespace XFramework.Editor
{
    public sealed class XDataTableRowDetailWindow : EditorWindow
    {
        private XDataTableEditorWindow m_Owner;
        private int m_RowIndex = -1;

        public static XDataTableRowDetailWindow ShowWindow(XDataTableEditorWindow owner, int rowIndex)
        {
            XDataTableRowDetailWindow window = GetOpenWindow() ?? CreateDockedWindow();
            window.minSize = new Vector2(420f, 620f);
            window.SetOwner(owner, rowIndex);
            window.Show();
            window.Repaint();
            return window;
        }

        internal static XDataTableRowDetailWindow GetOpenWindow()
        {
            return HasOpenInstances<XDataTableRowDetailWindow>()
                ? GetWindow<XDataTableRowDetailWindow>()
                : null;
        }

        public void CreateGUI()
        {
            BuildUI();
        }

        internal void RefreshFromOwner()
        {
            if (m_Owner == null || !m_Owner.HasSelectedRow)
            {
                Close();
                return;
            }

            m_RowIndex = m_Owner.SelectedRowIndex;
            BuildUI();
        }

        private void SetOwner(XDataTableEditorWindow owner, int rowIndex)
        {
            m_Owner = owner;
            m_RowIndex = rowIndex;
            BuildUI();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            if (m_Owner == null || m_Owner.Model == null || !m_Owner.HasSelectedRow)
            {
                titleContent = new GUIContent("Data Row Detail");
                root.Add(new Label("当前没有可显示的数据行。"));
                return;
            }

            m_RowIndex = m_Owner.SelectedRowIndex;
            XDataTableEditorModel model = m_Owner.Model;
            titleContent = new GUIContent($"{model.TableType.Name} Row {m_RowIndex + 1}");

            Label titleLabel = new($"行 #{m_RowIndex + 1}");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 14f;
            titleLabel.style.marginBottom = 8f;
            root.Add(titleLabel);

            Label capabilityLabel = new($"能力: {model.CapabilitySummary}");
            capabilityLabel.style.marginBottom = 8f;
            capabilityLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            root.Add(capabilityLabel);

            ScrollView scrollView = new();
            scrollView.style.flexGrow = 1f;
            root.Add(scrollView);

            VisualElement content = new();
            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 1f;
            content.style.alignItems = Align.Stretch;
            content.style.width = Length.Percent(100);
            scrollView.Add(content);

            VisualElement inspectorHost = new();
            inspectorHost.style.flexGrow = 1f;
            inspectorHost.style.alignSelf = Align.Stretch;
            inspectorHost.style.width = Length.Percent(100);
            content.Add(inspectorHost);

            if (!TryCreateRowBinding(model, m_RowIndex, out object bindingTarget))
            {
                Label errorLabel = new("当前数据行无法绑定到 XInspector。");
                errorLabel.style.whiteSpace = WhiteSpace.Normal;
                errorLabel.style.color = new Color(0.9f, 0.45f, 0.45f);
                inspectorHost.Add(errorLabel);
                return;
            }

            XInspector inspector = new(false);
            inspector.style.flexGrow = 1f;
            inspector.style.alignSelf = Align.Stretch;
            inspector.style.width = Length.Percent(100);
            inspector.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.18f));
            inspector.style.paddingLeft = 10f;
            inspector.style.paddingRight = 10f;
            inspector.style.paddingTop = 8f;
            inspector.style.paddingBottom = 8f;
            inspector.style.borderTopLeftRadius = 4f;
            inspector.style.borderTopRightRadius = 4f;
            inspector.style.borderBottomLeftRadius = 4f;
            inspector.style.borderBottomRightRadius = 4f;
            inspector.Bind(bindingTarget);
            inspector.ExpandFirstLevelElements();
            NormalizeInspectorLayout(inspector);
            inspectorHost.Add(inspector);
        }

        internal bool IsOwnedBy(XDataTableEditorWindow owner)
        {
            return m_Owner == owner;
        }

        private static XDataTableRowDetailWindow CreateDockedWindow()
        {
            Type inspectorWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorWindowType != null)
            {
                return CreateWindow<XDataTableRowDetailWindow>("Data Row Detail", inspectorWindowType);
            }

            return CreateWindow<XDataTableRowDetailWindow>("Data Row Detail");
        }

        private bool TryCreateRowBinding(XDataTableEditorModel model, int rowIndex, out object bindingTarget)
        {
            bindingTarget = null;
            if (model == null || m_Owner == null || rowIndex < 0 || rowIndex >= model.Rows.Count)
            {
                return false;
            }

            Type binderType = typeof(RowBindingContainer<>).MakeGenericType(model.DataType);
            bindingTarget = Activator.CreateInstance(binderType, m_Owner, rowIndex, model.Rows[rowIndex]);
            return bindingTarget != null;
        }

        private static void NormalizeInspectorLayout(XInspector inspector)
        {
            if (inspector == null)
            {
                return;
            }

            inspector.style.alignItems = Align.Stretch;

            foreach (VisualElement element in inspector.Query<VisualElement>().ToList())
            {
                if (element.ClassListContains("inspector-element"))
                {
                    element.style.width = Length.Percent(100);
                    element.style.alignSelf = Align.Stretch;
                }

                if (element.ClassListContains("inspector-label"))
                {
                    element.style.width = Length.Percent(40);
                    element.style.minWidth = 120f;
                    element.style.flexShrink = 0f;
                    element.style.whiteSpace = WhiteSpace.NoWrap;
                    element.style.paddingLeft = 5f;
                }

                if (element.ClassListContains("inspector-input"))
                {
                    element.style.width = Length.Percent(60);
                    element.style.minWidth = 160f;
                    element.style.flexGrow = 1f;
                }
            }
        }

        private sealed class RowBindingContainer<T> : IDataContainer
        {
            private readonly XDataTableEditorWindow m_Owner;
            private readonly int m_RowIndex;
            private T m_Data;

            public RowBindingContainer(XDataTableEditorWindow owner, int rowIndex, T value)
            {
                m_Owner = owner;
                m_RowIndex = rowIndex;
                m_Data = value;
            }

            public T data
            {
                get => m_Data;
                set
                {
                    m_Data = value;
                    m_Owner?.ApplyRowValueChangeFromDetail(m_RowIndex, value);
                }
            }

            public object Data => data;
        }
    }
}
