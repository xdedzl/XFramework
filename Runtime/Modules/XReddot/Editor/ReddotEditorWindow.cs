using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XReddot.Editor
{
    public class ReddotEditorWindow : EditorWindow
    { 
        [MenuItem("XFramework/红点树编辑器")]
        public static void Open()
        {
            GetWindow<ReddotEditorWindow>("红点树编辑器");
        }

        private ReddotGraphView graphView;
        private string m_currentFilePath;

        private void OnEnable()
        {
            var toolbar = new Toolbar();
            
            var saveBtn = new Button(SaveData) { text = "Save Data" };
            toolbar.Add(saveBtn);

            rootVisualElement.Add(toolbar);

            graphView = new ReddotGraphView(this)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(graphView);
            
            
            LoadData();
        }

        private void LoadData()
        {
            var path = ReddotManager.RED_DOT_TREE_FULL_PATH;
            if (!File.Exists(path))
            {
                return;
            }
            string json = File.ReadAllText(path);
            var reddotDates = JsonUtility.FromJson<ReddotDataArrayWrapper>(json).items;
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
                        graphView.Add(edge);
                    }
                }
            }
        }

        private void SaveData()
        {
            var path = ReddotManager.RED_DOT_TREE_FULL_PATH;
            var query = graphView.graphElements;
            var nodeDict = new Dictionary<string, ReddotData>();
            var nodes = new List<ReddotData>();
            
            query.ForEach((element) =>
            {
                if (element is ReddotNode node)
                {
                    if (nodeDict.ContainsKey(node.Key))
                    {
                        throw new System.Exception("存在重复的红点节点 Key：" + node.Key);
                    }

                    var redDotData = new ReddotData
                    {
                        key = node.Key,
                        children = node.RedotChildren,
                        name = node.ReddotName,
                        position = new Vector2(node.GetPosition().x, node.GetPosition().y)
                    };
                    
                    nodeDict.Add(node.Key, redDotData);
                    nodes.Add(redDotData);
                }
            });
        
            var wrapper = new ReddotDataArrayWrapper { items = nodes.ToArray() };
            string json = JsonUtility.ToJson(wrapper, true);
            
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            ReddotManager.Reload();
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

        private readonly EditorWindow m_editorWindow;
        public ReddotGraphView(EditorWindow editorWindow)
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

            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());

            nodeCreationRequest += context =>
            {
                var node = new ReddotNode();
                AddNode(node, context.screenMousePosition);
            };
        }

        public void AddNode(ReddotNode node, Vector2 screenMousePosition)
        {
            var mousePosition = m_editorWindow.rootVisualElement.ChangeCoordinatesTo(m_editorWindow.rootVisualElement.parent,
                   screenMousePosition - m_editorWindow.position.position);
            var graphMousePosition = this.contentViewContainer.WorldToLocal(mousePosition);

            this.AddElement(node);
            node.SetPosition(new Rect(graphMousePosition, node.GetPosition().size));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
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
    }
}