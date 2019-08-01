using System;
using UnityEditor;
using UnityEngine;

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
            Mainfest2Json,
        }

        [MenuItem("XFramework/Resource/AssetBundleWindow")]
        static void OpenWindow()
        {
            var window = GetWindow(typeof(AssetBundleEditor));
            window.Show();
            window.minSize = new Vector2(400, 100);
        }

        private TabMode m_TabMode;

        public SubWindow[] m_SubWindows = new SubWindow[]
        {
            new DefaultTab(),
            new DependenceTab(),
            new Mainfest2Json(),
        };

        private void OnEnable()
        {
            foreach (var item in m_SubWindows)
            {
                item.OnEnable();
            }
        } 

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    m_TabMode = (TabMode)GUILayout.Toolbar((int)m_TabMode, Enum.GetNames(typeof(TabMode)));
                }

                GUILayout.Space(10);
                m_SubWindows[(int)m_TabMode].OnGUI();
            }
        } 

        private void OnDisable()
        {
            foreach (var item in m_SubWindows)
            {
                item.OnDisable();
            }
        }
    }
}