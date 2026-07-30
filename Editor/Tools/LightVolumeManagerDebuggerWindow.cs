using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework;
using Object = UnityEngine.Object;
#pragma warning disable CS0618

namespace XFramework.Editor
{
    public class LightVolumeManagerDebuggerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/LightVolumeManagerDebugger";
        private const string FilterAll = "全部";
        private const string FilterCurrent = "当前生效";
        private const string FilterActive = "已激活";
        private const string FilterPlayerInside = "玩家在内";
        private const string FilterNoSettings = "无有效光设置";
        private const string FilterColliderNotTrigger = "Collider 非 Trigger";
        private const string FilterDisabled = "禁用对象";

        private readonly List<VolumeEntry> m_AllEntries = new();
        private readonly List<VolumeEntry> m_FilteredEntries = new();

        private TextField m_SearchField;
        private DropdownField m_FilterField;
        private Label m_SummaryLabel;
        private ListView m_ListView;
        private VisualElement m_DetailPane;

        private VolumeEntry m_SelectedEntry;
        private LightVolumeManagerDebugSnapshot? m_LightSnapshot;
        private bool m_IsManagerLoaded;
        private LightVolumeEditorPreview.EditModeSnapshot? m_EditModeSnapshot;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            LightVolumeManagerDebuggerWindow window = GetWindow<LightVolumeManagerDebuggerWindow>();
            window.titleContent = new GUIContent("LightVolumeManagerDebugger");
            window.minSize = new Vector2(860f, 540f);
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
            RefreshView();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;

            root.Add(BuildToolbar());

            m_SummaryLabel = new Label();
            m_SummaryLabel.style.marginTop = 4;
            m_SummaryLabel.style.marginBottom = 6;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(m_SummaryLabel);

