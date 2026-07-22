using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Resource;
using UObject = UnityEngine.Object;

namespace XFramework.Editor
{
    public sealed class ResourceManagerDebuggerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/Resource Manager Debugger";
        private const float AssetPaneWidth = 660f;
        private const float GroupPaneWidth = 460f;

        private readonly List<ResourceAssetDebugSnapshot> m_Assets = new();
        private readonly List<ResourceAssetDebugSnapshot> m_FilteredAssets = new();
        private readonly List<ResourceBundleDebugSnapshot> m_Bundles = new();
        private readonly List<ResourceBundleDebugSnapshot> m_FilteredBundles = new();
        private readonly List<ResourceInstanceGroupDebugSnapshot> m_InstanceGroups = new();
        private readonly List<ResourceInstanceGroupDebugSnapshot> m_FilteredInstanceGroups = new();
        private readonly List<ResourceInstanceDebugSnapshot> m_Instances = new();
        private readonly List<ResourceInstanceDebugSnapshot> m_FilteredInstances = new();

        private ResourceManagerDebugSnapshot? m_Snapshot;
        private DebugPage m_CurrentPage = DebugPage.AssetsAndBundles;
        private DetailSelectionKind m_DetailSelectionKind;

        private string m_SelectedAssetKey;
        private string m_SelectedBundlePath;
        private string m_SelectedGroupName;
        private int? m_SelectedInstanceId;

        private Button m_AssetsPageButton;
        private Button m_InstancesPageButton;
        private TextField m_SearchField;
        private Label m_SummaryLabel;
        private VisualElement m_AssetsPage;
        private VisualElement m_InstancesPage;
        private ListView m_AssetListView;
        private ListView m_BundleListView;
        private ListView m_InstanceGroupListView;
        private ListView m_InstanceListView;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            ResourceManagerDebuggerWindow window = GetWindow<ResourceManagerDebuggerWindow>();
            window.titleContent = new GUIContent("Resource Manager Debugger");
            window.minSize = new Vector2(1280f, 620f);
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

            m_SummaryLabel = new Label();
            m_SummaryLabel.style.marginTop = 4f;
            m_SummaryLabel.style.marginBottom = 6f;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(m_SummaryLabel);

            m_AssetsPage = BuildAssetsPage();
            m_InstancesPage = BuildInstancesPage();
            root.Add(m_AssetsPage);
            root.Add(m_InstancesPage);
            ApplyPageVisibility();
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

