using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Node = UnityEditor.Experimental.GraphView.Node;

namespace XFramework.NodeKit.Editor
{
    public class TargetRuntimeNodeAttribute : Attribute
    {
        public Type targetType;

        public TargetRuntimeNodeAttribute(Type runtimeNodeType)
        {
            this.targetType = runtimeNodeType;
        }
    }
    
    public class XPort : Port
    {
        private class XEdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly GraphViewChange m_GraphViewChange;

            private readonly List<Edge> m_EdgesToCreate;

            private readonly List<GraphElement> m_EdgesToDelete;

            public XEdgeConnectorListener()
            {
                m_EdgesToCreate = new List<Edge>();
                m_EdgesToDelete = new List<GraphElement>();
                m_GraphViewChange.edgesToCreate = m_EdgesToCreate;
            }

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                var node = edge?.output?.node as XEditorNodeBase ?? edge?.input?.node as XEditorNodeBase;
                if (node?.OwnerGraphView == null)
                {
                    return;
                }

                var screenPos = GUIUtility.GUIToScreenPoint(position);
                var searchWindowProvider = XNodeSelectWindowProvider.Create(node.OwnerGraphView, node.OwnerGraphView.OwnerWindow);
                SearchWindow.Open(new SearchWindowContext(screenPos), searchWindowProvider);
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

        protected XPort(Orientation portOrientation, Direction portDirection, Capacity portCapacity, Type type) : base(portOrientation, portDirection, portCapacity, type) { }

        public new static XPort Create<TEdge>(Orientation orientation, Direction direction, Capacity capacity, Type type) where TEdge : Edge, new()
        {
            XEdgeConnectorListener listener = new XEdgeConnectorListener();
            XPort port = new XPort(orientation, direction, capacity, type)
            {
                m_EdgeConnector = new EdgeConnector<TEdge>(listener)
            };
            port.AddManipulator(port.m_EdgeConnector);
            return port;
        }
    }


    public abstract class XEditorNodeBase : Node
    {
        private bool m_IsContentLayoutRefreshScheduled;

        protected abstract string nodeTitleName { get; }
        
        protected abstract string nodeName { get; set; }
        
        protected string showName => string.IsNullOrEmpty(nodeName) ? nodeTitleName : $"{nodeName} ({nodeTitleName})";
        
        public XNodeGraphView OwnerGraphView { get; private set; }

        protected XNodeGraphView graphView => OwnerGraphView;
        
        public abstract IXNode GetRuntimeNode();
        
        public abstract void SetRuntimeNode(IXNode node);
        
        public abstract void ApplyToRuntimeNode();
        public abstract void ApplyFromRuntimeNode();

        public abstract Port GetInputPort();
        public abstract Port GetOutputPort();
        
        protected XEditorNodeBase()
        {
            title = nodeTitleName;
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => ScheduleContentLayoutRefresh());
        }

        private void ScheduleContentLayoutRefresh()
        {
            if (m_IsContentLayoutRefreshScheduled)
            {
                return;
            }

            m_IsContentLayoutRefreshScheduled = true;
            schedule.Execute(() =>
            {
                m_IsContentLayoutRefreshScheduled = false;
                RefreshExpandedState();
                RefreshPorts();
                MarkDirtyRepaint();
            }).ExecuteLater(0);
        }

        public void SetOwnerGraphView(XNodeGraphView ownerGraphView)
        {
            OwnerGraphView = ownerGraphView;
        }

        public abstract string GetId();

        protected IEnumerable<XEditorNodeBase> GetConnectedNodes(Port port)
        {
            if (port == null)
            {
                return Enumerable.Empty<XEditorNodeBase>();
            }

            var connected = port.direction == Direction.Output
                ? port.connections.Select(e => e.input?.node)
                : port.connections.Select(e => e.output?.node);

            return connected.OfType<XEditorNodeBase>();
        }
        
        protected IEnumerable<string> GetConnectedNodeIds(Port port)
        {
            return GetConnectedNodes(port).Select(n => n.GetId());
        }

