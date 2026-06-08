using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;

namespace XFramework.NodeKit.Editor
{
    public abstract class ProcessEditorNode<T>:XEditorNodeBase<T> where T : ProcessNode, new()
    {
        private readonly Port inputPort;
        private readonly Port outPutPort;

        protected ProcessEditorNode()
        {
            inputPort = XPort.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "Pre";
            inputContainer.Add(inputPort);
            
            outPutPort = XPort.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            outPutPort.portName = "Next";
            outputContainer.Add(outPutPort);
        }
        
        public override void ApplyToRuntimeNode()
        {
            runtimeNode.nextNodeIds = GetConnectedNodeIds(outPutPort).ToArray();
        }

        public override void ApplyFromRuntimeNode()
        {
            if(runtimeNode.nextNodeIds == null) return;
            foreach (var nodeId in runtimeNode.nextNodeIds)
            {
                var editorNode = graphView.GetXNode(nodeId);
                var otherInputPort = editorNode.GetInputPort(); 
                var edge = outPutPort.ConnectTo(otherInputPort);
                graphView.AddElement(edge);
            }
        }

        public override Port GetInputPort()
        {
            return inputPort;
        }

        public override Port GetOutputPort()
        {
            throw new Exception("Delay Node has no passive output port");
        }
    }
    
    [MenuPath("流程节点/Process Node")]
    [TargetRuntimeNode(typeof(ProcessNode))]
    public class ProcessEditorNode : ProcessEditorNode<ProcessNode>
    {
    }
}