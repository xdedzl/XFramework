using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using XFramework.Editor; // 添加以使用 DateTime

public class SceneListWindow : EditorWindow
{
    private List<SceneInfo> sceneInfos = new List<SceneInfo>();
    private TextField searchField;
    private ListView sceneListView;
    private Label statusLabel;
    private Toggle showPackagesToggle;
    private bool includePackages;
    
    private enum SortMode
    {
        Name,
        LastOpenTime
    }
    private SortMode sortMode = SortMode.LastOpenTime;
    private const string LastOpenKeyPrefix = "XFramework.Scene.LastOpen:";

    [MenuItem("XFramework/Scene Viewer")]
    public static void ShowWindow()
    {
        SceneListWindow wnd = GetWindow<SceneListWindow>();
        wnd.titleContent = new GUIContent("场景列表");
        wnd.minSize = new Vector2(500, 200);
    }

    private void OnEnable()
    {
        RefreshSceneList();
        CreateUI();
    }

    private void RefreshSceneList()
    {
        sceneInfos.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Scene");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // 根据是否包含 Packages 路径的场景进行过滤
            if (!includePackages && path.StartsWith("Packages/"))
            {
                continue;
            }
            
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

            if (sceneAsset != null)
            {
                // 读取最后打开时间
                long ticks = 0;
                string key = LastOpenKeyPrefix + path;
                if (EditorPrefs.HasKey(key))
                {
                    var stored = EditorPrefs.GetString(key, string.Empty);
                    long.TryParse(stored, out ticks);
                }

                sceneInfos.Add(new SceneInfo
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Path = path,
                    Asset = sceneAsset,
                    LastOpenTicks = ticks
                });
            }
        }

        ApplySorting();
    }

    private void CreateUI()
    {
        // 根容器
        VisualElement root = rootVisualElement;
        root.Clear();

        // 顶部工具栏
        var toolbarContainer = new VisualElement();
        toolbarContainer.style.flexDirection = FlexDirection.Row;
        toolbarContainer.style.marginTop = 5;
        toolbarContainer.style.marginBottom = 5;
        toolbarContainer.style.marginLeft = 5;
        toolbarContainer.style.marginRight = 5;
        root.Add(toolbarContainer);

        // 排序方式（新增）
        var sortDropdown = new DropdownField(new List<string> { "名称", "最后打开时间" }, 1)
        {
            tooltip = "选择列表的排序方式",
            style =
            {
                width = 100
            }
        };
        sortDropdown.RegisterValueChangedCallback(evt =>
        {
            sortMode = evt.newValue == "最后打开时间" ? SortMode.LastOpenTime : SortMode.Name;
            ApplySorting();
            RefreshFilteredList();
        });
        toolbarContainer.Add(sortDropdown);

        // 搜索框
        searchField = new TextField
        {
            style =
            {
                flexGrow = 1,
                minWidth = 50
            }
        };
        searchField.RegisterValueChangedCallback(OnSearchChanged);
        toolbarContainer.Add(searchField);

        // 包含Packages选项
        showPackagesToggle = new Toggle("包含Packages下的场景")
        {
            value = includePackages
        };
        showPackagesToggle.RegisterValueChangedCallback(evt => {
            includePackages = evt.newValue;
            RefreshList();
        });
        showPackagesToggle.style.marginLeft = 10;
        showPackagesToggle.style.marginRight = 10;
        toolbarContainer.Add(showPackagesToggle);

        // 刷新按钮
        var refreshButton = new Button(RefreshList)
        {
            text = "刷新",
            style =
            {
                width = 60,
                marginLeft = 5
            }
        };
        toolbarContainer.Add(refreshButton);

        // 创建表头
        var headerContainer = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                height = 25
            }
        };

        var nameHeaderLabel = new Label("场景名称")
        {
            style =
            {
                width = 200,
                maxWidth = 200,
                unityFontStyleAndWeight = FontStyle.Bold,
                paddingLeft = 5
            }
        };
        headerContainer.Add(nameHeaderLabel);

        var pathHeaderLabel = new Label("路径")
        {
            style =
            {
                width = 400,
                maxWidth = 400,
                flexGrow = 1,
                unityFontStyleAndWeight = FontStyle.Bold,
                paddingLeft = 5
            }
        };
        headerContainer.Add(pathHeaderLabel);

        var operationsHeaderLabel = new Label("操作")
        {
            style =
            {
                width = 100,
                unityFontStyleAndWeight = FontStyle.Bold
            }
        };
        headerContainer.Add(operationsHeaderLabel);

        // root.Add(headerContainer);

        // 场景列表
        var listContainer = new VisualElement
        {
            style =
            {
                flexGrow = 1,
                marginLeft = 5,
                marginRight = 5,
                marginTop = 2,
                marginBottom = 2,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderTopWidth = 1,
                borderBottomWidth = 1,
                borderLeftColor = new Color(0.35f, 0.35f, 0.35f),
                borderRightColor = new Color(0.35f, 0.35f, 0.35f),
                borderTopColor = new Color(0.35f, 0.35f, 0.35f),
                borderBottomColor = new Color(0.35f, 0.35f, 0.35f),
                borderTopLeftRadius = 4,
                borderTopRightRadius = 4,
                borderBottomLeftRadius = 4,
                borderBottomRightRadius = 4,
                backgroundColor = new Color(0.18f, 0.18f, 0.18f)
            }
        };

        sceneListView = new ListView
        {
            style =
            {
                flexGrow = 1
            }
        };

        // 配置列表视图
        ConfigureListView();

        listContainer.Add(sceneListView);
        root.Add(listContainer);

        // 状态栏
        statusLabel = new Label($"共 {FilterScenes(searchField.value).Count} 个场景")
        {
            style =
            {
                marginTop = 5,
                marginBottom = 5,
                marginLeft = 5,
                paddingLeft = 5,
                backgroundColor = new Color(0.2f, 0.2f, 0.2f)
            }
        };
        root.Add(statusLabel);
    }

    private void ConfigureListView()
    {
        // 配置列表视图
        sceneListView.makeItem = () =>
        {
            var itemContainer = new XItemBox
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                }
            };

            var nameLabel = new Label
            {
                style =
                {
                    width = 200,
                    maxWidth = 200,
                    paddingLeft = 5,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };
            nameLabel.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.clickCount == 2 && itemContainer.userData is SceneInfo sceneInfo)
                {
                    OpenScene(sceneInfo.Path);
                }
            });
            itemContainer.Add(nameLabel);

            var pathLabel = new Label
            {
                style =
                {
                    width = 400,
                    maxWidth = 400,
                    paddingLeft = 5,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };
            // 新增：双击路径进行定位
            pathLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount != 2 || itemContainer.userData is not SceneInfo sceneInfo) return;
                Selection.activeObject = sceneInfo.Asset;
                EditorGUIUtility.PingObject(sceneInfo.Asset);
            });
            itemContainer.Add(pathLabel);

            // 新增：最后打开时间列（简要显示）
            var timeLabel = new Label
            {
                style =
                {
                    paddingLeft = 10,
                    width = 120,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingRight = 10
                }
            };
            itemContainer.Add(timeLabel);

            var buttonsContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    width = 100
                }
            };

            var locateButton = new Button
            {
                text = "定位",
                style =
                {
                    width = 45,
                    height = 20,
                    fontSize = 10
                }
            };
            buttonsContainer.Add(locateButton);

            var openButton = new Button
            {
                text = "打开",
                style =
                {
                    width = 45,
                    height = 20,
                    fontSize = 10,
                    marginLeft = 5
                }
            };
            buttonsContainer.Add(openButton);

            itemContainer.Add(buttonsContainer);

            return itemContainer;
        };

        sceneListView.bindItem = (element, i) => {
            var filteredList = FilterScenes(searchField.value);
            if (i < 0 || i >= filteredList.Count)
                return;

            var sceneInfo = filteredList[i];
            element.userData = sceneInfo;

            var labels = element.Query<Label>().ToList();
            // labels: 0-name, 1-path, 2-time
            labels[0].text = sceneInfo.Name;
            labels[0].tooltip = sceneInfo.Name; // 悬浮显示完整名称
            labels[1].text = sceneInfo.Path;
            labels[1].tooltip = sceneInfo.Path; // 悬浮显示完整路径
            labels[2].text = sceneInfo.LastOpenTicks > 0
                ? new DateTime(sceneInfo.LastOpenTicks).ToString("yyyy-MM-dd HH:mm")
                : "未打开";

            var buttons = element.Query<Button>().ToList();
            buttons[0].clicked += () => {
                Selection.activeObject = sceneInfo.Asset;
                EditorGUIUtility.PingObject(sceneInfo.Asset);
            };
            buttons[1].clicked += () => {
                OpenScene(sceneInfo.Path);
            };

            // 设置交错背景色 - 更明显的对比
            element.style.backgroundColor = i % 2 == 0 ?
                new Color(0.25f, 0.25f, 0.25f, 0.15f) :
                new Color(0.3f, 0.3f, 0.3f, 0.25f);
        };

        sceneListView.itemsSource = FilterScenes(string.Empty);
        sceneListView.fixedItemHeight = 25;
    }

    private List<SceneInfo> FilterScenes(string searchText)
    {
        // 基于名称过滤
        var baseList = string.IsNullOrEmpty(searchText)
            ? sceneInfos
            : sceneInfos.Where(s => s.Name.ToLower().Contains(searchText.ToLower())).ToList();

        // 过滤后也要维持排序
        if (sortMode == SortMode.LastOpenTime)
        {
            return baseList
                .OrderByDescending(s => s.LastOpenTicks)
                .ThenBy(s => s.Path) // 相同时间按路径稳定排序
                .ToList();
        }
        else
        {
            return baseList
                .OrderBy(s => s.Path) // 名称排序用路径的完整字典序，保持原有行为
                .ToList();
        }
    }

    private void OnSearchChanged(ChangeEvent<string> evt)
    {
        RefreshFilteredList();
    }

    private void RefreshFilteredList()
    {
        var filteredList = FilterScenes(searchField.value);
        sceneListView.itemsSource = filteredList;
        sceneListView.Rebuild();
        statusLabel.text = $"共 {filteredList.Count} 个场景";
    }

    private void RefreshList()
    {
        RefreshSceneList();
        RefreshFilteredList();
    }

    private void ApplySorting()
    {
        if (sortMode == SortMode.LastOpenTime)
        {
            sceneInfos = sceneInfos
                .OrderByDescending(s => s.LastOpenTicks)
                .ThenBy(s => s.Path)
                .ToList();
        }
        else
        {
            // 按完整路径排序（与原逻辑一致）
            sceneInfos = sceneInfos.OrderBy(s => s.Path).ToList();
        }
    }

    private void OpenScene(string path)
    {
        // 如果有未保存的修改，先提示保存
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            if (EditorUtility.DisplayDialog("保存当前场景",
                    "当前场景有未保存的修改，是否保存？", "保存", "不保存"))
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }

        // 打开选中的场景
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        // 记录最后打开时间
        string key = LastOpenKeyPrefix + path;
        EditorPrefs.SetString(key, DateTime.Now.Ticks.ToString());

        // 更新当前列表中的该项时间并刷新视图
        var target = sceneInfos.FirstOrDefault(s => s.Path == path);
        if (target != null)
        {
            target.LastOpenTicks = DateTime.Now.Ticks;
            ApplySorting();
            RefreshFilteredList();
        }
    }

    private class SceneInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public SceneAsset Asset { get; set; }
        public long LastOpenTicks { get; set; } // 新增：最后打开时间
    }
}