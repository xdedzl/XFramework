using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace XReddot.Editor
{
    using Node = UnityEditor.Experimental.GraphView.Node;

    internal class EditableLabel: VisualElement
    {
        private readonly TextElement m_label;
        private readonly TextField m_textField;
        
        public EditableLabel() : this(string.Empty)
        {
        }
        
        public EditableLabel(string text)
        {
            m_label = new TextElement()
            {
                text = text,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    height = new Length(100, LengthUnit.Percent),
                    width = new Length(100, LengthUnit.Percent),
                    fontSize = 15,
                }
            };

            m_textField = new TextField
            {
                value = text,
                style =
                {
                    justifyContent = Justify.Center,
                    // alignItems = Align.Center,
                    // height = new Length(100, LengthUnit.Percent),
                    // width = new Length(100, LengthUnit.Percent),
                    marginBottom = 5,
                    marginTop = 5,
                }
            };

            var inputText = m_textField.Q("unity-text-input").ElementAt(0);
            if (inputText != null)
            {
                inputText.style.fontSize = 15;  
            }
            
            Add(m_label);
        }
        
        public string text
        {
            get => m_label.text;
            set
            {
                m_label.text = value;
                m_textField.value = value;
            }
        }
        
        private void BeginEditTitle()
        {
            Remove(m_label);
            Add(m_textField);
            m_textField.Focus();
            m_textField.SelectAll();
        }

        private void EndEditTitle()
        {
            Remove(m_textField);
            Add(m_label);
            text = m_textField.value;
        }
        
        public void SetEditable(bool editable)
        {
            if (editable)
            {
                m_textField.RegisterCallback<FocusOutEvent>(OnFocusOut);
                m_textField.RegisterCallback<KeyDownEvent>(OnKeyDown);
                
                // 监听 title 区域的双击事件
                RegisterCallback<MouseDownEvent>(OnMouseDown);
            }
            else
            {
                m_textField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
                m_textField.UnregisterCallback<KeyDownEvent>(OnKeyDown);
                UnregisterCallback<MouseDownEvent>(OnMouseDown);
            }   
            
            return;

            void OnFocusOut(FocusOutEvent e)
            {
                EndEditTitle();
            }

            void OnKeyDown(KeyDownEvent e)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    EndEditTitle();
                }
            }

            void OnMouseDown(MouseDownEvent e)
            {
                if (e.clickCount == 2)
                {
                    BeginEditTitle();
                    e.StopPropagation();
                }
            }
        }
    }

    /// <summary>
    /// 红点节点
    /// </summary>
    public class ReddotNode : Node
    {
        private readonly TextField keyText;
        private readonly EditableLabel nameText;
        private readonly Port inputPort;
        private readonly Port outputPort;
        public string Key { get { return keyText.value; } }
        public string ReddotName { get { return nameText.text; } }

        public string[] ReddotChildren
        {
            get
            {
                List<string> children = new List<string>();
                foreach (var item in outputPort.connections)
                {
                    var node = item.input.node as ReddotNode;
                    if (string.IsNullOrEmpty(node.Key))
                    {
                        throw new System.Exception("有节点的key为空，请检查");
                    }
                    children.Add(node.Key);
                }

                if (children.Count > 0)
                    return children.ToArray();
                else
                    return null;
            }
        }

        private readonly StyleColor m_StyleColor;

        public ReddotNode(string key="New Reddot Key", string name = "New Node Name")
        {
            m_StyleColor = mainContainer.style.backgroundColor;
            
            inputPort = ReddotPort.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "parents";
            inputContainer.Add(inputPort);

            outputPort = ReddotPort.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            outputPort.portName = "children";
            outputContainer.Add(outputPort);

            keyText = new TextField
            {
                value = key
            };
            mainContainer.Add(keyText);

            titleContainer.RemoveAt(1);  // 删除折叠按钮
            titleContainer.RemoveAt(0);  // 删除默认标题标签
            nameText = new EditableLabel
            {
                text = name,
                style =
                {
                    minWidth = 100,
                    marginLeft = 10
                }
            };
            titleContainer.Insert(0, nameText);
            
            UpdateState();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            if (!Application.isPlaying)
                return;
            
            if (ReddotManager.ContainsNode(Key) && ReddotManager.GetNodeIsLeaf(Key))
            {
                var content = ReddotManager.GetNodeDebugString(Key);
                Debug.Log(content);
            }
        }

        /// <summary>
        /// 添加一个子节点
        /// </summary>
        /// <param name="childNode">子节点</param>
        /// <returns></returns>
        public Edge AddChild(ReddotNode childNode)
        {
            return outputPort.ConnectTo(childNode.inputPort);
        }

        public void UpdateState()
        {
            if (Application.isPlaying)
            {
                // 判断节点激活状态，激活时修改背景色
                if (ReddotManager.ContainsNode(Key) && ReddotManager.GetNodeIsActive(Key))
                {
                    mainContainer.style.backgroundColor = new StyleColor(new Color(0.8f, 1f, 0.8f, 1f)); // 激活：淡绿色
                }
                else
                {
                    mainContainer.style.backgroundColor = m_StyleColor;
                    
                }
            }
            else
            {
                mainContainer.style.backgroundColor = m_StyleColor;
            }
        }

        public void OnEnableEdit()
        {
            pickingMode = PickingMode.Position;
            // 恢复交互能力（保守：允许选择/移动/删除/缩放）
            capabilities |= (Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable | Capabilities.Resizable);
            foreach (var port in inputContainer.Children().OfType<Port>())
            {
                port.pickingMode = PickingMode.Position;
            }
            foreach (var port in outputContainer.Children().OfType<Port>())
            {
                port.pickingMode = PickingMode.Position;
            }
            
            // 允许编辑但保持样式
            keyText.isReadOnly = false;
            nameText.SetEditable(true);
        }

        public void OnDisableEdit()
        {
            // 只禁用拾取，不改变可视样式
            pickingMode = PickingMode.Ignore;
            // 去除交互能力，避免选择/移动/删除
            capabilities &= ~(Capabilities.Movable | Capabilities.Deletable | Capabilities.Resizable);
            foreach (var port in inputContainer.Children().OfType<Port>())
            {
                port.pickingMode = PickingMode.Ignore;
            }
            foreach (var port in outputContainer.Children().OfType<Port>())
            {
                port.pickingMode = PickingMode.Ignore;
            }
            
            // 仅设为只读，避免样式变化
            keyText.isReadOnly = true;
            nameText.SetEditable(false);
        }
    }

    public class ReddotPort : Port
    {
        public static ReddotGraphView graphView;

        class EdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly GraphViewChange m_GraphViewChange;

            private readonly List<Edge> m_EdgesToCreate;

            private readonly List<GraphElement> m_EdgesToDelete;

            public EdgeConnectorListener()
            {
                m_EdgesToCreate = new List<Edge>();
                m_EdgesToDelete = new List<GraphElement>();
                m_GraphViewChange.edgesToCreate = m_EdgesToCreate;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                var screenPos = GUIUtility.GUIToScreenPoint(UnityEngine.Event.current.mousePosition);

                // 添加一个新节点并连接
                var node = new ReddotNode(graphView.GetDefaultReddotKey());
                graphView.AddNode(node, screenPos);
                
                if (edge.input is null)
                {
                    edge = (edge.output.node as ReddotNode).AddChild(node);
                }
                else
                {
                    edge = node.AddChild(edge.input.node as ReddotNode);
                }

                graphView.Add(edge);
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                m_EdgesToCreate.Clear();
                m_EdgesToCreate.Add(edge);
                m_EdgesToDelete.Clear();
                if (edge.input.capacity == Capacity.Single)
                {
                    foreach (Edge connection in edge.input.connections)
                    {
                        if (connection != edge)
                        {
                            m_EdgesToDelete.Add(connection);
                        }
                    }
                }

                if (edge.output.capacity == Capacity.Single)
                {
                    foreach (Edge connection2 in edge.output.connections)
                    {
                        if (connection2 != edge)
                        {
                            m_EdgesToDelete.Add(connection2);
                        }
                    }
                }

                if (m_EdgesToDelete.Count > 0)
                {
                    graphView.DeleteElements(m_EdgesToDelete);
                }

                List<Edge> edgesToCreate = m_EdgesToCreate;
                if (graphView.graphViewChanged != null)
                {
                    edgesToCreate = graphView.graphViewChanged(m_GraphViewChange).edgesToCreate;
                }

                foreach (Edge item in edgesToCreate)
                {
                    graphView.AddElement(item);
                    edge.input.Connect(item);
                    edge.output.Connect(item);
                }
            }
        }

        protected ReddotPort(Orientation portOrientation, Direction portDirection, Capacity portCapacity, Type type) : base(portOrientation, portDirection, portCapacity, type) { }

        public static new ReddotPort Create<TEdge>(Orientation orientation, Direction direction, Capacity capacity, Type type) where TEdge : Edge, new()
        {
            EdgeConnectorListener listener = new EdgeConnectorListener();
            ReddotPort port = new ReddotPort(orientation, direction, capacity, type)
            {
                m_EdgeConnector = new EdgeConnector<TEdge>(listener)
            };
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }
    }
}

