using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    internal sealed class PrefabTabsView : IDisposable
    {
        private readonly bool m_Compact;
        private readonly VisualElement m_TabContainer;
        private readonly Label m_StatusLabel;

        public PrefabTabsView(bool compact)
        {
            m_Compact = compact;

            Root = new VisualElement();
            Root.style.flexGrow = 1;
            Root.style.flexDirection = compact ? FlexDirection.Row : FlexDirection.Column;
            Root.style.alignItems = compact ? Align.Center : Align.Stretch;
            Root.style.paddingLeft = compact ? 2 : 6;
            Root.style.paddingRight = compact ? 2 : 6;
            Root.style.paddingTop = compact ? 2 : 6;
            Root.style.paddingBottom = compact ? 2 : 6;

            Root.Add(BuildToolbar());

            ScrollView scrollView = new ScrollView(compact ? ScrollViewMode.Horizontal : ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.marginTop = compact ? 0 : 6;
            scrollView.style.marginLeft = compact ? 4 : 0;
            scrollView.contentContainer.style.flexDirection = compact ? FlexDirection.Row : FlexDirection.Column;
            m_TabContainer = scrollView;
            Root.Add(scrollView);

            m_StatusLabel = new Label();
            m_StatusLabel.style.marginTop = 6;
            m_StatusLabel.style.color = new Color(0.72f, 0.72f, 0.72f);
            if (!compact)
            {
                Root.Add(m_StatusLabel);
            }

            PrefabTabsStore.Changed += Refresh;
            Refresh();
        }

        public VisualElement Root { get; }

        public void Dispose()
        {
            PrefabTabsStore.Changed -= Refresh;
        }

        public void Refresh()
        {
            m_TabContainer.Clear();

            string currentPrefabPath = PrefabTabsStore.GetCurrentPrefabPath();
            IReadOnlyList<PrefabTabViewInfo> tabs = PrefabTabsStore.GetTabs();

            if (tabs.Count == 0)
            {
                Label emptyLabel = new Label(m_Compact ? "No Prefabs" : "暂无 Prefab 页签。");
                emptyLabel.style.marginLeft = m_Compact ? 4 : 0;
                emptyLabel.style.marginTop = m_Compact ? 0 : 12;
                emptyLabel.style.color = new Color(0.72f, 0.72f, 0.72f);
                m_TabContainer.Add(emptyLabel);
            }
            else
            {
                foreach (PrefabTabViewInfo tab in tabs)
                {
                    bool isCurrent = string.Equals(tab.AssetPath, currentPrefabPath, StringComparison.Ordinal);
                    m_TabContainer.Add(m_Compact ? BuildCompactTab(tab, isCurrent) : BuildListTab(tab, isCurrent));
                }
            }

            if (m_StatusLabel != null)
            {
                int pinnedCount = 0;
                foreach (PrefabTabViewInfo tab in tabs)
                {
                    if (tab.Pinned)
                    {
                        pinnedCount++;
                    }
                }

                m_StatusLabel.text = $"{tabs.Count} prefabs, {pinnedCount} pinned";
            }
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.flexShrink = 0;

            Button refreshButton = new Button(() =>
            {
                PrefabTabsStore.CleanupInvalidTabs();
                Refresh();
            })
            {
                text = m_Compact ? "R" : "Refresh",
                tooltip = "清理已删除或无效的 Prefab 页签。"
            };
            refreshButton.style.width = m_Compact ? 24 : 72;
            refreshButton.style.height = m_Compact ? 20 : 22;
            toolbar.Add(refreshButton);

            Button pingButton = new Button(PrefabTabsStore.PingCurrentPrefab)
            {
                text = m_Compact ? "P" : "Ping",
                tooltip = "在 Project 中定位当前 Prefab。"
            };
            pingButton.style.width = m_Compact ? 24 : 56;
            pingButton.style.height = m_Compact ? 20 : 22;
            pingButton.style.marginLeft = 4;
            toolbar.Add(pingButton);

            Button clearAllButton = new Button(PrefabTabsStore.ClearAll)
            {
                text = m_Compact ? "C" : "Clear All",
                tooltip = "清空全部 Prefab 页签。"
            };
            clearAllButton.style.width = m_Compact ? 24 : 72;
            clearAllButton.style.height = m_Compact ? 20 : 22;
            clearAllButton.style.marginLeft = 4;
            toolbar.Add(clearAllButton);

            if (!m_Compact)
            {
                Label hintLabel = new Label("双击 Prefab 后会自动记录到这里");
                hintLabel.style.marginLeft = 10;
                hintLabel.style.color = new Color(0.72f, 0.72f, 0.72f);
                hintLabel.style.flexGrow = 1;
                toolbar.Add(hintLabel);
            }

            return toolbar;
        }

        private VisualElement BuildCompactTab(PrefabTabViewInfo tab, bool isCurrent)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 22;
            row.style.marginRight = 3;
            row.style.paddingLeft = 2;
            row.style.paddingRight = 2;
            row.style.backgroundColor = isCurrent
                ? new Color(0.22f, 0.36f, 0.52f, 0.95f)
                : new Color(0.18f, 0.18f, 0.18f, 0.85f);

            Button openButton = new Button(() => PrefabTabsStore.OpenPrefab(tab.Guid))
            {
                text = GetTabDisplayName(tab),
                tooltip = $"{tab.AssetPath}\n右键固定或取消固定页签。"
            };
            openButton.style.height = 20;
            openButton.style.maxWidth = 150;
            openButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            openButton.RegisterCallback<ContextClickEvent>(evt => ShowTabContextMenu(evt, tab));
            row.Add(openButton);

            Button closeButton = new Button(() => PrefabTabsStore.Remove(tab.Guid))
            {
                text = "x",
                tooltip = "关闭此页签。"
            };
            closeButton.style.width = 22;
            closeButton.style.height = 20;
            closeButton.style.marginLeft = 1;
            row.Add(closeButton);

            row.RegisterCallback<ContextClickEvent>(evt => ShowTabContextMenu(evt, tab));
            return row;
        }

        private VisualElement BuildListTab(PrefabTabViewInfo tab, bool isCurrent)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 28;
            row.style.marginBottom = 3;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 4;
            row.style.backgroundColor = isCurrent
                ? new Color(0.22f, 0.36f, 0.52f, 0.95f)
                : new Color(0.18f, 0.18f, 0.18f, 0.85f);

            Button openButton = new Button(() => PrefabTabsStore.OpenPrefab(tab.Guid))
            {
                text = GetTabDisplayName(tab),
                tooltip = $"{tab.AssetPath}\n右键固定或取消固定页签。"
            };
            openButton.style.flexGrow = 1;
            openButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            openButton.style.height = 24;
            openButton.RegisterCallback<ContextClickEvent>(evt => ShowTabContextMenu(evt, tab));
            row.Add(openButton);

            Button pingButton = new Button(() => PrefabTabsStore.PingPrefab(tab.Guid))
            {
                text = "Ping",
                tooltip = "在 Project 中定位此 Prefab。"
            };
            pingButton.style.width = 48;
            pingButton.style.marginLeft = 4;
            row.Add(pingButton);

            Button closeButton = new Button(() => PrefabTabsStore.Remove(tab.Guid))
            {
                text = "X",
                tooltip = "关闭此页签。"
            };
            closeButton.style.width = 26;
            closeButton.style.marginLeft = 4;
            row.Add(closeButton);

            row.RegisterCallback<ContextClickEvent>(evt => ShowTabContextMenu(evt, tab));
            return row;
        }

        private static string GetTabDisplayName(PrefabTabViewInfo tab)
        {
            return tab.Pinned ? $"* {tab.DisplayName}" : tab.DisplayName;
        }

        private static void ShowTabContextMenu(ContextClickEvent evt, PrefabTabViewInfo tab)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(
                new GUIContent(tab.Pinned ? "取消固定页签" : "固定页签"),
                false,
                () => PrefabTabsStore.TogglePinned(tab.Guid));
            menu.ShowAsContext();
            evt.StopPropagation();
        }
    }
}
