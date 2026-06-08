using System;
using UnityEditor.Experimental.GraphView;

namespace XFramework.NodeKit.Editor
{
    [TargetRuntimeNode(typeof(DelayNode))]
    [MenuPath("控制节点/Delay Node")]
    public class DelayEditorNode : ProcessEditorNode<DelayNode>
    {
        private readonly FloatField floatField;

        public DelayEditorNode():base()
        {
            floatField = new FloatField("Delay")
            {
                value = 0f,
            };
            contentContainer.Add(floatField);
        }
        
        public override void ApplyToRuntimeNode()
        {
            base.ApplyToRuntimeNode();
            runtimeNode.delay = floatField.value;
        }

        public override void ApplyFromRuntimeNode()
        {
            base.ApplyFromRuntimeNode();
            floatField.value = runtimeNode.delay;
        }
    }
}