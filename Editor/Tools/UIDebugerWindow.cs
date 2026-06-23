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
    public class UIDebugerWindow : EditorWindow
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
        private readonly Dictionary<string, UIPanelDebugSnapshot> m_RuntimeSnapshots = new();

        private TextField m_SearchField;
        private DropdownField m_StatusFilter;
        private DropdownField m_TypeFilter;
        private Label m_SummaryLabel;
        private Label m_AutoRefreshLabel;
        private UIToolkitListView m_ListView;
        private GameObject m_TemporaryPanelObject;
        private PanelDebugItem m_SelectedItem;
        private double m_LastRefreshTime;

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

        private void OnEnable()
        {
            titleContent = new GUIContent("UI Debuger");
            EditorApplication.update += HandleEditorUpdate;
            RefreshPanels();
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            DestroyTemporaryPanel();
            XFrameworkInspectorWindow.ClearIfOwner(this);
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

            Button refreshButton = new(RefreshPanels)
            {
                text = "刷新"
            };
            refreshButton.style.marginLeft = 8f;
            refreshButton.style.width = 64f;
            refreshButton.tooltip = "重新扫描面板类型并刷新运行时状态";
            toolbar.Add(refreshButton);

            m_AutoRefreshLabel = new Label("自动刷新: 0.5s");
            m_AutoRefreshLabel.style.marginLeft = 10f;
            m_AutoRefreshLabel.style.color = new Color(0.70f, 0.70f, 0.70f);
            toolbar.Add(m_AutoRefreshLabel);

            return toolbar;
        }

        private static DropdownField CreateToolbarDropdown(VisualElement toolbar, string labelText, List<string> options, float width)
        {
            VisualElement group = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexShrink = 0f,
                    marginLeft = 8f
                }
            };

            Label label = new(labelText);
            label.style.marginRight = 4f;
            label.style.color = new Color(0.75f, 0.75f, 0.75f);
            group.Add(label);

            DropdownField dropdown = new(options, 0);
            dropdown.style.width = width;
            dropdown.style.flexShrink = 0f;
            group.Add(dropdown);

            toolbar.Add(group);
            return dropdown;
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
                itemsSource = m_FilteredItems,
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
            header.Add(CreateHeaderLabel("状态", 54f));
            header.Add(CreateHeaderLabel("面板", 150f));
            header.Add(CreateHeaderLabel("显示名", 120f));
            header.Add(CreateHeaderLabel("类型", 150f));
            header.Add(CreateHeaderLabel("Lv", 36f));
            header.Add(CreateHeaderLabel("UI", 82f));
            header.Add(CreateHeaderLabel("操作", 72f));

            Label path = CreateHeaderLabel("路径", 0f);
            path.style.flexGrow = 1f;
            header.Add(path);
            return header;
        }

        private VisualElement MakeListItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.RegisterCallback<MouseDownEvent>(OnListItemMouseDown);
            row.Add(CreateCellLabel(54f));
            row.Add(CreateCellLabel(150f, true));
            row.Add(CreateCellLabel(120f));
            row.Add(CreateCellLabel(150f));
            row.Add(CreateCellLabel(36f));
            row.Add(CreateCellLabel(82f));
            Button actionButton = null;
            actionButton = new Button(() => ToggleRuntimePanel(actionButton.userData as PanelDebugItem));
            actionButton.style.width = 72f;
            actionButton.style.flexShrink = 0f;
            actionButton.style.marginRight = 8f;
            actionButton.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            row.Add(actionButton);

            Label path = CreateCellLabel(0f);
            path.style.flexGrow = 1f;
            row.Add(path);
            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            PanelDebugItem item = m_FilteredItems[index];
            element.userData = item;
            IReadOnlyList<Label> labels = element.Query<Label>().ToList();
            labels[0].text = GetRuntimeStatusText(item);
            labels[0].style.color = GetRuntimeStatusColor(item);
            labels[1].text = item.PanelName;
            labels[2].text = item.ShowName;
            labels[3].text = item.TypeName;
            labels[4].text = item.Level.ToString();
            labels[5].text = item.IsUIToolkitPanel ? UIToolkitOption : UGUIOption;
            labels[6].text = string.IsNullOrEmpty(item.Path) ? "<empty>" : item.Path;
            Button actionButton = element.Q<Button>();
            BindRuntimeActionButton(actionButton, item);
            element.tooltip = item.FullTypeName;
            element.style.backgroundColor = index % 2 == 0
                ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                : new Color(0.31f, 0.31f, 0.31f, 0.18f);
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

        private static void OnListItemMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement row || row.userData is not PanelDebugItem item)
            {
                return;
            }

            if (evt.clickCount >= 2)
            {
                OpenPanelAsset(item);
                return;
            }

            PingPanelAsset(LoadPanelAsset(item));
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object selected in selection)
            {
                m_SelectedItem = selected as PanelDebugItem;
                DestroyTemporaryPanel();
                RefreshInspectorDetail(true);
                return;
            }

            m_SelectedItem = null;
            DestroyTemporaryPanel();
            XFrameworkInspectorWindow.ClearIfOwner(this);
        }

        private void HandleEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - m_LastRefreshTime < 0.5d)
            {
                return;
            }

            m_LastRefreshTime = EditorApplication.timeSinceStartup;
            RefreshRuntimeSnapshots();
            MergeRuntimeSnapshots();
            RefreshView(false);
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

            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_FilteredItems;
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
            parent.Add(CreateInfoRow("Show Name", item.ShowName));
            parent.Add(CreateInfoRow("Panel Name", item.PanelName));
            parent.Add(CreateInfoRow("Type", item.FullTypeName));
            parent.Add(CreateInfoRow("Path", string.IsNullOrEmpty(item.Path) ? "<empty>" : item.Path));
            parent.Add(CreatePanelAssetActionRow(item));
            parent.Add(CreateInfoRow("Level", item.Level.ToString()));
            parent.Add(CreateInfoRow("UI Type", item.IsUIToolkitPanel ? UIToolkitOption : UGUIOption));
            parent.Add(CreateInfoRow("Tags", FormatTags(item)));

            parent.Add(CreateSectionTitle("运行时状态"));
            parent.Add(CreateInfoRow("Cached", item.IsCached ? "Yes" : "No"));
            parent.Add(CreateInfoRow("Opened", item.IsOpened ? "Yes" : "No"));
            parent.Add(CreateInfoRow("Visible", item.IsVisible ? "Yes" : "No"));
            parent.Add(CreateInfoRow("Close Callback", item.HasCloseCallback ? "Yes" : "No"));
            parent.Add(CreateInfoRow("Hierarchy", string.IsNullOrEmpty(item.HierarchyPath) ? "<none>" : item.HierarchyPath));
            ObjectField objectField = new("GameObject")
            {
                objectType = typeof(GameObject),
                value = item.GameObject,
                allowSceneObjects = true
            };
            objectField.SetEnabled(false);
            objectField.style.marginTop = 2f;
            objectField.style.marginBottom = 2f;
            parent.Add(objectField);

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

            parent.Add(CreateInfoRow("UXML", FormatAssetStatus(uxmlPath, visualTree != null)));
            parent.Add(CreateInfoRow("USS", FormatAssetStatus(ussPath, styleSheet != null)));
            parent.Add(CreateInfoRow("Default PanelSettings", defaultPanelSettings != null ? defaultPanelSettings.name : "<none>"));
            parent.Add(CreateInfoRow("Runtime PanelSettings", runtimePanelSettings != null ? runtimePanelSettings.name : "<none>"));

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

        private static VisualElement CreatePanelAssetActionRow(PanelDebugItem item)
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
            assetField.style.marginRight = 6f;
            row.Add(assetField);

            Button pingButton = new(() => PingPanelAsset(panelAsset))
            {
                text = "Ping"
            };
            pingButton.style.width = 54f;
            pingButton.style.marginRight = 4f;
            pingButton.tooltip = "在 Project 中闪烁 PanelInfo.path 对应的 prefab/资源";
            pingButton.SetEnabled(panelAsset != null);
            row.Add(pingButton);

            Button locateButton = new(() => LocatePanelAsset(panelAsset))
            {
                text = "定位"
            };
            locateButton.style.width = 54f;
            locateButton.tooltip = "选中 PanelInfo.path 对应的 prefab/资源";
            locateButton.SetEnabled(panelAsset != null);
            row.Add(locateButton);

            if (panelAsset == null)
            {
                row.tooltip = string.IsNullOrEmpty(item.Path)
                    ? "PanelInfo.path 为空，无法定位 prefab。"
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

        private static void PingPanelAsset(UnityEngine.Object panelAsset)
        {
            if (panelAsset == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(panelAsset);
        }

        private static void LocatePanelAsset(UnityEngine.Object panelAsset)
        {
            if (panelAsset == null)
            {
                return;
            }

            Selection.activeObject = panelAsset;
            EditorGUIUtility.PingObject(panelAsset);
        }

        private static void OpenPanelAsset(PanelDebugItem item)
        {
            UnityEngine.Object panelAsset = LoadPanelAsset(item);
            if (panelAsset == null)
            {
                return;
            }

            AssetDatabase.OpenAsset(panelAsset);
        }

        private static VisualElement CreateRow(Color backgroundColor, float height)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = height,
                    paddingLeft = 4f,
                    paddingRight = 4f,
                    overflow = Overflow.Hidden,
                    backgroundColor = backgroundColor
                }
            };
            return row;
        }

        private static Label CreateHeaderLabel(string text, float width)
        {
            Label label = new(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.width = width;
            label.style.flexShrink = 0f;
            label.style.marginRight = 8f;
            label.style.overflow = Overflow.Hidden;
            return label;
        }

        private static Label CreateCellLabel(float width, bool bold = false)
        {
            Label label = new();
            label.style.width = width;
            label.style.flexShrink = 0f;
            label.style.marginRight = 8f;
            label.style.overflow = Overflow.Hidden;
            if (bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            return label;
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

        private static VisualElement CreateInfoRow(string labelText, string valueText)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    minHeight = 22f,
                    alignItems = Align.Center,
                    marginBottom = 2f
                }
            };

            Label label = new(labelText);
            label.style.width = 132f;
            label.style.flexShrink = 0f;
            label.style.color = new Color(0.70f, 0.70f, 0.70f);
            row.Add(label);

            Label value = new(valueText);
            value.style.flexGrow = 1f;
            value.style.whiteSpace = WhiteSpace.Normal;
            row.Add(value);
            return row;
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
    }
}
