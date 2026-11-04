using System;
using System.Collections;
using UnityEngine;

namespace XFramework.Command
{
    public sealed class WaitOperation : IXCommandAsyncOperation
    {
        private readonly IXCommandWaitRequest request;

        public WaitOperation(string name, IXCommandWaitRequest request)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("等待操作名称不能为空。", nameof(name));
            Name = name;
            this.request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public string Name { get; }
        public bool CanCancel => true;

        public IEnumerator Run(XCommandOperationContext context)
        {
            if (request.TimeoutSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(request), "timeout 必须大于 0。");

            request.Validate();

            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt <= request.TimeoutSeconds)
            {
                if (context.IsCancellationRequested)
                    yield break;
                if (request.Evaluate())
                {
                    context.SetOutput(request.CreateOutput(true, Time.realtimeSinceStartup - startedAt));
                    yield break;
                }
                yield return null;
            }

            context.SetOutput(request.CreateOutput(false, Time.realtimeSinceStartup - startedAt));
            throw new TimeoutException(request.TimeoutMessage);
        }
    }
}
