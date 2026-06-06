using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.UI;

namespace XFramework.Editor
{
    public class UIToolkitPanelPreviewWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/UI/UI Toolkit Panel Preview";

        private readonly List<PanelPreviewItem> m_Items = new();
        private ScrollView m_ListView;
        private VisualElement m_PreviewRoot;
        private Label m_StatusLabel;
        private Label m_PreviewTitleLabel;
        private TextField m_SearchField;
        private GameObject m_TemporaryPanelObject;
        private PanelPreviewItem m_SelectedItem;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<UIToolkitPanelPreviewWindow>();
            window.titleContent = new GUIContent("UI Toolkit Panel Preview");
            window.minSize = new Vector2(980f, 560f);
            window.RefreshItems();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            RefreshItems();
        }

        private void OnDisable()
        {
            DestroyTemporaryPanel();
        }

        public void CreateGUI()
        {
            BuildUI();
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

            root.Add(BuildToolbar());

            var splitView = new TwoPaneSplitView(0, 360, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            root.Add(splitView);

            splitView.Add(BuildListPane());
            splitView.Add(BuildPreviewPane());

            RebuildList();
            RefreshStatus();
            if (m_SelectedItem != null)
            {
                RenderPreview(m_SelectedItem);
            }
        }

        private VisualElement BuildToolbar()
        {
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
                text = "刷新"
            };
            refreshButton.tooltip = "重新扫描所有 UIToolkitPanelBase 面板";
            refreshButton.style.marginRight = 8f;
            toolbar.Add(refreshButton);

            m_SearchField = new TextField();
            m_SearchField.style.width = 260f;
            m_SearchField.tooltip = "按面板名、类型名或路径过滤";
            m_SearchField.RegisterValueChangedCallback(_ => RebuildList());
            toolbar.Add(m_SearchField);

            m_StatusLabel = new Label();
            m_StatusLabel.style.marginLeft = 12f;
            m_StatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbar.Add(m_StatusLabel);

            return toolbar;
        }

        private VisualElement BuildListPane()
        {
            VisualElement pane = new()
            {
                style =
                {
                    flexGrow = 1f,
                    marginRight = 6f,
                    paddingLeft = 6f,
                    paddingRight = 6f,
                    paddingTop = 6f,
                    paddingBottom = 6f,
                    backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.65f)
                }
            };

            pane.Add(BuildHeaderRow());
            m_ListView = new ScrollView();
            m_ListView.style.flexGrow = 1f;
            pane.Add(m_ListView);
            return pane;
        }

        private VisualElement BuildPreviewPane()
        {
            VisualElement pane = new()
            {
                style =
                {
                    flexGrow = 1f,
                    marginLeft = 6f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    paddingTop = 8f,
                    paddingBottom = 8f,
                    backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.70f)
                }
            };

            VisualElement header = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8f
                }
            };

            m_PreviewTitleLabel = new Label("请选择一个面板");
            m_PreviewTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_PreviewTitleLabel.style.flexGrow = 1f;
            header.Add(m_PreviewTitleLabel);

            Button rebuildButton = new(() =>
            {
                if (m_SelectedItem != null)
                {
                    RenderPreview(m_SelectedItem);
                }
            })
            {
                text = "刷新预览"
            };
            rebuildButton.tooltip = "重新调用选中面板的 BindUI";
            header.Add(rebuildButton);

            pane.Add(header);

            m_PreviewRoot = new VisualElement();
            m_PreviewRoot.style.flexGrow = 1f;
            m_PreviewRoot.style.minHeight = 420f;
            m_PreviewRoot.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.35f);
            pane.Add(m_PreviewRoot);
            return pane;
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
                    backgroundColor = new Color(0.20f, 0.20f, 0.20f, 0.70f)
                }
            };

            row.Add(CreateHeaderLabel("面板", 130f));
            row.Add(CreateHeaderLabel("类型", 150f));
            row.Add(CreateHeaderLabel("Lv", 32f));
            return row;
        }

        private void RefreshItems()
        {
            m_Items.Clear();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<UIToolkitPanelBase>())
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                PanelInfoAttribute panelInfo = type.GetCustomAttribute<PanelInfoAttribute>();
                if (panelInfo == null)
                {
                    continue;
                }

                m_Items.Add(PanelPreviewItem.Create(type, panelInfo));
            }

            m_Items.Sort((left, right) =>
            {
                int levelResult = left.Level.CompareTo(right.Level);
                return levelResult != 0
                    ? levelResult
                    : string.Compare(left.PanelName, right.PanelName, StringComparison.Ordinal);
            });

            if (m_SelectedItem != null)
            {
                m_SelectedItem = m_Items.Find(item => item.PanelType == m_SelectedItem.PanelType);
            }

            RebuildList();
            RefreshStatus();
        }

        private void RebuildList()
        {
            if (m_ListView == null)
            {
                return;
            }

            m_ListView.Clear();
            string filter = m_SearchField?.value;
            int visibleCount = 0;
            for (int i = 0; i < m_Items.Count; i++)
            {
                PanelPreviewItem item = m_Items[i];
                if (!MatchesFilter(item, filter))
                {
                    continue;
                }

                m_ListView.Add(BuildItemRow(item, visibleCount));
                visibleCount++;
            }

            if (visibleCount == 0)
            {
                Label emptyLabel = new("未找到可预览的 UI Toolkit 面板。");
                emptyLabel.style.marginTop = 12f;
                emptyLabel.style.color = new Color(0.96f, 0.55f, 0.55f);
                m_ListView.Add(emptyLabel);
            }
        }

        private VisualElement BuildItemRow(PanelPreviewItem item, int index)
        {
            bool isSelected = m_SelectedItem != null && m_SelectedItem.PanelType == item.PanelType;
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 32f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    paddingTop = 3f,
                    paddingBottom = 3f,
                    marginBottom = 1f,
                    backgroundColor = isSelected
                        ? new Color(0.24f, 0.42f, 0.72f, 0.45f)
                        : index % 2 == 0
                            ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                            : new Color(0.31f, 0.31f, 0.31f, 0.18f)
                }
            };

            row.tooltip = string.IsNullOrEmpty(item.Path) ? "代码生成面板，PanelInfo.path 为空" : item.Path;
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    SelectItem(item);
                    evt.StopPropagation();
                }
            });

            row.Add(CreateCellLabel(item.PanelName, 130f, true));
            row.Add(CreateCellLabel(item.TypeName, 150f));
            row.Add(CreateCellLabel(item.Level.ToString(), 32f));
            return row;
        }

        private void SelectItem(PanelPreviewItem item)
        {
            m_SelectedItem = item;
            RebuildList();
            RenderPreview(item);
        }

        private void RenderPreview(PanelPreviewItem item)
        {
            if (m_PreviewRoot == null)
            {
                return;
            }

            DestroyTemporaryPanel();
            m_PreviewRoot.Clear();
            ResetPreviewRootStyle();
            if (m_PreviewTitleLabel != null)
            {
                string path = string.IsNullOrEmpty(item.Path) ? "无 Prefab 路径" : item.Path;
                m_PreviewTitleLabel.text = $"{item.PanelName}  ({item.TypeName})  -  {path}";
            }

            m_TemporaryPanelObject = new GameObject($"UI Toolkit Preview - {item.PanelName}", typeof(RectTransform));
            m_TemporaryPanelObject.hideFlags = HideFlags.HideAndDontSave;

            try
            {
                var panel = (UIToolkitPanelBase)m_TemporaryPanelObject.AddComponent(item.PanelType);
                panel.EditorBuildPreview(m_PreviewRoot);
            }
            catch (Exception exception)
            {
                ShowPreviewError(item, exception);
            }
        }

        private void ShowPreviewError(PanelPreviewItem item, Exception exception)
        {
            m_PreviewRoot.Clear();
            ResetPreviewRootStyle();

            Label title = new($"预览 {item.PanelName} 失败");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 16f;
            title.style.color = new Color(1f, 0.62f, 0.48f);
            title.style.marginBottom = 8f;
            m_PreviewRoot.Add(title);

            Label message = new(exception.ToString());
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.color = new Color(0.96f, 0.82f, 0.75f);
            m_PreviewRoot.Add(message);
        }

        private void ResetPreviewRootStyle()
        {
            m_PreviewRoot.style.flexGrow = 1f;
            m_PreviewRoot.style.minHeight = 420f;
            m_PreviewRoot.style.position = Position.Relative;
            m_PreviewRoot.style.left = StyleKeyword.Auto;
            m_PreviewRoot.style.right = StyleKeyword.Auto;
            m_PreviewRoot.style.top = StyleKeyword.Auto;
            m_PreviewRoot.style.bottom = StyleKeyword.Auto;
            m_PreviewRoot.style.display = DisplayStyle.Flex;
            m_PreviewRoot.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.35f);
        }

        private void DestroyTemporaryPanel()
        {
            if (m_TemporaryPanelObject == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(m_TemporaryPanelObject);
            m_TemporaryPanelObject = null;
        }

        private void RefreshStatus()
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            int pathlessCount = 0;
            for (int i = 0; i < m_Items.Count; i++)
            {
                if (string.IsNullOrEmpty(m_Items[i].Path))
                {
                    pathlessCount++;
                }
            }

            m_StatusLabel.text = $"共 {m_Items.Count} 个 UI Toolkit 面板，{pathlessCount} 个代码生成/无 Prefab 路径。";
        }

        private static bool MatchesFilter(PanelPreviewItem item, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return item.PanelName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.TypeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   item.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Label CreateHeaderLabel(string text, float width)
        {
            Label label = new(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.width = width;
            label.style.flexShrink = 0f;
            label.style.marginRight = 8f;
            return label;
        }

        private static Label CreateCellLabel(string text, float width, bool bold = false)
        {
            Label label = new(text);
            label.style.width = width;
            label.style.flexShrink = 0f;
            label.style.marginRight = 8f;
            label.style.overflow = Overflow.Hidden;
            if (bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            return label;
        }

        private class PanelPreviewItem
        {
            public string PanelName;
            public string TypeName;
            public string Path;
            public int Level;
            public Type PanelType;

            public static PanelPreviewItem Create(Type type, PanelInfoAttribute panelInfo)
            {
                return new PanelPreviewItem
                {
                    PanelName = panelInfo.name,
                    TypeName = type.Name,
                    Path = panelInfo.path ?? string.Empty,
                    Level = panelInfo.level,
                    PanelType = type
                };
            }
        }
    }
}
