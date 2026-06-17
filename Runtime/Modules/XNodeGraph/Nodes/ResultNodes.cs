using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.NodeKit
{
    public abstract class ResultNode<T> : ImmediatelyProcessNodeBase
    {
        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            storyGraph.SetResult(GetResult());
            storyGraph.FinishNode(this);
        }

        protected abstract T GetResult();

        public override IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args)
        {
            return Array.Empty<string>();
        }
    }

    public abstract class ValueResultNode<T> : ResultNode<T>
    {
        public T value;

        protected override T GetResult()
        {
            return value;
        }
    }

    public class IntResultNode : ValueResultNode<int> { }

    public class FloatResultNode : ValueResultNode<float> { }

    public class BoolResultNode : ValueResultNode<bool> { }

    public class StringResultNode : ValueResultNode<string> { }

    public class Vector3ResultNode : ValueResultNode<Vector3> { }

    public class GameObjectRefResultNode : ResultNode<GameObject>
    {
        public string key;

        protected override GameObject GetResult()
        {
            return string.IsNullOrWhiteSpace(key) ? null : UObjectFinder.Find(key);
        }
    }
}
