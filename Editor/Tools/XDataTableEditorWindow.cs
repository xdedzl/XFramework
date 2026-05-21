using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using XFramework.Data;

namespace XFramework.Editor
{
    public class XDataTableEditorWindow : EditorWindow
    {
        private const float TableRowHeight = 28f;
        private const float InlineEditorHeight = 22f;

        private readonly Dictionary<(int rowIndex, int columnIndex), string> m_CellErrors = new();

        [SerializeField] private TextAsset m_SourceAsset;
        [SerializeField] private string m_SourceAssetGuid;
        [SerializeField] private string m_DataTableTypeName;
        [SerializeField] private bool m_IsUnionMode;
        [SerializeField] private string m_UnionTableTypeName;
        [SerializeField] private string m_UnionChildTableTypeName;
        private XDataTableEditorModel m_Model;
        private XDataTableEditorValidationResult m_ValidationResult;
        private List<XUnionDataTableEditorInfo> m_UnionTables;
        private XUnionDataTableEditorInfo m_CurrentUnionTable;
        private XUnionDataTableChildInfo m_CurrentUnionChild;
        private List<XDataTableAssetInfo> m_DataTableAssets;
        private XDataTableAssetInfo m_CurrentDataTableAsset;
        private string m_UnionMessage;

        private Label m_IssueLabel;
        private Label m_TableSummaryLabel;
        private VisualElement m_ReferenceContainer;
        private VisualElement m_HeaderContainer;
        private VisualElement m_RowContainer;
        private VisualElement m_FrozenRowContainer;
        private ScrollView m_TableScrollView;
        private ScrollView m_FrozenRowsScrollView;
        private ScrollView m_MainRowsScrollView;
        private ScrollView m_HeaderScrollView;
        private TextField m_LocateSearchField;
        private string m_LocateSearchText = string.Empty;

        private int m_SelectedRowIndex = -1;
        private int m_SortColumnIndex = -1;
        private bool m_SortAscending = true;
        private bool m_IsDirty;
        private bool m_FreezeHeaderEnabled = true;
        private bool m_FreezeIdEnabled = true;
        private bool m_FreezeAliasEnabled = true;
        private bool m_IsSyncingScroll;

        public static void ShowWindow(TextAsset textAsset)
        {
            XDataTableEditorWindow window = FindOpenWindow(textAsset) ?? CreateDockedWindow();
            window.minSize = new Vector2(1180f, 620f);
            window.LoadTextAsset(textAsset);
            window.Show();
            window.Focus();
        }

        public static void ShowWindowAndLocate(TextAsset textAsset, object keyValue)
        {
            XDataTableEditorWindow window = FindOpenWindow(textAsset) ?? CreateDockedWindow();
            window.minSize = new Vector2(1180f, 620f);
            window.LoadTextAsset(textAsset);
            window.Show();
            window.Focus();
            window.TryLocateByKeyValue(keyValue);
        }

        private static void ShowNewWindow(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                return;
            }

            XDataTableEditorWindow window = CreateDockedWindow();
            window.minSize = new Vector2(1180f, 620f);
            window.LoadTextAsset(textAsset);
            window.Show();
            window.Focus();
        }

        internal static void ShowUnionDataTableWindow(Type unionTableType)
        {
            XDataTableEditorWindow window = FindOpenUnionWindow(unionTableType) ?? CreateDockedWindow();
            window.minSize = new Vector2(1180f, 620f);
            window.LoadUnionMode(unionTableType);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            if (m_IsUnionMode)
            {
                RestoreUnionMode();
            }
            else
            {
                RestoreSourceAssetReference();
                if (m_SourceAsset != null && m_Model == null)
                {
                    LoadTextAsset(m_SourceAsset);
                }
            }

            UpdateWindowTitle();
        }

        private void OnDisable()
        {
            XDataTableRowDetailWindow detailWindow = XDataTableRowDetailWindow.GetOpenWindow();
            if (detailWindow != null && detailWindow.IsOwnedBy(this))
            {
                detailWindow.Close();
            }
        }

        public void CreateGUI()
        {
            if (m_Model != null || m_IsUnionMode)
            {
                BuildUI();
            }
            else
            {
                BuildPlaceholder();
            }
        }

        private void LoadTextAsset(TextAsset textAsset)
        {
            m_IsUnionMode = false;
            m_DataTableTypeName = string.Empty;
            m_DataTableAssets = null;
            m_CurrentDataTableAsset = null;
            m_UnionTableTypeName = string.Empty;
            m_UnionChildTableTypeName = string.Empty;
            m_UnionTables = null;
            m_CurrentUnionTable = null;
            m_CurrentUnionChild = null;
            m_UnionMessage = null;
            LoadTextAssetInternal(textAsset);
        }

        private void LoadTextAssetInternal(TextAsset textAsset)
        {
            m_SourceAsset = textAsset;
            m_SourceAssetGuid = ResolveAssetGuid(textAsset);
            m_CellErrors.Clear();
            m_SortColumnIndex = -1;
            m_SortAscending = true;
            m_IsDirty = false;

            try
            {
                m_Model = XDataTableEditorModel.Create(textAsset);
                m_DataTableTypeName = m_Model.TableType.AssemblyQualifiedName;
                m_DataTableAssets = BuildDataTableAssetInfos(m_Model.TableType);
                m_CurrentDataTableAsset = ResolveDataTableAsset(m_DataTableAssets, m_SourceAssetGuid);
                m_SelectedRowIndex = m_Model.Rows.Count > 0 ? 0 : -1;
                RefreshValidation();
            }
            catch (Exception exception)
            {
                m_Model = null;
                m_DataTableTypeName = string.Empty;
                m_DataTableAssets = null;
                m_CurrentDataTableAsset = null;
                m_SelectedRowIndex = -1;
                Debug.LogError(exception);
                EditorUtility.DisplayDialog("XDataTable 打开失败", exception.Message, "OK");
            }

            if (rootVisualElement.panel != null)
            {
                BuildUI();
            }

            UpdateWindowTitle();
        }

        private void LoadUnionMode(Type unionTableType)
        {
            m_IsUnionMode = true;
            m_SourceAsset = null;
            m_SourceAssetGuid = string.Empty;
            m_DataTableTypeName = string.Empty;
            m_DataTableAssets = null;
            m_CurrentDataTableAsset = null;
            m_Model = null;
            m_CellErrors.Clear();
            m_SortColumnIndex = -1;
            m_SortAscending = true;
            m_IsDirty = false;
            m_UnionTables = DiscoverUnionDataTables();
            if (unionTableType != null)
            {
                m_UnionTableTypeName = unionTableType.AssemblyQualifiedName;
                m_UnionChildTableTypeName = string.Empty;
            }

            m_CurrentUnionTable = ResolveUnionTable(m_UnionTableTypeName) ?? m_UnionTables.FirstOrDefault();
            if (m_CurrentUnionTable != null)
            {
                m_UnionTableTypeName = m_CurrentUnionTable.TableType.AssemblyQualifiedName;
                m_CurrentUnionChild = ResolveUnionChild(m_CurrentUnionTable, m_UnionChildTableTypeName)
                    ?? m_CurrentUnionTable.Children.FirstOrDefault();
                LoadCurrentUnionChild();
            }
            else
            {
                m_CurrentUnionChild = null;
                m_UnionChildTableTypeName = string.Empty;
                m_UnionMessage = "未找到 Union DataTable。";
            }

            if (rootVisualElement.panel != null)
            {
                BuildUI();
            }

            UpdateWindowTitle();
        }

        private void RestoreUnionMode()
        {
            if (m_UnionTables == null)
            {
                m_UnionTables = DiscoverUnionDataTables();
            }

            m_CurrentUnionTable = ResolveUnionTable(m_UnionTableTypeName) ?? m_UnionTables.FirstOrDefault();
            if (m_CurrentUnionTable == null)
            {
                m_Model = null;
                m_CurrentUnionChild = null;
                m_UnionMessage = "未找到 Union DataTable。";
                return;
            }

            m_UnionTableTypeName = m_CurrentUnionTable.TableType.AssemblyQualifiedName;
            m_CurrentUnionChild = ResolveUnionChild(m_CurrentUnionTable, m_UnionChildTableTypeName)
                ?? m_CurrentUnionTable.Children.FirstOrDefault();
            LoadCurrentUnionChild();
        }

        private void LoadCurrentUnionChild()
        {
            if (m_CurrentUnionChild == null)
            {
                m_Model = null;
                m_SourceAsset = null;
                m_SourceAssetGuid = string.Empty;
                m_UnionChildTableTypeName = string.Empty;
                m_UnionMessage = "当前 Union DataTable 没有子表。";
                return;
            }

            m_UnionChildTableTypeName = m_CurrentUnionChild.TableType.AssemblyQualifiedName;
            if (!string.IsNullOrEmpty(m_CurrentUnionChild.Error))
            {
                m_Model = null;
                m_SourceAsset = null;
                m_SourceAssetGuid = string.Empty;
                m_CellErrors.Clear();
                m_SelectedRowIndex = -1;
                m_IsDirty = false;
                m_UnionMessage = m_CurrentUnionChild.Error;
                return;
            }

            m_UnionMessage = null;
            LoadTextAssetInternal(m_CurrentUnionChild.Asset);
            m_IsUnionMode = true;
            m_UnionTableTypeName = m_CurrentUnionTable?.TableType.AssemblyQualifiedName ?? string.Empty;
            m_UnionChildTableTypeName = m_CurrentUnionChild.TableType.AssemblyQualifiedName;
        }

