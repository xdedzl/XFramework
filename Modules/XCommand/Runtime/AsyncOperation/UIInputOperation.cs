using System;
using System.Collections;

namespace XFramework.Command
{
    internal sealed class UIInputOperation : IXCommandAsyncOperation
    {
        private readonly XCommandInputSequence sequence;

        public UIInputOperation(XCommandInputSequence sequence)
        {
            if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
                throw new ArgumentException("输入序列必须包含非空 steps 数组。", nameof(sequence));
            this.sequence = sequence;
        }

        public string Name => "ui-input";

        public IEnumerator Run(XCommandOperationContext context)
        {
            return XCommandUtility.Input.Run(sequence, () => context.IsCancellationRequested, context.SetOutput);
        }
    }
}
