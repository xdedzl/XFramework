using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Save;
#pragma warning disable CS0618 // ListView.onSelectionChange is required for this Unity version.

namespace XFramework.Editor
{
    public sealed class SaveDebuggerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/Save Debugger";
        private const float ProfilePaneWidth = 300f;
        private const double AutoApplyInterval = 0.2d;

        protected override double RefreshInterval => AutoApplyInterval;

        private readonly List<SaveProfileDebugSnapshot> m_Profiles = new();
        private readonly List<SaveDatabaseDebugSnapshot> m_Databases = new();

        private Label m_FeedbackLabel;
        private Label m_SelectedProfileLabel;
        private ListView m_ProfileListView;
        private ListView m_DatabaseListView;
        private Button m_CreateButton;
        private Button m_RevealFolderButton;
        private Button m_SaveButton;
        private Button m_DeleteButton;
        private Button m_ClearButton;

        private SaveStorageDebugSnapshot? m_Snapshot;
        private SaveProfileDebugSnapshot? m_SelectedProfile;
        private SaveDatabaseDebugSnapshot? m_SelectedDatabase;
        private bool m_IsSaveManagerLoaded;
        private double m_LastAutoApplyTime;
        private string m_SelectedProfileSignature;
        private string m_LastAutoApplyFailureSignature;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            SaveDebuggerWindow window = GetWindow<SaveDebuggerWindow>();
            window.titleContent = new GUIContent("Save Debugger");
            window.minSize = new Vector2(940f, 500f);
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshData(true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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
            root.style.flexGrow = 1;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;

            root.Add(BuildToolbar());

            TwoPaneSplitView splitView = new(0, ProfilePaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            splitView.Add(BuildProfilePane());
            splitView.Add(BuildDatabasePane());
            root.Add(splitView);
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

            m_RevealFolderButton = new Button(RevealSaveFolder)
            {
                text = "打开目录"
            };
            m_RevealFolderButton.tooltip = "在文件管理器中打开存档根目录";
            toolbar.Add(m_RevealFolderButton);

            m_CreateButton = new Button(CreateNewProfile)
            {
                text = "新建存档"
            };
            m_CreateButton.style.marginLeft = 6;
            m_CreateButton.tooltip = "仅限非 Play Mode。优先创建 auto_save，之后按 save_001 递增。";
            toolbar.Add(m_CreateButton);

            m_SaveButton = new Button(SaveSelectedProfile)
            {
                text = "保存所选档"
            };
            m_SaveButton.style.marginLeft = 6;
            m_SaveButton.tooltip = "将所选档位当前显示的数据写回 JSON 文件";
            toolbar.Add(m_SaveButton);

            m_DeleteButton = new Button(DeleteSelectedProfile)
            {
                text = "删除所选档"
            };
            m_DeleteButton.style.marginLeft = 6;
            m_DeleteButton.tooltip = "仅限非 Play Mode，删除所选档位的整个文件夹";
            toolbar.Add(m_DeleteButton);

            m_ClearButton = new Button(ClearAllProfiles)
            {
                text = "清空全部"
            };
            m_ClearButton.style.marginLeft = 6;
            m_ClearButton.tooltip = "仅限非 Play Mode，删除存档根目录下的全部档位";
            toolbar.Add(m_ClearButton);

            AddRefreshControls(toolbar, "重新扫描磁盘上的存档和当前运行时数据");

            m_FeedbackLabel = new Label();
            m_FeedbackLabel.style.marginLeft = 8;
            m_FeedbackLabel.style.flexGrow = 1;
            m_FeedbackLabel.style.minWidth = 0;
            m_FeedbackLabel.style.whiteSpace = WhiteSpace.NoWrap;
            m_FeedbackLabel.style.overflow = Overflow.Hidden;
            toolbar.Add(m_FeedbackLabel);

            return toolbar;
        }

        private VisualElement BuildProfilePane()
        {
            VisualElement pane = CreatePane();
            pane.Add(CreatePaneTitle("存档列表", marginBottom: 5f));
            pane.Add(BuildProfileListHeader());

            m_ProfileListView = new ListView
            {
                itemsSource = m_Profiles,
                fixedItemHeight = 24,
                selectionType = SelectionType.Single,
                makeItem = MakeProfileListItem,
                bindItem = BindProfileListItem
            };
            m_ProfileListView.style.flexGrow = 1;
            m_ProfileListView.style.marginTop = 4;
            m_ProfileListView.onSelectionChange += OnProfileSelectionChanged;
            pane.Add(m_ProfileListView);
            return pane;
        }

        private VisualElement BuildDatabasePane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginLeft = 4;
            pane.Add(CreatePaneTitle("细分存档", marginBottom: 5f));

            m_SelectedProfileLabel = new();
            m_SelectedProfileLabel.style.marginBottom = 5;
            m_SelectedProfileLabel.style.whiteSpace = WhiteSpace.Normal;
            m_SelectedProfileLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            pane.Add(m_SelectedProfileLabel);

            pane.Add(BuildDatabaseListHeader());

            m_DatabaseListView = new ListView
            {
                itemsSource = m_Databases,
                fixedItemHeight = 24,
                selectionType = SelectionType.Single,
                makeItem = MakeDatabaseListItem,
                bindItem = BindDatabaseListItem
            };
            m_DatabaseListView.style.flexGrow = 1;
            m_DatabaseListView.style.marginTop = 4;
            m_DatabaseListView.onSelectionChange += OnDatabaseSelectionChanged;
            pane.Add(m_DatabaseListView);
            return pane;
        }