        private void RestoreSourceAssetReference()
        {
            if (m_SourceAsset != null || string.IsNullOrEmpty(m_SourceAssetGuid))
            {
                return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(m_SourceAssetGuid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            m_SourceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }

        private static string ResolveAssetGuid(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                return string.Empty;
            }

            string assetPath = AssetDatabase.GetAssetPath(textAsset);
            return string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
        }

        private void BuildPlaceholder()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 12f;
            root.style.paddingRight = 12f;
            root.style.paddingTop = 12f;
            root.style.paddingBottom = 12f;
            root.Add(new Label("双击 XDataTable 对应的 .xasset 资源以打开编辑器。")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            });
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
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);

            if (m_Model == null)
            {
                if (m_IsUnionMode)
                {
                    root.Add(BuildTopPanel());
                    RefreshStatus();
                    return;
                }

                BuildPlaceholder();
                return;
            }

            root.Add(BuildTopPanel());

            VisualElement tablePane = BuildTablePane();
            tablePane.style.flexGrow = 1f;
            root.Add(tablePane);

            RefreshStatus();
            RebuildHeader();
            RebuildRows();
            RebuildDetailPanel();
            RefreshActionState();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.S)
            {
                SaveCurrentAsset();
                evt.StopImmediatePropagation();
            }
        }

        private VisualElement BuildTopPanel()
        {
            XBox panel = new();
            panel.style.marginBottom = 6f;
            panel.style.paddingLeft = 6f;
            panel.style.paddingRight = 6f;
            panel.style.paddingTop = 6f;
            panel.style.paddingBottom = 6f;

            if (m_IsUnionMode)
            {
                VisualElement unionTableRow = BuildUnionTableRow();
                unionTableRow.style.marginBottom = 6f;
                panel.Add(unionTableRow);
            }
            else if (HasMultipleDataTableAssets())
            {
                VisualElement dataTableAssetRow = BuildDataTableAssetRow();
                dataTableAssetRow.style.marginBottom = 6f;
                panel.Add(dataTableAssetRow);
            }

            VisualElement toolbar = BuildToolbar();
            toolbar.style.marginBottom = 6f;
            panel.Add(toolbar);

            VisualElement statusRow = new();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.flexWrap = Wrap.Wrap;

            m_TableSummaryLabel = new Label();
            m_TableSummaryLabel.style.marginRight = 12f;
            m_TableSummaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            statusRow.Add(m_TableSummaryLabel);

            VisualElement separator = new();
            separator.style.width = 1f;
            separator.style.height = 14f;
            separator.style.marginRight = 12f;
            separator.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            statusRow.Add(separator);

            m_IssueLabel = new Label();
            m_IssueLabel.style.flexGrow = 1f;
            m_IssueLabel.style.whiteSpace = WhiteSpace.Normal;
            statusRow.Add(m_IssueLabel);

            panel.Add(statusRow);
            return panel;
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexWrap = Wrap.Wrap
                }
            };

            if (m_Model == null)
            {
                if (m_IsUnionMode)
                {
                    toolbar.Add(CreateToolbarButton("重载", ReloadFromDisk));
                }

                return toolbar;
            }

            toolbar.Add(CreateToolbarButton("保存", SaveCurrentAsset));
            toolbar.Add(CreateToolbarButton("重载", ReloadFromDisk));
            toolbar.Add(CreateToolbarButton("新增行", AddRow));

            toolbar.Add(CreateToolbarToggle("固定表头", m_FreezeHeaderEnabled, evt =>
            {
                m_FreezeHeaderEnabled = evt.newValue;
                BuildUI();
            }, marginLeft: 12f, marginRight: 14f));

            toolbar.Add(CreateToolbarToggle("固定 ID", m_FreezeIdEnabled, evt =>
            {
                m_FreezeIdEnabled = evt.newValue;
                BuildUI();
            }, marginRight: 14f));

            if (HasAliasColumn())
            {
                toolbar.Add(CreateToolbarToggle("固定 Alias", m_FreezeAliasEnabled, evt =>
                {
                    m_FreezeAliasEnabled = evt.newValue;
                    BuildUI();
                }, marginRight: 14f));
            }

            if (m_Model.SupportsKey || m_Model.SupportsAlias)
            {
                m_LocateSearchField = new TextField()
                {
                    style =
                    {
                        width = 220f,
                        marginLeft = 10f
                    }
                };
                m_LocateSearchField.value = m_LocateSearchText;
                m_LocateSearchField.isDelayed = true;
                m_LocateSearchField.labelElement.style.display = DisplayStyle.None;
                m_LocateSearchField.Q(TextField.textInputUssName).tooltip = "输入 Key 或 Alias";
                m_LocateSearchField.SetValueWithoutNotify(m_LocateSearchText);
                m_LocateSearchField.RegisterValueChangedCallback(evt => m_LocateSearchText = evt.newValue ?? string.Empty);
                toolbar.Add(m_LocateSearchField);
                toolbar.Add(CreateToolbarButton("定位", JumpToRow));
            }

            m_ReferenceContainer = BuildReferencePanel();
            m_ReferenceContainer.style.marginTop = 2f;
            m_ReferenceContainer.style.flexBasis = Length.Percent(100f);
            m_ReferenceContainer.style.minWidth = 0f;
            toolbar.Add(m_ReferenceContainer);

            return toolbar;
        }

        private VisualElement BuildUnionTableRow()
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexWrap = Wrap.Wrap;
            container.style.minHeight = 28f;

            Label titleLabel = new(m_CurrentUnionTable != null ? $"{m_CurrentUnionTable.DisplayName}:" : "Union DataTable:");
            titleLabel.style.marginRight = 8f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.flexShrink = 0f;
            container.Add(titleLabel);

            if (m_CurrentUnionTable == null || m_CurrentUnionTable.Children.Count == 0)
            {
                Label emptyLabel = new(string.IsNullOrEmpty(m_UnionMessage) ? "未找到可切换的子表。" : m_UnionMessage);
                emptyLabel.style.color = new Color(0.96f, 0.55f, 0.55f);
                emptyLabel.style.whiteSpace = WhiteSpace.Normal;
                container.Add(emptyLabel);
                return container;
            }

            foreach (XUnionDataTableChildInfo child in m_CurrentUnionTable.Children)
            {
                Button button = CreateUnionChildButton(child);
                container.Add(button);
            }

            return container;
        }

        private Button CreateUnionChildButton(XUnionDataTableChildInfo child)
        {
            bool selected = child == m_CurrentUnionChild;
            Button button = new(() => SelectUnionChild(child))
            {
                text = child.DisplayName
            };
            button.tooltip = string.IsNullOrEmpty(child.Error)
                ? string.IsNullOrEmpty(child.Path) ? $"切换到 {child.TableType.Name}" : $"切换到 {child.Path}"
                : child.Error;
            button.style.marginRight = 6f;
            button.style.marginBottom = 4f;
            button.style.height = 24f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            if (selected)
            {
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.style.backgroundColor = new Color(0.18f, 0.34f, 0.48f, 0.72f);
            }

            if (!string.IsNullOrEmpty(child.Error))
            {
                button.style.color = new Color(1f, 0.68f, 0.68f);
            }

            button.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowUnionChildContextMenu(child);
                evt.StopPropagation();
            });
            button.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                ShowUnionChildContextMenu(child);
                evt.StopPropagation();
            });

            return button;
        }

        private static void ShowUnionChildContextMenu(XUnionDataTableChildInfo child)
        {
            GenericMenu menu = new();
            if (child?.Asset != null)
            {
                menu.AddItem(new GUIContent("在新窗口里打开"), false, () => ShowNewWindow(child.Asset));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("在新窗口里打开"));
            }

            menu.ShowAsContext();
        }

        private bool HasMultipleDataTableAssets()
        {
            return m_DataTableAssets != null && m_DataTableAssets.Count > 1;
        }

        private VisualElement BuildDataTableAssetRow()
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexWrap = Wrap.Wrap;
            container.style.minHeight = 28f;

            Label titleLabel = new(m_Model != null ? $"{m_Model.TableType.Name}:" : "DataTable:");
            titleLabel.style.marginRight = 8f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.flexShrink = 0f;
            container.Add(titleLabel);

            foreach (XDataTableAssetInfo assetInfo in m_DataTableAssets)
            {
                container.Add(CreateDataTableAssetButton(assetInfo));
            }

            return container;
        }

        private Button CreateDataTableAssetButton(XDataTableAssetInfo assetInfo)
        {
            bool selected = assetInfo == m_CurrentDataTableAsset
                            || string.Equals(assetInfo.Guid, m_SourceAssetGuid, StringComparison.Ordinal);
            Button button = new(() => SelectDataTableAsset(assetInfo))
            {
                text = assetInfo.DisplayName
            };
            button.tooltip = assetInfo.Asset != null ? $"切换到 {assetInfo.Path}" : $"资源缺失: {assetInfo.Path}";
            button.style.marginRight = 6f;
            button.style.marginBottom = 4f;
            button.style.height = 24f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            if (selected)
            {
                button.style.unityFontStyleAndWeight = FontStyle.Bold;
                button.style.backgroundColor = new Color(0.18f, 0.34f, 0.48f, 0.72f);
            }

            if (assetInfo.Asset == null)
            {
                button.style.color = new Color(1f, 0.68f, 0.68f);
                button.SetEnabled(false);
            }

            button.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowDataTableAssetContextMenu(assetInfo);
                evt.StopPropagation();
            });
            button.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                ShowDataTableAssetContextMenu(assetInfo);
                evt.StopPropagation();
            });

            return button;
        }

        private static void ShowDataTableAssetContextMenu(XDataTableAssetInfo assetInfo)
        {
            GenericMenu menu = new();
            if (assetInfo?.Asset != null)
            {
                menu.AddItem(new GUIContent("在新窗口里打开"), false, () => ShowNewWindow(assetInfo.Asset));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("在新窗口里打开"));
            }

            menu.ShowAsContext();
        }

        private VisualElement BuildReferencePanel()
        {
            VisualElement panel = new();
            panel.style.flexDirection = FlexDirection.Row;
            panel.style.alignItems = Align.Center;
            panel.style.flexWrap = Wrap.Wrap;
            panel.style.flexGrow = 1f;
            panel.style.minWidth = 0f;

            panel.Add(CreateReferenceField("XAsset", m_SourceAsset, typeof(TextAsset), hasTrailingSpacing: true));

            MonoScript dataScript = ResolveScriptAsset(m_Model?.DataType, m_Model?.TableType);
            panel.Add(CreateReferenceField("数据类型", dataScript, typeof(MonoScript), hasTrailingSpacing: false));

            return panel;
        }

        private VisualElement CreateReferenceField(string label, Object targetObject, Type objectType, bool hasTrailingSpacing)
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexGrow = 1f;
            container.style.flexShrink = 1f;
            container.style.flexBasis = 180f;
            container.style.minWidth = 140f;
            container.style.marginRight = hasTrailingSpacing ? 6f : 0f;
            container.style.marginBottom = 0f;

            Label titleLabel = new(label);
            titleLabel.style.marginRight = 2f;
            titleLabel.style.flexShrink = 0f;
            container.Add(titleLabel);

            ObjectField field = CreatePingObjectField(string.Empty, targetObject, objectType);
            field.style.flexGrow = 1f;
            field.style.flexShrink = 1f;
            field.style.marginRight = 0f;
            field.style.minWidth = 0f;
            field.labelElement.style.display = DisplayStyle.None;
            container.Add(field);

            return container;
        }

        private VisualElement CreateToolbarToggle(
            string label,
            bool value,
            EventCallback<ChangeEvent<bool>> onValueChanged,
            float marginLeft = 0f,
            float marginRight = 0f)
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginLeft = marginLeft;
            container.style.marginRight = marginRight;

            Toggle toggle = new()
            {
                value = value
            };
            toggle.style.marginLeft = 0f;
            toggle.style.marginRight = 2f;
            toggle.style.flexShrink = 0f;
            toggle.labelElement.style.display = DisplayStyle.None;
            toggle.RegisterValueChangedCallback(onValueChanged);

            Label titleLabel = new(label);
            titleLabel.style.flexShrink = 0f;
            titleLabel.style.marginLeft = 0f;
            titleLabel.style.marginRight = 0f;
            titleLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                toggle.value = !toggle.value;
                evt.StopPropagation();
            });

            container.Add(toggle);
            container.Add(titleLabel);
            return container;
        }

        private ObjectField CreatePingObjectField(string label, Object targetObject, Type objectType)
        {
            ObjectField field = new(label)
            {
                objectType = objectType ?? typeof(Object),
                allowSceneObjects = false,
                value = targetObject
            };
            field.RegisterValueChangedCallback(_ => field.SetValueWithoutNotify(targetObject));
            field.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || targetObject == null)
                {
                    return;
                }

                Selection.activeObject = targetObject;
                EditorGUIUtility.PingObject(targetObject);
                if (evt.clickCount >= 2)
                {
                    AssetDatabase.OpenAsset(targetObject);
                }

                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            return field;
        }

        private static MonoScript ResolveScriptAsset(Type targetType, Type fallbackType = null)
        {
            if (targetType == null)
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets($"{targetType.Name} t:MonoScript");
            MonoScript fuzzyMatch = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                {
                    continue;
                }

                Type scriptType = script.GetClass();
                if (scriptType == targetType)
                {
                    return script;
                }

                string text = script.text;
                if (fuzzyMatch == null
                    && !string.IsNullOrEmpty(text)
                    && (text.Contains($"class {targetType.Name}")
                        || text.Contains($"struct {targetType.Name}")
                        || text.Contains($"enum {targetType.Name}")))
                {
                    fuzzyMatch = script;
                }
            }

            if (fuzzyMatch != null)
            {
                return fuzzyMatch;
            }

            if (fallbackType != null && fallbackType != targetType)
            {
                return ResolveScriptAsset(fallbackType);
            }

            return null;
        }

        private VisualElement BuildTablePane()
        {
            XBox pane = new();
            pane.style.flexGrow = 1f;
            pane.style.marginRight = 4f;
            pane.style.paddingLeft = 4f;
            pane.style.paddingRight = 4f;
            pane.style.paddingTop = 4f;
            pane.style.paddingBottom = 4f;

            List<XDataTableEditorColumn> frozenColumns = GetFrozenColumns();
            List<XDataTableEditorColumn> scrollableColumns = GetScrollableColumns(frozenColumns);

            m_FrozenRowsScrollView = null;
            m_MainRowsScrollView = null;
            m_HeaderScrollView = null;
            m_HeaderContainer = null;
            m_RowContainer = null;
            m_TableScrollView = null;

            if (m_FreezeHeaderEnabled)
            {
                if (frozenColumns.Count > 0)
                {
                    pane.Add(BuildFrozenHeaderLayout(frozenColumns, scrollableColumns));
                }
                else
                {
                    pane.Add(BuildHeaderOnlyLayout(scrollableColumns));
                }
            }
            else if (frozenColumns.Count > 0)
            {
                pane.Add(BuildFrozenBodyWithoutHeader(frozenColumns, scrollableColumns));
            }
            else
            {
                m_TableScrollView = CreateMainScrollView();
                VisualElement content = CreateScrollContent(m_Model.TotalColumnWidth);
                m_HeaderContainer = BuildHeaderRow(m_Model.Columns);
                content.Add(m_HeaderContainer);
                m_RowContainer = CreateRowColumnContainer();
                content.Add(m_RowContainer);
                m_TableScrollView.Add(content);
                pane.Add(m_TableScrollView);
            }

            HookScrollSync();
            return pane;
        }

        private VisualElement BuildFrozenHeaderLayout(
            List<XDataTableEditorColumn> frozenColumns,
            List<XDataTableEditorColumn> scrollableColumns)
        {
            VisualElement container = new();
            container.style.flexGrow = 1f;
            container.style.flexDirection = FlexDirection.Column;

            VisualElement topRow = new();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.marginBottom = 2f;

            if (frozenColumns.Count > 0)
            {
                VisualElement cornerHeader = BuildHeaderRow(frozenColumns);
                cornerHeader.style.width = GetColumnWidth(frozenColumns);
                cornerHeader.style.minWidth = GetColumnWidth(frozenColumns);
                cornerHeader.style.maxWidth = GetColumnWidth(frozenColumns);
                topRow.Add(cornerHeader);
            }

            m_HeaderScrollView = new ScrollView(ScrollViewMode.Horizontal);
            m_HeaderScrollView.style.flexGrow = 1f;
            m_HeaderScrollView.style.height = 30f;
            m_HeaderScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            m_HeaderScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_HeaderContainer = BuildHeaderRow(scrollableColumns);
            VisualElement headerContent = CreateScrollContent(GetColumnWidth(scrollableColumns));
            headerContent.Add(m_HeaderContainer);
            m_HeaderScrollView.Add(headerContent);
            topRow.Add(m_HeaderScrollView);
            container.Add(topRow);

            VisualElement bodyRow = new();
            bodyRow.style.flexGrow = 1f;
            bodyRow.style.flexDirection = FlexDirection.Row;

            if (frozenColumns.Count > 0)
            {
                m_FrozenRowsScrollView = new ScrollView(ScrollViewMode.Vertical);
                m_FrozenRowsScrollView.style.width = GetColumnWidth(frozenColumns);
                m_FrozenRowsScrollView.style.minWidth = GetColumnWidth(frozenColumns);
                m_FrozenRowsScrollView.style.maxWidth = GetColumnWidth(frozenColumns);
                m_FrozenRowsScrollView.style.flexShrink = 0f;
                m_FrozenRowsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                m_FrozenRowsScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                m_FrozenRowsScrollView.Add(CreateRowsContent(frozenColumns, out m_FrozenRowContainer));
                bodyRow.Add(m_FrozenRowsScrollView);
            }

            m_MainRowsScrollView = CreateMainScrollView();
            m_MainRowsScrollView.Add(CreateRowsContent(scrollableColumns, out m_RowContainer));
            bodyRow.Add(m_MainRowsScrollView);
            container.Add(bodyRow);
            return container;
        }

        private VisualElement BuildHeaderOnlyLayout(List<XDataTableEditorColumn> columns)
        {
            VisualElement container = new();
            container.style.flexGrow = 1f;
            container.style.flexDirection = FlexDirection.Column;

            m_HeaderScrollView = new ScrollView(ScrollViewMode.Horizontal);
            m_HeaderScrollView.style.height = 30f;
            m_HeaderScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            m_HeaderScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_HeaderContainer = BuildHeaderRow(columns);
            VisualElement headerContent = CreateScrollContent(GetColumnWidth(columns));
            headerContent.Add(m_HeaderContainer);
            m_HeaderScrollView.Add(headerContent);
            container.Add(m_HeaderScrollView);

            m_MainRowsScrollView = CreateMainScrollView();
            m_MainRowsScrollView.style.flexGrow = 1f;
            m_MainRowsScrollView.Add(CreateRowsContent(columns, out m_RowContainer));
            container.Add(m_MainRowsScrollView);
            return container;
        }

        private VisualElement BuildFrozenBodyWithoutHeader(
            List<XDataTableEditorColumn> frozenColumns,
            List<XDataTableEditorColumn> scrollableColumns)
        {
            VisualElement bodyRow = new();
            bodyRow.style.flexGrow = 1f;
            bodyRow.style.flexDirection = FlexDirection.Row;

            m_FrozenRowsScrollView = new ScrollView(ScrollViewMode.Vertical);
            m_FrozenRowsScrollView.style.width = GetColumnWidth(frozenColumns);
            m_FrozenRowsScrollView.style.minWidth = GetColumnWidth(frozenColumns);
            m_FrozenRowsScrollView.style.maxWidth = GetColumnWidth(frozenColumns);
            m_FrozenRowsScrollView.style.flexShrink = 0f;
            m_FrozenRowsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_FrozenRowsScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            VisualElement frozenContent = CreateScrollContent(GetColumnWidth(frozenColumns));
            frozenContent.Add(BuildHeaderRow(frozenColumns));
            frozenContent.Add(CreateRowColumnContainer(out m_FrozenRowContainer));
            m_FrozenRowsScrollView.Add(frozenContent);
            bodyRow.Add(m_FrozenRowsScrollView);

            m_MainRowsScrollView = CreateMainScrollView();
            VisualElement content = CreateScrollContent(GetColumnWidth(scrollableColumns));
            m_HeaderContainer = BuildHeaderRow(scrollableColumns);
            content.Add(m_HeaderContainer);
            m_RowContainer = CreateRowColumnContainer();
            content.Add(m_RowContainer);
            m_MainRowsScrollView.Add(content);
            bodyRow.Add(m_MainRowsScrollView);
            return bodyRow;
        }

        private ScrollView CreateMainScrollView()
        {
            ScrollView scrollView = new(ScrollViewMode.VerticalAndHorizontal);
            scrollView.style.flexGrow = 1f;
            return scrollView;
        }

        private VisualElement CreateScrollContent(float width)
        {
            VisualElement content = new();
            content.style.flexDirection = FlexDirection.Column;
            content.style.width = width;
            return content;
        }

        private VisualElement CreateRowsContent(List<XDataTableEditorColumn> columns, out VisualElement rowContainer)
        {
            VisualElement content = CreateScrollContent(GetColumnWidth(columns));
            rowContainer = CreateRowColumnContainer();
            content.Add(rowContainer);
            return content;
        }

        private VisualElement CreateRowColumnContainer()
        {
            return CreateRowColumnContainer(out _);
        }

        private VisualElement CreateRowColumnContainer(out VisualElement container)
        {
            container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            return container;
        }

        private VisualElement BuildHeaderRow(IReadOnlyList<XDataTableEditorColumn> columns)
        {
            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.height = TableRowHeight;
            header.style.minHeight = TableRowHeight;
            header.style.maxHeight = TableRowHeight;
            header.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            foreach (XDataTableEditorColumn column in columns)
            {
                Button button = new(() => ToggleSort(column))
                {
                    text = GetColumnTitle(column)
                };
                button.style.width = column.Width;
                button.style.minWidth = column.Width;
                button.style.maxWidth = column.Width;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginLeft = 0f;
                button.style.marginRight = 0f;
                button.style.borderLeftWidth = 0f;
                button.style.borderRightWidth = 1f;
                button.style.borderTopWidth = 0f;
                button.style.borderBottomWidth = 0f;
                button.style.paddingLeft = 6f;
                button.style.paddingRight = 6f;
                header.Add(button);
            }

            return header;
        }

        private void HookScrollSync()
        {
            if (m_FrozenRowsScrollView != null && m_MainRowsScrollView != null)
            {
                m_FrozenRowsScrollView.verticalScroller.valueChanged += _ => SyncVerticalScroll(m_FrozenRowsScrollView, m_MainRowsScrollView);
                m_MainRowsScrollView.verticalScroller.valueChanged += _ => SyncVerticalScroll(m_MainRowsScrollView, m_FrozenRowsScrollView);
            }

            if (m_HeaderScrollView != null && m_MainRowsScrollView != null)
            {
                m_HeaderScrollView.horizontalScroller.valueChanged += _ => SyncHorizontalScroll(m_HeaderScrollView, m_MainRowsScrollView);
                m_MainRowsScrollView.horizontalScroller.valueChanged += _ => SyncHorizontalScroll(m_MainRowsScrollView, m_HeaderScrollView);
            }
        }

        private void SyncVerticalScroll(ScrollView source, ScrollView target)
        {
            if (source == null || target == null || m_IsSyncingScroll)
            {
                return;
            }

            m_IsSyncingScroll = true;
            Vector2 offset = target.scrollOffset;
            offset.y = Mathf.Clamp(source.scrollOffset.y, 0f, Mathf.Max(0f, target.verticalScroller.highValue));
            target.scrollOffset = offset;
            m_IsSyncingScroll = false;
        }

        private void SyncHorizontalScroll(ScrollView source, ScrollView target)
        {
            if (source == null || target == null || m_IsSyncingScroll)
            {
                return;
            }

            m_IsSyncingScroll = true;
            Vector2 offset = target.scrollOffset;
            offset.x = Mathf.Clamp(source.scrollOffset.x, 0f, Mathf.Max(0f, target.horizontalScroller.highValue));
            target.scrollOffset = offset;
            m_IsSyncingScroll = false;
        }

        private float GetColumnWidth(IReadOnlyList<XDataTableEditorColumn> columns)
        {
            float width = 0f;
            foreach (XDataTableEditorColumn column in columns)
            {
                width += column.Width;
            }

            return width;
        }

        private List<XDataTableEditorColumn> GetFrozenColumns()
        {
            var result = new List<XDataTableEditorColumn>();
            foreach (XDataTableEditorColumn column in m_Model.Columns)
            {
                if (m_FreezeIdEnabled && IsIdColumn(column))
                {
                    result.Add(column);
                    continue;
                }

                if (m_FreezeAliasEnabled && IsAliasColumn(column))
                {
                    result.Add(column);
                }
            }

            return result;
        }

        private List<XDataTableEditorColumn> GetScrollableColumns(List<XDataTableEditorColumn> frozenColumns)
        {
            return m_Model.Columns.Where(column => !frozenColumns.Contains(column)).ToList();
        }

        private bool IsIdColumn(XDataTableEditorColumn column)
        {
            return column != null
                   && (column.IsKey || string.Equals(column.Field.Name, "id", StringComparison.OrdinalIgnoreCase));
        }

        private bool IsAliasColumn(XDataTableEditorColumn column)
        {
            return column != null
                   && (column.IsAlias || string.Equals(column.Field.Name, "alias", StringComparison.OrdinalIgnoreCase));
        }

        private bool HasAliasColumn()
        {
            return m_Model != null && m_Model.Columns.Any(IsAliasColumn);
        }

        private Button CreateToolbarButton(string text, Action onClick)
        {
            Button button = new(onClick)
            {
                text = text
            };
            button.style.marginRight = 6f;
            button.style.marginBottom = 4f;
            return button;
        }

        private Button CreateSmallRoundButton(string text, Action onClick)
        {
            Button button = new(onClick)
            {
                text = text
            };
            button.style.width = 22f;
            button.style.minWidth = 22f;
            button.style.maxWidth = 22f;
            button.style.height = 18f;
            button.style.minHeight = 18f;
            button.style.maxHeight = 18f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderTopLeftRadius = 9f;
            button.style.borderTopRightRadius = 9f;
            button.style.borderBottomLeftRadius = 9f;
            button.style.borderBottomRightRadius = 9f;
            button.style.flexShrink = 0f;
            return button;
        }

        private void RefreshValidation()
        {
            m_ValidationResult = m_Model?.Validate();
        }

        private void RefreshStatus()
        {
            if (m_Model == null)
            {
                if (m_IsUnionMode)
                {
                    if (m_TableSummaryLabel != null)
                    {
                        m_TableSummaryLabel.text = m_CurrentUnionTable != null
                            ? $"Union: {m_CurrentUnionTable.DisplayName}"
                            : "Union: 无";
                    }

                    if (m_IssueLabel != null)
                    {
                        m_IssueLabel.text = string.IsNullOrEmpty(m_UnionMessage) ? "请选择可用的子表。" : m_UnionMessage;
                        m_IssueLabel.style.color = new Color(0.96f, 0.55f, 0.55f);
                    }
                }

                return;
            }

            int blockingCount = m_ValidationResult?.Issues.Count(issue => issue.Blocking) ?? 0;
            int issueCount = m_ValidationResult?.Issues.Count ?? 0;
            m_IssueLabel.text = issueCount == 0
                ? "当前无校验问题。"
                : $"校验问题 {issueCount} 条，阻断保存 {blockingCount} 条。{m_ValidationResult.Issues[0].Message}";
            m_IssueLabel.style.color = issueCount == 0
                ? new Color(0.6f, 0.84f, 0.62f)
                : new Color(0.96f, 0.55f, 0.55f);

            string sortText = m_SortColumnIndex >= 0
                ? $" | 排序: {GetColumnTitle(m_Model.Columns[m_SortColumnIndex])} {(m_SortAscending ? "↑" : "↓")}"
                : string.Empty;
            string unionText = m_IsUnionMode && m_CurrentUnionTable != null
                ? $"Union: {m_CurrentUnionTable.DisplayName} | 子表: {m_Model.TableType.Name} | "
                : string.Empty;
            string assetText = !m_IsUnionMode && m_CurrentDataTableAsset != null
                ? $"资源: {m_CurrentDataTableAsset.DisplayName} | "
                : string.Empty;
            m_TableSummaryLabel.text = $"{unionText}{assetText}行数: {m_Model.Rows.Count}{sortText}";
        }

        private void RebuildHeader()
        {
            if (m_HeaderContainer == null)
            {
                return;
            }

            m_HeaderContainer.Clear();
            List<XDataTableEditorColumn> frozenColumns = GetFrozenColumns();
            List<XDataTableEditorColumn> headerColumns = GetScrollableColumns(frozenColumns);
            if (headerColumns.Count == 0)
            {
                headerColumns = m_Model.Columns;
            }

            foreach (XDataTableEditorColumn column in headerColumns)
            {
                Button button = new(() => ToggleSort(column))
                {
                    text = GetColumnTitle(column)
                };
                button.style.width = column.Width;
                button.style.minWidth = column.Width;
                button.style.maxWidth = column.Width;
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginLeft = 0f;
                button.style.marginRight = 0f;
                button.style.borderLeftWidth = 0f;
                button.style.borderRightWidth = 1f;
                button.style.borderTopWidth = 0f;
                button.style.borderBottomWidth = 0f;
                button.style.paddingLeft = 6f;
                button.style.paddingRight = 6f;
                m_HeaderContainer.Add(button);
            }
        }

        private void RebuildRows()
        {
            m_RowContainer?.Clear();
            m_FrozenRowContainer?.Clear();

            List<XDataTableEditorColumn> frozenColumns = GetFrozenColumns();
            List<XDataTableEditorColumn> scrollableColumns = GetScrollableColumns(frozenColumns);

            if (m_Model.Rows.Count == 0)
            {
                Label emptyLabel = new("暂无数据，点击“新增行”开始编辑。");
                emptyLabel.style.marginTop = 10f;
                emptyLabel.style.marginLeft = 6f;
                m_RowContainer?.Add(emptyLabel);
                return;
            }

            for (int rowIndex = 0; rowIndex < m_Model.Rows.Count; rowIndex++)
            {
                if (m_FrozenRowContainer != null && frozenColumns.Count > 0)
                {
                    m_FrozenRowContainer.Add(BuildRowElement(rowIndex, frozenColumns));
                }

                m_RowContainer?.Add(BuildRowElement(rowIndex, scrollableColumns.Count > 0 ? scrollableColumns : m_Model.Columns));
            }
        }

        private VisualElement BuildRowElement(int rowIndex, IReadOnlyList<XDataTableEditorColumn> columns)
        {
            XItemBox row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = TableRowHeight;
            row.style.minHeight = TableRowHeight;
            row.style.maxHeight = TableRowHeight;
            row.style.backgroundColor = GetRowBackgroundColor(rowIndex);

            row.RegisterCallback<MouseDownEvent>(_ => SelectRow(rowIndex));
            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("复制当前行", _ => DuplicateRow(rowIndex));
                evt.menu.AppendAction("删除当前行", _ => RemoveRow(rowIndex));
                evt.menu.AppendAction("上移", _ => MoveRow(rowIndex, -1));
                evt.menu.AppendAction("下移", _ => MoveRow(rowIndex, 1));
            }));

            foreach (XDataTableEditorColumn column in columns)
            {
                row.Add(BuildGridCell(rowIndex, column));
            }

            return row;
        }

        private VisualElement BuildGridCell(int rowIndex, XDataTableEditorColumn column)
        {
            VisualElement container = new();
            container.style.width = column.Width;
            container.style.minWidth = column.Width;
            container.style.maxWidth = column.Width;
            container.style.height = TableRowHeight;
            container.style.minHeight = TableRowHeight;
            container.style.maxHeight = TableRowHeight;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingLeft = 4f;
            container.style.paddingRight = 4f;
            container.style.paddingTop = 2f;
            container.style.paddingBottom = 2f;
            container.style.borderRightWidth = 1f;
            container.style.borderRightColor = new Color(0.24f, 0.24f, 0.24f, 0.85f);
            container.style.backgroundColor = GetCellBackgroundColor(rowIndex, column);
            container.RegisterCallback<MouseDownEvent>(_ => SelectRow(rowIndex));

            string issueMessage = GetCellIssueMessage(rowIndex, column);
            if (!string.IsNullOrEmpty(issueMessage))
            {
                container.tooltip = issueMessage;
            }

            VisualElement editor = CreateGridEditor(rowIndex, column);
            container.Add(editor);
            return container;
        }

        private VisualElement CreateGridEditor(int rowIndex, XDataTableEditorColumn column)
        {
            object value = m_Model.GetValue(rowIndex, column);
            string displayValue = m_Model.GetDisplayValue(rowIndex, column);

            switch (column.InlineKind)
            {
                case XDataTableEditorInlineKind.Text:
                {
                    TextField textField = new()
                    {
                        value = value as string ?? string.Empty
                    };
                    textField.isDelayed = true;
                    textField.tooltip = value as string ?? string.Empty;
                    ApplyInlineEditorStyle(textField);
                    textField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue));
                    return textField;
                }
                case XDataTableEditorInlineKind.Integer:
                {
                    IntegerField integerField = new()
                    {
                        value = value is int intValue ? intValue : 0
                    };
                    integerField.isDelayed = true;
                    ApplyInlineEditorStyle(integerField);
                    integerField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue));
                    return integerField;
                }
                case XDataTableEditorInlineKind.UnsignedInteger:
                {
                    LongField longField = new()
                    {
                        value = value is uint uintValue ? uintValue : 0L
                    };
                    longField.isDelayed = true;
                    ApplyInlineEditorStyle(longField);
                    longField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue));
                    return longField;
                }
                case XDataTableEditorInlineKind.Float:
                {
                    FloatField floatField = new()
                    {
                        value = value is float floatValue ? floatValue : 0f
                    };
                    floatField.isDelayed = true;
                    ApplyInlineEditorStyle(floatField);
                    floatField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue));
                    return floatField;
                }
                case XDataTableEditorInlineKind.Toggle:
                {
                    Toggle toggle = new()
                    {
                        value = value is bool boolValue && boolValue
                    };
                    ApplyInlineEditorStyle(toggle, grow: false);
                    toggle.style.alignSelf = Align.Center;
                    toggle.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue));
                    return toggle;
                }
                case XDataTableEditorInlineKind.Enum:
                {
                    EnumField enumField = new((Enum)value);
                    ApplyInlineEditorStyle(enumField);
                    enumField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue));
                    return enumField;
                }
                case XDataTableEditorInlineKind.AssetPath:
                {
                    ObjectField objectField = new()
                    {
                        objectType = column.AssetType ?? typeof(Object),
                        allowSceneObjects = false,
                        value = ResolveAsset(column, value as string)
                    };
                    ApplyInlineEditorStyle(objectField);
                    objectField.tooltip = value as string ?? string.Empty;
                    objectField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue));
                    return objectField;
                }
                case XDataTableEditorInlineKind.DataTableRef:
                {
                    return CreateDataTableRefEditor(rowIndex, column, scrollSelection: true);
                }
                default:
                {
                    Label label = new(displayValue);
                    ApplyInlineEditorStyle(label);
                    label.style.whiteSpace = WhiteSpace.NoWrap;
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                    label.tooltip = displayValue;
                    return label;
                }
            }
        }

        private void ApplyInlineEditorStyle(VisualElement element, bool grow = true)
        {
            if (element == null)
            {
                return;
            }

            if (grow)
            {
                element.style.flexGrow = 1f;
            }

            element.style.height = InlineEditorHeight;
            element.style.minHeight = InlineEditorHeight;
            element.style.maxHeight = InlineEditorHeight;
            element.style.marginTop = 0f;
            element.style.marginBottom = 0f;
        }

        private void RebuildDetailPanel()
        {
            if (!HasSelection())
            {
                return;
            }

            XDataTableRowDetailWindow.ShowWindow(this, m_SelectedRowIndex);
        }

        internal VisualElement BuildDetailField(int rowIndex, XDataTableEditorColumn column)
        {
            XBox container = new();
            container.style.paddingLeft = 6f;
            container.style.paddingRight = 6f;
            container.style.paddingTop = 6f;
            container.style.paddingBottom = 6f;
            container.style.marginBottom = 6f;

            Label titleLabel = new(GetColumnTitle(column));
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 4f;
            container.Add(titleLabel);

            if (column.IsList)
            {
                container.Add(BuildListEditor(rowIndex, column));
            }
            else if (column.IsAssetPath)
            {
                ObjectField objectField = new()
                {
                    objectType = column.AssetType ?? typeof(Object),
                    allowSceneObjects = false,
                    value = ResolveAsset(column, m_Model.GetValue(rowIndex, column) as string)
                };
                objectField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue, scrollSelection: false));
                container.Add(objectField);

                Label pathLabel = new(m_Model.GetValue(rowIndex, column) as string ?? string.Empty);
                pathLabel.style.marginTop = 4f;
                pathLabel.style.whiteSpace = WhiteSpace.Normal;
                pathLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                container.Add(pathLabel);
            }
            else if (column.IsDataTableRef)
            {
                container.Add(CreateDataTableRefEditor(rowIndex, column, scrollSelection: false));
            }
            else if (column.Field.FieldType == typeof(string))
            {
                TextField textField = new()
                {
                    value = m_Model.GetValue(rowIndex, column) as string ?? string.Empty,
                    multiline = true
                };
                textField.isDelayed = true;
                textField.style.minHeight = 68f;
                textField.RegisterValueChangedCallback(evt => ApplyValueChange(rowIndex, column, evt.newValue, scrollSelection: false));
                container.Add(textField);
            }
            else
            {
                VisualElement editor = CreateGridEditor(rowIndex, column);
                container.Add(editor);
            }

            string issueMessage = GetCellIssueMessage(rowIndex, column);
            if (!string.IsNullOrEmpty(issueMessage))
            {
                Label issueLabel = new(issueMessage);
                issueLabel.style.marginTop = 4f;
                issueLabel.style.whiteSpace = WhiteSpace.Normal;
                issueLabel.style.color = new Color(0.96f, 0.55f, 0.55f);
                container.Add(issueLabel);
            }

            return container;
        }

        private VisualElement CreateDataTableRefEditor(int rowIndex, XDataTableEditorColumn column, bool scrollSelection)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1f;
            row.style.height = InlineEditorHeight;
            row.style.minHeight = InlineEditorHeight;

            object keyValue = m_Model.GetValue(rowIndex, column);
            string displayText = m_Model.GetDisplayValue(rowIndex, column);
            if (string.IsNullOrEmpty(displayText))
            {
                displayText = "None";
            }

            VisualElement refField = new();
            refField.style.flexGrow = 1f;
            refField.style.height = InlineEditorHeight;
            refField.style.minHeight = InlineEditorHeight;
            refField.style.maxHeight = InlineEditorHeight;
            refField.style.marginRight = 4f;
            refField.style.paddingLeft = 6f;
            refField.style.paddingRight = 6f;
            refField.style.justifyContent = Justify.Center;
            refField.style.borderTopWidth = 1f;
            refField.style.borderBottomWidth = 1f;
            refField.style.borderLeftWidth = 1f;
            refField.style.borderRightWidth = 1f;
            refField.style.borderTopColor = new Color(0.32f, 0.32f, 0.32f, 0.95f);
            refField.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f, 0.95f);
            refField.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 0.95f);
            refField.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f, 0.95f);
            refField.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.85f);
            refField.tooltip = displayText;

            Label refLabel = new(displayText);
            refLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            refLabel.style.whiteSpace = WhiteSpace.NoWrap;
            refLabel.style.overflow = Overflow.Hidden;
            refLabel.style.textOverflow = TextOverflow.Ellipsis;
            refLabel.style.flexGrow = 1f;
            refField.Add(refLabel);

            refField.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                XDataTableRefResolver.PingReferencedTable(column?.DataTableRefMeta);
                if (evt.clickCount >= 2)
                {
                    XDataTableRefResolver.OpenReferencedTable(column?.DataTableRefMeta, keyValue, column?.Field?.FieldType);
                }

                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            row.Add(refField);

            Button pickerButton = CreateSmallRoundButton("◉", () =>
            {
                XDataTableRefPickerWindow.ShowWindow(column?.Header, column?.DataTableRefMeta, column?.Field?.FieldType, pickedValue =>
                {
                    ApplyValueChange(rowIndex, column, pickedValue, scrollSelection);
                }, position);
            });
            pickerButton.style.height = InlineEditorHeight;
            pickerButton.style.minHeight = InlineEditorHeight;
            pickerButton.style.maxHeight = InlineEditorHeight;
            pickerButton.tooltip = "选择引用";
            row.Add(pickerButton);

            return row;
        }

        private VisualElement BuildListEditor(int rowIndex, XDataTableEditorColumn column)
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Column;

            IList list = m_Model.GetValue(rowIndex, column) as IList;
            if (list == null)
            {
                list = Activator.CreateInstance(column.Field.FieldType) as IList;
                if (list != null)
                {
                    ApplyValueChange(rowIndex, column, list, scrollSelection: false);
                }
            }

            list ??= Array.Empty<object>();

            for (int i = 0; i < list.Count; i++)
            {
                int itemIndex = i;
                VisualElement row = new()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 4f
                    }
                };

                VisualElement editor = CreateListItemEditor(column.ListElementType, list[itemIndex], newValue =>
                {
                    if (TryConvertListValue(column.ListElementType, newValue, out object converted))
                    {
                        list[itemIndex] = converted;
                        MarkDirty();
                        RefreshValidation();
                        RefreshStatus();
                        RebuildRows();
                    }
                });
                editor.style.flexGrow = 1f;
                row.Add(editor);

                Button removeButton = new(() =>
                {
                    list.RemoveAt(itemIndex);
                    MarkDirty();
                    RefreshValidation();
                    RefreshStatus();
                    RebuildRows();
                    RebuildDetailPanel();
                })
                {
                    text = "删除"
                };
                removeButton.style.marginLeft = 6f;
                row.Add(removeButton);
                container.Add(row);
            }

            Button addButton = new(() =>
            {
                list.Add(CreateDefaultListItem(column.ListElementType));
                MarkDirty();
                RefreshValidation();
                RefreshStatus();
                RebuildRows();
                RebuildDetailPanel();
            })
            {
                text = "新增元素"
            };
            addButton.style.marginTop = 4f;
            container.Add(addButton);

            if (list.Count == 0)
            {
                Label emptyLabel = new("空列表");
                emptyLabel.style.marginBottom = 4f;
                emptyLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                container.Insert(0, emptyLabel);
            }

            return container;
        }

        private VisualElement CreateListItemEditor(Type elementType, object value, Action<object> onChanged)
        {
            if (elementType == typeof(string))
            {
                TextField field = new() { value = value as string ?? string.Empty };
                field.isDelayed = true;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (elementType == typeof(int))
            {
                IntegerField field = new() { value = value is int intValue ? intValue : 0 };
                field.isDelayed = true;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (elementType == typeof(uint))
            {
                LongField field = new() { value = value is uint uintValue ? uintValue : 0L };
                field.isDelayed = true;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (elementType == typeof(float))
            {
                FloatField field = new() { value = value is float floatValue ? floatValue : 0f };
                field.isDelayed = true;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (elementType == typeof(bool))
            {
                Toggle field = new() { value = value is bool boolValue && boolValue };
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            if (elementType != null && elementType.IsEnum)
            {
                EnumField field = new((Enum)value);
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            return new Label(value?.ToString() ?? string.Empty);
        }

        private bool TryConvertListValue(Type elementType, object rawValue, out object converted)
        {
            converted = rawValue;
            if (elementType == typeof(uint))
            {
                if (rawValue is long longValue && longValue >= 0 && longValue <= uint.MaxValue)
                {
                    converted = (uint)longValue;
                    return true;
                }

                ShowNotification(new GUIContent("uint 列表项超出范围"));
                return false;
            }

            return true;
        }

        private object CreateDefaultListItem(Type elementType)
        {
            if (elementType == null)
            {
                return null;
            }

            if (elementType == typeof(string))
            {
                return string.Empty;
            }

            if (elementType.IsValueType)
            {
                return Activator.CreateInstance(elementType);
            }

            return null;
        }

        private void ApplyValueChange(int rowIndex, XDataTableEditorColumn column, object newValue, bool scrollSelection = true)
        {
            if (!m_Model.TrySetValue(rowIndex, column, newValue, out string error))
            {
                m_CellErrors[(rowIndex, column.Index)] = error;
            }
            else
            {
                m_CellErrors.Remove((rowIndex, column.Index));
                MarkDirty();
            }

            RefreshValidation();
            RefreshStatus();
            RebuildRows();
            RebuildDetailPanel();
            if (scrollSelection)
            {
                ScrollToSelectedRow();
            }
        }

        private void SaveCurrentAsset()
        {
            if (m_Model == null)
            {
                ShowNotification(new GUIContent(string.IsNullOrEmpty(m_UnionMessage) ? "当前没有可保存的数据表。" : m_UnionMessage));
                return;
            }

            RefreshValidation();
            if (m_ValidationResult.HasBlockingIssues)
            {
                string message = string.Join("\n", m_ValidationResult.Issues
                    .Where(issue => issue.Blocking)
                    .Take(8)
                    .Select(issue => issue.RowIndex >= 0
                        ? $"Row {issue.RowIndex + 1} / {issue.Column?.Field.Name}: {issue.Message}"
                        : $"Column {issue.Column?.Field.Name}: {issue.Message}"));
                EditorUtility.DisplayDialog("保存被阻止", message, "OK");
                RefreshStatus();
                return;
            }

            try
            {
                m_Model.Save();
                m_IsDirty = false;
                ShowNotification(new GUIContent("XDataTable 已保存"));
                RefreshStatus();
                UpdateWindowTitle();
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                EditorUtility.DisplayDialog("保存失败", exception.Message, "OK");
            }
        }

        private void ReloadFromDisk()
        {
            if (m_Model == null)
            {
                if (m_IsUnionMode)
                {
                    ReloadUnionFromDisk();
                }

                return;
            }

            if (m_IsDirty && !EditorUtility.DisplayDialog("重载数据", "当前有未保存修改，确认从磁盘重载？", "重载", "取消"))
            {
                return;
            }

            if (m_IsUnionMode)
            {
                ReloadUnionFromDisk();
            }
            else
            {
                LoadTextAsset(m_SourceAsset);
            }
        }

        private void AddRow()
        {
            if (m_Model == null)
            {
                ShowNotification(new GUIContent(string.IsNullOrEmpty(m_UnionMessage) ? "当前没有可编辑的数据表。" : m_UnionMessage));
                return;
            }

            m_Model.AddRow();
            m_SelectedRowIndex = m_Model.Rows.Count - 1;
            MarkDirty();
            RefreshValidation();
            RefreshStatus();
            RebuildRows();
            RebuildDetailPanel();
            ScrollToSelectedRow();
        }

        private void DuplicateSelectedRow()
        {
            if (!HasSelection())
            {
                return;
            }

            DuplicateRow(m_SelectedRowIndex);
        }

        private void DuplicateRow(int rowIndex)
        {
            m_Model.DuplicateRow(rowIndex);
            m_SelectedRowIndex = Mathf.Clamp(rowIndex + 1, 0, m_Model.Rows.Count - 1);
            MarkDirty();
            RefreshValidation();
            RefreshStatus();
            RebuildRows();
            RebuildDetailPanel();
            ScrollToSelectedRow();
        }

        private void RemoveSelectedRow()
        {
            if (!HasSelection())
            {
                return;
            }

            RemoveRow(m_SelectedRowIndex);
        }

        private void RemoveRow(int rowIndex)
        {
            m_Model.RemoveRow(rowIndex);
            if (m_Model.Rows.Count == 0)
            {
                m_SelectedRowIndex = -1;
            }
            else
            {
                m_SelectedRowIndex = Mathf.Clamp(rowIndex, 0, m_Model.Rows.Count - 1);
            }

            MarkDirty();
            RefreshValidation();
            RefreshStatus();
            RebuildRows();
            RebuildDetailPanel();
        }

        private void MoveSelectedRow(int direction)
        {
            if (!HasSelection())
            {
                return;
            }

            MoveRow(m_SelectedRowIndex, direction);
        }

        private void MoveRow(int rowIndex, int direction)
        {
            int targetIndex = Mathf.Clamp(rowIndex + direction, 0, Mathf.Max(m_Model.Rows.Count - 1, 0));
            m_Model.MoveRow(rowIndex, direction);
            m_SelectedRowIndex = targetIndex;
            MarkDirty();
            RefreshValidation();
            RefreshStatus();
            RebuildRows();
            RebuildDetailPanel();
            ScrollToSelectedRow();
        }

        private void JumpToRow()
        {
            string searchText = m_LocateSearchField?.value;
            bool supportsKey = m_Model.SupportsKey;
            bool supportsAlias = m_Model.SupportsAlias;

            if (supportsKey && m_Model.TryFindRowByKey(searchText, out int keyRowIndex, out _))
            {
                SelectRow(keyRowIndex, true);
                return;
            }

            if (supportsAlias && m_Model.TryFindRowByAlias(searchText, out int aliasRowIndex, out _))
            {
                SelectRow(aliasRowIndex, true);
                return;
            }

            if (supportsKey && !supportsAlias)
            {
                m_Model.TryFindRowByKey(searchText, out _, out string keyError);
                ShowNotification(new GUIContent(keyError));
                return;
            }

            if (!supportsKey && supportsAlias)
            {
                m_Model.TryFindRowByAlias(searchText, out _, out string aliasError);
                ShowNotification(new GUIContent(aliasError));
                return;
            }

            ShowNotification(new GUIContent("未找到匹配的 Key 或 Alias。"));
        }

        private void ToggleSort(XDataTableEditorColumn column)
        {
            if (column == null)
            {
                return;
            }

            object selectedRow = HasSelection() ? m_Model.Rows[m_SelectedRowIndex] : null;
            if (m_SortColumnIndex == column.Index)
            {
                m_SortAscending = !m_SortAscending;
            }
            else
            {
                m_SortColumnIndex = column.Index;
                m_SortAscending = true;
            }

            m_Model.SortByColumn(column, m_SortAscending);
            if (selectedRow != null)
            {
                m_SelectedRowIndex = m_Model.Rows.IndexOf(selectedRow);
            }
            RefreshValidation();
            BuildUI();
        }

        private void SelectRow(int rowIndex, bool scrollSelection = false)
        {
            if (rowIndex < 0 || rowIndex >= m_Model.Rows.Count)
            {
                return;
            }

            m_SelectedRowIndex = rowIndex;
            RebuildRows();
            RebuildDetailPanel();
            RefreshActionState();
            if (scrollSelection)
            {
                ScrollToSelectedRow();
            }
        }

        private void TryLocateByKeyValue(object keyValue)
        {
            if (m_Model == null || keyValue == null)
            {
                return;
            }

            if (m_Model.TryFindRowByKeyValue(keyValue, out int rowIndex, out _))
            {
                SelectRow(rowIndex, true);
            }
        }

        private void ScrollToSelectedRow()
        {
            if (!HasSelection() || m_RowContainer == null || m_SelectedRowIndex >= m_RowContainer.childCount)
            {
                return;
            }

            VisualElement target = m_RowContainer[m_SelectedRowIndex];
            if (m_TableScrollView != null)
            {
                m_TableScrollView.ScrollTo(target);
                return;
            }

            m_MainRowsScrollView?.ScrollTo(target);
        }

        internal void ApplyRowValueChangeFromDetail(int rowIndex, object newRowValue)
        {
            if (m_Model == null || rowIndex < 0 || rowIndex >= m_Model.Rows.Count)
            {
                return;
            }

            m_Model.Rows[rowIndex] = newRowValue;
            MarkDirty();
            RefreshValidation();
            RefreshStatus();
            RebuildRows();
        }

        private void MarkDirty()
        {
            m_IsDirty = true;
            UpdateWindowTitle();
        }

        private void RefreshActionState()
        {
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            string title;
            if (m_Model != null)
            {
                title = m_IsUnionMode && m_CurrentUnionTable != null
                    ? $"{m_CurrentUnionTable.TableType.Name}/{m_Model.TableType.Name}{(m_IsDirty ? "*" : string.Empty)}"
                    : $"{m_Model.TableType.Name}{(m_IsDirty ? "*" : string.Empty)}";
            }
            else
            {
                title = m_IsUnionMode ? "Union XDataTable" : "XDataTable";
            }

            titleContent = new GUIContent(title);
        }

        private bool HasSelection()
        {
            return m_Model != null && m_SelectedRowIndex >= 0 && m_SelectedRowIndex < m_Model.Rows.Count;
        }

        private string GetColumnTitle(XDataTableEditorColumn column)
        {
            string suffix = column.Index == m_SortColumnIndex ? (m_SortAscending ? " ↑" : " ↓") : string.Empty;
            if (column.IsAlias)
            {
                return $"Alias:{column.Header}{suffix}";
            }

            if (column.IsKey)
            {
                return $"Key:{column.Header}{suffix}";
            }

            return $"{column.Header}{suffix}";
        }

        private Color GetRowBackgroundColor(int rowIndex)
        {
            if (rowIndex == m_SelectedRowIndex)
            {
                return new Color(0.2f, 0.38f, 0.55f, 0.42f);
            }

            return rowIndex % 2 == 0
                ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                : new Color(0.31f, 0.31f, 0.31f, 0.18f);
        }

        private Color GetCellBackgroundColor(int rowIndex, XDataTableEditorColumn column)
        {
            if (!string.IsNullOrEmpty(GetCellIssueMessage(rowIndex, column)))
            {
                return new Color(0.55f, 0.16f, 0.16f, 0.36f);
            }

            if (column.IsKey)
            {
                return new Color(0.16f, 0.22f, 0.34f, 0.28f);
            }

            if (column.IsAlias)
            {
                return new Color(0.18f, 0.28f, 0.2f, 0.24f);
            }

            return Color.clear;
        }

        private string GetCellIssueMessage(int rowIndex, XDataTableEditorColumn column)
        {
            string validationMessage = m_ValidationResult?.GetIssueMessage(rowIndex, column);
            if (m_CellErrors.TryGetValue((rowIndex, column.Index), out string runtimeError))
            {
                if (string.IsNullOrEmpty(validationMessage))
                {
                    return runtimeError;
                }

                return $"{runtimeError}\n{validationMessage}";
            }

            return validationMessage;
        }

        private Object ResolveAsset(XDataTableEditorColumn column, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath(assetPath, column.AssetType ?? typeof(Object));
        }

        private void SelectUnionChild(XUnionDataTableChildInfo child)
        {
            if (child == null || child == m_CurrentUnionChild)
            {
                BuildUI();
                return;
            }

            if (!ConfirmDiscardOrSaveDirtyChanges())
            {
                BuildUI();
                return;
            }

            m_CurrentUnionChild = child;
            LoadCurrentUnionChild();
            BuildUI();
        }

        private void SelectDataTableAsset(XDataTableAssetInfo assetInfo)
        {
            if (assetInfo == null || assetInfo.Asset == null)
            {
                BuildUI();
                return;
            }

            if (assetInfo == m_CurrentDataTableAsset
                || string.Equals(assetInfo.Guid, m_SourceAssetGuid, StringComparison.Ordinal))
            {
                BuildUI();
                return;
            }

            if (!ConfirmDiscardOrSaveDirtyChanges("切换 DataTable 资源"))
            {
                BuildUI();
                return;
            }

            LoadTextAssetInternal(assetInfo.Asset);
            BuildUI();
        }

        private void ReloadUnionFromDisk()
        {
            if (!ConfirmDiscardOrSaveDirtyChanges())
            {
                return;
            }

            LoadUnionMode(null);
        }

        private bool ConfirmDiscardOrSaveDirtyChanges(string title = "切换 Union DataTable")
        {
            if (!m_IsDirty)
            {
                return true;
            }

            int option = EditorUtility.DisplayDialogComplex(
                title,
                "当前数据表有未保存修改，切换前要如何处理？",
                "保存",
                "取消",
                "丢弃");

            switch (option)
            {
                case 0:
                    SaveCurrentAsset();
                    return !m_IsDirty;
                case 2:
                    m_IsDirty = false;
                    return true;
                default:
                    return false;
            }
        }

        private XUnionDataTableEditorInfo ResolveUnionTable(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || m_UnionTables == null)
            {
                return null;
            }

            return m_UnionTables.FirstOrDefault(table =>
                string.Equals(table.TableType.AssemblyQualifiedName, typeName, StringComparison.Ordinal)
                || string.Equals(table.TableType.FullName, typeName, StringComparison.Ordinal));
        }

        private static XUnionDataTableChildInfo ResolveUnionChild(XUnionDataTableEditorInfo unionTable, string typeName)
        {
            if (unionTable == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            return unionTable.Children.FirstOrDefault(child =>
                string.Equals(child.TableType.AssemblyQualifiedName, typeName, StringComparison.Ordinal)
                || string.Equals(child.TableType.FullName, typeName, StringComparison.Ordinal));
        }

        private static List<XUnionDataTableEditorInfo> DiscoverUnionDataTables()
        {
            return Utility.Reflection.GetTypesInAllAssemblies(IsConcreteUnionDataTable)
                .Select(BuildUnionDataTableInfo)
                .OrderBy(table => table.DisplayName)
                .ToList();
        }

        private static bool IsConcreteUnionDataTable(Type type)
        {
            return type != null
                && type.IsClass
                && !type.IsAbstract
                && type.GetCustomAttribute<UnionDataTablesAttribute>(false) != null
                && IsUnionDataTableType(type);
        }

        private static bool IsUnionDataTableType(Type type)
        {
            return Utility.Reflection.IsSubclassOfGeneric(type, typeof(XUnionDataTable<,>))
                || Utility.Reflection.IsSubclassOfGeneric(type, typeof(XUnionDataTableHasKey<,,>))
                || Utility.Reflection.IsSubclassOfGeneric(type, typeof(XUnionDataTableHasAlias<,,>));
        }

        private static XUnionDataTableEditorInfo BuildUnionDataTableInfo(Type unionTableType)
        {
            UnionDataTablesAttribute attribute = unionTableType.GetCustomAttribute<UnionDataTablesAttribute>(false);
            var children = new List<XUnionDataTableChildInfo>();
            if (attribute?.tableTypes != null)
            {
                foreach (Type childTableType in attribute.tableTypes)
                {
                    children.AddRange(BuildUnionChildInfos(unionTableType, childTableType));
                }
            }

            return new XUnionDataTableEditorInfo(unionTableType, children);
        }

        private static IEnumerable<XUnionDataTableChildInfo> BuildUnionChildInfos(Type unionTableType, Type childTableType)
        {
            if (childTableType == null)
            {
                yield return new XUnionDataTableChildInfo(null, null, string.Empty, 0, "空子表类型");
                yield break;
            }

            if (!typeof(XDataTable).IsAssignableFrom(childTableType))
            {
                yield return new XUnionDataTableChildInfo(childTableType, null, string.Empty, 0,
                    $"{childTableType.FullName} 不是 XDataTable。");
                yield break;
            }

            if (childTableType.GetCustomAttribute<TargetDataType>(false)?.targetType == null)
            {
                yield return new XUnionDataTableChildInfo(childTableType, null, string.Empty, 0,
                    $"{childTableType.FullName} 缺少 TargetDataType。");
                yield break;
            }

            DataResourcePath pathAttribute = childTableType.GetCustomAttribute<DataResourcePath>(false);
            string[] childPaths = pathAttribute?.GetPaths()
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray() ?? Array.Empty<string>();
            if (childPaths.Length == 0)
            {
                yield return new XUnionDataTableChildInfo(childTableType, null, string.Empty, 0,
                    $"{childTableType.FullName} 缺少 DataResourcePath。");
                yield break;
            }

            for (int i = 0; i < childPaths.Length; i++)
            {
                string childPath = childPaths[i];
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(childPath);
                if (asset == null)
                {
                    yield return new XUnionDataTableChildInfo(childTableType, null, childPath, i,
                        $"{unionTableType.Name} 的子表 {childTableType.Name} 找不到资源: {childPath}");
                    continue;
                }

                yield return new XUnionDataTableChildInfo(childTableType, asset, childPath, i, null);
            }
        }

        private static List<XDataTableAssetInfo> BuildDataTableAssetInfos(Type tableType)
        {
            var result = new List<XDataTableAssetInfo>();
            DataResourcePath pathAttribute = tableType?.GetCustomAttribute<DataResourcePath>(false);
            if (pathAttribute == null)
            {
                return result;
            }

            string[] paths = pathAttribute.GetPaths();
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                result.Add(new XDataTableAssetInfo(tableType, path, asset, i));
            }

            return result;
        }

        private static XDataTableAssetInfo ResolveDataTableAsset(IReadOnlyList<XDataTableAssetInfo> assets, string guid)
        {
            if (assets == null || string.IsNullOrEmpty(guid))
            {
                return null;
            }

            return assets.FirstOrDefault(asset => string.Equals(asset.Guid, guid, StringComparison.Ordinal));
        }

        private static Type ResolveDataTableType(TextAsset textAsset)
        {
            string assetPath = textAsset != null ? AssetDatabase.GetAssetPath(textAsset) : string.Empty;
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            IEnumerable<Type> tableTypes = Utility.Reflection.GetGenericTypes(typeof(XDataTable<>), 5, "Assembly-CSharp", "XFrameworkRuntime");
            foreach (Type tableType in tableTypes)
            {
                DataResourcePath pathAttribute = tableType.GetCustomAttribute<DataResourcePath>(false);
                if (pathAttribute == null)
                {
                    continue;
                }

                foreach (string path in pathAttribute.GetPaths())
                {
                    if (string.Equals(path, assetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return tableType;
                    }
                }
            }

            return null;
        }

        private static XDataTableEditorWindow FindOpenWindow(TextAsset textAsset)
        {
            string assetPath = textAsset != null ? AssetDatabase.GetAssetPath(textAsset) : string.Empty;
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            Type tableType = ResolveDataTableType(textAsset);
            string typeName = tableType?.AssemblyQualifiedName;
            XDataTableEditorWindow[] windows = Resources.FindObjectsOfTypeAll<XDataTableEditorWindow>();
            foreach (XDataTableEditorWindow window in windows)
            {
                if (window == null)
                {
                    continue;
                }

                if (string.Equals(window.SourceAssetPath, assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return window;
                }

                if (!window.m_IsUnionMode
                    && !string.IsNullOrEmpty(typeName)
                    && (string.Equals(window.m_DataTableTypeName, typeName, StringComparison.Ordinal)
                        || string.Equals(window.m_Model?.TableType.AssemblyQualifiedName, typeName, StringComparison.Ordinal)))
                {
                    return window;
                }
            }

            return null;
        }

        private static XDataTableEditorWindow FindOpenUnionWindow(Type unionTableType)
        {
            if (unionTableType == null)
            {
                return null;
            }

            string typeName = unionTableType.AssemblyQualifiedName;
            XDataTableEditorWindow[] windows = Resources.FindObjectsOfTypeAll<XDataTableEditorWindow>();
            foreach (XDataTableEditorWindow window in windows)
            {
                if (window == null || !window.m_IsUnionMode)
                {
                    continue;
                }

                if (string.Equals(window.m_UnionTableTypeName, typeName, StringComparison.Ordinal)
                    || string.Equals(window.m_CurrentUnionTable?.TableType.AssemblyQualifiedName, typeName, StringComparison.Ordinal))
                {
                    return window;
                }
            }

            return null;
        }

        private static XDataTableEditorWindow CreateDockedWindow()
        {
            Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType != null)
            {
                return CreateWindow<XDataTableEditorWindow>("XDataTable", typeof(SceneView), gameViewType);
            }

            return CreateWindow<XDataTableEditorWindow>("XDataTable", typeof(SceneView));
        }

        internal XDataTableEditorModel Model => m_Model;
        internal int SelectedRowIndex => m_SelectedRowIndex;
        internal bool HasSelectedRow => HasSelection();
        internal string SourceAssetPath => m_SourceAsset != null ? AssetDatabase.GetAssetPath(m_SourceAsset) : string.Empty;

        private sealed class XUnionDataTableEditorInfo
        {
            public XUnionDataTableEditorInfo(Type tableType, List<XUnionDataTableChildInfo> children)
            {
                TableType = tableType;
                Children = children ?? new List<XUnionDataTableChildInfo>();
            }

            public Type TableType { get; }
            public List<XUnionDataTableChildInfo> Children { get; }
            public string DisplayName => TableType?.Name ?? "Missing Union";
        }

        private sealed class XUnionDataTableChildInfo
        {
            public XUnionDataTableChildInfo(Type tableType, TextAsset asset, string path, int pathIndex, string error)
            {
                TableType = tableType;
                Asset = asset;
                Path = path;
                PathIndex = pathIndex;
                Error = error;
            }

            public Type TableType { get; }
            public TextAsset Asset { get; }
            public string Path { get; }
            public int PathIndex { get; }
            public string Error { get; }
            public string DisplayName => TableType == null
                ? "Missing Child"
                : string.IsNullOrEmpty(Path)
                    ? string.IsNullOrEmpty(Error) ? TableType.Name : $"{TableType.Name} (!)"
                    : string.IsNullOrEmpty(Error)
                        ? System.IO.Path.GetFileNameWithoutExtension(Path)
                        : $"{System.IO.Path.GetFileNameWithoutExtension(Path)} (!)";
        }

        private sealed class XDataTableAssetInfo
        {
            public XDataTableAssetInfo(Type tableType, string path, TextAsset asset, int index)
            {
                TableType = tableType;
                Path = path;
                Asset = asset;
                Index = index;
                Guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            }

            public Type TableType { get; }
            public string Path { get; }
            public TextAsset Asset { get; }
            public int Index { get; }
            public string Guid { get; }
            public string DisplayName => string.IsNullOrEmpty(Path)
                ? $"Asset {Index + 1}"
                : System.IO.Path.GetFileNameWithoutExtension(Path);
        }
    }
}
