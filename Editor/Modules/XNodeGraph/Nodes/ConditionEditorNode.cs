using System.Linq;
using UnityEditor.Experimental.GraphView;

namespace XFramework.NodeKit.Editor
{
    [MenuPath("条件节点/Condition Node")]
    [TargetRuntimeNode(typeof(ConditionNode))]
    public class ConditionEditorNode : XEditorNodeBase<ConditionNode>
    {
        private readonly Port valuePort;
        private readonly Port inputPort;
        private readonly Port truePort;
        private readonly Port falsePort;
        
        public ConditionEditorNode() : base()
        {
            valuePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            valuePort.portName = "InputValue";
            inputContainer.Add(valuePort);
            
            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "Pre";
            inputContainer.Add(inputPort);
            
            truePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            truePort.portName = "True";
            outputContainer.Add(truePort);
            
            falsePort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            falsePort.portName = "False";
            outputContainer.Add(falsePort);
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.trueNodeIds = GetConnectedNodeIds(truePort).ToArray();
            runtimeNode.falseNodeIds = GetConnectedNodeIds(falsePort).ToArray();
            runtimeNode.valueNodeId = GetConnectedNodeId(valuePort);
        }

        public override void ApplyFromRuntimeNode()
        {
            foreach (var nodeId in runtimeNode.trueNodeIds)
            {
                var editorNode = graphView.GetXNode(nodeId);
                var otherInputPort = editorNode.GetInputPort();
                var edge = truePort.ConnectTo(otherInputPort);
                graphView.AddElement(edge);
            }
            foreach (var nodeId in runtimeNode.falseNodeIds)
            {
                var editorNode = graphView.GetXNode(nodeId);
                var otherInputPort = editorNode.GetInputPort();
                var edge = falsePort.ConnectTo(otherInputPort);
                graphView.AddElement(edge);
            }
            
            var valueNodeId = runtimeNode.valueNodeId;
            if (!string.IsNullOrEmpty(valueNodeId))
            {
                var editorNode = graphView.GetXNode(valueNodeId);
                var otherOutputPort = editorNode.GetOutputPort();
                var edge = valuePort.ConnectTo(otherOutputPort);
                graphView.AddElement(edge);
            }
        }
        
        public override Port GetInputPort()
        {
            return inputPort;
        }
        
        public override Port GetOutputPort()
        {
            throw new System.Exception("Condition Node has no passive output port");
        }
    }
    
    [MenuPath("条件节点/Selection Node")]
    [TargetRuntimeNode(typeof(SelectionNode))]
    public class SelectorEditorNode : XEditorNodeBase<SelectionNode>
    {
        public SelectorEditorNode() : base()
        {
        }

        public override void ApplyToRuntimeNode()
        {
        }

        public override void ApplyFromRuntimeNode()
        {
        }

        public override Port GetInputPort()
        {
            throw new System.Exception("Selector Node has no passive input port");
        }

        public override Port GetOutputPort()
        {
            throw new System.Exception("Selector Node has no passive output port");
        }
    }
}
