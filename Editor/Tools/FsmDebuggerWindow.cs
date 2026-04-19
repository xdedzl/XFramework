using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class FsmDebuggerWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/FSM Debugger";

        private readonly List<XFramework.Fsm.FsmDebugEntry> m_AllEntries = new List<XFramework.Fsm.FsmDebugEntry>();
        private readonly List<XFramework.Fsm.FsmDebugEntry> m_FilteredEntries = new List<XFramework.Fsm.FsmDebugEntry>();

        private TextField m_KeySearchField;
        private TextField m_StateSearchField;
        private DropdownField m_ScopeField;
        private ListView m_ListView;
        private Label m_StatusLabel;
        private Label m_SummaryLabel;
        private ObjectField m_OwnerField;
        private Label m_KeyLabel;
        private Label m_ScopeLabel;
        private Label m_ContextLabel;
        private Label m_CurrentStateLabel;
        private Label m_PreviousStateLabel;
        private Label m_LastTransitionLabel;
        private Label m_LastPayloadLabel;
        private Label m_StatesLabel;

        private double m_LastRefreshTime;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<FsmDebuggerWindow>();
            window.titleContent = new GUIContent("FSM Debugger");
            window.minSize = new Vector2(900f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
            RefreshEntries();
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
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
            root.Add(m_SummaryLabel);

            var splitView = new TwoPaneSplitView(0, 460, TwoPaneSplitViewOrientation.Horizontal);
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

            m_KeySearchField = new TextField("Key");
            m_KeySearchField.style.flexGrow = 1;
            m_KeySearchField.style.minWidth = 180;
            m_KeySearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_KeySearchField);

            m_StateSearchField = new TextField("State");
            m_StateSearchField.style.flexGrow = 1;
            m_StateSearchField.style.minWidth = 180;
            m_StateSearchField.style.marginLeft = 8;
            m_StateSearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_StateSearchField);

            m_ScopeField = new DropdownField("Scope", new List<string>
            {
                "全部",
                XFramework.Fsm.FsmScope.Global.ToString(),
                XFramework.Fsm.FsmScope.Instance.ToString()
            }, 0);
            m_ScopeField.style.width = 160;
            m_ScopeField.style.marginLeft = 8;
            m_ScopeField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_ScopeField);

            var refreshButton = new Button(RefreshEntries)
            {
                text = "刷新"
            };
            refreshButton.style.marginLeft = 8;
            refreshButton.style.width = 64;
            toolbar.Add(refreshButton);

            return toolbar;
        }

        private VisualElement BuildListPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1;
            pane.style.flexDirection = FlexDirection.Column;
            pane.style.marginRight = 4;
            pane.style.paddingLeft = 4;
            pane.style.paddingRight = 4;
            pane.style.paddingTop = 4;
            pane.style.paddingBottom = 4;
            pane.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f);

            pane.Add(BuildListHeader());

            m_ListView = new ListView
            {
                itemsSource = m_FilteredEntries,
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
            header.style.height = 22;
            header.style.paddingLeft = 4;
            header.style.paddingRight = 4;
            header.style.alignItems = Align.Center;
            header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);

            header.Add(CreateHeaderLabel("Key", 170));
            header.Add(CreateHeaderLabel("Scope", 70));
            header.Add(CreateHeaderLabel("Context", 100));

            var state = CreateHeaderLabel("Current State", 0);
            state.style.flexGrow = 1;
            header.Add(state);

            return header;
        }

        private VisualElement BuildDetailPane()
        {
            var pane = new ScrollView();
            pane.style.flexGrow = 1;
            pane.style.paddingLeft = 10;
            pane.style.paddingRight = 10;
            pane.style.paddingTop = 10;
            pane.style.paddingBottom = 10;
            pane.style.marginLeft = 4;
            pane.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f);

            m_StatusLabel = new Label("等待运行中的 FSM。");
            m_StatusLabel.style.whiteSpace = WhiteSpace.Normal;
            m_StatusLabel.style.marginBottom = 10;
            pane.Add(m_StatusLabel);

            m_OwnerField = new ObjectField("Owner")
            {
                objectType = typeof(UnityEngine.Object)
            };
            m_OwnerField.SetEnabled(false);
            pane.Add(m_OwnerField);

            m_KeyLabel = AddDetailRow(pane, "Key");
            m_ScopeLabel = AddDetailRow(pane, "Scope");
            m_ContextLabel = AddDetailRow(pane, "Context");
            m_CurrentStateLabel = AddDetailRow(pane, "Current State");
            m_PreviousStateLabel = AddDetailRow(pane, "Previous State");
            m_LastTransitionLabel = AddDetailRow(pane, "Last Transition");
            m_LastPayloadLabel = AddDetailRow(pane, "Payload", true);
            m_StatesLabel = AddDetailRow(pane, "Registered States", true);

            return pane;
        }

        private void HandleEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - m_LastRefreshTime < 0.5d)
            {
                return;
            }

            m_LastRefreshTime = EditorApplication.timeSinceStartup;
            RefreshEntries();
        }

        private void RefreshEntries()
        {
            m_AllEntries.Clear();
            if (Application.isPlaying && XFramework.GameEntry.IsModuleLoaded<XFramework.Fsm.FsmManager>())
            {
                m_AllEntries.AddRange(XFramework.Fsm.FsmManager.Instance.GetDebugEntries());
            }

            RefreshView();
        }

        private void RefreshView()
        {
            m_FilteredEntries.Clear();

            string keySearch = m_KeySearchField != null ? m_KeySearchField.value?.Trim() : string.Empty;
            string stateSearch = m_StateSearchField != null ? m_StateSearchField.value?.Trim() : string.Empty;
            string scopeFilter = m_ScopeField != null ? m_ScopeField.value : "全部";

            for (int i = 0; i < m_AllEntries.Count; i++)
            {
                XFramework.Fsm.FsmDebugEntry entry = m_AllEntries[i];

                if (!string.IsNullOrEmpty(keySearch) && entry.Key.IndexOf(keySearch, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                string stateName = string.IsNullOrEmpty(entry.CurrentStateName) ? entry.PreviousStateName : entry.CurrentStateName;
                if (!string.IsNullOrEmpty(stateSearch) && (stateName == null || stateName.IndexOf(stateSearch, System.StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                if (scopeFilter != "全部" && entry.Scope.ToString() != scopeFilter)
                {
                    continue;
                }

                m_FilteredEntries.Add(entry);
            }

            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_FilteredEntries;
                m_ListView.Rebuild();
            }

            if (m_SummaryLabel != null)
            {
                if (!Application.isPlaying)
                {
                    m_SummaryLabel.text = "进入 Play Mode 后会显示运行中的 FSM。";
                }
                else if (!XFramework.GameEntry.IsModuleLoaded<XFramework.Fsm.FsmManager>())
                {
                    m_SummaryLabel.text = "FsmManager 尚未加载。";
                }
                else
                {
                    m_SummaryLabel.text = $"活动 FSM：{m_FilteredEntries.Count} / {m_AllEntries.Count}";
                }
            }

            if (m_FilteredEntries.Count == 0)
            {
                ShowEntryDetail(null);
            }
        }

        private VisualElement MakeListItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            row.Add(CreateCellLabel("key", 170));
            row.Add(CreateCellLabel("scope", 70));
            row.Add(CreateCellLabel("context", 100));

            var state = CreateCellLabel("state", 0);
            state.style.flexGrow = 1;
            row.Add(state);

            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_FilteredEntries.Count)
            {
                return;
            }

            XFramework.Fsm.FsmDebugEntry entry = m_FilteredEntries[index];
            element.Q<Label>("key").text = entry.Key;
            element.Q<Label>("scope").text = entry.Scope.ToString();
            element.Q<Label>("context").text = entry.ContextTypeName;
            element.Q<Label>("state").text = string.IsNullOrEmpty(entry.CurrentStateName) ? "<Stopped>" : entry.CurrentStateName;
        }

        private void OnSelectionChanged(IEnumerable<object> selected)
        {
            object first = selected.FirstOrDefault();
            if (first is XFramework.Fsm.FsmDebugEntry entry)
            {
                ShowEntryDetail(entry);
                return;
            }

            ShowEntryDetail(null);
        }

        private void ShowEntryDetail(XFramework.Fsm.FsmDebugEntry? entry)
        {
            if (!entry.HasValue)
            {
                if (m_StatusLabel != null)
                {
                    m_StatusLabel.text = Application.isPlaying
                        ? "从左侧选择一个 FSM 查看详情。"
                        : "进入 Play Mode 后会显示运行中的 FSM。";
                }

                if (m_OwnerField != null)
                {
                    m_OwnerField.value = null;
                }

                SetLabelValue(m_KeyLabel, string.Empty);
                SetLabelValue(m_ScopeLabel, string.Empty);
                SetLabelValue(m_ContextLabel, string.Empty);
                SetLabelValue(m_CurrentStateLabel, string.Empty);
                SetLabelValue(m_PreviousStateLabel, string.Empty);
                SetLabelValue(m_LastTransitionLabel, string.Empty);
                SetLabelValue(m_LastPayloadLabel, string.Empty);
                SetLabelValue(m_StatesLabel, string.Empty);
                return;
            }

            XFramework.Fsm.FsmDebugEntry value = entry.Value;
            if (m_StatusLabel != null)
            {
                m_StatusLabel.text = $"DebugName: {value.Fsm.DebugName}";
            }

            if (m_OwnerField != null)
            {
                m_OwnerField.value = value.Owner;
            }

            SetLabelValue(m_KeyLabel, value.Key);
            SetLabelValue(m_ScopeLabel, value.Scope.ToString());
            SetLabelValue(m_ContextLabel, value.ContextTypeName);
            SetLabelValue(m_CurrentStateLabel, string.IsNullOrEmpty(value.CurrentStateName) ? "<Stopped>" : value.CurrentStateName);
            SetLabelValue(m_PreviousStateLabel, string.IsNullOrEmpty(value.PreviousStateName) ? "<None>" : value.PreviousStateName);
            SetLabelValue(m_LastTransitionLabel, $"{value.LastTransition.FrameCount} / {value.LastTransition.RealtimeSinceStartup:F3}s");
            SetLabelValue(m_LastPayloadLabel, value.LastPayloadSummary);
            SetLabelValue(m_StatesLabel, value.Fsm != null ? string.Join(", ", value.Fsm.RegisteredStateNames) : string.Empty);
        }

        private static Label AddDetailRow(VisualElement parent, string title, bool multiline = false)
        {
            var container = new VisualElement();
            container.style.marginBottom = 6;
            container.style.flexDirection = FlexDirection.Column;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(titleLabel);

            var valueLabel = new Label();
            valueLabel.style.whiteSpace = multiline ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            valueLabel.style.marginTop = 2;
            valueLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            container.Add(valueLabel);

            parent.Add(container);
            return valueLabel;
        }

        private static Label CreateHeaderLabel(string text, float width)
        {
            var label = new Label(text);
            label.style.width = width;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Label CreateCellLabel(string name, float width)
        {
            var label = new Label
            {
                name = name
            };
            if (width > 0)
            {
                label.style.width = width;
            }

            return label;
        }

        private static void SetLabelValue(Label label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
