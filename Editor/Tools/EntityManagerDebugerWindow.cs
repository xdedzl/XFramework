using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Entity;
using EntityComponent = XFramework.Entity.Entity;
#pragma warning disable CS0618 // Type or member is obsolete

namespace XFramework.Editor
{
    public class EntityManagerDebugerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/Entity Manager Debuger";
        private const float ContainerPaneWidth = 300f;

        private readonly List<EntityContainerDebugSnapshot> m_Containers = new();
        private readonly List<EntityDebugSnapshot> m_AllEntries = new();
        private readonly List<EntityDebugSnapshot> m_FilteredEntries = new();

        private TextField m_SearchField;
        private Label m_SummaryLabel;
        
        private ListView m_ContainerListView;
        private ListView m_EntityListView;
        private EntityManagerDebugSnapshot? m_ManagerSnapshot;
        private string m_SelectedContainerName;
        private string m_SelectedEntityId;
        private DetailSelectionKind m_DetailSelectionKind;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            EntityManagerDebugerWindow window = GetWindow<EntityManagerDebugerWindow>();
            window.titleContent = new GUIContent("Entity Manager Debuger");
            window.minSize = new Vector2(1080f, 540f);
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        public void CreateGUI()
        {
            BuildUI();
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
            m_SearchField.tooltip = "按 Id、Alias、类型、对象名或场景过滤当前容器内的 Entity";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            AddRefreshControls(toolbar, "刷新 EntityManager 当前有效实体快照");

            return toolbar;
        }

        private VisualElement BuildSplitPane()
        {
            TwoPaneSplitView splitView = new(0, ContainerPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            splitView.Add(BuildContainerPane());
            splitView.Add(BuildEntityPane());
            return splitView;
        }

        private VisualElement BuildContainerPane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginRight = 4f;

            Label title = CreatePaneTitle("Containers", height: 22f);
            pane.Add(title);
            pane.Add(BuildContainerHeader());

            m_ContainerListView = new ListView
            {
                itemsSource = m_Containers,
                fixedItemHeight = 28f,
                selectionType = SelectionType.Single,
                makeItem = MakeContainerItem,
                bindItem = BindContainerItem
            };
            m_ContainerListView.style.flexGrow = 1f;
            m_ContainerListView.style.marginTop = 4f;
            m_ContainerListView.onSelectionChange += OnContainerSelectionChanged;
            pane.Add(m_ContainerListView);
            return pane;
        }

        private VisualElement BuildEntityPane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginLeft = 4f;

            Label title = CreatePaneTitle("Entities", height: 22f);
            pane.Add(title);
            pane.Add(BuildEntityHeader());

