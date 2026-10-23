using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Command;

namespace XFramework.Editor
{
    public sealed class XCommandEditorWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/XCommand";
        private const string AllModeFilter = "全部";
        private const string AvailableModeFilter = "当前可用";
        private const string AllCategoryFilter = "全部分类";
        private const int MAX_CONSOLE_TEXT_LENGTH = 8000;
        private const int MAX_CONSOLE_TEXT_LINES = 60;
        private const int MAX_COPY_ALL_TEXT_LENGTH = 512000;
        private const string COPY_ALL_TRUNCATION_NOTICE = "\n… [复制已截断：总长度超过限制；请用单条记录的复制按钮获取完整内容]";

        private static readonly Color TerminalBackground = new Color(0.055f, 0.06f, 0.065f);
        private static readonly Color TerminalBorder = new Color(0.20f, 0.23f, 0.25f);
        private static readonly Color TerminalText = new Color(0.78f, 0.82f, 0.84f);
        private static readonly Color CommandColor = new Color(0.37f, 0.82f, 0.94f);
        private static readonly Color PromptColor = new Color(0.38f, 0.92f, 0.52f);
        private static readonly Color FavoriteStarColor = new Color(1f, 0.78f, 0.18f);
        private static readonly Color FavoriteStarInactiveColor = new Color(0.58f, 0.61f, 0.63f);
        private readonly List<XCommandDescriptor> m_AllCommands = new List<XCommandDescriptor>();
        private readonly List<XCommandDescriptor> m_FilteredCommands = new List<XCommandDescriptor>();
        private readonly List<XCommandDescriptor> m_CommandSuggestions = new List<XCommandDescriptor>();
        private readonly List<XCommandDiagnostic> m_Diagnostics = new List<XCommandDiagnostic>();
        private readonly HashSet<string> m_Favorites = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<XWorkflowEntry> m_Workflows = new List<XWorkflowEntry>();
        private readonly List<XCommandRecord> loadedCommandRecords = new List<XCommandRecord>();
        private readonly HashSet<long> loadedCommandRecordIds = new HashSet<long>();
        private readonly List<XCommandRecord> displayedCommandRecords = new List<XCommandRecord>();
        private readonly Dictionary<long, int> displayedRecordIndices = new Dictionary<long, int>();
        private readonly Dictionary<long, XCommandRecord> pendingConsoleRecords = new Dictionary<long, XCommandRecord>();

        private Label m_EnvironmentLabel;
        private Label m_TerminalSummaryLabel;
        private Button m_CliServerMenuButton;
        private Toggle m_AutoScrollToggle;
        private Toggle m_ShowHiddenCommandRecordsToggle;
        private Toggle m_CommandReferenceToggle;
        private TwoPaneSplitView m_MainSplitView;
        private ListView consoleList;
        private VisualElement consoleEmptyState;
        private IVisualElementScheduledItem consoleRefreshSchedule;
        private IVisualElementScheduledItem consoleScrollSchedule;
        private bool consoleStructureDirty;
        private bool consoleScrollPending;
        private TextField m_CommandLineField;
        private Button m_ExecuteButton;
        private Label m_PromptHintLabel;
        private VisualElement m_SuggestionPanel;
        private Label m_SuggestionSummaryLabel;
        private ListView m_SuggestionList;
        private TextField m_SearchField;
        private DropdownField m_ModeFilter;
        private DropdownField m_CategoryFilter;
        private Button m_FavoritesOnlyButton;
        private bool m_FavoritesOnly;
        private Label m_CommandSummaryLabel;
        private ListView m_CommandList;
        private Label m_DetailTitleLabel;
        private Label m_DetailAvailabilityLabel;
        private Label m_DetailDescriptionLabel;
        private Label m_DetailUsageLabel;
        private Label m_DetailMetadataLabel;
        private Label m_DetailDeclarationLabel;
        private Button m_FillCommandButton;
        private Foldout m_DiagnosticsFoldout;
        private VisualElement m_DiagnosticsContainer;
        private Button m_CommandTabButton;
        private Button m_WorkflowTabButton;
        private VisualElement m_CommandTabContent;
        private VisualElement m_WorkflowTabContent;
        private ListView m_WorkflowList;
        private Label m_WorkflowDetailTitleLabel;
        private Label m_WorkflowDetailPathLabel;
        private Label m_WorkflowDetailContentLabel;

        private XCommandDescriptor m_SelectedCommand;
        private XWorkflowEntry m_SelectedWorkflow;
        private string m_CommandSummaryText = string.Empty;
        private string m_WorkflowScanMessage = string.Empty;
        private int m_CommandHistoryIndex = -1;
        private int m_CommandSuggestionIndex = -1;
        private bool m_ApplyingCommandHistory;
        private bool m_ShowingCommandSuggestions;
        private bool m_CommandHubSubscribed;
        private IVisualElementScheduledItem m_CliServerStatusSchedule;
        [SerializeField] private long m_VisibleAfterRecordId;
        [SerializeField] private bool m_ShowHiddenCommandRecords = true;
        [SerializeField] private string m_SelectedCommandName;
        [SerializeField] private bool m_VisibleCommandRecordsInitialized;
        [SerializeField] private int m_ActiveReferenceTab;
        [SerializeField] private string m_SelectedWorkflowName;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XCommandEditorWindow window = GetWindow<XCommandEditorWindow>();
            window.titleContent = new GUIContent("XCommand");
            window.minSize = new Vector2(980f, 600f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            XCommandEditorHistory.EnsureInitialized();
            LoadPreferences();
            XCommandRegistry.Refresh();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            StopConsoleUpdates();
            m_CliServerStatusSchedule?.Pause();
            m_CliServerStatusSchedule = null;
            UnsubscribeCommandHub();
            SavePreferences();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public void CreateGUI()
        {
            StopConsoleUpdates();
            displayedCommandRecords.Clear();
            displayedRecordIndices.Clear();
            m_CliServerStatusSchedule?.Pause();
            UnsubscribeCommandHub();
            BuildUI();
            if (m_VisibleCommandRecordsInitialized)
            {
                RestoreVisibleCommandRecords();
            }
            else
            {
                m_VisibleCommandRecordsInitialized = true;
                ResetVisibleCommandRecords();
            }
            SubscribeCommandHub();
            RefreshCliServerStatus();
            m_CliServerStatusSchedule = rootVisualElement.schedule.Execute(RefreshCliServerStatus).Every(500);
            RefreshRegistry();
            RefreshWorkflows();
            ApplyReferenceTabState();
            FocusCommandLine();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 4f;
            root.style.paddingRight = 4f;
            root.style.paddingTop = 4f;
            root.style.paddingBottom = 4f;
            root.style.backgroundColor = new Color(0.11f, 0.115f, 0.12f);

            root.Add(BuildConsoleToolbar());

            m_MainSplitView = new TwoPaneSplitView(0, 690f, TwoPaneSplitViewOrientation.Horizontal);
            m_MainSplitView.style.flexGrow = 1f;
            m_MainSplitView.style.marginTop = 4f;
            m_MainSplitView.Add(BuildTerminalPane());
            m_MainSplitView.Add(BuildCommandReferencePane());
            root.Add(m_MainSplitView);
            m_MainSplitView.CollapseChild(1);
        }

        private VisualElement BuildConsoleToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.height = 32f;
            toolbar.style.flexShrink = 0f;
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 8f;
            toolbar.style.paddingRight = 6f;
            toolbar.style.backgroundColor = new Color(0.075f, 0.08f, 0.085f);
            toolbar.style.borderBottomWidth = 1f;
            toolbar.style.borderBottomColor = TerminalBorder;

            var title = new Label("$ XCommand");
            title.style.width = 112f;
            title.style.fontSize = 15f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = PromptColor;
            toolbar.Add(title);

            m_EnvironmentLabel = new Label();
            m_EnvironmentLabel.style.width = 92f;
            m_EnvironmentLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_EnvironmentLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(m_EnvironmentLabel);

            m_CliServerMenuButton = CreateToolbarButton("CLI", ShowCliServerMenu, 104f);
            toolbar.Add(m_CliServerMenuButton);

            m_TerminalSummaryLabel = new Label();
            m_TerminalSummaryLabel.style.flexGrow = 1f;
            m_TerminalSummaryLabel.style.marginLeft = 12f;
            m_TerminalSummaryLabel.style.color = new Color(0.56f, 0.60f, 0.62f);
            toolbar.Add(m_TerminalSummaryLabel);

            m_AutoScrollToggle = new Toggle("自动滚动")
            {
                value = true
            };
            m_AutoScrollToggle.RegisterValueChangedCallback(OnConsoleAutoScrollChanged);
            m_AutoScrollToggle.style.marginRight = 8f;
            toolbar.Add(m_AutoScrollToggle);

            m_CommandReferenceToggle = new Toggle("Command Reference")
            {
                value = false
            };
            m_CommandReferenceToggle.style.marginRight = 8f;
            m_CommandReferenceToggle.tooltip = "显示或隐藏右侧命令手册与 Workflow 列表。";
            m_CommandReferenceToggle.RegisterValueChangedCallback(evt => SetCommandReferenceVisible(evt.newValue));
            toolbar.Add(m_CommandReferenceToggle);

            toolbar.Add(CreateToolbarButton("复制全部", CopyAllConsoleOutput, 72f));
            toolbar.Add(CreateToolbarButton("刷新命令", RefreshCommands, 72f));
            return toolbar;
        }

        private void RefreshCliServerStatus()
        {
            bool running = XFrameworkCliServer.IsRunning;
            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            string endpoint = running ? $"127.0.0.1:{XFrameworkCliServer.Port}" : "未监听";
            m_CliServerMenuButton.text = running ? $"CLI ● :{XFrameworkCliServer.Port}" : "CLI ○ 已停止";
            m_CliServerMenuButton.style.color = running ? PromptColor : new Color(0.85f, 0.48f, 0.40f);
            m_CliServerMenuButton.tooltip = $"{(running ? "运行中" : "已停止")}  ·  {endpoint}\nPID {processId}  ·  Editor  ·  {(Application.isPlaying ? "Play Mode" : "Edit Mode")}\nLoopback Only\n\n点击管理 Server";
        }

