using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class GameModuleDebugerWindow : XFrameworkDebugWindowBase
    {
        private const string MenuPath = "XFramework/Debug/Game Module Debuger";
        private const string AllOption = "全部";
        private const string LoadedOption = "已加载";
        private const string UnloadedOption = "未加载";
        private const string MonoOption = "Mono";
        private const string NonMonoOption = "非 Mono";

        private readonly List<ModuleInfo> m_AllModules = new List<ModuleInfo>();
        private readonly List<ModuleInfo> m_FilteredModules = new List<ModuleInfo>();
        private readonly Dictionary<Type, GameEntry.GameModuleDebugSnapshot> m_RuntimeSnapshots = new Dictionary<Type, GameEntry.GameModuleDebugSnapshot>();
        private readonly Dictionary<Type, List<Type>> m_StaticDependents = new Dictionary<Type, List<Type>>();
        private readonly Dictionary<Type, List<Type>> m_ProcedureUsers = new Dictionary<Type, List<Type>>();
        private readonly Dictionary<string, bool> m_DetailFoldoutStates = new Dictionary<string, bool>();

        private TextField m_SearchField;
        private DropdownField m_LifecycleFilter;
        private DropdownField m_LoadedFilter;
        private DropdownField m_MonoFilter;
        private ListView m_ListView;
        private Label m_SummaryLabel;

        private ModuleInfo m_SelectedModule;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<GameModuleDebugerWindow>();
            window.titleContent = new GUIContent("Game Module Debuger");
            window.minSize = new Vector2(980f, 540f);
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshModules();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
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

            m_LifecycleFilter = CreateToolbarDropdown(toolbar, "生命周期", BuildLifecycleOptions(), 96);
            m_LifecycleFilter.RegisterValueChangedCallback(_ => RefreshView());

            m_LoadedFilter = CreateToolbarDropdown(toolbar, "状态", new List<string> { AllOption, LoadedOption, UnloadedOption }, 86);
            m_LoadedFilter.RegisterValueChangedCallback(_ => RefreshView());

            m_MonoFilter = CreateToolbarDropdown(toolbar, "类型", new List<string> { AllOption, MonoOption, NonMonoOption }, 86);
            m_MonoFilter.RegisterValueChangedCallback(_ => RefreshView());

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
                itemsSource = m_FilteredModules,
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

            header.Add(CreateHeaderLabel("状态", 54));
            header.Add(CreateHeaderLabel("模块", 180));
            header.Add(CreateHeaderLabel("生命周期", 110));
            header.Add(CreateHeaderLabel("类型", 92));
            header.Add(CreateHeaderLabel("依赖", 58));
            header.Add(CreateHeaderLabel("优先级", 64));
            header.Add(CreateHeaderLabel("顺序", 52));

            var assembly = CreateHeaderLabel("程序集", 0);
            assembly.style.flexGrow = 1;
            header.Add(assembly);

            return header;
        }

        protected override void OnAutoRefresh()
        {
            RefreshRuntimeSnapshots();
            RefreshView();
        }

        protected override void OnRefreshClicked()
        {
            RefreshModules();
        }

        private void RefreshModules()
        {
            m_AllModules.Clear();
            m_StaticDependents.Clear();
            m_ProcedureUsers.Clear();

            BuildProcedureUsers();

            var modules = DiscoverModuleTypes()
                .Select(CreateModuleInfo)
                .OrderBy(module => module.Type.FullName, StringComparer.Ordinal)
                .ToList();

            foreach (var module in modules)
            {
                m_AllModules.Add(module);
            }

            BuildStaticDependents();
            RefreshRuntimeSnapshots();
            RefreshView();
        }

        private void RefreshRuntimeSnapshots()
        {
            m_RuntimeSnapshots.Clear();
            if (!Application.isPlaying)
            {
                return;
            }

            foreach (var snapshot in GameEntry.GetDebugModuleSnapshots())
            {
                if (snapshot.ModuleType != null)
                {
                    m_RuntimeSnapshots[snapshot.ModuleType] = snapshot;
                }
            }
        }

        private void RefreshView()
        {
            m_FilteredModules.Clear();

            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;
            string lifecycleFilter = m_LifecycleFilter != null ? m_LifecycleFilter.value : AllOption;
            string loadedFilter = m_LoadedFilter != null ? m_LoadedFilter.value : AllOption;
            string monoFilter = m_MonoFilter != null ? m_MonoFilter.value : AllOption;

            foreach (var module in m_AllModules)
            {
                bool isLoaded = m_RuntimeSnapshots.ContainsKey(module.Type);
                if (!string.IsNullOrEmpty(search) && !IsSearchMatch(module, search))
                {
                    continue;
                }

                if (lifecycleFilter != AllOption && module.Lifecycle.ToString() != lifecycleFilter)
                {
                    continue;
                }

                if (loadedFilter == LoadedOption && !isLoaded)
                {
                    continue;
                }

                if (loadedFilter == UnloadedOption && isLoaded)
                {
                    continue;
                }

                if (monoFilter == MonoOption && !module.IsMonoModule)
                {
                    continue;
                }

                if (monoFilter == NonMonoOption && module.IsMonoModule)
                {
                    continue;
                }

                m_FilteredModules.Add(module);
            }

            if (m_ListView != null)
            {
                m_ListView.itemsSource = m_FilteredModules;
                m_ListView.Rebuild();
            }

            RefreshSummary();

            if (m_SelectedModule != null && !m_FilteredModules.Contains(m_SelectedModule))
            {
                m_SelectedModule = null;
            }

            ShowModuleDetail(m_SelectedModule, false);
        }

        private void RefreshSummary()
        {
            if (m_SummaryLabel == null)
            {
                return;
            }

            int loadedCount = m_AllModules.Count(module => m_RuntimeSnapshots.ContainsKey(module.Type));
            int warningCount = m_AllModules.Count(module => module.Warnings.Count > 0);
            string runtimeText = Application.isPlaying ? $"已加载 {loadedCount}" : "未进入 Play Mode";
            m_SummaryLabel.text = $"模块 {m_FilteredModules.Count} / {m_AllModules.Count} | {runtimeText} | 配置警告 {warningCount}";
        }

        private VisualElement MakeListItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            row.Add(CreateCellLabel("loaded", 54));
            row.Add(CreateCellLabel("name", 180));
            row.Add(CreateCellLabel("lifecycle", 110));
            row.Add(CreateCellLabel("kind", 92));
            row.Add(CreateCellLabel("dependencies", 58));
            row.Add(CreateCellLabel("priority", 64));
            row.Add(CreateCellLabel("order", 52));

            var assembly = CreateCellLabel("assembly", 0);
            assembly.style.flexGrow = 1;
            row.Add(assembly);

            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= m_FilteredModules.Count)
            {
                return;
            }

            ModuleInfo module = m_FilteredModules[index];
            bool isLoaded = m_RuntimeSnapshots.TryGetValue(module.Type, out var snapshot);
            element.Q<Label>("loaded").text = isLoaded ? "Loaded" : "-";
            element.Q<Label>("name").text = module.Type.Name;
            element.Q<Label>("lifecycle").text = module.Lifecycle.ToString();
            element.Q<Label>("kind").text = module.KindDisplayName;
            element.Q<Label>("dependencies").text = FormatDependencyHealth(module);
            element.Q<Label>("priority").text = isLoaded ? snapshot.Priority.ToString() : "-";
            element.Q<Label>("order").text = isLoaded && snapshot.UpdateOrder >= 0 ? snapshot.UpdateOrder.ToString() : "-";
            element.Q<Label>("assembly").text = module.AssemblyName;

            Color rowColor = index % 2 == 0
                ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                : new Color(0.31f, 0.31f, 0.31f, 0.18f);
            element.style.backgroundColor = rowColor;
            element.tooltip = module.Type.FullName;
        }

        private void OnSelectionChanged(IEnumerable<object> selected)
        {
            m_SelectedModule = selected.FirstOrDefault() as ModuleInfo;
            ShowModuleDetail(m_SelectedModule, true);
        }

        private void ShowModuleDetail(ModuleInfo module, bool openInspector)
        {
            if (module == null)
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            if (openInspector)
            {
                PublishModuleDetail(module);
            }
            else
            {
                XFrameworkInspectorWindow.RefreshIfOwner(this);
            }
        }

        private void PublishModuleDetail(ModuleInfo module)
        {
            if (module == null)
            {
                XFrameworkInspectorWindow.ClearIfOwner(this);
                return;
            }

            XFrameworkInspectorWindow.InspectCustom(
                this,
                module.Type.Name,
                parent => BuildModuleDetailContent(parent, module),
                module.Type.FullName);
        }

        private void BuildModuleDetailContent(VisualElement parent, ModuleInfo module)
        {
            if (module == null)
            {
                parent.Add(CreateEmptyDetail());
                return;
            }

            bool isLoaded = m_RuntimeSnapshots.TryGetValue(module.Type, out var snapshot);

            parent.Add(CreateInspectorHeader(module, isLoaded));
            parent.Add(CreateScriptSection(module));
            parent.Add(CreateTypeSection(module));
            parent.Add(CreateRuntimeSection(isLoaded, snapshot));
            parent.Add(CreateTypeListSection("依赖模块", module.Dependencies, "无依赖模块。", true));
            parent.Add(CreateTypeListSection("反向依赖", GetStaticDependents(module.Type), "没有其它模块依赖它。", true));
            parent.Add(CreateTypeListSection("Procedure 引用", GetProcedureUsers(module.Type), "没有 Procedure 通过特性引用它。", false));
            parent.Add(CreateWarningsSection(module));
        }

        private void BuildStaticDependents()
        {
            foreach (var module in m_AllModules)
            {
                foreach (Type dependenceType in module.Dependencies)
                {
                    if (dependenceType == null)
                    {
                        continue;
                    }

                    if (!m_StaticDependents.TryGetValue(dependenceType, out var dependents))
                    {
                        dependents = new List<Type>();
                        m_StaticDependents.Add(dependenceType, dependents);
                    }

                    dependents.Add(module.Type);
                }
            }
        }

        private void BuildProcedureUsers()
        {
            foreach (Type procedureType in DiscoverProcedureTypes())
            {
                var attr = procedureType.GetCustomAttribute<ProcedureModuleAttribute>(true);
                if (attr?.ModuleTypes == null)
                {
                    continue;
                }

                foreach (Type moduleType in attr.ModuleTypes)
                {
                    if (moduleType == null)
                    {
                        continue;
                    }

                    if (!m_ProcedureUsers.TryGetValue(moduleType, out var procedures))
                    {
                        procedures = new List<Type>();
                        m_ProcedureUsers.Add(moduleType, procedures);
                    }

                    procedures.Add(procedureType);
                }
            }
        }

        private ModuleInfo CreateModuleInfo(Type type)
        {
            var lifecycleAttr = type.GetCustomAttribute<ModuleLifecycleAttribute>(false);
            var dependenceAttrs = type.GetCustomAttributes<DependenceModuleAttribute>(true).ToArray();
            var dependencies = dependenceAttrs
                .Select(attr => attr.moduleType)
                .Where(dependenceType => dependenceType != null)
                .Distinct()
                .OrderBy(dependenceType => dependenceType.FullName, StringComparer.Ordinal)
                .ToList();

            var module = new ModuleInfo(
                type,
                lifecycleAttr?.Lifecycle ?? ModuleLifecycle.Normal,
                typeof(IMonoGameModule).IsAssignableFrom(type),
                IsSubclassOfGeneric(type, typeof(GameModuleWithEvent<>)) || IsSubclassOfGeneric(type, typeof(MonoGameModuleWithEvent<>)),
                dependencies,
                FindMonoScript(type));

            PopulateWarnings(module);
            return module;
        }

        private void PopulateWarnings(ModuleInfo module)
        {
            foreach (Type dependenceType in module.Dependencies)
            {
                if (!typeof(IGameModule).IsAssignableFrom(dependenceType))
                {
                    module.Warnings.Add($"依赖 {FormatTypeName(dependenceType)} 不是 IGameModule。");
                }

                if (dependenceType.IsAbstract || !dependenceType.IsClass)
                {
                    module.Warnings.Add($"依赖 {FormatTypeName(dependenceType)} 不是可实例化类。");
                }
            }

            if (HasDependencyCycle(module.Type, module.Type, new HashSet<Type>()))
            {
                module.Warnings.Add("检测到依赖环。");
            }

            if (m_ProcedureUsers.TryGetValue(module.Type, out var procedures)
                && procedures.Count > 0
                && module.Lifecycle != ModuleLifecycle.Procedure)
            {
                module.Warnings.Add("被 ProcedureModuleAttribute 引用，但生命周期不是 Procedure。");
            }
        }

        private bool HasDependencyCycle(Type startType, Type currentType, HashSet<Type> visited)
        {
            if (!visited.Add(currentType))
            {
                return false;
            }

            Type[] dependencies = currentType
                .GetCustomAttributes<DependenceModuleAttribute>(true)
                .Select(attr => attr.moduleType)
                .Where(type => type != null)
                .ToArray();

            foreach (Type dependenceType in dependencies)
            {
                if (dependenceType == startType)
                {
                    return true;
                }

                if (HasDependencyCycle(startType, dependenceType, visited))
                {
                    return true;
                }
            }

            visited.Remove(currentType);
            return false;
        }

        private List<Type> GetStaticDependents(Type moduleType)
        {
            return m_StaticDependents.TryGetValue(moduleType, out var dependents)
                ? dependents.OrderBy(type => type.FullName, StringComparer.Ordinal).ToList()
                : new List<Type>();
        }

        private List<Type> GetProcedureUsers(Type moduleType)
        {
            return m_ProcedureUsers.TryGetValue(moduleType, out var procedures)
                ? procedures.OrderBy(type => type.FullName, StringComparer.Ordinal).ToList()
                : new List<Type>();
        }

        private VisualElement CreateEmptyDetail()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.justifyContent = Justify.Center;

            var label = new Label("从左侧选择一个 GameModule 查看详情。");
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = new Color(0.72f, 0.72f, 0.72f);
            label.style.marginTop = 80;
            container.Add(label);
            return container;
        }

        private VisualElement CreateInspectorHeader(ModuleInfo module, bool isLoaded)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10;
            header.style.paddingLeft = 8;
            header.style.paddingRight = 8;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.backgroundColor = new Color(0.21f, 0.21f, 0.21f, 0.9f);

            var icon = new Label(isLoaded ? "ON" : "OFF");
            icon.style.width = 42;
            icon.style.unityTextAlign = TextAnchor.MiddleCenter;
            icon.style.unityFontStyleAndWeight = FontStyle.Bold;
            icon.style.color = isLoaded ? new Color(0.6f, 1f, 0.65f) : new Color(0.78f, 0.78f, 0.78f);
            header.Add(icon);

            var titleGroup = new VisualElement();
            titleGroup.style.flexGrow = 1;
            titleGroup.style.marginLeft = 8;

            var title = new Label(module.Type.Name);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15;
            titleGroup.Add(title);

            var subtitle = new Label(module.Type.Namespace ?? "<No Namespace>");
            subtitle.style.color = new Color(0.72f, 0.72f, 0.72f);
            subtitle.style.marginTop = 2;
            titleGroup.Add(subtitle);
            header.Add(titleGroup);

            var badge = new Label(module.KindDisplayName);
            badge.style.width = 92;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = new Color(0.9f, 0.9f, 0.9f);
            header.Add(badge);

            return header;
        }

        private VisualElement CreateScriptSection(ModuleInfo module)
        {
            var foldout = CreateSection("Script", true);
            var objectField = new ObjectField("脚本")
            {
                objectType = typeof(MonoScript),
                value = module.Script
            };
            objectField.SetEnabled(false);
            foldout.Add(objectField);

            return foldout;
        }

        private VisualElement CreateTypeSection(ModuleInfo module)
        {
            var foldout = CreateSection("Type", true);
            foldout.Add(CreateReadOnlyTextField("完整类型", module.Type.FullName));
            foldout.Add(CreateReadOnlyTextField("程序集", module.AssemblyName));
            foldout.Add(CreateReadOnlyTextField("基类", FormatTypeName(module.Type.BaseType)));
            foldout.Add(CreateReadOnlyTextField("生命周期", module.Lifecycle.ToString()));
            foldout.Add(CreateReadOnlyTextField("模块类型", module.KindDisplayName));
            return foldout;
        }

        private VisualElement CreateRuntimeSection(bool isLoaded, GameEntry.GameModuleDebugSnapshot snapshot)
        {
            var foldout = CreateSection("Runtime", true);
            foldout.Add(CreateReadOnlyTextField("状态", Application.isPlaying ? (isLoaded ? "已加载" : "未加载") : "未进入 Play Mode"));
            foldout.Add(CreateReadOnlyTextField("优先级", isLoaded ? snapshot.Priority.ToString() : "-"));
            foldout.Add(CreateReadOnlyTextField("持久化", isLoaded ? snapshot.IsPersistent.ToString() : "-"));
            foldout.Add(CreateReadOnlyTextField("Mono 模块", isLoaded ? snapshot.IsMonoModule.ToString() : "-"));
            foldout.Add(CreateReadOnlyTextField("Update 顺序", isLoaded && snapshot.UpdateOrder >= 0 ? snapshot.UpdateOrder.ToString() : "-"));
            IEnumerable<Type> runtimeDependencies = isLoaded ? snapshot.DependenceTypes : Enumerable.Empty<Type>();
            foldout.Add(CreateTypeList(runtimeDependencies, "无运行时依赖记录。", true));
            return foldout;
        }

        private VisualElement CreateTypeListSection(string title, IEnumerable<Type> types, string emptyText, bool allowSelectModule)
        {
            var foldout = CreateSection(title, true);
            foldout.Add(CreateTypeList(types, emptyText, allowSelectModule));
            return foldout;
        }

        private VisualElement CreateTypeList(IEnumerable<Type> types, string emptyText, bool allowSelectModule)
        {
            var container = new VisualElement();
            container.style.marginTop = 2;

            var typeArray = types.Where(type => type != null).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
            if (typeArray.Length == 0)
            {
                var empty = new Label(emptyText);
                empty.style.color = new Color(0.68f, 0.68f, 0.68f);
                empty.style.marginLeft = 3;
                container.Add(empty);
                return container;
            }

            foreach (Type type in typeArray)
            {
                container.Add(CreateTypeRow(type, allowSelectModule));
            }

            return container;
        }

        private VisualElement CreateTypeRow(Type type, bool allowSelectModule)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 24;
            row.style.marginTop = 1;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.backgroundColor = new Color(0.24f, 0.24f, 0.24f, 0.25f);
            row.tooltip = type.FullName;

            var name = new Label(type.Name);
            name.style.width = 150;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(name);

            var fullName = new Label(type.FullName);
            fullName.style.flexGrow = 1;
            fullName.style.color = new Color(0.74f, 0.74f, 0.74f);
            row.Add(fullName);

            var script = FindMonoScript(type);
            var pingButton = new Button(() => PingScript(script))
            {
                text = "Ping"
            };
            pingButton.style.width = 52;
            pingButton.style.marginLeft = 4;
            pingButton.SetEnabled(script != null);
            row.Add(pingButton);

            if (allowSelectModule)
            {
                ModuleInfo module = m_AllModules.FirstOrDefault(item => item.Type == type);
                var selectButton = new Button(() => SelectModule(module))
                {
                    text = "查看"
                };
                selectButton.style.width = 52;
                selectButton.style.marginLeft = 4;
                selectButton.SetEnabled(module != null);
                row.Add(selectButton);
            }

            return row;
        }

        private VisualElement CreateWarningsSection(ModuleInfo module)
        {
            var foldout = CreateSection("Warnings", true);
            if (module.Warnings.Count == 0)
            {
                foldout.Add(CreateHelpBox("未发现明显配置问题。", HelpBoxMessageType.Info));
                return foldout;
            }

            foreach (string warning in module.Warnings)
            {
                foldout.Add(CreateHelpBox(warning, HelpBoxMessageType.Warning));
            }

            return foldout;
        }

        private Foldout CreateSection(string title, bool value)
        {
            string stateKey = GetDetailFoldoutStateKey(title);
            bool stateValue = stateKey != null && m_DetailFoldoutStates.TryGetValue(stateKey, out bool storedValue)
                ? storedValue
                : value;
            var foldout = new Foldout
            {
                text = title,
                value = stateValue
            };
            if (stateKey != null)
            {
                foldout.RegisterValueChangedCallback(evt => m_DetailFoldoutStates[stateKey] = evt.newValue);
            }

            foldout.style.marginBottom = 8;
            foldout.style.paddingLeft = 4;
            foldout.style.paddingRight = 4;
            foldout.style.paddingTop = 4;
            foldout.style.paddingBottom = 6;
            foldout.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.7f);
            return foldout;
        }

        private string GetDetailFoldoutStateKey(string title)
        {
            return m_SelectedModule != null ? $"{m_SelectedModule.Type.FullName}:{title}" : null;
        }

        private static TextField CreateReadOnlyTextField(string label, string value)
        {
            var field = new TextField(label)
            {
                value = string.IsNullOrEmpty(value) ? "-" : value
            };
            field.isReadOnly = true;
            field.style.marginBottom = 3;
            return field;
        }

        private static HelpBox CreateHelpBox(string text, HelpBoxMessageType type)
        {
            var helpBox = new HelpBox(text, type);
            helpBox.style.marginTop = 3;
            helpBox.style.marginBottom = 3;
            return helpBox;
        }

        private void SelectModule(ModuleInfo module)
        {
            if (module == null)
            {
                return;
            }

            m_SelectedModule = module;
            int index = m_FilteredModules.IndexOf(module);
            if (index >= 0 && m_ListView != null)
            {
                m_ListView.SetSelection(index);
                m_ListView.ScrollToItem(index);
            }
            else
            {
                ShowModuleDetail(module, true);
            }
        }

        private static void PingScript(MonoScript script)
        {
            if (script == null)
            {
                return;
            }

            EditorGUIUtility.PingObject(script);
            Selection.activeObject = script;
        }

        private static IEnumerable<Type> DiscoverModuleTypes()
        {
            return GetSafeTypesInAllAssemblies()
                .Where(type => type != null
                    && type.IsClass
                    && !type.IsAbstract
                    && typeof(IGameModule).IsAssignableFrom(type));
        }

        private static IEnumerable<Type> DiscoverProcedureTypes()
        {
            return GetSafeTypesInAllAssemblies()
                .Where(type => type != null
                    && type.IsClass
                    && !type.IsAbstract
                    && typeof(ProcedureBase).IsAssignableFrom(type)
                    && type.GetCustomAttribute<ProcedureModuleAttribute>(true) != null);
        }

        private static IEnumerable<Type> GetSafeTypesInAllAssemblies()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    yield return type;
                }
            }
        }

        private static bool IsSubclassOfGeneric(Type type, Type genericType)
        {
            while (type != null && type != typeof(object))
            {
                Type current = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (current == genericType)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool IsSearchMatch(ModuleInfo module, string search)
        {
            return Contains(module.Type.Name, search)
                || Contains(module.Type.FullName, search)
                || Contains(module.AssemblyName, search)
                || Contains(module.Lifecycle.ToString(), search)
                || Contains(module.KindDisplayName, search);
        }

        private static bool Contains(string text, string search)
        {
            return !string.IsNullOrEmpty(text)
                && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MonoScript FindMonoScript(Type type)
        {
            string[] guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    return script;
                }
            }

            return null;
        }

        private static string FormatDependencyHealth(ModuleInfo module)
        {
            string count = module.Dependencies.Count.ToString();
            return module.Warnings.Count > 0 ? $"{count} !" : count;
        }

        private static string FormatTypeName(Type type)
        {
            return type == null ? "<null>" : type.FullName;
        }

        private static List<string> BuildLifecycleOptions()
        {
            var options = new List<string> { AllOption };
            options.AddRange(Enum.GetNames(typeof(ModuleLifecycle)));
            return options;
        }

        private sealed class ModuleInfo
        {
            public ModuleInfo(Type type, ModuleLifecycle lifecycle, bool isMonoModule, bool isEventModule, List<Type> dependencies, MonoScript script)
            {
                Type = type;
                Lifecycle = lifecycle;
                IsMonoModule = isMonoModule;
                IsEventModule = isEventModule;
                Dependencies = dependencies;
                Script = script;
                AssemblyName = type.Assembly.GetName().Name;
            }

            public Type Type { get; }
            public ModuleLifecycle Lifecycle { get; }
            public bool IsMonoModule { get; }
            public bool IsEventModule { get; }
            public List<Type> Dependencies { get; }
            public List<string> Warnings { get; } = new List<string>();
            public MonoScript Script { get; }
            public string AssemblyName { get; }
            public string KindDisplayName
            {
                get
                {
                    if (IsMonoModule && IsEventModule)
                    {
                        return "Mono+Event";
                    }

                    if (IsMonoModule)
                    {
                        return "Mono";
                    }

                    return IsEventModule ? "Event" : "Normal";
                }
            }
        }
    }
}
