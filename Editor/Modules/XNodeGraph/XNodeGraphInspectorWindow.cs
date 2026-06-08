using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.UI;

namespace XFramework.NodeKit.Editor
{
    internal sealed class XNodeGraphInspectorWindow : EditorWindow
    {
        private XNodeGraphEditorWindow m_Owner;
        private IXNode m_RuntimeNode;

        public static XNodeGraphInspectorWindow ShowWindow(XNodeGraphEditorWindow owner, IXNode runtimeNode)
        {
            XNodeGraphInspectorWindow window = GetOpenWindow() ?? CreateDockedWindow();
            window.minSize = new Vector2(360f, 520f);
            window.SetOwner(owner, runtimeNode);
            window.Show();
            window.Repaint();
            return window;
        }

        internal static XNodeGraphInspectorWindow GetOpenWindow()
        {
            return HasOpenInstances<XNodeGraphInspectorWindow>()
                ? GetWindow<XNodeGraphInspectorWindow>()
                : null;
        }

        public void CreateGUI()
        {
            BuildUI();
        }

        internal void RefreshFromOwner()
        {
            BuildUI();
        }

        internal bool IsOwnedBy(XNodeGraphEditorWindow owner)
        {
            return m_Owner == owner;
        }

        private void SetOwner(XNodeGraphEditorWindow owner, IXNode runtimeNode)
        {
            m_Owner = owner;
            m_RuntimeNode = runtimeNode;
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

            if (m_Owner == null || m_RuntimeNode == null)
            {
                titleContent = new GUIContent("Node Inspector");
                root.Add(new Label("当前没有选中的节点。"));
                return;
            }

            titleContent = new GUIContent($"Node Inspector");

            Label titleLabel = new(GetNodeTitle(m_RuntimeNode));
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 14f;
            titleLabel.style.marginBottom = 8f;
            root.Add(titleLabel);

            Label typeLabel = new(m_RuntimeNode.GetType().Name);
            typeLabel.style.marginBottom = 8f;
            typeLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            root.Add(typeLabel);

            ScrollView scrollView = new();
            scrollView.style.flexGrow = 1f;
            root.Add(scrollView);

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
            inspector.Bind(m_RuntimeNode);
            inspector.ExpandFirstLevelElements();
            NormalizeInspectorLayout(inspector);
            scrollView.Add(inspector);
        }

        internal void ClearSelection()
        {
            m_RuntimeNode = null;
            BuildUI();
        }

        private static XNodeGraphInspectorWindow CreateDockedWindow()
        {
            Type inspectorWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorWindowType != null)
            {
                return CreateWindow<XNodeGraphInspectorWindow>("Node Inspector", inspectorWindowType);
            }

            return CreateWindow<XNodeGraphInspectorWindow>("Node Inspector");
        }

        private static string GetNodeTitle(IXNode runtimeNode)
        {
            if (runtimeNode == null)
            {
                return "Node";
            }

            string typeName = runtimeNode.GetType().Name;
            return string.IsNullOrEmpty(runtimeNode.name) ? typeName : $"{runtimeNode.name} ({typeName})";
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
    }
}