        protected override void OnAutoRefresh()
        {
            TryAutoApplySelectedProfile();

            if (!Application.isPlaying || (m_SelectedProfile.HasValue && !m_SelectedProfile.Value.IsActive))
            {
                return;
            }

            RefreshData(false);
        }

        protected override void OnRefreshClicked()
        {
            RefreshFromToolbar();
        }

        private void RefreshFromToolbar()
        {
            TryAutoApplySelectedProfile();
            RefreshData(true);
            SetFeedback("已刷新存档列表。", MessageType.None);
        }

        private void RefreshData(bool forceViewRefresh)
        {
            SaveStorageDebugSnapshot? previousSnapshot = m_Snapshot;
            bool wasSaveManagerLoaded = m_IsSaveManagerLoaded;

            try
            {
                m_IsSaveManagerLoaded = Application.isPlaying && GameEntry.IsModuleLoaded<SaveManager>();
                m_Snapshot = m_IsSaveManagerLoaded
                    ? SaveManager.Instance.GetDebugStorageSnapshot()
                    : SaveManager.GetStoredDebugSnapshot();
                RebuildEntries();

                bool snapshotChanged = wasSaveManagerLoaded != m_IsSaveManagerLoaded
                    || !HasSameSnapshotState(previousSnapshot, m_Snapshot);
                RefreshView(forceViewRefresh || snapshotChanged);

                if (snapshotChanged)
                {
                    RefreshInspectorAfterSnapshotChange();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                m_IsSaveManagerLoaded = false;
                m_Snapshot = null;
                m_Profiles.Clear();
                m_Databases.Clear();
                m_SelectedProfile = null;
                m_SelectedDatabase = null;
                m_SelectedProfileSignature = null;
                m_LastAutoApplyFailureSignature = null;
                RefreshView(true);
                XFrameworkInspectorWindow.ClearIfOwner(this);
                SetFeedback($"读取存档失败: {exception.Message}", MessageType.Error);
            }
        }

        private void RebuildEntries()
        {
            m_Profiles.Clear();
            if (m_Snapshot.HasValue)
            {
                m_Profiles.AddRange(m_Snapshot.Value.Profiles);
            }

            m_SelectedProfile = ResolveSelectedProfile();
            if (!m_SelectedProfile.HasValue && m_Profiles.Count > 0)
            {
                m_SelectedProfile = FindDefaultProfile();
            }

            m_Databases.Clear();
            if (m_SelectedProfile.HasValue)
            {
                m_Databases.AddRange(m_SelectedProfile.Value.Databases);
            }

            m_SelectedDatabase = ResolveSelectedDatabase();
            CaptureSelectedProfileSignature();
        }

        private SaveProfileDebugSnapshot? ResolveSelectedProfile()
        {
            if (!m_SelectedProfile.HasValue)
            {
                return null;
            }

            string previousPath = m_SelectedProfile.Value.DirectoryPath;
            for (int i = 0; i < m_Profiles.Count; i++)
            {
                if (string.Equals(m_Profiles[i].DirectoryPath, previousPath, StringComparison.OrdinalIgnoreCase))
                {
                    return m_Profiles[i];
                }
            }

            return null;
        }

        private SaveProfileDebugSnapshot? FindDefaultProfile()
        {
            for (int i = 0; i < m_Profiles.Count; i++)
            {
                if (m_Profiles[i].IsActive)
                {
                    return m_Profiles[i];
                }
            }

            return m_Profiles.Count > 0 ? m_Profiles[0] : null;
        }

        private SaveDatabaseDebugSnapshot? ResolveSelectedDatabase()
        {
            if (!m_SelectedDatabase.HasValue)
            {
                return null;
            }

            SaveDatabaseDebugSnapshot previousDatabase = m_SelectedDatabase.Value;
            for (int i = 0; i < m_Databases.Count; i++)
            {
                SaveDatabaseDebugSnapshot database = m_Databases[i];
                if (string.Equals(database.FileName, previousDatabase.FileName, StringComparison.OrdinalIgnoreCase)
                    && database.DatabaseType == previousDatabase.DatabaseType)
                {
                    return database;
                }
            }

            return null;
        }

        private void RefreshView(bool rebuildLists)
        {
            RefreshSelectedProfileLabel();
            RefreshButtons();

            if (!rebuildLists)
            {
                return;
            }

            if (m_ProfileListView != null)
            {
                m_ProfileListView.itemsSource = m_Profiles;
                m_ProfileListView.Rebuild();
            }

            if (m_DatabaseListView != null)
            {
                m_DatabaseListView.itemsSource = m_Databases;
                m_DatabaseListView.Rebuild();
            }
        }

        private void RefreshSelectedProfileLabel()
        {
            if (m_SelectedProfileLabel == null)
            {
                return;
            }

            if (!m_SelectedProfile.HasValue)
            {
                m_SelectedProfileLabel.text = "请选择左侧存档。";
                return;
            }

            SaveProfileDebugSnapshot profile = m_SelectedProfile.Value;
            string profileName = FormatProfileName(profile);
            string status = profile.IsActive ? "当前运行时档位" : "磁盘档位";
            string error = string.IsNullOrWhiteSpace(profile.Error) ? string.Empty : $" | 元数据异常: {profile.Error}";
            m_SelectedProfileLabel.text = $"{status}: {profileName} | 数据库: {profile.Databases.Count}{error}";
        }

        private void RefreshButtons()
        {
            bool hasSnapshot = m_Snapshot.HasValue;
            bool hasRootPath = hasSnapshot && !string.IsNullOrWhiteSpace(m_Snapshot.Value.SaveRootPath);
            bool hasSelectedProfile = m_SelectedProfile.HasValue && m_SelectedProfile.Value.Meta != null;
            bool isActiveRuntimeProfile = Application.isPlaying && m_SelectedProfile.HasValue && m_SelectedProfile.Value.IsActive;
            bool canCreate = !Application.isPlaying;
            bool canDelete = hasSelectedProfile && !Application.isPlaying;
            bool canClear = m_Profiles.Count > 0 && !Application.isPlaying;

            if (m_RevealFolderButton != null)
            {
                m_RevealFolderButton.SetEnabled(hasRootPath);
            }

            if (m_CreateButton != null)
            {
                m_CreateButton.SetEnabled(canCreate);
            }

            if (m_SaveButton != null)
            {
                m_SaveButton.SetEnabled(hasSelectedProfile && !isActiveRuntimeProfile);
            }

            if (m_DeleteButton != null)
            {
                m_DeleteButton.SetEnabled(canDelete);
            }

            if (m_ClearButton != null)
            {
                m_ClearButton.SetEnabled(canClear);
            }
        }

        private void RevealSaveFolder()
        {
            if (!m_Snapshot.HasValue || string.IsNullOrWhiteSpace(m_Snapshot.Value.SaveRootPath))
            {
                SetFeedback("当前没有可打开的存档根目录。", MessageType.Warning);
                return;
            }

            EditorUtility.RevealInFinder(m_Snapshot.Value.SaveRootPath);
            SetFeedback("已打开存档根目录。", MessageType.None);
        }

        private void SaveSelectedProfile()
        {
            if (!m_SelectedProfile.HasValue)
            {
                SetFeedback("请先选择一个存档。", MessageType.Warning);
                return;
            }

            SaveProfileDebugSnapshot profile = m_SelectedProfile.Value;
            if (Application.isPlaying && profile.IsActive)
            {
                SetFeedback("当前运行中的存档为只读，不能保存或修改。", MessageType.Warning);
                return;
            }

            try
            {
                SaveManager.SaveDebugProfile(profile);

                CaptureSelectedProfileSignature();
                RefreshData(true);
                SetFeedback($"已保存存档 '{FormatProfileName(profile)}'。", MessageType.None);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetFeedback($"保存存档失败: {exception.Message}", MessageType.Error);
            }
        }

        private void TryAutoApplySelectedProfile()
        {
            if (!m_SelectedProfile.HasValue || m_SelectedProfile.Value.Meta == null)
            {
                return;
            }

            SaveProfileDebugSnapshot profile = m_SelectedProfile.Value;
            if (Application.isPlaying && profile.IsActive)
            {
                return;
            }

            string currentSignature;
            try
            {
                currentSignature = SaveManager.GetDebugProfileContentSignature(profile);
            }
            catch (Exception exception)
            {
                SetFeedback($"检测存档修改失败: {exception.Message}", MessageType.Error);
                return;
            }

            if (string.Equals(currentSignature, m_SelectedProfileSignature, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                SaveManager.SaveDebugProfile(profile);
                m_SelectedProfileSignature = currentSignature;
                m_LastAutoApplyFailureSignature = null;
                SetFeedback($"已自动应用存档 '{FormatProfileName(profile)}' 的修改。", MessageType.None);
            }
            catch (Exception exception)
            {
                if (!string.Equals(m_LastAutoApplyFailureSignature, currentSignature, StringComparison.Ordinal))
                {
                    Debug.LogException(exception);
                    m_LastAutoApplyFailureSignature = currentSignature;
                }

                SetFeedback($"自动应用存档失败: {exception.Message}", MessageType.Error);
            }
        }

        private void CaptureSelectedProfileSignature()
        {
            m_SelectedProfileSignature = null;
            m_LastAutoApplyFailureSignature = null;

            if (!m_SelectedProfile.HasValue || m_SelectedProfile.Value.Meta == null)
            {
                return;
            }

            SaveProfileDebugSnapshot profile = m_SelectedProfile.Value;
            if (Application.isPlaying && profile.IsActive)
            {
                return;
            }

            try
            {
                m_SelectedProfileSignature = SaveManager.GetDebugProfileContentSignature(profile);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetFeedback($"检测存档修改失败: {exception.Message}", MessageType.Error);
            }
        }

        private void CreateNewProfile()
        {
            if (Application.isPlaying)
            {
                SetFeedback("新建存档仅限非 Play Mode。", MessageType.Warning);
                return;
            }

            try
            {
                SaveProfileDebugSnapshot profile = SaveManager.CreateStoredDebugProfile();
                m_SelectedProfile = profile;
                m_SelectedDatabase = null;
                RefreshData(true);
                InspectSelectedProfile();
                SetFeedback($"已新建存档 '{FormatProfileName(profile)}'。", MessageType.None);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetFeedback($"新建存档失败: {exception.Message}", MessageType.Error);
            }
        }

        private void DeleteSelectedProfile()
        {
            if (Application.isPlaying)
            {
                SetFeedback("删除存档仅限非 Play Mode。", MessageType.Warning);
                return;
            }

            if (!m_SelectedProfile.HasValue)
            {
                SetFeedback("请先选择一个存档。", MessageType.Warning);
                return;
            }

            SaveProfileDebugSnapshot profile = m_SelectedProfile.Value;
            string profileName = FormatProfileName(profile);
            if (!EditorUtility.DisplayDialog(
                    "删除存档",
                    $"将永久删除存档 '{profileName}' 及其全部细分存档。",
                    "删除",
                    "取消"))
            {
                return;
            }

            try
            {
                SaveManager.DeleteDebugProfile(profile);
                m_SelectedProfile = null;
                m_SelectedDatabase = null;
                RefreshData(true);
                XFrameworkInspectorWindow.ClearIfOwner(this);
                SetFeedback($"已删除存档 '{profileName}'。", MessageType.None);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetFeedback($"删除存档失败: {exception.Message}", MessageType.Error);
            }
        }

        private void ClearAllProfiles()
        {
            if (Application.isPlaying)
            {
                SetFeedback("清空存档仅限非 Play Mode。", MessageType.Warning);
                return;
            }

            if (m_Profiles.Count == 0)
            {
                SetFeedback("没有可清空的存档。", MessageType.Warning);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "清空全部存档",
                    $"将永久删除全部 {m_Profiles.Count} 个存档及其细分存档，此操作无法撤销。",
                    "清空全部",
                    "取消"))
            {
                return;
            }

            try
            {
                SaveManager.ClearStoredDebugProfiles();
                m_SelectedProfile = null;
                m_SelectedDatabase = null;
                RefreshData(true);
                XFrameworkInspectorWindow.ClearIfOwner(this);
                SetFeedback("已清空全部存档。", MessageType.None);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetFeedback($"清空存档失败: {exception.Message}", MessageType.Error);
            }
        }

        private void OnProfileSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object selectedItem in selectedItems)
            {
                if (selectedItem is SaveProfileDebugSnapshot profile)
                {
                    m_SelectedProfile = profile;
                    m_SelectedDatabase = null;
                    m_Databases.Clear();
                    m_Databases.AddRange(profile.Databases);
                    CaptureSelectedProfileSignature();
                    RefreshView(true);
                    InspectSelectedProfile();
                    return;
                }
            }

