using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using XFramework.UI;
using UIToolkitListView = UnityEngine.UIElements.ListView;

namespace XFramework.Editor
{
    public sealed class ProcedureDebuggerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/Procedure Debugger";
        private const string AllOption = "全部";
        private const string ErrorOption = "Error";
        private const string WarningOption = "Warning";
        private const string OkOption = "OK";
        private const string CurrentOption = "Current";
        private const string CachedOption = "Cached";
        private const string UncreatedOption = "Uncreated";

        private readonly List<ProcedureDebugItem> m_AllItems = new();
        private readonly List<ProcedureDebugItem> m_FilteredItems = new();
        private readonly Dictionary<Type, ProcedureDebugItem> m_ItemByType = new();
        private readonly Dictionary<string, List<Type>> m_PanelTypesByName = new(StringComparer.Ordinal);
        private readonly List<GameBaseDebugInfo> m_GameBases = new();
        private readonly List<UObjectReference> m_ObjectReferences = new();
        private readonly List<ProcedureDiagnostic> m_SceneDiagnostics = new();
        private readonly List<ProcedureDiagnostic> m_RuntimeDiagnostics = new();

        private TextField m_SearchField;
        private DropdownField m_KindFilter;
        private DropdownField m_StatusFilter;
        private Label m_SummaryLabel;
        private UIToolkitListView m_ListView;
        private Type m_SelectedType;
        private ProcedureManagerDebugSnapshot? m_RuntimeSnapshot;
        private ProcedureEffectiveConfig m_CurrentEffectiveConfig;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            ProcedureDebuggerWindow window = GetWindow<ProcedureDebuggerWindow>();
            window.titleContent = new GUIContent("Procedure Debugger");
            window.minSize = new Vector2(980f, 520f);
            window.Show();
            window.Focus();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            titleContent = new GUIContent("Procedure Debugger");
            ScanDefinitions();
            RefreshDynamicState();
        }

        public void CreateGUI()
        {
            BuildUI();
            RefreshView();
        }

        protected override void OnRefreshClicked()
        {
            ScanDefinitions();
            RefreshDynamicState();
            RefreshView();
        }

        protected override void OnAutoRefresh()
        {
            RefreshDynamicState();
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
            m_SummaryLabel.style.marginTop = 5f;
            m_SummaryLabel.style.marginBottom = 6f;
            m_SummaryLabel.style.color = new Color(0.76f, 0.76f, 0.76f);
            m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(m_SummaryLabel);

            VisualElement pane = CreatePane();
            pane.Add(BuildListHeader());

            m_ListView = new UIToolkitListView
            {
                itemsSource = m_FilteredItems,
                fixedItemHeight = 26f,
                selectionType = SelectionType.Single,
                makeItem = MakeListItem,
                bindItem = BindListItem
            };
            m_ListView.style.flexGrow = 1f;
            m_ListView.style.marginTop = 4f;
            m_ListView.selectionChanged += OnSelectionChanged;
            pane.Add(m_ListView);
            root.Add(pane);
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
            m_SearchField.style.minWidth = 190f;
            m_SearchField.tooltip = "按类型名、程序集、配置内容或诊断信息过滤";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            m_KindFilter = CreateToolbarDropdown(toolbar, "分类", new List<string>
            {
                AllOption,
                "Procedure",
                "SubProcedure",
                "Overlay"
            }, 116f);
            m_KindFilter.RegisterValueChangedCallback(_ => RefreshView());

            m_StatusFilter = CreateToolbarDropdown(toolbar, "状态", new List<string>
            {
                AllOption,
                ErrorOption,
                WarningOption,
                OkOption,
                CurrentOption,
                CachedOption,
                UncreatedOption
            }, 104f);
            m_StatusFilter.RegisterValueChangedCallback(_ => RefreshView());

            AddRefreshControls(toolbar, "重新扫描流程和面板类型，并刷新当前场景与运行时状态");
            return toolbar;
        }

        private static VisualElement BuildListHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22f);
            header.Add(CreateHeaderLabel("运行状态", 92f, 8f, true));
            header.Add(CreateHeaderLabel("配置诊断", 88f, 8f, true));
            header.Add(CreateHeaderLabel("分类", 96f, 8f, true));

            Label typeLabel = CreateHeaderLabel("类型", 0f, 8f, true);
            typeLabel.style.flexGrow = 1f;
            header.Add(typeLabel);

            header.Add(CreateHeaderLabel("Module", 58f, 8f, true));
            header.Add(CreateHeaderLabel("UI", 42f, 8f, true));
            header.Add(CreateHeaderLabel("Assembly", 170f, 0f, true));
            return header;
        }

        private static VisualElement MakeListItem()
        {
            VisualElement row = CreateRow(Color.clear, 26f);
            row.Add(CreateCellLabel("runtime", 92f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));
            row.Add(CreateCellLabel("diagnostic", 88f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));
            row.Add(CreateCellLabel("kind", 96f, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));

            Label typeLabel = CreateCellLabel("type", 0f, bold: true, marginRight: 8f, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true);
            typeLabel.style.flexGrow = 1f;
            row.Add(typeLabel);

            row.Add(CreateCellLabel("modules", 58f, marginRight: 8f, flexShrink: true, noWrap: true));
            row.Add(CreateCellLabel("ui", 42f, marginRight: 8f, flexShrink: true, noWrap: true));
            row.Add(CreateCellLabel("assembly", 170f, flexShrink: true, textOverflow: TextOverflow.Ellipsis, noWrap: true));
            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            ProcedureDebugItem item = m_FilteredItems[index];
            element.userData = item;

            Label runtime = element.Q<Label>("runtime");
            runtime.text = GetRuntimeStatusText(item);
            runtime.style.color = GetRuntimeStatusColor(item);

            Label diagnostic = element.Q<Label>("diagnostic");
            diagnostic.text = GetDiagnosticSummary(item);
            diagnostic.style.color = GetDiagnosticColor(GetMaxSeverity(item));

            element.Q<Label>("kind").text = GetKindText(item.Kind);
            Label typeLabel = element.Q<Label>("type");
            typeLabel.text = item.Type.FullName;
            typeLabel.tooltip = item.IsHidden
                ? $"{item.Type.AssemblyQualifiedName}\n[HideInEditor]"
                : item.Type.AssemblyQualifiedName;
            element.Q<Label>("modules").text = item.DisplayConfig.ModuleTypes.Count.ToString();
            element.Q<Label>("ui").text = item.DisplayConfig.PanelNames.Count.ToString();
            element.Q<Label>("assembly").text = item.Type.Assembly.GetName().Name;
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            ProcedureDebugItem item = selection.OfType<ProcedureDebugItem>().FirstOrDefault();
            if (item == null)
            {
                return;
            }

            m_SelectedType = item.Type;
            XFrameworkInspectorWindow.InspectCustom(
                this,
                item.Type.Name,
                BuildSelectedInspector,
                $"{GetKindText(item.Kind)} · {item.Type.FullName}");
        }

        private void ScanDefinitions()
        {
            m_AllItems.Clear();
            m_ItemByType.Clear();
            m_PanelTypesByName.Clear();

            ScanPanelDefinitions();
            AddTypes(TypeCache.GetTypesDerivedFrom<ProcedureBase>(), ProcedureDebugKind.Procedure);
            AddTypes(TypeCache.GetTypesDerivedFrom<SubProcedureBase>(), ProcedureDebugKind.SubProcedure);
            AddTypes(TypeCache.GetTypesDerivedFrom<ProcedureOverlayBase>(), ProcedureDebugKind.Overlay);

            foreach (ProcedureDebugItem item in m_AllItems)
            {
                m_ItemByType.Add(item.Type, item);
                if (item.Kind == ProcedureDebugKind.SubProcedure)
                {
                    item.ParentProcedureType = FindSubProcedureParent(item.Type);
                }
            }

            ValidateDefinitions();
            foreach (ProcedureDebugItem item in m_AllItems)
            {
                item.DisplayConfig = BuildDisplayConfig(item);
            }
        }

        private void ScanPanelDefinitions()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<PanelBase>())
            {
                if (!IsConcreteType(type))
                {
                    continue;
                }

                PanelInfoAttribute panelInfo = type.GetCustomAttribute<PanelInfoAttribute>();
                if (panelInfo == null || string.IsNullOrEmpty(panelInfo.name))
                {
                    continue;
                }

                if (!m_PanelTypesByName.TryGetValue(panelInfo.name, out List<Type> panelTypes))
                {
                    panelTypes = new List<Type>();
                    m_PanelTypesByName.Add(panelInfo.name, panelTypes);
                }
                panelTypes.Add(type);
            }
        }

        private void AddTypes(IEnumerable<Type> types, ProcedureDebugKind kind)
        {
            foreach (Type type in types)
            {
                if (!IsConcreteType(type))
                {
                    continue;
                }

                var item = new ProcedureDebugItem(type, kind)
                {
                    IsHidden = type.GetCustomAttribute<HideInEditor>() != null,
                    Attributes = ProcedureAttributeSet.Create(type)
                };
                m_AllItems.Add(item);
            }
        }

        private static bool IsConcreteType(Type type)
        {
            return type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters;
        }

        private void ValidateDefinitions()
        {
            foreach (ProcedureDebugItem item in m_AllItems)
            {
                if (item.Type.GetConstructor(Type.EmptyTypes) == null)
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        "类型缺少公开无参构造函数，现有 Procedure 创建链无法实例化。"));
                }

                if (item.Kind == ProcedureDebugKind.SubProcedure && item.ParentProcedureType == null)
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        "无法从继承链解析 SubProcedureBase<T> 的父 Procedure。"));
                }

                ValidateModuleAttribute(item);
                ValidateUIAttribute(item);
            }

            IEnumerable<IGrouping<string, ProcedureDebugItem>> duplicateNames = m_AllItems
                .Where(item => item.Kind == ProcedureDebugKind.Procedure)
                .GroupBy(item => item.Type.Name, StringComparer.Ordinal)
                .Where(group => group.Count() > 1);

            foreach (IGrouping<string, ProcedureDebugItem> group in duplicateNames)
            {
                string types = string.Join(", ", group.Select(item => item.Type.FullName));
                foreach (ProcedureDebugItem item in group)
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        $"Procedure 简单类名 '{group.Key}' 重复；ProcedureManager 缓存键会冲突：{types}"));
                }
            }
        }

        private static void ValidateModuleAttribute(ProcedureDebugItem item)
        {
            ProcedureModuleAttribute attribute = item.Attributes.Module;
            if (attribute == null)
            {
                return;
            }

            foreach (Type moduleType in attribute.ModuleTypes)
            {
                if (moduleType == null)
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        "ProcedureModuleAttribute 包含空模块类型。"));
                    continue;
                }

                if (!typeof(IGameModule).IsAssignableFrom(moduleType))
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        $"模块 {moduleType.FullName} 未实现 IGameModule。"));
                    continue;
                }

                ModuleLifecycleAttribute lifecycle = moduleType.GetCustomAttribute<ModuleLifecycleAttribute>();
                if (lifecycle == null || lifecycle.Lifecycle != ModuleLifecycle.Procedure)
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        $"模块 {moduleType.FullName} 未声明 ModuleLifecycle.Procedure。"));
                }
            }
        }

        private void ValidateUIAttribute(ProcedureDebugItem item)
        {
            ProcedureUIAttribute attribute = item.Attributes.UI;
            if (attribute == null)
            {
                return;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string panelName in attribute.PanelNames)
            {
                if (string.IsNullOrWhiteSpace(panelName))
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        "ProcedureUIAttribute 包含空面板名称。"));
                    continue;
                }

                if (!visited.Add(panelName))
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Warning,
                        $"ProcedureUIAttribute 重复填写面板 '{panelName}'。"));
                }

                if (!m_PanelTypesByName.TryGetValue(panelName, out List<Type> panelTypes))
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        $"面板 '{panelName}' 无法解析到 PanelInfoAttribute。"));
                }
                else if (panelTypes.Count > 1)
                {
                    item.StaticDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        $"面板名 '{panelName}' 被多个类型声明：{string.Join(", ", panelTypes.Select(type => type.FullName))}"));
                }
            }
        }

        private void RefreshDynamicState()
        {
            ScanOpenScenes();
            RefreshRuntimeState();
        }

        private void ScanOpenScenes()
        {
            m_GameBases.Clear();
            m_ObjectReferences.Clear();
            m_SceneDiagnostics.Clear();
            foreach (ProcedureDebugItem item in m_AllItems)
            {
                item.SceneDiagnostics.Clear();
            }

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (GameBase gameBase in root.GetComponentsInChildren<GameBase>(true))
                    {
                        m_GameBases.Add(BuildGameBaseInfo(gameBase));
                    }

                    m_ObjectReferences.AddRange(root.GetComponentsInChildren<UObjectReference>(true));
                }
            }

            int activeGameCount = m_GameBases.Count(info => info.GameBase.isActiveAndEnabled);
            if (activeGameCount > 1)
            {
                m_SceneDiagnostics.Add(new ProcedureDiagnostic(
                    DiagnosticSeverity.Warning,
                    $"当前已加载场景中存在 {activeGameCount} 个启用状态的 GameBase。"));
            }

            foreach (GameBaseDebugInfo info in m_GameBases)
            {
                if (!string.IsNullOrEmpty(info.Error))
                {
                    m_SceneDiagnostics.Add(new ProcedureDiagnostic(DiagnosticSeverity.Error, info.Error));
                }
            }

            foreach (ProcedureDebugItem item in m_AllItems)
            {
                ValidateSceneCamera(item);
            }
        }

        private GameBaseDebugInfo BuildGameBaseInfo(GameBase gameBase)
        {
            Type serializedType = gameBase.startProcedure?.GetType();
            Type resolvedType = null;
            string error = null;

            if (serializedType != null && serializedType.Name == gameBase.startTypeName)
            {
                resolvedType = serializedType;
            }
            else if (string.IsNullOrWhiteSpace(gameBase.startTypeName))
            {
                error = $"GameBase '{GetHierarchyPath(gameBase.transform)}' 未配置 startTypeName。";
            }
            else
            {
                List<Type> matches = m_AllItems
                    .Where(item => item.Kind == ProcedureDebugKind.Procedure && item.Type.FullName == gameBase.startTypeName)
                    .Select(item => item.Type)
                    .ToList();

                if (matches.Count == 1)
                {
                    resolvedType = matches[0];
                }
                else if (matches.Count == 0)
                {
                    error = $"GameBase '{GetHierarchyPath(gameBase.transform)}' 的启动流程 '{gameBase.startTypeName}' 无法解析。";
                }
                else
                {
                    error = $"GameBase '{GetHierarchyPath(gameBase.transform)}' 的启动流程 '{gameBase.startTypeName}' 在多个程序集中存在。";
                }
            }

            if (resolvedType != null && (!typeof(ProcedureBase).IsAssignableFrom(resolvedType) || !IsConcreteType(resolvedType)))
            {
                error = $"GameBase '{GetHierarchyPath(gameBase.transform)}' 的启动类型不是可创建的 Procedure。";
            }

            return new GameBaseDebugInfo(gameBase, serializedType, resolvedType, error);
        }

        private void ValidateSceneCamera(ProcedureDebugItem item)
        {
            ProcedureEffectiveConfig config = item.DisplayConfig;
            if (!config.HasCamera)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(config.CameraName))
            {
                item.SceneDiagnostics.Add(new ProcedureDiagnostic(
                    DiagnosticSeverity.Error,
                    "ProcedureCameraAttribute 的相机键为空。"));
                return;
            }

            List<UObjectReference> singleReferences = m_ObjectReferences
                .Where(reference => reference.Mode == UObjectReference.RegistrationMode.Single && reference.Path == config.CameraName)
                .ToList();

            if (singleReferences.Count == 0)
            {
                item.SceneDiagnostics.Add(new ProcedureDiagnostic(
                    DiagnosticSeverity.Warning,
                    $"当前已加载场景中未找到相机键 '{config.CameraName}' 的 Single UObjectReference。"));
            }
            else if (singleReferences.Count > 1)
            {
                item.SceneDiagnostics.Add(new ProcedureDiagnostic(
                    DiagnosticSeverity.Warning,
                    $"当前已加载场景中相机键 '{config.CameraName}' 存在 {singleReferences.Count} 个 Single UObjectReference，运行时会覆盖。"));
            }
        }

        private void RefreshRuntimeState()
        {
            m_RuntimeSnapshot = null;
            m_CurrentEffectiveConfig = null;
            m_RuntimeDiagnostics.Clear();
            foreach (ProcedureDebugItem item in m_AllItems)
            {
                item.RuntimeStatus = ProcedureRuntimeStatus.Uncreated;
                item.RuntimeInstance = null;
            }

            if (!Application.isPlaying || !ProcedureManager.IsValid)
            {
                return;
            }

            ProcedureManagerDebugSnapshot snapshot = ProcedureManager.Instance.GetDebugSnapshot();
            m_RuntimeSnapshot = snapshot;

            foreach (ProcedureCacheDebugSnapshot cache in snapshot.CachedProcedures)
            {
                SetRuntimeState(cache.Procedure, ProcedureRuntimeStatus.Cached);
                foreach (SubProcedureBase subProcedure in cache.SubProcedures)
                {
                    SetRuntimeState(subProcedure, ProcedureRuntimeStatus.Cached);
                }
            }

            SetRuntimeState(snapshot.CurrentProcedure, ProcedureRuntimeStatus.Current);
            SetRuntimeState(snapshot.CurrentSubProcedure, ProcedureRuntimeStatus.Current);
            SetRuntimeState(snapshot.CurrentOverlay, ProcedureRuntimeStatus.Current);

            m_CurrentEffectiveConfig = BuildCurrentEffectiveConfig(snapshot);
            CompareRuntimeState(snapshot, m_CurrentEffectiveConfig);
        }

        private void SetRuntimeState(object instance, ProcedureRuntimeStatus status)
        {
            if (instance == null || !m_ItemByType.TryGetValue(instance.GetType(), out ProcedureDebugItem item))
            {
                return;
            }

            item.RuntimeInstance = instance;
            item.RuntimeStatus = status;
        }

        private void CompareRuntimeState(ProcedureManagerDebugSnapshot snapshot, ProcedureEffectiveConfig expected)
        {
            if (snapshot.CurrentSubProcedure != null)
            {
                Type declaredParent = FindSubProcedureParent(snapshot.CurrentSubProcedure.GetType());
                Type actualParent = snapshot.CurrentProcedure?.GetType();
                if (declaredParent != actualParent)
                {
                    m_RuntimeDiagnostics.Add(new ProcedureDiagnostic(
                        DiagnosticSeverity.Error,
                        $"当前 SubProcedure 声明的父类型是 {declaredParent?.FullName ?? "None"}，实际父流程是 {actualParent?.FullName ?? "None"}。"));
                }
            }

            HashSet<Type> loadedModules = new(
                GameEntry.GetLoadedModuleTypes(ModuleLifecycle.Procedure));
            AddSetDifferences(
                expected.ModuleTypes,
                loadedModules,
                "模块未加载",
                "存在额外 Procedure 模块");

            var managedPanels = new HashSet<string>(snapshot.ManagedPanelNames, StringComparer.Ordinal);
            AddSetDifferences(
                expected.PanelNames,
                managedPanels,
                "面板未进入 Procedure 管理集合",
                "Procedure 管理集合存在额外面板");

            if (expected.PanelNames.Count > 0)
            {
                if (!GameEntry.IsModuleLoaded(typeof(UIManager)))
                {
                    AddRuntimeDifference("UIManager 未加载，无法打开期望面板。", true);
                }
                else
                {
                    Dictionary<string, UIPanelDebugSnapshot> panelSnapshots = UIManager.Instance
                        .GetDebugPanelSnapshots()
                        .ToDictionary(panel => panel.PanelName, StringComparer.Ordinal);
                    foreach (string panelName in expected.PanelNames)
                    {
                        if (!panelSnapshots.TryGetValue(panelName, out UIPanelDebugSnapshot panel) || !panel.IsOpened)
                        {
                            AddRuntimeDifference($"期望面板 '{panelName}' 当前未打开。", true);
                        }
                    }
                }
            }

            CompareCamera(snapshot, expected);

            if (expected.HasCursor &&
                (UnityEngine.Cursor.lockState != expected.CursorLockMode || UnityEngine.Cursor.visible != expected.CursorVisible))
            {
                AddRuntimeDifference(
                    $"Cursor 实际值为 {UnityEngine.Cursor.lockState} / Visible={UnityEngine.Cursor.visible}，期望 {expected.CursorLockMode} / Visible={expected.CursorVisible}。",
                    false);
            }

            if (expected.HasTimeScale && !Mathf.Approximately(Time.timeScale, expected.TimeScale))
            {
                AddRuntimeDifference(
                    $"Time.timeScale 实际值为 {Time.timeScale}，期望 {expected.TimeScale}。",
                    false);
            }
        }

        private void CompareCamera(
            ProcedureManagerDebugSnapshot snapshot,
            ProcedureEffectiveConfig expected)
        {
            if (!expected.HasCamera)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(expected.CameraName))
            {
                AddRuntimeDifference("ProcedureCameraAttribute 的相机键为空。", true);
                return;
            }

            if (snapshot.ActiveCameraName != expected.CameraName)
            {
                AddRuntimeDifference(
                    $"Camera Processor 当前键为 '{snapshot.ActiveCameraName ?? "None"}'，期望 '{expected.CameraName}'。",
                    false);
            }

            GameObject cameraObject = UObjectFinder.Find(expected.CameraName);
            if (cameraObject == null)
            {
                AddRuntimeDifference($"UObjectFinder 找不到相机键 '{expected.CameraName}'。", true);
                return;
            }

            Behaviour[] behaviours = cameraObject.GetComponents<Behaviour>();
            List<Behaviour> cameraBehaviours = behaviours.Where(IsCameraBehaviour).ToList();
            bool isActive = cameraBehaviours.Count > 0
                ? cameraBehaviours.Any(behaviour => behaviour.isActiveAndEnabled)
                : cameraObject.activeInHierarchy;
            if (!isActive)
            {
                AddRuntimeDifference($"相机对象 '{cameraObject.name}' 当前未启用。", true);
            }
        }

        private static bool IsCameraBehaviour(Behaviour behaviour)
        {
            string typeName = behaviour.GetType().Name;
            return typeName == "Camera" ||
                   typeName.Contains("CinemachineCamera") ||
                   typeName.Contains("CinemachineVirtualCamera") ||
                   typeName.Contains("CinemachineFreeLook");
        }

        private void AddSetDifferences<T>(
            HashSet<T> expected,
            HashSet<T> actual,
            string missingPrefix,
            string extraPrefix)
        {
            foreach (T missing in expected.Except(actual))
            {
                AddRuntimeDifference($"{missingPrefix}: {FormatValue(missing)}", true);
            }

            foreach (T extra in actual.Except(expected))
            {
                AddRuntimeDifference($"{extraPrefix}: {FormatValue(extra)}", false);
            }
        }

        private void AddRuntimeDifference(string message, bool isMissing)
        {
            m_RuntimeDiagnostics.Add(new ProcedureDiagnostic(
                isMissing ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                message));
        }

        private void RefreshView()
        {
            if (m_ListView == null)
            {
                return;
            }

            Type selectedType = m_SelectedType;
            string search = m_SearchField?.value?.Trim();
            string kindFilter = m_KindFilter?.value ?? AllOption;
            string statusFilter = m_StatusFilter?.value ?? AllOption;

            m_FilteredItems.Clear();
            foreach (ProcedureDebugItem item in m_AllItems)
            {
                if (MatchesKind(item, kindFilter) &&
                    MatchesStatus(item, statusFilter) &&
                    MatchesSearch(item, search))
                {
                    m_FilteredItems.Add(item);
                }
            }

            m_FilteredItems.Sort(CompareItems);
            m_ListView.Rebuild();

            int selectedIndex = selectedType == null
                ? -1
                : m_FilteredItems.FindIndex(item => item.Type == selectedType);
            if (selectedIndex >= 0)
            {
                m_ListView.SetSelectionWithoutNotify(new[] { selectedIndex });
            }
            else
            {
                m_ListView.ClearSelection();
            }

            UpdateSummary();
            XFrameworkInspectorWindow.RefreshIfOwner(this);
        }

        private void UpdateSummary()
        {
            int errorCount = m_AllItems.Count(item => GetMaxSeverity(item) == DiagnosticSeverity.Error);
            int warningCount = m_AllItems.Count(item => GetMaxSeverity(item) == DiagnosticSeverity.Warning);
            int sceneErrorCount = m_SceneDiagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            int sceneWarningCount = m_SceneDiagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            string mode = Application.isPlaying ? "Play Mode" : "Edit Mode";
            string runtimeText = string.Empty;

            if (m_RuntimeSnapshot.HasValue)
            {
                ProcedureManagerDebugSnapshot snapshot = m_RuntimeSnapshot.Value;
                runtimeText = $" | 当前链: {GetTypeName(snapshot.CurrentProcedure)} / {GetTypeName(snapshot.CurrentSubProcedure)} / {GetTypeName(snapshot.CurrentOverlay)}";
            }
            else if (Application.isPlaying)
            {
                runtimeText = " | ProcedureManager 尚未创建";
            }

            m_SummaryLabel.text =
                $"{mode} | 类型 {m_AllItems.Count} | 当前显示 {m_FilteredItems.Count} | GameBase {m_GameBases.Count} | " +
                $"类型 Error {errorCount} / Warning {warningCount} | 场景 Error {sceneErrorCount} / Warning {sceneWarningCount}{runtimeText}";
        }

        private bool MatchesStatus(ProcedureDebugItem item, string filter)
        {
            return filter switch
            {
                ErrorOption => GetMaxSeverity(item) == DiagnosticSeverity.Error,
                WarningOption => GetMaxSeverity(item) == DiagnosticSeverity.Warning,
                OkOption => GetMaxSeverity(item) == DiagnosticSeverity.Info,
                CurrentOption => HasRuntimeManager() && item.RuntimeStatus == ProcedureRuntimeStatus.Current,
                CachedOption => HasRuntimeManager() && item.RuntimeStatus == ProcedureRuntimeStatus.Cached,
                UncreatedOption => HasRuntimeManager() && item.RuntimeStatus == ProcedureRuntimeStatus.Uncreated,
                _ => true
            };
        }

        private static bool HasRuntimeManager()
        {
            return Application.isPlaying && ProcedureManager.IsValid;
        }

        private static bool MatchesKind(ProcedureDebugItem item, string filter)
        {
            return filter == AllOption || GetKindText(item.Kind) == filter;
        }

        private bool MatchesSearch(ProcedureDebugItem item, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            string searchable = string.Join(" ",
                item.Type.FullName,
                item.Type.Assembly.GetName().Name,
                GetKindText(item.Kind),
                string.Join(" ", item.DisplayConfig.ModuleTypes.Select(type => type.FullName)),
                string.Join(" ", item.DisplayConfig.PanelNames),
                item.DisplayConfig.CameraName,
                string.Join(" ", GetDiagnostics(item).Select(diagnostic => diagnostic.Message)));
            return searchable.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int CompareItems(ProcedureDebugItem left, ProcedureDebugItem right)
        {
            int severity = GetMaxSeverity(right).CompareTo(GetMaxSeverity(left));
            if (severity != 0)
            {
                return severity;
            }

            int runtime = GetRuntimeRank(left.RuntimeStatus).CompareTo(GetRuntimeRank(right.RuntimeStatus));
            if (runtime != 0)
            {
                return runtime;
            }

            int kind = left.Kind.CompareTo(right.Kind);
            return kind != 0
                ? kind
                : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
        }

        private static int GetRuntimeRank(ProcedureRuntimeStatus status)
        {
            return status switch
            {
                ProcedureRuntimeStatus.Current => 0,
                ProcedureRuntimeStatus.Cached => 1,
                _ => 2
            };
        }

        private void BuildSelectedInspector(VisualElement root)
        {
            if (m_SelectedType == null || !m_ItemByType.TryGetValue(m_SelectedType, out ProcedureDebugItem item))
            {
                root.Add(CreateMutedLabel("当前选择已不存在。"));
                return;
            }

            VisualElement identity = CreateSection("类型");
            identity.Add(CreateInfoRow("类型", item.Type.FullName));
            identity.Add(CreateInfoRow("分类", GetKindText(item.Kind)));
            identity.Add(CreateInfoRow("程序集", item.Type.Assembly.GetName().Name));
            identity.Add(CreateInfoRow("运行状态", GetRuntimeStatusText(item)));
            identity.Add(CreateInfoRow("Hidden", item.IsHidden ? "Yes" : "No"));
            if (item.Kind == ProcedureDebugKind.SubProcedure)
            {
                identity.Add(CreateInfoRow("父 Procedure", item.ParentProcedureType?.FullName ?? "无法解析"));
            }
            root.Add(identity);

            VisualElement attributes = CreateSection("声明特性与来源");
            attributes.Add(CreateInfoRow("Module", FormatAttribute(item.Type, item.Attributes.Module)));
            attributes.Add(CreateInfoRow("UI", FormatAttribute(item.Type, item.Attributes.UI)));
            attributes.Add(CreateInfoRow("Camera", FormatAttribute(item.Type, item.Attributes.Camera)));
            attributes.Add(CreateInfoRow("Cursor", FormatAttribute(item.Type, item.Attributes.Cursor)));
            attributes.Add(CreateInfoRow("TimeScale", FormatAttribute(item.Type, item.Attributes.TimeScale)));
            root.Add(attributes);

            VisualElement effective = CreateSection(item.Kind == ProcedureDebugKind.Overlay ? "Overlay 配置影响" : "有效配置");
            AddConfigRows(effective, item.DisplayConfig);
            root.Add(effective);

            VisualElement diagnostics = CreateSection("诊断");
            List<ProcedureDiagnostic> itemDiagnostics = GetDiagnostics(item).ToList();
            if (itemDiagnostics.Count == 0)
            {
                diagnostics.Add(CreateMutedLabel("OK", color: GetDiagnosticColor(DiagnosticSeverity.Info)));
            }
            else
            {
                foreach (ProcedureDiagnostic diagnostic in itemDiagnostics)
                {
                    Label label = CreateMutedLabel($"[{diagnostic.Severity}] {diagnostic.Message}", true, 2f, GetDiagnosticColor(diagnostic.Severity));
                    diagnostics.Add(label);
                }
            }
            root.Add(diagnostics);

            AddSceneInspector(root, item);
            AddRuntimeInspector(root, item);
        }

        private void AddSceneInspector(VisualElement root, ProcedureDebugItem item)
        {
            VisualElement scene = CreateSection("当前已加载场景");
            foreach (ProcedureDiagnostic diagnostic in m_SceneDiagnostics)
            {
                scene.Add(CreateMutedLabel(
                    $"[{diagnostic.Severity}] {diagnostic.Message}",
                    true,
                    2f,
                    GetDiagnosticColor(diagnostic.Severity)));
            }

            if (m_GameBases.Count == 0)
            {
                scene.Add(CreateMutedLabel("未找到 GameBase。"));
            }
            else
            {
                foreach (GameBaseDebugInfo info in m_GameBases)
                {
                    string value =
                        $"{GetHierarchyPath(info.GameBase.transform)} | startTypeName={info.GameBase.startTypeName} | " +
                        $"Serialized={info.SerializedType?.FullName ?? "None"} | Resolved={info.ResolvedType?.FullName ?? "None"}";
                    scene.Add(CreateMutedLabel(value, true, 2f));

                    if (info.GameBase.startProcedure is SceneProcedureBase sceneProcedure)
                    {
                        scene.Add(CreateMutedLabel(
                            $"ScenePath={sceneProcedure.ScenePath} | LoadSceneMode={sceneProcedure.LoadSceneMode}",
                            true,
                            2f));
                    }
                }
            }

            if (item.DisplayConfig.HasCamera)
            {
                List<UObjectReference> matches = m_ObjectReferences
                    .Where(reference => reference.Path == item.DisplayConfig.CameraName)
                    .ToList();
                scene.Add(CreateInfoRow("Camera Key", item.DisplayConfig.CameraName ?? "Empty"));
                scene.Add(CreateInfoRow(
                    "References",
                    matches.Count == 0
                        ? "None"
                        : string.Join(", ", matches.Select(reference => $"{GetHierarchyPath(reference.transform)} ({reference.Mode})"))));
            }
            root.Add(scene);
        }

        private void AddRuntimeInspector(VisualElement root, ProcedureDebugItem item)
        {
            if (!m_RuntimeSnapshot.HasValue)
            {
                return;
            }

            ProcedureManagerDebugSnapshot snapshot = m_RuntimeSnapshot.Value;
            VisualElement runtime = CreateSection("运行时现场");
            runtime.Add(CreateInfoRow("Current", GetTypeName(snapshot.CurrentProcedure)));
            runtime.Add(CreateInfoRow("Sub", GetTypeName(snapshot.CurrentSubProcedure)));
            runtime.Add(CreateInfoRow("Overlay", GetTypeName(snapshot.CurrentOverlay)));
            runtime.Add(CreateInfoRow("Managed UI", string.Join(", ", snapshot.ManagedPanelNames)));
            runtime.Add(CreateInfoRow("Active Camera", snapshot.ActiveCameraName ?? "None"));
            runtime.Add(CreateInfoRow("Processors", string.Join(", ", snapshot.ProcessorTypes.Select(type => type.Name))));
            root.Add(runtime);

            if (IsCurrentChainItem(item))
            {
                VisualElement liveConfig = CreateSection("当前链最终有效配置");
                AddConfigRows(liveConfig, m_CurrentEffectiveConfig);
                root.Add(liveConfig);
            }
        }

        private bool IsCurrentChainItem(ProcedureDebugItem item)
        {
            if (!m_RuntimeSnapshot.HasValue)
            {
                return false;
            }

            ProcedureManagerDebugSnapshot snapshot = m_RuntimeSnapshot.Value;
            return item.Type == snapshot.CurrentProcedure?.GetType() ||
                   item.Type == snapshot.CurrentSubProcedure?.GetType() ||
                   item.Type == snapshot.CurrentOverlay?.GetType();
        }

        private static void AddConfigRows(VisualElement section, ProcedureEffectiveConfig config)
        {
            section.Add(CreateInfoRow("Modules", config.ModuleTypes.Count == 0
                ? "None"
                : string.Join(", ", config.ModuleTypes.Select(type => type.FullName).OrderBy(name => name))));
            section.Add(CreateInfoRow("UI", config.PanelNames.Count == 0
                ? "None"
                : string.Join(", ", config.PanelNames.OrderBy(name => name))));
            section.Add(CreateInfoRow("Camera", config.HasCamera ? config.CameraName ?? "Empty" : "未配置"));
            section.Add(CreateInfoRow("Cursor", config.HasCursor
                ? $"{config.CursorLockMode}, Visible={config.CursorVisible}"
                : "未配置"));
            section.Add(CreateInfoRow("TimeScale", config.HasTimeScale ? config.TimeScale.ToString() : "未配置"));
        }

        private ProcedureEffectiveConfig BuildDisplayConfig(ProcedureDebugItem item)
        {
            if (item.Kind != ProcedureDebugKind.SubProcedure || item.ParentProcedureType == null)
            {
                return ProcedureEffectiveConfig.FromAttributes(item.Attributes);
            }

            ProcedureAttributeSet parent = ProcedureAttributeSet.Create(item.ParentProcedureType);
            return ProcedureEffectiveConfig.FromAttributes(new ProcedureAttributeSet(
                item.Attributes.Module ?? parent.Module,
                item.Attributes.UI ?? parent.UI,
                item.Attributes.Camera ?? parent.Camera,
                item.Attributes.Cursor ?? parent.Cursor,
                item.Attributes.TimeScale ?? parent.TimeScale));
        }

        private ProcedureEffectiveConfig BuildCurrentEffectiveConfig(ProcedureManagerDebugSnapshot snapshot)
        {
            ProcedureAttributeSet parentAttributes = snapshot.CurrentProcedure != null
                ? ProcedureAttributeSet.Create(snapshot.CurrentProcedure.GetType())
                : default;
            ProcedureAttributeSet effectiveBaseAttributes = parentAttributes;
            if (snapshot.CurrentSubProcedure != null)
            {
                ProcedureAttributeSet subAttributes = ProcedureAttributeSet.Create(snapshot.CurrentSubProcedure.GetType());
                effectiveBaseAttributes = new ProcedureAttributeSet(
                    subAttributes.Module ?? parentAttributes.Module,
                    subAttributes.UI ?? parentAttributes.UI,
                    subAttributes.Camera ?? parentAttributes.Camera,
                    subAttributes.Cursor ?? parentAttributes.Cursor,
                    subAttributes.TimeScale ?? parentAttributes.TimeScale);
            }

            ProcedureEffectiveConfig config = ProcedureEffectiveConfig.FromAttributes(effectiveBaseAttributes);

            if (snapshot.CurrentOverlay == null)
            {
                return config;
            }

            ProcedureAttributeSet overlayAttributes = ProcedureAttributeSet.Create(snapshot.CurrentOverlay.GetType());

            foreach (Type moduleType in overlayAttributes.Module?.ModuleTypes ?? Array.Empty<Type>())
            {
                if (moduleType != null)
                {
                    config.ModuleTypes.Add(moduleType);
                }
            }

            if (overlayAttributes.UI != null)
            {
                if (overlayAttributes.UI.Mode == ProcedureAttributeMode.Replace)
                {
                    config.PanelNames.Clear();
                }
                foreach (string panelName in overlayAttributes.UI.PanelNames)
                {
                    if (!string.IsNullOrWhiteSpace(panelName))
                    {
                        config.PanelNames.Add(panelName);
                    }
                }
            }

            if (overlayAttributes.Camera != null)
            {
                config.HasCamera = true;
                config.CameraName = overlayAttributes.Camera.CameraName;
            }
            if (overlayAttributes.Cursor != null)
            {
                config.HasCursor = true;
                config.CursorLockMode = overlayAttributes.Cursor.CursorLockMode;
                config.CursorVisible = overlayAttributes.Cursor.Visible;
            }
            if (overlayAttributes.TimeScale != null)
            {
                config.HasTimeScale = true;
                config.TimeScale = overlayAttributes.TimeScale.TimeScale;
            }
            return config;
        }

        private IEnumerable<ProcedureDiagnostic> GetDiagnostics(ProcedureDebugItem item)
        {
            foreach (ProcedureDiagnostic diagnostic in item.StaticDiagnostics)
            {
                yield return diagnostic;
            }
            foreach (ProcedureDiagnostic diagnostic in item.SceneDiagnostics)
            {
                yield return diagnostic;
            }
            if (IsCurrentChainItem(item))
            {
                foreach (ProcedureDiagnostic diagnostic in m_RuntimeDiagnostics)
                {
                    yield return diagnostic;
                }
            }
        }

        private DiagnosticSeverity GetMaxSeverity(ProcedureDebugItem item)
        {
            DiagnosticSeverity severity = DiagnosticSeverity.Info;
            foreach (ProcedureDiagnostic diagnostic in GetDiagnostics(item))
            {
                if (diagnostic.Severity > severity)
                {
                    severity = diagnostic.Severity;
                }
            }
            return severity;
        }

        private string GetDiagnosticSummary(ProcedureDebugItem item)
        {
            List<ProcedureDiagnostic> diagnostics = GetDiagnostics(item).ToList();
            int errors = diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            int warnings = diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
            if (errors > 0)
            {
                return $"Error {errors}";
            }
            return warnings > 0 ? $"Warning {warnings}" : "OK";
        }

        private static Color GetDiagnosticColor(DiagnosticSeverity severity)
        {
            return severity switch
            {
                DiagnosticSeverity.Error => new Color(1f, 0.36f, 0.32f),
                DiagnosticSeverity.Warning => new Color(1f, 0.76f, 0.28f),
                _ => new Color(0.34f, 0.86f, 0.48f)
            };
        }

        private string GetRuntimeStatusText(ProcedureDebugItem item)
        {
            if (!Application.isPlaying)
            {
                return GetMaxSeverity(item) switch
                {
                    DiagnosticSeverity.Error => ErrorOption,
                    DiagnosticSeverity.Warning => WarningOption,
                    _ => "OK"
                };
            }
            if (!ProcedureManager.IsValid)
            {
                return "No Manager";
            }
            return item.RuntimeStatus.ToString();
        }

        private Color GetRuntimeStatusColor(ProcedureDebugItem item)
        {
            if (!Application.isPlaying)
            {
                return GetDiagnosticColor(GetMaxSeverity(item));
            }

            if (!ProcedureManager.IsValid)
            {
                return new Color(0.62f, 0.62f, 0.62f);
            }

            return item.RuntimeStatus switch
            {
                ProcedureRuntimeStatus.Current => new Color(0.34f, 0.86f, 0.48f),
                ProcedureRuntimeStatus.Cached => new Color(0.38f, 0.70f, 1f),
                _ => new Color(0.58f, 0.58f, 0.58f)
            };
        }

        private static string GetKindText(ProcedureDebugKind kind)
        {
            return kind switch
            {
                ProcedureDebugKind.SubProcedure => "SubProcedure",
                ProcedureDebugKind.Overlay => "Overlay",
                _ => "Procedure"
            };
        }

        private static Type FindSubProcedureParent(Type type)
        {
            Type current = type;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(SubProcedureBase<>))
                {
                    Type parent = current.GetGenericArguments()[0];
                    return typeof(ProcedureBase).IsAssignableFrom(parent) ? parent : null;
                }
                current = current.BaseType;
            }
            return null;
        }

        private static string FormatAttribute(Type ownerType, Attribute attribute)
        {
            if (attribute == null)
            {
                return "未配置";
            }

            Type source = FindAttributeSource(ownerType, attribute.GetType());
            string sourceText = source == ownerType ? "直接声明" : $"继承自 {source?.FullName}";
            string value = attribute switch
            {
                ProcedureModuleAttribute module => string.Join(", ", module.ModuleTypes.Select(type => type?.FullName ?? "Null")),
                ProcedureUIAttribute ui => $"{ui.Mode}: {string.Join(", ", ui.PanelNames)}",
                ProcedureCameraAttribute camera => camera.CameraName,
                ProcedureCursorAttribute cursor => $"{cursor.CursorLockMode}, Visible={cursor.Visible}",
                ProcedureTimeScaleAttribute timeScale => timeScale.TimeScale.ToString(),
                _ => attribute.ToString()
            };
            return $"{value} ({sourceText})";
        }

        private static Type FindAttributeSource(Type type, Type attributeType)
        {
            Type current = type;
            while (current != null && current != typeof(object))
            {
                if (current.GetCustomAttribute(attributeType, false) != null)
                {
                    return current;
                }
                current = current.BaseType;
            }
            return null;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return $"{transform.gameObject.scene.name}:{path}";
        }

        private static string GetTypeName(object instance)
        {
            return instance?.GetType().Name ?? "None";
        }

        private static string FormatValue<T>(T value)
        {
            return value is Type type ? type.FullName : value?.ToString();
        }

        private enum ProcedureDebugKind
        {
            Procedure,
            SubProcedure,
            Overlay
        }

        private enum ProcedureRuntimeStatus
        {
            Uncreated,
            Cached,
            Current
        }

        private enum DiagnosticSeverity
        {
            Info,
            Warning,
            Error
        }

        private sealed class ProcedureDebugItem
        {
            public ProcedureDebugItem(Type type, ProcedureDebugKind kind)
            {
                Type = type;
                Kind = kind;
            }

            public Type Type { get; }
            public ProcedureDebugKind Kind { get; }
            public Type ParentProcedureType { get; set; }
            public bool IsHidden { get; set; }
            public ProcedureAttributeSet Attributes { get; set; }
            public ProcedureEffectiveConfig DisplayConfig { get; set; }
            public ProcedureRuntimeStatus RuntimeStatus { get; set; }
            public object RuntimeInstance { get; set; }
            public List<ProcedureDiagnostic> StaticDiagnostics { get; } = new();
            public List<ProcedureDiagnostic> SceneDiagnostics { get; } = new();
        }

        private readonly struct ProcedureDiagnostic
        {
            public ProcedureDiagnostic(DiagnosticSeverity severity, string message)
            {
                Severity = severity;
                Message = message;
            }

            public DiagnosticSeverity Severity { get; }
            public string Message { get; }
        }

        private readonly struct ProcedureAttributeSet
        {
            public ProcedureAttributeSet(
                ProcedureModuleAttribute module,
                ProcedureUIAttribute ui,
                ProcedureCameraAttribute camera,
                ProcedureCursorAttribute cursor,
                ProcedureTimeScaleAttribute timeScale)
            {
                Module = module;
                UI = ui;
                Camera = camera;
                Cursor = cursor;
                TimeScale = timeScale;
            }

            public ProcedureModuleAttribute Module { get; }
            public ProcedureUIAttribute UI { get; }
            public ProcedureCameraAttribute Camera { get; }
            public ProcedureCursorAttribute Cursor { get; }
            public ProcedureTimeScaleAttribute TimeScale { get; }

            public static ProcedureAttributeSet Create(Type type)
            {
                return new ProcedureAttributeSet(
                    type.GetCustomAttribute<ProcedureModuleAttribute>(),
                    type.GetCustomAttribute<ProcedureUIAttribute>(),
                    type.GetCustomAttribute<ProcedureCameraAttribute>(),
                    type.GetCustomAttribute<ProcedureCursorAttribute>(),
                    type.GetCustomAttribute<ProcedureTimeScaleAttribute>());
            }
        }

        private sealed class ProcedureEffectiveConfig
        {
            public HashSet<Type> ModuleTypes { get; } = new();
            public HashSet<string> PanelNames { get; } = new(StringComparer.Ordinal);
            public bool HasCamera { get; set; }
            public string CameraName { get; set; }
            public bool HasCursor { get; set; }
            public CursorLockMode CursorLockMode { get; set; }
            public bool CursorVisible { get; set; }
            public bool HasTimeScale { get; set; }
            public float TimeScale { get; set; }

            public static ProcedureEffectiveConfig FromAttributes(ProcedureAttributeSet attributes)
            {
                var config = new ProcedureEffectiveConfig();
                foreach (Type moduleType in attributes.Module?.ModuleTypes ?? Array.Empty<Type>())
                {
                    if (moduleType != null)
                    {
                        config.ModuleTypes.Add(moduleType);
                    }
                }
                foreach (string panelName in attributes.UI?.PanelNames ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(panelName))
                    {
                        config.PanelNames.Add(panelName);
                    }
                }

                if (attributes.Camera != null)
                {
                    config.HasCamera = true;
                    config.CameraName = attributes.Camera.CameraName;
                }
                if (attributes.Cursor != null)
                {
                    config.HasCursor = true;
                    config.CursorLockMode = attributes.Cursor.CursorLockMode;
                    config.CursorVisible = attributes.Cursor.Visible;
                }
                if (attributes.TimeScale != null)
                {
                    config.HasTimeScale = true;
                    config.TimeScale = attributes.TimeScale.TimeScale;
                }
                return config;
            }

        }

        private readonly struct GameBaseDebugInfo
        {
            public GameBaseDebugInfo(GameBase gameBase, Type serializedType, Type resolvedType, string error)
            {
                GameBase = gameBase;
                SerializedType = serializedType;
                ResolvedType = resolvedType;
                Error = error;
            }

            public GameBase GameBase { get; }
            public Type SerializedType { get; }
            public Type ResolvedType { get; }
            public string Error { get; }
        }
    }
}
