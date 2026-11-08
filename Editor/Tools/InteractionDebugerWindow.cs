using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Interaction;
using XFramework.UI;
using ListView = UnityEngine.UIElements.ListView;

namespace XFramework.Editor
{
    /// <summary>
    /// 交互物编辑器 Debug 窗口：任意状态下列出当前场景所有交互物，
    /// Play Mode 下额外展示 <see cref="InteractionManager"/> 的运行时状态与候选集。
    /// </summary>
    public class InteractionDebugerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/Interaction Debuger";
        private const string AllOption = "全部";
        private const string ValidOption = "正常 (valid)";
        private const string InvalidOption = "异常 (invalid)";
        private const string CandidateOption = "候选";
        private const string SceneOption = "场景";
        private const string CurrentTargetOption = "激活中";

        private readonly List<RowInfo> m_AllRows = new();
        private readonly List<RowInfo> m_FilteredRows = new();

        private TextField m_SearchField;
        private DropdownField m_ValidFilter;
        private ListView m_ListView;
        private Label m_SummaryLabel;

        private RowInfo m_SelectedRow;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<InteractionDebugerWindow>();
            window.titleContent = new GUIContent("Interaction Debuger");
            window.minSize = new Vector2(980f, 540f);
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        public void CreateGUI()
        {
            BuildUI();
            RefreshAll();
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
            m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);

            var summaryScroll = new ScrollView(ScrollViewMode.Vertical);
            summaryScroll.style.maxHeight = 92;
            summaryScroll.Add(m_SummaryLabel);
            root.Add(summaryScroll);

            root.Add(BuildListPane());
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1;
            m_SearchField.style.minWidth = 180;
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            m_ValidFilter = CreateToolbarDropdown(toolbar, "筛选", new List<string> { AllOption, ValidOption, InvalidOption }, 120);
            m_ValidFilter.RegisterValueChangedCallback(_ => RefreshView());

            AddRefreshControls(toolbar);

            return toolbar;
        }

        private VisualElement BuildListPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1;
            pane.style.flexDirection = FlexDirection.Column;
            pane.style.paddingLeft = 4;
            pane.style.paddingRight = 4;
            pane.style.paddingTop = 4;
            pane.style.paddingBottom = 4;
            pane.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f);

            pane.Add(BuildListHeader());

            m_ListView = new ListView
            {
                itemsSource = m_FilteredRows,
                fixedItemHeight = 24,
                selectionType = SelectionType.Single
            };
            m_ListView.style.flexGrow = 1;
            m_ListView.style.marginTop = 4;
            m_ListView.makeItem = MakeListItem;
            m_ListView.bindItem = BindListItem;
#pragma warning disable CS0618 // Type or member is obsolete
            m_ListView.onSelectionChange += OnSelectionChanged;
