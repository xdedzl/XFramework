using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;

namespace  XFramework.Editor
{
    public class CustomUIElementsWindow : EditorWindow
    {
        private ObjectField mainGameObject;
        private ListView gameObjectsList;
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private Button executeButton;
        private int selectedIndex = -1;

        [MenuItem("XFramework/Animation Converter")]
        public static void ShowWindow()
        { 
            CustomUIElementsWindow wnd = GetWindow<CustomUIElementsWindow>();
            wnd.titleContent = new GUIContent("Animation Converter");
            wnd.minSize = new Vector2(300, 200);
        }

        public void CreateGUI()
        {
            // 创建根容器
            var root = rootVisualElement;
        
            // 设置根容器样式
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;

            // 创建主GameObject字段
            mainGameObject = new ObjectField("Target FBX:")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                style =
                {
                    marginBottom = 10
                }
            };
            root.Add(mainGameObject);

            // 创建列表容器
            var listContainer = new Box
            {
                style =
                {
                    marginBottom = 10,
                    paddingTop = 5,
                    paddingBottom = 5,
                    paddingLeft = 5,
                    paddingRight = 5,
                    backgroundColor = new Color(0, 0, 0, 0.1f),
                    flexGrow = 1,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3
                }
            };
            root.Add(listContainer);

