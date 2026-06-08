using System.Collections.Generic;
using UnityEngine;

namespace XFramework.NodeKit
{
    public abstract class ProcessNodeBase : XNodeBase, IProcessNode
    {
        public abstract IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args);

        public virtual void OnNodeStart(IXNodeGraph storyGraph)
        {
            
        }
    }
    
    public abstract class ImmediatelyProcessNodeBase : ProcessNodeBase
    {
        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            storyGraph.FinishNode(this);
        }
    }
    
    public abstract class ImmediatelyProcessNode : ImmediatelyProcessNodeBase
    {
        [HideInInspector]
        public string[] nextNodeIds;

        public override IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args)
        {
            return nextNodeIds;
        }
        
        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            storyGraph.FinishNode(this);
        }
    }
    
    public class ProcessNode : ProcessNodeBase
    {
        [HideInInspector]
        public string[] nextNodeIds;

        public override IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args)
        {
            return nextNodeIds;
        }
    }
}
