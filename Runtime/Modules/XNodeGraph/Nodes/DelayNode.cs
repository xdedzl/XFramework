using System.Collections.Generic;
using UnityEngine;

namespace XFramework.NodeKit
{
    public class DelayNode : ProcessNode
    {
        public float delay;

        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            Timer.Register(delay, () => { storyGraph.FinishNode(this); });
        }
    }
}