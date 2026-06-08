using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using XFramework.UI;

namespace XFramework.NodeKit.Editor
{
    public abstract class ValueEditorNode<T>: XEditorNodeBase<T> where T : XNodeBase, IValueNode, new()
    {
        private readonly Port inputPort;
        private readonly Port outputPort;

        protected ValueEditorNode():base()
        {
            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "input";
            inputContainer.Add(inputPort);
            
            outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            outputPort.portName = "output";
            outputContainer.Add(outputPort);
        }

        public override void ApplyToRuntimeNode()
        {
            
        }
        
        public override void ApplyFromRuntimeNode()
        {
            
        }

        public override Port GetInputPort()
        {
            throw new Exception("Value Node has no passive input port");
        }

        public override Port GetOutputPort()
        {
            return outputPort;
        }
    }
    
    [TargetRuntimeNode(typeof(IntValueNode))]
    [MenuPath("数值节点/Int Value Node")]
    public class IntValueEditorNode : ValueEditorNode<IntValueNode>
    {
        private readonly IntField intField;
        
        public IntValueEditorNode() : base()
        {
            intField = new IntField
            {
                label = "Value",
                value = 0,
            };
            
            intField.labelElement.style.width = 40;
            intField.labelElement.style.minWidth = 40;
            intField.labelElement.style.maxWidth = 40;
            
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

    [TargetRuntimeNode(typeof(FloatValueNode))]
    [MenuPath("数值节点/Float Value Node")]
    public class FloatValueEditorNode : ValueEditorNode<FloatValueNode>
    {
        private readonly FloatField floatField;

        public FloatValueEditorNode() : base()
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
    
    [TargetRuntimeNode(typeof(BoolValueNode))]
    [MenuPath("数值节点/Bool Value Node")]
    public class BoolValueEditorNode : ValueEditorNode<BoolValueNode>
    {
        private BoolField boolField;
        
        public BoolValueEditorNode() : base()
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

    [TargetRuntimeNode(typeof(StringValueNode))]
    [MenuPath("数值节点/String Value Node")]
    public class StringValueEditorNode : ValueEditorNode<StringValueNode>
    {
        private readonly TextField stringField;

        public StringValueEditorNode() : base()
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

    [TargetRuntimeNode(typeof(UObjectValueNode))]
    [MenuPath("数值节点/UObject Value Node")]
    public class UObjectValueEditorNode : ValueEditorNode<UObjectValueNode>
    {
        private readonly XInspector m_Inspector;

        public UObjectValueEditorNode()
        {
            style.minWidth = 220;
            m_Inspector = new XInspector(false)
            {
                style =
                {
                    width = 260,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f),
                    marginBottom = 2,
                    marginLeft = 2,
                    marginRight = 2,
                    marginTop = 2,
                    paddingLeft = 15,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5,
                    borderTopRightRadius = 5,
                    borderTopLeftRadius = 5,
                },
            };
            contentContainer.Add(m_Inspector);
            RefreshExpandedState();
            RefreshPorts();
        }

        public override void OnRuntimeNodeChange()
        {
            m_Inspector.Bind(runtimeNode);
        }
    }


    public class IntField : TextField
    {
        public new int value
        {
            get => int.Parse(base.value);
            set
            {
                base.value = value.ToString();
            }
        }
        
        public IntField() : base()
        {
            this.RegisterValueChangedCallback(OnValueChanged);
        }
        
        public IntField(string label) : base(label)
        {
            this.RegisterValueChangedCallback(OnValueChanged);
        }

        private void OnValueChanged(ChangeEvent<string> evt)
        {
            if (!int.TryParse(evt.newValue, out _))
            {
                base.value = evt.previousValue;
            }
        }
    }
    
    public class FloatField : TextField
    {
        public new float value
        {
            get => float.Parse(base.value);
            set
            {
                base.value = value.ToString();
            }
        }
        
        public FloatField(string label) : base(label)
        {
            this.RegisterValueChangedCallback(OnValueChanged);
        }

        private void OnValueChanged(ChangeEvent<string> evt)
        {
            if (!float.TryParse(evt.newValue, out _))
            {
                base.value = evt.previousValue;
            }
        }
    }
    
    public class BoolField : Toggle
    {
        
    }
}
