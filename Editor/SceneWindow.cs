using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class SceneListWindow : EditorWindow
{
    private List<SceneInfo> sceneInfos = new List<SceneInfo>();
    private TextField searchField;
    private ListView sceneListView;
    private Label statusLabel;
    private Toggle showPackagesToggle;
    private bool includePackages = false;

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
                sceneInfos.Add(new SceneInfo
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Path = path,
                    Asset = sceneAsset
                });
            }
        }

        // 按完整路径排序
        sceneInfos = sceneInfos.OrderBy(s => s.Path).ToList();
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

        // 搜索框
        searchField = new TextField("搜索:");
        searchField.style.flexGrow = 1;
        searchField.RegisterValueChangedCallback(OnSearchChanged);
        toolbarContainer.Add(searchField);

        // 包含Packages选项
        showPackagesToggle = new Toggle("包含Packages下的场景");
        showPackagesToggle.value = includePackages;
        showPackagesToggle.RegisterValueChangedCallback(evt => {
            includePackages = evt.newValue;
            RefreshList();
        });
        showPackagesToggle.style.marginLeft = 10;
        showPackagesToggle.style.marginRight = 10;
        toolbarContainer.Add(showPackagesToggle);

        // 刷新按钮
        var refreshButton = new Button(RefreshList) { text = "刷新" };
        refreshButton.style.width = 60;
        refreshButton.style.marginLeft = 5;
        toolbarContainer.Add(refreshButton);

        // 创建表头
        var headerContainer = new VisualElement();
        headerContainer.style.flexDirection = FlexDirection.Row;
        headerContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        headerContainer.style.height = 25;

        var nameHeaderLabel = new Label("场景名称");
        nameHeaderLabel.style.width = 200;
        nameHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameHeaderLabel.style.paddingLeft = 5;
        headerContainer.Add(nameHeaderLabel);

        var pathHeaderLabel = new Label("路径");
        pathHeaderLabel.style.flexGrow = 1;
        pathHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerContainer.Add(pathHeaderLabel);

        var operationsHeaderLabel = new Label("操作");
        operationsHeaderLabel.style.width = 100;
        operationsHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        headerContainer.Add(operationsHeaderLabel);

        // root.Add(headerContainer);

        // 场景列表
        sceneListView = new ListView();
        sceneListView.style.flexGrow = 1;
        
        // 添加边框样式
        // sceneListView.style.borderWidth = 1;
        // sceneListView.style.borderColor = new Color(0.3f, 0.3f, 0.3f);
        sceneListView.style.marginLeft = 5;
        sceneListView.style.marginRight = 5;
        
        // 配置列表视图
        ConfigureListView();

        root.Add(sceneListView);

        // 状态栏
        statusLabel = new Label($"共 {FilterScenes(searchField.value).Count} 个场景");
        statusLabel.style.marginTop = 5;
        statusLabel.style.marginBottom = 5;
        statusLabel.style.marginLeft = 5;
        statusLabel.style.paddingLeft = 5;
        statusLabel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        root.Add(statusLabel);
    }

    private void ConfigureListView()
    {
        // 配置列表视图
        sceneListView.makeItem = () => {
            var itemContainer = new VisualElement();
            itemContainer.style.flexDirection = FlexDirection.Row;
            itemContainer.style.paddingTop = 2;
            itemContainer.style.paddingBottom = 2;

            var nameLabel = new Label();
            nameLabel.style.width = 200;
            nameLabel.style.paddingLeft = 5;
            nameLabel.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.clickCount == 2 && itemContainer.userData is SceneInfo sceneInfo)
                {
                    OpenScene(sceneInfo.Path);
                }
            });
            itemContainer.Add(nameLabel);

            var pathLabel = new Label();
            pathLabel.style.flexGrow = 1;
            itemContainer.Add(pathLabel);

            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexDirection = FlexDirection.Row;
            buttonsContainer.style.width = 100;

            var locateButton = new Button { text = "定位" };
            locateButton.style.width = 45;
            locateButton.style.height = 20;
            locateButton.style.fontSize = 10;
            buttonsContainer.Add(locateButton);

            var openButton = new Button { text = "打开" };
            openButton.style.width = 45;
            openButton.style.height = 20;
            openButton.style.fontSize = 10;
            openButton.style.marginLeft = 5;
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
            labels[0].text = sceneInfo.Name;
            labels[1].text = sceneInfo.Path;

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
        if (string.IsNullOrEmpty(searchText))
            return sceneInfos;

        return sceneInfos.Where(s =>
            s.Name.ToLower().Contains(searchText.ToLower()) ||
            s.Path.ToLower().Contains(searchText.ToLower())
        ).ToList();
    }

    private void OnSearchChanged(ChangeEvent<string> evt)
    {
        var filteredList = FilterScenes(evt.newValue);
        sceneListView.itemsSource = filteredList;
        sceneListView.Rebuild();
        statusLabel.text = $"共 {filteredList.Count} 个场景";
    }

    private void RefreshList()
    {
        RefreshSceneList();
        sceneListView.itemsSource = FilterScenes(searchField.value);
        sceneListView.Rebuild();
        statusLabel.text = $"共 {sceneListView.itemsSource.Count} 个场景";
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
    }

    private class SceneInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public SceneAsset Asset { get; set; }
    }
}