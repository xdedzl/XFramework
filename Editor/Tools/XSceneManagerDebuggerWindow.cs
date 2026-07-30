using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public sealed class XSceneManagerDebuggerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/XScene Manager Debugger";
        private const float XScenePaneWidth = 560f;

        private readonly List<LoadedXSceneDebugSnapshot> m_XScenes = new();
        private readonly List<LoadedXSceneDebugSnapshot> m_FilteredXScenes = new();
        private readonly List<UnitySceneDebugSnapshot> m_UnityScenes = new();
        private readonly List<UnitySceneDebugSnapshot> m_FilteredUnityScenes = new();

        private XSceneManagerDebugSnapshot? m_Snapshot;
        private string m_SelectedXScenePath;
        private string m_SelectedUnityScenePath;
        private DetailSelectionKind m_DetailSelectionKind;

        private TextField m_SearchField;
        private Label m_ActiveSceneLabel;
        private Label m_SummaryLabel;
        private ListView m_XSceneListView;
        private ListView m_UnitySceneListView;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XSceneManagerDebuggerWindow window = GetWindow<XSceneManagerDebuggerWindow>();
            window.titleContent = new GUIContent("XScene Manager Debugger");
            window.minSize = new Vector2(1080f, 540f);
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshData();
        }

        public void CreateGUI()
        {
            BuildUI();
            RefreshData();
        }

        protected override void OnRefreshClicked()
        {
            RefreshData();
        }

        protected override void OnAutoRefresh()
        {
            RefreshData();
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

            m_ActiveSceneLabel = new Label();
            m_ActiveSceneLabel.style.marginTop = 6f;
            m_ActiveSceneLabel.style.marginBottom = 4f;
            m_ActiveSceneLabel.style.paddingLeft = 6f;
            m_ActiveSceneLabel.style.paddingRight = 6f;
            m_ActiveSceneLabel.style.paddingTop = 4f;
            m_ActiveSceneLabel.style.paddingBottom = 4f;
            m_ActiveSceneLabel.style.color = new Color(0.95f, 0.95f, 0.95f);
            m_ActiveSceneLabel.style.whiteSpace = WhiteSpace.Normal;
            m_ActiveSceneLabel.style.backgroundColor = new Color(0.18f, 0.36f, 0.58f, 0.45f);
            m_ActiveSceneLabel.style.borderTopLeftRadius = 4f;
            m_ActiveSceneLabel.style.borderTopRightRadius = 4f;
            m_ActiveSceneLabel.style.borderBottomLeftRadius = 4f;
            m_ActiveSceneLabel.style.borderBottomRightRadius = 4f;
            root.Add(m_ActiveSceneLabel);

            m_SummaryLabel = new Label();
            m_SummaryLabel.style.marginTop = 4f;
            m_SummaryLabel.style.marginBottom = 6f;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(m_SummaryLabel);

            root.Add(BuildSplitPane());
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
            m_SearchField.tooltip = "按 XScene 路径、场景类型或 Unity 场景路径过滤";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            AddRefreshControls(toolbar, "刷新 XSceneManager 当前已加载场景快照");

            return toolbar;
        }

        private VisualElement BuildSplitPane()
        {
            TwoPaneSplitView splitView = new(0, XScenePaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            splitView.Add(BuildXScenePane());
            splitView.Add(BuildUnityScenePane());
            return splitView;
        }

        private VisualElement BuildXScenePane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginRight = 4f;

            Label title = CreatePaneTitle("Loaded XScenes", height: 22f);
            pane.Add(title);
            pane.Add(BuildXSceneHeader());

            m_XSceneListView = new ListView
            {
                itemsSource = m_FilteredXScenes,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeXSceneItem,
                bindItem = BindXSceneItem
            };
            m_XSceneListView.style.flexGrow = 1f;
            m_XSceneListView.style.marginTop = 4f;
            m_XSceneListView.onSelectionChange += OnXSceneSelectionChanged;
            pane.Add(m_XSceneListView);
            return pane;
        }

        private VisualElement BuildUnityScenePane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginLeft = 4f;

            Label title = CreatePaneTitle("Unity Scenes", height: 22f);
            pane.Add(title);
            pane.Add(BuildUnitySceneHeader());

            m_UnitySceneListView = new ListView
            {
                itemsSource = m_FilteredUnityScenes,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeUnitySceneItem,
                bindItem = BindUnitySceneItem
            };
            m_UnitySceneListView.style.flexGrow = 1f;
            m_UnitySceneListView.style.marginTop = 4f;
            m_UnitySceneListView.onSelectionChange += OnUnitySceneSelectionChanged;
            pane.Add(m_UnitySceneListView);
            return pane;
        }

        private VisualElement BuildXSceneHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("XScene Path", 0f, flexShrink: true));

            Label filler = CreateHeaderLabel(string.Empty, 0f);
            filler.style.flexGrow = 1f;
            header.Add(filler);

            header.Add(CreateHeaderLabel("类型", 80f));
            header.Add(CreateHeaderLabel("状态", 62f));
            header.Add(CreateHeaderLabel("Order", 70f));
            header.Add(CreateHeaderLabel("忙", 36f));
            return header;
        }

        private VisualElement BuildUnitySceneHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("Unity Scene Path", 0f, flexShrink: true));

            Label filler = CreateHeaderLabel(string.Empty, 0f);
            filler.style.flexGrow = 1f;
            header.Add(filler);

            header.Add(CreateHeaderLabel("Name", 130f));
            header.Add(CreateHeaderLabel("Loaded", 62f));
            header.Add(CreateHeaderLabel("Roots", 50f));
            return header;
        }

        private void RefreshData()
        {
            m_Snapshot = null;
            m_XScenes.Clear();
            m_UnityScenes.Clear();

            if (Application.isPlaying)
            {
                XSceneManagerDebugSnapshot snapshot = XSceneManager.GetDebugSnapshot();
                m_Snapshot = snapshot;
                m_XScenes.AddRange(snapshot.LoadedXScenes);
            }

            EnsureValidSelection();
            RefreshView();
        }

        private void EnsureValidSelection()
        {
            if (!TryFindXScene(m_SelectedXScenePath, out _))
            {
                m_SelectedXScenePath = m_XScenes.Count > 0 ? m_XScenes[0].XScenePath : null;
                m_SelectedUnityScenePath = null;
                if (m_DetailSelectionKind != DetailSelectionKind.None)
                {
                    m_DetailSelectionKind = DetailSelectionKind.None;
                }
            }

            if (!string.IsNullOrEmpty(m_SelectedXScenePath))
            {
                RefreshUnityScenesForSelection();
            }
            else
            {
                m_UnityScenes.Clear();
            }

            if (!string.IsNullOrEmpty(m_SelectedUnityScenePath)
                && !TryFindUnityScene(m_UnityScenes, m_SelectedUnityScenePath, out _))
            {
                m_SelectedUnityScenePath = null;
                if (m_DetailSelectionKind == DetailSelectionKind.UnityScene)
                {
                    m_DetailSelectionKind = DetailSelectionKind.None;
                }
            }
        }

        private void RefreshUnityScenesForSelection()
        {
            m_UnityScenes.Clear();
            if (string.IsNullOrEmpty(m_SelectedXScenePath) ||
                !TryFindXScene(m_SelectedXScenePath, out LoadedXSceneDebugSnapshot xScene))
            {
                return;
            }

            for (int i = 0; i < xScene.UnityScenes.Count; i++)
            {
                m_UnityScenes.Add(xScene.UnityScenes[i]);
            }
        }

        private void RefreshView()
        {
            EnsureValidSelection();
            RefreshFilteredXScenes();
            RefreshFilteredUnityScenes();
            RefreshXSceneList();
            RefreshUnitySceneList();
            RefreshSummary();
            RefreshInspectorSelection();
        }

        private void RefreshFilteredXScenes()
        {
            m_FilteredXScenes.Clear();
            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;

            for (int i = 0; i < m_XScenes.Count; i++)
            {
                LoadedXSceneDebugSnapshot entry = m_XScenes[i];
                if (!string.IsNullOrEmpty(search) && !IsXSceneSearchMatch(entry, search))
                {
                    continue;
                }

                m_FilteredXScenes.Add(entry);
            }
        }

        private void RefreshFilteredUnityScenes()
        {
            m_FilteredUnityScenes.Clear();
            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;

            for (int i = 0; i < m_UnityScenes.Count; i++)
            {
                UnitySceneDebugSnapshot entry = m_UnityScenes[i];
                if (!string.IsNullOrEmpty(search) && !IsUnitySceneSearchMatch(entry, search))
                {
                    continue;
                }

                m_FilteredUnityScenes.Add(entry);
            }
        }

        private void RefreshXSceneList()
        {
            if (m_XSceneListView == null)
            {
                return;
            }

            m_XSceneListView.itemsSource = m_FilteredXScenes;
            m_XSceneListView.Rebuild();
            int selectedIndex = GetSelectedXSceneIndex();
            if (selectedIndex >= 0)
            {
                m_XSceneListView.SetSelectionWithoutNotify(new[] { selectedIndex });
            }
        }

        private void RefreshUnitySceneList()
        {
            if (m_UnitySceneListView == null)
            {
                return;
            }

            m_UnitySceneListView.itemsSource = m_FilteredUnityScenes;
            m_UnitySceneListView.Rebuild();
            int selectedIndex = GetSelectedUnitySceneIndex();
            if (selectedIndex >= 0)
            {
                m_UnitySceneListView.SetSelectionWithoutNotify(new[] { selectedIndex });
            }
        }

        private void RefreshSummary()
        {
            if (m_SummaryLabel == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                if (m_ActiveSceneLabel != null)
                {
                    m_ActiveSceneLabel.text = "Active Scene：进入 Play Mode 后显示";
                }

                m_SummaryLabel.text = "进入 Play Mode 后会显示当前已加载 XScene。";
                return;
            }

            XSceneManagerDebugSnapshot snapshot = m_Snapshot.GetValueOrDefault();
            int totalUnityScenes = 0;
            for (int i = 0; i < m_XScenes.Count; i++)
            {
                totalUnityScenes += m_XScenes[i].UnityScenes.Count;
            }

            if (m_ActiveSceneLabel != null)
            {
                Scene activeScene = snapshot.ActiveScene;
                if (activeScene.IsValid())
                {
                    int rootCount = activeScene.isLoaded ? activeScene.rootCount : 0;
                    m_ActiveSceneLabel.text =
                        $"Active Scene：{activeScene.name}    " +
                        $"Path：{activeScene.path}    " +
                        $"BuildIndex：{activeScene.buildIndex}    " +
                        $"Roots：{rootCount}";
                }
                else
                {
                    m_ActiveSceneLabel.text = "Active Scene：<无效>";
                }
            }

            m_SummaryLabel.text =
                $"已加载 XScene：{m_XScenes.Count} | 场景类型：{snapshot.SceneTypes.Count} | " +
                $"忙碌 XScene：{snapshot.BusyXScenePaths.Count} | Unity 场景：{totalUnityScenes}";
        }

        private void RefreshInspectorSelection()
        {
            switch (m_DetailSelectionKind)
            {
                case DetailSelectionKind.XScene:
                    if (!TryFindXScene(m_SelectedXScenePath, out _))
                    {
                        m_DetailSelectionKind = DetailSelectionKind.None;
                        XFrameworkInspectorWindow.ClearIfOwner(this);
                        return;
                    }

                    XFrameworkInspectorWindow.RefreshIfOwner(this);
                    return;

                case DetailSelectionKind.UnityScene:
                    if (string.IsNullOrEmpty(m_SelectedUnityScenePath) ||
                        !TryFindUnityScene(m_FilteredUnityScenes, m_SelectedUnityScenePath, out _))
                    {
                        m_SelectedUnityScenePath = null;
                        m_DetailSelectionKind = DetailSelectionKind.None;
                        XFrameworkInspectorWindow.ClearIfOwner(this);
                        return;
                    }

                    XFrameworkInspectorWindow.RefreshIfOwner(this);
                    return;

                default:
                    XFrameworkInspectorWindow.ClearIfOwner(this);
                    return;
            }
        }

        private VisualElement MakeXSceneItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.Add(CreateCellLabel("path", 0f, bold: true, flexShrink: true));

            Label filler = CreateCellLabel("filler", 0f);
            filler.style.flexGrow = 1f;
            row.Add(filler);

            row.Add(CreateCellLabel("type", 80f, flexShrink: true));
            row.Add(CreateCellLabel("status", 62f, flexShrink: true));
            row.Add(CreateCellLabel("order", 70f, flexShrink: true));
            row.Add(CreateCellLabel("busy", 36f, flexShrink: true));
            return row;
        }

        private void BindXSceneItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_FilteredXScenes.Count)
            {
                return;
            }

            LoadedXSceneDebugSnapshot entry = m_FilteredXScenes[index];
            element.style.backgroundColor = entry.XScenePath == m_SelectedXScenePath
                ? new Color(0.24f, 0.42f, 0.72f, 0.45f)
                : index % 2 == 0
                    ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                    : new Color(0.31f, 0.31f, 0.31f, 0.18f);
            element.tooltip = entry.XScenePath;

            element.Q<Label>("path").text = FormatEmpty(entry.XScenePath);
            element.Q<Label>("path").style.color = entry.IsActive
                ? new Color(0.45f, 0.90f, 0.48f)
                : new Color(0.86f, 0.86f, 0.86f);
            element.Q<Label>("type").text = FormatEmpty(entry.SceneTypeName);
            Label statusLabel = element.Q<Label>("status");
            statusLabel.text = entry.IsActive ? "激活" : "未激活";
            statusLabel.style.color = entry.IsActive
                ? new Color(0.45f, 0.90f, 0.48f)
                : new Color(0.95f, 0.78f, 0.35f);
            element.Q<Label>("order").text = entry.LoadOrder.ToString();
            Label busyLabel = element.Q<Label>("busy");
            busyLabel.text = entry.IsBusy ? "是" : "-";
            busyLabel.style.color = entry.IsBusy
                ? new Color(1f, 0.48f, 0.42f)
                : new Color(0.55f, 0.55f, 0.55f);
        }

        private VisualElement MakeUnitySceneItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.Add(CreateCellLabel("path", 0f, bold: true, flexShrink: true));

            Label filler = CreateCellLabel("filler", 0f);
            filler.style.flexGrow = 1f;
            row.Add(filler);

            row.Add(CreateCellLabel("name", 130f, flexShrink: true));
            row.Add(CreateCellLabel("loaded", 62f, flexShrink: true));
            row.Add(CreateCellLabel("roots", 50f, flexShrink: true));
            return row;
        }

        private void BindUnitySceneItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_FilteredUnityScenes.Count)
            {
                return;
            }

            UnitySceneDebugSnapshot entry = m_FilteredUnityScenes[index];
            element.style.backgroundColor = entry.Path == m_SelectedUnityScenePath
                ? new Color(0.24f, 0.42f, 0.72f, 0.45f)
                : index % 2 == 0
                    ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                    : new Color(0.31f, 0.31f, 0.31f, 0.18f);
            element.tooltip = entry.Path;

            element.Q<Label>("path").text = FormatEmpty(entry.Path);
            element.Q<Label>("name").text = FormatEmpty(entry.Name);
            Label loadedLabel = element.Q<Label>("loaded");
            loadedLabel.text = entry.IsLoaded ? "是" : "否";
            loadedLabel.style.color = entry.IsLoaded
                ? new Color(0.45f, 0.90f, 0.48f)
                : new Color(1f, 0.48f, 0.42f);
            element.Q<Label>("roots").text = entry.RootCount.ToString();
        }

        private void OnXSceneSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is LoadedXSceneDebugSnapshot xScene)
                {
                    m_SelectedXScenePath = xScene.XScenePath;
                    m_SelectedUnityScenePath = null;
                    m_DetailSelectionKind = DetailSelectionKind.XScene;
                    RefreshView();
                    ShowXSceneDetail(true);
                    return;
                }
            }
        }

        private void OnUnitySceneSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is UnitySceneDebugSnapshot unityScene)
                {
                    m_SelectedUnityScenePath = unityScene.Path;
                    m_DetailSelectionKind = DetailSelectionKind.UnityScene;
                    RefreshView();
                    ShowUnitySceneDetail(true);
                    return;
                }
            }

            m_SelectedUnityScenePath = null;
            if (m_DetailSelectionKind == DetailSelectionKind.UnityScene)
            {
                m_DetailSelectionKind = DetailSelectionKind.None;
                XFrameworkInspectorWindow.ClearIfOwner(this);
            }
        }

        private void ShowXSceneDetail(bool openInspector)
        {
            if (string.IsNullOrEmpty(m_SelectedXScenePath) ||
                !TryFindXScene(m_SelectedXScenePath, out LoadedXSceneDebugSnapshot xScene))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(
                    this,
                    GetXSceneDisplayName(xScene),
                    BuildXSceneInspectorContent,
                    xScene.SceneTypeName);
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void ShowUnitySceneDetail(bool openInspector)
        {
            if (string.IsNullOrEmpty(m_SelectedUnityScenePath) ||
                !TryFindUnityScene(m_UnityScenes, m_SelectedUnityScenePath, out UnitySceneDebugSnapshot unityScene))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(
                    this,
                    unityScene.Name,
                    BuildUnitySceneInspectorContent,
                    unityScene.Path);
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void BuildXSceneInspectorContent(VisualElement parent)
        {
            if (string.IsNullOrEmpty(m_SelectedXScenePath) ||
                !TryFindXScene(m_SelectedXScenePath, out LoadedXSceneDebugSnapshot xScene))
            {
                Label emptyLabel = new(Application.isPlaying
                    ? "从 XScene Manager Debugger 左侧选择一个 XScene。"
                    : "进入 Play Mode 后会显示当前已加载 XScene。");
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
                emptyLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                parent.Add(emptyLabel);
                return;
            }

            parent.Add(BuildXSceneActionSection(xScene));
            parent.Add(BuildXSceneIdentitySection(xScene));
            parent.Add(BuildXSceneUnityScenesSection(xScene));
            parent.Add(BuildSceneTypesSection());
        }

        private void BuildUnitySceneInspectorContent(VisualElement parent)
        {
            if (string.IsNullOrEmpty(m_SelectedUnityScenePath) ||
                !TryFindUnityScene(m_UnityScenes, m_SelectedUnityScenePath, out UnitySceneDebugSnapshot unityScene))
            {
                Label emptyLabel = new(Application.isPlaying
                    ? "从 XScene Manager Debugger 右侧选择一个 Unity 场景。"
                    : "进入 Play Mode 后会显示当前 Unity 场景。");
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
                emptyLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                parent.Add(emptyLabel);
                return;
            }

            parent.Add(BuildUnitySceneActionSection(unityScene));
            parent.Add(BuildUnitySceneIdentitySection(unityScene));
            parent.Add(BuildUnitySceneRootsSection(unityScene));
        }

        private VisualElement BuildXSceneActionSection(LoadedXSceneDebugSnapshot xScene)
        {
            VisualElement section = CreateSection("XScene Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("Unload",
                () => TriggerUnload(xScene.XScenePath),
                !xScene.IsBusy));
            buttonRow.Add(CreateActionButton(xScene.IsActive ? "Set Inactive" : "Set Active",
                () => TriggerSetActive(xScene.XScenePath, !xScene.IsActive),
                !xScene.IsBusy));
            buttonRow.Add(CreateActionButton("Ping XScene Asset",
                () => PingObject(xScene.XScene),
                xScene.XScene != null));
            buttonRow.Add(CreateActionButton("复制路径",
                () => CopyToClipboard(xScene.XScenePath),
                !string.IsNullOrEmpty(xScene.XScenePath)));
            section.Add(buttonRow);
            return section;
        }

        private VisualElement BuildXSceneIdentitySection(LoadedXSceneDebugSnapshot xScene)
        {
            VisualElement section = CreateSection("Identity", marginBottom: 12f);
            section.Add(CreateInfoRow("XScene Path", xScene.XScenePath));
            section.Add(CreateInfoRow("Scene Type", xScene.SceneTypeName));
            section.Add(CreateInfoRow("Active Priority", xScene.ActivePriority.ToString()));
            section.Add(CreateInfoRow("UnloadOnMainSceneChanged", FormatBool(xScene.UnloadOnMainSceneChanged)));
            section.Add(CreateInfoRow("Load Order", xScene.LoadOrder.ToString()));
            section.Add(CreateInfoRow("Is Active", FormatBool(xScene.IsActive)));
            section.Add(CreateInfoRow("Is Busy", FormatBool(xScene.IsBusy)));
            section.Add(CreateInfoRow("Unity Scene Count", xScene.UnityScenes.Count.ToString()));
            section.Add(CreateInfoRow("Root GameObject Count", xScene.RootGameObjectCount.ToString()));
            return section;
        }

        private VisualElement BuildXSceneUnityScenesSection(LoadedXSceneDebugSnapshot xScene)
        {
            VisualElement section = CreateSection("Unity Scenes", marginBottom: 12f);
            if (xScene.UnityScenes.Count == 0)
            {
                section.Add(CreateInfoRow("Unity Scenes", "无"));
                return section;
            }

            for (int i = 0; i < xScene.UnityScenes.Count; i++)
            {
                UnitySceneDebugSnapshot unityScene = xScene.UnityScenes[i];
                VisualElement row = new()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        minHeight = 24f,
                        marginTop = 2f,
                        marginBottom = 2f
                    }
                };

                Label name = new(FormatUnitySceneDisplay(unityScene));
                name.style.flexGrow = 1f;
                name.style.minWidth = 0f;
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(name);

                Button selectButton = new(() => SelectUnitySceneFromXScene(unityScene.Path))
                {
                    text = "查看"
                };
                selectButton.style.width = 54f;
                selectButton.style.marginLeft = 6f;
                row.Add(selectButton);

                Button assetButton = new(() => SelectSceneAsset(unityScene.Path))
                {
                    text = "选中资源"
                };
                assetButton.style.width = 72f;
                assetButton.style.marginLeft = 4f;
                row.Add(assetButton);

                section.Add(row);
            }

            return section;
        }

        private VisualElement BuildSceneTypesSection()
        {
            VisualElement section = CreateSection("Scene Types", marginBottom: 12f);
            if (!m_Snapshot.HasValue)
            {
                section.Add(CreateInfoRow("Scene Types", "无"));
                return section;
            }

            XSceneManagerDebugSnapshot snapshot = m_Snapshot.Value;
            if (snapshot.SceneTypes.Count == 0)
            {
                section.Add(CreateInfoRow("Scene Types", "无"));
                return section;
            }

            for (int i = 0; i < snapshot.SceneTypes.Count; i++)
            {
                XSceneTypeDebugSnapshot sceneType = snapshot.SceneTypes[i];
                string capacity = sceneType.MaxLoadedSceneCount == int.MaxValue
                    ? "∞"
                    : sceneType.MaxLoadedSceneCount.ToString();
                section.Add(CreateInfoRow(
                    sceneType.Name,
                    $"Loaded: {sceneType.LoadedCount} / {capacity} | Priority: {sceneType.ActivePriority} | UnloadOnMainChanged: {FormatBool(sceneType.UnloadOnMainSceneChanged)}",
                    labelWidth: 90f));
            }

            return section;
        }

        private VisualElement BuildUnitySceneActionSection(UnitySceneDebugSnapshot unityScene)
        {
            VisualElement section = CreateSection("Unity Scene Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("选中资源",
                () => SelectSceneAsset(unityScene.Path),
                !string.IsNullOrEmpty(unityScene.Path)));
            buttonRow.Add(CreateActionButton("Ping 资源",
                () => PingSceneAsset(unityScene.Path),
                !string.IsNullOrEmpty(unityScene.Path)));
            buttonRow.Add(CreateActionButton("复制路径",
                () => CopyToClipboard(unityScene.Path),
                !string.IsNullOrEmpty(unityScene.Path)));
            section.Add(buttonRow);
            return section;
        }

        private VisualElement BuildUnitySceneIdentitySection(UnitySceneDebugSnapshot unityScene)
        {
            VisualElement section = CreateSection("Identity", marginBottom: 12f);
            section.Add(CreateInfoRow("Path", unityScene.Path));
            section.Add(CreateInfoRow("Name", FormatEmpty(unityScene.Name)));
            section.Add(CreateInfoRow("Is Valid", FormatBool(unityScene.IsValid)));
            section.Add(CreateInfoRow("Is Loaded", FormatBool(unityScene.IsLoaded)));
            section.Add(CreateInfoRow("Root Count", unityScene.RootCount.ToString()));
            if (unityScene.Scene.IsValid())
            {
                section.Add(CreateInfoRow("Build Index", unityScene.Scene.buildIndex.ToString()));
                section.Add(CreateInfoRow("Is Dirty", FormatBool(unityScene.Scene.isDirty)));
            }

            return section;
        }

        private VisualElement BuildUnitySceneRootsSection(UnitySceneDebugSnapshot unityScene)
        {
            VisualElement section = CreateSection("Root GameObjects", marginBottom: 12f);
            if (!unityScene.IsValid || !unityScene.IsLoaded)
            {
                section.Add(CreateInfoRow("Root GameObjects", "场景未加载"));
                return section;
            }

            GameObject[] roots = unityScene.Scene.GetRootGameObjects();
            if (roots.Length == 0)
            {
                section.Add(CreateInfoRow("Root GameObjects", "无"));
                return section;
            }

            int previewCount = Math.Min(roots.Length, 20);
            for (int i = 0; i < previewCount; i++)
            {
                GameObject root = roots[i];
                VisualElement row = new()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        minHeight = 24f,
                        marginTop = 2f,
                        marginBottom = 2f
                    }
                };

                Label name = new(root.name);
                name.style.flexGrow = 1f;
                name.style.minWidth = 0f;
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(name);

                Button selectButton = new(() => SelectObject(root))
                {
                    text = "选中"
                };
                selectButton.style.width = 54f;
                selectButton.style.marginLeft = 6f;
                row.Add(selectButton);

                Button pingButton = new(() => PingObject(root))
                {
                    text = "Ping"
                };
                pingButton.style.width = 54f;
                pingButton.style.marginLeft = 4f;
                row.Add(pingButton);

                section.Add(row);
            }

            if (roots.Length > previewCount)
            {
                section.Add(CreateInfoRow("More", $"还有 {roots.Length - previewCount} 个根物体未展开。"));
            }

            return section;
        }

        private void SelectUnitySceneFromXScene(string unityScenePath)
        {
            if (string.IsNullOrEmpty(unityScenePath) ||
                !TryFindUnityScene(m_UnityScenes, unityScenePath, out _))
            {
                return;
            }

            m_SelectedUnityScenePath = unityScenePath;
            m_DetailSelectionKind = DetailSelectionKind.UnityScene;
            RefreshView();
            ShowUnitySceneDetail(true);
        }

        private void TriggerUnload(string xScenePath)
        {
            if (string.IsNullOrEmpty(xScenePath))
            {
                return;
            }

            _ = XSceneManager.UnloadSceneAsync(xScenePath);
            MarkRefreshDirty();
        }

        private void TriggerSetActive(string xScenePath, bool active)
        {
            if (string.IsNullOrEmpty(xScenePath))
            {
                return;
            }

            XSceneManager.SetActive(xScenePath, active);
            MarkRefreshDirty();
        }

        private int GetSelectedXSceneIndex()
        {
            if (string.IsNullOrEmpty(m_SelectedXScenePath))
            {
                return -1;
            }

            for (int i = 0; i < m_FilteredXScenes.Count; i++)
            {
                if (m_FilteredXScenes[i].XScenePath == m_SelectedXScenePath)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetSelectedUnitySceneIndex()
        {
            if (string.IsNullOrEmpty(m_SelectedUnityScenePath))
            {
                return -1;
            }

            for (int i = 0; i < m_FilteredUnityScenes.Count; i++)
            {
                if (m_FilteredUnityScenes[i].Path == m_SelectedUnityScenePath)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryFindXScene(string xScenePath, out LoadedXSceneDebugSnapshot xScene)
        {
            if (!string.IsNullOrEmpty(xScenePath))
            {
                for (int i = 0; i < m_XScenes.Count; i++)
                {
                    if (m_XScenes[i].XScenePath == xScenePath)
                    {
                        xScene = m_XScenes[i];
                        return true;
                    }
                }
            }

            xScene = default;
            return false;
        }

        private static bool TryFindUnityScene(
            List<UnitySceneDebugSnapshot> scenes,
            string unityScenePath,
            out UnitySceneDebugSnapshot scene)
        {
            if (!string.IsNullOrEmpty(unityScenePath))
            {
                for (int i = 0; i < scenes.Count; i++)
                {
                    if (scenes[i].Path == unityScenePath)
                    {
                        scene = scenes[i];
                        return true;
                    }
                }
            }

            scene = default;
            return false;
        }

        private static bool IsXSceneSearchMatch(LoadedXSceneDebugSnapshot entry, string search)
        {
            if (Contains(entry.XScenePath, search) || Contains(entry.SceneTypeName, search))
            {
                return true;
            }

            for (int i = 0; i < entry.UnityScenes.Count; i++)
            {
                if (Contains(entry.UnityScenes[i].Path, search))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnitySceneSearchMatch(UnitySceneDebugSnapshot entry, string search)
        {
            return Contains(entry.Path, search) || Contains(entry.Name, search);
        }

        private static bool Contains(string text, string search)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetXSceneDisplayName(LoadedXSceneDebugSnapshot xScene)
        {
            if (xScene.XScene != null)
            {
                return xScene.XScene.name;
            }

            return string.IsNullOrEmpty(xScene.XScenePath) ? "XScene" : xScene.XScenePath;
        }

        private static string FormatUnitySceneDisplay(UnitySceneDebugSnapshot unityScene)
        {
            string loaded = unityScene.IsLoaded ? "[Loaded]" : "[Unloaded]";
            return $"{loaded} {FormatEmpty(unityScene.Path)}";
        }

        private static void SelectObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
        }

        private static void PingObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(target);
        }

        private static void SelectSceneAsset(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return;
            }

            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static void PingSceneAsset(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                return;
            }

            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static void CopyToClipboard(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = value;
        }

        private static string FormatBool(bool value)
        {
            return value ? "是" : "否";
        }

        private static string FormatEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private Button CreateActionButton(string text, Action action, bool enabled)
        {
            Button button = new(action)
            {
                text = text
            };
            button.style.marginRight = 6f;
            button.style.marginBottom = 4f;
            button.style.minWidth = 76f;
            button.SetEnabled(enabled);
            return button;
        }

        private static VisualElement CreateButtonRow()
        {
            VisualElement buttonRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginTop = 2f
                }
            };
            return buttonRow;
        }

        private enum DetailSelectionKind
        {
            None,
            XScene,
            UnityScene
        }
    }
}