            m_EntityListView = new ListView
            {
                itemsSource = m_FilteredEntries,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeEntityItem,
                bindItem = BindEntityItem
            };
            m_EntityListView.style.flexGrow = 1f;
            m_EntityListView.style.marginTop = 4f;
            m_EntityListView.onSelectionChange += OnEntitySelectionChanged;
            pane.Add(m_EntityListView);
            return pane;
        }

        private VisualElement BuildContainerHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("容器", 150f));
            header.Add(CreateHeaderLabel("数量", 46f));

            Label type = CreateHeaderLabel("类型", 0f);
            type.style.flexGrow = 1f;
            header.Add(type);
            return header;
        }

        private VisualElement BuildEntityHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("状态", 62f));
            header.Add(CreateHeaderLabel("类型", 160f));
            header.Add(CreateHeaderLabel("Alias", 130f));
            header.Add(CreateHeaderLabel("对象", 180f));
            header.Add(CreateHeaderLabel("子数", 48f));

            Label scene = CreateHeaderLabel("场景", 0f);
            scene.style.flexGrow = 1f;
            header.Add(scene);
            return header;
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
            m_ManagerSnapshot = null;
            m_Containers.Clear();
            m_AllEntries.Clear();

            if (Application.isPlaying && GameEntry.IsModuleLoaded<EntityManager>())
            {
                EntityManagerDebugSnapshot snapshot = EntityManager.Instance.GetDebugSnapshot();
                m_ManagerSnapshot = snapshot;
                m_Containers.AddRange(snapshot.Containers);
                m_AllEntries.AddRange(snapshot.Entities);
            }

            EnsureValidSelection();
            RefreshView();
        }

        private void EnsureValidSelection()
        {
            if (!TryFindContainer(m_SelectedContainerName, out _))
            {
                m_SelectedContainerName = m_Containers.Count > 0 ? m_Containers[0].Name : null;
                m_SelectedEntityId = null;
                if (m_DetailSelectionKind != DetailSelectionKind.None)
                {
                    m_DetailSelectionKind = DetailSelectionKind.None;
                }
            }

            if (!string.IsNullOrEmpty(m_SelectedEntityId)
                && (!TryFindEntry(m_AllEntries, m_SelectedEntityId, out EntityDebugSnapshot selectedEntry)
                    || selectedEntry.ContainerName != m_SelectedContainerName))
            {
                m_SelectedEntityId = null;
                if (m_DetailSelectionKind == DetailSelectionKind.Entity)
                {
                    m_DetailSelectionKind = DetailSelectionKind.None;
                }
            }
        }

        private void RefreshView()
        {
            EnsureValidSelection();
            RefreshFilteredEntries();
            RefreshContainerList();
            RefreshEntityList();
            RefreshSummary();
            RefreshInspectorSelection();
        }

        private void RefreshFilteredEntries()
        {
            m_FilteredEntries.Clear();

            if (string.IsNullOrEmpty(m_SelectedContainerName))
            {
                return;
            }

            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;
            for (int i = 0; i < m_AllEntries.Count; i++)
            {
                EntityDebugSnapshot entry = m_AllEntries[i];
                if (entry.ContainerName != m_SelectedContainerName)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(search) && !IsSearchMatch(entry, search))
                {
                    continue;
                }

                m_FilteredEntries.Add(entry);
            }
        }

        private void RefreshContainerList()
        {
            if (m_ContainerListView == null)
            {
                return;
            }

            m_ContainerListView.itemsSource = m_Containers;
            m_ContainerListView.Rebuild();
            int selectedIndex = GetSelectedContainerIndex();
            if (selectedIndex >= 0)
            {
                m_ContainerListView.SetSelectionWithoutNotify(new[] { selectedIndex });
            }
        }

        private void RefreshEntityList()
        {
            if (m_EntityListView == null)
            {
                return;
            }

            m_EntityListView.itemsSource = m_FilteredEntries;
            m_EntityListView.Rebuild();
            int selectedIndex = GetSelectedEntityIndex();
            if (selectedIndex >= 0)
            {
                m_EntityListView.SetSelectionWithoutNotify(new[] { selectedIndex });
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
                m_SummaryLabel.text = "进入 Play Mode 后会显示当前有效 Entity。";
                return;
            }

            if (!GameEntry.IsModuleLoaded<EntityManager>())
            {
                m_SummaryLabel.text = "EntityManager 尚未加载。";
                return;
            }

            int aliasCount = m_ManagerSnapshot?.AliasCount ?? 0;
            string selectedContainer = string.IsNullOrEmpty(m_SelectedContainerName) ? "未选择" : m_SelectedContainerName;
            m_SummaryLabel.text = $"容器：{m_Containers.Count} | 当前容器：{selectedContainer} | Entity：{m_FilteredEntries.Count} / {m_AllEntries.Count} | Alias：{aliasCount}";
        }

        private void RefreshInspectorSelection()
        {
            switch (m_DetailSelectionKind)
            {
                case DetailSelectionKind.Container:
                    if (!TryFindContainer(m_SelectedContainerName, out _))
                    {
                        m_DetailSelectionKind = DetailSelectionKind.None;
                        XFrameworkInspectorWindow.ClearIfOwner(this);
                        return;
                    }

                    XFrameworkInspectorWindow.RefreshIfOwner(this);
                    return;

                case DetailSelectionKind.Entity:
                    if (string.IsNullOrEmpty(m_SelectedEntityId) || !TryFindEntry(m_FilteredEntries, m_SelectedEntityId, out _))
                    {
                        m_SelectedEntityId = null;
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

        private VisualElement MakeContainerItem()
        {
            VisualElement row = CreateRow(Color.clear, 28f);
            row.Add(CreateCellLabel("name", 150f, bold: true, flexShrink: true));
            row.Add(CreateCellLabel("count", 46f, flexShrink: true));

            Label type = CreateCellLabel("type", 0f, flexShrink: true);
            type.style.flexGrow = 1f;
            row.Add(type);
            return row;
        }

        private void BindContainerItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_Containers.Count)
            {
                return;
            }

            EntityContainerDebugSnapshot container = m_Containers[index];
            element.style.backgroundColor = container.Name == m_SelectedContainerName
                ? new Color(0.24f, 0.42f, 0.72f, 0.45f)
                : index % 2 == 0
                    ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                    : new Color(0.31f, 0.31f, 0.31f, 0.18f);
            element.tooltip = container.EntityType != null ? container.EntityType.FullName : container.Name;

            element.Q<Label>("name").text = FormatEmpty(container.Name);
            element.Q<Label>("count").text = container.ActiveEntityCount.ToString();
            element.Q<Label>("type").text = container.EntityType != null ? container.EntityType.Name : "-";
        }

        private VisualElement MakeEntityItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.Add(CreateCellLabel("status", 62f, flexShrink: true));
            row.Add(CreateCellLabel("type", 160f, flexShrink: true));
            row.Add(CreateCellLabel("alias", 130f, flexShrink: true));
            row.Add(CreateCellLabel("name", 180f, flexShrink: true));
            row.Add(CreateCellLabel("children", 48f, flexShrink: true));

            Label scene = CreateCellLabel("scene", 0f, flexShrink: true);
            scene.style.flexGrow = 1f;
            row.Add(scene);
            return row;
        }

        private void BindEntityItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_FilteredEntries.Count)
            {
                return;
            }

            EntityDebugSnapshot entry = m_FilteredEntries[index];
            element.style.backgroundColor = entry.Id == m_SelectedEntityId
                ? new Color(0.24f, 0.42f, 0.72f, 0.45f)
                : index % 2 == 0
                    ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                    : new Color(0.31f, 0.31f, 0.31f, 0.18f);
            element.tooltip = entry.Id;

            element.Q<Label>("status").text = GetStatusText(entry);
            element.Q<Label>("status").style.color = GetStatusColor(entry);
            element.Q<Label>("type").text = entry.EntityType != null ? entry.EntityType.Name : "-";
            element.Q<Label>("alias").text = FormatEmpty(entry.Alias);
            element.Q<Label>("name").text = FormatEmpty(entry.Name);
            element.Q<Label>("children").text = entry.ChildCount.ToString();
            element.Q<Label>("scene").text = FormatEmpty(entry.SceneName);
        }

        private void OnContainerSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is EntityContainerDebugSnapshot container)
                {
                    m_SelectedContainerName = container.Name;
                    m_SelectedEntityId = null;
                    m_DetailSelectionKind = DetailSelectionKind.Container;
                    RefreshView();
                    ShowContainerDetail(true);
                    return;
                }
            }
        }

        private void OnEntitySelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is EntityDebugSnapshot entry)
                {
                    m_SelectedContainerName = entry.ContainerName;
                    m_SelectedEntityId = entry.Id;
                    m_DetailSelectionKind = DetailSelectionKind.Entity;
                    RefreshView();
                    ShowEntityDetail(true);
                    return;
                }
            }

            m_SelectedEntityId = null;
            if (m_DetailSelectionKind == DetailSelectionKind.Entity)
            {
                m_DetailSelectionKind = DetailSelectionKind.None;
                XFrameworkInspectorWindow.ClearIfOwner(this);
            }
        }

        private void ShowContainerDetail(bool openInspector)
        {
            if (string.IsNullOrEmpty(m_SelectedContainerName) || !TryFindContainer(m_SelectedContainerName, out EntityContainerDebugSnapshot container))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(
                    this,
                    string.IsNullOrEmpty(container.Name) ? "EntityContainer" : container.Name,
                    BuildContainerInspectorContent,
                    container.EntityType != null ? container.EntityType.FullName : "EntityContainer");
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void ShowEntityDetail(bool openInspector)
        {
            if (string.IsNullOrEmpty(m_SelectedEntityId) || !TryFindEntry(m_AllEntries, m_SelectedEntityId, out EntityDebugSnapshot entry))
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                XFrameworkInspectorWindow.InspectCustom(
                    this,
                    string.IsNullOrEmpty(entry.Name) ? "Entity" : entry.Name,
                    BuildEntityInspectorContent,
                    entry.EntityType != null ? entry.EntityType.FullName : entry.ContainerName);
                return;
            }

            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void BuildContainerInspectorContent(VisualElement parent)
        {
            if (string.IsNullOrEmpty(m_SelectedContainerName) || !TryFindContainer(m_SelectedContainerName, out EntityContainerDebugSnapshot container))
            {
                Label emptyLabel = new(Application.isPlaying
                    ? "从 Entity Manager Debuger 左侧选择一个容器。"
                    : "进入 Play Mode 后会显示当前容器。");
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
                emptyLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                parent.Add(emptyLabel);
                return;
            }

            parent.Add(BuildContainerActionSection(container));
            parent.Add(BuildContainerIdentitySection(container));
            parent.Add(BuildContainerEntityPreviewSection(container));
        }

        private void BuildEntityInspectorContent(VisualElement parent)
        {
            if (string.IsNullOrEmpty(m_SelectedEntityId) || !TryFindEntry(m_AllEntries, m_SelectedEntityId, out EntityDebugSnapshot entry))
            {
                Label emptyLabel = new(Application.isPlaying
                    ? "从 Entity Manager Debuger 右侧选择一个 Entity。"
                    : "进入 Play Mode 后会显示当前有效 Entity。");
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
                emptyLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                parent.Add(emptyLabel);
                return;
            }

            parent.Add(BuildActionSection(entry));
            parent.Add(BuildObjectSection(entry));
            parent.Add(BuildIdentitySection(entry));
            parent.Add(BuildTransformSection(entry));
            parent.Add(BuildRelationSection(entry));
        }

        private VisualElement BuildContainerActionSection(EntityContainerDebugSnapshot container)
        {
            VisualElement section = CreateSection("Container Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("Ping模板", () => PingObject(container.Template), container.Template != null));
            buttonRow.Add(CreateActionButton("选中模板", () => SelectObject(container.Template), container.Template != null));
            buttonRow.Add(CreateActionButton("复制容器名", () => CopyToClipboard(container.Name), !string.IsNullOrEmpty(container.Name)));
            section.Add(buttonRow);
            return section;
        }

        private VisualElement BuildContainerIdentitySection(EntityContainerDebugSnapshot container)
        {
            VisualElement section = CreateSection("Container", marginBottom: 12f);
            section.Add(CreateEntityInfoRow("Name", container.Name));
            section.Add(CreateEntityInfoRow("Type", container.EntityType != null ? container.EntityType.FullName : "-"));
            section.Add(CreateEntityInfoRow("Active Count", container.ActiveEntityCount.ToString()));
            section.Add(CreateEntityInfoRow("Filtered Count", m_FilteredEntries.Count.ToString()));
            section.Add(CreateObjectFieldRow("Template", typeof(GameObject), container.Template));
            return section;
        }

        private VisualElement BuildContainerEntityPreviewSection(EntityContainerDebugSnapshot container)
        {
            VisualElement section = CreateSection("Entities", marginBottom: 12f);
            List<EntityDebugSnapshot> entries = GetEntriesInContainer(container.Name);
            if (entries.Count == 0)
            {
                section.Add(CreateEntityInfoRow("Entities", "无"));
                return section;
            }

            int previewCount = Math.Min(entries.Count, 20);
            for (int i = 0; i < previewCount; i++)
            {
                EntityDebugSnapshot entry = entries[i];
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

                Label name = new(FormatEntityDisplayName(entry));
                name.style.flexGrow = 1f;
                name.style.minWidth = 0f;
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(name);

                Button selectButton = new(() => SelectEntityFromContainerPreview(entry.Id))
                {
                    text = "查看"
                };
                selectButton.style.width = 54f;
                selectButton.style.marginLeft = 6f;
                row.Add(selectButton);
                section.Add(row);
            }

            if (entries.Count > previewCount)
            {
                section.Add(CreateEntityInfoRow("More", $"还有 {entries.Count - previewCount} 个 Entity 未在详情中展开。"));
            }

            return section;
        }

        private VisualElement BuildActionSection(EntityDebugSnapshot entry)
        {
            VisualElement section = CreateSection("Entity Actions", marginBottom: 12f);
            VisualElement buttonRow = CreateButtonRow();
            buttonRow.Add(CreateActionButton("选中对象", () => SelectObject(entry.GameObject), entry.GameObject != null));
            buttonRow.Add(CreateActionButton("Ping对象", () => PingObject(entry.GameObject), entry.GameObject != null));
            buttonRow.Add(CreateActionButton("复制Id", () => CopyToClipboard(entry.Id), !string.IsNullOrEmpty(entry.Id)));
            buttonRow.Add(CreateActionButton("复制Alias", () => CopyToClipboard(entry.Alias), !string.IsNullOrEmpty(entry.Alias)));
            section.Add(buttonRow);
            return section;
        }

        private VisualElement BuildObjectSection(EntityDebugSnapshot entry)
        {
            VisualElement section = CreateSection("Object", marginBottom: 12f);
            section.Add(CreateObjectFieldRow("Entity", typeof(EntityComponent), entry.Entity));
            section.Add(CreateObjectFieldRow("GameObject", typeof(GameObject), entry.GameObject));
            section.Add(CreateEntityInfoRow("Hierarchy", entry.GameObject != null ? GetHierarchyPath(entry.GameObject) : "-"));
            return section;
        }

        private VisualElement BuildIdentitySection(EntityDebugSnapshot entry)
        {
            VisualElement section = CreateSection("Identity", marginBottom: 12f);
            section.Add(CreateEntityInfoRow("Id", entry.Id));
            section.Add(CreateEntityInfoRow("Container", entry.ContainerName));
            section.Add(CreateEntityInfoRow("Alias", FormatEmpty(entry.Alias)));
            section.Add(CreateEntityInfoRow("Type", entry.EntityType != null ? entry.EntityType.FullName : "-"));
            section.Add(CreateEntityInfoRow("Name", FormatEmpty(entry.Name)));
            section.Add(CreateEntityInfoRow("Status", GetStatusText(entry)));
            section.Add(CreateEntityInfoRow("ActiveSelf", FormatBool(entry.ActiveSelf)));
            section.Add(CreateEntityInfoRow("ActiveInHierarchy", FormatBool(entry.ActiveInHierarchy)));
            section.Add(CreateEntityInfoRow("Scene", FormatEmpty(entry.SceneName)));
            return section;
        }

        private VisualElement BuildTransformSection(EntityDebugSnapshot entry)
        {
            VisualElement section = CreateSection("Transform", marginBottom: 12f);
            Transform transform = entry.GameObject != null ? entry.GameObject.transform : null;
            if (transform == null)
            {
                section.Add(CreateEntityInfoRow("Transform", "-"));
                return section;
            }

            section.Add(CreateEntityInfoRow("Position", transform.position.ToString()));
            section.Add(CreateEntityInfoRow("Rotation", transform.rotation.eulerAngles.ToString()));
            section.Add(CreateEntityInfoRow("Scale", transform.localScale.ToString()));
            return section;
        }

        private VisualElement BuildRelationSection(EntityDebugSnapshot entry)
        {
            VisualElement section = CreateSection("Relations", marginBottom: 12f);
            section.Add(CreateEntityInfoRow("ChildCount", entry.ChildCount.ToString()));
            section.Add(CreateEntityReferenceRow("Parent", entry.Parent));

            if (entry.Children == null || entry.Children.Count == 0)
            {
                section.Add(CreateEntityInfoRow("Children", "无"));
                return section;
            }

            Label title = new("Children");
            title.style.marginTop = 8f;
            title.style.marginBottom = 4f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(title);

            for (int i = 0; i < entry.Children.Count; i++)
            {
                section.Add(CreateEntityReferenceRow($"[{i}]", entry.Children[i]));
            }

            return section;
        }

        private void SelectEntityFromContainerPreview(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) || !TryFindEntry(m_AllEntries, entityId, out EntityDebugSnapshot entry))
            {
                return;
            }

            m_SelectedContainerName = entry.ContainerName;
            m_SelectedEntityId = entityId;
            m_DetailSelectionKind = DetailSelectionKind.Entity;
            RefreshView();
            ShowEntityDetail(true);
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

        private static VisualElement CreateObjectFieldRow(string labelText, Type objectType, UnityEngine.Object value)
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

        private VisualElement CreateEntityReferenceRow(string labelText, EntityComponent entity)
        {
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

            Label label = new(labelText);
            label.style.width = 112f;
            label.style.flexShrink = 0f;
            label.style.color = new Color(0.72f, 0.72f, 0.72f);
            row.Add(label);

            ObjectField field = new()
            {
                objectType = typeof(EntityComponent),
                value = entity,
                allowSceneObjects = true
            };
            field.SetEnabled(false);
            field.style.flexGrow = 1f;
            field.style.minWidth = 0f;
            row.Add(field);

            Button selectButton = new(() => SelectObject(entity != null ? entity.gameObject : null))
            {
                text = "选中"
            };
            selectButton.style.width = 54f;
            selectButton.style.marginLeft = 6f;
            selectButton.SetEnabled(entity != null);
            row.Add(selectButton);

            Button pingButton = new(() => PingObject(entity != null ? entity.gameObject : null))
            {
                text = "Ping"
            };
            pingButton.style.width = 54f;
            pingButton.style.marginLeft = 4f;
            pingButton.SetEnabled(entity != null);
            row.Add(pingButton);

            Button copyButton = new(() => CopyToClipboard(entity != null ? entity.Id : string.Empty))
            {
                text = "复制Id"
            };
            copyButton.style.width = 64f;
            copyButton.style.marginLeft = 4f;
            copyButton.SetEnabled(entity != null && !string.IsNullOrEmpty(entity.Id));
            row.Add(copyButton);

            return row;
        }

        private static VisualElement CreateInspectorFieldRow(string labelText, VisualElement field)
        {
            VisualElement row = CreateInspectorRow(labelText);
            field.style.flexGrow = 1f;
            row.Add(field);
            return row;
        }

        private static VisualElement CreateEntityInfoRow(string labelText, string valueText)
        {
            Label value = new(string.IsNullOrEmpty(valueText) ? "-" : valueText);
            value.style.flexGrow = 1f;
            value.style.whiteSpace = WhiteSpace.Normal;
            value.style.color = new Color(0.86f, 0.86f, 0.86f);
            return CreateInspectorFieldRow(labelText, value);
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

        private int GetSelectedContainerIndex()
        {
            if (string.IsNullOrEmpty(m_SelectedContainerName))
            {
                return -1;
            }

            for (int i = 0; i < m_Containers.Count; i++)
            {
                if (m_Containers[i].Name == m_SelectedContainerName)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetSelectedEntityIndex()
        {
            if (string.IsNullOrEmpty(m_SelectedEntityId))
            {
                return -1;
            }

            for (int i = 0; i < m_FilteredEntries.Count; i++)
            {
                if (m_FilteredEntries[i].Id == m_SelectedEntityId)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryFindContainer(string containerName, out EntityContainerDebugSnapshot container)
        {
            if (!string.IsNullOrEmpty(containerName))
            {
                for (int i = 0; i < m_Containers.Count; i++)
                {
                    if (m_Containers[i].Name == containerName)
                    {
                        container = m_Containers[i];
                        return true;
                    }
                }
            }

            container = default;
            return false;
        }

        private static bool TryFindEntry(List<EntityDebugSnapshot> entries, string entityId, out EntityDebugSnapshot entry)
        {
            if (!string.IsNullOrEmpty(entityId))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Id == entityId)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }

        private List<EntityDebugSnapshot> GetEntriesInContainer(string containerName)
        {
            var entries = new List<EntityDebugSnapshot>();
            if (string.IsNullOrEmpty(containerName))
            {
                return entries;
            }

            for (int i = 0; i < m_AllEntries.Count; i++)
            {
                if (m_AllEntries[i].ContainerName == containerName)
                {
                    entries.Add(m_AllEntries[i]);
                }
            }

            return entries;
        }

        private static bool IsSearchMatch(EntityDebugSnapshot entry, string search)
        {
            return Contains(entry.Id, search)
                || Contains(entry.Alias, search)
                || Contains(entry.Name, search)
                || Contains(entry.SceneName, search)
                || Contains(entry.EntityType != null ? entry.EntityType.Name : string.Empty, search)
                || Contains(entry.EntityType != null ? entry.EntityType.FullName : string.Empty, search)
                || Contains(entry.GameObject != null ? entry.GameObject.name : string.Empty, search);
        }

        private static bool Contains(string text, string search)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetStatusText(EntityDebugSnapshot entry)
        {
            if (entry.Entity == null || entry.GameObject == null)
            {
                return "失效";
            }

            if (entry.ActiveInHierarchy)
            {
                return "激活";
            }

            return entry.ActiveSelf ? "父级关" : "未激活";
        }

        private static Color GetStatusColor(EntityDebugSnapshot entry)
        {
            if (entry.Entity == null || entry.GameObject == null)
            {
                return new Color(1f, 0.48f, 0.42f);
            }

            if (entry.ActiveInHierarchy)
            {
                return new Color(0.45f, 0.90f, 0.48f);
            }

            return new Color(0.95f, 0.78f, 0.35f);
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

        private static void CopyToClipboard(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            EditorGUIUtility.systemCopyBuffer = value;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "-";
            }

            Transform current = gameObject.transform;
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }

        private static string FormatEntityDisplayName(EntityDebugSnapshot entry)
        {
            string name = string.IsNullOrEmpty(entry.Name) ? "<No Name>" : entry.Name;
            string type = entry.EntityType != null ? entry.EntityType.Name : "-";
            string alias = string.IsNullOrEmpty(entry.Alias) ? string.Empty : $" | Alias: {entry.Alias}";
            return $"{name} ({type}){alias}";
        }

        private static string FormatBool(bool value)
        {
            return value ? "是" : "否";
        }

        private static string FormatEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private enum DetailSelectionKind
        {
            None,
            Container,
            Entity
        }
    }
}
