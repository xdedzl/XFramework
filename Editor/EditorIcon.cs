using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    /// <summary>
    /// 编辑器图标资源
    /// </summary>
    public static class EditorIcon
    {
        private static Texture2D m_Setting;
        private static Texture2D m_Refresh;
        private static Texture2D m_Folder;
        private static Texture2D m_Trash;
        private static Texture2D m_Duplicate;
        private static Texture2D m_Plus;


        public static Texture2D Setting
        {
            get
            {
                if (!m_Setting)
                {
                    m_Setting = EditorGUIUtility.FindTexture("SettingsIcon");
                }
                return m_Setting;
            }
        }
        public static Texture2D Refresh
        {
            get
            {
                if (!m_Refresh)
                {
                    m_Refresh = EditorGUIUtility.FindTexture("Refresh");
                }
                return m_Refresh;
            }
        }

        public static Texture2D Trash
        {
            get
            {
                if (!m_Trash)
                {
                    m_Trash = EditorGUIUtility.FindTexture("TreeEditor.Trash");
                }
                return m_Trash;
            }
        }

        public static Texture2D Folder
        {
            get
            {
                if (!m_Folder)
                {
                    m_Folder = EditorGUIUtility.FindTexture("Project");
                }
                return m_Folder;
            }
        }

        public static Texture2D Duplicate
        {
            get
            {
                if (!m_Duplicate)
                {
                    m_Duplicate = EditorGUIUtility.FindTexture("Duplicate");
                }
                return m_Duplicate;
            }
        }

        public static Texture2D Plus
        {
            get
            {
                if (!m_Plus)
                {
                    m_Plus = EditorGUIUtility.FindTexture("Toolbar Plus");
                }
                return m_Plus;
            }
        }
    }
}