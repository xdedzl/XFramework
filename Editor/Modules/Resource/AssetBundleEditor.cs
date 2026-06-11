using System;
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
            BuildProject,
            Preview,
        }

        [MenuItem("XFramework/Build/单独打AB")]
        private static void OpenAssetBundleWindow()
        {
            OpenWindow(TabMode.Default);
        }

        [MenuItem("XFramework/Build/打包项目")]
        private static void OpenBuildProjectWindow()
        {
            OpenWindow(TabMode.BuildProject);
        }

        private static void OpenWindow(TabMode tabMode)
        {
            var window = GetWindow<AssetBundleEditor>();
            window.titleContent = new GUIContent("AssetBundle");
            window.Show();
            window.minSize = new Vector2(400, 100);
            window.SwitchTabWhenReady(tabMode);
        }

        private TabMode m_TabMode;

        public SubWindow[] m_SubWindows = new SubWindow[]
        {
            new DefaultTab(),
            new BuildTab(),
            new PreviewTab(),
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
            int savedTabMode = EditorPrefs.GetInt("TabMode", (int)TabMode.BuildProject);
            m_TabMode = Enum.IsDefined(typeof(TabMode), savedTabMode) && savedTabMode < m_SubWindows.Length
                ? (TabMode)savedTabMode
                : TabMode.BuildProject;

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
                    text = GetTabDisplayName((TabMode)tabIndex)
                };
                _toolbar.Add(button);
            }
            rootVisualElement.Add(_toolbar);

            // Content area fills remaining space
            _contentRoot = new VisualElement();
            _contentRoot.style.flexGrow = 1;
            _contentRoot.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(_contentRoot);

            // Create tab containers lazily so expensive editor tabs do not refresh until selected.
            _imguiContainers = new IMGUIContainer[m_SubWindows.Length];
            _veTabContents = new VisualElement[m_SubWindows.Length];

            // Show the initially selected tab
            SwitchTab(m_TabMode);
        }

        private static string GetTabDisplayName(TabMode mode)
        {
            return mode switch
            {
                TabMode.Default => "AB包配置",
                TabMode.Preview => "AB包预览",
                _ => mode.ToString()
            };
        }

        private void SwitchTab(TabMode mode)
        {
            m_TabMode = mode;
            EnsureTabContent((int)m_TabMode);

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

        private void SwitchTabWhenReady(TabMode mode)
        {
            m_TabMode = mode;
            EditorPrefs.SetInt("TabMode", (int)m_TabMode);

            if (_contentRoot != null && m_SubWindows != null && (int)mode < m_SubWindows.Length)
            {
                SwitchTab(mode);
            }
        }

        private void EnsureTabContent(int index)
        {
            if (_veTabContents[index] != null || _imguiContainers[index] != null)
            {
                return;
            }

            var ve = m_SubWindows[index].BuildUI();
            if (ve != null)
            {
                ve.style.flexGrow = 1;
                ve.style.display = DisplayStyle.None;
                _veTabContents[index] = ve;
                _contentRoot.Add(ve);
                return;
            }

            var tabIndex = index;
            var container = new IMGUIContainer(() =>
            {
                GUILayout.Space(4);
                m_SubWindows[tabIndex].OnGUI();
            });
            container.style.flexGrow = 1;
            container.style.display = DisplayStyle.None;
            _imguiContainers[index] = container;
            _contentRoot.Add(container);
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