            m_AssetsPageButton = CreatePageButton("Assets / Bundles", DebugPage.AssetsAndBundles, 126f);
            m_InstancesPageButton = CreatePageButton("Instances / Pool", DebugPage.InstancesAndPool, 126f);
            toolbar.Add(m_AssetsPageButton);
            toolbar.Add(m_InstancesPageButton);

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1f;
            m_SearchField.style.minWidth = 220f;
            m_SearchField.style.marginLeft = 10f;
            m_SearchField.tooltip = "搜索当前页的资源路径、Bundle、依赖、对象名称或实例状态";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            AddRefreshControls(toolbar, "刷新 ResourceManager 当前运行快照");
            ApplyPageButtonStyles();
            return toolbar;
        }

        private Button CreatePageButton(string text, DebugPage page, float width)
        {
            Button button = new(() => SwitchPage(page))
            {
                text = text
            };
            button.style.width = width;
            button.style.height = 24f;
            button.style.marginRight = 4f;
            return button;
        }

        private VisualElement BuildAssetsPage()
        {
            TwoPaneSplitView splitView = new(0, AssetPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            splitView.Add(BuildAssetPane());
            splitView.Add(BuildBundlePane());
            return splitView;
        }

        private VisualElement BuildInstancesPage()
        {
            TwoPaneSplitView splitView = new(0, GroupPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            splitView.Add(BuildInstanceGroupPane());
            splitView.Add(BuildInstancePane());
            return splitView;
        }

        private VisualElement BuildAssetPane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginRight = 4f;
            pane.Add(CreatePaneTitle("Assets", height: 22f));

            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("路径", 270f));
            header.Add(CreateHeaderLabel("状态", 98f));
            header.Add(CreateHeaderLabel("资源对象", 150f));
            Label bundle = CreateHeaderLabel("所属 Bundle", 0f);
            bundle.style.flexGrow = 1f;
            header.Add(bundle);
            pane.Add(header);

            m_AssetListView = new ListView
            {
                itemsSource = m_FilteredAssets,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeAssetItem,
                bindItem = BindAssetItem
            };
            m_AssetListView.style.flexGrow = 1f;
            m_AssetListView.style.marginTop = 4f;
            m_AssetListView.selectionChanged += OnAssetSelectionChanged;
            pane.Add(m_AssetListView);
            return pane;
        }

        private VisualElement BuildBundlePane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginLeft = 4f;
            pane.Add(CreatePaneTitle("Bundles", height: 22f));

            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("Bundle 路径", 260f));
            header.Add(CreateHeaderLabel("状态", 98f));
            Label bundleObject = CreateHeaderLabel("Bundle 对象", 0f);
            bundleObject.style.flexGrow = 1f;
            header.Add(bundleObject);
            header.Add(CreateHeaderLabel("依赖", 48f));
            header.Add(CreateHeaderLabel("资源", 48f));
            pane.Add(header);

            m_BundleListView = new ListView
            {
                itemsSource = m_FilteredBundles,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeBundleItem,
                bindItem = BindBundleItem
            };
            m_BundleListView.style.flexGrow = 1f;
            m_BundleListView.style.marginTop = 4f;
            m_BundleListView.selectionChanged += OnBundleSelectionChanged;
            pane.Add(m_BundleListView);
            return pane;
        }

        private VisualElement BuildInstanceGroupPane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginRight = 4f;
            pane.Add(CreatePaneTitle("Resources", height: 22f));

            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            Label resource = CreateHeaderLabel("资源名", 0f);
            resource.style.flexGrow = 1f;
            header.Add(resource);
            header.Add(CreateHeaderLabel("普通", 46f));
            header.Add(CreateHeaderLabel("池活跃", 52f));
            header.Add(CreateHeaderLabel("空闲", 46f));
            header.Add(CreateHeaderLabel("合计", 46f));
            pane.Add(header);

            m_InstanceGroupListView = new ListView
            {
                itemsSource = m_FilteredInstanceGroups,
                fixedItemHeight = 28f,
                selectionType = SelectionType.Single,
                makeItem = MakeInstanceGroupItem,
                bindItem = BindInstanceGroupItem
            };
            m_InstanceGroupListView.style.flexGrow = 1f;
            m_InstanceGroupListView.style.marginTop = 4f;
            m_InstanceGroupListView.selectionChanged += OnInstanceGroupSelectionChanged;
            pane.Add(m_InstanceGroupListView);
            return pane;
        }

        private VisualElement BuildInstancePane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginLeft = 4f;
            pane.Add(CreatePaneTitle("Instances", height: 22f));

            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("状态", 94f));
            header.Add(CreateHeaderLabel("实例对象", 170f));
            Label asset = CreateHeaderLabel("原始资源", 0f);
            asset.style.flexGrow = 1f;
            header.Add(asset);
            header.Add(CreateHeaderLabel("空闲时长", 72f));
            header.Add(CreateHeaderLabel("队列", 46f));
            header.Add(CreateHeaderLabel("LRU", 46f));
            pane.Add(header);

            m_InstanceListView = new ListView
            {
                itemsSource = m_FilteredInstances,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeInstanceItem,
                bindItem = BindInstanceItem
            };
            m_InstanceListView.style.flexGrow = 1f;
            m_InstanceListView.style.marginTop = 4f;
            m_InstanceListView.selectionChanged += OnInstanceSelectionChanged;
            pane.Add(m_InstanceListView);
            return pane;
        }

        private void SwitchPage(DebugPage page)
        {
            if (m_CurrentPage == page)
            {
                return;
            }

            m_CurrentPage = page;
            m_DetailSelectionKind = DetailSelectionKind.None;
            m_SearchField.SetValueWithoutNotify(string.Empty);
            ApplyPageVisibility();
            ApplyPageButtonStyles();
            RefreshView();
            XFrameworkInspectorWindow.ClearIfOwner(this);
        }

        private void ApplyPageVisibility()
        {
            if (m_AssetsPage == null || m_InstancesPage == null)
            {
                return;
            }

            m_AssetsPage.style.display = m_CurrentPage == DebugPage.AssetsAndBundles ? DisplayStyle.Flex : DisplayStyle.None;
            m_InstancesPage.style.display = m_CurrentPage == DebugPage.InstancesAndPool ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyPageButtonStyles()
        {
            ApplyPageButtonStyle(m_AssetsPageButton, m_CurrentPage == DebugPage.AssetsAndBundles);
            ApplyPageButtonStyle(m_InstancesPageButton, m_CurrentPage == DebugPage.InstancesAndPool);
        }

        private static void ApplyPageButtonStyle(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = selected
                ? new Color(0.24f, 0.43f, 0.68f)
                : new Color(0.24f, 0.24f, 0.24f);
            button.style.color = selected ? Color.white : new Color(0.76f, 0.76f, 0.76f);
        }

        private void RefreshData()
        {
            m_Snapshot = null;
            m_Assets.Clear();
            m_Bundles.Clear();
            m_InstanceGroups.Clear();
            m_Instances.Clear();

            if (Application.isPlaying && GameEntry.IsModuleLoaded<ResourceManager>())
            {
                ResourceManagerDebugSnapshot snapshot = ResourceManager.Instance.GetDebugSnapshot();
                m_Snapshot = snapshot;
                m_Assets.AddRange(snapshot.Assets);
                m_Bundles.AddRange(snapshot.Bundles);
                m_InstanceGroups.AddRange(snapshot.InstanceGroups);
                m_Instances.AddRange(snapshot.Instances);
            }

            EnsureValidSelection();
            RefreshView();
        }

        private void EnsureValidSelection()
        {
            if (!TryFindAsset(m_Assets, m_SelectedAssetKey, out _))
            {
                m_SelectedAssetKey = null;
                ClearDetailSelection(DetailSelectionKind.Asset);
            }

            if (!TryFindBundle(m_Bundles, m_SelectedBundlePath, out _))
            {
                m_SelectedBundlePath = null;
                ClearDetailSelection(DetailSelectionKind.Bundle);
            }

            if (!TryFindInstanceGroup(m_InstanceGroups, m_SelectedGroupName, out _))
            {
                m_SelectedGroupName = m_InstanceGroups.Count > 0 ? m_InstanceGroups[0].AssetName : null;
                m_SelectedInstanceId = null;
                ClearDetailSelection(DetailSelectionKind.InstanceGroup);
                ClearDetailSelection(DetailSelectionKind.Instance);
            }

            if (m_SelectedInstanceId.HasValue
                && (!TryFindInstance(m_Instances, m_SelectedInstanceId.Value, out ResourceInstanceDebugSnapshot instance)
                    || instance.AssetName != m_SelectedGroupName))
            {
                m_SelectedInstanceId = null;
                ClearDetailSelection(DetailSelectionKind.Instance);
            }
        }

        private void ClearDetailSelection(DetailSelectionKind kind)
        {
            if (m_DetailSelectionKind == kind)
            {
                m_DetailSelectionKind = DetailSelectionKind.None;
            }
        }

        private void RefreshView()
        {
            RefreshFilteredAssets();
            RefreshFilteredBundles();
            RefreshFilteredInstanceGroups();
            RefreshFilteredInstances();
            RefreshAssetList();
            RefreshBundleList();
            RefreshInstanceGroupList();
            RefreshInstanceList();
            RefreshSummary();
            RefreshInspectorSelection();
        }

        private void RefreshFilteredAssets()
        {
            m_FilteredAssets.Clear();
            string search = GetSearchText();
            foreach (ResourceAssetDebugSnapshot asset in m_Assets)
            {
                if (string.IsNullOrEmpty(search) || IsAssetSearchMatch(asset, search))
                {
                    m_FilteredAssets.Add(asset);
                }
            }
        }

        private void RefreshFilteredBundles()
        {
            m_FilteredBundles.Clear();
            string search = GetSearchText();
            foreach (ResourceBundleDebugSnapshot bundle in m_Bundles)
            {
                if (string.IsNullOrEmpty(search) || IsBundleSearchMatch(bundle, search))
                {
                    m_FilteredBundles.Add(bundle);
                }
            }
        }

        private void RefreshFilteredInstanceGroups()
        {
            m_FilteredInstanceGroups.Clear();
            string search = GetSearchText();
            foreach (ResourceInstanceGroupDebugSnapshot group in m_InstanceGroups)
            {
                if (string.IsNullOrEmpty(search) || ContainsIgnoreCase(group.AssetName, search))
                {
                    m_FilteredInstanceGroups.Add(group);
                }
            }
        }

        private void RefreshFilteredInstances()
        {
            m_FilteredInstances.Clear();
            if (string.IsNullOrEmpty(m_SelectedGroupName))
            {
                return;
            }

            string search = GetSearchText();
            foreach (ResourceInstanceDebugSnapshot instance in m_Instances)
            {
                if (instance.AssetName != m_SelectedGroupName)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(search) || IsInstanceSearchMatch(instance, search))
                {
                    m_FilteredInstances.Add(instance);
                }
            }
        }

        private void RefreshAssetList()
        {
            if (m_AssetListView == null)
            {
                return;
            }

            m_AssetListView.itemsSource = m_FilteredAssets;
            m_AssetListView.Rebuild();
            int index = FindAssetIndex(m_FilteredAssets, m_SelectedAssetKey);
            if (index >= 0)
            {
                m_AssetListView.SetSelectionWithoutNotify(new[] { index });
            }
        }

        private void RefreshBundleList()
        {
            if (m_BundleListView == null)
            {
                return;
            }

            m_BundleListView.itemsSource = m_FilteredBundles;
            m_BundleListView.Rebuild();
            int index = FindBundleIndex(m_FilteredBundles, m_SelectedBundlePath);
            if (index >= 0)
            {
                m_BundleListView.SetSelectionWithoutNotify(new[] { index });
            }
        }

        private void RefreshInstanceGroupList()
        {
            if (m_InstanceGroupListView == null)
            {
                return;
            }

            m_InstanceGroupListView.itemsSource = m_FilteredInstanceGroups;
            m_InstanceGroupListView.Rebuild();
            int index = FindInstanceGroupIndex(m_FilteredInstanceGroups, m_SelectedGroupName);
            if (index >= 0)
            {
                m_InstanceGroupListView.SetSelectionWithoutNotify(new[] { index });
            }
        }

        private void RefreshInstanceList()
        {
            if (m_InstanceListView == null)
            {
                return;
            }

            m_InstanceListView.itemsSource = m_FilteredInstances;
            m_InstanceListView.Rebuild();
            int index = FindInstanceIndex(m_FilteredInstances, m_SelectedInstanceId);
            if (index >= 0)
            {
                m_InstanceListView.SetSelectionWithoutNotify(new[] { index });
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
                m_SummaryLabel.text = "进入 Play Mode 后会显示 ResourceManager 运行快照。";
                return;
            }

            if (!GameEntry.IsModuleLoaded<ResourceManager>())
            {
                m_SummaryLabel.text = "ResourceManager 尚未加载。";
                return;
            }

            ResourceManagerDebugSnapshot snapshot = m_Snapshot.Value;
            if (m_CurrentPage == DebugPage.AssetsAndBundles)
            {
                if (!snapshot.UsesAssetBundles)
                {
                    m_SummaryLabel.text = $"加载器：{snapshot.LoadHelperName} | AssetDatabase 模式不维护 AB 加载缓存；Instances / Pool 页面仍可使用。";
                    return;
                }

                CountLoadStates(m_Assets, out int loadedAssets, out int loadingAssets);
                CountLoadStates(m_Bundles, out int loadedBundles, out int loadingBundles);
                m_SummaryLabel.text = $"加载器：{snapshot.LoadHelperName} | 根目录：{snapshot.AssetPath} | Assets 已加载 {loadedAssets} / 加载中 {loadingAssets} | Bundles 已加载 {loadedBundles} / 加载中 {loadingBundles}";
                return;
            }

            int normalActiveCount = 0;
            int pooledActiveCount = 0;
            int freeCount = 0;
            foreach (ResourceInstanceGroupDebugSnapshot group in m_InstanceGroups)
            {
                normalActiveCount += group.NormalActiveCount;
                pooledActiveCount += group.PooledActiveCount;
                freeCount += group.FreeCount;
            }

            string selectedGroup = string.IsNullOrEmpty(m_SelectedGroupName) ? "未选择" : m_SelectedGroupName;
            m_SummaryLabel.text = $"资源组：{m_InstanceGroups.Count} | 普通活跃：{normalActiveCount} | 池化活跃：{pooledActiveCount} | 空闲：{freeCount} | 当前资源：{selectedGroup} | 当前列表：{m_FilteredInstances.Count}";
        }

        private static void CountLoadStates(List<ResourceAssetDebugSnapshot> snapshots, out int loaded, out int loading)
        {
            loaded = 0;
            loading = 0;
            foreach (ResourceAssetDebugSnapshot snapshot in snapshots)
            {
                if (snapshot.IsLoaded) loaded++;
                if (snapshot.IsLoading) loading++;
            }
        }

        private static void CountLoadStates(List<ResourceBundleDebugSnapshot> snapshots, out int loaded, out int loading)
        {
            loaded = 0;
            loading = 0;
            foreach (ResourceBundleDebugSnapshot snapshot in snapshots)
            {
                if (snapshot.IsLoaded) loaded++;
                if (snapshot.IsLoading) loading++;
            }
        }

        private VisualElement MakeAssetItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.Add(CreateCellLabel("path", 270f, bold: true, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));
            row.Add(CreateCellLabel("status", 98f, flexShrink: true));
            row.Add(CreateCellLabel("object", 150f, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));
            Label bundle = CreateCellLabel("bundle", 0f, textOverflow: TextOverflow.Ellipsis, noWrap: true);
            bundle.style.flexGrow = 1f;
            row.Add(bundle);
            return row;
        }

        private void BindAssetItem(VisualElement element, int index)
        {
            ResourceAssetDebugSnapshot asset = m_FilteredAssets[index];
            element.style.backgroundColor = GetAlternatingRowColor(index);
            Label path = element.Q<Label>("path");
            path.text = asset.CacheKey;
            path.tooltip = asset.CacheKey;
            Label status = element.Q<Label>("status");
            status.text = GetLoadStatus(asset.IsLoaded, asset.IsLoading);
            status.style.color = GetLoadStatusColor(asset.IsLoaded, asset.IsLoading);
            Label assetObject = element.Q<Label>("object");
            assetObject.text = FormatObject(asset.Asset);
            assetObject.tooltip = assetObject.text;
            Label bundle = element.Q<Label>("bundle");
            bundle.text = asset.BundlePath;
            bundle.tooltip = asset.BundlePath;
        }

        private VisualElement MakeBundleItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.Add(CreateCellLabel("path", 260f, bold: true, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));
            row.Add(CreateCellLabel("status", 98f, flexShrink: true));
            Label bundleObject = CreateCellLabel("object", 0f, textOverflow: TextOverflow.Ellipsis, noWrap: true);
            bundleObject.style.flexGrow = 1f;
            row.Add(bundleObject);
            row.Add(CreateCellLabel("dependencies", 48f, flexShrink: true));
            row.Add(CreateCellLabel("assets", 48f, flexShrink: true));
            return row;
        }

        private void BindBundleItem(VisualElement element, int index)
        {
            ResourceBundleDebugSnapshot bundle = m_FilteredBundles[index];
            element.style.backgroundColor = GetAlternatingRowColor(index);
            Label path = element.Q<Label>("path");
            path.text = bundle.BundlePath;
            path.tooltip = bundle.BundlePath;
            Label status = element.Q<Label>("status");
            status.text = GetLoadStatus(bundle.IsLoaded, bundle.IsLoading);
            status.style.color = GetLoadStatusColor(bundle.IsLoaded, bundle.IsLoading);
            Label bundleObject = element.Q<Label>("object");
            bundleObject.text = FormatObject(bundle.Bundle);
            bundleObject.tooltip = bundleObject.text;
            element.Q<Label>("dependencies").text = bundle.DirectDependencies.Count.ToString();
            element.Q<Label>("assets").text = GetTrackedAssetCount(bundle.BundlePath).ToString();
        }

        private VisualElement MakeInstanceGroupItem()
        {
            VisualElement row = CreateRow(Color.clear, 28f);
            Label resource = CreateCellLabel("resource", 0f, bold: true, textOverflow: TextOverflow.Ellipsis, noWrap: true);
            resource.style.flexGrow = 1f;
            row.Add(resource);
            row.Add(CreateCellLabel("normal", 46f, flexShrink: true));
            row.Add(CreateCellLabel("pooled", 52f, flexShrink: true));
            row.Add(CreateCellLabel("free", 46f, flexShrink: true));
            row.Add(CreateCellLabel("total", 46f, flexShrink: true));
            return row;
        }

        private void BindInstanceGroupItem(VisualElement element, int index)
        {
            ResourceInstanceGroupDebugSnapshot group = m_FilteredInstanceGroups[index];
            element.style.backgroundColor = GetAlternatingRowColor(index);
            Label resource = element.Q<Label>("resource");
            resource.text = group.AssetName;
            resource.tooltip = group.AssetName;
            element.Q<Label>("normal").text = group.NormalActiveCount.ToString();
            element.Q<Label>("pooled").text = group.PooledActiveCount.ToString();
            element.Q<Label>("free").text = group.FreeCount.ToString();
            element.Q<Label>("total").text = group.TotalCount.ToString();
        }

        private VisualElement MakeInstanceItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.Add(CreateCellLabel("status", 94f, flexShrink: true));
            row.Add(CreateCellLabel("instance", 170f, bold: true, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));
            Label asset = CreateCellLabel("asset", 0f, textOverflow: TextOverflow.Ellipsis, noWrap: true);
            asset.style.flexGrow = 1f;
            row.Add(asset);
            row.Add(CreateCellLabel("duration", 72f, flexShrink: true));
            row.Add(CreateCellLabel("queue", 46f, flexShrink: true));
            row.Add(CreateCellLabel("lru", 46f, flexShrink: true));
            return row;
        }

        private void BindInstanceItem(VisualElement element, int index)
        {
            ResourceInstanceDebugSnapshot instance = m_FilteredInstances[index];
            element.style.backgroundColor = GetAlternatingRowColor(index);
            Label status = element.Q<Label>("status");
            status.text = GetInstanceStateText(instance.State);
            status.style.color = GetInstanceStateColor(instance.State);
            Label instanceObject = element.Q<Label>("instance");
            instanceObject.text = FormatObject(instance.Instance);
            instanceObject.tooltip = instanceObject.text;
            Label asset = element.Q<Label>("asset");
            asset.text = FormatObject(instance.Asset);
            asset.tooltip = asset.text;
            bool isFree = instance.State == ResourceInstanceDebugState.PooledFree;
            element.Q<Label>("duration").text = isFree ? $"{instance.FreeDuration:F1}s" : "-";
            element.Q<Label>("queue").text = isFree ? instance.FreeQueueOrder.ToString() : "-";
            element.Q<Label>("lru").text = isFree ? instance.LruOrder.ToString() : "-";
        }

        private void OnAssetSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is ResourceAssetDebugSnapshot asset)
                {
                    m_SelectedAssetKey = asset.CacheKey;
                    m_DetailSelectionKind = DetailSelectionKind.Asset;
                    ShowAssetDetail(true);
                    return;
                }
            }
        }

        private void OnBundleSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is ResourceBundleDebugSnapshot bundle)
                {
                    m_SelectedBundlePath = bundle.BundlePath;
                    m_DetailSelectionKind = DetailSelectionKind.Bundle;
                    ShowBundleDetail(true);
                    return;
                }
            }
        }

        private void OnInstanceGroupSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is ResourceInstanceGroupDebugSnapshot group)
                {
                    m_SelectedGroupName = group.AssetName;
                    m_SelectedInstanceId = null;
                    m_DetailSelectionKind = DetailSelectionKind.InstanceGroup;
                    RefreshFilteredInstances();
                    RefreshInstanceList();
                    RefreshSummary();
                    ShowInstanceGroupDetail(true);
                    return;
                }
            }
        }

        private void OnInstanceSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is ResourceInstanceDebugSnapshot instance)
                {
                    m_SelectedGroupName = instance.AssetName;
                    m_SelectedInstanceId = instance.InstanceId;
                    m_DetailSelectionKind = DetailSelectionKind.Instance;
                    ShowInstanceDetail(true);
                    return;
                }
            }
        }

        private void RefreshInspectorSelection()
        {
            if (!IsDetailKindOnCurrentPage(m_DetailSelectionKind))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            switch (m_DetailSelectionKind)
            {
                case DetailSelectionKind.Asset:
                    ShowAssetDetail(false);
                    break;
                case DetailSelectionKind.Bundle:
                    ShowBundleDetail(false);
                    break;
                case DetailSelectionKind.InstanceGroup:
                    ShowInstanceGroupDetail(false);
                    break;
                case DetailSelectionKind.Instance:
                    ShowInstanceDetail(false);
                    break;
                default:
                    XFrameworkInspectorWindow.ClearIfOwner(this);
                    break;
            }
        }

        private bool IsDetailKindOnCurrentPage(DetailSelectionKind kind)
        {
            return m_CurrentPage switch
            {
                DebugPage.AssetsAndBundles => kind is DetailSelectionKind.Asset or DetailSelectionKind.Bundle,
                DebugPage.InstancesAndPool => kind is DetailSelectionKind.InstanceGroup or DetailSelectionKind.Instance,
                _ => false
            };
        }

        private void ShowAssetDetail(bool openInspector)
        {
            if (!TryFindAsset(m_Assets, m_SelectedAssetKey, out ResourceAssetDebugSnapshot asset))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(this, asset.CacheKey, BuildAssetInspectorContent, "Resource Asset");
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void ShowBundleDetail(bool openInspector)
        {
            if (!TryFindBundle(m_Bundles, m_SelectedBundlePath, out ResourceBundleDebugSnapshot bundle))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(this, bundle.BundlePath, BuildBundleInspectorContent, "AssetBundle");
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void ShowInstanceGroupDetail(bool openInspector)
        {
            if (!TryFindInstanceGroup(m_InstanceGroups, m_SelectedGroupName, out ResourceInstanceGroupDebugSnapshot group))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(this, group.AssetName, BuildInstanceGroupInspectorContent, "Resource Instance Group");
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void ShowInstanceDetail(bool openInspector)
        {
            if (!m_SelectedInstanceId.HasValue
                || !TryFindInstance(m_Instances, m_SelectedInstanceId.Value, out ResourceInstanceDebugSnapshot instance))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(this, FormatObject(instance.Instance), BuildInstanceInspectorContent, "Resource Instance");
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void BuildAssetInspectorContent(VisualElement parent)
        {
            if (!TryFindAsset(m_Assets, m_SelectedAssetKey, out ResourceAssetDebugSnapshot asset))
            {
                parent.Add(CreateMutedLabel("所选资源已不在 ResourceManager 快照中。", wrap: true));
                return;
            }

            VisualElement actions = CreateSection("Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("选中资源", () => SelectObject(asset.Asset), asset.Asset != null));
            buttonRow.Add(CreateActionButton("Ping资源", () => PingObject(asset.Asset), asset.Asset != null));
            buttonRow.Add(CreateActionButton("查看所属AB", () => SelectBundle(asset.BundlePath), !string.IsNullOrEmpty(asset.BundlePath)));
            buttonRow.Add(CreateActionButton("复制路径", () => CopyToClipboard(asset.CacheKey), true));
            actions.Add(buttonRow);
            parent.Add(actions);

            VisualElement identity = CreateSection("Asset", marginBottom: 12f);
            identity.Add(CreateDebugInfoRow("Cache Key", asset.CacheKey));
            identity.Add(CreateDebugInfoRow("Asset Path", asset.AssetPath));
            identity.Add(CreateDebugInfoRow("Sub Asset", FormatEmpty(asset.SubAssetName)));
            identity.Add(CreateDebugInfoRow("Bundle", asset.BundlePath));
            identity.Add(CreateDebugInfoRow("Status", GetLoadStatus(asset.IsLoaded, asset.IsLoading)));
            identity.Add(CreateObjectFieldRow("Asset", typeof(UObject), asset.Asset));
            parent.Add(identity);
        }

        private void BuildBundleInspectorContent(VisualElement parent)
        {
            if (!TryFindBundle(m_Bundles, m_SelectedBundlePath, out ResourceBundleDebugSnapshot bundle))
            {
                parent.Add(CreateMutedLabel("所选 Bundle 已不在 ResourceManager 快照中。", wrap: true));
                return;
            }

            VisualElement actions = CreateSection("Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("选中对象", () => SelectObject(bundle.Bundle), bundle.Bundle != null));
            buttonRow.Add(CreateActionButton("Ping对象", () => PingObject(bundle.Bundle), bundle.Bundle != null));
            buttonRow.Add(CreateActionButton("复制路径", () => CopyToClipboard(bundle.BundlePath), true));
            actions.Add(buttonRow);
            parent.Add(actions);

            VisualElement identity = CreateSection("Bundle", marginBottom: 12f);
            identity.Add(CreateDebugInfoRow("Path", bundle.BundlePath));
            identity.Add(CreateDebugInfoRow("Status", GetLoadStatus(bundle.IsLoaded, bundle.IsLoading)));
            identity.Add(CreateDebugInfoRow("Tracked Assets", GetTrackedAssetCount(bundle.BundlePath).ToString()));
            identity.Add(CreateObjectFieldRow("Bundle", typeof(AssetBundle), bundle.Bundle));
            parent.Add(identity);

            VisualElement dependencies = CreateSection("Direct Dependencies", marginBottom: 12f);
            if (bundle.DirectDependencies.Count == 0)
            {
                dependencies.Add(CreateDebugInfoRow("Dependencies", "无"));
            }
            else
            {
                for (int i = 0; i < bundle.DirectDependencies.Count; i++)
                {
                    string dependency = bundle.DirectDependencies[i];
                    dependencies.Add(CreateNavigationRow($"[{i + 1}]", dependency, () => SelectBundle(dependency)));
                }
            }
            parent.Add(dependencies);

            VisualElement trackedAssets = CreateSection("Tracked Assets", marginBottom: 12f);
            int trackedIndex = 0;
            foreach (ResourceAssetDebugSnapshot asset in m_Assets)
            {
                if (asset.BundlePath != bundle.BundlePath)
                {
                    continue;
                }

                ResourceAssetDebugSnapshot capturedAsset = asset;
                trackedAssets.Add(CreateNavigationRow($"[{++trackedIndex}]", asset.CacheKey, () => SelectAsset(capturedAsset.CacheKey)));
            }
            if (trackedIndex == 0)
            {
                trackedAssets.Add(CreateDebugInfoRow("Assets", "无"));
            }
            parent.Add(trackedAssets);
        }

        private void BuildInstanceGroupInspectorContent(VisualElement parent)
        {
            if (!TryFindInstanceGroup(m_InstanceGroups, m_SelectedGroupName, out ResourceInstanceGroupDebugSnapshot group))
            {
                parent.Add(CreateMutedLabel("所选资源组已不在 ResourceManager 快照中。", wrap: true));
                return;
            }

            VisualElement actions = CreateSection("Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("复制资源名", () => CopyToClipboard(group.AssetName), true));
            actions.Add(buttonRow);
            parent.Add(actions);

            VisualElement identity = CreateSection("Instance Group", marginBottom: 12f);
            identity.Add(CreateDebugInfoRow("Asset Name", group.AssetName));
            identity.Add(CreateDebugInfoRow("Normal Active", group.NormalActiveCount.ToString()));
            identity.Add(CreateDebugInfoRow("Pooled Active", group.PooledActiveCount.ToString()));
            identity.Add(CreateDebugInfoRow("Free Queue", group.FreeCount.ToString()));
            identity.Add(CreateDebugInfoRow("Total", group.TotalCount.ToString()));
            parent.Add(identity);
        }

        private void BuildInstanceInspectorContent(VisualElement parent)
        {
            if (!m_SelectedInstanceId.HasValue
                || !TryFindInstance(m_Instances, m_SelectedInstanceId.Value, out ResourceInstanceDebugSnapshot instance))
            {
                parent.Add(CreateMutedLabel("所选实例已不在 ResourceManager 快照中。", wrap: true));
                return;
            }

            UObject target = GetSelectableObject(instance.Instance);
            VisualElement actions = CreateSection("Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("选中实例", () => SelectObject(target), target != null));
            buttonRow.Add(CreateActionButton("Ping实例", () => PingObject(target), target != null));
            buttonRow.Add(CreateActionButton("选中资源", () => SelectObject(instance.Asset), instance.Asset != null));
            buttonRow.Add(CreateActionButton("复制资源名", () => CopyToClipboard(instance.AssetName), true));
            actions.Add(buttonRow);
            parent.Add(actions);

            VisualElement identity = CreateSection("Instance", marginBottom: 12f);
            identity.Add(CreateDebugInfoRow("Instance Id", instance.InstanceId.ToString()));
            identity.Add(CreateDebugInfoRow("Asset Name", instance.AssetName));
            identity.Add(CreateDebugInfoRow("State", GetInstanceStateText(instance.State)));
            identity.Add(CreateObjectFieldRow("Instance", typeof(UObject), instance.Instance));
            identity.Add(CreateObjectFieldRow("Source Asset", typeof(UObject), instance.Asset));
            identity.Add(CreateDebugInfoRow("Hierarchy", GetHierarchyPath(target)));
            parent.Add(identity);

            VisualElement pool = CreateSection("Pool", marginBottom: 12f);
            bool isFree = instance.State == ResourceInstanceDebugState.PooledFree;
            pool.Add(CreateDebugInfoRow("Pooled", instance.State == ResourceInstanceDebugState.NormalActive ? "否" : "是"));
            pool.Add(CreateDebugInfoRow("Free Duration", isFree ? $"{instance.FreeDuration:F1}s" : "-"));
            pool.Add(CreateDebugInfoRow("Queue Order", isFree ? instance.FreeQueueOrder.ToString() : "-"));
            pool.Add(CreateDebugInfoRow("LRU Order", isFree ? instance.LruOrder.ToString() : "-"));
            parent.Add(pool);
        }

        private void SelectAsset(string cacheKey)
        {
            if (!TryFindAsset(m_Assets, cacheKey, out _))
            {
                return;
            }

            m_SelectedAssetKey = cacheKey;
            m_DetailSelectionKind = DetailSelectionKind.Asset;
            m_SearchField.SetValueWithoutNotify(string.Empty);
            RefreshView();
            ShowAssetDetail(true);
        }

        private void SelectBundle(string bundlePath)
        {
            if (!TryFindBundle(m_Bundles, bundlePath, out _))
            {
                return;
            }

            m_SelectedBundlePath = bundlePath;
            m_DetailSelectionKind = DetailSelectionKind.Bundle;
            m_SearchField.SetValueWithoutNotify(string.Empty);
            RefreshView();
            ShowBundleDetail(true);
        }

        private VisualElement CreateNavigationRow(string labelText, string valueText, Action action)
        {
            VisualElement row = CreateInspectorRow(labelText);
            Label value = new(valueText);
            value.style.flexGrow = 1f;
            value.style.minWidth = 0f;
            value.style.overflow = Overflow.Hidden;
            value.style.textOverflow = TextOverflow.Ellipsis;
            value.tooltip = valueText;
            row.Add(value);

            Button button = new(action)
            {
                text = "查看"
            };
            button.style.width = 54f;
            button.style.marginLeft = 6f;
            row.Add(button);
            return row;
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

        private static VisualElement CreateObjectFieldRow(string labelText, Type objectType, UObject value)
        {
            ObjectField field = new()
            {
                objectType = objectType,
                value = value,
                allowSceneObjects = true
            };
            field.SetEnabled(false);
            field.style.flexGrow = 1f;
            field.style.minWidth = 0f;
            return CreateInspectorFieldRow(labelText, field);
        }

        private static VisualElement CreateDebugInfoRow(string labelText, string valueText)
        {
            Label value = new(FormatEmpty(valueText));
            value.style.flexGrow = 1f;
            value.style.whiteSpace = WhiteSpace.Normal;
            value.style.color = new Color(0.86f, 0.86f, 0.86f);
            return CreateInspectorFieldRow(labelText, value);
        }

        private static VisualElement CreateInspectorFieldRow(string labelText, VisualElement field)
        {
            VisualElement row = CreateInspectorRow(labelText);
            field.style.flexGrow = 1f;
            row.Add(field);
            return row;
        }

        private static VisualElement CreateInspectorRow(string labelText)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 24f,
                    marginTop = 1f,
                    marginBottom = 1f
                }
            };

            Label label = new(labelText);
            label.style.width = 112f;
            label.style.minWidth = 112f;
            label.style.maxWidth = 112f;
            label.style.marginRight = 6f;
            label.style.color = new Color(0.72f, 0.72f, 0.72f);
            row.Add(label);
            return row;
        }

        private static VisualElement CreateButtonRow()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginTop = 2f
                }
            };
        }

        private int GetTrackedAssetCount(string bundlePath)
        {
            int count = 0;
            foreach (ResourceAssetDebugSnapshot asset in m_Assets)
            {
                if (asset.BundlePath == bundlePath)
                {
                    count++;
                }
            }
            return count;
        }

        private string GetSearchText()
        {
            return m_SearchField?.value?.Trim() ?? string.Empty;
        }

        private static bool IsAssetSearchMatch(ResourceAssetDebugSnapshot asset, string search)
        {
            return ContainsIgnoreCase(asset.CacheKey, search)
                || ContainsIgnoreCase(asset.BundlePath, search)
                || ContainsIgnoreCase(FormatObject(asset.Asset), search)
                || ContainsIgnoreCase(GetLoadStatus(asset.IsLoaded, asset.IsLoading), search);
        }

        private static bool IsBundleSearchMatch(ResourceBundleDebugSnapshot bundle, string search)
        {
            if (ContainsIgnoreCase(bundle.BundlePath, search)
                || ContainsIgnoreCase(FormatObject(bundle.Bundle), search)
                || ContainsIgnoreCase(GetLoadStatus(bundle.IsLoaded, bundle.IsLoading), search))
            {
                return true;
            }

            foreach (string dependency in bundle.DirectDependencies)
            {
                if (ContainsIgnoreCase(dependency, search))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsInstanceSearchMatch(ResourceInstanceDebugSnapshot instance, string search)
        {
            return ContainsIgnoreCase(instance.AssetName, search)
                || ContainsIgnoreCase(FormatObject(instance.Instance), search)
                || ContainsIgnoreCase(FormatObject(instance.Asset), search)
                || ContainsIgnoreCase(GetInstanceStateText(instance.State), search)
                || instance.InstanceId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source != null && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetLoadStatus(bool isLoaded, bool isLoading)
        {
            if (isLoaded && isLoading) return "已加载+加载中";
            return isLoading ? "加载中" : "已加载";
        }

        private static Color GetLoadStatusColor(bool isLoaded, bool isLoading)
        {
            if (isLoaded && isLoading) return new Color(1f, 0.48f, 0.30f);
            return isLoading ? new Color(0.96f, 0.78f, 0.35f) : new Color(0.45f, 0.88f, 0.55f);
        }

        private static string GetInstanceStateText(ResourceInstanceDebugState state)
        {
            return state switch
            {
                ResourceInstanceDebugState.NormalActive => "普通活跃",
                ResourceInstanceDebugState.PooledActive => "池化活跃",
                ResourceInstanceDebugState.PooledFree => "池化空闲",
                _ => state.ToString()
            };
        }

        private static Color GetInstanceStateColor(ResourceInstanceDebugState state)
        {
            return state switch
            {
                ResourceInstanceDebugState.NormalActive => new Color(0.72f, 0.82f, 0.96f),
                ResourceInstanceDebugState.PooledActive => new Color(0.45f, 0.88f, 0.55f),
                ResourceInstanceDebugState.PooledFree => new Color(0.96f, 0.78f, 0.35f),
                _ => Color.white
            };
        }

        private static Color GetAlternatingRowColor(int index)
        {
            return index % 2 == 0
                ? new Color(0.13f, 0.13f, 0.13f, 0.42f)
                : new Color(0.18f, 0.18f, 0.18f, 0.42f);
        }

        private static string FormatObject(UObject value)
        {
            return value != null ? $"{value.name} ({value.GetType().Name})" : "-";
        }

        private static string FormatEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static UObject GetSelectableObject(UObject value)
        {
            return value is Component component ? component.gameObject : value;
        }

        private static string GetHierarchyPath(UObject value)
        {
            GameObject gameObject = value switch
            {
                GameObject go => go,
                Component component => component.gameObject,
                _ => null
            };
            if (gameObject == null)
            {
                return "-";
            }

            var names = new List<string>();
            Transform current = gameObject.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static void SelectObject(UObject target)
        {
            if (target != null)
            {
                Selection.activeObject = target;
            }
        }

        private static void PingObject(UObject target)
        {
            if (target != null)
            {
                EditorGUIUtility.PingObject(target);
            }
        }

        private static void CopyToClipboard(string value)
        {
            EditorGUIUtility.systemCopyBuffer = value;
        }

        private static bool TryFindAsset(List<ResourceAssetDebugSnapshot> assets, string cacheKey, out ResourceAssetDebugSnapshot result)
        {
            int index = FindAssetIndex(assets, cacheKey);
            result = index >= 0 ? assets[index] : default;
            return index >= 0;
        }

        private static int FindAssetIndex(List<ResourceAssetDebugSnapshot> assets, string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey)) return -1;
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i].CacheKey == cacheKey) return i;
            }
            return -1;
        }

        private static bool TryFindBundle(List<ResourceBundleDebugSnapshot> bundles, string bundlePath, out ResourceBundleDebugSnapshot result)
        {
            int index = FindBundleIndex(bundles, bundlePath);
            result = index >= 0 ? bundles[index] : default;
            return index >= 0;
        }

        private static int FindBundleIndex(List<ResourceBundleDebugSnapshot> bundles, string bundlePath)
        {
            if (string.IsNullOrEmpty(bundlePath)) return -1;
            for (int i = 0; i < bundles.Count; i++)
            {
                if (bundles[i].BundlePath == bundlePath) return i;
            }
            return -1;
        }

        private static bool TryFindInstanceGroup(List<ResourceInstanceGroupDebugSnapshot> groups, string assetName, out ResourceInstanceGroupDebugSnapshot result)
        {
            int index = FindInstanceGroupIndex(groups, assetName);
            result = index >= 0 ? groups[index] : default;
            return index >= 0;
        }

        private static int FindInstanceGroupIndex(List<ResourceInstanceGroupDebugSnapshot> groups, string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return -1;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].AssetName == assetName) return i;
            }
            return -1;
        }

        private static bool TryFindInstance(List<ResourceInstanceDebugSnapshot> instances, int instanceId, out ResourceInstanceDebugSnapshot result)
        {
            int index = FindInstanceIndex(instances, instanceId);
            result = index >= 0 ? instances[index] : default;
            return index >= 0;
        }

        private static int FindInstanceIndex(List<ResourceInstanceDebugSnapshot> instances, int? instanceId)
        {
            return instanceId.HasValue ? FindInstanceIndex(instances, instanceId.Value) : -1;
        }

        private static int FindInstanceIndex(List<ResourceInstanceDebugSnapshot> instances, int instanceId)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i].InstanceId == instanceId) return i;
            }
            return -1;
        }

        private enum DebugPage
        {
            AssetsAndBundles,
            InstancesAndPool
        }

        private enum DetailSelectionKind
        {
            None,
            Asset,
            Bundle,
            InstanceGroup,
            Instance
        }
    }
}
