using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.UI;

namespace XFramework.Editor
{
    public sealed class XFrameworkInspectorWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Debug/X Inspector";
        private const string WindowTitle = "X Inspector";

        private object m_Source;
        private string m_Title;
        private string m_Subtitle;
        private object m_Target;
        private Action<VisualElement> m_CustomBuilder;
        private bool m_ExpandFirstLevel;
        private ContentMode m_ContentMode;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XFrameworkInspectorWindow window = OpenOrCreateWindow();
            window.Show();
            window.Focus();
        }

        public static void InspectObject(
            object source,
            string title,
            object target,
            string subtitle = null,
            bool expandFirstLevel = true)
        {
            XFrameworkInspectorWindow window = OpenOrCreateWindow();
            window.m_Source = source;
            window.m_Title = title;
            window.m_Subtitle = subtitle;
            window.m_Target = target;
            window.m_CustomBuilder = null;
            window.m_ExpandFirstLevel = expandFirstLevel;
            window.m_ContentMode = ContentMode.Object;
            window.BuildUI();
            window.Show();
            window.Focus();
        }

        public static void InspectCustom(
            object source,
            string title,
            Action<VisualElement> buildContent,
            string subtitle = null)
        {
            XFrameworkInspectorWindow window = OpenOrCreateWindow();
            window.m_Source = source;
            window.m_Title = title;
            window.m_Subtitle = subtitle;
            window.m_Target = null;
            window.m_CustomBuilder = buildContent;
            window.m_ExpandFirstLevel = false;
            window.m_ContentMode = ContentMode.Custom;
            window.BuildUI();
            window.Show();
            window.Focus();
        }

        public static void ClearIfOwner(object source)
        {
            XFrameworkInspectorWindow window = GetOpenWindow();
            if (window == null || !window.IsSource(source))
            {
                return;
            }

            window.ClearCurrent();
        }

        public static bool IsOwnedBy(object source)
        {
            return GetOpenWindow()?.IsSource(source) ?? false;
        }

        public static void RefreshIfOwner(object source)
        {
            XFrameworkInspectorWindow window = GetOpenWindow();
            if (window == null || !window.IsSource(source))
            {
                return;
            }

            window.BuildUI(true);
        }

        public static void RefreshCurrent()
        {
            GetOpenWindow()?.BuildUI(true);
        }

        public void CreateGUI()
        {
            BuildUI();
        }

        private static XFrameworkInspectorWindow OpenOrCreateWindow()
        {
            return GetOpenWindow() ?? CreateDockedWindow();
        }

        private static XFrameworkInspectorWindow GetOpenWindow()
        {
            XFrameworkInspectorWindow[] windows = Resources.FindObjectsOfTypeAll<XFrameworkInspectorWindow>();
            return windows != null && windows.Length > 0 ? windows[0] : null;
        }

        private static XFrameworkInspectorWindow CreateDockedWindow()
        {
            Type inspectorWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            if (inspectorWindowType != null)
            {
                return CreateWindow<XFrameworkInspectorWindow>(WindowTitle, inspectorWindowType);
            }

            return CreateWindow<XFrameworkInspectorWindow>(WindowTitle);
        }

        private void ClearCurrent()
        {
            m_Source = null;
            m_Title = null;
            m_Subtitle = null;
            m_Target = null;
            m_CustomBuilder = null;
            m_ExpandFirstLevel = false;
            m_ContentMode = ContentMode.Empty;
            BuildUI();
        }

        private bool IsSource(object source)
        {
            return ReferenceEquals(m_Source, source);
        }

        private void BuildUI(bool preserveScrollOffset = false)
        {
            titleContent = new GUIContent(WindowTitle);

            VisualElement root = rootVisualElement;
            Vector2 scrollOffset = preserveScrollOffset ? GetCurrentScrollOffset(root) : Vector2.zero;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            if (m_ContentMode == ContentMode.Empty)
            {
                root.Add(CreateEmptyState());
                return;
            }

            root.Add(CreateHeader());

            ScrollView scrollView = new();
            scrollView.style.flexGrow = 1;
            scrollView.style.marginTop = 8;
            root.Add(scrollView);

            switch (m_ContentMode)
            {
                case ContentMode.Object:
                    BuildObjectContent(scrollView);
                    break;
                case ContentMode.Custom:
                    BuildCustomContent(scrollView);
                    break;
                default:
                    scrollView.Add(CreateEmptyState());
                    break;
            }

            if (preserveScrollOffset)
            {
                RestoreScrollOffset(scrollView, scrollOffset);
            }
        }

        private static Vector2 GetCurrentScrollOffset(VisualElement root)
        {
            ScrollView scrollView = root?.Q<ScrollView>();
            return scrollView != null ? scrollView.scrollOffset : Vector2.zero;
        }

        private static void RestoreScrollOffset(ScrollView scrollView, Vector2 scrollOffset)
        {
            scrollView.scrollOffset = scrollOffset;
            scrollView.schedule.Execute(() => scrollView.scrollOffset = scrollOffset);
        }

        private VisualElement CreateHeader()
        {
            VisualElement header = new()
            {
                style =
                {
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 7,
                    paddingBottom = 7,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.86f)
                }
            };

            Label title = new(string.IsNullOrWhiteSpace(m_Title) ? WindowTitle : m_Title);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.whiteSpace = WhiteSpace.Normal;
            header.Add(title);

            if (!string.IsNullOrWhiteSpace(m_Subtitle))
            {
                Label subtitle = new(m_Subtitle);
                subtitle.style.marginTop = 3;
                subtitle.style.whiteSpace = WhiteSpace.Normal;
                subtitle.style.color = new Color(0.72f, 0.72f, 0.72f);
                header.Add(subtitle);
            }

            return header;
        }

        private static VisualElement CreateEmptyState()
        {
            VisualElement container = new()
            {
                style =
                {
                    flexGrow = 1,
                    justifyContent = Justify.Center
                }
            };

            Label label = new("未选择对象");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = new Color(0.72f, 0.72f, 0.72f);
            label.style.marginTop = 80;
            container.Add(label);
            return container;
        }

        private void BuildObjectContent(VisualElement parent)
        {
            if (m_Target == null)
            {
                parent.Add(CreateMessage("目标对象为空。", MessageType.Warning));
                return;
            }

            if (m_Target is not IDataContainer && m_Target.GetType().IsValueType)
            {
                parent.Add(CreateMessage("不能直接绑定值类型对象，请先使用 StructContainer 或其它引用类型容器包装。", MessageType.Error));
                return;
            }

            try
            {
                XInspector inspector = new(false);
                inspector.style.flexGrow = 1;
                inspector.style.alignSelf = Align.Stretch;
                inspector.style.width = Length.Percent(100);
                inspector.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.18f));
                inspector.style.paddingLeft = 10;
                inspector.style.paddingRight = 10;
                inspector.style.paddingTop = 8;
                inspector.style.paddingBottom = 8;
                inspector.style.borderTopLeftRadius = 4;
                inspector.style.borderTopRightRadius = 4;
                inspector.style.borderBottomLeftRadius = 4;
                inspector.style.borderBottomRightRadius = 4;
                inspector.Bind(m_Target);
                if (m_ExpandFirstLevel)
                {
                    inspector.ExpandFirstLevelElements();
                }

                parent.Add(inspector);
            }
            catch (Exception exception)
            {
                parent.Add(CreateExceptionContent("构建对象 Inspector 失败", exception));
            }
        }

        private void BuildCustomContent(VisualElement parent)
        {
            if (m_CustomBuilder == null)
            {
                parent.Add(CreateMessage("自定义详情构建函数为空。", MessageType.Warning));
                return;
            }

            try
            {
                m_CustomBuilder(parent);
            }
            catch (Exception exception)
            {
                parent.Add(CreateExceptionContent("构建自定义 Inspector 失败", exception));
            }
        }

        private static VisualElement CreateMessage(string text, MessageType type)
        {
            HelpBox helpBox = new(text, type == MessageType.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning);
            helpBox.style.marginTop = 4;
            return helpBox;
        }

        private static VisualElement CreateExceptionContent(string title, Exception exception)
        {
            VisualElement container = new()
            {
                style =
                {
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = new Color(0.32f, 0.12f, 0.10f, 0.55f)
                }
            };

            Label titleLabel = new(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(1f, 0.72f, 0.64f);
            titleLabel.style.marginBottom = 6;
            container.Add(titleLabel);

            Label messageLabel = new(exception.ToString());
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageLabel.style.color = new Color(0.96f, 0.82f, 0.78f);
            container.Add(messageLabel);
            return container;
        }

        private enum ContentMode
        {
            Empty,
            Object,
            Custom
        }
    }
}
