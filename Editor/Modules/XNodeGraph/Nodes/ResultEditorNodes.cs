using System;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.NodeKit.Editor
{
    public abstract class ResultEditorNode<TNode, TValue> : XEditorNodeBase<TNode>
        where TNode : ValueResultNode<TValue>, new()
    {
        private readonly Port inputPort;

        protected ResultEditorNode()
        {
            inputPort = XPort.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "Pre";
            inputContainer.Add(inputPort);
        }

        public override void ApplyFromRuntimeNode()
        {
        }

        public override Port GetInputPort()
        {
            return inputPort;
        }

        public override Port GetOutputPort()
        {
            throw new Exception("Result Node has no passive output port");
        }
    }

    [MenuPath("结果节点/Int Result Node")]
    [TargetRuntimeNode(typeof(IntResultNode))]
    public class IntResultEditorNode : ResultEditorNode<IntResultNode, int>
    {
        private readonly IntField intField;

        public IntResultEditorNode()
        {
            intField = new IntField
            {
                label = "Value",
                value = 0,
            };
            contentContainer.Add(intField);
        }

        public override void ApplyFromRuntimeNode()
        {
            intField.value = runtimeNode.value;
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.value = intField.value;
        }
    }

    [MenuPath("结果节点/Float Result Node")]
    [TargetRuntimeNode(typeof(FloatResultNode))]
    public class FloatResultEditorNode : ResultEditorNode<FloatResultNode, float>
    {
        private readonly FloatField floatField;

        public FloatResultEditorNode()
        {
            floatField = new FloatField("Value")
            {
                value = 0f,
            };
            contentContainer.Add(floatField);
        }

        public override void ApplyFromRuntimeNode()
        {
            floatField.value = runtimeNode.value;
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.value = floatField.value;
        }
    }

    [MenuPath("结果节点/Bool Result Node")]
    [TargetRuntimeNode(typeof(BoolResultNode))]
    public class BoolResultEditorNode : ResultEditorNode<BoolResultNode, bool>
    {
        private readonly BoolField boolField;

        public BoolResultEditorNode()
        {
            boolField = new BoolField
            {
                label = "Value",
                value = false,
            };
            contentContainer.Add(boolField);
        }

        public override void ApplyFromRuntimeNode()
        {
            boolField.value = runtimeNode.value;
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.value = boolField.value;
        }
    }

    [MenuPath("结果节点/String Result Node")]
    [TargetRuntimeNode(typeof(StringResultNode))]
    public class StringResultEditorNode : ResultEditorNode<StringResultNode, string>
    {
        private readonly TextField stringField;

        public StringResultEditorNode()
        {
            stringField = new TextField
            {
                label = "Value",
                value = string.Empty,
            };
            contentContainer.Add(stringField);
        }

        public override void ApplyFromRuntimeNode()
        {
            stringField.value = runtimeNode.value;
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.value = stringField.value;
        }
    }

    [MenuPath("结果节点/Vector3 Result Node")]
    [TargetRuntimeNode(typeof(Vector3ResultNode))]
    public class Vector3ResultEditorNode : ResultEditorNode<Vector3ResultNode, Vector3>
    {
        private readonly Vector3Field vector3Field;

        public Vector3ResultEditorNode()
        {
            vector3Field = new Vector3Field("Value")
            {
                value = Vector3.zero,
            };
            contentContainer.Add(vector3Field);
        }

        public override void ApplyFromRuntimeNode()
        {
            vector3Field.value = runtimeNode.value;
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.value = vector3Field.value;
        }
    }

    [MenuPath("结果节点/GameObject Ref Result Node")]
    [TargetRuntimeNode(typeof(GameObjectRefResultNode))]
    public class GameObjectRefResultEditorNode : XEditorNodeBase<GameObjectRefResultNode>
    {
        private readonly Port inputPort;
        private readonly TextField keyField;

        public GameObjectRefResultEditorNode()
        {
            inputPort = XPort.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "Pre";
            inputContainer.Add(inputPort);

            keyField = new TextField
            {
                label = "Key",
                value = string.Empty,
            };
            contentContainer.Add(keyField);
        }

        public override void ApplyFromRuntimeNode()
        {
            keyField.value = runtimeNode.key;
        }

        public override void ApplyToRuntimeNode()
        {
            runtimeNode.key = keyField.value;
        }

        public override Port GetInputPort()
        {
            return inputPort;
        }

        public override Port GetOutputPort()
        {
            throw new Exception("Result Node has no passive output port");
        }
    }
}