        private void ShowCliServerMenu()
        {
            bool running = XFrameworkCliServer.IsRunning;
            var menu = new GenericMenu();
            menu.AddDisabledItem(new GUIContent(running ? $"运行中 · PID {System.Diagnostics.Process.GetCurrentProcess().Id} · 127.0.0.1:{XFrameworkCliServer.Port}" : "Server 已停止"));
            menu.AddSeparator(string.Empty);
            if (running)
            {
                menu.AddItem(new GUIContent("停止 Server"), false, StopCliServer);
                menu.AddItem(new GUIContent("重启 Server"), false, RestartCliServer);
            }
            else
            {
                menu.AddItem(new GUIContent("启动 Server"), false, StartCliServer);
                menu.AddDisabledItem(new GUIContent("重启 Server"));
            }
            menu.AddSeparator(string.Empty);
            if (running)
            {
                menu.AddItem(new GUIContent("复制连接参数"), false, CopyCliServerConnection);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("复制连接参数"));
            }
            menu.ShowAsContext();
        }

        private void StopCliServer()
        {
            XFrameworkCliServer.Stop();
            RefreshCliServerStatus();
        }

        private void RestartCliServer()
        {
            XFrameworkCliServer.Stop();
            StartCliServer();
        }

        private void StartCliServer()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            XFrameworkCliServer.Start(XFrameworkCliProcessType.Editor, projectPath);
            RefreshCliServerStatus();
        }

        private static void CopyCliServerConnection()
        {
            EditorGUIUtility.systemCopyBuffer = $"--port {XFrameworkCliServer.Port}";
        }

        private void SetCommandReferenceVisible(bool visible)
        {
            if (visible)
            {
                m_MainSplitView.UnCollapse();
            }
            else
            {
                m_MainSplitView.CollapseChild(1);
                FocusCommandLine();
            }
        }

        private VisualElement BuildTerminalPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1f;
            pane.style.minWidth = 0f;
            pane.style.marginRight = 3f;
            pane.style.backgroundColor = TerminalBackground;
            pane.style.borderLeftWidth = 1f;
            pane.style.borderRightWidth = 1f;
            pane.style.borderTopWidth = 1f;
            pane.style.borderBottomWidth = 1f;
            pane.style.borderLeftColor = TerminalBorder;
            pane.style.borderRightColor = TerminalBorder;
            pane.style.borderTopColor = TerminalBorder;
            pane.style.borderBottomColor = TerminalBorder;

            var terminalHeader = new VisualElement();
            terminalHeader.style.height = 28f;
            terminalHeader.style.flexShrink = 0f;
            terminalHeader.style.flexDirection = FlexDirection.Row;
            terminalHeader.style.alignItems = Align.Center;
            terminalHeader.style.paddingLeft = 9f;
            terminalHeader.style.paddingRight = 8f;
            terminalHeader.style.backgroundColor = new Color(0.08f, 0.085f, 0.09f);
            terminalHeader.style.borderBottomWidth = 1f;
            terminalHeader.style.borderBottomColor = TerminalBorder;

            var headerTitle = new Label("TERMINAL");
            headerTitle.style.flexGrow = 1f;
            headerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerTitle.style.color = new Color(0.65f, 0.69f, 0.71f);
            terminalHeader.Add(headerTitle);

            m_ShowHiddenCommandRecordsToggle = new Toggle("显示隐藏命令")
            {
                value = m_ShowHiddenCommandRecords
            };
            m_ShowHiddenCommandRecordsToggle.style.marginRight = 8f;
            m_ShowHiddenCommandRecordsToggle.tooltip = "控制当前 Editor Terminal 是否显示 Hidden 命令的执行记录，不删除 Hub 记录。";
            m_ShowHiddenCommandRecordsToggle.RegisterValueChangedCallback(evt =>
            {
                m_ShowHiddenCommandRecords = evt.newValue;
                RequestConsoleRefresh(true);
            });
            terminalHeader.Add(m_ShowHiddenCommandRecordsToggle);

            terminalHeader.Add(CreateEntryButton("加载历史", LoadCommandHistory));
            terminalHeader.Add(CreateEntryButton("清空输出", ClearConsoleOutput));
            var keyboardHint = new Label("Enter 执行  ·  Tab 预测  ·  ↑↓ 候选/历史  ·  Esc 关闭/清空");
            keyboardHint.style.marginLeft = 8f;
            keyboardHint.style.color = new Color(0.43f, 0.47f, 0.49f);
            terminalHeader.Add(keyboardHint);
            pane.Add(terminalHeader);

            pane.Add(BuildConsoleContent());

            pane.Add(BuildPromptArea());
            return pane;
        }

        private VisualElement BuildPromptArea()
        {
            var promptArea = new VisualElement();
            promptArea.style.flexShrink = 0f;
            promptArea.style.paddingLeft = 9f;
            promptArea.style.paddingRight = 8f;
            promptArea.style.paddingTop = 7f;
            promptArea.style.paddingBottom = 6f;
            promptArea.style.backgroundColor = new Color(0.07f, 0.075f, 0.08f);
            promptArea.style.borderTopWidth = 1f;
            promptArea.style.borderTopColor = TerminalBorder;

            var promptRow = new VisualElement();
            promptRow.style.height = 28f;
            promptRow.style.flexDirection = FlexDirection.Row;
            promptRow.style.alignItems = Align.Center;

            var prompt = new Label(">");
            prompt.style.width = 20f;
            prompt.style.fontSize = 16f;
            prompt.style.unityFontStyleAndWeight = FontStyle.Bold;
            prompt.style.color = PromptColor;
            promptRow.Add(prompt);

            m_CommandLineField = new TextField();
            m_CommandLineField.style.flexGrow = 1f;
            m_CommandLineField.style.height = 24f;
            m_CommandLineField.style.color = TerminalText;
            m_CommandLineField.tooltip = "输入完整命令行。命令参数继续以原始 string 传递。";
            m_CommandLineField.RegisterValueChangedCallback(_ => OnCommandLineChanged());
            m_CommandLineField.RegisterCallback<KeyDownEvent>(OnCommandLineKeyDown, TrickleDown.TrickleDown);
            VisualElement textInput = m_CommandLineField.Q<VisualElement>("unity-text-input");
            textInput.style.backgroundColor = new Color(0.045f, 0.05f, 0.055f);
            textInput.style.borderLeftWidth = 0f;
            textInput.style.borderRightWidth = 0f;
            textInput.style.borderTopWidth = 0f;
            textInput.style.borderBottomWidth = 0f;
            promptRow.Add(m_CommandLineField);

            m_ExecuteButton = new Button(ExecuteCommandLine)
            {
                text = "执行"
            };
            m_ExecuteButton.style.width = 58f;
            m_ExecuteButton.style.height = 24f;
            m_ExecuteButton.style.marginLeft = 7f;
            promptRow.Add(m_ExecuteButton);
            promptArea.Add(promptRow);

            promptArea.Add(BuildCommandSuggestionPanel());

            m_PromptHintLabel = new Label("输入命令，或从右侧命令手册双击填入。输入前缀后按 Tab 可补全。 ");
            m_PromptHintLabel.style.height = 20f;
            m_PromptHintLabel.style.marginLeft = 20f;
            m_PromptHintLabel.style.marginTop = 3f;
            m_PromptHintLabel.style.color = new Color(0.46f, 0.51f, 0.53f);
            promptArea.Add(m_PromptHintLabel);
            return promptArea;
        }

        private VisualElement BuildCommandSuggestionPanel()
        {
            m_SuggestionPanel = new VisualElement();
            m_SuggestionPanel.style.display = DisplayStyle.None;
            m_SuggestionPanel.style.marginLeft = 20f;
            m_SuggestionPanel.style.marginTop = 4f;
            m_SuggestionPanel.style.marginBottom = 2f;
            m_SuggestionPanel.style.paddingLeft = 5f;
            m_SuggestionPanel.style.paddingRight = 5f;
            m_SuggestionPanel.style.paddingTop = 4f;
            m_SuggestionPanel.style.paddingBottom = 4f;
            m_SuggestionPanel.style.backgroundColor = new Color(0.065f, 0.07f, 0.075f);
            m_SuggestionPanel.style.borderLeftWidth = 1f;
            m_SuggestionPanel.style.borderRightWidth = 1f;
            m_SuggestionPanel.style.borderTopWidth = 1f;
            m_SuggestionPanel.style.borderBottomWidth = 1f;
            m_SuggestionPanel.style.borderLeftColor = TerminalBorder;
            m_SuggestionPanel.style.borderRightColor = TerminalBorder;
            m_SuggestionPanel.style.borderTopColor = TerminalBorder;
            m_SuggestionPanel.style.borderBottomColor = TerminalBorder;

            m_SuggestionSummaryLabel = new Label();
            m_SuggestionSummaryLabel.style.height = 20f;
            m_SuggestionSummaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_SuggestionSummaryLabel.style.color = CommandColor;
            m_SuggestionPanel.Add(m_SuggestionSummaryLabel);

            m_SuggestionList = new ListView
            {
                itemsSource = m_CommandSuggestions,
                fixedItemHeight = 38f,
                selectionType = SelectionType.Single,
                makeItem = MakeCommandSuggestionItem,
                bindItem = BindCommandSuggestionItem
            };
            m_SuggestionList.selectionChanged += OnCommandSuggestionSelectionChanged;
            m_SuggestionList.itemsChosen += OnCommandSuggestionChosen;
            m_SuggestionPanel.Add(m_SuggestionList);
            return m_SuggestionPanel;
        }

        private VisualElement BuildCommandReferencePane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1f;
            pane.style.minWidth = 300f;
            pane.style.minHeight = 0f;
            pane.style.marginLeft = 3f;
            pane.style.paddingLeft = 8f;
            pane.style.paddingRight = 8f;
            pane.style.paddingTop = 7f;
            pane.style.paddingBottom = 6f;
            pane.style.backgroundColor = new Color(0.14f, 0.145f, 0.15f);
            pane.style.borderLeftWidth = 1f;
            pane.style.borderRightWidth = 1f;
            pane.style.borderTopWidth = 1f;
            pane.style.borderBottomWidth = 1f;
            pane.style.borderLeftColor = TerminalBorder;
            pane.style.borderRightColor = TerminalBorder;
            pane.style.borderTopColor = TerminalBorder;
            pane.style.borderBottomColor = TerminalBorder;

            var titleRow = new VisualElement();
            titleRow.style.height = 26f;
            titleRow.style.flexShrink = 0f;
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var title = new Label("COMMAND REFERENCE");
            title.style.flexGrow = 1f;
            title.style.fontSize = 13f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = CommandColor;
            titleRow.Add(title);

            m_CommandSummaryLabel = new Label();
            m_CommandSummaryLabel.style.color = new Color(0.55f, 0.59f, 0.61f);
            titleRow.Add(m_CommandSummaryLabel);
            pane.Add(titleRow);

            var tabBar = new VisualElement();
            tabBar.style.flexShrink = 0f;
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.alignItems = Align.Center;
            tabBar.style.marginTop = 6f;
            tabBar.style.marginBottom = 2f;

            m_CommandTabButton = CreateReferenceTabButton("命令", () => SetActiveReferenceTab(0));
            tabBar.Add(m_CommandTabButton);

            m_WorkflowTabButton = CreateReferenceTabButton("Workflow", () => SetActiveReferenceTab(1));
            m_WorkflowTabButton.style.marginLeft = 4f;
            tabBar.Add(m_WorkflowTabButton);
            pane.Add(tabBar);

            m_CommandTabContent = new VisualElement();
            m_CommandTabContent.style.flexGrow = 1f;
            m_CommandTabContent.style.flexBasis = 0f;
            m_CommandTabContent.style.minHeight = 0f;
            pane.Add(m_CommandTabContent);

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexShrink = 0f;
            m_SearchField.tooltip = "匹配命令名、显示名、分类、说明和声明类型；空格分词后全部命中。";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshCommandView());
            m_CommandTabContent.Add(m_SearchField);

            var filterRow = new VisualElement();
            filterRow.style.flexShrink = 0f;
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.alignItems = Align.Center;
            filterRow.style.marginTop = 4f;

            m_ModeFilter = new DropdownField(new List<string> { AllModeFilter, AvailableModeFilter, XCommandMode.Runtime.ToString(), XCommandMode.Editor.ToString(), XCommandMode.Both.ToString() }, 0);
            m_ModeFilter.style.width = 112f;
            m_ModeFilter.style.flexShrink = 0f;
            m_ModeFilter.tooltip = "按命令作用域筛选。";
            m_ModeFilter.RegisterValueChangedCallback(_ => RefreshCommandView());
            filterRow.Add(m_ModeFilter);

            m_CategoryFilter = new DropdownField(new List<string> { AllCategoryFilter }, 0);
            m_CategoryFilter.style.flexGrow = 1f;
            m_CategoryFilter.style.flexShrink = 1f;
            m_CategoryFilter.style.minWidth = 65f;
            m_CategoryFilter.style.marginLeft = 5f;
            m_CategoryFilter.tooltip = "按命令分类筛选。";
            m_CategoryFilter.RegisterValueChangedCallback(_ => RefreshCommandView());
            filterRow.Add(m_CategoryFilter);

            m_FavoritesOnlyButton = new Button(ToggleFavoritesOnly)
            {
                text = "[ ] 仅收藏"
            };
            m_FavoritesOnlyButton.style.width = 86f;
            m_FavoritesOnlyButton.style.flexShrink = 0f;
            m_FavoritesOnlyButton.style.marginLeft = 6f;
            m_FavoritesOnlyButton.tooltip = "只显示已收藏命令。再次点击后恢复全部筛选结果。";
            filterRow.Add(m_FavoritesOnlyButton);
            m_CommandTabContent.Add(filterRow);

            m_CommandList = new ListView
            {
                itemsSource = m_FilteredCommands,
                fixedItemHeight = 44f,
                selectionType = SelectionType.Single,
                makeItem = MakeCommandItem,
                bindItem = BindCommandItem
            };
            m_CommandList.style.height = 270f;
            m_CommandList.style.minHeight = 88f;
            m_CommandList.style.flexShrink = 1f;
            m_CommandList.style.marginTop = 6f;
            m_CommandList.style.marginBottom = 6f;
            m_CommandList.selectionChanged += OnCommandSelectionChanged;
            m_CommandList.itemsChosen += OnCommandItemsChosen;
            m_CommandTabContent.Add(m_CommandList);

            ScrollView detailsScroll = BuildCommandDetails();
            detailsScroll.style.flexGrow = 1f;
            detailsScroll.style.minHeight = 0f;
            m_CommandTabContent.Add(detailsScroll);

            m_WorkflowTabContent = new VisualElement();
            m_WorkflowTabContent.style.flexGrow = 1f;
            m_WorkflowTabContent.style.flexBasis = 0f;
            m_WorkflowTabContent.style.minHeight = 0f;
            m_WorkflowTabContent.style.display = DisplayStyle.None;
            pane.Add(m_WorkflowTabContent);

            m_WorkflowList = new ListView
            {
                itemsSource = m_Workflows,
                fixedItemHeight = 44f,
                selectionType = SelectionType.Single,
                makeItem = MakeWorkflowItem,
                bindItem = BindWorkflowItem
            };
            m_WorkflowList.style.height = 270f;
            m_WorkflowList.style.minHeight = 88f;
            m_WorkflowList.style.flexShrink = 1f;
            m_WorkflowList.style.marginTop = 6f;
            m_WorkflowList.style.marginBottom = 6f;
            m_WorkflowList.selectionChanged += OnWorkflowSelectionChanged;
            m_WorkflowTabContent.Add(m_WorkflowList);

            ScrollView workflowDetailsScroll = BuildWorkflowDetails();
            workflowDetailsScroll.style.flexGrow = 1f;
            workflowDetailsScroll.style.minHeight = 0f;
            m_WorkflowTabContent.Add(workflowDetailsScroll);
            return pane;
        }

        private ScrollView BuildCommandDetails()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.borderTopWidth = 1f;
            scroll.style.borderTopColor = TerminalBorder;
            scroll.style.paddingTop = 7f;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            m_DetailTitleLabel = new Label("请选择一条命令");
            m_DetailTitleLabel.style.flexGrow = 1f;
            m_DetailTitleLabel.style.fontSize = 14f;
            m_DetailTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_DetailTitleLabel.style.color = CommandColor;
            titleRow.Add(m_DetailTitleLabel);

            m_FillCommandButton = new Button(FillSelectedCommandLine)
            {
                text = "填入命令行"
            };
            m_FillCommandButton.style.width = 88f;
            titleRow.Add(m_FillCommandButton);
            scroll.Add(titleRow);

            m_DetailAvailabilityLabel = CreateDetailLabel(true);
            scroll.Add(m_DetailAvailabilityLabel);
            m_DetailDescriptionLabel = CreateDetailLabel(false);
            scroll.Add(m_DetailDescriptionLabel);
            m_DetailUsageLabel = CreateDetailLabel(false);
            scroll.Add(m_DetailUsageLabel);
            m_DetailMetadataLabel = CreateDetailLabel(false);
            scroll.Add(m_DetailMetadataLabel);
            m_DetailDeclarationLabel = CreateDetailLabel(false);
            scroll.Add(m_DetailDeclarationLabel);

            m_DiagnosticsFoldout = new Foldout();
            m_DiagnosticsFoldout.style.marginTop = 10f;
            m_DiagnosticsFoldout.style.borderTopWidth = 1f;
            m_DiagnosticsFoldout.style.borderTopColor = TerminalBorder;
            m_DiagnosticsFoldout.value = false;
            m_DiagnosticsContainer = new VisualElement();
            m_DiagnosticsContainer.style.marginTop = 4f;
            m_DiagnosticsContainer.style.marginLeft = 2f;
            m_DiagnosticsFoldout.Add(m_DiagnosticsContainer);
            scroll.Add(m_DiagnosticsFoldout);
            return scroll;
        }

        private VisualElement MakeCommandSuggestionItem()
        {
            var row = new VisualElement();
            row.style.height = 38f;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;

            var command = new Label
            {
                name = "command"
            };
            command.style.width = 145f;
            command.style.unityFontStyleAndWeight = FontStyle.Bold;
            command.style.color = CommandColor;
            row.Add(command);

            var information = new VisualElement();
            information.style.flexGrow = 1f;
            information.style.minWidth = 0f;

            var displayName = new Label
            {
                name = "displayName"
            };
            displayName.style.height = 19f;
            displayName.style.color = TerminalText;
            information.Add(displayName);

            var description = new Label
            {
                name = "description"
            };
            description.style.height = 17f;
            description.style.color = new Color(0.49f, 0.54f, 0.56f);
            information.Add(description);
            row.Add(information);

            var mode = new Label
            {
                name = "mode"
            };
            mode.style.width = 56f;
            mode.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(mode);

            var state = new Label
            {
                name = "state"
            };
            state.style.width = 18f;
            state.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(state);
            return row;
        }

        private void BindCommandSuggestionItem(VisualElement element, int index)
        {
            XCommandDescriptor command = m_CommandSuggestions[index];
            bool available = XCommandRegistry.CanExecute(command);
            element.Q<Label>("command").text = command.Command;
            element.Q<Label>("displayName").text = command.Name;
            element.Q<Label>("description").text = string.IsNullOrEmpty(command.Description) ? command.Usage : command.Description;
            Label modeLabel = element.Q<Label>("mode");
            modeLabel.text = command.Mode.ToString();
            modeLabel.style.color = GetModeColor(command.Mode);
            Label stateLabel = element.Q<Label>("state");
            stateLabel.text = available ? "●" : "○";
            stateLabel.style.color = available ? PromptColor : new Color(0.75f, 0.42f, 0.32f);
            element.style.opacity = available ? 1f : 0.48f;
            element.tooltip = $"{command.Usage}\n{command.Description}";
        }

        private VisualElement MakeCommandItem()
        {
            var row = new VisualElement();
            row.style.height = 44f;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 3f;
            row.style.paddingRight = 4f;

            var favoriteButton = new Button(() => ToggleFavorite((XCommandDescriptor)row.userData))
            {
                name = "favorite"
            };
            favoriteButton.style.width = 26f;
            favoriteButton.style.height = 22f;
            favoriteButton.style.fontSize = 16f;
            favoriteButton.style.marginRight = 5f;
            row.Add(favoriteButton);

            var names = new VisualElement();
            names.style.flexGrow = 1f;
            names.style.minWidth = 0f;

            var command = new Label
            {
                name = "command"
            };
            command.style.height = 21f;
            command.style.unityFontStyleAndWeight = FontStyle.Bold;
            command.style.color = CommandColor;
            names.Add(command);

            var displayName = new Label
            {
                name = "displayName"
            };
            displayName.style.height = 18f;
            displayName.style.color = new Color(0.65f, 0.68f, 0.70f);
            names.Add(displayName);
            row.Add(names);

            var mode = new Label
            {
                name = "mode"
            };
            mode.style.width = 56f;
            mode.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(mode);

            var state = new Label
            {
                name = "state"
            };
            state.style.width = 18f;
            state.style.unityTextAlign = TextAnchor.MiddleCenter;
            state.style.fontSize = 15f;
            row.Add(state);

            Button quickRunButton = CreateEntryButton("执行", () => QuickRunCommand((XCommandDescriptor)row.userData));
            quickRunButton.name = "quickRun";
            quickRunButton.style.width = 46f;
            quickRunButton.style.flexShrink = 0f;
            row.Add(quickRunButton);
            return row;
        }

        private void BindCommandItem(VisualElement element, int index)
        {
            XCommandDescriptor command = m_FilteredCommands[index];
            bool available = XCommandRegistry.CanExecute(command);
            element.userData = command;
            Button favoriteButton = element.Q<Button>("favorite");
            bool isFavorite = m_Favorites.Contains(command.Command);
            favoriteButton.text = isFavorite ? "★" : "☆";
            favoriteButton.style.color = isFavorite ? FavoriteStarColor : FavoriteStarInactiveColor;
            favoriteButton.tooltip = isFavorite ? "取消收藏" : "收藏命令";
            element.Q<Label>("command").text = command.Command;
            element.Q<Label>("displayName").text = $"{command.Name}  ·  {command.Category}";
            Label modeLabel = element.Q<Label>("mode");
            modeLabel.text = command.Mode.ToString();
            modeLabel.style.color = GetModeColor(command.Mode);
            Label stateLabel = element.Q<Label>("state");
            stateLabel.text = available ? "●" : "○";
            stateLabel.style.color = available ? PromptColor : new Color(0.75f, 0.42f, 0.32f);
            bool requiresArguments = RequiresArguments(command);
            Button quickRunButton = element.Q<Button>("quickRun");
            quickRunButton.text = requiresArguments ? "填入" : "执行";
            quickRunButton.SetEnabled(available);
            quickRunButton.tooltip = available
                ? (requiresArguments ? "该命令需要参数，点击后填入命令栏。" : "点击直接执行该命令。")
                : GetUnavailableReason(command);
            element.style.opacity = available ? 1f : 0.48f;
            element.tooltip = available ? command.Description : GetUnavailableReason(command);
        }

        private void RefreshCommands()
        {
            XCommandRegistry.Refresh();
            RefreshRegistry();
            RefreshWorkflows();
            ApplyReferenceTabState();
        }

        private void RefreshRegistry()
        {
            string selectedCommandName = m_SelectedCommand?.Command ?? m_SelectedCommandName;
            m_AllCommands.Clear();
            for (int i = 0; i < XCommandRegistry.VisibleCommands.Count; i++)
            {
                m_AllCommands.Add(XCommandRegistry.VisibleCommands[i]);
            }

            m_Diagnostics.Clear();
            for (int i = 0; i < XCommandRegistry.Diagnostics.Count; i++)
            {
                m_Diagnostics.Add(XCommandRegistry.Diagnostics[i]);
            }

            m_SelectedCommand = FindCommandByName(selectedCommandName);
            m_SelectedCommandName = m_SelectedCommand?.Command;
            CloseCommandSuggestions(false);
            RefreshCategoryChoices();
            UpdateEnvironmentLabel();
            RefreshCommandView();
            RefreshCommandDetail();
            RefreshDiagnosticsView();
            RequestConsoleRefresh(true);
        }

        private void RefreshCategoryChoices()
        {
            if (m_CategoryFilter == null)
            {
                return;
            }

            string currentValue = m_CategoryFilter.value;
            var categories = new List<string>();
            for (int i = 0; i < m_AllCommands.Count; i++)
            {
                if (!categories.Contains(m_AllCommands[i].Category))
                {
                    categories.Add(m_AllCommands[i].Category);
                }
            }
            categories.Sort(StringComparer.OrdinalIgnoreCase);
            categories.Insert(0, AllCategoryFilter);
            m_CategoryFilter.choices = categories;
            m_CategoryFilter.SetValueWithoutNotify(categories.Contains(currentValue) ? currentValue : AllCategoryFilter);
        }

        private void RefreshCommandView()
        {
            m_FilteredCommands.Clear();
            string search = m_SearchField?.value?.Trim() ?? string.Empty;
            string[] searchTerms = search.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string modeFilter = m_ModeFilter?.value ?? AllModeFilter;
            string categoryFilter = m_CategoryFilter?.value ?? AllCategoryFilter;
            bool favoritesOnly = m_FavoritesOnly;

            for (int i = 0; i < m_AllCommands.Count; i++)
            {
                XCommandDescriptor command = m_AllCommands[i];
                if (!MatchesSearch(command, searchTerms) || !MatchesMode(command, modeFilter) || categoryFilter != AllCategoryFilter && command.Category != categoryFilter || favoritesOnly && !m_Favorites.Contains(command.Command))
                {
                    continue;
                }
                m_FilteredCommands.Add(command);
            }

            if (m_CommandList != null)
            {
                m_CommandList.itemsSource = m_FilteredCommands;
                m_CommandList.Rebuild();
                m_CommandList.selectedIndex = m_SelectedCommand == null ? -1 : m_FilteredCommands.IndexOf(m_SelectedCommand);
            }

            UpdateCommandSummary();

            RefreshExecuteButton();
        }

        private static bool MatchesSearch(XCommandDescriptor command, string[] terms)
        {
            if (terms.Length == 0)
            {
                return true;
            }

            string searchable = $"{command.Command} {command.Name} {command.Category} {command.Description} {command.DeclaringTypeName}";
            for (int i = 0; i < terms.Length; i++)
            {
                if (searchable.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool MatchesMode(XCommandDescriptor command, string modeFilter)
        {
            if (modeFilter == AvailableModeFilter)
            {
                return XCommandRegistry.CanExecute(command);
            }
            if (modeFilter == AllModeFilter)
            {
                return true;
            }
            return command.Mode.ToString() == modeFilter;
        }

        private void OnCommandSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                m_SelectedCommand = item as XCommandDescriptor;
                m_SelectedCommandName = m_SelectedCommand?.Command;
                RefreshCommandDetail();
                return;
            }
        }

        private void OnCommandItemsChosen(IEnumerable<object> chosenItems)
        {
            foreach (object item in chosenItems)
            {
                if (item is XCommandDescriptor command)
                {
                    FillCommandLine(command);
                }
                return;
            }
        }

        private void RefreshCommandDetail()
        {
            if (m_DetailTitleLabel == null)
            {
                return;
            }

            if (m_SelectedCommand == null)
            {
                m_DetailTitleLabel.text = "请选择一条命令";
                m_DetailAvailabilityLabel.text = "单击命令查看完整信息；双击可填入，行内按钮可直接执行或填入参数。";
                m_DetailAvailabilityLabel.style.color = new Color(0.55f, 0.59f, 0.61f);
                m_DetailDescriptionLabel.text = string.Empty;
                m_DetailUsageLabel.text = string.Empty;
                m_DetailMetadataLabel.text = string.Empty;
                m_DetailDeclarationLabel.text = string.Empty;
                m_FillCommandButton.SetEnabled(false);
                return;
            }

            XCommandDescriptor command = m_SelectedCommand;
            bool available = XCommandRegistry.CanExecute(command);
            m_DetailTitleLabel.text = $"{command.Name}  ({command.Command})";
            m_DetailAvailabilityLabel.text = available ? "● 当前环境可执行" : $"○ {GetUnavailableReason(command)}";
            m_DetailAvailabilityLabel.style.color = available ? PromptColor : new Color(0.95f, 0.55f, 0.40f);
            m_DetailDescriptionLabel.text = string.IsNullOrEmpty(command.Description) ? "说明\n<未填写>" : $"说明\n{command.Description}";
            m_DetailUsageLabel.text = $"用法\n{command.Usage}";
            m_DetailMetadataLabel.text = $"分类：{command.Category}\n作用域：{command.Mode}\n参数：{(command.HasArgument ? "string 原始参数" : "无")}\n执行确认：{(command.RequireConfirmation ? "需要" : "不需要")}";
            m_DetailDeclarationLabel.text = $"声明方法\n{command.DeclaringTypeName}.{command.MethodName}\n\n程序集\n{command.AssemblyName}";
            m_FillCommandButton.SetEnabled(true);
        }

        private void FillSelectedCommandLine()
        {
            if (m_SelectedCommand != null)
            {
                FillCommandLine(m_SelectedCommand);
            }
        }

        private void FillCommandLine(XCommandDescriptor command)
        {
            m_CommandLineField.value = command.Command + (command.HasArgument ? " " : string.Empty);
            FocusCommandLine();
        }

        private void ToggleFavorite(XCommandDescriptor command)
        {
            if (m_Favorites.Contains(command.Command))
            {
                m_Favorites.Remove(command.Command);
            }
            else
            {
                m_Favorites.Add(command.Command);
            }
            SaveFavorites();
            RefreshCommandView();
        }

        private void OnCommandLineChanged()
        {
            if (!m_ApplyingCommandHistory)
            {
                m_CommandHistoryIndex = -1;
            }
            CloseCommandSuggestions(false);
            RefreshExecuteButton();
            RefreshPromptHint();
        }

        private void RefreshExecuteButton()
        {
            if (m_ExecuteButton == null)
            {
                return;
            }
            m_ExecuteButton.SetEnabled(!string.IsNullOrWhiteSpace(m_CommandLineField?.value));
        }

        private void RefreshPromptHint()
        {
            if (m_PromptHintLabel == null)
            {
                return;
            }

            string commandLine = m_CommandLineField.value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(commandLine))
            {
                m_PromptHintLabel.text = "输入命令，或从右侧命令手册双击填入。输入前缀后按 Tab 可补全。";
                m_PromptHintLabel.style.color = new Color(0.46f, 0.51f, 0.53f);
                return;
            }

            if (XCommandRegistry.TryGetCommand(commandLine, out XCommandDescriptor command))
            {
                bool available = XCommandRegistry.CanExecute(command);
                if (command.DisplayType == XCommandDisplayType.Hidden)
                {
                    m_PromptHintLabel.text = available ? "按 Enter 执行命令。" : GetUnavailableReason(command);
                    m_PromptHintLabel.style.color = available ? new Color(0.55f, 0.76f, 0.60f) : new Color(0.85f, 0.56f, 0.40f);
                    return;
                }
                m_PromptHintLabel.text = $"{command.Usage}  ·  {command.Mode}  ·  {(available ? "当前可执行" : GetUnavailableReason(command))}";
                m_PromptHintLabel.style.color = available ? new Color(0.55f, 0.76f, 0.60f) : new Color(0.85f, 0.56f, 0.40f);
                SelectCommand(command);
                return;
            }

            m_PromptHintLabel.text = "未匹配到完整命令；按 Tab 查看可补全项。";
            m_PromptHintLabel.style.color = new Color(0.72f, 0.58f, 0.36f);
        }

        private void ExecuteCommandLine()
        {
            ExecuteCommandLine(m_CommandLineField.value);
        }

        private void ExecuteCommandLine(string rawCommandLine)
        {
            string commandLine = rawCommandLine?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(commandLine))
            {
                return;
            }

            CloseCommandSuggestions(false);

            if (XCommandRegistry.TryGetCommand(commandLine, out XCommandDescriptor command) && XCommandRegistry.CanExecute(command) && command.RequireConfirmation && !EditorUtility.DisplayDialog("确认执行命令", $"确定执行以下命令？\n\n{commandLine}", "执行", "取消"))
            {
                return;
            }

            XCommandExecutionResult result = XCommandHub.Execute(commandLine, XCommandSource.Editor);
            if (result.Command != null && result.Command.DisplayType == XCommandDisplayType.Visible)
            {
                SelectCommand(result.Command);
            }
            m_CommandLineField.SetValueWithoutNotify(string.Empty);
            m_CommandHistoryIndex = -1;
            RefreshExecuteButton();
            RefreshPromptHint();
            FocusCommandLine();
        }

        private void OnCommandLineKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                ConsumeCommandLineKeyEvent(evt);
                ExecuteCommandLine();
                FocusCommandLine();
                return;
            }
            if (evt.keyCode == KeyCode.UpArrow)
            {
                ConsumeCommandLineKeyEvent(evt);
                if (m_ShowingCommandSuggestions)
                {
                    CycleCommandSuggestion(-1);
                }
                else
                {
                    NavigateCommandHistory(1);
                }
                return;
            }
            if (evt.keyCode == KeyCode.DownArrow)
            {
                ConsumeCommandLineKeyEvent(evt);
                if (m_ShowingCommandSuggestions)
                {
                    CycleCommandSuggestion(1);
                }
                else
                {
                    NavigateCommandHistory(-1);
                }
                return;
            }
            if (evt.keyCode == KeyCode.Tab)
            {
                ConsumeCommandLineKeyEvent(evt);
                CompleteCommand(evt.shiftKey ? -1 : 1);
                return;
            }
            if (evt.keyCode == KeyCode.Escape)
            {
                ConsumeCommandLineKeyEvent(evt);
                if (m_ShowingCommandSuggestions)
                {
                    CloseCommandSuggestions(true);
                }
                else
                {
                    m_CommandLineField.value = string.Empty;
                }
            }
        }

        private static void ConsumeCommandLineKeyEvent(KeyDownEvent evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private void FocusCommandLine()
        {
            m_CommandLineField.schedule.Execute(() => m_CommandLineField.Focus());
        }

        private void NavigateCommandHistory(int direction)
        {
            IReadOnlyList<string> commandHistory = XCommandHub.CommandHistory;
            if (commandHistory.Count == 0)
            {
                return;
            }

            int nextIndex = Mathf.Clamp(m_CommandHistoryIndex + direction, -1, commandHistory.Count - 1);
            m_CommandHistoryIndex = nextIndex;
            m_ApplyingCommandHistory = true;
            m_CommandLineField.value = nextIndex < 0 ? string.Empty : commandHistory[nextIndex];
            m_ApplyingCommandHistory = false;
        }

        private void CompleteCommand(int direction)
        {
            if (m_ShowingCommandSuggestions)
            {
                CycleCommandSuggestion(direction);
                return;
            }

            string prefix = m_CommandLineField.value?.Trim() ?? string.Empty;
            if (prefix.IndexOf(' ') >= 0)
            {
                return;
            }

            var matches = new List<XCommandDescriptor>();
            for (int i = 0; i < m_AllCommands.Count; i++)
            {
                if (XCommandRegistry.CanExecute(m_AllCommands[i]) && m_AllCommands[i].Command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(m_AllCommands[i]);
                }
            }

            matches.Sort(CompareCommandSuggestions);

            if (matches.Count == 1)
            {
                FillCommandLine(matches[0]);
                SelectCommand(matches[0]);
                return;
            }

            if (matches.Count == 0)
            {
                m_PromptHintLabel.text = "没有可补全的命令。";
                m_PromptHintLabel.style.color = new Color(0.85f, 0.48f, 0.38f);
                return;
            }

            ShowCommandSuggestions(matches, direction < 0 ? matches.Count - 1 : 0);
        }

        private static int CompareCommandSuggestions(XCommandDescriptor left, XCommandDescriptor right)
        {
            bool leftAvailable = XCommandRegistry.CanExecute(left);
            bool rightAvailable = XCommandRegistry.CanExecute(right);
            if (leftAvailable != rightAvailable)
            {
                return leftAvailable ? -1 : 1;
            }
            return string.Compare(left.Command, right.Command, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowCommandSuggestions(List<XCommandDescriptor> suggestions, int selectedIndex)
        {
            m_CommandSuggestions.Clear();
            m_CommandSuggestions.AddRange(suggestions);
            m_CommandSuggestionIndex = selectedIndex;
            m_ShowingCommandSuggestions = true;
            m_SuggestionPanel.style.display = DisplayStyle.Flex;
            m_SuggestionList.style.height = Mathf.Min(m_CommandSuggestions.Count, 6) * 38f + 2f;
            m_SuggestionList.itemsSource = m_CommandSuggestions;
            m_SuggestionList.Rebuild();
            m_SuggestionList.selectedIndex = -1;
            m_SuggestionList.selectedIndex = selectedIndex;
            m_SuggestionList.ScrollToItem(selectedIndex);
        }

        private void CycleCommandSuggestion(int direction)
        {
            int count = m_CommandSuggestions.Count;
            m_CommandSuggestionIndex = (m_CommandSuggestionIndex + direction + count) % count;
            m_SuggestionList.selectedIndex = m_CommandSuggestionIndex;
            m_SuggestionList.ScrollToItem(m_CommandSuggestionIndex);
        }

        private void OnCommandSuggestionSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                XCommandDescriptor command = (XCommandDescriptor)item;
                m_CommandSuggestionIndex = m_CommandSuggestions.IndexOf(command);
                ApplyCommandSuggestion(command);
                return;
            }
        }

        private void OnCommandSuggestionChosen(IEnumerable<object> chosenItems)
        {
            foreach (object item in chosenItems)
            {
                ApplyCommandSuggestion((XCommandDescriptor)item);
                CloseCommandSuggestions(true);
                FocusCommandLine();
                return;
            }
        }

        private void ApplyCommandSuggestion(XCommandDescriptor command)
        {
            m_CommandLineField.SetValueWithoutNotify(command.Command + (command.HasArgument ? " " : string.Empty));
            string executionHint = command.HasArgument ? "输入参数后 Enter 执行" : "Enter 执行";
            m_SuggestionSummaryLabel.text = $"{m_CommandSuggestionIndex + 1}/{m_CommandSuggestions.Count}  ·  Tab/Shift+Tab 或 ↑↓ 切换  ·  {executionHint}";
            m_PromptHintLabel.text = $"{command.Usage}  ·  {command.Name}  ·  {(XCommandRegistry.CanExecute(command) ? "当前可执行" : GetUnavailableReason(command))}";
            m_PromptHintLabel.style.color = XCommandRegistry.CanExecute(command) ? new Color(0.55f, 0.76f, 0.60f) : new Color(0.85f, 0.56f, 0.40f);
            RefreshExecuteButton();
            SelectCommand(command);
        }

        private void CloseCommandSuggestions(bool refreshPromptHint)
        {
            if (!m_ShowingCommandSuggestions)
            {
                return;
            }

            m_ShowingCommandSuggestions = false;
            m_CommandSuggestionIndex = -1;
            m_CommandSuggestions.Clear();
            m_SuggestionPanel.style.display = DisplayStyle.None;
            if (refreshPromptHint)
            {
                RefreshPromptHint();
            }
        }

        private void SelectCommand(XCommandDescriptor command)
        {
            if (command.DisplayType == XCommandDisplayType.Hidden)
            {
                return;
            }
            m_SelectedCommand = command;
            m_SelectedCommandName = command.Command;
            RefreshCommandDetail();
            if (m_CommandList != null)
            {
                m_CommandList.selectedIndex = m_FilteredCommands.IndexOf(command);
            }
        }

        private VisualElement CreateWelcomeMessage()
        {
            var welcome = new VisualElement();
            welcome.style.paddingLeft = 6f;
            welcome.style.paddingTop = 8f;

            var ready = new Label("[system] XCommand command terminal ready.");
            ready.style.unityFontStyleAndWeight = FontStyle.Bold;
            ready.style.color = PromptColor;
            welcome.Add(ready);

            var description = new Label("在下方提示符输入完整命令行，或使用右侧 Command Reference 的快捷按钮执行/填入。\n当前窗口默认只接收新命令；点击“加载历史”可查看共享命令记录。 ");
            description.style.marginTop = 6f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = TerminalText;
            welcome.Add(description);
            return welcome;
        }

        private void ScrollConsoleToBottom()
        {
            consoleScrollSchedule?.Pause();
            if (!consoleScrollPending || m_AutoScrollToggle == null || !m_AutoScrollToggle.value || displayedCommandRecords.Count == 0)
            {
                consoleScrollPending = false;
                return;
            }
            if (consoleList == null || consoleList.panel == null || !(consoleList.layout.height > 0f))
            {
                return;
            }
            consoleScrollPending = false;
            // -1 表示末尾，动态高度列表会在后续测量完成时继续校正到底部。
            consoleList.ScrollToItem(-1);
        }

        private void CopyAllConsoleOutput()
        {
            StringBuilder builder = new StringBuilder();
            int limit = MAX_COPY_ALL_TEXT_LENGTH - COPY_ALL_TRUNCATION_NOTICE.Length;
            for (int i = 0; i < loadedCommandRecords.Count; i++)
            {
                XCommandRecord record = loadedCommandRecords[i];
                if (ShouldDisplayCommandRecord(record) && !AppendConsoleRecordForCopy(builder, record, limit))
                {
                    builder.Append(COPY_ALL_TRUNCATION_NOTICE);
                    break;
                }
            }
            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private void ClearConsoleOutput()
        {
            ResetVisibleCommandRecords();
            RequestConsoleRefresh(true);
            FocusCommandLine();
        }

        private void LoadCommandHistory()
        {
            CancelPendingConsoleWork();
            loadedCommandRecords.Clear();
            loadedCommandRecordIds.Clear();
            IReadOnlyList<XCommandRecord> records = XCommandHub.Records;
            for (int i = 0; i < records.Count; i++)
            {
                XCommandRecord record = records[i];
                if (loadedCommandRecordIds.Add(record.Id))
                {
                    loadedCommandRecords.Add(record);
                }
            }
            loadedCommandRecords.Sort(CompareConsoleRecords);
            m_VisibleAfterRecordId = loadedCommandRecords.Count == 0 ? XCommandHub.LatestRecordId : loadedCommandRecords[0].Id - 1;
            RequestConsoleRefresh(true);
            FocusCommandLine();
        }

        private void ResetVisibleCommandRecords()
        {
            CancelPendingConsoleWork();
            m_VisibleAfterRecordId = XCommandHub.LatestRecordId;
            loadedCommandRecords.Clear();
            loadedCommandRecordIds.Clear();
        }

        private void RestoreVisibleCommandRecords()
        {
            loadedCommandRecords.Clear();
            loadedCommandRecordIds.Clear();
            IReadOnlyList<XCommandRecord> records = XCommandHub.Records;
            if (records.Count == 0 || m_VisibleAfterRecordId > XCommandHub.LatestRecordId)
            {
                m_VisibleAfterRecordId = 0;
            }
            for (int i = 0; i < records.Count; i++)
            {
                XCommandRecord record = records[i];
                if (record.Id > m_VisibleAfterRecordId && loadedCommandRecordIds.Add(record.Id))
                {
                    loadedCommandRecords.Add(record);
                }
            }
        }

        private void SubscribeCommandHub()
        {
            if (m_CommandHubSubscribed)
            {
                return;
            }
            XCommandHub.RecordAdded += OnCommandRecordAdded;
            XCommandHub.RecordUpdated += OnCommandRecordUpdated;
            XCommandHub.RecordsTrimmed += OnCommandRecordTrimmed;
            XCommandHub.DisplayClearRequested += OnDisplayClearRequested;
            XCommandHub.DisplayHistoryRequested += OnDisplayHistoryRequested;
            m_CommandHubSubscribed = true;
        }

        private void UnsubscribeCommandHub()
        {
            if (!m_CommandHubSubscribed)
            {
                return;
            }
            XCommandHub.RecordAdded -= OnCommandRecordAdded;
            XCommandHub.RecordUpdated -= OnCommandRecordUpdated;
            XCommandHub.RecordsTrimmed -= OnCommandRecordTrimmed;
            XCommandHub.DisplayClearRequested -= OnDisplayClearRequested;
            XCommandHub.DisplayHistoryRequested -= OnDisplayHistoryRequested;
            m_CommandHubSubscribed = false;
        }

        private void OnDisplayClearRequested(XCommandSource source)
        {
            if (source == XCommandSource.Editor)
            {
                ClearConsoleOutput();
            }
        }

        private void OnDisplayHistoryRequested(XCommandSource source)
        {
            if (source == XCommandSource.Editor)
            {
                LoadCommandHistory();
            }
        }

        private void OnCommandRecordAdded(XCommandRecord record)
        {
            m_CommandHistoryIndex = -1;
            if (record.Id <= m_VisibleAfterRecordId || !loadedCommandRecordIds.Add(record.Id))
            {
                return;
            }
            loadedCommandRecords.Add(record);
            RequestConsoleRefresh(true);
        }

        private void OnCommandRecordUpdated(XCommandRecord record)
        {
            if (!loadedCommandRecordIds.Contains(record.Id))
            {
                return;
            }
            pendingConsoleRecords[record.Id] = record;
            RequestConsoleRefresh(false);
        }

        private void OnCommandRecordTrimmed(long recordId)
        {
            if (loadedCommandRecordIds.Remove(recordId))
            {
                for (int i = 0; i < loadedCommandRecords.Count; i++)
                {
                    if (loadedCommandRecords[i].Id == recordId)
                    {
                        loadedCommandRecords.RemoveAt(i);
                        break;
                    }
                }
                pendingConsoleRecords.Remove(recordId);
                RequestConsoleRefresh(true);
            }
        }

        private void RefreshTerminalSummary()
        {
            if (m_TerminalSummaryLabel != null)
            {
                m_TerminalSummaryLabel.text = $"visible {displayedCommandRecords.Count}  ·  retained {XCommandHub.Records.Count}/{XCommandHub.RecordLimit}  ·  diagnostics {m_Diagnostics.Count}";
            }
        }

        private bool ShouldDisplayCommandRecord(XCommandRecord record)
        {
            if (m_ShowHiddenCommandRecords)
            {
                return true;
            }

            XCommandDescriptor command = record.Command;
            if (command == null && !XCommandRegistry.TryGetCommand(record.CommandLine, out command))
            {
                return true;
            }
            return command.DisplayType == XCommandDisplayType.Visible;
        }

        private void RefreshDiagnosticsView()
        {
            if (m_DiagnosticsFoldout == null)
            {
                return;
            }

            m_DiagnosticsFoldout.text = $"注册诊断 ({m_Diagnostics.Count})";
            m_DiagnosticsContainer.Clear();
            if (m_Diagnostics.Count == 0)
            {
                Label noIssues = CreateDiagnosticText("未发现重复命令或非法方法签名。", new Color(0.52f, 0.70f, 0.56f));
                m_DiagnosticsContainer.Add(noIssues);
                return;
            }

            for (int i = 0; i < m_Diagnostics.Count; i++)
            {
                XCommandDiagnostic diagnostic = m_Diagnostics[i];
                string declaration = $"{diagnostic.DeclaringTypeName}.{diagnostic.MethodName}";
                Label label = CreateDiagnosticText($"{diagnostic.Command}\n{declaration}\n{diagnostic.Message}", new Color(0.92f, 0.57f, 0.42f));
                label.style.marginBottom = 7f;
                m_DiagnosticsContainer.Add(label);
            }
        }

        private void UpdateEnvironmentLabel()
        {
            if (m_EnvironmentLabel == null)
            {
                return;
            }
            bool runtime = Application.isPlaying;
            m_EnvironmentLabel.text = runtime ? "● Runtime" : "● Edit Mode";
            m_EnvironmentLabel.style.color = runtime ? new Color(0.35f, 0.85f, 1f) : PromptColor;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            EditorApplication.delayCall += RefreshEnvironmentState;
        }

        private void RefreshEnvironmentState()
        {
            if (this == null)
            {
                return;
            }
            UpdateEnvironmentLabel();
            RefreshCommandView();
            RefreshCommandDetail();
            CloseCommandSuggestions(false);
            RefreshPromptHint();
        }

        private XCommandDescriptor FindCommandByName(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                return null;
            }
            for (int i = 0; i < m_AllCommands.Count; i++)
            {
                if (m_AllCommands[i].Command == commandName)
                {
                    return m_AllCommands[i];
                }
            }
            return null;
        }

        private static string GetUnavailableReason(XCommandDescriptor command)
        {
            return command.Mode == XCommandMode.Runtime ? "仅能在 Runtime 执行" : "仅能在 Edit Mode 执行";
        }

        private static string FormatStatus(XCommandExecutionStatus status)
        {
            switch (status)
            {
                case XCommandExecutionStatus.Succeeded: return "成功";
                case XCommandExecutionStatus.NotFound: return "未找到";
                case XCommandExecutionStatus.Unavailable: return "不可用";
                default: return "失败";
            }
        }

        private static string FormatRecordStatus(XCommandRecord record)
        {
            return record.State == XCommandRecordState.Running ? "执行中" : FormatStatus(record.Status);
        }

        private static string FormatSource(XCommandSource source)
        {
            switch (source)
            {
                case XCommandSource.Api: return "API";
                case XCommandSource.Ugui: return "UGUI";
                case XCommandSource.Cli: return "CLI";
                default: return source.ToString();
            }
        }

        private static Color GetStatusColor(XCommandExecutionStatus status)
        {
            switch (status)
            {
                case XCommandExecutionStatus.Succeeded: return PromptColor;
                case XCommandExecutionStatus.Unavailable: return new Color(0.95f, 0.70f, 0.30f);
                case XCommandExecutionStatus.NotFound: return new Color(0.95f, 0.55f, 0.40f);
                default: return new Color(1f, 0.35f, 0.35f);
            }
        }

        private static Color GetModeColor(XCommandMode mode)
        {
            switch (mode)
            {
                case XCommandMode.Runtime: return new Color(0.36f, 0.78f, 0.94f);
                case XCommandMode.Editor: return new Color(0.65f, 0.84f, 0.47f);
                default: return new Color(0.80f, 0.66f, 0.95f);
            }
        }

        private static string FormatCommandRecordOutput(XCommandRecord record)
        {
            if (record.State == XCommandRecordState.Running)
            {
                return "执行中...";
            }
            if (record.Succeeded)
            {
                return record.Output;
            }
            return string.IsNullOrEmpty(record.Exception) ? record.Message : $"{record.Message}\n\n{record.Exception}";
        }

        private static Button CreateToolbarButton(string text, Action callback, float width)
        {
            var button = new Button(callback)
            {
                text = text
            };
            button.style.width = width;
            button.style.height = 23f;
            button.style.marginLeft = 4f;
            return button;
        }

        private static Button CreateEntryButton(string text, Action callback)
        {
            var button = new Button(callback)
            {
                text = text
            };
            button.style.height = 19f;
            button.style.marginLeft = 4f;
            button.style.paddingLeft = 5f;
            button.style.paddingRight = 5f;
            return button;
        }

        private static Label CreateDetailLabel(bool emphasized)
        {
            var label = new Label();
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 6f;
            label.style.color = new Color(0.75f, 0.78f, 0.80f);
            if (emphasized)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            return label;
        }

        private static Label CreateDiagnosticText(string text, Color color)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = color;
            return label;
        }

        private void LoadPreferences()
        {
            m_Favorites.Clear();
            StringListStorage favoriteStorage = LoadStringList(GetPreferencesKey("Favorites"));
            for (int i = 0; i < favoriteStorage.values.Count; i++)
            {
                m_Favorites.Add(favoriteStorage.values[i]);
            }
        }

        private void SavePreferences()
        {
            SaveFavorites();
        }

        private void SaveFavorites()
        {
            var storage = new StringListStorage();
            storage.values.AddRange(m_Favorites);
            storage.values.Sort(StringComparer.Ordinal);
            SaveStringList(GetPreferencesKey("Favorites"), storage);
        }

        private static StringListStorage LoadStringList(string key)
        {
            string json = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new StringListStorage();
            }

            StringListStorage storage = JsonUtility.FromJson<StringListStorage>(json);
            return storage == null || storage.values == null ? new StringListStorage() : storage;
        }

        private static void SaveStringList(string key, StringListStorage storage)
        {
            EditorPrefs.SetString(key, JsonUtility.ToJson(storage));
        }

        private static string GetPreferencesKey(string suffix)
        {
            return $"XFramework.XCommand.{Hash128.Compute(Application.dataPath)}.{suffix}";
        }

        private ScrollView BuildWorkflowDetails()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.borderTopWidth = 1f;
            scroll.style.borderTopColor = TerminalBorder;
            scroll.style.paddingTop = 7f;

            m_WorkflowDetailTitleLabel = new Label("请选择一个 Workflow");
            m_WorkflowDetailTitleLabel.style.fontSize = 14f;
            m_WorkflowDetailTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_WorkflowDetailTitleLabel.style.color = CommandColor;
            scroll.Add(m_WorkflowDetailTitleLabel);

            m_WorkflowDetailPathLabel = CreateDetailLabel(false);
            scroll.Add(m_WorkflowDetailPathLabel);

            m_WorkflowDetailContentLabel = CreateDetailLabel(false);
            scroll.Add(m_WorkflowDetailContentLabel);
            return scroll;
        }

        private VisualElement MakeWorkflowItem()
        {
            var row = new VisualElement();
            row.style.height = 44f;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 4f;

            var name = new Label
            {
                name = "workflowName"
            };
            name.style.flexGrow = 1f;
            name.style.minWidth = 0f;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = CommandColor;
            row.Add(name);
            return row;
        }

        private void BindWorkflowItem(VisualElement element, int index)
        {
            XWorkflowEntry entry = m_Workflows[index];
            element.Q<Label>("workflowName").text = entry.Name;
            element.tooltip = entry.RelativeDirectoryPath;
        }

        private void SetActiveReferenceTab(int index)
        {
            m_ActiveReferenceTab = index == 1 ? 1 : 0;
            if (m_ActiveReferenceTab == 1)
            {
                RefreshWorkflows();
            }
            ApplyReferenceTabState();
        }

        private void ApplyReferenceTabState()
        {
            bool commandsActive = m_ActiveReferenceTab != 1;
            if (m_CommandTabContent != null)
            {
                m_CommandTabContent.style.display = commandsActive ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (m_WorkflowTabContent != null)
            {
                m_WorkflowTabContent.style.display = commandsActive ? DisplayStyle.None : DisplayStyle.Flex;
            }
            ApplyReferenceTabButtonStyle(m_CommandTabButton, commandsActive);
            ApplyReferenceTabButtonStyle(m_WorkflowTabButton, !commandsActive);
            ApplyReferenceSummary();
        }

        private void UpdateCommandSummary()
        {
            int availableCount = 0;
            int favoriteCount = 0;
            for (int i = 0; i < m_AllCommands.Count; i++)
            {
                if (XCommandRegistry.CanExecute(m_AllCommands[i]))
                {
                    availableCount++;
                }
                if (m_Favorites.Contains(m_AllCommands[i].Command))
                {
                    favoriteCount++;
                }
            }
            m_CommandSummaryText = $"{m_FilteredCommands.Count}/{m_AllCommands.Count}  ·  可用 {availableCount}  ·  ★ {favoriteCount}";
            ApplyReferenceSummary();
        }

        private void ApplyReferenceSummary()
        {
            if (m_CommandSummaryLabel == null)
            {
                return;
            }
            m_CommandSummaryLabel.text = m_ActiveReferenceTab == 1 ? $"{m_Workflows.Count} 个 Workflow" : m_CommandSummaryText;
        }

        private void RefreshWorkflows()
        {
            string selectedName = m_SelectedWorkflow?.Name ?? m_SelectedWorkflowName;
            m_Workflows.Clear();
            m_Workflows.AddRange(XWorkflowCatalog.Discover(out string message));
            m_WorkflowScanMessage = message;
            m_SelectedWorkflow = FindWorkflowByName(selectedName);
            m_SelectedWorkflowName = m_SelectedWorkflow?.Name;
            RefreshWorkflowList();
            RefreshWorkflowDetail();
        }

        private void RefreshWorkflowList()
        {
            if (m_WorkflowList != null)
            {
                m_WorkflowList.itemsSource = m_Workflows;
                m_WorkflowList.Rebuild();
                m_WorkflowList.selectedIndex = m_SelectedWorkflow == null ? -1 : m_Workflows.IndexOf(m_SelectedWorkflow);
            }
            ApplyReferenceSummary();
        }

        private void OnWorkflowSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                m_SelectedWorkflow = item as XWorkflowEntry;
                m_SelectedWorkflowName = m_SelectedWorkflow?.Name;
                RefreshWorkflowDetail();
                return;
            }
        }

        private void RefreshWorkflowDetail()
        {
            if (m_WorkflowDetailTitleLabel == null)
            {
                return;
            }

            if (m_SelectedWorkflow == null)
            {
                m_WorkflowDetailTitleLabel.text = "请选择一个 Workflow";
                m_WorkflowDetailPathLabel.text = m_WorkflowScanMessage;
                m_WorkflowDetailPathLabel.style.color = new Color(0.55f, 0.59f, 0.61f);
                m_WorkflowDetailContentLabel.text = string.Empty;
                return;
            }

            m_WorkflowDetailTitleLabel.text = m_SelectedWorkflow.Name;
            m_WorkflowDetailPathLabel.text = m_SelectedWorkflow.RelativeDirectoryPath;
            m_WorkflowDetailPathLabel.style.color = new Color(0.55f, 0.59f, 0.61f);

            string error;
            string content = XWorkflowCatalog.ReadReadme(m_SelectedWorkflow, out error);
            if (!string.IsNullOrEmpty(error))
            {
                m_WorkflowDetailContentLabel.text = error;
                m_WorkflowDetailContentLabel.style.color = new Color(0.95f, 0.55f, 0.40f);
                return;
            }

            m_WorkflowDetailContentLabel.text = content;
            m_WorkflowDetailContentLabel.style.color = new Color(0.75f, 0.78f, 0.80f);
        }

        private XWorkflowEntry FindWorkflowByName(string workflowName)
        {
            if (string.IsNullOrEmpty(workflowName))
            {
                return null;
            }
            for (int i = 0; i < m_Workflows.Count; i++)
            {
                if (m_Workflows[i].Name == workflowName)
                {
                    return m_Workflows[i];
                }
            }
            return null;
        }

        private static Button CreateReferenceTabButton(string text, Action callback)
        {
            var button = new Button(callback)
            {
                text = text
            };
            button.style.height = 24f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderBottomWidth = 2f;
            return button;
        }

        private static void ApplyReferenceTabButtonStyle(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }
            button.style.color = active ? CommandColor : new Color(0.55f, 0.59f, 0.61f);
            button.style.borderBottomColor = active ? CommandColor : Color.clear;
        }

        private void ToggleFavoritesOnly()
        {
            m_FavoritesOnly = !m_FavoritesOnly;
            m_FavoritesOnlyButton.text = m_FavoritesOnly ? "[√] 仅收藏" : "[ ] 仅收藏";
            m_FavoritesOnlyButton.style.color = m_FavoritesOnly ? FavoriteStarColor : TerminalText;
            RefreshCommandView();
        }

        private void QuickRunCommand(XCommandDescriptor command)
        {
            if (command == null || !XCommandRegistry.CanExecute(command))
            {
                return;
            }

            SelectCommand(command);
            if (RequiresArguments(command))
            {
                FillCommandLine(command);
                return;
            }

            ExecuteCommandLine(command.Command);
        }

        private static bool RequiresArguments(XCommandDescriptor command)
        {
            if (command == null || !command.HasArgument)
            {
                return false;
            }

            string usage = command.Usage ?? string.Empty;
            if (usage.StartsWith(command.Command, StringComparison.OrdinalIgnoreCase))
            {
                usage = usage.Substring(command.Command.Length);
            }

            var requiredText = new StringBuilder();
            int bracketDepth = 0;
            for (int i = 0; i < usage.Length; i++)
            {
                char character = usage[i];
                if (character == '[')
                {
                    bracketDepth++;
                    continue;
                }
                if (character == ']' && bracketDepth > 0)
                {
                    bracketDepth--;
                    continue;
                }
                if (bracketDepth == 0)
                {
                    requiredText.Append(character);
                }
            }

            string remaining = requiredText.ToString().Trim();
            return remaining.Trim('[', ']', '(', ')', '|', ',', ';', ' ').Length > 0;
        }

        private VisualElement BuildConsoleContent()
        {
            VisualElement container = new VisualElement();
            container.style.flexGrow = 1f;
            container.style.flexBasis = 0f;
            container.style.minHeight = 0f;
            container.style.minWidth = 0f;
            container.style.paddingLeft = 10f;
            container.style.paddingRight = 10f;
            container.style.paddingTop = 8f;
            container.style.paddingBottom = 8f;

            consoleList = new ListView
            {
                name = "command-console",
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                fixedItemHeight = 80f,
                selectionType = SelectionType.None,
                showBoundCollectionSize = false,
                makeItem = MakeConsoleEntry,
                bindItem = BindConsoleEntry,
                unbindItem = UnbindConsoleEntry,
                itemsSource = displayedCommandRecords
            };
            consoleList.style.flexGrow = 1f;
            consoleList.style.flexBasis = 0f;
            consoleList.style.minHeight = 0f;
            consoleList.style.minWidth = 0f;
            consoleList.style.display = DisplayStyle.None;
            consoleList.RegisterCallback<GeometryChangedEvent>(OnConsoleGeometryChanged);
            container.Add(consoleList);

            consoleEmptyState = CreateWelcomeMessage();
            consoleEmptyState.style.flexGrow = 1f;
            container.Add(consoleEmptyState);

            // 使用窗口根节点调度；空列表隐藏时仍需处理新记录。
            consoleRefreshSchedule = rootVisualElement.schedule.Execute(FlushConsoleUpdates);
            consoleRefreshSchedule.Pause();
            consoleScrollSchedule = rootVisualElement.schedule.Execute(ScrollConsoleToBottom);
            consoleScrollSchedule.Pause();
            return container;
        }

        private void RequestConsoleRefresh(bool structureChanged)
        {
            consoleScrollSchedule?.Pause();
            consoleScrollPending = false;
            consoleStructureDirty |= structureChanged;
            if (consoleRefreshSchedule != null && !consoleRefreshSchedule.isActive)
            {
                consoleRefreshSchedule.ExecuteLater(0);
            }
        }

        private void FlushConsoleUpdates()
        {
            consoleRefreshSchedule?.Pause();
            if (consoleList == null)
            {
                return;
            }

            bool refreshItems = consoleStructureDirty || HasConsoleVisibilityChanges();
            consoleStructureDirty = false;
            if (refreshItems)
            {
                RebuildDisplayedCommandRecords();
            }
            bool hasRecords = displayedCommandRecords.Count > 0;
            consoleList.style.display = hasRecords ? DisplayStyle.Flex : DisplayStyle.None;
            consoleEmptyState.style.display = hasRecords ? DisplayStyle.None : DisplayStyle.Flex;

            bool visibleContentChanged = refreshItems;
            if (refreshItems)
            {
                consoleList.RefreshItems();
            }
            else
            {
                foreach (long recordId in pendingConsoleRecords.Keys)
                {
                    if (displayedRecordIndices.TryGetValue(recordId, out int index))
                    {
                        consoleList.RefreshItem(index);
                        visibleContentChanged = true;
                    }
                }
            }
            pendingConsoleRecords.Clear();
            RefreshTerminalSummary();
            if (hasRecords && visibleContentChanged)
            {
                RequestConsoleScroll();
            }
        }

        private bool HasConsoleVisibilityChanges()
        {
            foreach (XCommandRecord record in pendingConsoleRecords.Values)
            {
                if (ShouldDisplayCommandRecord(record) != displayedRecordIndices.ContainsKey(record.Id))
                {
                    return true;
                }
            }
            return false;
        }

        private void RebuildDisplayedCommandRecords()
        {
            displayedCommandRecords.Clear();
            displayedRecordIndices.Clear();
            for (int i = 0; i < loadedCommandRecords.Count; i++)
            {
                XCommandRecord record = loadedCommandRecords[i];
                if (ShouldDisplayCommandRecord(record))
                {
                    displayedRecordIndices.Add(record.Id, displayedCommandRecords.Count);
                    displayedCommandRecords.Add(record);
                }
            }
        }

        private void CancelPendingConsoleWork()
        {
            consoleRefreshSchedule?.Pause();
            consoleScrollSchedule?.Pause();
            consoleScrollPending = false;
            consoleStructureDirty = false;
            pendingConsoleRecords.Clear();
        }

        private void StopConsoleUpdates()
        {
            CancelPendingConsoleWork();
            consoleRefreshSchedule = null;
            consoleScrollSchedule = null;
            if (consoleList != null)
            {
                consoleList.UnregisterCallback<GeometryChangedEvent>(OnConsoleGeometryChanged);
            }
            if (m_AutoScrollToggle != null)
            {
                m_AutoScrollToggle.UnregisterValueChangedCallback(OnConsoleAutoScrollChanged);
            }
        }

        private VisualElement MakeConsoleEntry()
        {
            return new ConsoleEntryView(this);
        }

        private void BindConsoleEntry(VisualElement element, int index)
        {
            ConsoleEntryView view = (ConsoleEntryView)element;
            if (index < 0 || index >= displayedCommandRecords.Count)
            {
                view.Unbind();
                return;
            }
            view.Bind(displayedCommandRecords[index]);
        }

        private static void UnbindConsoleEntry(VisualElement element, int index)
        {
            ((ConsoleEntryView)element).Unbind();
        }

        private void RequestConsoleScroll()
        {
            if (m_AutoScrollToggle == null || !m_AutoScrollToggle.value || consoleScrollSchedule == null || displayedCommandRecords.Count == 0)
            {
                return;
            }
            consoleScrollPending = true;
            if (!consoleScrollSchedule.isActive)
            {
                consoleScrollSchedule.ExecuteLater(0);
            }
        }

        private void OnConsoleGeometryChanged(GeometryChangedEvent evt)
        {
            if (consoleScrollPending && evt.newRect.height > 0f)
            {
                RequestConsoleScroll();
            }
        }

        private void OnConsoleAutoScrollChanged(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
            {
                RequestConsoleScroll();
            }
            else
            {
                consoleScrollPending = false;
                consoleScrollSchedule?.Pause();
            }
        }

        private static bool AppendConsoleRecordForCopy(StringBuilder builder, XCommandRecord record, int limit)
        {
            string duration = record.State == XCommandRecordState.Completed ? $" · {record.DurationMilliseconds:F2} ms" : string.Empty;
            string header = $"[{record.ExecutedAtUtc.ToLocalTime():HH:mm:ss}] {FormatSource(record.Source)} · {FormatRecordStatus(record)}{duration}";
            GetConsoleOutputParts(record, out string first, out string separator, out string last);
            return AppendCopyText(builder, header, limit)
                && AppendCopyText(builder, Environment.NewLine, limit)
                && AppendCopyText(builder, "> ", limit)
                && AppendCopyText(builder, record.CommandLine, limit)
                && AppendCopyText(builder, Environment.NewLine, limit)
                && AppendCopyText(builder, first, limit)
                && AppendCopyText(builder, separator, limit)
                && AppendCopyText(builder, last, limit)
                && AppendCopyText(builder, Environment.NewLine, limit)
                && AppendCopyText(builder, Environment.NewLine, limit);
        }

        private static bool AppendCopyText(StringBuilder builder, string text, int limit)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            int length = Math.Min(text.Length, limit - builder.Length);
            if (length < text.Length && length > 0)
            {
                if (char.IsHighSurrogate(text[length - 1]) || text[length - 1] == '\r')
                {
                    length--;
                }
            }
            builder.Append(text, 0, length);
            return length == text.Length;
        }

        private static void GetConsoleOutputParts(XCommandRecord record, out string first, out string separator, out string last)
        {
            separator = string.Empty;
            last = string.Empty;
            if (record.State == XCommandRecordState.Running)
            {
                first = "执行中...";
            }
            else if (record.Succeeded)
            {
                first = record.Output ?? string.Empty;
            }
            else
            {
                first = record.Message ?? string.Empty;
                if (!string.IsNullOrEmpty(record.Exception))
                {
                    separator = "\n\n";
                    last = record.Exception;
                }
            }
        }

        private static string BuildConsolePreview(string first, string separator, string last, out long originalLength)
        {
            originalLength = (long)(first?.Length ?? 0) + (separator?.Length ?? 0) + (last?.Length ?? 0);
            StringBuilder builder = new StringBuilder((int)Math.Min(MAX_CONSOLE_TEXT_LENGTH, originalLength));
            int lineCount = 1;
            bool previousCarriageReturn = false;
            if (AppendConsolePreviewPart(builder, first, ref lineCount, ref previousCarriageReturn)
                && AppendConsolePreviewPart(builder, separator, ref lineCount, ref previousCarriageReturn))
            {
                AppendConsolePreviewPart(builder, last, ref lineCount, ref previousCarriageReturn);
            }
            if (builder.Length < originalLength && builder.Length > 0)
            {
                char finalCharacter = builder[builder.Length - 1];
                if (char.IsHighSurrogate(finalCharacter) || finalCharacter == '\r')
                {
                    builder.Length--;
                }
            }
            return builder.ToString();
        }

        private static bool AppendConsolePreviewPart(StringBuilder builder, string text, ref int lineCount, ref bool previousCarriageReturn)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            int limit = Math.Min(text.Length, MAX_CONSOLE_TEXT_LENGTH - builder.Length);
            int length = 0;
            while (length < limit)
            {
                char character = text[length];
                bool startsLine = character == '\r' || character == '\n' && !previousCarriageReturn;
                if (startsLine)
                {
                    if (lineCount == MAX_CONSOLE_TEXT_LINES)
                    {
                        break;
                    }
                    lineCount++;
                }
                previousCarriageReturn = character == '\r';
                length++;
            }
            builder.Append(text, 0, length);
            return length == text.Length;
        }

        private static int CompareConsoleRecords(XCommandRecord left, XCommandRecord right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private sealed class ConsoleEntryView : VisualElement
        {
            private readonly XCommandEditorWindow owner;
            private readonly VisualElement card;
            private readonly Label status;
            private readonly Label command;
            private readonly Label commandNotice;
            private readonly Label output;
            private readonly Label outputNotice;
            private XCommandRecord record;

            public ConsoleEntryView(XCommandEditorWindow owner)
            {
                this.owner = owner;
                // 行间距放入条目自身高度，避免动态高度虚拟化遗漏外边距。
                style.paddingBottom = 8f;
                style.flexShrink = 0f;
                style.minWidth = 0f;
                card = new VisualElement();
                card.style.paddingLeft = 9f;
                card.style.paddingRight = 7f;
                card.style.paddingTop = 6f;
                card.style.paddingBottom = 7f;
                card.style.backgroundColor = new Color(0.075f, 0.08f, 0.085f);
                card.style.borderLeftWidth = 2f;
                Add(card);

                VisualElement header = new VisualElement();
                header.style.height = 22f;
                header.style.flexShrink = 0f;
                header.style.flexDirection = FlexDirection.Row;
                header.style.alignItems = Align.Center;
                status = new Label { enableRichText = false };
                status.style.flexGrow = 1f;
                status.style.minWidth = 0f;
                status.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.Add(status);
                header.Add(CreateEntryButton("重跑", Rerun));
                header.Add(CreateEntryButton("复制命令", CopyCommand));
                header.Add(CreateEntryButton("复制输出", CopyOutput));
                card.Add(header);

                command = AddTextLabel(3f, CommandColor);
                command.style.unityFontStyleAndWeight = FontStyle.Bold;
                commandNotice = AddNoticeLabel();
                output = AddTextLabel(5f, TerminalText);
                outputNotice = AddNoticeLabel();
            }

            private Label AddTextLabel(float marginTop, Color color)
            {
                Label label = new Label { enableRichText = false };
                label.style.marginTop = marginTop;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.color = color;
                card.Add(label);
                return label;
            }

            private Label AddNoticeLabel()
            {
                Label label = AddTextLabel(3f, new Color(0.95f, 0.70f, 0.30f));
                label.style.display = DisplayStyle.None;
                return label;
            }

            public void Bind(XCommandRecord value)
            {
                record = value;
                Color statusColor = record.State == XCommandRecordState.Running ? CommandColor : GetStatusColor(record.Status);
                string duration = record.State == XCommandRecordState.Running ? "执行中" : $"{record.DurationMilliseconds:F2} ms";
                card.style.borderLeftColor = statusColor;
                status.text = $"[{record.ExecutedAtUtc.ToLocalTime():HH:mm:ss}]  {FormatSource(record.Source)}  ·  {FormatRecordStatus(record)}  ·  {duration}";
                status.style.color = statusColor;
                BindPreview(command, commandNotice, record.CommandLine, string.Empty, string.Empty, "> ", "复制命令");
                GetConsoleOutputParts(record, out string first, out string separator, out string last);
                BindPreview(output, outputNotice, first, separator, last, string.Empty, "复制输出");
                output.style.color = record.State == XCommandRecordState.Running || record.Succeeded ? TerminalText : statusColor;
            }

            private static void BindPreview(Label label, Label notice, string first, string separator, string last, string prefix, string copyAction)
            {
                string preview = BuildConsolePreview(first, separator, last, out long originalLength);
                label.text = prefix + preview;
                bool truncated = preview.Length < originalLength;
                notice.text = truncated ? $"… [已截断显示：原文 {originalLength} 个 UTF-16 单元；“{copyAction}”可获取完整内容]" : string.Empty;
                notice.style.display = truncated ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void Unbind()
            {
                record = null;
                status.text = string.Empty;
                command.text = string.Empty;
                commandNotice.text = string.Empty;
                output.text = string.Empty;
                outputNotice.text = string.Empty;
                commandNotice.style.display = DisplayStyle.None;
                outputNotice.style.display = DisplayStyle.None;
            }

            private bool HasBoundRecord()
            {
                return owner != null && record != null && owner.loadedCommandRecordIds.Contains(record.Id);
            }

            private void Rerun()
            {
                if (HasBoundRecord())
                {
                    owner.ExecuteCommandLine(record.CommandLine);
                }
            }

            private void CopyCommand()
            {
                if (HasBoundRecord())
                {
                    EditorGUIUtility.systemCopyBuffer = record.CommandLine;
                }
            }

            private void CopyOutput()
            {
                if (HasBoundRecord())
                {
                    EditorGUIUtility.systemCopyBuffer = FormatCommandRecordOutput(record) ?? string.Empty;
                }
            }
        }

        [Serializable]
        private sealed class StringListStorage
        {
            public List<string> values = new List<string>();
        }
    }
}
