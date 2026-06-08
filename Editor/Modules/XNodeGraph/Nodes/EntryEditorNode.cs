using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;

namespace XFramework.NodeKit.Editor
{
    [MenuPath("启动节点")]
    [TargetRuntimeNode(typeof(EntryNode))]
    public class EntryEditorNode : XEditorNodeBase<EntryNode>
    {
        private readonly Port outPutPort;

        public EntryEditorNode()
        {
            outPutPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            outPutPort.portName = "Next";
            outputContainer.Add(outPutPort);
        }
        
        public override void ApplyToRuntimeNode()
        {
            runtimeNode.nextNodeIds = GetConnectedNodeIds(outPutPort).ToArray();
        }

        public override void ApplyFromRuntimeNode()
        {
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
            throw new Exception("Delay Node has no passive input port");
        }

        public override Port GetOutputPort()
        {
            throw new Exception("Delay Node has no passive output port");
        }
    }
}