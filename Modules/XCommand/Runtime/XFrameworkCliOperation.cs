using System;

namespace XFramework.Command
{
    public enum XFrameworkCliOperationState
    {
        Queued,
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    public sealed class XFrameworkCliOperationInfo
    {
        public XFrameworkCliOperationInfo(long id, string name, XFrameworkCliOperationState state, DateTime createdTimeUtc, DateTime? startedTimeUtc, DateTime? completedTimeUtc, double durationMilliseconds, string output, string error)
        {
            Id = id;
            Name = name;
            State = state;
            CreatedTimeUtc = createdTimeUtc;
            StartedTimeUtc = startedTimeUtc;
            CompletedTimeUtc = completedTimeUtc;
            DurationMilliseconds = durationMilliseconds;
            Output = output;
            Error = error;
        }

        public long Id { get; }
        public string Name { get; }
        public XFrameworkCliOperationState State { get; }
        public DateTime CreatedTimeUtc { get; }
        public DateTime? StartedTimeUtc { get; }
        public DateTime? CompletedTimeUtc { get; }
        public double DurationMilliseconds { get; }
        public string Output { get; }
        public string Error { get; }
    }

    public interface IXFrameworkCliOperationProvider
    {
        bool TryResolveOperation(object commandResult, out long operationId);
        bool TryGetOperation(long operationId, out XFrameworkCliOperationInfo operation);
    }
}