        protected string GetConnectedNodeId(Port port)
        {
            return GetConnectedNodeIds(port).FirstOrDefault();
        }
    }
    
    public abstract class XEditorNodeBase<T> : XEditorNodeBase where T : XNodeBase, new()
    {
        protected T runtimeNode;
        
        protected override string nodeTitleName => GetShortNodeTypeName();


        protected override string nodeName
        {
            get
            {
                return runtimeNode.name;
            }
            set
            {
                runtimeNode.name = value;
                title = showName;
            }
        }
        
        private Label m_CachedTitleLabel;
        private int m_CachedLabelIndex = -1;
        
        public override string GetId()
        {
            return runtimeNode.GetId();
        }

        private static string GetShortNodeTypeName()
        {
            const string nodeSuffix = "Node";
            var typeName = typeof(T).Name;
            return typeName.EndsWith(nodeSuffix, StringComparison.Ordinal) 
                ? typeName.Substring(0, typeName.Length - nodeSuffix.Length) 
                : typeName;
        }
        
        protected XEditorNodeBase()
        {
            this.RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenuPopulate);
            
            // 监听 title 区域的双击事件
            titleContainer.RegisterCallback<MouseDownEvent>((evt) =>
            {
                if (evt.clickCount == 2)
                {
                    BeginEditTitle();
                    evt.StopPropagation();
                }
            });
        }
        
        public override IXNode GetRuntimeNode()
        {
            return runtimeNode;
        }

        public override void OnSelected()
        {
            base.OnSelected();
            graphView?.OwnerWindow?.BindInspectorTarget(runtimeNode);
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            var selectedNode = graphView?.selection
                .OfType<XEditorNodeBase>()
                .LastOrDefault();
            if (selectedNode != null)
            {
                graphView?.OwnerWindow?.BindInspectorTarget(selectedNode.GetRuntimeNode());
                return;
            }

            graphView?.OwnerWindow?.ClearInspectorTarget();
        }
        
        public override void SetRuntimeNode(IXNode node)
        {
            runtimeNode = node as T;
            if (runtimeNode is not null)
            {
                title = showName;
                OnRuntimeNodeChange();
            }
            else
            {
                Debug.LogError($"XEditorNodeBase<{typeof(T).Name}> SetRuntimeNode传入的node类型错误，无法转换为{typeof(T).Name}");
            }
        }

        public virtual void OnRuntimeNodeChange()
        {
            
        }

        private void BeginEditTitle()
        {
            m_CachedTitleLabel = titleContainer.Children().OfType<Label>().FirstOrDefault();
            if (m_CachedTitleLabel == null)
            {
                throw new Exception("无法找到标题Label控件");
            }
            m_CachedLabelIndex = titleContainer.IndexOf(m_CachedTitleLabel);
            titleContainer.Remove(m_CachedTitleLabel);
            var textField = new TextField
            {
                value = nodeName,
                style =
                {
                    flexGrow = 1
                }
            };
            titleContainer.Insert(m_CachedLabelIndex, textField);
            textField.Focus();
            textField.SelectAll();
            textField.RegisterCallback<FocusOutEvent>((e) => EndEditTitle(textField));
            textField.RegisterCallback<KeyDownEvent>((e) =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    EndEditTitle(textField);
                }
            });
        }

        private void EndEditTitle(TextField textField)
        {
            titleContainer.Remove(textField);

            if (m_CachedLabelIndex >= 0 && m_CachedLabelIndex <= titleContainer.childCount)
            {
                titleContainer.Insert(m_CachedLabelIndex, m_CachedTitleLabel);
            }
            else
            {
                titleContainer.Add(m_CachedTitleLabel);
            }
            m_CachedTitleLabel = null;
            m_CachedLabelIndex = -1;
            
            nodeName = textField.value;
        }

        private void OnContextMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            evt.menu.InsertAction(0, "设为启动节点", (action) =>
            {
                if(runtimeNode is IProcessNode)
                {
                    
                }
                else
                {
                    Debug.LogError("只能将流程节点设为启动节点，当前节点类型：" + typeof(T).Name);
                }
            });
            evt.menu.InsertAction(1, "打开 EditorNode 脚本", (action) => OpenScriptAsset(GetType(), "EditorNode"));
            evt.menu.InsertAction(2, "打开 RuntimeNode 脚本", (action) => OpenScriptAsset(typeof(T), "RuntimeNode"));
        }

        private static void OpenScriptAsset(Type scriptType, string scriptLabel)
        {
            var monoScript = FindMonoScript(scriptType);
            if (monoScript == null)
            {
                Debug.LogError($"未找到 {scriptLabel} 脚本: {scriptType.FullName}");
                return;
            }

            AssetDatabase.OpenAsset(monoScript);
        }

        private static MonoScript FindMonoScript(Type scriptType)
        {
            var guids = AssetDatabase.FindAssets($"{scriptType.Name} t:MonoScript");
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                if (monoScript?.GetClass() == scriptType)
                {
                    return monoScript;
                }
            }

            return null;
        }
    }
}
