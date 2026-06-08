using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace XFramework.NodeKit.Editor
{
    public abstract class DebugEditorNodeBase<T>: XEditorNodeBase<T> where T : DebugNodeBase, new()
    {
        private readonly Port inputPort;
        private readonly Port outPutPort;
        private readonly TextField textField;
            
        protected DebugEditorNodeBase() : base()
        {
            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "Pre";
            inputContainer.Add(inputPort);
            
            outPutPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            outPutPort.portName = "Next";
            outputContainer.Add(outPutPort);
                
            textField = new TextField("Content:")
            {
                multiline = true,
                style =
                {
                    // minHeight = 60,
                    maxWidth = 300,
                    minWidth = 200,
                    whiteSpace = WhiteSpace.Normal,
                }
            };
            textField.labelElement.style.width = 50;
            textField.labelElement.style.minWidth = 50;
            textField.labelElement.style.maxWidth = 50;
            
            contentContainer.Add(textField);
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

            textField.value = runtimeNode.content;
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.nextNodeIds = GetConnectedNodeIds(outPutPort).ToArray();
            runtimeNode.content = textField.value;
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
    
    [TargetRuntimeNode(typeof(LogNode))]
    [MenuPath("Debug/Log")]
    public class LogEditorNode : DebugEditorNodeBase<LogNode>
    {

    }
    
    [TargetRuntimeNode(typeof(ErrorNode))]
    [MenuPath("Debug/Error")]
    public class ErrorEditorNode : DebugEditorNodeBase<ErrorNode>
    {
    }

    [TargetRuntimeNode(typeof(WarningNode))]
    [MenuPath("Debug/Warning")]
    public class WarningEditorNode : DebugEditorNodeBase<WarningNode>
    {
    }
}