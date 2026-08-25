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
    public sealed class XCommandEditorWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/XCommand";
        private const string AllModeFilter = "全部";
        private const string AvailableModeFilter = "当前可用";
        private const string AllCategoryFilter = "全部分类";

        private static readonly Color TerminalBackground = new Color(0.055f, 0.06f, 0.065f);
        private static readonly Color TerminalBorder = new Color(0.20f, 0.23f, 0.25f);
        private static readonly Color TerminalText = new Color(0.78f, 0.82f, 0.84f);
        private static readonly Color CommandColor = new Color(0.37f, 0.82f, 0.94f);
        private static readonly Color PromptColor = new Color(0.38f, 0.92f, 0.52f);
        private readonly List<XCommandDescriptor> m_AllCommands = new List<XCommandDescriptor>();
        private readonly List<XCommandDescriptor> m_FilteredCommands = new List<XCommandDescriptor>();
        private readonly List<XCommandDescriptor> m_CommandSuggestions = new List<XCommandDescriptor>();
        private readonly List<XCommandDiagnostic> m_Diagnostics = new List<XCommandDiagnostic>();
        private readonly HashSet<string> m_Favorites = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<XCommandRecord> m_VisibleCommandRecords = new List<XCommandRecord>();
        private readonly HashSet<long> m_VisibleCommandRecordIds = new HashSet<long>();

        private Label m_EnvironmentLabel;
        private Label m_TerminalSummaryLabel;
        private Button m_CliServerMenuButton;
        private Toggle m_AutoScrollToggle;
        private Toggle m_ShowHiddenCommandRecordsToggle;
        private Toggle m_CommandReferenceToggle;
        private TwoPaneSplitView m_MainSplitView;
        private ScrollView m_ConsoleScroll;
        private TextField m_CommandLineField;
        private Button m_ExecuteButton;
        private Label m_PromptHintLabel;
        private VisualElement m_SuggestionPanel;
        private Label m_SuggestionSummaryLabel;
        private ListView m_SuggestionList;
        private TextField m_SearchField;
        private DropdownField m_ModeFilter;
        private DropdownField m_CategoryFilter;
        private Toggle m_FavoritesOnlyToggle;
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

        private XCommandDescriptor m_SelectedCommand;
        private int m_CommandHistoryIndex = -1;
        private int m_CommandSuggestionIndex = -1;
        private bool m_ApplyingCommandHistory;
        private bool m_ShowingCommandSuggestions;
        private bool m_CommandHubSubscribed;
        private long m_VisibleAfterRecordId;
        private IVisualElementScheduledItem m_CliServerStatusSchedule;
        [SerializeField] private string m_SelectedCommandName;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XCommandEditorWindow window = GetWindow<XCommandEditorWindow>();
            window.titleContent = new GUIContent("XCommand");
            window.minSize = new Vector2(980f, 600f);
            window.Show();
            window.Focus();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            LoadPreferences();
            XCommandRegistry.Refresh();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        protected override void OnDisable()
        {
            m_CliServerStatusSchedule?.Pause();
            m_CliServerStatusSchedule = null;
            UnsubscribeCommandHub();
            SavePreferences();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            base.OnDisable();
        }

        public void CreateGUI()
        {
            m_CliServerStatusSchedule?.Pause();
            UnsubscribeCommandHub();
            BuildUI();
            ResetVisibleCommandRecords();
            SubscribeCommandHub();
            RefreshCliServerStatus();
            m_CliServerStatusSchedule = rootVisualElement.schedule.Execute(RefreshCliServerStatus).Every(500);
            RefreshRegistry();
            RenderConsole();
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
            m_AutoScrollToggle.style.marginRight = 8f;
            toolbar.Add(m_AutoScrollToggle);

            m_CommandReferenceToggle = new Toggle("Command Reference")
            {
                value = false
            };
            m_CommandReferenceToggle.style.marginRight = 8f;
            m_CommandReferenceToggle.tooltip = "显示或隐藏右侧命令手册。";
            m_CommandReferenceToggle.RegisterValueChangedCallback(evt => SetCommandReferenceVisible(evt.newValue));
            toolbar.Add(m_CommandReferenceToggle);

            toolbar.Add(CreateToolbarButton("复制全部", CopyAllConsoleOutput, 72f));
            toolbar.Add(CreateToolbarButton("清空输出", ClearConsoleOutput, 72f));
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
            menu.AddDisabledItem(new GUIContent(running ? $"运行中 · 127.0.0.1:{XFrameworkCliServer.Port}" : "Server 已停止"));
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
            pane.style.minWidth = 420f;
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
                value = true
            };
            m_ShowHiddenCommandRecordsToggle.style.marginRight = 8f;
            m_ShowHiddenCommandRecordsToggle.tooltip = "控制当前 Editor Terminal 是否显示 Hidden 命令的执行记录，不删除 Hub 记录。";
            m_ShowHiddenCommandRecordsToggle.RegisterValueChangedCallback(_ =>
            {
                RefreshTerminalSummary();
                RenderConsole();
            });
            terminalHeader.Add(m_ShowHiddenCommandRecordsToggle);

            terminalHeader.Add(CreateEntryButton("加载历史", LoadCommandHistory));
            var keyboardHint = new Label("Enter 执行  ·  Tab 预测  ·  ↑↓ 候选/历史  ·  Esc 关闭/清空");
            keyboardHint.style.marginLeft = 8f;
            keyboardHint.style.color = new Color(0.43f, 0.47f, 0.49f);
            terminalHeader.Add(keyboardHint);
            pane.Add(terminalHeader);

            m_ConsoleScroll = new ScrollView(ScrollViewMode.Vertical);
            m_ConsoleScroll.style.flexGrow = 1f;
            m_ConsoleScroll.style.paddingLeft = 10f;
            m_ConsoleScroll.style.paddingRight = 10f;
            m_ConsoleScroll.style.paddingTop = 8f;
            m_ConsoleScroll.style.paddingBottom = 8f;
            pane.Add(m_ConsoleScroll);

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

            m_SearchField = new TextField("搜索");
            m_SearchField.tooltip = "匹配命令名、显示名、分类、说明和声明类型；空格分词后全部命中。";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshCommandView());
            pane.Add(m_SearchField);

            var filterRow = new VisualElement();
            filterRow.style.flexDirection = FlexDirection.Row;
            filterRow.style.alignItems = Align.Center;
            filterRow.style.marginTop = 4f;

            m_ModeFilter = new DropdownField(new List<string> { AllModeFilter, AvailableModeFilter, XCommandMode.Runtime.ToString(), XCommandMode.Editor.ToString(), XCommandMode.Both.ToString() }, 0);
            m_ModeFilter.style.width = 112f;
            m_ModeFilter.tooltip = "按命令作用域筛选。";
            m_ModeFilter.RegisterValueChangedCallback(_ => RefreshCommandView());
            filterRow.Add(m_ModeFilter);

            m_CategoryFilter = new DropdownField(new List<string> { AllCategoryFilter }, 0);
            m_CategoryFilter.style.flexGrow = 1f;
            m_CategoryFilter.style.marginLeft = 5f;
            m_CategoryFilter.tooltip = "按命令分类筛选。";
            m_CategoryFilter.RegisterValueChangedCallback(_ => RefreshCommandView());
            filterRow.Add(m_CategoryFilter);

            m_FavoritesOnlyToggle = new Toggle("仅收藏");
            m_FavoritesOnlyToggle.style.marginLeft = 6f;
            m_FavoritesOnlyToggle.RegisterValueChangedCallback(_ => RefreshCommandView());
            filterRow.Add(m_FavoritesOnlyToggle);
            pane.Add(filterRow);

            m_CommandList = new ListView
            {
                itemsSource = m_FilteredCommands,
                fixedItemHeight = 44f,
                selectionType = SelectionType.Single,
                makeItem = MakeCommandItem,
                bindItem = BindCommandItem
            };
            m_CommandList.style.height = 270f;
            m_CommandList.style.flexShrink = 0f;
            m_CommandList.style.marginTop = 6f;
            m_CommandList.style.marginBottom = 6f;
            m_CommandList.selectionChanged += OnCommandSelectionChanged;
            m_CommandList.itemsChosen += OnCommandItemsChosen;
            pane.Add(m_CommandList);

            ScrollView detailsScroll = BuildCommandDetails();
            detailsScroll.style.flexGrow = 1f;
            pane.Add(detailsScroll);
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
            return row;
        }

        private void BindCommandItem(VisualElement element, int index)
        {
            XCommandDescriptor command = m_FilteredCommands[index];
            bool available = XCommandRegistry.CanExecute(command);
            element.userData = command;
            element.Q<Button>("favorite").text = m_Favorites.Contains(command.Command) ? "★" : "☆";
            element.Q<Label>("command").text = command.Command;
            element.Q<Label>("displayName").text = $"{command.Name}  ·  {command.Category}";
            Label modeLabel = element.Q<Label>("mode");
            modeLabel.text = command.Mode.ToString();
            modeLabel.style.color = GetModeColor(command.Mode);
            Label stateLabel = element.Q<Label>("state");
            stateLabel.text = available ? "●" : "○";
            stateLabel.style.color = available ? PromptColor : new Color(0.75f, 0.42f, 0.32f);
            element.style.opacity = available ? 1f : 0.48f;
            element.tooltip = available ? command.Description : GetUnavailableReason(command);
        }

        private void RefreshCommands()
        {
            XCommandRegistry.Refresh();
            RefreshRegistry();
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
            RefreshTerminalSummary();
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
            bool favoritesOnly = m_FavoritesOnlyToggle?.value ?? false;

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

            if (m_CommandSummaryLabel != null)
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
                m_CommandSummaryLabel.text = $"{m_FilteredCommands.Count}/{m_AllCommands.Count}  ·  可用 {availableCount}  ·  ★ {favoriteCount}";
            }

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
                m_DetailAvailabilityLabel.text = "单击命令查看完整信息，双击可填入终端。";
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

        private void RenderConsole()
        {
            if (m_ConsoleScroll == null)
            {
                return;
            }

            VisualElement content = m_ConsoleScroll.contentContainer;
            content.Clear();
            int displayedCount = 0;
            for (int i = 0; i < m_VisibleCommandRecords.Count; i++)
            {
                XCommandRecord record = m_VisibleCommandRecords[i];
                if (!ShouldDisplayCommandRecord(record))
                {
                    continue;
                }
                content.Add(CreateConsoleEntry(record));
                displayedCount++;
            }
            if (displayedCount == 0)
            {
                content.Add(CreateWelcomeMessage());
                return;
            }
            ScrollConsoleToBottom();
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

            var description = new Label("在下方提示符输入完整命令行，或从右侧 Command Reference 选择命令。\n当前窗口默认只接收新命令；点击“加载历史”可查看共享命令记录。 ");
            description.style.marginTop = 6f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = TerminalText;
            welcome.Add(description);
            return welcome;
        }

        private VisualElement CreateConsoleEntry(XCommandRecord record)
        {
            Color statusColor = record.State == XCommandRecordState.Running ? CommandColor : GetStatusColor(record.Status);
            var card = new VisualElement();
            card.style.marginBottom = 8f;
            card.style.paddingLeft = 9f;
            card.style.paddingRight = 7f;
            card.style.paddingTop = 6f;
            card.style.paddingBottom = 7f;
            card.style.backgroundColor = new Color(0.075f, 0.08f, 0.085f);
            card.style.borderLeftWidth = 2f;
            card.style.borderLeftColor = statusColor;

            var header = new VisualElement();
            header.style.height = 22f;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            string duration = record.State == XCommandRecordState.Running ? "执行中" : $"{record.DurationMilliseconds:F2} ms";
            var status = new Label($"[{record.ExecutedAtUtc.ToLocalTime():HH:mm:ss}]  {FormatSource(record.Source)}  ·  {FormatRecordStatus(record)}  ·  {duration}");
            status.style.flexGrow = 1f;
            status.style.unityFontStyleAndWeight = FontStyle.Bold;
            status.style.color = statusColor;
            header.Add(status);

            header.Add(CreateEntryButton("重跑", () => ExecuteCommandLine(record.CommandLine)));
            header.Add(CreateEntryButton("复制命令", () => EditorGUIUtility.systemCopyBuffer = record.CommandLine));
            header.Add(CreateEntryButton("复制输出", () => EditorGUIUtility.systemCopyBuffer = FormatCommandRecordOutput(record)));
            card.Add(header);

            var command = new Label($"> {record.CommandLine}");
            command.style.marginTop = 3f;
            command.style.unityFontStyleAndWeight = FontStyle.Bold;
            command.style.whiteSpace = WhiteSpace.Normal;
            command.style.color = CommandColor;
            card.Add(command);

            var output = new Label(FormatCommandRecordOutput(record));
            output.style.marginTop = 5f;
            output.style.whiteSpace = WhiteSpace.Normal;
            output.style.color = record.State == XCommandRecordState.Running || record.Succeeded ? TerminalText : statusColor;
            card.Add(output);
            return card;
        }

        private void ScrollConsoleToBottom()
        {
            if (m_AutoScrollToggle == null || !m_AutoScrollToggle.value || m_ConsoleScroll.contentContainer.childCount == 0)
            {
                return;
            }

            m_ConsoleScroll.schedule.Execute(() =>
            {
                VisualElement last = m_ConsoleScroll.contentContainer.ElementAt(m_ConsoleScroll.contentContainer.childCount - 1);
                m_ConsoleScroll.ScrollTo(last);
            });
        }

        private void CopyAllConsoleOutput()
        {
            var builder = new StringBuilder();
            for (int i = 0; i < m_VisibleCommandRecords.Count; i++)
            {
                XCommandRecord record = m_VisibleCommandRecords[i];
                if (!ShouldDisplayCommandRecord(record))
                {
                    continue;
                }
                builder.Append('[').Append(record.ExecutedAtUtc.ToLocalTime().ToString("HH:mm:ss")).Append("] ").Append(FormatSource(record.Source)).Append(" · ").Append(FormatRecordStatus(record));
                if (record.State == XCommandRecordState.Completed)
                {
                    builder.Append(" · ").Append(record.DurationMilliseconds.ToString("F2")).Append(" ms");
                }
                builder.AppendLine();
                builder.Append("> ").AppendLine(record.CommandLine);
                builder.AppendLine(FormatCommandRecordOutput(record));
                builder.AppendLine();
            }
            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private void ClearConsoleOutput()
        {
            ResetVisibleCommandRecords();
            RefreshTerminalSummary();
            RenderConsole();
            FocusCommandLine();
        }

        private void LoadCommandHistory()
        {
            IReadOnlyList<XCommandRecord> records = XCommandHub.Records;
            for (int i = 0; i < records.Count; i++)
            {
                XCommandRecord record = records[i];
                if (m_VisibleCommandRecordIds.Add(record.Id))
                {
                    m_VisibleCommandRecords.Add(record);
                }
            }
            m_VisibleCommandRecords.Sort((left, right) => left.Id.CompareTo(right.Id));
            m_VisibleAfterRecordId = records.Count == 0 ? XCommandHub.LatestRecordId : records[0].Id - 1;
            RefreshTerminalSummary();
            RenderConsole();
            FocusCommandLine();
        }

        private void ResetVisibleCommandRecords()
        {
            m_VisibleAfterRecordId = XCommandHub.LatestRecordId;
            m_VisibleCommandRecords.Clear();
            m_VisibleCommandRecordIds.Clear();
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
            if (record.Id <= m_VisibleAfterRecordId)
            {
                return;
            }
            m_VisibleCommandRecordIds.Add(record.Id);
            m_VisibleCommandRecords.Add(record);
            RefreshTerminalSummary();
            RenderConsole();
        }

        private void OnCommandRecordUpdated(XCommandRecord record)
        {
            if (m_VisibleCommandRecordIds.Contains(record.Id))
            {
                RefreshTerminalSummary();
                RenderConsole();
            }
        }

        private void OnCommandRecordTrimmed(long recordId)
        {
            if (!m_VisibleCommandRecordIds.Remove(recordId))
            {
                return;
            }
            m_VisibleCommandRecords.RemoveAll(record => record.Id == recordId);
            RefreshTerminalSummary();
            RenderConsole();
        }

        private void RefreshTerminalSummary()
        {
            if (m_TerminalSummaryLabel != null)
            {
                int displayedCount = 0;
                for (int i = 0; i < m_VisibleCommandRecords.Count; i++)
                {
                    if (ShouldDisplayCommandRecord(m_VisibleCommandRecords[i]))
                    {
                        displayedCount++;
                    }
                }
                m_TerminalSummaryLabel.text = $"visible {displayedCount}  ·  retained {XCommandHub.Records.Count}/{XCommandHub.RecordLimit}  ·  diagnostics {m_Diagnostics.Count}";
            }
        }

        private bool ShouldDisplayCommandRecord(XCommandRecord record)
        {
            if (m_ShowHiddenCommandRecordsToggle.value)
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

        [Serializable]
        private sealed class StringListStorage
        {
            public List<string> values = new List<string>();
        }
    }
}
