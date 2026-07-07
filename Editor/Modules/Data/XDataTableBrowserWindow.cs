using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Data;

namespace XFramework.Editor
{
    public class XDataTableBrowserWindow : EditorWindow
    {
        private readonly List<XDataTableBrowserItem> m_Items = new();
        private ScrollView m_ListView;
        private Label m_StatusLabel;

        [MenuItem("XFramework/Data/Open Data Table")]
        public static void ShowWindow()
        {
            XDataTableBrowserWindow window = GetWindow<XDataTableBrowserWindow>("Data Table");
            window.minSize = new Vector2(760f, 520f);
            window.RefreshItems();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            RefreshItems();
        }

        public void CreateGUI()
        {
            BuildUI();
        }

        private void RefreshItems()
        {
            m_Items.Clear();
            m_Items.AddRange(DiscoverDataTables());
            m_Items.AddRange(DiscoverUnionDataTables());
            m_Items.Sort((left, right) =>
            {
                int categoryResult = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
                return categoryResult != 0
                    ? categoryResult
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            });

            if (rootVisualElement.panel != null)
            {
                RebuildList();
                RefreshStatus();
            }
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            VisualElement toolbar = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };

            Button refreshButton = new(RefreshItems)
            {
                text = "重载"
            };
            refreshButton.tooltip = "重新扫描所有普通 DataTable 和 Union DataTable";
            refreshButton.style.marginRight = 8f;
            toolbar.Add(refreshButton);

            m_StatusLabel = new Label();
            m_StatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(m_StatusLabel);
            root.Add(toolbar);

            VisualElement header = BuildHeaderRow();
            root.Add(header);

            m_ListView = new ScrollView();
            m_ListView.style.flexGrow = 1f;
            root.Add(m_ListView);

            RebuildList();
            RefreshStatus();
        }

