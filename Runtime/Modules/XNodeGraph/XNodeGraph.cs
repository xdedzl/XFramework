using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace XFramework.NodeKit
{
    public interface IXNodeGraph
    {
        string Id { get; }
        IXNode GetNode(string nodeId);
        void FinishNode(IXNode node, params object[] args);
        T GetNode<T>(string nodeId) where T : IXNode;
    }
    
    public partial class XNodeGraph : IXNodeGraph
    {
        private readonly Dictionary<string, IXNode> m_NodeDict = new ();
        private readonly XNodeManager m_Manager;
        
        private readonly Dictionary<string, IProcessNode> m_RunningNodes = new ();
        
        public string id { get; private set; }
        public string Id => id;

        public XNodeGraph(XNodeManager manager, XNodeGraphAsset asset)
        {
            var node = asset.nodes;

            m_Manager = manager;
            this.id = Guid.NewGuid().ToString("N");
            foreach (var n in node)
            {
                m_NodeDict.Add(n.GetId(), n);
            }
        }

        public void Start(params string[] nodeIds)
        {
            if (nodeIds == null || nodeIds.Length == 0)
            {
                foreach (var node in m_NodeDict.Values)
                {
                    if (node is EntryNode entryNode)
                    {
                        StartNode(entryNode.GetId());
                    }
                }
            }
            else
            {
                // todo: validate node ids
                foreach (var nodeId in nodeIds)
                {
                    StartNode(nodeId);
                }
            }
        }

        public void Pause()
        {
            
        }

        public void Stop()
        {
            
        }

        public void StartNode(string nodeId)
        {
            if (m_NodeDict.TryGetValue(nodeId, out var node))
            {
                if (node is IProcessNode processNode)
                {
                    m_RunningNodes.Add(nodeId, processNode);
                    processNode.OnNodeStart(this);
                }
                else
                {
                    throw new InvalidCastException($"Node with ID {nodeId} is not a process node in StoryGraph {id}.");
                }
            }
            else
            { 
                throw new KeyNotFoundException($"Node with ID {nodeId} not found in StoryGraph {id}.");
            }
        }
        
        public void FinishNode(string nodeId, params object[] args)
        {
            if(m_RunningNodes.Remove(nodeId, out var node))
            {
                var nextNodeIds = node.GetNextNodeIds(this, args);
                var nodeIds = nextNodeIds as string[] ?? nextNodeIds.ToArray();
                // Debug.Log($"[XNodeGraph] Node {nodeId} finished in StoryGraph {id}. Next nodes: {string.Join(", ", nodeIds)}");
                
                foreach (var nextNodeId in nodeIds)
                {
                    StartNode(nextNodeId);
                }

                // 如果没有新节点启动，且当前图中已无运行中的节点，则通知管理模块本剧情已自然结束
                if (m_RunningNodes.Count == 0 && nodeIds.Length == 0)
                {
                    m_Manager.StopGraph(this.id, true);
                }
            }
            else
            {
                throw new KeyNotFoundException($"Node with ID {nodeId} is not running in StoryGraph {id}.");
            }
        }
        
        public void FinishNode(IXNode node, params object[] args)
        {
            FinishNode(node.GetId(), args);
        }

        public IXNode GetNode(string nodeId)
        {
            if (m_NodeDict.TryGetValue(nodeId, out var node))
            {
                return node;
            }

            throw new KeyNotFoundException($"Node with ID {nodeId} not found in StoryGraph {id}.");
        }

        public T GetNode<T>(string nodeId) where T : IXNode
        {
            var node = GetNode(nodeId);
            if (node is T typedNode)
            {
                return typedNode;
            }

            throw new InvalidCastException($"Node with ID {nodeId} is not of type {typeof(T).Name}.");
        }
    }
}
