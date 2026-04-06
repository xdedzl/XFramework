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

        /// <summary>
        /// 视觉树根节点
        /// </summary>
        protected VisualElement Root => m_UIDocument?.rootVisualElement;

        /// <summary>
        /// UXML VisualTreeAsset 的资源路径，子类必须提供
        /// </summary>
        protected abstract string UxmlPath { get; }

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
                var settings = Resources.Load<PanelSettings>("DefaultPanelSettings");
                if (settings == null)
                {
                    settings = Resource.ResourceManager.Instance.Load<PanelSettings>(
                        "Assets/ABRes/UI/DefaultPanelSettings.asset");
                }

                if (settings != null)
                {
                    m_UIDocument.panelSettings = settings;
                }
                else
                {
                    Debug.LogWarning($"[UIToolkitPanelBase] PanelSettings not found for {PanelName}.");
                }
            }

            // 加载 UXML 布局
            if (!string.IsNullOrEmpty(UxmlPath))
            {
                var visualTree = Resource.ResourceManager.Instance.Load<VisualTreeAsset>(UxmlPath);
                if (visualTree != null)
                {
                    // 如果 UIDocument 没有设置 sourceAsset，则通过 CloneTree 加载
                    if (m_UIDocument.visualTreeAsset == null)
                    {
                        m_UIDocument.visualTreeAsset = visualTree;
                    }
                }
                else
                {
                    Debug.LogError($"[UIToolkitPanelBase] Failed to load UXML: {UxmlPath}");
                }
            }

            // 绑定 UI 元素引用
            BindUI();

            // 初始隐藏
            SetVisible(false);
        }

        /// <summary>
        /// UXML 加载完成后调用，子类通过 Root.Q() 获取元素引用并绑定事件。
        /// </summary>
        protected virtual void BindUI() { }

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
