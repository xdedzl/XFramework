using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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
            scrollView.Add(content);

            foreach (XDataTableEditorColumn column in model.Columns)
            {
                content.Add(m_Owner.BuildDetailField(m_RowIndex, column));
            }
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
    }
}
