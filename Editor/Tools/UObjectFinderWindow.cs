using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const float LeftPaneWidth = 780f;
        private const float PathColumnWidth = 280f;
        private const float NameColumnWidth = 150f;
        private const float SceneColumnWidth = 120f;
        private const float ModeColumnWidth = 110f;

        private static readonly string[] SearchSourceRoots =
        {
            "Assets/Scripts",
            "Packages/com.xdedzl.xframework"
        };

        private static readonly Regex LookupRegex = new(
            @"UObjectFinder\s*\.\s*(?<method>FindList|Find)\s*(?:<\s*(?<type>[^>\r\n]+?)\s*>)?\s*\(\s*""(?<path>(?:[^""\\]|\\.)*)""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly List<UObjectReferenceEntry> m_AllEntries = new();
        private readonly List<UObjectReferenceEntry> m_FilteredEntries = new();
        private readonly List<UObjectFinderMissingTargetIssue> m_AllMissingTargetIssues = new();
        private readonly List<UObjectFinderMissingTargetIssue> m_FilteredMissingTargetIssues = new();
        private readonly List<UObjectFinderLookupUsage> m_LookupUsages = new();
        private readonly List<UObjectFinderDisplayRow> m_DisplayRows = new();
        private readonly HashSet<string> m_ExpandedListKeys = new();

        private bool m_LookupUsagesDirty = true;

        private TextField m_SearchField;
        private Toggle m_OnlyIssuesToggle;
        private DropdownField m_IssueFilterField;
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
        private Label m_DetailModeLabel;
        private Label m_DetailPathLabel;
        private Label m_DetailExpectedComponentLabel;
        private Label m_DetailSourceLabel;
        private Label m_DetailMessageLabel;
        private Button m_DetailLocateButton;
        private Button m_DetailCopyPathButton;
        private Button m_DetailCopySecondaryButton;

        private UObjectFinderDisplayRow m_SelectedRow;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<UObjectFinderWindow>();
            window.titleContent = new GUIContent("UObject Finder");
            window.minSize = new Vector2(980f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += OnTrackedEditorStateChanged;
            EditorApplication.projectChanged += OnProjectChanged;
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
            EditorApplication.projectChanged -= OnProjectChanged;
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

            var splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            root.Add(splitView);

            splitView.Add(BuildListPane());
            splitView.Add(BuildDetailPane());
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 2f;

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1f;
            m_SearchField.style.minWidth = 180f;
            m_SearchField.tooltip = "搜索路径、对象名、场景名、层级路径、组件需求或代码来源";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_SearchField);

            m_OnlyIssuesToggle = new Toggle("仅问题项");
            m_OnlyIssuesToggle.style.marginLeft = 8f;
            m_OnlyIssuesToggle.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_OnlyIssuesToggle);

            m_IssueFilterField = new DropdownField("问题筛选", new List<string>
            {
                "全部",
                "Missing Target",
                "Missing Component",
                "Duplicate",
                "Invalid Path",
                "Inactive"
            }, 0);
            m_IssueFilterField.style.width = 170f;
            m_IssueFilterField.style.marginLeft = 8f;
            m_IssueFilterField.tooltip = "按问题类型过滤";
            m_IssueFilterField.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_IssueFilterField);

            m_KeyFilterField = new DropdownField("Key筛选", new List<string>
            {
                "全部",
                "仅自定义Key",
                "仅默认Name"
            }, 0);
            m_KeyFilterField.style.width = 170f;
            m_KeyFilterField.style.marginLeft = 8f;
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
            m_ModeFilterField.style.marginLeft = 8f;
            m_ModeFilterField.tooltip = "按 UObjectFinder 注册模式过滤";
            m_ModeFilterField.RegisterValueChangedCallback(_ => RefreshFilteredEntries());
            toolbar.Add(m_ModeFilterField);

            var refreshButton = new Button(RefreshEntries)
            {
                text = "刷新"
            };
            refreshButton.style.width = 64f;
            refreshButton.style.marginLeft = 8f;
            toolbar.Add(refreshButton);

            return toolbar;
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
                itemsSource = m_DisplayRows,
                fixedItemHeight = 24,
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

            header.Add(CreateHeaderLabel("Path", PathColumnWidth));
            header.Add(CreateHeaderLabel("对象", NameColumnWidth));
            header.Add(CreateHeaderLabel("场景", SceneColumnWidth));
            header.Add(CreateHeaderLabel("模式", ModeColumnWidth));

            Label statusLabel = CreateHeaderLabel("状态", 90f);
            statusLabel.style.flexGrow = 1f;
            header.Add(statusLabel);

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

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;
            pane.Add(scrollView);

            m_DetailTitleLabel = new Label("未选择条目");
            m_DetailTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_DetailTitleLabel.style.fontSize = 14;
            m_DetailTitleLabel.style.marginBottom = 8;
            scrollView.Add(m_DetailTitleLabel);

            m_DetailStatusLabel = new Label("从左侧选择一个条目查看详情。");
            m_DetailStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            m_DetailStatusLabel.style.marginBottom = 10f;
            scrollView.Add(m_DetailStatusLabel);

            m_DetailObjectField = new ObjectField("对象")
            {
                objectType = typeof(GameObject)
            };
            m_DetailObjectField.SetEnabled(false);
            scrollView.Add(m_DetailObjectField);

            m_DetailSceneLabel = AddDetailRow(scrollView, "场景");
            m_DetailHierarchyLabel = AddDetailRow(scrollView, "层级路径", true);
            m_DetailActiveLabel = AddDetailRow(scrollView, "激活状态");
            m_DetailModeLabel = AddDetailRow(scrollView, "查询/注册模式");
            m_DetailPathLabel = AddDetailRow(scrollView, "解析路径");
            m_DetailExpectedComponentLabel = AddDetailRow(scrollView, "组件需求", true);
            m_DetailSourceLabel = AddDetailRow(scrollView, "代码来源", true);
            m_DetailMessageLabel = AddDetailRow(scrollView, "说明", true);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 12f;
            scrollView.Add(buttonRow);

            m_DetailLocateButton = new Button(LocateSelectedItem)
            {
                text = "定位"
            };
            m_DetailLocateButton.style.width = 88f;
            buttonRow.Add(m_DetailLocateButton);

            m_DetailCopyPathButton = new Button(() => CopyToClipboard(GetSelectedPathForCopy()))
            {
                text = "复制路径"
            };
            m_DetailCopyPathButton.style.width = 90f;
            m_DetailCopyPathButton.style.marginLeft = 8f;
            buttonRow.Add(m_DetailCopyPathButton);

            m_DetailCopySecondaryButton = new Button(() => CopyToClipboard(GetSelectedSecondaryCopyValue()))
            {
                text = "复制来源"
            };
            m_DetailCopySecondaryButton.style.width = 110f;
            m_DetailCopySecondaryButton.style.marginLeft = 8f;
            buttonRow.Add(m_DetailCopySecondaryButton);

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
            container.style.marginTop = 8f;
            parent.Add(container);

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 2f;
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
            return new UObjectFinderRow(ToggleListGroup, LocateDisplayRow);
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
            string selectedIssueId = GetSelectedMissingTargetIssue()?.Id;

            m_AllEntries.Clear();
            m_AllMissingTargetIssues.Clear();

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

            EnsureLookupUsages();
            AnalyzeLookupDiagnostics();

            m_AllEntries.Sort(CompareEntriesForDisplay);
            m_AllMissingTargetIssues.Sort(CompareMissingTargetIssues);
            RefreshFilteredEntries(selectedReferenceId, selectedGroupKey, selectedIssueId);
        }

        private void RefreshFilteredEntries()
        {
            int selectedReferenceId = GetSelectedEntry()?.Reference != null ? GetSelectedEntry().Reference.GetInstanceID() : 0;
            string selectedGroupKey = GetSelectedGroup()?.Key;
            string selectedIssueId = GetSelectedMissingTargetIssue()?.Id;
            RefreshFilteredEntries(selectedReferenceId, selectedGroupKey, selectedIssueId);
        }

        private void RefreshFilteredEntries(int selectedReferenceId, string selectedGroupKey, string selectedIssueId)
        {
            m_FilteredEntries.Clear();
            m_FilteredMissingTargetIssues.Clear();

            string search = m_SearchField?.value?.Trim();
            bool onlyIssues = m_OnlyIssuesToggle?.value ?? false;
            string issueFilter = m_IssueFilterField?.value ?? "全部";
            string keyFilter = m_KeyFilterField?.value ?? "全部";
            string modeFilter = m_ModeFilterField?.value ?? "全部";

            foreach (UObjectReferenceEntry entry in m_AllEntries)
            {
                if (onlyIssues && !entry.HasIssue)
                {
                    continue;
                }

                if (!MatchesIssueFilter(entry, issueFilter)
                    || !MatchesKeyFilter(entry, keyFilter)
                    || !MatchesModeFilter(entry, modeFilter)
                    || !MatchesSearch(entry, search))
                {
                    continue;
                }

                m_FilteredEntries.Add(entry);
            }

            foreach (UObjectFinderMissingTargetIssue issue in m_AllMissingTargetIssues)
            {
                if (onlyIssues && !issue.HasIssue)
                {
                    continue;
                }

                if (!MatchesIssueFilter(issue, issueFilter)
                    || !MatchesKeyFilter(issue, keyFilter)
                    || !MatchesModeFilter(issue, modeFilter)
                    || !MatchesSearch(issue, search))
                {
                    continue;
                }

                m_FilteredMissingTargetIssues.Add(issue);
            }

            BuildDisplayRows();
            RefreshView(selectedReferenceId, selectedGroupKey, selectedIssueId);
        }

        private void EnsureLookupUsages()
        {
            if (!m_LookupUsagesDirty)
            {
                return;
            }

            m_LookupUsagesDirty = false;
            m_LookupUsages.Clear();

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            foreach (string relativeRoot in SearchSourceRoots)
            {
                string absoluteRoot = Path.Combine(projectRoot, relativeRoot);
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (string filePath in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string content;
                    try
                    {
                        content = File.ReadAllText(filePath);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(content))
                    {
                        continue;
                    }

                    string assetPath = ToAssetPath(projectRoot, filePath);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    MatchCollection matches = LookupRegex.Matches(content);
                    foreach (Match match in matches)
                    {
                        if (!match.Success)
                        {
                            continue;
                        }

                        string path = Regex.Unescape(match.Groups["path"].Value);
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            continue;
                        }

                        string componentType = NormalizeComponentTypeName(match.Groups["type"].Value);
                        int line = GetLineNumber(content, match.Index);
                        LookupMode mode = match.Groups["method"].Value == "FindList" ? LookupMode.List : LookupMode.Single;

                        m_LookupUsages.Add(new UObjectFinderLookupUsage
                        {
                            Path = path,
                            Mode = mode,
                            ComponentTypeName = componentType,
                            AssetPath = assetPath,
                            FileName = Path.GetFileName(filePath),
                            Line = line
                        });
                    }
                }
            }
        }

        private void AnalyzeLookupDiagnostics()
        {
            foreach (UObjectReferenceEntry entry in m_AllEntries)
            {
                entry.ListGroupCount = 1;
                entry.ExpectedComponentNames.Clear();
                entry.MissingExpectedComponentNames.Clear();
                entry.MatchedUsages.Clear();
            }

            MarkSceneConflicts(m_AllEntries);

            var singleEntriesByPath = m_AllEntries
                .Where(entry => entry.Mode == UObjectReference.RegistrationMode.Single)
                .GroupBy(entry => entry.ResolvedPath)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            var listEntriesByPath = m_AllEntries
                .Where(entry => entry.Mode == UObjectReference.RegistrationMode.List)
                .GroupBy(entry => entry.ResolvedPath)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            var missingIssueMap = new Dictionary<string, UObjectFinderMissingTargetIssue>(StringComparer.Ordinal);

            foreach (UObjectFinderLookupUsage usage in m_LookupUsages)
            {
                List<UObjectReferenceEntry> matchingEntries = usage.Mode == LookupMode.List
                    ? GetMatchingEntries(listEntriesByPath, usage.Path)
                    : GetMatchingEntries(singleEntriesByPath, usage.Path);

                if (matchingEntries.Count == 0)
                {
                    string issueId = BuildMissingTargetIssueId(usage.Mode, usage.Path);
                    if (!missingIssueMap.TryGetValue(issueId, out UObjectFinderMissingTargetIssue issue))
                    {
                        issue = new UObjectFinderMissingTargetIssue
                        {
                            Id = issueId,
                            Path = usage.Path,
                            Mode = usage.Mode,
                            Issues = IssueFlags.MissingTarget
                        };
                        missingIssueMap.Add(issueId, issue);
                    }

                    issue.Usages.Add(usage);
                    AddUnique(issue.ExpectedComponentNames, usage.ComponentTypeName);
                    continue;
                }

                foreach (UObjectReferenceEntry entry in matchingEntries)
                {
                    entry.MatchedUsages.Add(usage);
                    AddUnique(entry.ExpectedComponentNames, usage.ComponentTypeName);

                    if (!HasExpectedComponent(entry.GameObject, usage.ComponentTypeName))
                    {
                        entry.Issues |= IssueFlags.MissingExpectedComponent;
                        AddUnique(entry.MissingExpectedComponentNames, usage.ComponentTypeName);
                    }
                }
            }

            foreach (UObjectFinderMissingTargetIssue issue in missingIssueMap.Values)
            {
                issue.Usages.Sort(CompareLookupUsages);
                issue.StatusMessage = BuildMissingTargetMessage(issue);
                m_AllMissingTargetIssues.Add(issue);
            }

            foreach (UObjectReferenceEntry entry in m_AllEntries)
            {
                entry.StatusMessage = BuildStatusMessage(entry);
            }
        }

        private static List<UObjectReferenceEntry> GetMatchingEntries(
            Dictionary<string, List<UObjectReferenceEntry>> map,
            string path)
        {
            if (string.IsNullOrEmpty(path) || !map.TryGetValue(path, out List<UObjectReferenceEntry> entries))
            {
                return s_EmptyEntries;
            }

            return entries;
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
                         .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                UObjectFinderListGroup listGroup = CreateListGroup(group);
                topLevelItems.Add(TopLevelDisplayItem.CreateGroup(listGroup));
            }

            foreach (UObjectFinderMissingTargetIssue issue in m_FilteredMissingTargetIssues)
            {
                topLevelItems.Add(TopLevelDisplayItem.CreateMissingTarget(issue));
            }

            topLevelItems.Sort(CompareTopLevelItems);

            foreach (TopLevelDisplayItem item in topLevelItems)
            {
                if (item.SingleEntry != null)
                {
                    m_DisplayRows.Add(UObjectFinderDisplayRow.CreateSingle(item.SingleEntry));
                    continue;
                }

                if (item.Group != null)
                {
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

                    continue;
                }

                if (item.MissingTargetIssue != null)
                {
                    m_DisplayRows.Add(UObjectFinderDisplayRow.CreateMissingTarget(item.MissingTargetIssue));
                }
            }
        }

        private void RefreshView(int selectedReferenceId = 0, string selectedGroupKey = null, string selectedIssueId = null)
        {
            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_DisplayRows;
                m_ListView.Rebuild();
                RestoreSelectionInView(selectedReferenceId, selectedGroupKey, selectedIssueId);
            }

            UpdateSummary();
            RefreshDetailPane();
        }

        private void RestoreSelectionInView(int selectedReferenceId, string selectedGroupKey, string selectedIssueId)
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
                index = m_DisplayRows.FindIndex(row => row.Kind == DisplayRowKind.ListGroup && row.Group?.Key == selectedGroupKey);
            }

            if (index < 0 && !string.IsNullOrEmpty(selectedIssueId))
            {
                index = m_DisplayRows.FindIndex(row => row.Kind == DisplayRowKind.MissingTarget && row.MissingTargetIssue?.Id == selectedIssueId);
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

            int displayedCount = m_FilteredEntries.Count + m_FilteredMissingTargetIssues.Count;
            int problemCount = m_AllEntries.Count(entry => entry.HasIssue) + m_AllMissingTargetIssues.Count;
            int missingTargetCount = m_AllMissingTargetIssues.Count;
            int missingComponentCount = m_AllEntries.Count(entry => entry.Issues.HasFlag(IssueFlags.MissingExpectedComponent));
            int duplicateCount = m_AllEntries.Count(entry => entry.Issues.HasFlag(IssueFlags.Duplicate));
            int invalidPathCount = m_AllEntries.Count(entry => entry.Issues.HasFlag(IssueFlags.InvalidPath));
            int inactiveCount = m_AllEntries.Count(entry => entry.Issues.HasFlag(IssueFlags.Inactive));
            int listGroupCount = m_FilteredEntries.Where(entry => entry.Mode == UObjectReference.RegistrationMode.List)
                .Select(entry => entry.ResolvedPath)
                .Distinct()
                .Count();

            m_SummaryLabel.text =
                $"已显示 {displayedCount} 项 | 问题: {problemCount} | Missing Target: {missingTargetCount} | Missing Component: {missingComponentCount} | Duplicate: {duplicateCount} | Invalid Path: {invalidPathCount} | Inactive: {inactiveCount} | List组: {listGroupCount}";
        }

        private void RefreshDetailPane()
        {
            if (m_DetailTitleLabel == null)
            {
                return;
            }

            UObjectReferenceEntry selectedEntry = GetSelectedEntry();
            UObjectFinderListGroup selectedGroup = GetSelectedGroup();
            UObjectFinderMissingTargetIssue selectedIssue = GetSelectedMissingTargetIssue();

            if (selectedEntry != null)
            {
                m_DetailTitleLabel.text = selectedEntry.GameObjectName;
                m_DetailStatusLabel.text = GetDisplayStatus(selectedEntry);
                m_DetailStatusLabel.style.color = GetStatusColor(selectedEntry.Issues);

                m_DetailObjectField.value = selectedEntry.GameObject;
                m_DetailSceneLabel.text = selectedEntry.SceneName;
                m_DetailHierarchyLabel.text = selectedEntry.HierarchyPath;
                m_DetailHierarchyLabel.tooltip = selectedEntry.HierarchyPath;
                m_DetailActiveLabel.text = selectedEntry.Issues.HasFlag(IssueFlags.Inactive)
                    ? "Inactive In Hierarchy"
                    : "Active In Hierarchy";
                m_DetailModeLabel.text = GetEntryModeDisplay(selectedEntry);
                m_DetailPathLabel.text = GetPathDisplay(selectedEntry);
                m_DetailPathLabel.tooltip = GetPathDisplay(selectedEntry);
                m_DetailExpectedComponentLabel.text = GetExpectedComponentDisplay(selectedEntry.ExpectedComponentNames, selectedEntry.MissingExpectedComponentNames);
                m_DetailSourceLabel.text = BuildUsageDisplayText(selectedEntry.MatchedUsages);
                m_DetailSourceLabel.tooltip = BuildUsageTooltip(selectedEntry.MatchedUsages);
                m_DetailMessageLabel.text = selectedEntry.StatusMessage;

                m_DetailLocateButton.text = "定位对象";
                m_DetailLocateButton.SetEnabled(selectedEntry.GameObject != null);
                m_DetailCopyPathButton.SetEnabled(true);
                m_DetailCopySecondaryButton.text = "复制来源";
                m_DetailCopySecondaryButton.SetEnabled(selectedEntry.MatchedUsages.Count > 0 || !string.IsNullOrEmpty(selectedEntry.HierarchyPath));
                return;
            }

            if (selectedGroup != null)
            {
                m_DetailTitleLabel.text = GetPathDisplay(selectedGroup.Key);
                m_DetailStatusLabel.text = GetGroupStatusText(selectedGroup);
                m_DetailStatusLabel.style.color = GetStatusColor(selectedGroup.Issues);

                m_DetailObjectField.value = null;
                m_DetailSceneLabel.text = selectedGroup.SceneSummary;
                m_DetailHierarchyLabel.text = $"共 {selectedGroup.Items.Count} 项";
                m_DetailHierarchyLabel.tooltip = string.Join("\n", selectedGroup.Items.Select(item => item.HierarchyPath));
                m_DetailActiveLabel.text = GetGroupActiveDisplay(selectedGroup);
                m_DetailModeLabel.text = "List / 自定义Key";
                m_DetailPathLabel.text = GetPathDisplay(selectedGroup.Key);
                m_DetailPathLabel.tooltip = GetPathDisplay(selectedGroup.Key);
                m_DetailExpectedComponentLabel.text = GetExpectedComponentDisplay(selectedGroup.ExpectedComponentNames, selectedGroup.MissingExpectedComponentNames);
                m_DetailSourceLabel.text = BuildUsageDisplayText(selectedGroup.MatchedUsages);
                m_DetailSourceLabel.tooltip = BuildUsageTooltip(selectedGroup.MatchedUsages);
                m_DetailMessageLabel.text = GetGroupStatusMessage(selectedGroup);

                m_DetailLocateButton.text = "定位";
                m_DetailLocateButton.SetEnabled(false);
                m_DetailCopyPathButton.SetEnabled(true);
                m_DetailCopySecondaryButton.text = "复制来源";
                m_DetailCopySecondaryButton.SetEnabled(selectedGroup.MatchedUsages.Count > 0);
                return;
            }

            if (selectedIssue != null)
            {
                m_DetailTitleLabel.text = $"<缺失目标> {GetPathDisplay(selectedIssue.Path)}";
                m_DetailStatusLabel.text = GetDisplayStatus(selectedIssue);
                m_DetailStatusLabel.style.color = GetStatusColor(selectedIssue.Issues);

                m_DetailObjectField.value = null;
                m_DetailSceneLabel.text = "代码调用";
                m_DetailHierarchyLabel.text = $"{selectedIssue.Usages.Count} 处调用";
                m_DetailHierarchyLabel.tooltip = BuildUsageTooltip(selectedIssue.Usages);
                m_DetailActiveLabel.text = "-";
                m_DetailModeLabel.text = GetLookupModeDisplay(selectedIssue.Mode);
                m_DetailPathLabel.text = GetPathDisplay(selectedIssue.Path);
                m_DetailPathLabel.tooltip = GetPathDisplay(selectedIssue.Path);
                m_DetailExpectedComponentLabel.text = selectedIssue.ExpectedComponentNames.Count == 0
                    ? "仅要求 GameObject 存在。"
                    : string.Join(", ", selectedIssue.ExpectedComponentNames);
                m_DetailSourceLabel.text = BuildUsageDisplayText(selectedIssue.Usages);
                m_DetailSourceLabel.tooltip = BuildUsageTooltip(selectedIssue.Usages);
                m_DetailMessageLabel.text = selectedIssue.StatusMessage;

                m_DetailLocateButton.text = "定位代码";
                m_DetailLocateButton.SetEnabled(selectedIssue.Usages.Count > 0);
                m_DetailCopyPathButton.SetEnabled(true);
                m_DetailCopySecondaryButton.text = "复制来源";
                m_DetailCopySecondaryButton.SetEnabled(selectedIssue.Usages.Count > 0);
                return;
            }

            m_DetailTitleLabel.text = "未选择条目";
            m_DetailStatusLabel.text = "从左侧选择一个条目查看详情。";
            m_DetailStatusLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            m_DetailObjectField.value = null;
            m_DetailSceneLabel.text = "-";
            m_DetailHierarchyLabel.text = "-";
            m_DetailHierarchyLabel.tooltip = string.Empty;
            m_DetailActiveLabel.text = "-";
            m_DetailModeLabel.text = "-";
            m_DetailPathLabel.text = "-";
            m_DetailPathLabel.tooltip = string.Empty;
            m_DetailExpectedComponentLabel.text = "-";
            m_DetailSourceLabel.text = "-";
            m_DetailSourceLabel.tooltip = string.Empty;
            m_DetailMessageLabel.text = "-";
            m_DetailLocateButton.text = "定位";
            m_DetailLocateButton.SetEnabled(false);
            m_DetailCopyPathButton.SetEnabled(false);
            m_DetailCopySecondaryButton.text = "复制来源";
            m_DetailCopySecondaryButton.SetEnabled(false);
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
                   || ContainsIgnoreCase(entry.HierarchyPath, search)
                   || ContainsIgnoreCase(GetEntryModeDisplay(entry), search)
                   || entry.ExpectedComponentNames.Any(name => ContainsIgnoreCase(name, search))
                   || entry.MatchedUsages.Any(usage => ContainsIgnoreCase(usage.DisplayText, search));
        }

        private static bool MatchesSearch(UObjectFinderMissingTargetIssue issue, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            return ContainsIgnoreCase(issue.Path, search)
                   || ContainsIgnoreCase(GetLookupModeDisplay(issue.Mode), search)
                   || issue.ExpectedComponentNames.Any(name => ContainsIgnoreCase(name, search))
                   || issue.Usages.Any(usage => ContainsIgnoreCase(usage.DisplayText, search));
        }

        private static bool MatchesIssueFilter(UObjectReferenceEntry entry, string issueFilter)
        {
            return issueFilter switch
            {
                "Missing Target" => false,
                "Missing Component" => entry.Issues.HasFlag(IssueFlags.MissingExpectedComponent),
                "Duplicate" => entry.Issues.HasFlag(IssueFlags.Duplicate),
                "Invalid Path" => entry.Issues.HasFlag(IssueFlags.InvalidPath),
                "Inactive" => entry.Issues.HasFlag(IssueFlags.Inactive),
                _ => true,
            };
        }

        private static bool MatchesIssueFilter(UObjectFinderMissingTargetIssue issue, string issueFilter)
        {
            return issueFilter switch
            {
                "Missing Target" => true,
                "Missing Component" => false,
                "Duplicate" => false,
                "Invalid Path" => false,
                "Inactive" => false,
                _ => true,
            };
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

        private static bool MatchesKeyFilter(UObjectFinderMissingTargetIssue issue, string keyFilter)
        {
            return keyFilter switch
            {
                "仅自定义Key" => issue.Mode == LookupMode.List,
                "仅默认Name" => false,
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

        private static bool MatchesModeFilter(UObjectFinderMissingTargetIssue issue, string modeFilter)
        {
            return modeFilter switch
            {
                "仅Single" => issue.Mode == LookupMode.Single,
                "仅List" => issue.Mode == LookupMode.List,
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

            string customKey = keyProperty != null ? keyProperty.stringValue : string.Empty;
            UObjectReference.RegistrationMode mode = modeProperty != null
                ? (UObjectReference.RegistrationMode)modeProperty.enumValueIndex
                : UObjectReference.RegistrationMode.Single;

            string resolvedPath = reference.Path;
            IssueFlags issues = IssueFlags.None;
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                issues |= IssueFlags.InvalidPath;
            }

            if (!reference.gameObject.activeInHierarchy)
            {
                issues |= IssueFlags.Inactive;
            }

            return new UObjectReferenceEntry
            {
                Reference = reference,
                GameObject = reference.gameObject,
                GameObjectName = reference.gameObject.name,
                UseCustomKey = reference.UseKey,
                CustomKey = customKey,
                Mode = mode,
                ResolvedPath = resolvedPath,
                HierarchyPath = BuildHierarchyPath(reference.transform),
                SceneName = GetSceneDisplayName(reference.gameObject.scene),
                Issues = issues
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

        private static void MarkSceneConflicts(List<UObjectReferenceEntry> entries)
        {
            foreach (UObjectReferenceEntry entry in entries)
            {
                entry.ListGroupCount = 1;
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
                    entry.Issues |= IssueFlags.Duplicate;
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
        }

        private static string BuildStatusMessage(UObjectReferenceEntry entry)
        {
            var parts = new List<string>();

            if (entry.Issues.HasFlag(IssueFlags.Duplicate))
            {
                parts.Add("Duplicate: 当前已加载场景中存在相同解析路径的多个 Single 注册，运行时会发生覆盖。");
            }

            if (entry.Issues.HasFlag(IssueFlags.InvalidPath))
            {
                parts.Add(entry.UseCustomKey
                    ? "Invalid Path: 当前条目依赖自定义 Key，但 Key 为空或仅包含空白字符，运行时不会注册。"
                    : "Invalid Path: 当前条目依赖对象名作为路径，但对象名为空或仅包含空白字符，运行时不会注册。");
            }

            if (entry.Issues.HasFlag(IssueFlags.MissingExpectedComponent))
            {
                parts.Add($"Missing Component: 代码要求该路径提供 {string.Join(", ", entry.MissingExpectedComponentNames)}，但当前对象缺少对应组件。");
            }

            if (entry.Issues.HasFlag(IssueFlags.Inactive))
            {
                parts.Add("Inactive: 该对象当前在 Hierarchy 中未激活，Awake 触发前不会注册到 UObjectFinder。");
            }

            if (entry.Mode == UObjectReference.RegistrationMode.List)
            {
                parts.Add($"List: 当前列表 key 共 {entry.ListGroupCount} 项。");
            }

            if (entry.MatchedUsages.Count == 0)
            {
                parts.Add("当前未扫描到源码中的字面量 UObjectFinder 调用引用该路径。");
            }

            return parts.Count == 0 ? "状态正常。" : string.Join(" ", parts);
        }

        private static string BuildMissingTargetMessage(UObjectFinderMissingTargetIssue issue)
        {
            string componentText = issue.ExpectedComponentNames.Count == 0
                ? "仅要求目标 GameObject 存在。"
                : $"并期望提供组件 {string.Join(", ", issue.ExpectedComponentNames)}。";

            return $"{GetLookupModeDisplay(issue.Mode)} 在当前已加载场景中未找到解析路径为 '{issue.Path}' 的 UObjectReference，运行时会返回空结果 {componentText}";
        }

        private static int GetSortPriority(IssueFlags issues)
        {
            if (issues.HasFlag(IssueFlags.MissingTarget))
            {
                return 0;
            }

            if (issues.HasFlag(IssueFlags.Duplicate))
            {
                return 1;
            }

            if (issues.HasFlag(IssueFlags.InvalidPath))
            {
                return 2;
            }

            if (issues.HasFlag(IssueFlags.MissingExpectedComponent))
            {
                return 3;
            }

            if (issues.HasFlag(IssueFlags.Inactive))
            {
                return 4;
            }

            return 5;
        }

        private static int CompareEntriesForDisplay(UObjectReferenceEntry left, UObjectReferenceEntry right)
        {
            int severityCompare = GetSortPriority(left.Issues).CompareTo(GetSortPriority(right.Issues));
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

        private static int CompareMissingTargetIssues(UObjectFinderMissingTargetIssue left, UObjectFinderMissingTargetIssue right)
        {
            int severityCompare = GetSortPriority(left.Issues).CompareTo(GetSortPriority(right.Issues));
            if (severityCompare != 0)
            {
                return severityCompare;
            }

            int pathCompare = string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
            if (pathCompare != 0)
            {
                return pathCompare;
            }

            return CompareLookupUsages(left.Usages.FirstOrDefault(), right.Usages.FirstOrDefault());
        }

        private static int CompareEntriesForGroupChildren(UObjectReferenceEntry left, UObjectReferenceEntry right)
        {
            int severityCompare = GetSortPriority(left.Issues).CompareTo(GetSortPriority(right.Issues));
            if (severityCompare != 0)
            {
                return severityCompare;
            }

            int sceneCompare = string.Compare(left.SceneName, right.SceneName, StringComparison.OrdinalIgnoreCase);
            if (sceneCompare != 0)
            {
                return sceneCompare;
            }

            return string.Compare(left.HierarchyPath, right.HierarchyPath, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareTopLevelItems(TopLevelDisplayItem left, TopLevelDisplayItem right)
        {
            int severityCompare = left.SortPriority.CompareTo(right.SortPriority);
            if (severityCompare != 0)
            {
                return severityCompare;
            }

            int pathCompare = string.Compare(left.SortPath, right.SortPath, StringComparison.OrdinalIgnoreCase);
            if (pathCompare != 0)
            {
                return pathCompare;
            }

            int sceneCompare = string.Compare(left.SortScene, right.SortScene, StringComparison.OrdinalIgnoreCase);
            if (sceneCompare != 0)
            {
                return sceneCompare;
            }

            return string.Compare(left.SortHierarchy, right.SortHierarchy, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareLookupUsages(UObjectFinderLookupUsage left, UObjectFinderLookupUsage right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int fileCompare = string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase);
            if (fileCompare != 0)
            {
                return fileCompare;
            }

            return left.Line.CompareTo(right.Line);
        }

        private static UObjectFinderListGroup CreateListGroup(IGrouping<string, UObjectReferenceEntry> group)
        {
            List<UObjectReferenceEntry> items = group
                .OrderBy(entry => entry, Comparer<UObjectReferenceEntry>.Create(CompareEntriesForGroupChildren))
                .ToList();

            List<string> sceneNames = items.Select(entry => entry.SceneName).Distinct().ToList();
            var matchedUsages = items.SelectMany(entry => entry.MatchedUsages)
                .Distinct(UObjectFinderLookupUsageComparer.Instance)
                .OrderBy(usage => usage, Comparer<UObjectFinderLookupUsage>.Create(CompareLookupUsages))
                .ToList();

            IssueFlags issues = IssueFlags.None;
            if (items.Any(entry => entry.Issues.HasFlag(IssueFlags.InvalidPath)))
            {
                issues |= IssueFlags.InvalidPath;
            }

            if (items.Any(entry => entry.Issues.HasFlag(IssueFlags.MissingExpectedComponent)))
            {
                issues |= IssueFlags.MissingExpectedComponent;
            }

            if (items.Any(entry => entry.Issues.HasFlag(IssueFlags.Inactive)))
            {
                issues |= IssueFlags.Inactive;
            }

            return new UObjectFinderListGroup
            {
                Key = group.Key,
                Items = items,
                SceneSummary = sceneNames.Count == 1 ? sceneNames[0] : $"多场景 ({sceneNames.Count})",
                Issues = issues,
                ExpectedComponentNames = items.SelectMany(entry => entry.ExpectedComponentNames).Distinct().OrderBy(name => name).ToList(),
                MissingExpectedComponentNames = items.SelectMany(entry => entry.MissingExpectedComponentNames).Distinct().OrderBy(name => name).ToList(),
                MatchedUsages = matchedUsages
            };
        }

        private static string GetDisplayStatus(UObjectReferenceEntry entry)
        {
            var parts = new List<string>
            {
                entry.Mode == UObjectReference.RegistrationMode.List ? $"List x{entry.ListGroupCount}" : "Single"
            };

            if (entry.Issues.HasFlag(IssueFlags.Duplicate))
            {
                parts.Add("Duplicate");
            }

            if (entry.Issues.HasFlag(IssueFlags.InvalidPath))
            {
                parts.Add("Invalid Path");
            }

            if (entry.Issues.HasFlag(IssueFlags.MissingExpectedComponent))
            {
                parts.Add("Missing Component");
            }

            if (entry.Issues.HasFlag(IssueFlags.Inactive))
            {
                parts.Add("Inactive");
            }

            return string.Join(" | ", parts);
        }

        private static string GetDisplayStatus(UObjectFinderMissingTargetIssue issue)
        {
            return $"Missing Target | {GetLookupModeDisplay(issue.Mode)} | {issue.Usages.Count} 处调用";
        }

        private static string GetGroupStatusText(UObjectFinderListGroup group)
        {
            var parts = new List<string> { $"List Group x{group.Items.Count}" };

            if (group.Issues.HasFlag(IssueFlags.InvalidPath))
            {
                parts.Add("Invalid Path");
            }

            if (group.Issues.HasFlag(IssueFlags.MissingExpectedComponent))
            {
                parts.Add("Missing Component");
            }

            if (group.Issues.HasFlag(IssueFlags.Inactive))
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
            return string.IsNullOrWhiteSpace(path) ? "<空路径>" : path;
        }

        private static string GetEntryModeDisplay(UObjectReferenceEntry entry)
        {
            if (entry == null)
            {
                return "-";
            }

            string mode = entry.Mode == UObjectReference.RegistrationMode.List ? "List" : "Single";
            string keyMode = entry.UseCustomKey ? "自定义Key" : "默认Name";
            return $"{mode} / {keyMode}";
        }

        private static string GetLookupModeDisplay(LookupMode mode)
        {
            return mode == LookupMode.List ? "List 查询" : "Single 查询";
        }

        private static string GetGroupStatusMessage(UObjectFinderListGroup group)
        {
            var parts = new List<string> { $"List Group: 当前列表 key 共 {group.Items.Count} 项。" };

            if (group.Issues.HasFlag(IssueFlags.InvalidPath))
            {
                parts.Add("存在解析路径为空的条目，这些对象在运行时不会注册。");
            }

            if (group.Issues.HasFlag(IssueFlags.MissingExpectedComponent))
            {
                parts.Add($"存在缺少组件 {string.Join(", ", group.MissingExpectedComponentNames)} 的条目。");
            }

            if (group.Issues.HasFlag(IssueFlags.Inactive))
            {
                parts.Add("存在未激活条目，运行时注册结果可能少于当前列表。");
            }

            if (group.MatchedUsages.Count == 0)
            {
                parts.Add("当前未扫描到源码中的字面量 FindList 调用引用该路径。");
            }

            return string.Join(" ", parts);
        }

        private static string GetGroupActiveDisplay(UObjectFinderListGroup group)
        {
            int activeCount = group.Items.Count(item => !item.Issues.HasFlag(IssueFlags.Inactive));
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

        private static Color GetStatusColor(IssueFlags issues)
        {
            if (issues.HasFlag(IssueFlags.MissingTarget))
            {
                return new Color(1f, 0.4f, 0.35f);
            }

            if (issues.HasFlag(IssueFlags.Duplicate))
            {
                return new Color(1f, 0.55f, 0.3f);
            }

            if (issues.HasFlag(IssueFlags.InvalidPath))
            {
                return new Color(0.98f, 0.78f, 0.35f);
            }

            if (issues.HasFlag(IssueFlags.MissingExpectedComponent))
            {
                return new Color(0.95f, 0.85f, 0.45f);
            }

            if (issues.HasFlag(IssueFlags.Inactive))
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

        private static void LocateMissingTargetIssue(UObjectFinderMissingTargetIssue issue)
        {
            LocateUsage(issue?.Usages.FirstOrDefault());
        }

        private static void LocateUsage(UObjectFinderLookupUsage usage)
        {
            if (usage == null || string.IsNullOrEmpty(usage.AssetPath))
            {
                return;
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(usage.AssetPath);
            if (asset == null)
            {
                return;
            }

            AssetDatabase.OpenAsset(asset, usage.Line);
            EditorGUIUtility.PingObject(asset);
        }

        private void LocateSelectedItem()
        {
            LocateDisplayRow(m_SelectedRow);
        }

        private static void LocateDisplayRow(UObjectFinderDisplayRow row)
        {
            if (row == null)
            {
                return;
            }

            if (row.Entry != null)
            {
                LocateEntry(row.Entry);
                return;
            }

            if (row.MissingTargetIssue != null)
            {
                LocateMissingTargetIssue(row.MissingTargetIssue);
            }
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

        private void OnProjectChanged()
        {
            m_LookupUsagesDirty = true;
            RefreshEntries();
        }

        private UObjectReferenceEntry GetSelectedEntry()
        {
            return m_SelectedRow?.Entry;
        }

        private UObjectFinderListGroup GetSelectedGroup()
        {
            return m_SelectedRow?.Kind == DisplayRowKind.ListGroup ? m_SelectedRow.Group : null;
        }

        private UObjectFinderMissingTargetIssue GetSelectedMissingTargetIssue()
        {
            return m_SelectedRow?.MissingTargetIssue;
        }

        private string GetSelectedPathForCopy()
        {
            if (GetSelectedEntry() != null)
            {
                return GetSelectedEntry().ResolvedPath;
            }

            if (GetSelectedGroup() != null)
            {
                return GetSelectedGroup().Key;
            }

            return GetSelectedMissingTargetIssue()?.Path;
        }

        private string GetSelectedSecondaryCopyValue()
        {
            UObjectReferenceEntry entry = GetSelectedEntry();
            if (entry != null)
            {
                if (entry.MatchedUsages.Count > 0)
                {
                    return BuildUsageTooltip(entry.MatchedUsages);
                }

                return entry.HierarchyPath;
            }

            UObjectFinderListGroup group = GetSelectedGroup();
            if (group != null)
            {
                return BuildUsageTooltip(group.MatchedUsages);
            }

            return BuildUsageTooltip(GetSelectedMissingTargetIssue()?.Usages);
        }

        private static string GetExpectedComponentDisplay(IEnumerable<string> expectedComponents, IEnumerable<string> missingComponents)
        {
            List<string> expected = expectedComponents?.Where(name => !string.IsNullOrEmpty(name)).Distinct().OrderBy(name => name).ToList()
                ?? new List<string>();
            List<string> missing = missingComponents?.Where(name => !string.IsNullOrEmpty(name)).Distinct().OrderBy(name => name).ToList()
                ?? new List<string>();

            if (expected.Count == 0)
            {
                return "当前未扫描到泛型 Find<T>/FindList<T> 组件需求。";
            }

            if (missing.Count == 0)
            {
                return $"要求组件: {string.Join(", ", expected)}";
            }

            return $"要求组件: {string.Join(", ", expected)}\n缺失组件: {string.Join(", ", missing)}";
        }

        private static string BuildUsageDisplayText(IReadOnlyList<UObjectFinderLookupUsage> usages)
        {
            if (usages == null || usages.Count == 0)
            {
                return "未扫描到字面量 UObjectFinder 调用。";
            }

            const int maxLines = 6;
            var lines = usages.Take(maxLines).Select(usage => usage.DisplayText).ToList();
            if (usages.Count > maxLines)
            {
                lines.Add($"... 另有 {usages.Count - maxLines} 处调用");
            }

            return string.Join("\n", lines);
        }

        private static string BuildUsageTooltip(IReadOnlyList<UObjectFinderLookupUsage> usages)
        {
            if (usages == null || usages.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", usages.Select(usage => usage.DisplayText));
        }

        private static bool HasExpectedComponent(GameObject gameObject, string componentTypeName)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(componentTypeName))
            {
                return true;
            }

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                if (string.Equals(type.Name, componentTypeName, StringComparison.Ordinal)
                    || string.Equals(type.FullName, componentTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeComponentTypeName(string rawTypeName)
        {
            if (string.IsNullOrWhiteSpace(rawTypeName))
            {
                return string.Empty;
            }

            string typeName = rawTypeName.Trim();
            int commaIndex = typeName.IndexOf(',');
            if (commaIndex >= 0)
            {
                typeName = typeName.Substring(0, commaIndex);
            }

            return typeName.Trim();
        }

        private static int GetLineNumber(string content, int charIndex)
        {
            if (string.IsNullOrEmpty(content) || charIndex <= 0)
            {
                return 1;
            }

            int line = 1;
            for (int i = 0; i < charIndex && i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        private static string ToAssetPath(string projectRoot, string absolutePath)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(absolutePath))
            {
                return string.Empty;
            }

            string normalizedProjectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            string normalizedAbsolutePath = absolutePath.Replace('\\', '/');
            if (!normalizedAbsolutePath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string relativePath = normalizedAbsolutePath.Substring(normalizedProjectRoot.Length).TrimStart('/');
            return relativePath;
        }

        private static string BuildMissingTargetIssueId(LookupMode mode, string path)
        {
            return $"{mode}:{path}";
        }

        private static void AddUnique(List<string> target, string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(value) || target.Contains(value))
            {
                return;
            }

            target.Add(value);
            target.Sort(StringComparer.Ordinal);
        }

        [Flags]
        private enum IssueFlags
        {
            None = 0,
            Duplicate = 1 << 0,
            InvalidPath = 1 << 1,
            Inactive = 1 << 2,
            MissingExpectedComponent = 1 << 3,
            MissingTarget = 1 << 4
        }

        private enum LookupMode
        {
            Single,
            List
        }

        private enum DisplayRowKind
        {
            SingleEntry,
            ListGroup,
            ListItem,
            MissingTarget
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
            public IssueFlags Issues;
            public string StatusMessage;
            public int ListGroupCount;
            public List<string> ExpectedComponentNames = new();
            public List<string> MissingExpectedComponentNames = new();
            public List<UObjectFinderLookupUsage> MatchedUsages = new();

            public bool HasIssue => Issues != IssueFlags.None;
        }

        private sealed class UObjectFinderListGroup
        {
            public string Key;
            public List<UObjectReferenceEntry> Items;
            public string SceneSummary;
            public IssueFlags Issues;
            public List<string> ExpectedComponentNames;
            public List<string> MissingExpectedComponentNames;
            public List<UObjectFinderLookupUsage> MatchedUsages;
        }

        private sealed class UObjectFinderMissingTargetIssue
        {
            public string Id;
            public string Path;
            public LookupMode Mode;
            public IssueFlags Issues;
            public string StatusMessage;
            public List<string> ExpectedComponentNames = new();
            public List<UObjectFinderLookupUsage> Usages = new();

            public bool HasIssue => Issues != IssueFlags.None;
        }

        private sealed class UObjectFinderLookupUsage
        {
            public string Path;
            public LookupMode Mode;
            public string ComponentTypeName;
            public string AssetPath;
            public string FileName;
            public int Line;

            public string DisplayText
            {
                get
                {
                    string method = Mode == LookupMode.List ? "FindList" : "Find";
                    string typeSuffix = string.IsNullOrEmpty(ComponentTypeName) ? string.Empty : $"<{ComponentTypeName}>";
                    return $"{AssetPath}:{Line}  {method}{typeSuffix}(\"{Path}\")";
                }
            }
        }

        private sealed class UObjectFinderLookupUsageComparer : IEqualityComparer<UObjectFinderLookupUsage>
        {
            public static readonly UObjectFinderLookupUsageComparer Instance = new();

            public bool Equals(UObjectFinderLookupUsage x, UObjectFinderLookupUsage y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x == null || y == null)
                {
                    return false;
                }

                return x.Mode == y.Mode
                       && string.Equals(x.Path, y.Path, StringComparison.Ordinal)
                       && string.Equals(x.ComponentTypeName, y.ComponentTypeName, StringComparison.Ordinal)
                       && string.Equals(x.AssetPath, y.AssetPath, StringComparison.Ordinal)
                       && x.Line == y.Line;
            }

            public int GetHashCode(UObjectFinderLookupUsage obj)
            {
                if (obj == null)
                {
                    return 0;
                }

                int hashCode = (int)obj.Mode;
                hashCode = (hashCode * 397) ^ (obj.Path?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (obj.ComponentTypeName?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (obj.AssetPath?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ obj.Line;
                return hashCode;
            }
        }

        private sealed class TopLevelDisplayItem
        {
            public UObjectReferenceEntry SingleEntry;
            public UObjectFinderListGroup Group;
            public UObjectFinderMissingTargetIssue MissingTargetIssue;
            public int SortPriority;
            public string SortPath;
            public string SortScene;
            public string SortHierarchy;

            public static TopLevelDisplayItem CreateSingle(UObjectReferenceEntry entry)
            {
                return new TopLevelDisplayItem
                {
                    SingleEntry = entry,
                    SortPriority = GetSortPriority(entry.Issues),
                    SortPath = entry.ResolvedPath,
                    SortScene = entry.SceneName,
                    SortHierarchy = entry.HierarchyPath
                };
            }

            public static TopLevelDisplayItem CreateGroup(UObjectFinderListGroup group)
            {
                UObjectReferenceEntry sortEntry = group.Items[0];
                return new TopLevelDisplayItem
                {
                    Group = group,
                    SortPriority = GetSortPriority(group.Issues),
                    SortPath = group.Key,
                    SortScene = sortEntry.SceneName,
                    SortHierarchy = sortEntry.HierarchyPath
                };
            }

            public static TopLevelDisplayItem CreateMissingTarget(UObjectFinderMissingTargetIssue issue)
            {
                UObjectFinderLookupUsage firstUsage = issue.Usages.FirstOrDefault();
                return new TopLevelDisplayItem
                {
                    MissingTargetIssue = issue,
                    SortPriority = GetSortPriority(issue.Issues),
                    SortPath = issue.Path,
                    SortScene = firstUsage?.AssetPath,
                    SortHierarchy = firstUsage?.DisplayText
                };
            }
        }

        private sealed class UObjectFinderDisplayRow
        {
            public DisplayRowKind Kind;
            public UObjectReferenceEntry Entry;
            public UObjectFinderListGroup Group;
            public UObjectFinderMissingTargetIssue MissingTargetIssue;
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

            public static UObjectFinderDisplayRow CreateMissingTarget(UObjectFinderMissingTargetIssue issue)
            {
                return new UObjectFinderDisplayRow
                {
                    Kind = DisplayRowKind.MissingTarget,
                    MissingTargetIssue = issue
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
            private readonly Label m_ModeLabel;
            private readonly Label m_StatusLabel;
            private readonly Button m_LocateButton;

            public UObjectFinderRow(Action<UObjectFinderListGroup> toggleAction, Action<UObjectFinderDisplayRow> locateAction)
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

                m_ModeLabel = CreateFixedLabel(ModeColumnWidth);
                Add(m_ModeLabel);

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
                    if (userData is UObjectFinderDisplayRow row)
                    {
                        locateAction(row);
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
                    else
                    {
                        locateAction(row);
                    }
                });
            }

            public void Bind(UObjectFinderDisplayRow row, bool useOddStyle)
            {
                userData = row;
                style.backgroundColor = useOddStyle
                    ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                    : new Color(0.3f, 0.3f, 0.3f, 0.18f);

                switch (row.Kind)
                {
                    case DisplayRowKind.ListGroup:
                        BindGroupRow(row);
                        break;
                    case DisplayRowKind.MissingTarget:
                        BindMissingTargetRow(row);
                        break;
                    default:
                        BindEntryRow(row);
                        break;
                }
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
                m_ModeLabel.text = "List / Key";
                m_ModeLabel.tooltip = "列表组固定使用自定义Key";
                m_StatusLabel.text = GetGroupStatusText(group);
                m_StatusLabel.tooltip = GetGroupStatusMessage(group);
                m_StatusLabel.style.color = GetStatusColor(group.Issues);
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
                m_ModeLabel.text = entry.Mode == UObjectReference.RegistrationMode.List ? "List / Key" : entry.UseCustomKey ? "Single / Key" : "Single / Name";
                m_ModeLabel.tooltip = GetEntryModeDisplay(entry);
                m_StatusLabel.text = GetDisplayStatus(entry);
                m_StatusLabel.tooltip = entry.StatusMessage;
                m_StatusLabel.style.color = GetStatusColor(entry.Issues);
            }

            private void BindMissingTargetRow(UObjectFinderDisplayRow row)
            {
                UObjectFinderMissingTargetIssue issue = row.MissingTargetIssue;

                m_IndentElement.style.width = 0f;
                m_ExpandButton.style.display = DisplayStyle.None;
                m_LocateButton.style.display = DisplayStyle.Flex;

                m_PathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                m_PathLabel.text = GetPathDisplay(issue.Path);
                m_PathLabel.tooltip = GetPathDisplay(issue.Path);
                m_NameLabel.text = "<Missing Target>";
                m_NameLabel.tooltip = issue.StatusMessage;
                m_SceneLabel.text = "代码调用";
                m_SceneLabel.tooltip = BuildUsageTooltip(issue.Usages);
                m_ModeLabel.text = issue.Mode == LookupMode.List ? "List 查询" : "Single 查询";
                m_ModeLabel.tooltip = m_ModeLabel.text;
                m_StatusLabel.text = GetDisplayStatus(issue);
                m_StatusLabel.tooltip = issue.StatusMessage;
                m_StatusLabel.style.color = GetStatusColor(issue.Issues);
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

        private static readonly List<UObjectReferenceEntry> s_EmptyEntries = new();
    }
}
