using System.Collections.Generic;
using UnityEngine;

namespace XFramework.NodeKit
{
    public abstract class DebugNodeBase : ProcessNode
    {
        public string content;
    }
    
    public class LogNode : DebugNodeBase
    {
        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            Debug.Log($"XNode Log: {content}");
            storyGraph.FinishNode(this);
        }
    }
    
    public class ErrorNode : DebugNodeBase
    {
        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            Debug.LogError($"XNode Error: {content}");
            storyGraph.FinishNode(this);
        }
    }
    
    public class WarningNode : DebugNodeBase
    {
        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            Debug.LogWarning($"XNode Waring: {content}");
            storyGraph.FinishNode(this);
        }
    }
}