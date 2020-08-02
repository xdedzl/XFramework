using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI.Editor
{
    using Node = UnityEditor.Experimental.GraphView.Node;
    public class ReddotNode : Node
    {
        private TextField keyText;
        private TextField nameText;
        private Port inputPort;
        private Port outputPort;
        public string Key { get { return keyText.value; } }
        public string ReddotName { get { return nameText.value; } }

        public string[] RedotChildren
        {
            get
            {
                List<string> childen = new List<string>();
                foreach (var item in outputPort.connections)
                {
                    var node = item.input.node as ReddotNode;
                    if (string.IsNullOrEmpty(node.Key))
                    {
                        throw new System.Exception("有节点的key为空，请检查");
                    }
                    childen.Add(node.Key);
                }

                if (childen.Count > 0)
                    return childen.ToArray();
                else
                    return null;
            }
        }

        public ReddotNode() : this("", "") { }

        public ReddotNode(string key, string name = "Reddot")
        {
            title = "Reddot";

            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
            inputPort.portName = "parents";
            inputContainer.Add(inputPort);

            outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
            outputPort.portName = "children";
            outputContainer.Add(outputPort);

            keyText = new TextField();
            keyText.value = key;
            mainContainer.Add(keyText);

            titleContainer.RemoveAt(0);
            nameText = new TextField();
            nameText.value = name;
            nameText.style.minWidth = 100;
            nameText.style.maxWidth = 100;

            var inputElement = nameText.ElementAt(0);
            var color = new StyleColor(Color.clear);
            inputElement.style.backgroundColor = color;
            inputElement.style.borderLeftColor = color;
            inputElement.style.borderRightColor = color;
            inputElement.style.borderTopColor = color;
            inputElement.style.borderBottomColor = color;

            titleContainer.Insert(0, nameText);
        }

        public Edge AddChild(ReddotNode childNode)
        {
            return outputPort.ConnectTo(childNode.inputPort);
        }
    }
}