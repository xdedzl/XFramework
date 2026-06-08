using System.Collections.Generic;
using System.Linq;

namespace XFramework.NodeKit
{
    public class ConditionNode : XNodeBase, ISwitchNode
    {
        public string valueNodeId;
        public string[] trueNodeIds;
        public string[] falseNodeIds;
        
        public void OnNodeStart(IXNodeGraph storyGraph)
        {
            storyGraph.FinishNode(this);
        }
        
        public IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args)
        {
            if (string.IsNullOrEmpty(valueNodeId))
            {
                return Enumerable.Empty<string>();
            }
            
            var valueNode = storyGraph.GetNode<IValueNode<bool>>(valueNodeId);
            if (valueNode == null)
            {
                throw new System.Exception($"ConditionNode GetNextNodeId: valueNode is null, valueNodeId: {valueNodeId}");
            }

            return valueNode.GetValue() ? trueNodeIds : falseNodeIds;
        }
    }


    public class SelectionNode : XNodeBase, ISwitchNode
    {
        public string valueNodeId;
        public string[][] nextNodes;
        
        public void OnNodeStart(IXNodeGraph storyGraph)
        {
            storyGraph.FinishNode(this);
        }
        
        public IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args)
        {
            if (string.IsNullOrEmpty(valueNodeId))
            {
                return Enumerable.Empty<string>();
            }
            
            var valueNode = storyGraph.GetNode<IValueNode<int>>(valueNodeId);
            if (valueNode == null)
            {
                throw new System.Exception($"ConditionNode GetNextNodeId: valueNode is null, valueNodeId: {valueNodeId}");
            }

            var index = valueNode.GetValue();

            if (index < 0 || index >= nextNodes.Length)
            {
                return Enumerable.Empty<string>();
            }
            
            return nextNodes[index];
        }
    }
}