            m_SelectedProfile = null;
            m_SelectedDatabase = null;
            m_Databases.Clear();
            CaptureSelectedProfileSignature();
            RefreshView(true);
            XFrameworkInspectorWindow.ClearIfOwner(this);
        }

        private void OnDatabaseSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object selectedItem in selectedItems)
            {
                if (selectedItem is SaveDatabaseDebugSnapshot database)
                {
                    m_SelectedDatabase = database;
                    InspectSelectedDatabase();
                    return;
                }
            }

            m_SelectedDatabase = null;
            InspectSelectedProfile();
        }

        private void InspectSelectedProfile()
        {
            if (!m_SelectedProfile.HasValue || m_SelectedProfile.Value.Meta == null)
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            SaveProfileDebugSnapshot profile = m_SelectedProfile.Value;
            bool readOnly = Application.isPlaying && profile.IsActive;
            string source = readOnly ? "当前运行时档位（只读）" : "磁盘 meta.json（修改自动写入）";
            XFrameworkInspectorWindow.InspectObject(
                this,
                $"{FormatProfileName(profile)} - Metadata",
                profile.Meta,
                $"{source}\n{profile.DirectoryPath}",
                readOnly: readOnly);
        }

        private void InspectSelectedDatabase()
        {
            if (!m_SelectedProfile.HasValue || !m_SelectedDatabase.HasValue)
            {
                InspectSelectedProfile();
                return;
            }

            SaveProfileDebugSnapshot profile = m_SelectedProfile.Value;
            SaveDatabaseDebugSnapshot database = m_SelectedDatabase.Value;
            string typeName = database.DatabaseType?.FullName ?? "未注册数据库";
            bool readOnly = Application.isPlaying && profile.IsActive;
            string source = readOnly ? "当前运行时数据（只读）" : "磁盘 JSON 数据（修改自动写入）";

            if (database.Database != null)
            {
                XFrameworkInspectorWindow.InspectObject(
                    this,
                    database.FileName,
                    database.Database,
                    $"{source} | {FormatProfileName(profile)}\n{typeName}",
                    readOnly: readOnly);
                return;
            }

            XFrameworkInspectorWindow.InspectCustom(
                this,
                database.FileName,
                BuildUnavailableDatabaseInspector,
                $"{source} | {FormatProfileName(profile)}\n{typeName}");
        }

        private void BuildUnavailableDatabaseInspector(VisualElement parent)
        {
            if (!m_SelectedDatabase.HasValue)
            {
                parent.Add(CreateMutedLabel("请选择一个细分存档。", wrap: true, marginTop: 6f));
                return;
            }

            SaveDatabaseDebugSnapshot database = m_SelectedDatabase.Value;
            parent.Add(CreateInfoLabel("无法绑定为 SaveDatabase。"));
            if (!string.IsNullOrWhiteSpace(database.Error))
            {
                parent.Add(CreateMutedLabel(database.Error, wrap: true, marginTop: 6f));
            }

            if (string.IsNullOrWhiteSpace(database.RawJson))
            {
                return;
            }

            TextField rawJsonField = new("Raw JSON")
            {
                multiline = true,
                value = database.RawJson
            };
            rawJsonField.style.marginTop = 10;
            rawJsonField.style.height = 260;
            rawJsonField.SetEnabled(false);
            parent.Add(rawJsonField);
        }

        private void RefreshInspectorAfterSnapshotChange()
        {
            if (m_SelectedDatabase.HasValue)
            {
                InspectSelectedDatabase();
                return;
            }

            if (m_SelectedProfile.HasValue)
            {
                InspectSelectedProfile();
                return;
            }

            XFrameworkInspectorWindow.ClearIfOwner(this);
        }

        private VisualElement MakeProfileListItem()
        {
            VisualElement row = CreateRow(Color.clear, 24);
            row.Add(CreateCellLabel("status", 48));
            row.Add(CreateCellLabel("name", 116));

            Label displayName = CreateCellLabel("displayName", 0);
            displayName.style.flexGrow = 1;
            row.Add(displayName);
            return row;
        }

        private void BindProfileListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_Profiles.Count)
            {
                return;
            }

            SaveProfileDebugSnapshot profile = m_Profiles[index];
            bool isSelected = m_SelectedProfile.HasValue
                && string.Equals(m_SelectedProfile.Value.DirectoryPath, profile.DirectoryPath, StringComparison.OrdinalIgnoreCase);
            element.style.backgroundColor = GetRowColor(index, isSelected);
            element.tooltip = profile.DirectoryPath;

            element.Q<Label>("status").text = profile.IsActive ? "当前" : "磁盘";
            element.Q<Label>("name").text = profile.Meta?.saveName ?? "<Unknown>";
            element.Q<Label>("displayName").text = profile.Meta?.displayName ?? "<Unknown>";
        }

        private VisualElement MakeDatabaseListItem()
        {
            VisualElement row = CreateRow(Color.clear, 24);
            row.Add(CreateCellLabel("fileName", 180));

            Label databaseType = CreateCellLabel("databaseType", 0);
            databaseType.style.flexGrow = 1;
            row.Add(databaseType);

            row.Add(CreateCellLabel("status", 72));
            return row;
        }

        private void BindDatabaseListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_Databases.Count)
            {
                return;
            }

            SaveDatabaseDebugSnapshot database = m_Databases[index];
            bool isSelected = m_SelectedDatabase.HasValue
                && string.Equals(m_SelectedDatabase.Value.FileName, database.FileName, StringComparison.OrdinalIgnoreCase)
                && m_SelectedDatabase.Value.DatabaseType == database.DatabaseType;
            element.style.backgroundColor = GetRowColor(index, isSelected);
            element.tooltip = BuildDatabaseTooltip(database);

            element.Q<Label>("fileName").text = database.FileName;
            element.Q<Label>("databaseType").text = database.DatabaseType?.Name ?? "<Unregistered>";
            element.Q<Label>("status").text = GetDatabaseStatus(database);
        }

        private static bool HasSameSnapshotState(
            SaveStorageDebugSnapshot? previous,
            SaveStorageDebugSnapshot? current)
        {
            if (previous.HasValue != current.HasValue)
            {
                return false;
            }

            if (!previous.HasValue)
            {
                return true;
            }

            SaveStorageDebugSnapshot previousValue = previous.Value;
            SaveStorageDebugSnapshot currentValue = current.Value;
            if (!string.Equals(previousValue.SaveRootPath, currentValue.SaveRootPath, StringComparison.Ordinal)
                || previousValue.Profiles.Count != currentValue.Profiles.Count)
            {
                return false;
            }

            for (int i = 0; i < previousValue.Profiles.Count; i++)
            {
                if (!HasSameProfileState(previousValue.Profiles[i], currentValue.Profiles[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasSameProfileState(SaveProfileDebugSnapshot previous, SaveProfileDebugSnapshot current)
        {
            if (!string.Equals(previous.DirectoryPath, current.DirectoryPath, StringComparison.OrdinalIgnoreCase)
                || previous.IsActive != current.IsActive
                || !string.Equals(previous.Error, current.Error, StringComparison.Ordinal)
                || !HasSameMeta(previous.Meta, current.Meta)
                || previous.Databases.Count != current.Databases.Count)
            {
                return false;
            }

            for (int i = 0; i < previous.Databases.Count; i++)
            {
                SaveDatabaseDebugSnapshot previousDatabase = previous.Databases[i];
                SaveDatabaseDebugSnapshot currentDatabase = current.Databases[i];
                if (!string.Equals(previousDatabase.FileName, currentDatabase.FileName, StringComparison.OrdinalIgnoreCase)
                    || previousDatabase.DatabaseType != currentDatabase.DatabaseType
                    || !string.Equals(previousDatabase.Error, currentDatabase.Error, StringComparison.Ordinal)
                    || !string.Equals(previousDatabase.RawJson, currentDatabase.RawJson, StringComparison.Ordinal)
                    || (current.IsActive && previousDatabase.Database != currentDatabase.Database))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasSameMeta(SaveMeta previous, SaveMeta current)
        {
            if (previous == null || current == null)
            {
                return previous == current;
            }

            return previous.saveName == current.saveName
                && previous.displayName == current.displayName
                && previous.createdAt == current.createdAt
                && previous.updatedAt == current.updatedAt
                && previous.version == current.version;
        }

        private static VisualElement BuildProfileListHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22);
            header.Add(CreateHeaderLabel("状态", 48));
            header.Add(CreateHeaderLabel("档位", 116));

            Label displayName = CreateHeaderLabel("显示名", 0);
            displayName.style.flexGrow = 1;
            header.Add(displayName);
            return header;
        }

        private static VisualElement BuildDatabaseListHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22);
            header.Add(CreateHeaderLabel("JSON 文件", 180));

            Label databaseType = CreateHeaderLabel("数据库类型", 0);
            databaseType.style.flexGrow = 1;
            header.Add(databaseType);

            header.Add(CreateHeaderLabel("状态", 72));
            return header;
        }

        private static Label CreateInfoLabel(string text)
        {
            Label label = new(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(1f, 0.72f, 0.50f);
            return label;
        }

        private static Color GetRowColor(int index, bool isSelected)
        {
            if (isSelected)
            {
                return new Color(0.24f, 0.42f, 0.72f, 0.45f);
            }

            return index % 2 == 0
                ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                : new Color(0.31f, 0.31f, 0.31f, 0.18f);
        }

        private static string FormatProfileName(SaveProfileDebugSnapshot profile)
        {
            return string.IsNullOrWhiteSpace(profile.Meta?.saveName)
                ? "<Unknown>"
                : profile.Meta.saveName;
        }

        private static string GetDatabaseStatus(SaveDatabaseDebugSnapshot database)
        {
            if (database.Database != null)
            {
                return string.IsNullOrWhiteSpace(database.Error) ? "可检查" : "有警告";
            }

            return string.IsNullOrWhiteSpace(database.Error) ? "缺失" : "异常";
        }

        private static string BuildDatabaseTooltip(SaveDatabaseDebugSnapshot database)
        {
            string typeName = database.DatabaseType?.FullName ?? "未注册数据库";
            return string.IsNullOrWhiteSpace(database.Error)
                ? typeName
                : $"{typeName}\n{database.Error}";
        }

        private void SetFeedback(string text, MessageType type)
        {
            if (m_FeedbackLabel == null)
            {
                return;
            }

            m_FeedbackLabel.text = text ?? string.Empty;
            m_FeedbackLabel.style.color = type switch
            {
                MessageType.Error => new Color(1f, 0.52f, 0.48f),
                MessageType.Warning => new Color(0.95f, 0.78f, 0.35f),
                _ => new Color(0.62f, 0.90f, 0.66f)
            };
        }
    }
}
#pragma warning restore CS0618
