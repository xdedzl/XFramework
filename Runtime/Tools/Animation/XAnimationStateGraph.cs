using System;

namespace XFramework.Animation
{
    public sealed class XAnimationStateGraph
    {
        public void Evaluate(XAnimationContext context)
        {
            throw new NotSupportedException("XAnimationStateGraph runtime is not supported in phase 1.");
        }

        public void Reset()
        {
        }
    }
}