            // 创建列表控制按钮
            var listControlsContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginBottom = 5
                }
            };

            var clearButton = new Button(ClearAllGameObjects)
            {
                text = "清空",
                style =
                {
                    height = 25,
                    marginRight = 5,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 0,
                    paddingBottom = 0
                }
            };

            var addButton = new Button(() => AddGameObject(null))
            {
                text = "+",
                style =
                {
                    width = 25,
                    height = 25,
                    marginLeft = 5,
                    fontSize = 14,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 0,
                    paddingBottom = 0
                }
            };

            listControlsContainer.Add(clearButton);
            listControlsContainer.Add(addButton);
            listContainer.Add(listControlsContainer);

            // 创建GameObject列表
            gameObjectsList = new ListView(gameObjects, 22, MakeListItem, BindListItem)
            {
                selectionType = SelectionType.Single
            };
            gameObjectsList.selectionChanged += OnListSelectionChange;
            gameObjectsList.style.flexGrow = 1;
            gameObjectsList.style.minHeight = 200;
        
            // 设置拖放功能
            gameObjectsList.RegisterCallback<DragEnterEvent>(OnDragEnter);
            gameObjectsList.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            gameObjectsList.RegisterCallback<DragExitedEvent>(OnDragExited);
            gameObjectsList.RegisterCallback<DragPerformEvent>(OnDragPerform);
            gameObjectsList.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);

            listContainer.Add(gameObjectsList);

            // 创建执行按钮
            executeButton = new Button(Execute)
            {
                text = "执行",
                style =
                {
                    height = 40,
                    fontSize = 14,
                    backgroundColor = new Color(0, 0, 0, 0.1f),
                    color = Color.white,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4
                }
            };

            root.Add(executeButton);

            // 设置整个窗口的拖放功能
            root.RegisterCallback<DragEnterEvent>(OnDragEnter);
            root.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            root.RegisterCallback<DragExitedEvent>(OnDragExited);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
        }

        private VisualElement MakeListItem()
        {
            var item = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingBottom = 2,
                    paddingTop = 2,
                    paddingLeft = 2,
                    paddingRight = 2
                }
            };

            var objectField = new ObjectField
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                style =
                {
                    flexGrow = 1
                }
            };

            var deleteButton = new Button
            {
                text = "×",
                style =
                {
                    width = 20,
                    height = 20,
                    marginLeft = 5,
                    fontSize = 14,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 0,
                    paddingBottom = 0
                }
            };

            item.Add(objectField);
            item.Add(deleteButton);
            return item;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index >= 0 && index < gameObjects.Count)
            {
                var objectField = element.Q<ObjectField>();
                objectField.value = gameObjects[index];
                objectField.RegisterValueChangedCallback(evt =>
                {
                    gameObjects[index] = evt.newValue as GameObject;
                });
            
                var deleteButton = element.Q<Button>();
                // deleteButton.Un ;
                // deleteButton.clickable.clicked -= deleteButton.clickable.clicked;
                deleteButton.clicked += () => {
                    gameObjects.RemoveAt(index);
                    gameObjectsList.Rebuild();
                };
            }
        }

        private void OnListSelectionChange(IEnumerable<object> selectedItems)
        {
            foreach (var item in selectedItems)
            {
                selectedIndex = gameObjects.IndexOf(item as GameObject);
                break;
            }
        }

        private void AddGameObject(GameObject gameObject)
        {
            gameObjects.Add(gameObject);
            gameObjectsList.Rebuild();
        }
    
        private void ClearAllGameObjects()
        {
            gameObjects.Clear();
            gameObjectsList.Rebuild();
            selectedIndex = -1;
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.None;
        }

        private void OnDragExited(DragExitedEvent evt)
        {
            // 不需要执行任何操作
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            foreach (var draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is GameObject go)
                {
                    AddGameObject(go);
                }
            }

            DragAndDrop.AcceptDrag();
        }

        private void Execute()
        {
            // 收集所有动画剪辑
            List<AnimationClip> allClips = CollectAllAnimationClips();
            
            if (allClips.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "在选择的FBX文件中没有找到动画剪辑", "确定");
                return;
            }

            var targetObject = mainGameObject.value;
            string targetPath = AssetDatabase.GetAssetPath(targetObject);
            ModelImporter modelImporter = AssetImporter.GetAtPath(targetPath) as ModelImporter;
        
            if (modelImporter == null)
            {
                throw new System.Exception("无法获取目标FBX的ModelImporter");
            }

            // 应用设置
            modelImporter.clipAnimations = allClips.Select(clip => new ModelImporterClipAnimation
            {
                name = clip.name,
                takeName = clip.name,
                firstFrame = 0,
                lastFrame = Mathf.RoundToInt(clip.length * clip.frameRate),
                wrapMode = WrapMode.Default,
                loopTime = false
            }).ToArray();
            modelImporter.SaveAndReimport();
        
            // 保存动画剪辑为子资源
            foreach (AnimationClip clip in allClips)
            {
                // 将动画剪辑添加为目标FBX的子资源
                AssetDatabase.AddObjectToAsset(clip, targetPath);
            }
        
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        
            Debug.Log($"成功将 {allClips.Count} 个动画剪辑添加到目标FBX: {targetPath}");
        
        
            EditorUtility.DisplayDialog("完成", 
                $"成功合并 {allClips.Count} 个动画剪辑到目标FBX！\n请检查目标FBX的导入设置。", "确定");
        }
    
        private List<AnimationClip> CollectAllAnimationClips()
        {
            List<AnimationClip> allClips = new List<AnimationClip>();
            HashSet<string> clipNames = new HashSet<string>();
        
            // 收集所有源FBX文件的动画剪辑
            foreach (GameObject fbxObject in gameObjects)
            {
                if (fbxObject == null) continue;
            
                string path = AssetDatabase.GetAssetPath(fbxObject);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            
                foreach (Object asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                    {
                        // 处理重名动画
                        string uniqueName = clip.name;
                        int counter = 1;
                        while (clipNames.Contains(uniqueName))
                        {
                            uniqueName = $"{clip.name}_{counter}";
                            counter++;
                        }
                    
                        // 创建动画剪辑副本
                        AnimationClip newClip = new AnimationClip();
                        EditorUtility.CopySerialized(clip, newClip);
                        newClip.name = uniqueName;
                    
                        allClips.Add(newClip);
                        clipNames.Add(uniqueName);
                    }
                }
            }
        
            return allClips;
        }
    }
}
