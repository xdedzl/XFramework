using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    public sealed class PrefabTabsWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/Prefab Tabs";
        private const string WindowTitle = "Prefab Tabs";

        private PrefabTabsView m_View;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            PrefabTabsWindow window = GetWindow<PrefabTabsWindow>();
            window.SetWindowTitle();
            window.minSize = new Vector2(420f, 160f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            SetWindowTitle();
        }

        private void OnDisable()
        {
            m_View?.Dispose();
            m_View = null;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            m_View?.Dispose();
            m_View = new PrefabTabsView(false);
            rootVisualElement.Add(m_View.Root);
        }

        private void SetWindowTitle()
        {
            titleContent = new GUIContent(WindowTitle);
        }
    }
}