#pragma warning restore CS0618 // Type or member is obsolete
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

            header.Add(CreateHeaderLabel("名称", 200));
            header.Add(CreateHeaderLabel("valid", 56));
            header.Add(CreateHeaderLabel("交互类型", 190));
            header.Add(CreateHeaderLabel("状态", 70));
            var position = CreateHeaderLabel("位置", 0);
            position.style.flexGrow = 1;
            header.Add(position);

            return header;
        }

        protected override void OnAutoRefresh()
        {
            RefreshAll();
        }

        protected override void OnRefreshClicked()
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            m_AllRows.Clear();

            var sceneObjects = UnityEngine.Object.FindObjectsByType<InteractionObject>(FindObjectsSortMode.None);
            HashSet<InteractionObject> candidates = CollectCandidates();
            InteractionObject currentTarget = CollectCurrentTarget();
            ResolvePlayerPosition(out Vector3 playerPosition, out bool hasPlayer);

            foreach (var obj in sceneObjects)
            {
                if (obj == null)
                {
                    continue;
                }

                bool isCandidate = candidates?.Contains(obj) ?? false;
                float sqrDistance = 0f;
                bool hasDistance = Application.isPlaying && hasPlayer;
                if (hasDistance)
                {
                    var planar = obj.transform.position - playerPosition;
                    planar.y = 0f;
                    sqrDistance = planar.sqrMagnitude;
                }

                m_AllRows.Add(new RowInfo
                {
                    Object = obj,
                    InteractableTypeName = obj.interactable != null ? obj.interactable.GetType().Name : "<null>",
                    IsCandidate = isCandidate,
                    IsCurrentTarget = currentTarget != null && ReferenceEquals(obj, currentTarget),
                    PlanarSqrDistance = sqrDistance,
                    HasDistance = hasDistance
                });
            }

            m_AllRows.Sort((a, b) => string.Compare(a.Object.name, b.Object.name, StringComparison.Ordinal));
            RefreshView();
        }

        private HashSet<InteractionObject> CollectCandidates()
        {
            if (!Application.isPlaying || !InteractionManager.IsValid)
            {
                return null;
            }

            return new HashSet<InteractionObject>(InteractionManager.Instance.Candidates);
        }

        private InteractionObject CollectCurrentTarget()
        {
            if (!Application.isPlaying || !InteractionManager.IsValid)
            {
                return null;
            }

            return InteractionManager.Instance.CurrentTarget;
        }

        private static void ResolvePlayerPosition(out Vector3 playerPosition, out bool hasPlayer)
        {
            if (!Application.isPlaying)
            {
                playerPosition = default;
                hasPlayer = false;
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player)
            {
                playerPosition = player.transform.position;
                hasPlayer = true;
                return;
            }

            playerPosition = default;
            hasPlayer = false;
        }

        private void RefreshView()
        {
            m_FilteredRows.Clear();

            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;
            string validFilter = m_ValidFilter != null ? m_ValidFilter.value : AllOption;

            foreach (var row in m_AllRows)
            {
                if (row.Object == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(search) && !IsSearchMatch(row, search))
                {
                    continue;
                }

                if (validFilter == ValidOption && !row.Object.valid)
                {
                    continue;
                }

                if (validFilter == InvalidOption && row.Object.valid)
                {
                    continue;
                }

                m_FilteredRows.Add(row);
            }

            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_FilteredRows;
                m_ListView.Rebuild();
            }

            RefreshSummary();

            if (m_SelectedRow != null && (m_SelectedRow.Object == null || !m_FilteredRows.Contains(m_SelectedRow)))
            {
                m_SelectedRow = null;
            }

            ShowDetail(m_SelectedRow, false);
        }

        private void RefreshSummary()
        {
            if (m_SummaryLabel == null)
            {
                return;
            }

            int sceneCount = m_AllRows.Count;
            int validCount = m_AllRows.Count(row => row.Object.valid);

            if (!Application.isPlaying || !InteractionManager.IsValid)
            {
                m_SummaryLabel.text =
                    $"场景交互物 {m_FilteredRows.Count} / {sceneCount}（valid {validCount}）| 编辑态：显示的是当前场景中的全部交互物，运行候选与处理器状态在 Play Mode 下展示。";
                return;
            }

            var manager = InteractionManager.Instance;
            string enableText = manager.IsInteractionEnabled ? "启用" : "停用";
            string visibleText = manager.IsInteractionVisible ? "可见" : "不可见";

            string currentHandler = manager.CurrentHandler != null ? manager.CurrentHandler.GetType().Name : "-";
            string currentTarget = manager.CurrentTarget != null ? manager.CurrentTarget.name : "-";
            int candidateCount = manager.Candidates != null ? manager.Candidates.Count : 0;

            string reasons = manager.InteractionInvisibleReasons.Count > 0
                ? string.Join(", ", manager.InteractionInvisibleReasons)
                : "-";

            m_SummaryLabel.text =
                $"交互 {enableText} | 提示 {visibleText}（原因: {reasons}）| 当前处理器: {currentHandler} | 当前真实激活: {currentTarget} | 处理器注册 {manager.HandlerMap?.Count ?? 0} 类\n" +
                $"场景内共 {sceneCount} 个交互物，本次显示 {m_FilteredRows.Count} | 候选 {candidateCount} 个（其中激活目标 1 个）";
        }

        private static bool IsSearchMatch(RowInfo row, string search)
        {
            return Contains(row.Object.name, search)
                || Contains(row.InteractableTypeName, search)
                || Contains(row.Object.GetType().Name, search);
        }

        private static bool Contains(string text, string search)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement MakeListItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            row.Add(CreateCellLabel("name", 200));
            row.Add(CreateCellLabel("valid", 56));
            row.Add(CreateCellLabel("interactable", 190));
            row.Add(CreateCellLabel("status", 70));
            row.Add(CreateCellLabel("position", 0));
            row.Q<Label>("position").style.flexGrow = 1;

            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_FilteredRows.Count)
            {
                return;
            }

            var row = m_FilteredRows[index];
            if (row.Object == null)
            {
                return;
            }

            element.Q<Label>("name").text = row.Object.name;
            element.Q<Label>("valid").text = row.Object.valid ? "是" : "否";
            element.Q<Label>("valid").style.color = row.Object.valid
                ? new Color(0.6f, 1f, 0.65f)
                : new Color(1f, 0.55f, 0.55f);
            element.Q<Label>("interactable").text = row.InteractableTypeName;

            Label statusLabel = element.Q<Label>("status");
            if (row.IsCurrentTarget)
            {
                statusLabel.text = CurrentTargetOption;
                statusLabel.style.color = new Color(1f, 0.67f, 0.28f);
            }
            else if (row.IsCandidate)
            {
                statusLabel.text = CandidateOption;
                statusLabel.style.color = new Color(0.6f, 1f, 0.75f);
            }
            else
            {
                statusLabel.text = SceneOption;
                statusLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            }

            string position = row.Object.transform.position.ToString("F1");
            if (row.HasDistance)
            {
                float distance = Mathf.Sqrt(row.PlanarSqrDistance);
                position += $"  (距玩家 {distance:F1})";
            }

            element.Q<Label>("position").text = position;

            Color rowColor = index % 2 == 0
                ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                : new Color(0.31f, 0.31f, 0.31f, 0.18f);
            element.style.backgroundColor = rowColor;
            element.tooltip = row.Object.gameObject.scene.name;
        }

        private void OnSelectionChanged(IEnumerable<object> selected)
        {
            m_SelectedRow = selected.FirstOrDefault() as RowInfo;
            ShowDetail(m_SelectedRow, true);
        }

        private void ShowDetail(RowInfo row, bool openInspector)
        {
            if (row?.Object == null)
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                PublishDetail(row);
            }
            else
            {
                XFrameworkInspectorWindow.RefreshIfOwner(this);
            }
        }

        private void PublishDetail(RowInfo row)
        {
            if (row?.Object == null)
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            XFrameworkInspectorWindow.InspectCustom(
                this,
                row.Object.name,
                parent => BuildDetailContent(parent, row),
                row.InteractableTypeName);
        }

        private void BuildDetailContent(VisualElement parent, RowInfo row)
        {
            if (row?.Object == null)
            {
                parent.Add(CreateMutedLabel("未选择交互物。"));
                return;
            }

            var obj = row.Object;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.backgroundColor = new Color(0.21f, 0.21f, 0.21f, 0.9f);

            var icon = new Label(obj.valid ? "OK" : "ERR");
            icon.style.width = 48;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.unityFontStyleAndWeight = FontStyle.Bold;
            icon.style.color = obj.valid ? new Color(0.6f, 1f, 0.65f) : new Color(1f, 0.55f, 0.55f);
            header.Add(icon);

            var titleGroup = new VisualElement();
            titleGroup.style.flexGrow = 1;
            titleGroup.style.marginLeft = 8;

            var title = new Label(obj.name);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            titleGroup.Add(title);

            var subtitle = new Label(obj.GetType().FullName);
            subtitle.style.color = new Color(0.72f, 0.72f, 0.72f);
            subtitle.style.marginTop = 2;
            titleGroup.Add(subtitle);
            header.Add(titleGroup);

            var sceneLabel = new Label(obj.gameObject.scene.name);
            sceneLabel.style.width = 160;
            sceneLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            sceneLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            header.Add(sceneLabel);

            parent.Add(header);

            var infoFoldout = new Foldout
            {
                text = "运行信息",
                value = true
            };
            infoFoldout.style.marginBottom = 8;
            infoFoldout.style.paddingLeft = 4;
            infoFoldout.style.paddingRight = 4;
            infoFoldout.style.paddingTop = 4;
            infoFoldout.style.paddingBottom = 4;
            infoFoldout.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.7f);

            infoFoldout.Add(CreateReadOnlyField("交互组件", row.InteractableTypeName));
            infoFoldout.Add(CreateReadOnlyField("配置有效 (valid)", obj.valid ? "是" : "否"));

            bool showRuntime = Application.isPlaying;
            string status = row.IsCurrentTarget ? "当前真实激活（已被选中）" : row.IsCandidate ? "候选（在交互范围内）" : "场景对象（未进入候选）";
            infoFoldout.Add(CreateReadOnlyField("状态", status));
            if (showRuntime && row.HasDistance)
            {
                infoFoldout.Add(CreateReadOnlyField("距玩家平面距离", Mathf.Sqrt(row.PlanarSqrDistance).ToString("F2")));
            }
            else if (showRuntime)
            {
                infoFoldout.Add(CreateReadOnlyField("距玩家平面距离", "未找到 Player"));
            }

            parent.Add(infoFoldout);

            var objectField = new ObjectField("对象")
            {
                objectType = typeof(GameObject),
                value = obj.gameObject
            };
            objectField.RegisterValueChangedCallback(_ => EditorGUIUtility.PingObject(obj));
            objectField.style.marginTop = 4;
            parent.Add(objectField);

            try
            {
                var inspector = new XInspector(false);
                inspector.style.flexGrow = 1;
                inspector.style.alignSelf = Align.Stretch;
                inspector.style.width = Length.Percent(100);
                inspector.style.marginTop = 8;
                inspector.Bind(obj);
                parent.Add(inspector);
            }
            catch (System.Exception exception)
            {
                parent.Add(new HelpBox($"构建 Inspector 失败: {exception.Message}", HelpBoxMessageType.Error));
            }
        }

        private static TextField CreateReadOnlyField(string labelText, string value)
        {
            var field = new TextField(labelText)
            {
                value = string.IsNullOrEmpty(value) ? "-" : value
            };
            field.isReadOnly = true;
            field.style.marginBottom = 3;
            return field;
        }

        private sealed class RowInfo
        {
            public InteractionObject Object;
            public string InteractableTypeName;
            public bool IsCandidate;
            public bool IsCurrentTarget;
            public float PlanarSqrDistance;
            public bool HasDistance;
        }
    }
}