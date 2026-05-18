using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    /// <summary>
    /// 所有 UI Toolkit 运行时面板的基类。
    /// 继承 PanelBase 以复用 UIManager 的面板管理流程，
    /// 但 UI 渲染走 UIDocument + VisualElement 而非 UGUI。
    /// 子类通过 UXML 定义布局、USS 定义样式、C# 绑定逻辑。
    /// </summary>
    public abstract class UIToolkitPanelBase : PanelBase
    {
        private UIDocument m_UIDocument;
#if UNITY_EDITOR
        private VisualElement m_EditorPreviewRoot;
#endif

        /// <summary>
        /// 视觉树根节点
        /// </summary>
        protected VisualElement Root
        {
            get
            {
#if UNITY_EDITOR
                return m_EditorPreviewRoot ?? m_UIDocument?.rootVisualElement;
#else
                return m_UIDocument?.rootVisualElement;
#endif
            }
        }

        protected override void OnInit()
        {
            m_UIDocument = GetComponent<UIDocument>();
            if (m_UIDocument == null)
            {
                m_UIDocument = gameObject.AddComponent<UIDocument>();
            }

            // PanelSettings
            if (m_UIDocument.panelSettings == null)
            {
                if (XApplication.Setting.defaultUIToolkitPanelSettings != null)
                {
                    m_UIDocument.panelSettings = XApplication.Setting.defaultUIToolkitPanelSettings;
                }
                else
                {
                    Debug.LogWarning($"[UIToolkitPanelBase] PanelSettings not found for {PanelName}.");
                }
            }

            LoadStyleSheet();
            LoadVisualTree();

            // 绑定 UI 元素引用
            BindUI();

            // 初始隐藏
            SetVisible(false);
        }

        /// <summary>
        /// UXML 加载完成后调用，子类通过 Root.Q() 获取元素引用并绑定事件。
        /// </summary>
        protected virtual void BindUI() { }

        private void LoadStyleSheet()
        {
            string ussPath = GetSiblingAssetPath(".uss");
            if (string.IsNullOrEmpty(ussPath) || !Resource.ResourceManager.Instance.IsAssetExist(ussPath))
            {
                return;
            }

            StyleSheet styleSheet = Resource.ResourceManager.Instance.Load<StyleSheet>(ussPath);
            if (styleSheet != null && Root != null && !Root.styleSheets.Contains(styleSheet))
            {
                Root.styleSheets.Add(styleSheet);
            }
        }

        private void LoadVisualTree()
        {
            string uxmlPath = GetSiblingAssetPath(".uxml");
            if (string.IsNullOrEmpty(uxmlPath) || !Resource.ResourceManager.Instance.IsAssetExist(uxmlPath))
            {
                return;
            }

            var visualTree = Resource.ResourceManager.Instance.Load<VisualTreeAsset>(uxmlPath);
            if (visualTree != null && Root != null)
            {
                Root.Clear();
                visualTree.CloneTree(Root);
            }
            else
            {
                Debug.LogError($"[UIToolkitPanelBase] Failed to load UXML: {uxmlPath}");
            }
        }

        private string GetSiblingAssetPath(string extension)
        {
            if (string.IsNullOrEmpty(PanelPath))
            {
                return string.Empty;
            }

            return Path.ChangeExtension(PanelPath, extension);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器预览构建入口。仅生成 VisualElement 树，不走 UIManager 面板生命周期。
        /// </summary>
        public void EditorBuildPreview(VisualElement previewRoot)
        {
            if (previewRoot == null)
            {
                throw new ArgumentNullException(nameof(previewRoot));
            }

            VisualElement previousRoot = m_EditorPreviewRoot;
            m_EditorPreviewRoot = previewRoot;
            try
            {
                CloneUxmlForEditorPreview(previewRoot);
                BindUI();
            }
            finally
            {
                m_EditorPreviewRoot = previousRoot;
            }
        }

        private void CloneUxmlForEditorPreview(VisualElement previewRoot)
        {
            string uxmlPath = GetSiblingAssetPath(".uxml");
            if (string.IsNullOrEmpty(uxmlPath))
            {
                return;
            }

            previewRoot.Clear();
            string ussPath = GetSiblingAssetPath(".uss");
            StyleSheet styleSheet = string.IsNullOrEmpty(ussPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            if (styleSheet != null)
            {
                previewRoot.styleSheets.Add(styleSheet);
            }

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[UIToolkitPanelBase] Failed to load preview UXML: {uxmlPath}");
                return;
            }

            visualTree.CloneTree(previewRoot);
        }
#endif

        /// <summary>
        /// 通过 VisualElement.style.display 控制显隐
        /// </summary>
        public override void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public override bool IsVisible
        {
            get
            {
                if (Root == null) return false;
                return Root.resolvedStyle.display != DisplayStyle.None;
            }
        }
    }
}
