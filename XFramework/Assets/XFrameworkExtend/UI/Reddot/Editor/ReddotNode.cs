using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI.Editor
{
    using Node = UnityEditor.Experimental.GraphView.Node;

    /// <summary>
    /// 红点节点
    /// </summary>
    public class ReddotNode : Node
    {
        private TextField keyText;
        private TextField nameText;
        private Port inputPort;
        private Port outputPort;
        public string Key { get { return keyText.value; } }
        public string ReddotName { get { return nameText.value; } }

        public string[] RedotChildren
        {
            get
            {
                List<string> childen = new List<string>();
                foreach (var item in outputPort.connections)
                {
                    var node = item.input.node as ReddotNode;
                    if (string.IsNullOrEmpty(node.Key))
                    {
                        throw new System.Exception("有节点的key为空，请检查");
                    }
                    childen.Add(node.Key);
                }

                if (childen.Count > 0)
                    return childen.ToArray();
                else
                    return null;
            }
        }

        public ReddotNode() : this("", "") { }

        public ReddotNode(string key, string name = "Reddot")
        {
            title = "Reddot";

            inputPort = ReddotPort.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "parents";
            inputContainer.Add(inputPort);

            outputPort = ReddotPort.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            outputPort.portName = "children";
            outputContainer.Add(outputPort);

            keyText = new TextField();
            keyText.value = key;
            mainContainer.Add(keyText);

            titleContainer.RemoveAt(0);
            nameText = new TextField();
            nameText.value = name;
            nameText.style.minWidth = 100;
            nameText.style.maxWidth = 100;

            var inputElement = nameText.ElementAt(0);
            var color = new StyleColor(Color.clear);
            inputElement.style.backgroundColor = color;
            inputElement.style.borderLeftColor = color;
            inputElement.style.borderRightColor = color;
            inputElement.style.borderTopColor = color;
            inputElement.style.borderBottomColor = color;

            titleContainer.Insert(0, nameText);
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
    }

    public class ReddotPort : Port
    {
        public static ReddotGraphView graphView;

        class EdgeConnectorListener : IEdgeConnectorListener
        {
            private GraphViewChange m_GraphViewChange;

            private List<Edge> m_EdgesToCreate;

            private List<GraphElement> m_EdgesToDelete;

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
                var node = new ReddotNode();
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