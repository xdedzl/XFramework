using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class MenuItemSearchWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/MenuItem Search";
        private const float RootColumnWidth = 110f;
        private const float SourceColumnWidth = 86f;
        private const float ShortcutColumnWidth = 72f;
        private const float MethodColumnWidth = 220f;
        private const string XFrameworkPackageRoot = "Packages/com.xdedzl.xframework/";
        private const string XAnimationPackageRoot = "Packages/com.xdedzl.xanimation/";

        private readonly List<MenuItemInfo> m_AllItems = new();
        private readonly List<MenuItemInfo> m_FilteredItems = new();

        private TextField m_SearchField;
        private Toggle m_ShowDisabledToggle;
        private Button m_SourceFilterButton;
        private readonly HashSet<MenuItemSourceKind> m_SelectedSourceKinds = new()
        {
            MenuItemSourceKind.XFramework,
            MenuItemSourceKind.XAnimation,
            MenuItemSourceKind.Project
        };
        private Label m_SummaryLabel;
        private ListView m_ListView;
        private ScrollView m_DetailPane;
        private Button m_ExecuteButton;
        private Button m_RefreshButton;
        private VisualElement m_LoadingOverlay;
        private Label m_LoadingTitleLabel;
        private Label m_LoadingStatusLabel;
        private ProgressBar m_LoadingProgressBar;

        private MenuItemInfo m_SelectedItem;
        private Task<List<MenuItemInfo>> m_ScanTask;
        private int m_ActiveScanVersion;
        private readonly object m_ScanProgressLock = new();
        private int m_ScanVersion;
        private bool m_IsScanning;
        private float m_ScanProgress;
        private string m_ScanStatus = string.Empty;
        private string m_ScanSelectedPath;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            MenuItemSearchWindow window = GetWindow<MenuItemSearchWindow>();
            window.titleContent = new GUIContent("MenuItem Search");
            window.minSize = new Vector2(900f, 520f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            RefreshItems();
            EditorApplication.update += HandleEditorUpdate;
        }

        private void OnDisable()
        {
            m_ScanVersion++;
            EditorApplication.update -= HandleEditorUpdate;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
        }

        public void CreateGUI()
        {
            BuildUI();
            RefreshView();
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
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
            m_SummaryLabel.style.marginLeft = 2f;
            m_SummaryLabel.style.marginTop = 4f;
            m_SummaryLabel.style.marginBottom = 6f;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            root.Add(m_SummaryLabel);

            var splitView = new TwoPaneSplitView(0, 620, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            root.Add(splitView);

            splitView.Add(BuildListPane());
            splitView.Add(BuildDetailPane());

            root.Add(BuildLoadingOverlay());
            UpdateLoadingState();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1f;
            m_SearchField.style.minWidth = 180f;
            m_SearchField.tooltip = "搜索菜单路径、根目录、声明类型、方法或程序集。空格分词后全部命中才显示。";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            m_ShowDisabledToggle = new Toggle("显示不可用项");
            m_ShowDisabledToggle.style.marginLeft = 8f;
            m_ShowDisabledToggle.tooltip = "显示当前 validation 返回 false 的菜单项。";
            m_ShowDisabledToggle.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_ShowDisabledToggle);

            m_SourceFilterButton = new Button(ShowSourceFilterMenu);
            m_SourceFilterButton.style.width = 190f;
            m_SourceFilterButton.style.marginLeft = 8f;
            m_SourceFilterButton.tooltip = "选择要显示的 MenuItem 来源。";
            toolbar.Add(m_SourceFilterButton);
            RefreshSourceFilterButtonText();

            m_RefreshButton = new Button(RefreshItems)
            {
                text = "刷新"
            };
            m_RefreshButton.style.width = 64f;
            m_RefreshButton.style.marginLeft = 8f;
            toolbar.Add(m_RefreshButton);

            return toolbar;
        }

        private VisualElement BuildLoadingOverlay()
        {
            m_LoadingOverlay = new VisualElement();
            m_LoadingOverlay.style.position = Position.Absolute;
            m_LoadingOverlay.style.left = 0f;
            m_LoadingOverlay.style.right = 0f;
            m_LoadingOverlay.style.top = 0f;
            m_LoadingOverlay.style.bottom = 0f;
            m_LoadingOverlay.style.justifyContent = Justify.Center;
            m_LoadingOverlay.style.alignItems = Align.Center;
            m_LoadingOverlay.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.72f);

            var panel = new XBox();
            panel.style.width = 360f;
            panel.style.paddingLeft = 16f;
            panel.style.paddingRight = 16f;
            panel.style.paddingTop = 14f;
            panel.style.paddingBottom = 14f;
            panel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.96f);
            m_LoadingOverlay.Add(panel);

            m_LoadingTitleLabel = new Label("正在扫描 MenuItem");
            m_LoadingTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_LoadingTitleLabel.style.fontSize = 14f;
            m_LoadingTitleLabel.style.marginBottom = 8f;
            panel.Add(m_LoadingTitleLabel);

            m_LoadingProgressBar = new ProgressBar();
            m_LoadingProgressBar.lowValue = 0f;
            m_LoadingProgressBar.highValue = 1f;
            m_LoadingProgressBar.value = 0f;
            panel.Add(m_LoadingProgressBar);

            m_LoadingStatusLabel = new Label();
            m_LoadingStatusLabel.style.marginTop = 8f;
            m_LoadingStatusLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            m_LoadingStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(m_LoadingStatusLabel);

            return m_LoadingOverlay;
        }

        private void ShowSourceFilterMenu()
        {
            var menu = new GenericMenu();
            bool allSelected = AreAllSourcesSelected();
            menu.AddItem(new GUIContent("ALL"), allSelected, ToggleAllSources);
            menu.AddSeparator(string.Empty);
            AddSourceMenuItem(menu, MenuItemSourceKind.XFramework);
            AddSourceMenuItem(menu, MenuItemSourceKind.XAnimation);
            AddSourceMenuItem(menu, MenuItemSourceKind.Project);
            AddSourceMenuItem(menu, MenuItemSourceKind.Package);
            AddSourceMenuItem(menu, MenuItemSourceKind.Unity);
            menu.ShowAsContext();
        }

        private void AddSourceMenuItem(GenericMenu menu, MenuItemSourceKind sourceKind)
        {
            menu.AddItem(new GUIContent(GetSourceLabel(sourceKind)), m_SelectedSourceKinds.Contains(sourceKind), () => ToggleSource(sourceKind));
        }

        private void ToggleAllSources()
        {
            if (AreAllSourcesSelected())
            {
                m_SelectedSourceKinds.Clear();
            }
            else
            {
                m_SelectedSourceKinds.Clear();
                foreach (MenuItemSourceKind sourceKind in GetAllSourceKinds())
                {
                    m_SelectedSourceKinds.Add(sourceKind);
                }
            }

            RefreshSourceFilterButtonText();
            RefreshView();
        }

        private void ToggleSource(MenuItemSourceKind sourceKind)
        {
            if (!m_SelectedSourceKinds.Add(sourceKind))
            {
                m_SelectedSourceKinds.Remove(sourceKind);
            }

            RefreshSourceFilterButtonText();
            RefreshView();
        }

        private void RefreshSourceFilterButtonText()
        {
            if (m_SourceFilterButton == null)
            {
                return;
            }

            if (AreAllSourcesSelected())
            {
                m_SourceFilterButton.text = "来源: ALL";
                return;
            }

            if (m_SelectedSourceKinds.Count == 0)
            {
                m_SourceFilterButton.text = "来源: 无";
                return;
            }

            m_SourceFilterButton.text = $"来源: {string.Join(", ", GetAllSourceKinds().Where(m_SelectedSourceKinds.Contains).Select(GetSourceLabel))}";
        }

        private bool AreAllSourcesSelected()
        {
            return GetAllSourceKinds().All(m_SelectedSourceKinds.Contains);
        }

        private VisualElement BuildListPane()
        {
            var pane = new XBox();
            pane.style.flexGrow = 1f;
            pane.style.paddingLeft = 4f;
            pane.style.paddingRight = 4f;
            pane.style.paddingTop = 4f;
            pane.style.paddingBottom = 4f;
            pane.style.marginRight = 4f;

            pane.Add(BuildListHeader());

            m_ListView = new ListView
            {
                itemsSource = m_FilteredItems,
                fixedItemHeight = 25,
                selectionType = SelectionType.Single
            };
            m_ListView.style.flexGrow = 1f;
            m_ListView.style.marginTop = 4f;
            m_ListView.makeItem = MakeListItem;
            m_ListView.bindItem = BindListItem;
            m_ListView.selectionChanged += OnSelectionChanged;
            pane.Add(m_ListView);

            return pane;
        }

        private VisualElement BuildListHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.height = 22f;
            header.style.paddingLeft = 5f;
            header.style.paddingRight = 6f;
            header.style.alignItems = Align.Center;
            header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            header.Add(CreateHeaderLabel("根目录", RootColumnWidth));
            header.Add(CreateHeaderLabel("来源", SourceColumnWidth));
            header.Add(CreateHeaderLabel("菜单路径", 1f, true));
            header.Add(CreateHeaderLabel("快捷键", ShortcutColumnWidth));
            header.Add(CreateHeaderLabel("方法", MethodColumnWidth));
            return header;
        }

        private VisualElement BuildDetailPane()
        {
            var pane = new XBox();
            pane.style.flexGrow = 1f;
            pane.style.paddingLeft = 10f;
            pane.style.paddingRight = 10f;
            pane.style.paddingTop = 10f;
            pane.style.paddingBottom = 10f;
            pane.style.marginLeft = 4f;

            m_DetailPane = new ScrollView();
            m_DetailPane.style.flexGrow = 1f;
            pane.Add(m_DetailPane);

            ShowItemDetail(m_SelectedItem);
            return pane;
        }

        private VisualElement MakeListItem()
        {
            var row = new MenuItemRow(ExecuteItem);
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount >= 2 && row.userData is MenuItemInfo item)
                {
                    ExecuteItem(item);
                    evt.StopPropagation();
                }
            });

            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_FilteredItems.Count)
            {
                return;
            }

            if (element is MenuItemRow row)
            {
                row.Bind(m_FilteredItems[index], index % 2 == 0);
            }
        }

        private void OnSelectionChanged(IEnumerable<object> selected)
        {
            m_SelectedItem = selected.FirstOrDefault() as MenuItemInfo;
            ShowItemDetail(m_SelectedItem);
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            ExecuteItem(m_SelectedItem);
            evt.StopPropagation();
        }

        private void RefreshItems()
        {
            int scanVersion = ++m_ScanVersion;
            m_ActiveScanVersion = scanVersion;
            m_ScanSelectedPath = m_SelectedItem?.MenuPath;
            m_AllItems.Clear();
            m_FilteredItems.Clear();
            m_SelectedItem = null;
            m_IsScanning = true;
            SetScanProgress(0f, "准备扫描程序集...");
            UpdateLoadingState();
            RefreshView(null);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            m_ScanTask = Task.Run(() => DiscoverMenuItems(scanVersion, projectRoot));
        }

        private void HandleEditorUpdate()
        {
            if (m_IsScanning)
            {
                UpdateLoadingState();
            }

            if (m_ScanTask == null || !m_ScanTask.IsCompleted)
            {
                return;
            }

            Task<List<MenuItemInfo>> completedTask = m_ScanTask;
            m_ScanTask = null;
            if (completedTask.IsFaulted)
            {
                m_IsScanning = false;
                UpdateLoadingState();
                Debug.LogException(completedTask.Exception);
                return;
            }

            m_AllItems.Clear();
            List<MenuItemInfo> result = completedTask.Result ?? new List<MenuItemInfo>();
            m_AllItems.AddRange(result);
            m_IsScanning = false;
            UpdateLoadingState();
            RefreshView(m_ScanSelectedPath);
        }

        private void SetScanProgress(float progress, string status)
        {
            lock (m_ScanProgressLock)
            {
                m_ScanProgress = Math.Max(0f, Math.Min(1f, progress));
                m_ScanStatus = status ?? string.Empty;
            }
        }

        private void UpdateLoadingState()
        {
            if (m_RefreshButton != null)
            {
                m_RefreshButton.SetEnabled(!m_IsScanning);
            }

            if (m_LoadingOverlay == null)
            {
                return;
            }

            m_LoadingOverlay.style.display = m_IsScanning ? DisplayStyle.Flex : DisplayStyle.None;
            if (!m_IsScanning)
            {
                return;
            }

            float progress;
            string status;
            lock (m_ScanProgressLock)
            {
                progress = m_ScanProgress;
                status = m_ScanStatus;
            }

            if (m_LoadingProgressBar != null)
            {
                m_LoadingProgressBar.value = progress;
                m_LoadingProgressBar.title = $"{(int)Math.Round(progress * 100f)}%";
            }

            if (m_LoadingStatusLabel != null)
            {
                m_LoadingStatusLabel.text = string.IsNullOrEmpty(status) ? "扫描中..." : status;
            }
        }

        private void RefreshView()
        {
            RefreshView(m_SelectedItem?.MenuPath);
        }

        private void RefreshView(string selectedPath)
        {
            m_FilteredItems.Clear();

            string search = m_SearchField?.value ?? string.Empty;
            string[] keywords = search
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(keyword => keyword.Trim())
                .Where(keyword => keyword.Length > 0)
                .ToArray();
            bool showDisabled = m_ShowDisabledToggle?.value ?? false;

            foreach (MenuItemInfo item in m_AllItems)
            {
                if (!m_SelectedSourceKinds.Contains(item.SourceKind))
                {
                    continue;
                }

                item.RefreshAvailability();
                if (!showDisabled && !item.IsAvailable)
                {
                    continue;
                }

                if (keywords.Length > 0 && !keywords.All(item.Matches))
                {
                    continue;
                }

                m_FilteredItems.Add(item);
            }

            m_FilteredItems.Sort(CompareMenuItems);

            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_FilteredItems;
                m_ListView.Rebuild();
            }

            RefreshSummary();
            RestoreSelection(selectedPath);
        }

        private void RefreshSummary()
        {
            if (m_SummaryLabel == null)
            {
                return;
            }

            int availableCount = m_AllItems.Count(item => item.IsAvailable);
            int disabledCount = m_AllItems.Count - availableCount;
            int defaultSourceCount = m_AllItems.Count(item => IsDefaultSource(item.SourceKind));
            m_SummaryLabel.text = $"菜单项 {m_FilteredItems.Count} / {m_AllItems.Count} | 默认来源 {defaultSourceCount} | 可执行 {availableCount} | 不可用 {disabledCount}";
        }

        private void RestoreSelection(string selectedPath)
        {
            int selectedIndex = -1;
            if (!string.IsNullOrEmpty(selectedPath))
            {
                selectedIndex = m_FilteredItems.FindIndex(item => item.MenuPath == selectedPath);
            }

            if (selectedIndex < 0 && m_SelectedItem != null)
            {
                selectedIndex = m_FilteredItems.IndexOf(m_SelectedItem);
            }

            m_SelectedItem = selectedIndex >= 0 ? m_FilteredItems[selectedIndex] : null;

            if (m_ListView != null)
            {
                if (selectedIndex >= 0)
                {
                    m_ListView.SetSelectionWithoutNotify(new[] { selectedIndex });
                    m_ListView.ScrollToItem(selectedIndex);
                }
                else
                {
                    m_ListView.ClearSelection();
                }
            }

            ShowItemDetail(m_SelectedItem);
        }

        private void ShowItemDetail(MenuItemInfo item)
        {
            if (m_DetailPane == null)
            {
                return;
            }

            m_DetailPane.Clear();
            if (item == null)
            {
                var emptyTitle = new Label("未选择菜单项");
                emptyTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                emptyTitle.style.fontSize = 14f;
                emptyTitle.style.marginBottom = 8f;
                m_DetailPane.Add(emptyTitle);

                var hint = new Label("从左侧选择一个 MenuItem 查看详情。");
                hint.style.color = new Color(0.72f, 0.72f, 0.72f);
                m_DetailPane.Add(hint);
                m_ExecuteButton = null;
                return;
            }

            item.RefreshAvailability();

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10f;
            header.style.paddingLeft = 8f;
            header.style.paddingRight = 8f;
            header.style.paddingTop = 8f;
            header.style.paddingBottom = 8f;
            header.style.backgroundColor = new Color(0.21f, 0.21f, 0.21f, 0.9f);

            var status = new Label(item.IsAvailable ? "ON" : "OFF");
            status.style.width = 42f;
            status.style.unityTextAlign = TextAnchor.MiddleCenter;
            status.style.unityFontStyleAndWeight = FontStyle.Bold;
            status.style.color = item.IsAvailable ? new Color(0.6f, 1f, 0.65f) : new Color(0.95f, 0.55f, 0.55f);
            header.Add(status);

            var titleGroup = new VisualElement();
            titleGroup.style.flexGrow = 1f;
            titleGroup.style.marginLeft = 8f;

            var pathTitle = new Label(item.MenuPath);
            pathTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            pathTitle.style.fontSize = 14f;
            pathTitle.style.whiteSpace = WhiteSpace.Normal;
            titleGroup.Add(pathTitle);

            var subtitle = new Label($"{item.DeclaringType.FullName}.{item.Method.Name}");
            subtitle.style.color = new Color(0.72f, 0.72f, 0.72f);
            subtitle.style.marginTop = 2f;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            titleGroup.Add(subtitle);
            header.Add(titleGroup);

            m_DetailPane.Add(header);

            m_DetailPane.Add(CreateReadOnlyTextField("完整路径", item.MenuPath));
            m_DetailPane.Add(CreateReadOnlyTextField("原始声明", item.AttributePath));
            m_DetailPane.Add(CreateReadOnlyTextField("根目录", item.Root));
            m_DetailPane.Add(CreateReadOnlyTextField("来源", item.SourceLabel));
            m_DetailPane.Add(CreateReadOnlyTextField("脚本路径", item.SourcePath));
            m_DetailPane.Add(CreateReadOnlyTextField("快捷键", string.IsNullOrEmpty(item.Shortcut) ? "-" : item.Shortcut));
            m_DetailPane.Add(CreateReadOnlyTextField("优先级", item.Priority.ToString()));
            m_DetailPane.Add(CreateReadOnlyTextField("声明类型", item.DeclaringType.FullName));
            m_DetailPane.Add(CreateReadOnlyTextField("方法", item.Method.Name));
            m_DetailPane.Add(CreateReadOnlyTextField("程序集", item.AssemblyName));
            m_DetailPane.Add(CreateReadOnlyTextField("Validation", item.ValidateMethod != null ? item.ValidateMethod.Name : "无"));
            m_DetailPane.Add(CreateReadOnlyTextField("当前状态", item.IsAvailable ? "可执行" : item.DisabledReason));

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 12f;
            m_DetailPane.Add(buttonRow);

            m_ExecuteButton = new Button(() => ExecuteItem(item))
            {
                text = "执行"
            };
            m_ExecuteButton.style.width = 88f;
            m_ExecuteButton.SetEnabled(item.IsAvailable);
            buttonRow.Add(m_ExecuteButton);

            var copyButton = new Button(() => CopyToClipboard(item.MenuPath))
            {
                text = "复制路径"
            };
            copyButton.style.width = 90f;
            copyButton.style.marginLeft = 8f;
            buttonRow.Add(copyButton);
        }

        private void ExecuteItem(MenuItemInfo item)
        {
            if (item == null)
            {
                return;
            }

            item.RefreshAvailability();
            if (!item.IsAvailable)
            {
                return;
            }

            bool executed = EditorApplication.ExecuteMenuItem(item.MenuPath);
            if (!executed && !string.Equals(item.MenuPath, item.AttributePath, StringComparison.Ordinal))
            {
                executed = EditorApplication.ExecuteMenuItem(item.AttributePath);
            }

            if (!executed)
            {
                Debug.LogWarning($"Execute menu item failed: {item.MenuPath}");
            }
        }

        private List<MenuItemInfo> DiscoverMenuItems(int scanVersion, string projectRoot)
        {
            var executableItems = new Dictionary<string, MenuItemInfo>(StringComparer.Ordinal);
            var validators = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
            var sourceCache = new Dictionary<Type, MenuItemSourceInfo>();
            SetScanProgress(0.02f, "建立脚本索引...");
            SourcePathIndex sourcePathIndex = BuildSourcePathIndex(projectRoot);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int totalAssemblies = Math.Max(assemblies.Length, 1);
            int assemblyIndex = 0;

            foreach (Assembly assembly in assemblies)
            {
                if (scanVersion != m_ScanVersion)
                {
                    return new List<MenuItemInfo>();
                }

                assemblyIndex++;
                SetScanProgress(assemblyIndex / (float)totalAssemblies * 0.9f, $"扫描程序集 {assembly.GetName().Name}");
                foreach (Type type in GetSafeTypes(assembly))
                {
                    if (type == null)
                    {
                        continue;
                    }

                    const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    foreach (MethodInfo method in type.GetMethods(flags))
                    {
                        foreach (UnityEditor.MenuItem attribute in method.GetCustomAttributes<UnityEditor.MenuItem>(false))
                        {
                            string attributePath = attribute.menuItem;
                            string menuPath = StripShortcut(attributePath, out string shortcut);
                            if (string.IsNullOrWhiteSpace(menuPath))
                            {
                                continue;
                            }

                            if (attribute.validate)
                            {
                                validators[menuPath] = method;
                                continue;
                            }

                            if (!executableItems.TryGetValue(menuPath, out MenuItemInfo item)
                                || attribute.priority < item.Priority
                                || (attribute.priority == item.Priority
                                    && string.Compare(method.DeclaringType?.FullName, item.DeclaringType.FullName, StringComparison.Ordinal) < 0))
                            {
                                MenuItemSourceInfo sourceInfo = GetSourceInfo(method.DeclaringType, sourceCache, sourcePathIndex);
                                executableItems[menuPath] = new MenuItemInfo(
                                    scanVersion,
                                    attributePath,
                                    menuPath,
                                    shortcut,
                                    attribute.priority,
                                    method,
                                    sourceInfo);
                            }
                        }
                    }
                }
            }

            SetScanProgress(0.95f, "整理扫描结果...");
            foreach (MenuItemInfo item in executableItems.Values)
            {
                if (validators.TryGetValue(item.MenuPath, out MethodInfo validateMethod))
                {
                    item.ValidateMethod = validateMethod;
                }
            }

            SetScanProgress(1f, "扫描完成");
            return executableItems.Values
                .OrderBy(item => GetSourceSortOrder(item.SourceKind))
                .ThenBy(item => item.Priority)
                .ThenBy(item => item.MenuPath, StringComparer.Ordinal)
                .ToList();
        }

        private static int CompareMenuItems(MenuItemInfo left, MenuItemInfo right)
        {
            int sourceResult = GetSourceSortOrder(left.SourceKind).CompareTo(GetSourceSortOrder(right.SourceKind));
            if (sourceResult != 0)
            {
                return sourceResult;
            }

            int priorityResult = left.Priority.CompareTo(right.Priority);
            return priorityResult != 0
                ? priorityResult
                : string.Compare(left.MenuPath, right.MenuPath, StringComparison.Ordinal);
        }

        private static bool IsDefaultSource(MenuItemSourceKind sourceKind)
        {
            return sourceKind == MenuItemSourceKind.Project
                || sourceKind == MenuItemSourceKind.XFramework
                || sourceKind == MenuItemSourceKind.XAnimation;
        }

        private static int GetSourceSortOrder(MenuItemSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case MenuItemSourceKind.XFramework:
                    return 0;
                case MenuItemSourceKind.XAnimation:
                    return 1;
                case MenuItemSourceKind.Project:
                    return 2;
                case MenuItemSourceKind.Package:
                    return 3;
                case MenuItemSourceKind.Unity:
                    return 4;
                default:
                    return 99;
            }
        }

        private static IEnumerable<MenuItemSourceKind> GetAllSourceKinds()
        {
            yield return MenuItemSourceKind.XFramework;
            yield return MenuItemSourceKind.XAnimation;
            yield return MenuItemSourceKind.Project;
            yield return MenuItemSourceKind.Package;
            yield return MenuItemSourceKind.Unity;
        }

        private static string GetSourceLabel(MenuItemSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case MenuItemSourceKind.XFramework:
                    return "XFramework";
                case MenuItemSourceKind.XAnimation:
                    return "XAnimation";
                case MenuItemSourceKind.Project:
                    return "项目";
                case MenuItemSourceKind.Package:
                    return "Package";
                case MenuItemSourceKind.Unity:
                    return "Unity 内置";
                default:
                    return sourceKind.ToString();
            }
        }

        private static IEnumerable<Type> GetSafeTypes(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type != null).ToArray();
            }
            catch
            {
                yield break;
            }

            foreach (Type type in types)
            {
                yield return type;
            }
        }

        private static string StripShortcut(string attributePath, out string shortcut)
        {
            shortcut = string.Empty;
            if (string.IsNullOrWhiteSpace(attributePath))
            {
                return string.Empty;
            }

            string trimmed = attributePath.Trim();
            int lastSpaceIndex = trimmed.LastIndexOf(' ');
            if (lastSpaceIndex < 0 || lastSpaceIndex >= trimmed.Length - 1)
            {
                return trimmed;
            }

            string suffix = trimmed.Substring(lastSpaceIndex + 1);
            if (!LooksLikeShortcut(suffix))
            {
                return trimmed;
            }

            shortcut = suffix;
            return trimmed.Substring(0, lastSpaceIndex);
        }

        private static MenuItemSourceInfo GetSourceInfo(Type declaringType, Dictionary<Type, MenuItemSourceInfo> cache, SourcePathIndex sourcePathIndex)
        {
            if (declaringType == null)
            {
                return MenuItemSourceInfo.Other("-");
            }

            if (cache.TryGetValue(declaringType, out MenuItemSourceInfo cachedInfo))
            {
                return cachedInfo;
            }

            string assemblyName = declaringType.Assembly.GetName().Name;
            MenuItemSourceInfo assemblySourceInfo = ClassifySourceByAssemblyName(assemblyName);
            if (assemblySourceInfo.Kind == MenuItemSourceKind.Unity)
            {
                cache.Add(declaringType, assemblySourceInfo);
                return assemblySourceInfo;
            }

            string scriptPath = sourcePathIndex.FindScriptPath(declaringType);
            MenuItemSourceInfo sourceInfo = ClassifySource(declaringType, scriptPath);
            cache.Add(declaringType, sourceInfo);
            return sourceInfo;
        }

        private static MenuItemSourceInfo ClassifySource(Type type, string scriptPath)
        {
            string normalizedPath = (scriptPath ?? string.Empty).Replace('\\', '/');
            if (normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.Project, GetSourceLabel(MenuItemSourceKind.Project), normalizedPath);
            }

            if (normalizedPath.StartsWith(XFrameworkPackageRoot, StringComparison.OrdinalIgnoreCase))
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.XFramework, GetSourceLabel(MenuItemSourceKind.XFramework), normalizedPath);
            }

            if (normalizedPath.StartsWith(XAnimationPackageRoot, StringComparison.OrdinalIgnoreCase))
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.XAnimation, GetSourceLabel(MenuItemSourceKind.XAnimation), normalizedPath);
            }

            if (normalizedPath.StartsWith("Packages/com.unity.", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith("Packages/com.unity/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.IndexOf("/PackageCache/com.unity.", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/PackageCache/com.unity/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.Unity, GetSourceLabel(MenuItemSourceKind.Unity), normalizedPath);
            }

            if (normalizedPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.IndexOf("/PackageCache/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.Package, GetSourceLabel(MenuItemSourceKind.Package), normalizedPath);
            }

            MenuItemSourceInfo assemblySourceInfo = ClassifySourceByAssemblyName(type.Assembly.GetName().Name);
            if (assemblySourceInfo.Kind != MenuItemSourceKind.Package)
            {
                return assemblySourceInfo;
            }

            return new MenuItemSourceInfo(MenuItemSourceKind.Package, "Package", "-");
        }

        private static MenuItemSourceInfo ClassifySourceByAssemblyName(string assemblyName)
        {
            if (string.Equals(assemblyName, "Assembly-CSharp", StringComparison.Ordinal)
                || string.Equals(assemblyName, "Assembly-CSharp-Editor", StringComparison.Ordinal))
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.Project, GetSourceLabel(MenuItemSourceKind.Project), "-");
            }

            if (assemblyName == "XFrameworkEditor"
                || assemblyName == "XFrameworkRuntime"
                || assemblyName == "XFrameworkCore"
                || assemblyName == "XInspector")
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.XFramework, GetSourceLabel(MenuItemSourceKind.XFramework), "-");
            }

            if (assemblyName == "XAnimation"
                || assemblyName == "XAnimationEditor")
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.XAnimation, GetSourceLabel(MenuItemSourceKind.XAnimation), "-");
            }

            if (assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal)
                || assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal)
                || assemblyName.StartsWith("Unity.", StringComparison.Ordinal))
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.Unity, GetSourceLabel(MenuItemSourceKind.Unity), "-");
            }

            return new MenuItemSourceInfo(MenuItemSourceKind.Package, GetSourceLabel(MenuItemSourceKind.Package), "-");
        }

        private static SourcePathIndex BuildSourcePathIndex(string projectRoot)
        {
            return SourcePathIndex.Build(projectRoot);
        }

        private static bool LooksLikeShortcut(string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                return false;
            }

            if (suffix[0] == '_' || suffix.IndexOf('%') >= 0 || suffix.IndexOf('#') >= 0 || suffix.IndexOf('&') >= 0)
            {
                return true;
            }

            return suffix.All(character => char.IsUpper(character) || char.IsDigit(character) || character == '_');
        }

        private static TextField CreateReadOnlyTextField(string label, string value)
        {
            var field = new TextField(label)
            {
                value = string.IsNullOrEmpty(value) ? "-" : value
            };
            field.isReadOnly = true;
            field.style.marginBottom = 3f;
            return field;
        }

        private static Label CreateHeaderLabel(string text, float widthOrGrow, bool grow = false)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexShrink = 0f;
            label.style.marginRight = 8f;
            if (grow)
            {
                label.style.flexGrow = widthOrGrow;
            }
            else
            {
                label.style.width = widthOrGrow;
                label.style.maxWidth = widthOrGrow;
                label.style.minWidth = widthOrGrow;
            }

            return label;
        }

        private static void CopyToClipboard(string value)
        {
            EditorGUIUtility.systemCopyBuffer = value ?? string.Empty;
        }

        private sealed class MenuItemInfo
        {
            public MenuItemInfo(int scanVersion, string attributePath, string menuPath, string shortcut, int priority, MethodInfo method, MenuItemSourceInfo sourceInfo)
            {
                ScanVersion = scanVersion;
                AttributePath = attributePath;
                MenuPath = menuPath;
                Shortcut = shortcut;
                Priority = priority;
                Method = method;
                DeclaringType = method.DeclaringType;
                AssemblyName = method.DeclaringType?.Assembly.GetName().Name ?? "-";
                Root = GetRoot(menuPath);
                SourceKind = sourceInfo.Kind;
                SourceLabel = sourceInfo.Label;
                SourcePath = sourceInfo.Path;
                SearchText = $"{menuPath} {Root} {SourceLabel} {SourcePath} {method.Name} {DeclaringType?.FullName} {AssemblyName}";
            }

            public string AttributePath { get; }
            public int ScanVersion { get; }
            public string MenuPath { get; }
            public string Shortcut { get; }
            public int Priority { get; }
            public MethodInfo Method { get; }
            public Type DeclaringType { get; }
            public string AssemblyName { get; }
            public string Root { get; }
            public MenuItemSourceKind SourceKind { get; }
            public string SourceLabel { get; }
            public string SourcePath { get; }
            public string SearchText { get; }
            public MethodInfo ValidateMethod { get; set; }
            public bool IsAvailable { get; private set; } = true;
            public string DisabledReason { get; private set; } = "不可用";

            public bool Matches(string keyword)
            {
                return !string.IsNullOrEmpty(SearchText)
                    && SearchText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            public void RefreshAvailability()
            {
                if (ValidateMethod == null)
                {
                    IsAvailable = true;
                    DisabledReason = "可执行";
                    return;
                }

                try
                {
                    object result = ValidateMethod.Invoke(null, null);
                    IsAvailable = result is bool value && value;
                    DisabledReason = IsAvailable ? "可执行" : "Validation 返回 false";
                }
                catch (Exception ex)
                {
                    IsAvailable = false;
                    DisabledReason = $"Validation 异常: {GetExceptionMessage(ex)}";
                }
            }

            private static string GetRoot(string menuPath)
            {
                if (string.IsNullOrEmpty(menuPath))
                {
                    return "-";
                }

                int slashIndex = menuPath.IndexOf('/');
                return slashIndex > 0 ? menuPath.Substring(0, slashIndex) : menuPath;
            }

            private static string GetExceptionMessage(Exception ex)
            {
                if (ex is TargetInvocationException invocationException && invocationException.InnerException != null)
                {
                    return invocationException.InnerException.Message;
                }

                return ex.Message;
            }
        }

        private sealed class MenuItemRow : XItemBox
        {
            private readonly Label m_RootLabel;
            private readonly Label m_SourceLabel;
            private readonly Label m_PathLabel;
            private readonly Label m_ShortcutLabel;
            private readonly Label m_MethodLabel;
            private readonly Button m_ExecuteButton;

            public MenuItemRow(Action<MenuItemInfo> executeAction)
            {
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                style.paddingLeft = 5f;
                style.paddingRight = 6f;

                m_RootLabel = CreateFixedLabel(RootColumnWidth);
                Add(m_RootLabel);

                m_SourceLabel = CreateFixedLabel(SourceColumnWidth);
                Add(m_SourceLabel);

                m_PathLabel = new Label();
                m_PathLabel.style.flexGrow = 1f;
                m_PathLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                m_PathLabel.style.whiteSpace = WhiteSpace.NoWrap;
                m_PathLabel.style.overflow = Overflow.Hidden;
                m_PathLabel.style.textOverflow = TextOverflow.Ellipsis;
                m_PathLabel.style.marginRight = 8f;
                Add(m_PathLabel);

                m_ShortcutLabel = CreateFixedLabel(ShortcutColumnWidth);
                Add(m_ShortcutLabel);

                m_MethodLabel = CreateFixedLabel(MethodColumnWidth);
                Add(m_MethodLabel);

                m_ExecuteButton = new Button(() =>
                {
                    if (userData is MenuItemInfo item)
                    {
                        executeAction(item);
                    }
                })
                {
                    text = "执行"
                };
                m_ExecuteButton.style.width = 48f;
                m_ExecuteButton.style.height = 18f;
                m_ExecuteButton.style.fontSize = 10f;
                Add(m_ExecuteButton);
            }

            public void Bind(MenuItemInfo item, bool useOddStyle)
            {
                userData = item;
                style.backgroundColor = useOddStyle
                    ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                    : new Color(0.3f, 0.3f, 0.3f, 0.18f);

                m_RootLabel.text = item.Root;
                m_RootLabel.tooltip = item.Root;
                m_SourceLabel.text = item.SourceLabel;
                m_SourceLabel.tooltip = item.SourcePath;
                m_PathLabel.text = item.MenuPath;
                m_PathLabel.tooltip = item.MenuPath;
                m_ShortcutLabel.text = string.IsNullOrEmpty(item.Shortcut) ? "-" : item.Shortcut;
                m_ShortcutLabel.tooltip = item.Shortcut;
                m_MethodLabel.text = item.Method.Name;
                m_MethodLabel.tooltip = $"{item.DeclaringType.FullName}.{item.Method.Name}";
                m_ExecuteButton.SetEnabled(item.IsAvailable);

                Color textColor = item.IsAvailable ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.62f, 0.62f, 0.62f);
                m_RootLabel.style.color = textColor;
                m_SourceLabel.style.color = GetSourceColor(item.SourceKind, textColor);
                m_PathLabel.style.color = textColor;
                m_ShortcutLabel.style.color = textColor;
                m_MethodLabel.style.color = textColor;
                tooltip = item.IsAvailable ? item.MenuPath : item.DisabledReason;
            }

            private static Color GetSourceColor(MenuItemSourceKind sourceKind, Color fallback)
            {
                if (sourceKind == MenuItemSourceKind.XFramework)
                {
                    return new Color(0.65f, 0.9f, 1f);
                }

                if (sourceKind == MenuItemSourceKind.XAnimation)
                {
                    return new Color(1f, 0.78f, 0.45f);
                }

                if (sourceKind == MenuItemSourceKind.Project)
                {
                    return new Color(0.78f, 1f, 0.72f);
                }

                return fallback;
            }

            private static Label CreateFixedLabel(float width)
            {
                var label = new Label();
                label.style.width = width;
                label.style.maxWidth = width;
                label.style.minWidth = width;
                label.style.flexShrink = 0f;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                label.style.marginRight = 8f;
                return label;
            }
        }

        private enum MenuItemSourceKind
        {
            Project,
            XFramework,
            XAnimation,
            Package,
            Unity
        }

        private struct MenuItemSourceInfo
        {
            public MenuItemSourceInfo(MenuItemSourceKind kind, string label, string path)
            {
                Kind = kind;
                Label = label;
                Path = string.IsNullOrEmpty(path) ? "-" : path;
            }

            public MenuItemSourceKind Kind { get; }
            public string Label { get; }
            public string Path { get; }

            public static MenuItemSourceInfo Other(string path)
            {
                return new MenuItemSourceInfo(MenuItemSourceKind.Package, GetSourceLabel(MenuItemSourceKind.Package), path);
            }
        }

        private sealed class SourcePathIndex
        {
            private static readonly Regex NamespaceRegex = new(@"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
            private static readonly Regex TypeRegex = new(@"\b(?:class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

            private readonly Dictionary<string, string> m_TypePaths;
            private readonly Dictionary<string, string> m_AssemblyPaths;

            private SourcePathIndex(Dictionary<string, string> typePaths, Dictionary<string, string> assemblyPaths)
            {
                m_TypePaths = typePaths;
                m_AssemblyPaths = assemblyPaths;
            }

            public static SourcePathIndex Build(string projectRoot)
            {
                var typePaths = new Dictionary<string, string>(StringComparer.Ordinal);
                var assemblyPaths = new Dictionary<string, string>(StringComparer.Ordinal);
                if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                {
                    return new SourcePathIndex(typePaths, assemblyPaths);
                }

                AddAssemblyDefinitions(projectRoot, "Assets", assemblyPaths);
                AddAssemblyDefinitions(projectRoot, "Packages", assemblyPaths);
                AddTypePaths(projectRoot, "Assets", typePaths);
                AddTypePaths(projectRoot, "Packages", typePaths);
                return new SourcePathIndex(typePaths, assemblyPaths);
            }

            public string FindScriptPath(Type type)
            {
                if (type == null)
                {
                    return string.Empty;
                }

                if (m_TypePaths.TryGetValue(type.FullName ?? string.Empty, out string path))
                {
                    return path;
                }

                if (m_TypePaths.TryGetValue(type.Name, out path))
                {
                    return path;
                }

                string assemblyName = type.Assembly.GetName().Name;
                return m_AssemblyPaths.TryGetValue(assemblyName, out path) ? path : string.Empty;
            }

            private static void AddAssemblyDefinitions(string projectRoot, string relativeRoot, Dictionary<string, string> assemblyPaths)
            {
                string absoluteRoot = Path.Combine(projectRoot, relativeRoot);
                if (!Directory.Exists(absoluteRoot))
                {
                    return;
                }

                foreach (string filePath in EnumerateFilesSafe(absoluteRoot, "*.asmdef"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    if (!string.IsNullOrEmpty(fileName) && !assemblyPaths.ContainsKey(fileName))
                    {
                        assemblyPaths.Add(fileName, ToProjectPath(projectRoot, filePath));
                    }

                    string content = ReadAllTextSafe(filePath);
                    string name = ExtractAsmdefName(content);
                    if (!string.IsNullOrEmpty(name) && !assemblyPaths.ContainsKey(name))
                    {
                        assemblyPaths.Add(name, ToProjectPath(projectRoot, filePath));
                    }
                }
            }

            private static void AddTypePaths(string projectRoot, string relativeRoot, Dictionary<string, string> typePaths)
            {
                string absoluteRoot = Path.Combine(projectRoot, relativeRoot);
                if (!Directory.Exists(absoluteRoot))
                {
                    return;
                }

                foreach (string filePath in EnumerateFilesSafe(absoluteRoot, "*.cs"))
                {
                    string content = ReadAllTextSafe(filePath);
                    if (string.IsNullOrEmpty(content))
                    {
                        continue;
                    }

                    string projectPath = ToProjectPath(projectRoot, filePath);
                    string namespaceName = ExtractNamespace(content);
                    foreach (string typeName in ExtractTypeNames(content))
                    {
                        AddTypePath(typePaths, typeName, projectPath);
                        if (!string.IsNullOrEmpty(namespaceName))
                        {
                            AddTypePath(typePaths, $"{namespaceName}.{typeName}", projectPath);
                        }
                    }
                }
            }

            private static IEnumerable<string> EnumerateFilesSafe(string root, string pattern)
            {
                try
                {
                    return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).ToArray();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            private static string ReadAllTextSafe(string filePath)
            {
                try
                {
                    return File.ReadAllText(filePath);
                }
                catch
                {
                    return string.Empty;
                }
            }

            private static string ExtractAsmdefName(string content)
            {
                if (string.IsNullOrEmpty(content))
                {
                    return string.Empty;
                }

                Match match = Regex.Match(content, @"""name""\s*:\s*""([^""]+)""");
                return match.Success ? match.Groups[1].Value : string.Empty;
            }

            private static string ExtractNamespace(string content)
            {
                Match match = NamespaceRegex.Match(content);
                return match.Success ? match.Groups[1].Value : string.Empty;
            }

            private static IEnumerable<string> ExtractTypeNames(string content)
            {
                foreach (Match match in TypeRegex.Matches(content))
                {
                    if (match.Success)
                    {
                        yield return match.Groups[1].Value;
                    }
                }
            }

            private static void AddTypePath(Dictionary<string, string> typePaths, string typeName, string projectPath)
            {
                if (!string.IsNullOrEmpty(typeName) && !typePaths.ContainsKey(typeName))
                {
                    typePaths.Add(typeName, projectPath);
                }
            }

            private static string ToProjectPath(string projectRoot, string absolutePath)
            {
                string normalizedProjectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
                string normalizedAbsolutePath = absolutePath.Replace('\\', '/');
                if (!normalizedAbsolutePath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return normalizedAbsolutePath;
                }

                return normalizedAbsolutePath.Substring(normalizedProjectRoot.Length).TrimStart('/');
            }
        }
    }
}