        private VisualElement BuildHeaderRow()
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 26f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    marginBottom = 2f,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.4f)
                }
            };

            row.Add(CreateHeaderLabel("类型", 90f));
            row.Add(CreateHeaderLabel("表名", 220f));
            row.Add(CreateHeaderLabel("资源/子表", 1f, true));
            return row;
        }

        private Label CreateHeaderLabel(string text, float widthOrGrow, bool grow = false)
        {
            Label label = new(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexShrink = 0f;
            label.style.marginRight = 8f;
            if (grow)
            {
                label.style.flexGrow = widthOrGrow;
            }
            else
            {
                label.style.width = widthOrGrow;
            }

            return label;
        }

        private void RebuildList()
        {
            if (m_ListView == null)
            {
                return;
            }

            m_ListView.Clear();
            if (m_Items.Count == 0)
            {
                Label emptyLabel = new("未找到 DataTable。");
                emptyLabel.style.marginTop = 12f;
                emptyLabel.style.color = new Color(0.96f, 0.55f, 0.55f);
                m_ListView.Add(emptyLabel);
                return;
            }

            for (int i = 0; i < m_Items.Count; i++)
            {
                m_ListView.Add(BuildItemRow(m_Items[i], i));
            }
        }

        private VisualElement BuildItemRow(XDataTableBrowserItem item, int index)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 30f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    marginBottom = 1f,
                    backgroundColor = index % 2 == 0
                        ? new Color(0.24f, 0.24f, 0.24f, 0.08f)
                        : new Color(0.31f, 0.31f, 0.31f, 0.18f)
                }
            };
            row.tooltip = "双击打开";
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount >= 2)
                {
                    OpenItem(item);
                    evt.StopPropagation();
                }
            });

            Label categoryLabel = new(item.Category);
            categoryLabel.style.width = 90f;
            categoryLabel.style.marginRight = 8f;
            categoryLabel.style.flexShrink = 0f;
            row.Add(categoryLabel);

            Label nameLabel = new(item.DisplayName);
            nameLabel.style.width = 220f;
            nameLabel.style.marginRight = 8f;
            nameLabel.style.flexShrink = 0f;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(nameLabel);

            Label detailLabel = new(item.Detail);
            detailLabel.style.flexGrow = 1f;
            detailLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(detailLabel);

            Button openButton = new(() => OpenItem(item))
            {
                text = "打开"
            };
            openButton.style.marginLeft = 8f;
            openButton.style.flexShrink = 0f;
            row.Add(openButton);

            return row;
        }

        private void RefreshStatus()
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            int dataCount = m_Items.Count(item => item.Kind == XDataTableBrowserItemKind.DataTable);
            int unionCount = m_Items.Count(item => item.Kind == XDataTableBrowserItemKind.UnionDataTable);
            m_StatusLabel.text = $"普通表 {dataCount} 个 | Union 表 {unionCount} 个";
        }

        private static void OpenItem(XDataTableBrowserItem item)
        {
            switch (item.Kind)
            {
                case XDataTableBrowserItemKind.DataTable:
                    if (item.Asset != null)
                    {
                        XDataTableEditorWindow.ShowWindow(item.Asset);
                    }

                    break;
                case XDataTableBrowserItemKind.UnionDataTable:
                    XDataTableEditorWindow.ShowUnionDataTableWindow(item.TableType);
                    break;
            }
        }

        private static IEnumerable<XDataTableBrowserItem> DiscoverDataTables()
        {
            IEnumerable<Type> tableTypes = Utility.Reflection.GetGenericTypes(typeof(XDataTable<>), 5, "Assembly-CSharp", "XFrameworkRuntime");
            foreach (Type tableType in tableTypes)
            {
                DataResourcePath pathAttribute = tableType.GetCustomAttribute<DataResourcePath>(false);
                if (pathAttribute == null)
                {
                    continue;
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
                    string tableDisplayName = GetTableDisplayName(tableType);
                    string displayName = paths.Length > 1 ? $"{tableDisplayName} [{i + 1}]" : tableDisplayName;
                    string detail = asset != null ? path : $"资源缺失: {path}";
                    yield return new XDataTableBrowserItem(
                        XDataTableBrowserItemKind.DataTable,
                        "普通",
                        displayName,
                        detail,
                        tableType,
                        asset);
                }
            }
        }

        private static IEnumerable<XDataTableBrowserItem> DiscoverUnionDataTables()
        {
            return Utility.Reflection.GetTypesInAllAssemblies(IsConcreteUnionDataTable)
                .OrderBy(type => type.Name)
                .Select(type =>
                {
                    UnionDataTablesAttribute attribute = type.GetCustomAttribute<UnionDataTablesAttribute>(false);
                    string detail = attribute?.tableTypes == null || attribute.tableTypes.Length == 0
                        ? "无子表"
                        : string.Join(", ", attribute.tableTypes.Select(GetTableDisplayName));
                    return new XDataTableBrowserItem(
                        XDataTableBrowserItemKind.UnionDataTable,
                        "Union",
                        GetTableDisplayName(type),
                        detail,
                        type,
                        null);
                });
        }

        private static string GetTableDisplayName(Type tableType)
        {
            if (tableType == null)
            {
                return "Missing Child";
            }

            DataTableInfoAttribute attribute = tableType.GetCustomAttribute<DataTableInfoAttribute>(true);
            return string.IsNullOrWhiteSpace(attribute?.showName) ? tableType.Name : attribute.showName;
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

        private enum XDataTableBrowserItemKind
        {
            DataTable,
            UnionDataTable
        }

        private sealed class XDataTableBrowserItem
        {
            public XDataTableBrowserItem(
                XDataTableBrowserItemKind kind,
                string category,
                string displayName,
                string detail,
                Type tableType,
                TextAsset asset)
            {
                Kind = kind;
                Category = category;
                DisplayName = displayName;
                Detail = detail;
                TableType = tableType;
                Asset = asset;
            }

            public XDataTableBrowserItemKind Kind { get; }
            public string Category { get; }
            public string DisplayName { get; }
            public string Detail { get; }
            public Type TableType { get; }
            public TextAsset Asset { get; }
        }
    }
}
