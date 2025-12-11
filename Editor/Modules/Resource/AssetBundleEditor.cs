using System;
using System.Collections.Generic;
using System.IO;
using XFramework.Resource;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace XFramework.Editor
{
    /// <summary>
    /// AssetBundle窗口
    /// </summary>
    public partial class AssetBundleEditor : EditorWindow
    {
        private enum TabMode
        {
            Default,
            Dependence,
            Manifest2Json,
            BuildProject,
        }

        [MenuItem("XFramework/Resource/AssetBundleWindow")]
        static void OpenWindow()
        {
            var window = GetWindow(typeof(AssetBundleEditor)) as AssetBundleEditor;
            window.titleContent = new GUIContent("AssetBundle");
            window.Show();
            window.minSize = new Vector2(400, 100);
        }

        private TabMode m_TabMode;

        public SubWindow[] m_SubWindows = new SubWindow[]
        {
            new DefaultTab(),
            new DependenceTab(),
            new Manifest2Json(),
            new BuildTab(),
        };

        // Root content container for current tab (UIElements)
        private VisualElement _contentRoot;
        // IMGUI containers to bridge existing tab content (keeps code working while using UIElements shell)
        private IMGUIContainer[] _imguiContainers;
        private VisualElement[] _veTabContents;
        // Toolbar with tab buttons
        private Toolbar _toolbar;

        private void OnEnable()
        {
            m_TabMode = (TabMode)EditorPrefs.GetInt("TabMode", (int)TabMode.BuildProject);

            foreach (var item in m_SubWindows)
            {
                item.OnEnable();
            }
        }

        // Build UIElements-based GUI (no UXML/USS)
        public void CreateGUI()
        {
            // Ensure rootVisualElement is clean
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.paddingLeft = 6;
            rootVisualElement.style.paddingRight = 6;
            rootVisualElement.style.paddingTop = 6;
            rootVisualElement.style.paddingBottom = 6;

            // Toolbar for tabs
            _toolbar = new Toolbar();
            _toolbar.style.flexShrink = 0;

            var tabNames = Enum.GetNames(typeof(TabMode));
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIndex = i;
                var button = new ToolbarButton(() => SwitchTab((TabMode)tabIndex))
                {
                    text = tabNames[i]
                };
                _toolbar.Add(button);
            }
            rootVisualElement.Add(_toolbar);

            // Content area fills remaining space
            _contentRoot = new VisualElement();
            _contentRoot.style.flexGrow = 1;
            _contentRoot.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_contentRoot);

            // Create containers for each tab
            _imguiContainers = new IMGUIContainer[m_SubWindows.Length];
            _veTabContents = new VisualElement[m_SubWindows.Length];
            for (int i = 0; i < m_SubWindows.Length; i++)
            {
                int idx = i;
                // Default tab uses UIElements view if available
                if (idx == (int)TabMode.Default && m_SubWindows[idx] is DefaultTab defaultTab)
                {
                    var ve = defaultTab.BuildUI();
                    ve.style.flexGrow = 1;
                    ve.style.display = DisplayStyle.None;
                    _veTabContents[idx] = ve;
                    _contentRoot.Add(ve);
                    continue;
                }
                
                if (idx == (int)TabMode.BuildProject && m_SubWindows[idx] is BuildTab buildTab)
                {
                    var ve = buildTab.BuildUI();
                    ve.style.flexGrow = 1;
                    ve.style.display = DisplayStyle.None;
                    _veTabContents[idx] = ve;
                    _contentRoot.Add(ve);
                    continue;
                }

                var container = new IMGUIContainer(() =>
                {
                    GUILayout.Space(4);
                    m_SubWindows[idx].OnGUI();
                });
                container.style.flexGrow = 1;
                container.style.display = DisplayStyle.None; // hidden by default
                _imguiContainers[idx] = container;
                _contentRoot.Add(container);
            }

            // Show the initially selected tab
            SwitchTab(m_TabMode);
        }

        private void SwitchTab(TabMode mode)
        {
            m_TabMode = mode;
            // Update visibility of content containers
            for (int i = 0; i < m_SubWindows.Length; i++)
            {
                var show = i == (int)m_TabMode;
                if (_veTabContents != null && _veTabContents[i] != null)
                    _veTabContents[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (_imguiContainers != null && _imguiContainers[i] != null)
                    _imguiContainers[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
            // Persist selection
            EditorPrefs.SetInt("TabMode", (int)m_TabMode);
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt("TabMode", (int)m_TabMode);

            foreach (var item in m_SubWindows)
            {
                item.OnDisable();
            }
        }
    }
}