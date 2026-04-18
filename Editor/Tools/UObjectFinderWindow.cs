using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class UObjectFinderWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/UObject Finder";
        private const float LeftPaneWidth = 760f;
        private const float PathColumnWidth = 280f;
        private const float NameColumnWidth = 130f;
        private const float SceneColumnWidth = 120f;
        private const float KeyModeColumnWidth = 90f;

        private readonly List<UObjectReferenceEntry> m_AllEntries = new();
        private readonly List<UObjectReferenceEntry> m_FilteredEntries = new();
        private readonly List<UObjectFinderDisplayRow> m_DisplayRows = new();
        private readonly HashSet<string> m_ExpandedListKeys = new();

        private TextField m_SearchField;
        private Toggle m_OnlyConflictsToggle;
        private Toggle m_OnlyInactiveToggle;
        private DropdownField m_KeyFilterField;
        private DropdownField m_ModeFilterField;
        private ListView m_ListView;
        private Label m_SummaryLabel;
        private Label m_DetailTitleLabel;
        private Label m_DetailStatusLabel;
        private ObjectField m_DetailObjectField;
        private Label m_DetailSceneLabel;
        private Label m_DetailHierarchyLabel;
        private Label m_DetailActiveLabel;
        private Label m_DetailKeyModeLabel;
        private Label m_DetailRegistrationModeLabel;
        private Label m_DetailPathLabel;
        private Label m_DetailMessageLabel;
        private Button m_DetailLocateButton;
        private Button m_DetailCopyPathButton;
        private Button m_DetailCopyHierarchyButton;

        private UObjectFinderDisplayRow m_SelectedRow;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<UObjectFinderWindow>();
            window.titleContent = new GUIContent("UObject Finder");
            window.minSize = new Vector2(900f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += OnTrackedEditorStateChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            Undo.undoRedoPerformed += OnTrackedEditorStateChanged;

            RefreshEntries();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnTrackedEditorStateChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
            Undo.undoRedoPerformed -= OnTrackedEditorStateChanged;
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
            m_SummaryLabel.style.marginLeft = 2;
            m_SummaryLabel.style.marginTop = 4;
            m_SummaryLabel.style.marginBottom = 6;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            root.Add(m_SummaryLabel);

            var splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            splitView.Add(BuildListPane());
            splitView.Add(BuildDetailPane());
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 2;

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1;
            m_SearchField.style.minWidth = 160f;
            m_SearchField.tooltip = "搜索路径、对象名、场景名、注册模式或层级路径";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_SearchField);

            m_OnlyConflictsToggle = new Toggle("仅冲突项");
            m_OnlyConflictsToggle.style.marginLeft = 8;
            m_OnlyConflictsToggle.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_OnlyConflictsToggle);

            m_OnlyInactiveToggle = new Toggle("仅Inactive");
            m_OnlyInactiveToggle.style.marginLeft = 8;
            m_OnlyInactiveToggle.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_OnlyInactiveToggle);

            m_KeyFilterField = new DropdownField("Key筛选", new List<string>
            {
                "全部",
                "仅自定义Key",
                "仅默认Name"
            }, 0);
            m_KeyFilterField.style.width = 170f;
            m_KeyFilterField.style.marginLeft = 8;
            m_KeyFilterField.tooltip = "按是否使用自定义 Key 过滤";
            m_KeyFilterField.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_KeyFilterField);

            m_ModeFilterField = new DropdownField("注册筛选", new List<string>
            {
                "全部",
                "仅Single",
                "仅List"
            }, 0);
            m_ModeFilterField.style.width = 150f;
            m_ModeFilterField.style.marginLeft = 8;
            m_ModeFilterField.tooltip = "按 UObjectFinder 注册模式过滤";
            m_ModeFilterField.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_ModeFilterField);

            var refreshButton = new Button(RefreshEntries)
            {
                text = "刷新"
            };
            refreshButton.style.marginLeft = 8;
            refreshButton.style.width = 64f;
            toolbar.Add(refreshButton);

            return toolbar;
        }

        private VisualElement BuildListPane()
        {
            var pane = new XBox();
            pane.style.flexGrow = 1;
            pane.style.paddingLeft = 4;
            pane.style.paddingRight = 4;
            pane.style.paddingTop = 4;
            pane.style.paddingBottom = 4;
            pane.style.marginRight = 4;

            pane.Add(BuildListHeader());

            m_ListView = new ListView
            {
                itemsSource = m_DisplayRows,
                fixedItemHeight = 24,
                selectionType = SelectionType.Single
            };
            m_ListView.style.flexGrow = 1;
            m_ListView.style.marginTop = 4;
            m_ListView.makeItem = MakeListItem;
            m_ListView.bindItem = BindListItem;
            m_ListView.onSelectionChange += OnSelectionChanged;
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

            header.Add(CreateHeaderLabel("Path", PathColumnWidth));
            header.Add(CreateHeaderLabel("对象", NameColumnWidth));
            header.Add(CreateHeaderLabel("场景", SceneColumnWidth));
            header.Add(CreateHeaderLabel("Key模式", KeyModeColumnWidth));

            Label statusLabel = CreateHeaderLabel("状态", 90f);
            statusLabel.style.flexGrow = 1;
            header.Add(statusLabel);

            return header;
        }

        private VisualElement BuildDetailPane()
        {
            var pane = new XBox();
            pane.style.flexGrow = 1;
            pane.style.paddingLeft = 10;
            pane.style.paddingRight = 10;
            pane.style.paddingTop = 10;
            pane.style.paddingBottom = 10;
            pane.style.marginLeft = 4;

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            pane.Add(scrollView);

            m_DetailTitleLabel = new Label("未选择条目");
            m_DetailTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_DetailTitleLabel.style.fontSize = 14;
            m_DetailTitleLabel.style.marginBottom = 8;
            scrollView.Add(m_DetailTitleLabel);

            m_DetailStatusLabel = new Label("从左侧选择一个 UObjectReference 查看详情。");
            m_DetailStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            m_DetailStatusLabel.style.marginBottom = 10;
            scrollView.Add(m_DetailStatusLabel);

            m_DetailObjectField = new ObjectField("对象")
            {
                objectType = typeof(GameObject)
            };
            m_DetailObjectField.SetEnabled(false);
            scrollView.Add(m_DetailObjectField);

            m_DetailSceneLabel = AddDetailRow(scrollView, "场景");
            m_DetailHierarchyLabel = AddDetailRow(scrollView, "层级路径");
            m_DetailActiveLabel = AddDetailRow(scrollView, "激活状态");
            m_DetailKeyModeLabel = AddDetailRow(scrollView, "Key模式");
            m_DetailRegistrationModeLabel = AddDetailRow(scrollView, "注册模式");
            m_DetailPathLabel = AddDetailRow(scrollView, "解析路径");
            m_DetailMessageLabel = AddDetailRow(scrollView, "说明", true);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 12;
            scrollView.Add(buttonRow);

            m_DetailLocateButton = new Button(() => LocateEntry(GetSelectedEntry()))
            {
                text = "定位"
            };
            m_DetailLocateButton.style.width = 78f;
            buttonRow.Add(m_DetailLocateButton);

            m_DetailCopyPathButton = new Button(() => CopyToClipboard(GetSelectedPathForCopy()))
            {
                text = "复制路径"
            };
            m_DetailCopyPathButton.style.marginLeft = 8;
            m_DetailCopyPathButton.style.width = 90f;
            buttonRow.Add(m_DetailCopyPathButton);

            m_DetailCopyHierarchyButton = new Button(() => CopyToClipboard(GetSelectedHierarchyForCopy()))
            {
                text = "复制层级路径"
            };
            m_DetailCopyHierarchyButton.style.marginLeft = 8;
            m_DetailCopyHierarchyButton.style.width = 110f;
            buttonRow.Add(m_DetailCopyHierarchyButton);

            return pane;
        }

        private static Label CreateHeaderLabel(string text, float width)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.maxWidth = width;
            label.style.minWidth = width;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        private static Label AddDetailRow(VisualElement parent, string title, bool multiline = false)
        {
            var container = new VisualElement();
            container.style.marginTop = 8;
            parent.Add(container);

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 2;
            container.Add(titleLabel);

            var valueLabel = new Label("-");
            valueLabel.style.whiteSpace = multiline ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            valueLabel.style.overflow = multiline ? Overflow.Visible : Overflow.Hidden;
            if (!multiline)
            {
                valueLabel.style.textOverflow = TextOverflow.Ellipsis;
            }
            valueLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            container.Add(valueLabel);

            return valueLabel;
        }

        private VisualElement MakeListItem()
        {
            return new UObjectFinderRow(ToggleListGroup, LocateEntry);
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_DisplayRows.Count)
            {
                return;
            }

            if (element is not UObjectFinderRow row)
            {
                return;
            }

            row.Bind(m_DisplayRows[index], index % 2 == 0);
        }

        private void RefreshEntries()
        {
            int selectedReferenceId = GetSelectedEntry()?.Reference != null ? GetSelectedEntry().Reference.GetInstanceID() : 0;
            string selectedGroupKey = GetSelectedGroup()?.Key;

            m_AllEntries.Clear();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject rootGameObject in scene.GetRootGameObjects())
                {
                    if (rootGameObject == null)
                    {
                        continue;
                    }

                    UObjectReference[] references = rootGameObject.GetComponentsInChildren<UObjectReference>(true);
                    foreach (UObjectReference reference in references)
                    {
                        if (reference != null)
                        {
                            m_AllEntries.Add(CreateEntry(reference));
                        }
                    }
                }
            }

            MarkConflicts(m_AllEntries);
            m_AllEntries.Sort(CompareEntriesForDisplay);
            RefreshFilteredEntries(selectedReferenceId, selectedGroupKey);
        }

        private void RefreshFilteredEntries()
        {
            int selectedReferenceId = GetSelectedEntry()?.Reference != null ? GetSelectedEntry().Reference.GetInstanceID() : 0;
            string selectedGroupKey = GetSelectedGroup()?.Key;
            RefreshFilteredEntries(selectedReferenceId, selectedGroupKey);
        }

        private void RefreshFilteredEntries(int selectedReferenceId, string selectedGroupKey)
        {
            m_FilteredEntries.Clear();

            string search = m_SearchField?.value?.Trim();
            bool onlyConflicts = m_OnlyConflictsToggle?.value ?? false;
            bool onlyInactive = m_OnlyInactiveToggle?.value ?? false;
            string keyFilter = m_KeyFilterField?.value ?? "全部";
            string modeFilter = m_ModeFilterField?.value ?? "全部";

            foreach (UObjectReferenceEntry entry in m_AllEntries)
            {
                if (onlyConflicts && !entry.HasConflict)
                {
                    continue;
                }

                if (onlyInactive && entry.IsActiveInHierarchy)
                {
                    continue;
                }

                if (!MatchesKeyFilter(entry, keyFilter) || !MatchesModeFilter(entry, modeFilter))
                {
                    continue;
                }

                if (!MatchesSearch(entry, search))
                {
                    continue;
                }

                m_FilteredEntries.Add(entry);
            }

            BuildDisplayRows();
            RefreshView(selectedReferenceId, selectedGroupKey);
        }

        private void BuildDisplayRows()
        {
            m_DisplayRows.Clear();

            var topLevelItems = new List<TopLevelDisplayItem>();

            foreach (UObjectReferenceEntry entry in m_FilteredEntries.Where(item => item.Mode == UObjectReference.RegistrationMode.Single))
            {
                topLevelItems.Add(TopLevelDisplayItem.CreateSingle(entry));
            }

            foreach (IGrouping<string, UObjectReferenceEntry> group in m_FilteredEntries
                         .Where(item => item.Mode == UObjectReference.RegistrationMode.List)
                         .GroupBy(item => item.ResolvedPath)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                UObjectFinderListGroup listGroup = CreateListGroup(group);
                topLevelItems.Add(TopLevelDisplayItem.CreateGroup(listGroup));
            }

            topLevelItems.Sort(CompareTopLevelItems);

            foreach (TopLevelDisplayItem item in topLevelItems)
            {
                if (item.SingleEntry != null)
                {
                    m_DisplayRows.Add(UObjectFinderDisplayRow.CreateSingle(item.SingleEntry));
                    continue;
                }

                if (item.Group == null)
                {
                    continue;
                }

                bool isExpanded = m_ExpandedListKeys.Contains(item.Group.Key);
                m_DisplayRows.Add(UObjectFinderDisplayRow.CreateGroup(item.Group, isExpanded));

                if (!isExpanded)
                {
                    continue;
                }

                foreach (UObjectReferenceEntry childEntry in item.Group.Items)
                {
                    m_DisplayRows.Add(UObjectFinderDisplayRow.CreateListItem(item.Group, childEntry));
                }
            }
        }

        private void RefreshView(int selectedReferenceId = 0, string selectedGroupKey = null)
        {
            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_DisplayRows;
                m_ListView.Rebuild();
                RestoreSelectionInView(selectedReferenceId, selectedGroupKey);
            }

            UpdateSummary();
            RefreshDetailPane();
        }

        private void RestoreSelectionInView(int selectedReferenceId, string selectedGroupKey)
        {
            if (m_ListView == null)
            {
                return;
            }

            int index = -1;
            if (selectedReferenceId != 0)
            {
                index = m_DisplayRows.FindIndex(row => row.Entry?.Reference != null && row.Entry.Reference.GetInstanceID() == selectedReferenceId);
            }

            if (index < 0 && !string.IsNullOrEmpty(selectedGroupKey))
            {
                index = m_DisplayRows.FindIndex(row => row.Group != null && row.Group.Key == selectedGroupKey && row.Kind == DisplayRowKind.ListGroup);
            }

            if (index < 0)
            {
                m_SelectedRow = null;
                m_ListView.ClearSelection();
                return;
            }

            m_SelectedRow = m_DisplayRows[index];
            m_ListView.SetSelectionWithoutNotify(new[] { index });
            m_ListView.ScrollToItem(index);
        }

        private void UpdateSummary()
        {
            if (m_SummaryLabel == null)
            {
                return;
            }

            int duplicateCount = m_AllEntries.Count(entry => entry.Status.HasFlag(EntryStatus.Duplicate));
            int invalidCount = m_AllEntries.Count(entry => entry.Status.HasFlag(EntryStatus.InvalidKey));
            int inactiveCount = m_AllEntries.Count(entry => !entry.IsActiveInHierarchy);
            int listGroupCount = m_FilteredEntries.Where(entry => entry.Mode == UObjectReference.RegistrationMode.List)
                .Select(entry => entry.ResolvedPath)
                .Distinct()
                .Count();

            m_SummaryLabel.text = $"已显示 {m_FilteredEntries.Count} 项 | List组: {listGroupCount} | Duplicate: {duplicateCount} | Invalid Key: {invalidCount} | Inactive: {inactiveCount}";
        }

        private void RefreshDetailPane()
        {
            if (m_DetailTitleLabel == null)
            {
                return;
            }

            UObjectReferenceEntry selectedEntry = GetSelectedEntry();
            UObjectFinderListGroup selectedGroup = GetSelectedGroup();

            if (selectedEntry != null)
            {
                m_DetailTitleLabel.text = selectedEntry.GameObjectName;
                m_DetailStatusLabel.text = GetDisplayStatus(selectedEntry);
                m_DetailStatusLabel.style.color = GetStatusColor(selectedEntry);

                m_DetailObjectField.value = selectedEntry.GameObject;
                m_DetailSceneLabel.text = selectedEntry.SceneName;
                m_DetailHierarchyLabel.text = selectedEntry.HierarchyPath;
                m_DetailHierarchyLabel.tooltip = selectedEntry.HierarchyPath;
                m_DetailActiveLabel.text = selectedEntry.IsActiveInHierarchy ? "Active In Hierarchy" : "Inactive In Hierarchy";
                m_DetailKeyModeLabel.text = GetKeyModeDisplay(selectedEntry);
                m_DetailRegistrationModeLabel.text = GetRegistrationModeDisplay(selectedEntry);
                m_DetailPathLabel.text = GetPathDisplay(selectedEntry);
                m_DetailPathLabel.tooltip = GetPathDisplay(selectedEntry);
                m_DetailMessageLabel.text = selectedEntry.StatusMessage;

                m_DetailLocateButton.SetEnabled(true);
                m_DetailCopyPathButton.SetEnabled(true);
                m_DetailCopyHierarchyButton.SetEnabled(true);
                return;
            }

            if (selectedGroup != null)
            {
                m_DetailTitleLabel.text = GetPathDisplay(selectedGroup.Key);
                m_DetailStatusLabel.text = GetGroupStatusText(selectedGroup);
                m_DetailStatusLabel.style.color = GetGroupStatusColor(selectedGroup);

                m_DetailObjectField.value = null;
                m_DetailSceneLabel.text = selectedGroup.SceneSummary;
                m_DetailHierarchyLabel.text = $"共 {selectedGroup.Items.Count} 项";
                m_DetailHierarchyLabel.tooltip = string.Join("\n", selectedGroup.Items.Select(item => item.HierarchyPath));
                m_DetailActiveLabel.text = GetGroupActiveDisplay(selectedGroup);
                m_DetailKeyModeLabel.text = "自定义Key";
                m_DetailRegistrationModeLabel.text = "List Group";
                m_DetailPathLabel.text = GetPathDisplay(selectedGroup.Key);
                m_DetailPathLabel.tooltip = GetPathDisplay(selectedGroup.Key);
                m_DetailMessageLabel.text = GetGroupStatusMessage(selectedGroup);

                m_DetailLocateButton.SetEnabled(false);
                m_DetailCopyPathButton.SetEnabled(true);
                m_DetailCopyHierarchyButton.SetEnabled(false);
                return;
            }

            m_DetailTitleLabel.text = "未选择条目";
            m_DetailStatusLabel.text = "从左侧选择一个 UObjectReference 查看详情。";
            m_DetailStatusLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            m_DetailObjectField.value = null;
            m_DetailSceneLabel.text = "-";
            m_DetailHierarchyLabel.text = "-";
            m_DetailHierarchyLabel.tooltip = string.Empty;
            m_DetailActiveLabel.text = "-";
            m_DetailKeyModeLabel.text = "-";
            m_DetailRegistrationModeLabel.text = "-";
            m_DetailPathLabel.text = "-";
            m_DetailPathLabel.tooltip = string.Empty;
            m_DetailMessageLabel.text = "-";
            m_DetailLocateButton.SetEnabled(false);
            m_DetailCopyPathButton.SetEnabled(false);
            m_DetailCopyHierarchyButton.SetEnabled(false);
        }

        private static bool MatchesSearch(UObjectReferenceEntry entry, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            return ContainsIgnoreCase(entry.ResolvedPath, search)
                   || ContainsIgnoreCase(GetPathDisplay(entry), search)
                   || ContainsIgnoreCase(entry.GameObjectName, search)
                   || ContainsIgnoreCase(entry.SceneName, search)
                   || ContainsIgnoreCase(GetKeyModeDisplay(entry), search)
                   || ContainsIgnoreCase(GetRegistrationModeDisplay(entry), search)
                   || ContainsIgnoreCase(entry.HierarchyPath, search);
        }

        private static bool MatchesKeyFilter(UObjectReferenceEntry entry, string keyFilter)
        {
            return keyFilter switch
            {
                "仅自定义Key" => entry.UseCustomKey,
                "仅默认Name" => !entry.UseCustomKey,
                _ => true,
            };
        }

        private static bool MatchesModeFilter(UObjectReferenceEntry entry, string modeFilter)
        {
            return modeFilter switch
            {
                "仅Single" => entry.Mode == UObjectReference.RegistrationMode.Single,
                "仅List" => entry.Mode == UObjectReference.RegistrationMode.List,
                _ => true,
            };
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static UObjectReferenceEntry CreateEntry(UObjectReference reference)
        {
            SerializedObject serializedObject = new SerializedObject(reference);
            serializedObject.Update();
            SerializedProperty keyProperty = serializedObject.FindProperty("key");
            SerializedProperty modeProperty = serializedObject.FindProperty("registrationMode");

            string key = keyProperty != null ? keyProperty.stringValue : string.Empty;
            UObjectReference.RegistrationMode mode = modeProperty != null
                ? (UObjectReference.RegistrationMode)modeProperty.enumValueIndex
                : UObjectReference.RegistrationMode.Single;

            EntryStatus status = EntryStatus.None;
            if (reference.UseKey && string.IsNullOrWhiteSpace(key))
            {
                status |= EntryStatus.InvalidKey;
            }

            return new UObjectReferenceEntry
            {
                Reference = reference,
                GameObject = reference.gameObject,
                GameObjectName = reference.gameObject.name,
                UseCustomKey = reference.UseKey,
                CustomKey = key,
                Mode = mode,
                ResolvedPath = reference.Path,
                HierarchyPath = BuildHierarchyPath(reference.transform),
                SceneName = GetSceneDisplayName(reference.gameObject.scene),
                IsActiveInHierarchy = reference.gameObject.activeInHierarchy,
                Status = status
            };
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var stack = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", stack);
        }

        private static string GetSceneDisplayName(Scene scene)
        {
            if (!scene.IsValid())
            {
                return "<Invalid Scene>";
            }

            if (!string.IsNullOrEmpty(scene.name))
            {
                return scene.name;
            }

            return string.IsNullOrEmpty(scene.path) ? "<Untitled Scene>" : scene.path;
        }

        private static void MarkConflicts(List<UObjectReferenceEntry> entries)
        {
            foreach (UObjectReferenceEntry entry in entries)
            {
                entry.ListGroupCount = 1;
                entry.StatusMessage = BuildStatusMessage(entry);
            }

            foreach (IGrouping<string, UObjectReferenceEntry> group in entries
                         .Where(entry => entry.Mode == UObjectReference.RegistrationMode.Single)
                         .GroupBy(entry => entry.ResolvedPath))
            {
                if (group.Count() <= 1)
                {
                    continue;
                }

                foreach (UObjectReferenceEntry entry in group)
                {
                    entry.Status |= EntryStatus.Duplicate;
                }
            }

            foreach (IGrouping<string, UObjectReferenceEntry> group in entries
                         .Where(entry => entry.Mode == UObjectReference.RegistrationMode.List)
                         .GroupBy(entry => entry.ResolvedPath))
            {
                int count = group.Count();
                foreach (UObjectReferenceEntry entry in group)
                {
                    entry.ListGroupCount = count;
                }
            }

            foreach (UObjectReferenceEntry entry in entries)
            {
                entry.StatusMessage = BuildStatusMessage(entry);
            }
        }

        private static string BuildStatusMessage(UObjectReferenceEntry entry)
        {
            var parts = new List<string>();

            if (entry.Status.HasFlag(EntryStatus.Duplicate))
            {
                parts.Add("Duplicate: 当前已加载场景中存在相同解析路径的多个 UObjectReference。");
            }

            if (entry.Status.HasFlag(EntryStatus.InvalidKey))
            {
                parts.Add("Invalid Key: key 为空或仅包含空白字符。");
            }

            if (entry.Mode == UObjectReference.RegistrationMode.List)
            {
                parts.Add($"List: 当前列表 key 共 {entry.ListGroupCount} 项。");
            }

            if (!entry.IsActiveInHierarchy)
            {
                parts.Add("Inactive: 该对象当前在 Hierarchy 中未激活。");
            }

            return parts.Count == 0 ? "状态正常。" : string.Join(" ", parts);
        }

        private static int GetSortPriority(UObjectReferenceEntry entry)
        {
            if (entry.Status.HasFlag(EntryStatus.Duplicate))
            {
                return 0;
            }

            if (entry.Status.HasFlag(EntryStatus.InvalidKey))
            {
                return 1;
            }

            return 2;
        }

        private static int CompareEntriesForDisplay(UObjectReferenceEntry left, UObjectReferenceEntry right)
        {
            int severityCompare = GetSortPriority(left).CompareTo(GetSortPriority(right));
            if (severityCompare != 0)
            {
                return severityCompare;
            }

            int pathCompare = string.Compare(left.ResolvedPath, right.ResolvedPath, StringComparison.OrdinalIgnoreCase);
            if (pathCompare != 0)
            {
                return pathCompare;
            }

            int sceneCompare = string.Compare(left.SceneName, right.SceneName, StringComparison.OrdinalIgnoreCase);
            if (sceneCompare != 0)
            {
                return sceneCompare;
            }

            return string.Compare(left.HierarchyPath, right.HierarchyPath, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareEntriesForGroupChildren(UObjectReferenceEntry left, UObjectReferenceEntry right)
        {
            int sceneCompare = string.Compare(left.SceneName, right.SceneName, StringComparison.OrdinalIgnoreCase);
            if (sceneCompare != 0)
            {
                return sceneCompare;
            }

            return string.Compare(left.HierarchyPath, right.HierarchyPath, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareTopLevelItems(TopLevelDisplayItem left, TopLevelDisplayItem right)
        {
            return CompareEntriesForDisplay(left.SortEntry, right.SortEntry);
        }

        private static UObjectFinderListGroup CreateListGroup(IGrouping<string, UObjectReferenceEntry> group)
        {
            var items = group.OrderBy(entry => entry, Comparer<UObjectReferenceEntry>.Create(CompareEntriesForGroupChildren)).ToList();
            var sceneNames = items.Select(entry => entry.SceneName).Distinct().ToList();

            return new UObjectFinderListGroup
            {
                Key = group.Key,
                Items = items,
                SceneSummary = sceneNames.Count == 1 ? sceneNames[0] : $"多场景 ({sceneNames.Count})",
                HasInvalidKey = items.Any(entry => entry.Status.HasFlag(EntryStatus.InvalidKey)),
                HasInactive = items.Any(entry => !entry.IsActiveInHierarchy),
            };
        }

        private static string GetDisplayStatus(UObjectReferenceEntry entry)
        {
            var parts = new List<string>
            {
                entry.Mode == UObjectReference.RegistrationMode.List ? $"List x{entry.ListGroupCount}" : "Single"
            };

            if (entry.Status.HasFlag(EntryStatus.Duplicate))
            {
                parts.Add("Duplicate");
            }

            if (entry.Status.HasFlag(EntryStatus.InvalidKey))
            {
                parts.Add("Invalid Key");
            }

            if (!entry.IsActiveInHierarchy)
            {
                parts.Add("Inactive");
            }

            return string.Join(" | ", parts);
        }

        private static string GetGroupStatusText(UObjectFinderListGroup group)
        {
            var parts = new List<string> { $"List Group x{group.Items.Count}" };

            if (group.HasInvalidKey)
            {
                parts.Add("Invalid Key");
            }

            if (group.HasInactive)
            {
                parts.Add("Inactive");
            }

            return string.Join(" | ", parts);
        }

        private static string GetPathDisplay(UObjectReferenceEntry entry)
        {
            return GetPathDisplay(entry?.ResolvedPath);
        }

        private static string GetPathDisplay(string path)
        {
            return string.IsNullOrEmpty(path) ? "<空路径>" : path;
        }

        private static string GetRegistrationModeDisplay(UObjectReferenceEntry entry)
        {
            return entry.Mode == UObjectReference.RegistrationMode.List ? "List" : "Single";
        }

        private static string GetKeyModeDisplay(UObjectReferenceEntry entry)
        {
            if (entry.UseCustomKey)
            {
                return string.IsNullOrWhiteSpace(entry.CustomKey)
                    ? "自定义Key: <空>"
                    : $"自定义Key: {entry.CustomKey}";
            }

            return "默认Name";
        }

        private static string GetGroupStatusMessage(UObjectFinderListGroup group)
        {
            var parts = new List<string> { $"List Group: 当前列表 key 共 {group.Items.Count} 项。" };

            if (group.HasInvalidKey)
            {
                parts.Add("存在 key 为空或仅包含空白字符的条目。");
            }

            if (group.HasInactive)
            {
                parts.Add("存在未激活的条目。");
            }

            return string.Join(" ", parts);
        }

        private static string GetGroupActiveDisplay(UObjectFinderListGroup group)
        {
            int activeCount = group.Items.Count(entry => entry.IsActiveInHierarchy);
            if (activeCount == 0)
            {
                return "全部 Inactive";
            }

            if (activeCount == group.Items.Count)
            {
                return "全部 Active";
            }

            return $"Mixed ({activeCount}/{group.Items.Count} Active)";
        }

        private static Color GetStatusColor(UObjectReferenceEntry entry)
        {
            if (entry.Status.HasFlag(EntryStatus.Duplicate))
            {
                return new Color(1f, 0.55f, 0.3f);
            }

            if (entry.Status.HasFlag(EntryStatus.InvalidKey))
            {
                return new Color(0.98f, 0.78f, 0.35f);
            }

            if (!entry.IsActiveInHierarchy)
            {
                return new Color(0.6f, 0.7f, 0.95f);
            }

            return new Color(0.75f, 0.92f, 0.75f);
        }

        private static Color GetGroupStatusColor(UObjectFinderListGroup group)
        {
            if (group.HasInvalidKey)
            {
                return new Color(0.98f, 0.78f, 0.35f);
            }

            if (group.HasInactive)
            {
                return new Color(0.6f, 0.7f, 0.95f);
            }

            return new Color(0.75f, 0.92f, 0.75f);
        }

        private static void CopyToClipboard(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                EditorGUIUtility.systemCopyBuffer = value;
            }
        }

        private static void LocateEntry(UObjectReferenceEntry entry)
        {
            if (entry?.GameObject == null)
            {
                return;
            }

            Selection.activeGameObject = entry.GameObject;
            EditorGUIUtility.PingObject(entry.GameObject);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void ToggleListGroup(UObjectFinderListGroup group)
        {
            if (group == null)
            {
                return;
            }

            if (!m_ExpandedListKeys.Add(group.Key))
            {
                m_ExpandedListKeys.Remove(group.Key);
            }

            RefreshFilteredEntries();
        }

        private void OnSelectionChanged(IEnumerable<object> selectedItems)
        {
            m_SelectedRow = selectedItems.OfType<UObjectFinderDisplayRow>().FirstOrDefault();
            RefreshDetailPane();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RefreshEntries();
        }

        private void OnSceneClosed(Scene scene)
        {
            RefreshEntries();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            RefreshEntries();
        }

        private void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            RefreshEntries();
        }

        private void OnTrackedEditorStateChanged()
        {
            RefreshEntries();
        }

        private UObjectReferenceEntry GetSelectedEntry()
        {
            return m_SelectedRow?.Entry;
        }

        private UObjectFinderListGroup GetSelectedGroup()
        {
            if (m_SelectedRow == null)
            {
                return null;
            }

            return m_SelectedRow.Kind == DisplayRowKind.SingleEntry ? null : m_SelectedRow.Group;
        }

        private string GetSelectedPathForCopy()
        {
            if (GetSelectedEntry() != null)
            {
                return GetSelectedEntry().ResolvedPath;
            }

            return GetSelectedGroup()?.Key;
        }

        private string GetSelectedHierarchyForCopy()
        {
            return GetSelectedEntry()?.HierarchyPath;
        }

        [Flags]
        private enum EntryStatus
        {
            None = 0,
            Duplicate = 1 << 0,
            InvalidKey = 1 << 1,
        }

        private enum DisplayRowKind
        {
            SingleEntry,
            ListGroup,
            ListItem,
        }

        private sealed class UObjectReferenceEntry
        {
            public UObjectReference Reference;
            public GameObject GameObject;
            public string GameObjectName;
            public bool UseCustomKey;
            public string CustomKey;
            public UObjectReference.RegistrationMode Mode;
            public string ResolvedPath;
            public string HierarchyPath;
            public string SceneName;
            public bool IsActiveInHierarchy;
            public EntryStatus Status;
            public string StatusMessage;
            public int ListGroupCount;
            public bool HasConflict => Status != EntryStatus.None;
        }

        private sealed class UObjectFinderListGroup
        {
            public string Key;
            public List<UObjectReferenceEntry> Items;
            public string SceneSummary;
            public bool HasInvalidKey;
            public bool HasInactive;
        }

        private sealed class TopLevelDisplayItem
        {
            public UObjectReferenceEntry SingleEntry;
            public UObjectFinderListGroup Group;
            public UObjectReferenceEntry SortEntry;

            public static TopLevelDisplayItem CreateSingle(UObjectReferenceEntry entry)
            {
                return new TopLevelDisplayItem
                {
                    SingleEntry = entry,
                    SortEntry = entry
                };
            }

            public static TopLevelDisplayItem CreateGroup(UObjectFinderListGroup group)
            {
                return new TopLevelDisplayItem
                {
                    Group = group,
                    SortEntry = group.Items[0]
                };
            }
        }

        private sealed class UObjectFinderDisplayRow
        {
            public DisplayRowKind Kind;
            public UObjectReferenceEntry Entry;
            public UObjectFinderListGroup Group;
            public bool IsExpanded;

            public static UObjectFinderDisplayRow CreateSingle(UObjectReferenceEntry entry)
            {
                return new UObjectFinderDisplayRow
                {
                    Kind = DisplayRowKind.SingleEntry,
                    Entry = entry
                };
            }

            public static UObjectFinderDisplayRow CreateGroup(UObjectFinderListGroup group, bool isExpanded)
            {
                return new UObjectFinderDisplayRow
                {
                    Kind = DisplayRowKind.ListGroup,
                    Group = group,
                    IsExpanded = isExpanded
                };
            }

            public static UObjectFinderDisplayRow CreateListItem(UObjectFinderListGroup group, UObjectReferenceEntry entry)
            {
                return new UObjectFinderDisplayRow
                {
                    Kind = DisplayRowKind.ListItem,
                    Group = group,
                    Entry = entry
                };
            }
        }

        private sealed class UObjectFinderRow : XItemBox
        {
            private readonly VisualElement m_PathContainer;
            private readonly VisualElement m_IndentElement;
            private readonly Button m_ExpandButton;
            private readonly Label m_PathLabel;
            private readonly Label m_NameLabel;
            private readonly Label m_SceneLabel;
            private readonly Label m_KeyModeLabel;
            private readonly Label m_StatusLabel;
            private readonly Button m_LocateButton;

            public UObjectFinderRow(Action<UObjectFinderListGroup> toggleAction, Action<UObjectReferenceEntry> locateAction)
            {
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                style.paddingLeft = 5f;
                style.paddingRight = 6f;

                m_PathContainer = new VisualElement();
                m_PathContainer.style.flexDirection = FlexDirection.Row;
                m_PathContainer.style.alignItems = Align.Center;
                m_PathContainer.style.width = PathColumnWidth;
                m_PathContainer.style.maxWidth = PathColumnWidth;
                m_PathContainer.style.minWidth = PathColumnWidth;
                Add(m_PathContainer);

                m_IndentElement = new VisualElement();
                m_IndentElement.style.width = 0f;
                m_PathContainer.Add(m_IndentElement);

                m_ExpandButton = new Button(() =>
                {
                    if (userData is UObjectFinderDisplayRow row && row.Kind == DisplayRowKind.ListGroup)
                    {
                        toggleAction(row.Group);
                    }
                })
                {
                    text = "▶"
                };
                m_ExpandButton.style.width = 18f;
                m_ExpandButton.style.height = 18f;
                m_ExpandButton.style.marginRight = 4f;
                m_ExpandButton.style.paddingLeft = 0f;
                m_ExpandButton.style.paddingRight = 0f;
                m_ExpandButton.style.fontSize = 9f;
                m_PathContainer.Add(m_ExpandButton);

                m_PathLabel = CreateFlexibleLabel();
                m_PathContainer.Add(m_PathLabel);

                m_NameLabel = CreateFixedLabel(NameColumnWidth);
                Add(m_NameLabel);

                m_SceneLabel = CreateFixedLabel(SceneColumnWidth);
                Add(m_SceneLabel);

                m_KeyModeLabel = CreateFixedLabel(KeyModeColumnWidth);
                Add(m_KeyModeLabel);

                var statusContainer = new VisualElement();
                statusContainer.style.flexDirection = FlexDirection.Row;
                statusContainer.style.alignItems = Align.Center;
                statusContainer.style.flexGrow = 1f;
                Add(statusContainer);

                m_StatusLabel = new Label();
                m_StatusLabel.style.flexGrow = 1f;
                m_StatusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                m_StatusLabel.style.whiteSpace = WhiteSpace.NoWrap;
                m_StatusLabel.style.overflow = Overflow.Hidden;
                m_StatusLabel.style.textOverflow = TextOverflow.Ellipsis;
                statusContainer.Add(m_StatusLabel);

                m_LocateButton = new Button(() =>
                {
                    if (userData is UObjectFinderDisplayRow row && row.Entry != null)
                    {
                        locateAction(row.Entry);
                    }
                })
                {
                    text = "定位"
                };
                m_LocateButton.style.width = 48f;
                m_LocateButton.style.height = 18f;
                m_LocateButton.style.fontSize = 10f;
                statusContainer.Add(m_LocateButton);

                RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.clickCount != 2 || userData is not UObjectFinderDisplayRow row)
                    {
                        return;
                    }

                    if (row.Kind == DisplayRowKind.ListGroup)
                    {
                        toggleAction(row.Group);
                    }
                    else if (row.Entry != null)
                    {
                        locateAction(row.Entry);
                    }
                });
            }

            public void Bind(UObjectFinderDisplayRow row, bool useOddStyle)
            {
                userData = row;
                style.backgroundColor = useOddStyle
                    ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                    : new Color(0.3f, 0.3f, 0.3f, 0.18f);

                if (row.Kind == DisplayRowKind.ListGroup)
                {
                    BindGroupRow(row);
                    return;
                }

                BindEntryRow(row);
            }

            private void BindGroupRow(UObjectFinderDisplayRow row)
            {
                UObjectFinderListGroup group = row.Group;

                m_IndentElement.style.width = 0f;
                m_ExpandButton.style.display = DisplayStyle.Flex;
                m_ExpandButton.text = row.IsExpanded ? "▼" : "▶";
                m_LocateButton.style.display = DisplayStyle.None;

                m_PathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                m_PathLabel.text = GetPathDisplay(group.Key);
                m_PathLabel.tooltip = GetPathDisplay(group.Key);
                m_NameLabel.text = $"{group.Items.Count} Items";
                m_NameLabel.tooltip = $"{group.Items.Count} items";
                m_SceneLabel.text = group.SceneSummary;
                m_SceneLabel.tooltip = group.SceneSummary;
                m_KeyModeLabel.text = "自定义Key";
                m_KeyModeLabel.tooltip = "列表组固定使用自定义Key";
                m_StatusLabel.text = GetGroupStatusText(group);
                m_StatusLabel.tooltip = GetGroupStatusMessage(group);
                m_StatusLabel.style.color = GetGroupStatusColor(group);
            }

            private void BindEntryRow(UObjectFinderDisplayRow row)
            {
                UObjectReferenceEntry entry = row.Entry;

                m_IndentElement.style.width = row.Kind == DisplayRowKind.ListItem ? 18f : 0f;
                m_ExpandButton.style.display = DisplayStyle.None;
                m_LocateButton.style.display = DisplayStyle.Flex;

                m_PathLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                m_PathLabel.text = GetPathDisplay(entry);
                m_PathLabel.tooltip = GetPathDisplay(entry);
                m_NameLabel.text = entry.GameObjectName;
                m_NameLabel.tooltip = entry.HierarchyPath;
                m_SceneLabel.text = entry.SceneName;
                m_SceneLabel.tooltip = entry.SceneName;
                m_KeyModeLabel.text = entry.UseCustomKey ? "自定义Key" : "默认Name";
                m_KeyModeLabel.tooltip = GetKeyModeDisplay(entry);
                m_StatusLabel.text = GetDisplayStatus(entry);
                m_StatusLabel.tooltip = entry.StatusMessage;
                m_StatusLabel.style.color = GetStatusColor(entry);
            }

            private static Label CreateFixedLabel(float width)
            {
                var label = new Label();
                label.style.width = width;
                label.style.maxWidth = width;
                label.style.minWidth = width;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                return label;
            }

            private static Label CreateFlexibleLabel()
            {
                var label = new Label();
                label.style.flexGrow = 1f;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
                return label;
            }
        }
    }
}
