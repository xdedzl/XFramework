using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace XReddot.Editor
{
    public class ReddotEditorWindow : EditorWindow
    { 
        [MenuItem("Game Tools/红点树编辑器")]
        public static void Open()
        {
            GetWindow<ReddotEditorWindow>("红点树编辑器");
        }

        private ReddotGraphView graphView;
        private string m_currentFilePath;
        
        private ReddotTreeAsset m_treeAsset;

        public ReddotTreeAsset treeAsset => m_treeAsset;
        private Vector3 _lastContentViewTranslation;
        private Vector3 _lastContentViewScale;

        private void OnEnable()
        {
            var toolbar = new Toolbar();
            
            var saveBtn = new Button(()=>SaveData(true)) { text = "Save Data" };
            toolbar.Add(saveBtn);

            rootVisualElement.Add(toolbar);

            graphView = new ReddotGraphView(this)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(graphView);
            
            
            LoadData();
            
            Undo.undoRedoPerformed += OnUndoRedo;
            ReddotManager.onNodeStateChange += onNodeStateChange;
            ReddotManager.onReddotTreeLoad += onReddotTreeLoad;
            // 监听播放模式变化，动态切换交互
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            UpdateEditState();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ReddotManager.onNodeStateChange -= onNodeStateChange;
            ReddotManager.onReddotTreeLoad -= onReddotTreeLoad;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SaveData();
        }

        private void LoadData()
        {
            if (m_treeAsset == null)
            {
                m_treeAsset = AssetDatabase.LoadAssetAtPath<ReddotTreeAsset>(ReddotManager.RED_DOT_TREE_ASSET_PATH);
                if (m_treeAsset == null)
                {
                    m_treeAsset = ScriptableObject.CreateInstance<ReddotTreeAsset>();
                    AssetDatabase.CreateAsset(m_treeAsset, ReddotManager.RED_DOT_TREE_ASSET_PATH);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            
            var reddotDates = m_treeAsset.items;
            var nodeDic = new Dictionary<string, ReddotNode>();

            foreach (var item in reddotDates)
            {
                var node = new ReddotNode(item.key, item.name);
                nodeDic.Add(item.key, node);
                graphView.AddElement(node);

                var rect = node.GetPosition();
                rect.x = item.position.x;
                rect.y = item.position.y;
                node.SetPosition(rect);
            }

            foreach (var item in reddotDates)
            {
                if(item.children != null)
                {
                    foreach (var child in item.children)
                    {
                        var edge = nodeDic[item.key].AddChild(nodeDic[child]);
                        graphView.AddElement(edge);
                    }
                }
            }
        }

        public void SaveData(bool saveAsset=false)
        {
            var nodeDict = new Dictionary<string, ReddotData>();
            var nodes = new List<ReddotData>();

            foreach (var node in graphView.GetReddotNodes())
            {
                var redDotData = new ReddotData
                {
                    key = node.Key,
                    children = node.ReddotChildren,
                    name = node.ReddotName,
                    position = new Vector2(node.GetPosition().x, node.GetPosition().y)
                };
                    
                if (nodeDict.TryGetValue(node.Key, out ReddotData existData))
                {
                    Debug.LogWarning($"存在重复的红点节点Key, Name1:{existData.name} Name2：{node.ReddotName}");
                }
                else
                {
                    nodeDict.Add(node.Key, redDotData);
                }
                nodes.Add(redDotData);
            }

            m_treeAsset.items = nodes.ToArray();

            if (saveAsset)
            {
                EditorUtility.SetDirty(m_treeAsset);
                AssetDatabase.SaveAssets();
            }
            
            Debug.Log("Save Reddot Data Successful!");
        }
        
        private void OnUndoRedo()
        {
            // 先清理旧的 GraphView
            if (graphView != null)
            {
                // 记录当前视图状态
                _lastContentViewTranslation = graphView.contentViewContainer.resolvedStyle.translate;
                _lastContentViewScale = graphView.contentViewContainer.resolvedStyle.scale.value;
                
                rootVisualElement.Remove(graphView);
                graphView = null;
            }
            // 重新创建并加载数据
            graphView = new ReddotGraphView(this)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(graphView);
            LoadData();
            
            // 恢复视图状态
            graphView.contentViewContainer.style.translate = _lastContentViewTranslation;
            graphView.contentViewContainer.style.scale = _lastContentViewScale;
            
            UpdateEditState();
        }

        private void onNodeStateChange(string nodeKey, bool state)
        {
            foreach (var node in graphView.GetReddotNodes())
            {
                node.UpdateState();
            }
        }

        private void onReddotTreeLoad()
        {
            foreach (var node in graphView.GetReddotNodes())
            {
                node.UpdateState();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            UpdateEditState();
            foreach (var node in graphView.GetReddotNodes())
            {
                node.UpdateState();
            }
        }

        private void UpdateEditState()
        {
            if (Application.isPlaying)
            {
                graphView.DisableEdit();    
            }
            else
            {
                graphView.EnableEdit();
            }
        }
    }

    public class ReddotGraphView : GraphView
    {
        private const string DEFAULT_USS_PATH = "Assets/Editor/XReddot/Styles/reddot_graphview_style.uss";
        private const string DEFAULT_USS = @"
GridBackground {
    --grid-background-color: rgb(10, 10, 10);
    --line-color: rgba(193, 196, 192, 0.1);
    --thick-line-color: rgba(193, 196, 192, 0.1);
    --spacing: 10;
}

.input{
    width: 60%;
    min-width: 100;
	font-size: 10px;
    border-radius: 5px;
}
        ";

        private readonly ReddotEditorWindow m_editorWindow;
        // 编辑器操控器实例缓存，便于动态增删
        private SelectionDragger _selectionDragger;
        private RectangleSelector _rectangleSelector;
        private FreehandSelector _freehandSelector;

        public ReddotGraphView(ReddotEditorWindow editorWindow)
        {
            ReddotPort.graphView = this;
            m_editorWindow = editorWindow;

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(DEFAULT_USS_PATH);
            if (style == null)
            {
                var directory = Path.GetDirectoryName(DEFAULT_USS_PATH);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(DEFAULT_USS_PATH, DEFAULT_USS);
                AssetDatabase.ImportAsset(DEFAULT_USS_PATH);
                style = AssetDatabase.LoadAssetAtPath<StyleSheet>(DEFAULT_USS_PATH);
            }
            styleSheets.Add(style);
            
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            this.AddManipulator(new ContentDragger()); // 只允许画布拖动
            // 自动保存与Undo注册
            this.graphViewChanged += OnGraphViewChanged;
            nodeCreationRequest += context =>
            {
                if (Application.isPlaying)
                {
                    return;
                }
                Undo.RegisterCompleteObjectUndo(m_editorWindow.treeAsset, "新建红点节点");
                var node = new ReddotNode(GetDefaultReddotKey());
                AddNode(node, context.screenMousePosition);
                // 自动保存
                m_editorWindow.SaveData();
            };
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (Application.isPlaying)
            {
                return change;
            }
            
            if ((change.elementsToRemove != null && change.elementsToRemove.Count > 0) ||
                (change.edgesToCreate != null && change.edgesToCreate.Count > 0) ||
                (change.movedElements != null && change.movedElements.Count > 0))
            {
                Undo.RegisterCompleteObjectUndo(m_editorWindow.treeAsset, "红点树编辑操作");
                m_editorWindow.SaveData();
            }
            return change;
        }

        public void AddNode(ReddotNode node, Vector2 screenMousePosition)
        {
            var mousePosition = m_editorWindow.rootVisualElement.ChangeCoordinatesTo(m_editorWindow.rootVisualElement.parent,
                   screenMousePosition - m_editorWindow.position.position);
            var graphMousePosition = this.contentViewContainer.WorldToLocal(mousePosition);

            this.AddElement(node);
            node.SetPosition(new Rect(graphMousePosition, node.GetPosition().size));
        }


        public IList<ReddotNode> GetReddotNodes()
        {
            var query = graphElements;
            var nodes = new List<ReddotNode>();
            
            query.ForEach((element) =>
            {
                if (element is ReddotNode node)
                {
                    nodes.Add(node);
                }
            });
            return nodes;
        }
        
        public string GetDefaultReddotKey()
        {
            var keys = GetReddotNodes().Select((node)=> node.Key);
            var allKey = new HashSet<string>(keys);
            
            var defaultKey = "new_reddot_key";
            var key = defaultKey;

            int index = 0;
            
            while (allKey.Contains(key))
            {
                key = defaultKey + "_" + index;
                index++;
            }

            return key;
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            if (Application.isPlaying)
                return new List<Port>(); // 运行时禁止连线
            var compatiblePorts = new List<Port>();
            foreach (var port in ports.ToList())
            {
                if (startPort.direction == port.direction || startPort.portType != port.portType)
                {
                    continue;
                }
                compatiblePorts.Add(port);
            }
            return compatiblePorts;
        }
        
        public void EnableEdit()
        {
            EnableEditorManipulators();
            EnableAllNodeInteraction();
        }

        public void DisableEdit()
        {
            DisableEditorManipulators();
            DisableAllNodeInteraction();
        }
        
        private void EnableEditorManipulators()
        {
            if (_selectionDragger == null) _selectionDragger = new SelectionDragger();
            if (_rectangleSelector == null) _rectangleSelector = new RectangleSelector();
            if (_freehandSelector == null) _freehandSelector = new FreehandSelector();

            this.AddManipulator(_selectionDragger);
            this.AddManipulator(_rectangleSelector);
            this.AddManipulator(_freehandSelector);
        }

        private void DisableEditorManipulators()
        {
            if (_selectionDragger != null) this.RemoveManipulator(_selectionDragger);
            if (_rectangleSelector != null) this.RemoveManipulator(_rectangleSelector);
            if (_freehandSelector != null) this.RemoveManipulator(_freehandSelector);
        }
        
        private void DisableAllNodeInteraction()
        {
            foreach (var node in GetReddotNodes())
            {
                node.OnDisableEdit();
            }
            foreach (var edge in graphElements.ToList().OfType<Edge>())
            {
                edge.pickingMode = PickingMode.Ignore;
                edge.capabilities &= ~(Capabilities.Selectable | Capabilities.Deletable);
                // 禁止编辑已存在的边
                edge.SetEnabled(false);
            }
        }

        private void EnableAllNodeInteraction()
        {
            foreach (var node in GetReddotNodes())
            {
                node.OnEnableEdit();
            }
            foreach (var edge in graphElements.ToList().OfType<Edge>())
            {
                edge.pickingMode = PickingMode.Position;
                edge.capabilities |= (Capabilities.Selectable | Capabilities.Deletable);
                // 恢复边的编辑能力
                edge.SetEnabled(true);
            }
        }
    }
}
