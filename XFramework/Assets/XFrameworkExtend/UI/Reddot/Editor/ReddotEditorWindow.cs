using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI.Editor
{
    public class ReddotEditorWindow : EditorWindow
    {
        [MenuItem("XFramework/UI/ReddotEditor")]
        public static void Open()
        {
            GetWindow<ReddotEditorWindow>("ReddotEditor");
        }

        private ReddotGraphView graphView;
        private string m_currentFilePath;
        private string m_defaultSavePath;
        void OnEnable()
        {
            m_defaultSavePath = EditorPrefs.GetString("reddot_default_save_path", Application.dataPath);

            var toolbar = new Toolbar();


            var loadBtn = new Button(() => 
            {
                string path = EditorUtility.OpenFilePanel("Save Red Dot", m_defaultSavePath, "json");

                if (!string.IsNullOrEmpty(path))
                {
                    LoadData(path);
                    m_currentFilePath = path;
                    m_defaultSavePath = path;
                }
            }) { text = "Load Data" };
            toolbar.Add(loadBtn);

            var saveBtn = new Button(()=> 
            {
                var directory = string.IsNullOrEmpty(m_currentFilePath) ? m_defaultSavePath : Utility.Text.SplitPathName(m_currentFilePath)[0];
                string path = EditorUtility.SaveFilePanel("Save Red Dot", directory, "ReddotData", "json");

                if (!string.IsNullOrEmpty(path))
                {
                    SaveData(path);
                    m_defaultSavePath = path;
                }
            }) { text = "Save Data" };
            toolbar.Add(saveBtn);

            rootVisualElement.Add(toolbar);

            graphView = new ReddotGraphView(this)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(graphView);
        }

        private void OnDisable()
        {
            EditorPrefs.SetString("reddot_default_save_path", m_defaultSavePath);
        }

        private void LoadData(string path)
        {
            string json = File.ReadAllText(path);
            ReddotData[] reddotDatas = Newtonsoft.Json.JsonConvert.DeserializeObject<ReddotData[]>(json);

            Dictionary<string, ReddotNode> nodeDic = new Dictionary<string, ReddotNode>();

            foreach (var item in reddotDatas)
            {
                var node = new ReddotNode(item.key, item.name);
                nodeDic.Add(item.key, node);
                graphView.AddElement(node);

                var rect = node.GetPosition();
                rect.x = item.position.x;
                rect.y = item.position.y;
                node.SetPosition(rect);
            }

            foreach (var item in reddotDatas)
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

        private void SaveData(string path)
        {
            UQueryState<GraphElement> query = graphView.graphElements;
            Dictionary<string, ReddotData> nodes = new Dictionary<string, ReddotData>();

            query.ForEach((element) =>
            {
                if (element is ReddotNode node)
                {
                    if (nodes.ContainsKey(node.Key))
                    {
                        throw new System.Exception($"´æÔÚÖØ¸´µÄkey ¡¾{node.Key}¡¿");
                    }

                    nodes.Add(node.Key, new ReddotData 
                    {
                        key = node.Key,
                        children = node.RedotChildren,

                        name = node.ReddotName,

                        position = node.transform.position
                    });
                }
            });

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(nodes, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }

    public class ReddotGraphView : GraphView
    {
        private EditorWindow m_editorWindow;

        public ReddotGraphView(EditorWindow editorWindow)
        {
            ReddotPort.graphView = this;
            m_editorWindow = editorWindow;
            var nodeStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(@"Assets\XFrameworkExtend\UI\Reddot\Editor\NarrativeGraph.uss");
            styleSheets.Add(nodeStyle);

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
            node.transform.position = graphMousePosition;
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