            // 上下分割：列表 + 详情
            VisualElement content = new() { style = { flexGrow = 1, flexDirection = FlexDirection.Row } };
            content.Add(BuildListPane());
            content.Add(BuildDetailPane());
            root.Add(content);
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new()
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
            };

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1;
            m_SearchField.style.minWidth = 180;
            m_SearchField.tooltip = "按对象名或场景名过滤";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            m_FilterField = new DropdownField("状态", new List<string>
            {
                FilterAll,
                FilterCurrent,
                FilterActive,
                FilterPlayerInside,
                FilterNoSettings,
                FilterColliderNotTrigger,
                FilterDisabled
            }, 0);
            m_FilterField.style.width = 180;
            m_FilterField.style.marginLeft = 8;
            m_FilterField.tooltip = "筛选 AreaLightVolume 的运行或配置状态";
            m_FilterField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_FilterField);

            AddRefreshControls(toolbar, "重新扫描当前场景中的 AreaLightVolume");

            return toolbar;
        }

        private VisualElement BuildListPane()
        {
            VisualElement pane = CreatePane();
            pane.style.flexGrow = 1;

            pane.Add(CreatePaneTitle("AreaLightVolume 列表", 22, 4));

            pane.Add(BuildListHeader());

            m_ListView = new ListView
            {
                itemsSource = m_FilteredEntries,
                fixedItemHeight = 26,
                selectionType = SelectionType.Single,
                makeItem = MakeListItem,
                bindItem = BindListItem
            };
            m_ListView.style.flexGrow = 1;
            m_ListView.style.marginTop = 4;
            m_ListView.onSelectionChange += OnSelectionChanged;
            pane.Add(m_ListView);
            return pane;
        }

        private VisualElement BuildListHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22);
            header.Add(CreateHeaderLabel("状态", 82));
            header.Add(CreateHeaderLabel("对象", 170));
            header.Add(CreateHeaderLabel("Prio", 44));
            header.Add(CreateHeaderLabel("玩家", 44));
            header.Add(CreateHeaderLabel("启用", 44));
            header.Add(CreateHeaderLabel("Trigger", 58));

            Label sceneLabel = CreateHeaderLabel("场景", 0);
            sceneLabel.style.flexGrow = 1;
            header.Add(sceneLabel);
            return header;
        }

        private VisualElement BuildDetailPane()
        {
            m_DetailPane = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    width = 320,
                    marginLeft = 4,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f)
                }
            };
            return m_DetailPane;
        }

        protected override void OnAutoRefresh()
        {
            RefreshData();
        }

        protected override void OnRefreshClicked()
        {
            RefreshData();
        }

        private void RefreshData()
        {
            RefreshSnapshot();
            RefreshVolumeEntries();
            RefreshView(true);
        }

        private void RefreshSnapshot()
        {
            if (Application.isPlaying)
            {
                m_IsManagerLoaded = GameEntry.IsModuleLoaded<LightVolumeManager>();
                m_LightSnapshot = m_IsManagerLoaded ? LightVolumeManager.Instance.GetDebugSnapshot() : null;
                m_EditModeSnapshot = null;
            }
            else
            {
                m_IsManagerLoaded = false;
                m_LightSnapshot = null;
                m_EditModeSnapshot = LightVolumeEditorPreview.GetEditModeSnapshot();
            }
        }

        private void RefreshVolumeEntries()
        {
            AreaLightVolume selectedVolume = m_SelectedEntry?.Volume;
            m_AllEntries.Clear();

            AreaLightVolume currentVolume;
            HashSet<AreaLightVolume> activeVolumes = new();

            if (Application.isPlaying && m_LightSnapshot != null)
            {
                currentVolume = m_LightSnapshot.Value.CurrentLightVolume;
                if (m_LightSnapshot.Value.ActiveLightVolumes != null)
                {
                    foreach (AreaLightVolume volume in m_LightSnapshot.Value.ActiveLightVolumes)
                    {
                        if (volume != null)
                        {
                            activeVolumes.Add(volume);
                        }
                    }
                }
            }
            else if (!Application.isPlaying && m_EditModeSnapshot != null)
            {
                // 编辑模式：摄像机所在的 Volume 作为 current
                currentVolume = m_EditModeSnapshot.Value.CurrentVolumeAtCamera;
                if (currentVolume != null)
                {
                    activeVolumes.Add(currentVolume);
                }
            }
            else
            {
                currentVolume = null;
            }

            foreach (AreaLightVolume volume in Object.FindObjectsByType<AreaLightVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume == null || !volume.gameObject.scene.IsValid())
                {
                    continue;
                }

                m_AllEntries.Add(VolumeEntry.Create(volume, activeVolumes.Contains(volume), volume == currentVolume));
            }

            m_AllEntries.Sort(CompareEntries);
            m_SelectedEntry = selectedVolume != null ? m_AllEntries.Find(entry => entry.Volume == selectedVolume) : null;
        }

        private void RefreshView(bool rebuildDetail = false)
        {
            RefreshFilteredEntries();
            RefreshSummary();
            RefreshList();
            if (rebuildDetail || m_DetailPane.childCount == 0)
            {
                RefreshDetail();
            }
        }

        private void RefreshFilteredEntries()
        {
            m_FilteredEntries.Clear();
            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;
            string filter = m_FilterField != null ? m_FilterField.value : FilterAll;

            for (int i = 0; i < m_AllEntries.Count; i++)
            {
                VolumeEntry entry = m_AllEntries[i];
                if (!MatchesSearch(entry, search) || !MatchesFilter(entry, filter))
                {
                    continue;
                }

                m_FilteredEntries.Add(entry);
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
                if (m_EditModeSnapshot == null)
                {
                    m_SummaryLabel.text = $"Edit Mode | 场景 Volume: {m_AllEntries.Count}";
                    return;
                }

                LightVolumeEditorPreview.EditModeSnapshot snapshot = m_EditModeSnapshot.Value;
                string currentVolume = snapshot.CurrentVolumeAtCamera != null ? snapshot.CurrentVolumeAtCamera.name : "无";
                string sceneLight = snapshot.HasSceneMainLight ? snapshot.SceneMainLightName : "无";
                string mode = snapshot.HasSceneMainLight
                    ? (snapshot.IsOverridingSceneLight ? "覆盖全局光" : "等待覆盖")
                    : (snapshot.HasPreviewLight ? "临时光已创建" : "无临时光");
                m_SummaryLabel.text =
                    $"Edit Mode | 场景 Volume: {m_AllEntries.Count} | 摄像机所在区域: {currentVolume} | " +
                    $"场景全局光: {sceneLight} | 模式: {mode}";
                return;
            }

            if (!m_IsManagerLoaded || m_LightSnapshot == null)
            {
                m_SummaryLabel.text = $"Play Mode | LightVolumeManager 未加载 | 场景 Volume: {m_AllEntries.Count}";
                return;
            }

            LightVolumeManagerDebugSnapshot snapshot2 = m_LightSnapshot.Value;
            string currentVolume2 = snapshot2.CurrentLightVolume != null ? snapshot2.CurrentLightVolume.name : "无";
            string sceneLight2 = snapshot2.HasSceneMainLight ? snapshot2.SceneMainLightName : "无";
            string managerLight = snapshot2.HasManagerLight ? "已创建" : "无";
            m_SummaryLabel.text =
                $"Play Mode | 当前区域: {currentVolume2} | 激活区域: {snapshot2.ActiveLightVolumes.Count} | " +
                $"场景全局光: {sceneLight2} | Manager临时光: {managerLight}";
        }

        private void RefreshList()
        {
            if (m_ListView == null)
            {
                return;
            }

            m_ListView.itemsSource = m_FilteredEntries;
            m_ListView.Rebuild();
        }

        private void RefreshDetail()
        {
            if (m_DetailPane == null)
            {
                return;
            }

            m_DetailPane.Clear();
            m_DetailPane.Add(BuildManagerSection());

            m_SelectedEntry = ResolveCurrentSelectedEntry();
            if (m_SelectedEntry == null || m_SelectedEntry.Volume == null)
            {
                Label emptyLabel = new("请选择一个 AreaLightVolume。")
                {
                    style = { marginTop = 12, color = new Color(0.75f, 0.75f, 0.75f) }
                };
                m_DetailPane.Add(emptyLabel);
                return;
            }

            m_DetailPane.Add(BuildVolumeDetailSection(m_SelectedEntry));
        }

        private VisualElement BuildManagerSection()
        {
            VisualElement section = CreateSection(Application.isPlaying ? "LightVolumeManager (Play Mode)" : "LightVolumeEditorPreview (Edit Mode)");

            if (!Application.isPlaying)
            {
                if (m_EditModeSnapshot == null)
                {
                    section.Add(CreateInfoRow("运行状态", "Edit Mode | 无快照"));
                    return section;
                }

                LightVolumeEditorPreview.EditModeSnapshot snapshot = m_EditModeSnapshot.Value;
                section.Add(CreateInfoRow("运行状态", "Edit Mode"));
                section.Add(CreateInfoRow("预览机制", snapshot.HasSceneMainLight ? "覆盖场景全局光" : "创建临时光"));
                section.Add(CreateInfoRow("场景全局光", snapshot.HasSceneMainLight ? snapshot.SceneMainLightName : "无"));
                section.Add(CreateInfoRow("正在覆盖", FormatBool(snapshot.IsOverridingSceneLight)));
                section.Add(CreateInfoRow("临时光", snapshot.HasPreviewLight ? "已创建" : "无"));
                section.Add(CreateInfoRow("摄像机所在 Volume", snapshot.CurrentVolumeAtCamera != null ? snapshot.CurrentVolumeAtCamera.name : "无"));

                if (snapshot.OriginalSettings != null)
                {
                    section.Add(CreateInfoRow("  原始颜色", $"#{ColorUtility.ToHtmlStringRGBA(snapshot.OriginalSettings.Color)}"));
                    section.Add(CreateInfoRow("  原始强度", snapshot.OriginalSettings.Intensity.ToString("0.00")));
                    section.Add(CreateInfoRow("  原始角度", snapshot.OriginalSettings.EulerAngles.ToString("0.0")));
                }

                return section;
            }

            if (!m_IsManagerLoaded || m_LightSnapshot == null)
            {
                section.Add(CreateInfoRow("运行状态", "LightVolumeManager 未加载"));
                return section;
            }

            LightVolumeManagerDebugSnapshot snapshot2 = m_LightSnapshot.Value;
            section.Add(CreateInfoRow("当前区域", snapshot2.CurrentLightVolume != null ? snapshot2.CurrentLightVolume.name : "无"));
            section.Add(CreateInfoRow("激活区域数", snapshot2.ActiveLightVolumes.Count.ToString()));
            section.Add(CreateInfoRow("场景全局光", snapshot2.HasSceneMainLight ? snapshot2.SceneMainLightName : "无"));
            section.Add(CreateInfoRow("Manager临时光", snapshot2.HasManagerLight ? "已创建" : "无"));

            if (snapshot2.OriginalSettings != null)
            {
                section.Add(CreateInfoRow("  原始颜色", $"#{ColorUtility.ToHtmlStringRGBA(snapshot2.OriginalSettings.Color)}"));
                section.Add(CreateInfoRow("  原始强度", snapshot2.OriginalSettings.Intensity.ToString("0.00")));
                section.Add(CreateInfoRow("  原始角度", snapshot2.OriginalSettings.EulerAngles.ToString("0.0")));
            }

            return section;
        }

        private VisualElement BuildVolumeDetailSection(VolumeEntry entry)
        {
            VisualElement section = CreateSection($"Volume: {entry.VolumeName}");

            AreaLightVolume volume = entry.Volume;
            AreaLightVolumeDebugSnapshot snapshot = volume.GetDebugSnapshot();

            section.Add(CreateInfoRow("对象名", entry.VolumeName));
            section.Add(CreateInfoRow("场景", entry.SceneName));
            section.Add(CreateInfoRow("优先级", snapshot.Priority.ToString()));
            section.Add(CreateInfoRow("玩家碰撞体数", snapshot.PlayerColliderCount.ToString()));
            section.Add(CreateInfoRow("有效光设置", FormatBool(snapshot.HasLightSettings)));
            section.Add(CreateInfoRow("是当前生效", FormatBool(entry.IsCurrent)));
            section.Add(CreateInfoRow("已激活", FormatBool(entry.IsActive)));
            section.Add(CreateInfoRow("启用", FormatBool(volume.isActiveAndEnabled)));

            Collider collider = volume.GetComponent<Collider>();
            section.Add(CreateInfoRow("Collider", collider != null ? collider.GetType().Name : "无"));
            section.Add(CreateInfoRow("isTrigger", FormatBool(collider != null && collider.isTrigger)));

            // Light Settings 详情
            DirectionalLightSettings settings = volume.LightSettings;
            if (settings != null)
            {
                section.Add(CreateInfoRow("光设置", string.Empty, marginBottom: 4));
                section.Add(CreateInfoRow("  Enabled", FormatBool(settings.Enabled), 130));
                section.Add(CreateInfoRow("  Color", $"#{ColorUtility.ToHtmlStringRGBA(settings.Color)}", 130));
                section.Add(CreateInfoRow("  Intensity", settings.Intensity.ToString("0.00"), 130));
                section.Add(CreateInfoRow("  EulerAngles", settings.EulerAngles.ToString("0.0"), 130));
                section.Add(CreateInfoRow("  ShadowType", settings.ShadowType.ToString(), 130));
                section.Add(CreateInfoRow("  ShadowStrength", settings.ShadowStrength.ToString("0.00"), 130));
            }

            return section;
        }

        private void OnSelectionChanged(IEnumerable<object> selectedItems)
        {
            using IEnumerator<object> enumerator = selectedItems.GetEnumerator();
            if (enumerator.MoveNext())
            {
                m_SelectedEntry = enumerator.Current as VolumeEntry;
            }
            else
            {
                m_SelectedEntry = null;
            }

            RefreshDetail();
        }

        private VolumeEntry ResolveCurrentSelectedEntry()
        {
            if (m_ListView?.selectedItem is VolumeEntry entry)
            {
                return entry;
            }

            return m_SelectedEntry;
        }

        private bool MatchesSearch(VolumeEntry entry, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            return entry.VolumeName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0
                   || entry.SceneName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesFilter(VolumeEntry entry, string filter)
        {
            if (filter == FilterAll)
            {
                return true;
            }

            if (filter == FilterCurrent)
            {
                return entry.IsCurrent;
            }

            if (filter == FilterActive)
            {
                return entry.IsActive;
            }

            if (filter == FilterPlayerInside)
            {
                return entry.PlayerColliderCount > 0;
            }

            if (filter == FilterNoSettings)
            {
                return !entry.HasLightSettings;
            }

            if (filter == FilterColliderNotTrigger)
            {
                return !entry.IsTrigger;
            }

            if (filter == FilterDisabled)
            {
                return !entry.IsEnabled;
            }

            return true;
        }

        private static int CompareEntries(VolumeEntry a, VolumeEntry b)
        {
            int priorityCompare = b.Priority.CompareTo(a.Priority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            return string.CompareOrdinal(a.VolumeName, b.VolumeName);
        }

        private static string FormatBool(bool value)
        {
            return value ? "是" : "否";
        }

        private static VisualElement MakeListItem()
        {
            VisualElement row = new()
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 4, paddingRight = 4 }
            };

            row.Add(CreateCellLabel("status", 80));
            row.Add(CreateCellLabel("name", 170));
            row.Add(CreateCellLabel("prio", 44));
            row.Add(CreateCellLabel("player", 44));
            row.Add(CreateCellLabel("enabled", 44));
            row.Add(CreateCellLabel("trigger", 58));

            Label sceneLabel = CreateCellLabel("scene", 0);
            sceneLabel.style.flexGrow = 1;
            row.Add(sceneLabel);

            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            VolumeEntry entry = m_FilteredEntries[index];

            element.Q<Label>("status").text = entry.IsCurrent ? "● 当前" : entry.IsActive ? "○ 激活" : entry.IsEnabled ? "  " : "  禁用";
            element.Q<Label>("status").style.color = entry.IsCurrent ? new Color(0.30f, 0.85f, 0.46f) : entry.IsActive ? new Color(0.55f, 0.75f, 1f) : new Color(0.6f, 0.6f, 0.6f);

            element.Q<Label>("name").text = entry.VolumeName;
            element.Q<Label>("prio").text = entry.Priority.ToString();
            element.Q<Label>("player").text = entry.PlayerColliderCount > 0 ? entry.PlayerColliderCount.ToString() : "-";
            element.Q<Label>("enabled").text = entry.IsEnabled ? "✓" : "✗";
            element.Q<Label>("trigger").text = entry.HasCollider ? (entry.IsTrigger ? "✓" : "✗") : "无";
            element.Q<Label>("scene").text = entry.SceneName;
        }

        private class VolumeEntry
        {
            public AreaLightVolume Volume { get; private set; }
            public string VolumeName { get; private set; }
            public string SceneName { get; private set; }
            public int Priority { get; private set; }
            public int PlayerColliderCount { get; private set; }
            public bool HasLightSettings { get; private set; }
            public bool IsCurrent { get; private set; }
            public bool IsActive { get; private set; }
            public bool IsEnabled { get; private set; }
            public bool HasCollider { get; private set; }
            public bool IsTrigger { get; private set; }

            public static VolumeEntry Create(AreaLightVolume volume, bool isActive, bool isCurrent)
            {
                AreaLightVolumeDebugSnapshot snapshot = volume.GetDebugSnapshot();
                Collider collider = volume.GetComponent<Collider>();

                return new VolumeEntry
                {
                    Volume = volume,
                    VolumeName = volume.name,
                    SceneName = volume.gameObject.scene.name,
                    Priority = snapshot.Priority,
                    PlayerColliderCount = snapshot.PlayerColliderCount,
                    HasLightSettings = snapshot.HasLightSettings,
                    IsCurrent = isCurrent,
                    IsActive = isActive,
                    IsEnabled = volume.isActiveAndEnabled,
                    HasCollider = collider != null,
                    IsTrigger = collider != null && collider.isTrigger
                };
            }
        }
    }
}
