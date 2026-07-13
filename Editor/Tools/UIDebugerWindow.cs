using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.UI;
using UIToolkitListView = UnityEngine.UIElements.ListView;
#pragma warning disable CS0618 // Type or member is obsolete

namespace XFramework.Editor
{
    public class UIDebugerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/UI Debuger";
        private const string AllOption = "全部";
        private const string OpenedOption = "已打开";
        private const string CachedOption = "已缓存";
        private const string NotCachedOption = "未缓存";
        private const string UIToolkitOption = "UI Toolkit";
        private const string UGUIOption = "UGUI";

        private readonly List<PanelDebugItem> m_AllItems = new();
        private readonly List<PanelDebugItem> m_FilteredItems = new();
        private readonly List<PanelDebugDisplayRow> m_DisplayRows = new();
        private readonly HashSet<int> m_CollapsedLevels = new();
        private readonly Dictionary<string, UIPanelDebugSnapshot> m_RuntimeSnapshots = new();

        private TextField m_SearchField;
        private DropdownField m_StatusFilter;
        private DropdownField m_TypeFilter;
        private Label m_SummaryLabel;
        
        private UIToolkitListView m_ListView;
        private GameObject m_TemporaryPanelObject;
        private PanelDebugItem m_SelectedItem;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            UIDebugerWindow window = GetWindow<UIDebugerWindow>();
            window.titleContent = new GUIContent("UI Debuger");
            window.minSize = new Vector2(1040f, 560f);
            window.RefreshPanels();
            window.Show();
            window.Focus();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            titleContent = new GUIContent("UI Debuger");
            RefreshPanels();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DestroyTemporaryPanel();
        }

        public void CreateGUI()
        {
            BuildUI();
            RefreshView(true);
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 6f;

            root.Add(BuildToolbar());

            m_SummaryLabel = new Label();
            m_SummaryLabel.style.marginTop = 4f;
            m_SummaryLabel.style.marginBottom = 6f;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(m_SummaryLabel);

            root.Add(BuildListPane());
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1f;
            m_SearchField.style.minWidth = 180f;
            m_SearchField.tooltip = "按显示名、面板名、类型名、路径或标签过滤";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView(true));
            toolbar.Add(m_SearchField);

            m_StatusFilter = CreateToolbarDropdown(toolbar, "状态", new List<string>
            {
                AllOption,
                OpenedOption,
                CachedOption,
                NotCachedOption
            }, 92f);
            m_StatusFilter.RegisterValueChangedCallback(_ => RefreshView(true));

            m_TypeFilter = CreateToolbarDropdown(toolbar, "类型", new List<string>
            {
                AllOption,
                UIToolkitOption,
                UGUIOption
            }, 104f);
            m_TypeFilter.RegisterValueChangedCallback(_ => RefreshView(true));

            AddRefreshControls(toolbar, "重新扫描面板类型并刷新运行时状态");

            return toolbar;
        }

        private VisualElement BuildListPane()
        {
            VisualElement pane = new()
            {
                style =
                {
                    flexGrow = 1f,
                    flexDirection = FlexDirection.Column,
                    marginRight = 4f,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    paddingTop = 4f,
                    paddingBottom = 4f,
                    minWidth = 0f,
                    overflow = Overflow.Hidden,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f)
                }
            };

            pane.Add(BuildListHeader());

            m_ListView = new UIToolkitListView
            {
                itemsSource = m_DisplayRows,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeListItem,
                bindItem = BindListItem
            };
            m_ListView.style.flexGrow = 1f;
            m_ListView.style.marginTop = 4f;
            m_ListView.onSelectionChange += OnSelectionChanged;
            pane.Add(m_ListView);
            return pane;
        }

        private VisualElement BuildListHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("状态", 54f, marginRight: 8f, flexShrink: true));
            header.Add(CreateHeaderLabel("面板", 150f, marginRight: 8f, flexShrink: true));
            header.Add(CreateHeaderLabel("显示名", 120f, marginRight: 8f, flexShrink: true));
            header.Add(CreateHeaderLabel("类型", 150f, marginRight: 8f, flexShrink: true));
            header.Add(CreateHeaderLabel("Lv", 36f, marginRight: 8f, flexShrink: true));
            header.Add(CreateHeaderLabel("UI", 82f, marginRight: 8f, flexShrink: true));
            header.Add(CreateHeaderLabel("操作", 72f, marginRight: 8f, flexShrink: true));
            header.Add(CreateHeaderLabel("Prefab", 180f, marginRight: 8f, flexShrink: true));

            Label gameObject = CreateHeaderLabel("GameObject", 0f, marginRight: 8f, flexShrink: true);
            gameObject.style.flexGrow = 1f;
            header.Add(gameObject);
            return header;
        }

        private VisualElement MakeListItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.RegisterCallback<MouseDownEvent>(OnDisplayRowMouseDown);
            row.style.paddingLeft = 0f;
            row.style.paddingRight = 0f;

            VisualElement groupRow = CreateLevelGroupRow();
            row.Add(groupRow);

            VisualElement panelRow = new()
            {
                name = "panel-row",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1f,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    overflow = Overflow.Hidden
                }
            };
            panelRow.Add(CreateCellLabel(54f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis));
            panelRow.Add(CreateCellLabel(150f, bold: true, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis));
            panelRow.Add(CreateCellLabel(120f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis));
            panelRow.Add(CreateCellLabel(150f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis));
            panelRow.Add(CreateCellLabel(36f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis));
            panelRow.Add(CreateCellLabel(82f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis));
            Button actionButton = null;
            actionButton = new Button(() => ToggleRuntimePanel(actionButton.userData as PanelDebugItem));
            actionButton.name = "runtime-action-button";
            actionButton.style.width = 72f;
            actionButton.style.flexShrink = 0f;
            actionButton.style.marginRight = 8f;
            actionButton.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            panelRow.Add(actionButton);

            ObjectField prefabField = CreateReadOnlyObjectField("prefab-field", 180f, typeof(UnityEngine.Object), false);
            prefabField.style.marginRight = 8f;
            panelRow.Add(prefabField);

            ObjectField gameObjectField = CreateReadOnlyObjectField("game-object-field", 0f, typeof(GameObject), true);
            gameObjectField.style.flexGrow = 1f;
            panelRow.Add(gameObjectField);
            row.Add(panelRow);
            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            PanelDebugDisplayRow row = m_DisplayRows[index];
            element.userData = row;
            if (row.IsGroup)
            {
                BindGroupRow(element, row, index);
                return;
            }

            PanelDebugItem item = row.Item;
            VisualElement groupRow = element.Q<VisualElement>("level-group-row");
            groupRow.style.display = DisplayStyle.None;
            VisualElement panelRow = element.Q<VisualElement>("panel-row");
            panelRow.style.display = DisplayStyle.Flex;

            IReadOnlyList<Label> labels = panelRow.Query<Label>().ToList();
            labels[0].text = GetRuntimeStatusText(item);
            labels[0].style.color = GetRuntimeStatusColor(item);
            labels[1].text = "  " + item.PanelName;
            labels[2].text = item.ShowName;
            labels[3].text = item.TypeName;
            labels[4].text = item.Level.ToString();
            labels[5].text = item.IsUIToolkitPanel ? UIToolkitOption : UGUIOption;
            Button actionButton = element.Q<Button>("runtime-action-button");
            actionButton.style.display = DisplayStyle.Flex;
            BindRuntimeActionButton(actionButton, item);
            ObjectField prefabField = element.Q<ObjectField>("prefab-field");
            prefabField.style.display = DisplayStyle.Flex;
            BindObjectField(prefabField, LoadPanelAsset(item));
            ObjectField gameObjectField = element.Q<ObjectField>("game-object-field");
            gameObjectField.style.display = DisplayStyle.Flex;
            BindObjectField(gameObjectField, item.GameObject);
            element.tooltip = item.FullTypeName;
            element.style.backgroundColor = GetPanelRowBackgroundColor(row.StyleIndex);
        }

        private void BindGroupRow(VisualElement element, PanelDebugDisplayRow row, int index)
        {
            VisualElement groupRow = element.Q<VisualElement>("level-group-row");
            groupRow.style.display = DisplayStyle.Flex;
            VisualElement panelRow = element.Q<VisualElement>("panel-row");
            panelRow.style.display = DisplayStyle.None;

            Label expander = groupRow.Q<Label>("level-expander");
            expander.text = row.IsExpanded ? "▼" : "▶";
            Label title = groupRow.Q<Label>("level-title");
            title.text = $"Level {row.Level}";
            Label summary = groupRow.Q<Label>("level-summary");
            summary.text = FormatLevelSummary(row.Items);

            element.tooltip = row.IsExpanded ? "点击收起该 Level" : "点击展开该 Level";
            element.style.backgroundColor = GetLevelRowBackgroundColor(row.StyleIndex);
        }

        private void BindRuntimeActionButton(Button button, PanelDebugItem item)
        {
            if (button == null)
            {
                return;
            }

            button.userData = item;
            button.text = GetRuntimeActionText(item);
            button.tooltip = GetRuntimeActionTooltip(item);
            button.SetEnabled(CanRunRuntimeAction(item));
        }

        private void ToggleRuntimePanel(PanelDebugItem item)
        {
            if (!CanRunRuntimeAction(item))
            {
                return;
            }

            if (item.IsOpened)
            {
                if (!GameEntry.IsModuleLoaded<UIManager>())
                {
                    return;
                }

                UIManager.Instance.ClosePanel(item.PanelName);
            }
            else
            {
                GetOrCreateRuntimeUIManager().OpenPanel(item.PanelName);
            }

            RefreshRuntimeSnapshots();
            MergeRuntimeSnapshots();
            RefreshView(false);
            RefreshInspectorDetail(false);
        }

        private void OnDisplayRowMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement row || row.userData is not PanelDebugDisplayRow displayRow)
            {
                return;
            }

            if (!displayRow.IsGroup)
            {
                return;
            }

            ToggleLevelExpanded(displayRow.Level);
            evt.StopPropagation();
        }

        private void ToggleLevelExpanded(int level)
        {
            if (!m_CollapsedLevels.Add(level))
            {
                m_CollapsedLevels.Remove(level);
            }

            RefreshView(false);
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object selected in selection)
            {
                if (selected is PanelDebugDisplayRow { IsGroup: false } row)
                {
                    m_SelectedItem = row.Item;
                    DestroyTemporaryPanel();
                    RefreshInspectorDetail(true);
                    return;
                }

                m_SelectedItem = null;
                DestroyTemporaryPanel();
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            m_SelectedItem = null;
            DestroyTemporaryPanel();
            XFrameworkInspectorWindow.ClearIfOwner(this);
        }

        protected override void OnAutoRefresh()
        {
            RefreshRuntimeSnapshots();
            MergeRuntimeSnapshots();
            RefreshView(false);
        }

        protected override void OnRefreshClicked()
        {
            RefreshPanels();
        }

        private void RefreshPanels()
        {
            DiscoverPanels();
            RefreshRuntimeSnapshots();
            MergeRuntimeSnapshots();
            RefreshView(true);
        }

        private void DiscoverPanels()
        {
            m_AllItems.Clear();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<PanelBase>())
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                PanelInfoAttribute panelInfo = type.GetCustomAttribute<PanelInfoAttribute>();
                if (panelInfo == null)
                {
                    continue;
                }

                m_AllItems.Add(PanelDebugItem.Create(type, panelInfo));
            }

            m_AllItems.Sort((left, right) =>
            {
                int levelResult = left.Level.CompareTo(right.Level);
                return levelResult != 0
                    ? levelResult
                    : string.Compare(left.PanelName, right.PanelName, StringComparison.Ordinal);
            });
        }

        private void RefreshRuntimeSnapshots()
        {
            m_RuntimeSnapshots.Clear();
            if (!Application.isPlaying || !GameEntry.IsModuleLoaded<UIManager>())
            {
                return;
            }

            foreach (UIPanelDebugSnapshot snapshot in UIManager.Instance.GetDebugPanelSnapshots())
            {
                if (!string.IsNullOrEmpty(snapshot.PanelName))
                {
                    m_RuntimeSnapshots[snapshot.PanelName] = snapshot;
                }
            }
        }

        private void MergeRuntimeSnapshots()
        {
            for (int i = 0; i < m_AllItems.Count; i++)
            {
                PanelDebugItem item = m_AllItems[i];
                item.RuntimeSnapshot = m_RuntimeSnapshots.TryGetValue(item.PanelName, out UIPanelDebugSnapshot snapshot)
                    ? snapshot
                    : null;
            }
        }

        private void RefreshView(bool rebuildDetail)
        {
            m_FilteredItems.Clear();
            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;
            string statusFilter = m_StatusFilter != null ? m_StatusFilter.value : AllOption;
            string typeFilter = m_TypeFilter != null ? m_TypeFilter.value : AllOption;

            foreach (PanelDebugItem item in m_AllItems)
            {
                if (!string.IsNullOrEmpty(search) && !IsSearchMatch(item, search))
                {
                    continue;
                }

                if (!IsStatusMatch(item, statusFilter) || !IsTypeMatch(item, typeFilter))
                {
                    continue;
                }

                m_FilteredItems.Add(item);
            }

            BuildDisplayRows();

            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_DisplayRows;
                m_ListView.Rebuild();
            }

            RefreshSummary();
            if (rebuildDetail)
            {
                if (m_SelectedItem != null && !m_FilteredItems.Contains(m_SelectedItem))
                {
                    m_SelectedItem = null;
                    DestroyTemporaryPanel();
                }

                RefreshInspectorDetail(false);
            }
        }

        private void BuildDisplayRows()
        {
            m_DisplayRows.Clear();

            int groupStyleIndex = 0;
            foreach (IGrouping<int, PanelDebugItem> group in m_FilteredItems.GroupBy(item => item.Level).OrderBy(group => group.Key))
            {
                List<PanelDebugItem> items = group
                    .OrderBy(item => item.PanelName, StringComparer.Ordinal)
                    .ToList();
                bool isExpanded = !m_CollapsedLevels.Contains(group.Key);
                m_DisplayRows.Add(PanelDebugDisplayRow.CreateGroup(group.Key, items, isExpanded, groupStyleIndex));
                groupStyleIndex++;

                if (!isExpanded)
                {
                    continue;
                }

                int itemStyleIndex = 0;
                foreach (PanelDebugItem item in items)
                {
                    m_DisplayRows.Add(PanelDebugDisplayRow.CreateItem(item, itemStyleIndex));
                    itemStyleIndex++;
                }
            }
        }

        private void RefreshSummary()
        {
            if (m_SummaryLabel == null)
            {
                return;
            }

            int toolkitCount = 0;
            int cachedCount = 0;
            int openedCount = 0;
            for (int i = 0; i < m_AllItems.Count; i++)
            {
                PanelDebugItem item = m_AllItems[i];
                if (item.IsUIToolkitPanel)
                {
                    toolkitCount++;
                }

                if (item.IsCached)
                {
                    cachedCount++;
                }

                if (item.IsOpened)
                {
                    openedCount++;
                }
            }

            string playMode = Application.isPlaying ? "Play Mode" : "Edit Mode";
            m_SummaryLabel.text = $"{playMode} | 注册 {m_AllItems.Count} 个 PanelBase，UI Toolkit {toolkitCount} 个，已缓存 {cachedCount} 个，已打开 {openedCount} 个。";
        }

        private void RefreshInspectorDetail(bool openInspector)
        {
            if (m_SelectedItem == null)
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(
                    this,
                    GetInspectorTitle(m_SelectedItem),
                    BuildPanelInspectorContent,
                    m_SelectedItem.FullTypeName);
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void BuildPanelInspectorContent(VisualElement parent)
        {
            if (m_SelectedItem == null)
            {
                Label emptyLabel = new("请选择一个面板。");
                emptyLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                parent.Add(emptyLabel);
                return;
            }

            PanelDebugItem item = m_SelectedItem;
            parent.Add(CreateSectionTitle("注册信息"));
            parent.Add(CreateInfoRow("Show Name", item.ShowName, labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Panel Name", item.PanelName, labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Type", item.FullTypeName, labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Path", string.IsNullOrEmpty(item.Path) ? "<empty>" : item.Path, labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreatePanelAssetObjectRow(item));
            parent.Add(CreateInfoRow("Level", item.Level.ToString(), labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("UI Type", item.IsUIToolkitPanel ? UIToolkitOption : UGUIOption, labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Tags", FormatTags(item), labelWidth: 132f, marginBottom: 2f));

            parent.Add(CreateSectionTitle("运行时状态"));
            parent.Add(CreateInfoRow("Cached", item.IsCached ? "Yes" : "No", labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Opened", item.IsOpened ? "Yes" : "No", labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Visible", item.IsVisible ? "Yes" : "No", labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Close Callback", item.HasCloseCallback ? "Yes" : "No", labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Hierarchy", string.IsNullOrEmpty(item.HierarchyPath) ? "<none>" : item.HierarchyPath, labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateRuntimeGameObjectRow(item));

            if (item.IsUIToolkitPanel)
            {
                BuildUIToolkitDetail(parent, item);
            }
        }

        private static string GetInspectorTitle(PanelDebugItem item)
        {
            return item.ShowName == item.PanelName ? item.PanelName : $"{item.ShowName} ({item.PanelName})";
        }

        private void BuildUIToolkitDetail(VisualElement parent, PanelDebugItem item)
        {
            parent.Add(CreateSectionTitle("UI Toolkit"));

            string uxmlPath = GetSiblingAssetPath(item.Path, ".uxml");
            string ussPath = GetSiblingAssetPath(item.Path, ".uss");
            VisualTreeAsset visualTree = string.IsNullOrEmpty(uxmlPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            StyleSheet styleSheet = string.IsNullOrEmpty(ussPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
            XFrameworkSetting setting = Resources.Load<XFrameworkSetting>("XFrameworkSetting");
            PanelSettings defaultPanelSettings = setting != null ? setting.defaultUIToolkitPanelSettings : null;
            PanelSettings runtimePanelSettings = null;
            if (item.GameObject != null && item.GameObject.TryGetComponent(out UIDocument uiDocument))
            {
                runtimePanelSettings = uiDocument.panelSettings;
            }

            parent.Add(CreateInfoRow("UXML", FormatAssetStatus(uxmlPath, visualTree != null), labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("USS", FormatAssetStatus(ussPath, styleSheet != null), labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Default PanelSettings", defaultPanelSettings != null ? defaultPanelSettings.name : "<none>", labelWidth: 132f, marginBottom: 2f));
            parent.Add(CreateInfoRow("Runtime PanelSettings", runtimePanelSettings != null ? runtimePanelSettings.name : "<none>", labelWidth: 132f, marginBottom: 2f));

            VisualElement previewRoot = new()
            {
                name = "ui-debuger-preview-root",
                style =
                {
                    flexGrow = 1f,
                    minHeight = 260f,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 6f,
                    paddingBottom = 6f,
                    backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.35f)
                }
            };

            Button previewButton = new(() => RenderPreview(previewRoot, item))
            {
                text = "刷新预览"
            };
            previewButton.tooltip = "创建临时隐藏对象，克隆 UXML 并调用 BindUI";
            previewButton.style.width = 96f;
            previewButton.style.marginTop = 8f;
            previewButton.style.marginBottom = 8f;
            parent.Add(previewButton);
            parent.Add(previewRoot);
        }

        private void RenderPreview(VisualElement previewRoot, PanelDebugItem item)
        {
            if (item == null || !item.IsUIToolkitPanel)
            {
                return;
            }

            if (previewRoot == null)
            {
                return;
            }

            DestroyTemporaryPanel();
            previewRoot.Clear();
            ResetPreviewRootStyle(previewRoot);

            m_TemporaryPanelObject = new GameObject($"UI Debuger Preview - {item.PanelName}", typeof(RectTransform));
            m_TemporaryPanelObject.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var panel = (UIToolkitPanelBase)m_TemporaryPanelObject.AddComponent(item.PanelType);
                panel.EditorBuildPreview(previewRoot);
            }
            catch (Exception exception)
            {
                ShowPreviewError(previewRoot, item, exception);
            }
        }

        private static void ShowPreviewError(VisualElement previewRoot, PanelDebugItem item, Exception exception)
        {
            previewRoot.Clear();
            ResetPreviewRootStyle(previewRoot);

            Label title = new($"预览 {item.PanelName} 失败");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16f;
            title.style.color = new Color(1f, 0.62f, 0.48f);
            title.style.marginBottom = 8f;
            previewRoot.Add(title);

            Label message = new(exception.ToString());
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.color = new Color(0.96f, 0.82f, 0.75f);
            previewRoot.Add(message);
        }

        private static void ResetPreviewRootStyle(VisualElement previewRoot)
        {
            previewRoot.style.flexGrow = 1f;
            previewRoot.style.minHeight = 260f;
            previewRoot.style.position = Position.Relative;
            previewRoot.style.left = StyleKeyword.Auto;
            previewRoot.style.right = StyleKeyword.Auto;
            previewRoot.style.top = StyleKeyword.Auto;
            previewRoot.style.bottom = StyleKeyword.Auto;
            previewRoot.style.display = DisplayStyle.Flex;
            previewRoot.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.35f);
        }

        private void DestroyTemporaryPanel()
        {
            if (m_TemporaryPanelObject == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(m_TemporaryPanelObject);
            m_TemporaryPanelObject = null;
        }

        private static bool IsSearchMatch(PanelDebugItem item, string search)
        {
            return item.ShowName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.PanelName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.TypeName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.FullTypeName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.Tags.Any(tag => tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string FormatTags(PanelDebugItem item)
        {
            if (item.Tags.Length == 0)
            {
                return "<none>";
            }

            var values = new List<string>(item.Tags.Length);
            foreach (string tag in item.Tags)
            {
                if (TryGetRuntimeTagSnapshot(item, tag, out UIPanelTagDebugSnapshot runtimeTag))
                {
                    string state = runtimeTag.IsActive ? "Active" : "Inactive";
                    values.Add($"{tag} ({state}, {runtimeTag.ActivePanelCount})");
                    continue;
                }

                values.Add(tag);
            }

            return string.Join(", ", values);
        }

        private static string FormatLevelSummary(IReadOnlyList<PanelDebugItem> items)
        {
            int cachedCount = 0;
            int openedCount = 0;
            int toolkitCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                PanelDebugItem item = items[i];
                if (item.IsCached)
                {
                    cachedCount++;
                }

                if (item.IsOpened)
                {
                    openedCount++;
                }

                if (item.IsUIToolkitPanel)
                {
                    toolkitCount++;
                }
            }

            return $"{items.Count} 个面板，已缓存 {cachedCount}，已打开 {openedCount}，UI Toolkit {toolkitCount}";
        }

        private static bool TryGetRuntimeTagSnapshot(
            PanelDebugItem item,
            string tag,
            out UIPanelTagDebugSnapshot result)
        {
            IReadOnlyList<UIPanelTagDebugSnapshot> runtimeTags = item.RuntimeSnapshot?.Tags;
            if (runtimeTags != null)
            {
                foreach (UIPanelTagDebugSnapshot runtimeTag in runtimeTags)
                {
                    if (runtimeTag.Tag == tag)
                    {
                        result = runtimeTag;
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

        private static bool IsStatusMatch(PanelDebugItem item, string statusFilter)
        {
            return statusFilter switch
            {
                OpenedOption => item.IsOpened,
                CachedOption => item.IsCached,
                NotCachedOption => !item.IsCached,
                _ => true
            };
        }

        private static bool IsTypeMatch(PanelDebugItem item, string typeFilter)
        {
            return typeFilter switch
            {
                UIToolkitOption => item.IsUIToolkitPanel,
                UGUIOption => !item.IsUIToolkitPanel,
                _ => true
            };
        }

        private static string GetRuntimeStatusText(PanelDebugItem item)
        {
            if (item.IsOpened)
            {
                return "打开";
            }

            return item.IsCached ? "缓存" : "-";
        }

        private static string GetRuntimeActionText(PanelDebugItem item)
        {
            if (item.IsOpened)
            {
                return "关闭";
            }

            return "打开";
        }

        private static string GetRuntimeActionTooltip(PanelDebugItem item)
        {
            if (!Application.isPlaying)
            {
                return "运行时才能打开或关闭 UI 面板";
            }

            if (!GameEntry.IsModuleLoaded<UIManager>())
            {
                return "点击后会加载 UIManager 并打开运行时面板";
            }

            if (item.IsOpened)
            {
                return "关闭运行时面板";
            }

            return "打开运行时面板";
        }

        private static bool CanRunRuntimeAction(PanelDebugItem item)
        {
            if (item == null || !Application.isPlaying)
            {
                return false;
            }

            if (item.IsOpened)
            {
                return GameEntry.IsModuleLoaded<UIManager>();
            }

            return true;
        }

        private static UIManager GetOrCreateRuntimeUIManager()
        {
            return GameEntry.IsModuleLoaded<UIManager>()
                ? UIManager.Instance
                : GameEntry.AddModule<UIManager>();
        }

        private static Color GetRuntimeStatusColor(PanelDebugItem item)
        {
            if (item.IsOpened)
            {
                return new Color(0.45f, 0.90f, 0.48f);
            }

            return item.IsCached ? new Color(0.95f, 0.78f, 0.35f) : new Color(0.70f, 0.70f, 0.70f);
        }

        private static string GetSiblingAssetPath(string path, string extension)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : Path.ChangeExtension(path, extension).Replace('\\', '/');
        }

        private static string FormatAssetStatus(string path, bool exists)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "<empty>";
            }

            return exists ? $"{path} (OK)" : $"{path} (Missing)";
        }

        private static VisualElement CreatePanelAssetObjectRow(PanelDebugItem item)
        {
            UnityEngine.Object panelAsset = LoadPanelAsset(item);
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 24f,
                    marginBottom = 4f
                }
            };

            Label label = new("Prefab");
            label.style.width = 132f;
            label.style.flexShrink = 0f;
            label.style.color = new Color(0.70f, 0.70f, 0.70f);
            row.Add(label);

            ObjectField assetField = new()
            {
                objectType = typeof(UnityEngine.Object),
                value = panelAsset,
                allowSceneObjects = false
            };
            assetField.SetEnabled(false);
            assetField.style.flexGrow = 1f;
            row.Add(assetField);

            if (panelAsset == null)
            {
                row.tooltip = string.IsNullOrEmpty(item.Path)
                    ? "PanelInfo.path 为空，无法显示 prefab。"
                    : $"AssetDatabase 找不到路径：{item.Path}";
            }

            return row;
        }

        private static UnityEngine.Object LoadPanelAsset(PanelDebugItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.Path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.Path);
        }

        private static VisualElement CreateRuntimeGameObjectRow(PanelDebugItem item)
        {
            GameObject gameObject = item?.GameObject;
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 24f,
                    marginBottom = 4f
                }
            };

            Label label = new("GameObject");
            label.style.width = 132f;
            label.style.flexShrink = 0f;
            label.style.color = new Color(0.70f, 0.70f, 0.70f);
            row.Add(label);

            ObjectField objectField = new()
            {
                objectType = typeof(GameObject),
                value = gameObject,
                allowSceneObjects = true
            };
            objectField.SetEnabled(false);
            objectField.style.flexGrow = 1f;
            row.Add(objectField);

            if (gameObject == null)
            {
                row.tooltip = "面板打开或缓存后才会显示运行时 GameObject。";
            }

            return row;
        }

        

        private static Color GetLevelRowBackgroundColor(int index)
        {
            return index % 2 == 0
                ? new Color(0.16f, 0.19f, 0.23f, 0.92f)
                : new Color(0.19f, 0.23f, 0.28f, 0.92f);
        }

        private static Color GetPanelRowBackgroundColor(int index)
        {
            return index % 2 == 0
                ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                : new Color(0.31f, 0.31f, 0.31f, 0.16f);
        }

        private static VisualElement CreateLevelGroupRow()
        {
            VisualElement row = new()
            {
                name = "level-group-row",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1f,
                    minWidth = 0f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    overflow = Overflow.Hidden
                }
            };

            Label expander = new()
            {
                name = "level-expander"
            };
            expander.style.width = 14f;
            expander.style.flexShrink = 0f;
            expander.style.fontSize = 9f;
            expander.style.unityTextAlign = TextAnchor.MiddleCenter;
            expander.style.color = new Color(0.82f, 0.82f, 0.82f);
            row.Add(expander);

            Label title = new()
            {
                name = "level-title"
            };
            title.style.width = 96f;
            title.style.flexShrink = 0f;
            title.style.marginLeft = 4f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.90f, 0.90f, 0.90f);
            row.Add(title);

            Label summary = new()
            {
                name = "level-summary"
            };
            summary.style.flexGrow = 1f;
            summary.style.minWidth = 0f;
            summary.style.overflow = Overflow.Hidden;
            summary.style.textOverflow = TextOverflow.Ellipsis;
            summary.style.color = new Color(0.72f, 0.72f, 0.72f);
            row.Add(summary);

            return row;
        }

        

        

        private static ObjectField CreateReadOnlyObjectField(string name, float width, Type objectType, bool allowSceneObjects)
        {
            ObjectField field = new()
            {
                name = name,
                objectType = objectType,
                allowSceneObjects = allowSceneObjects
            };
            field.SetEnabled(false);
            field.style.flexShrink = 0f;
            field.style.minWidth = 0f;
            field.style.height = 20f;
            if (width > 0f)
            {
                field.style.width = width;
            }

            return field;
        }

        private static void BindObjectField(ObjectField field, UnityEngine.Object value)
        {
            if (field == null)
            {
                return;
            }

            field.value = value;
            field.tooltip = value != null ? value.name : string.Empty;
        }

        private static Label CreateSectionTitle(string text)
        {
            Label label = new(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 13f;
            label.style.marginTop = 10f;
            label.style.marginBottom = 4f;
            label.style.color = new Color(0.86f, 0.86f, 0.86f);
            return label;
        }

        

        private sealed class PanelDebugItem
        {
            public string PanelName;
            public string ShowName;
            public string TypeName;
            public string FullTypeName;
            public string Path;
            public int Level;
            public Type PanelType;
            public bool IsUIToolkitPanel;
            public string[] Tags;
            public UIPanelDebugSnapshot? RuntimeSnapshot;

            public bool IsCached => RuntimeSnapshot?.IsCached ?? false;
            public bool IsOpened => RuntimeSnapshot?.IsOpened ?? false;
            public bool IsVisible => RuntimeSnapshot?.IsVisible ?? false;
            public bool HasCloseCallback => RuntimeSnapshot?.HasCloseCallback ?? false;
            public GameObject GameObject => RuntimeSnapshot?.GameObject;
            public string HierarchyPath => RuntimeSnapshot?.HierarchyPath ?? string.Empty;

            public static PanelDebugItem Create(Type type, PanelInfoAttribute panelInfo)
            {
                PanelTagAttribute panelTag = type.GetCustomAttribute<PanelTagAttribute>();
                return new PanelDebugItem
                {
                    PanelName = panelInfo.name,
                    ShowName = panelInfo.showName,
                    TypeName = type.Name,
                    FullTypeName = type.FullName,
                    Path = panelInfo.path ?? string.Empty,
                    Level = panelInfo.level,
                    PanelType = type,
                    IsUIToolkitPanel = typeof(UIToolkitPanelBase).IsAssignableFrom(type),
                    Tags = panelTag?.Tags?
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray() ?? Array.Empty<string>()
                };
            }
        }

        private sealed class PanelDebugDisplayRow
        {
            public int Level;
            public List<PanelDebugItem> Items;
            public PanelDebugItem Item;
            public bool IsExpanded;
            public int StyleIndex;

            public bool IsGroup => Items != null;

            public static PanelDebugDisplayRow CreateGroup(int level, List<PanelDebugItem> items, bool isExpanded, int styleIndex)
            {
                return new PanelDebugDisplayRow
                {
                    Level = level,
                    Items = items,
                    IsExpanded = isExpanded,
                    StyleIndex = styleIndex
                };
            }

            public static PanelDebugDisplayRow CreateItem(PanelDebugItem item, int styleIndex)
            {
                return new PanelDebugDisplayRow
                {
                    Level = item.Level,
                    Item = item,
                    StyleIndex = styleIndex
                };
            }
        }
    }
}
