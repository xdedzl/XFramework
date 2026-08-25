using System;
using System.Collections.Generic;

namespace XFramework.Command
{
    public enum XCommandSource
    {
        Api,
        Editor,
        Ugui,
        Cli,
        Hunter
    }

    public enum XCommandRecordState
    {
        Running,
        Completed
    }

    public sealed class XCommandRecord
    {
        internal XCommandRecord(long id, DateTime executedAtUtc, XCommandSource source, string commandLine)
        {
            Id = id;
            ExecutedAtUtc = executedAtUtc;
            Source = source;
            CommandLine = commandLine;
            State = XCommandRecordState.Running;
        }

        public long Id { get; }
        public DateTime ExecutedAtUtc { get; }
        public DateTime? CompletedAtUtc { get; private set; }
        public XCommandSource Source { get; }
        public XCommandRecordState State { get; private set; }
        public string CommandLine { get; }
        public XCommandDescriptor Command { get; private set; }
        public XCommandExecutionStatus Status { get; private set; }
        public double DurationMilliseconds { get; private set; }
        public string Message { get; private set; }
        public string Output { get; private set; }
        public string Exception { get; private set; }
        public bool Succeeded => State == XCommandRecordState.Completed && Status == XCommandExecutionStatus.Succeeded;

        internal void Complete(XCommandExecutionResult result)
        {
            CompletedAtUtc = DateTime.UtcNow;
            State = XCommandRecordState.Completed;
            Command = result.Command;
            Status = result.Status;
            DurationMilliseconds = result.DurationMilliseconds;
            Message = result.Message;
            Output = result.Succeeded ? result.Value == null ? "OK" : result.Value.ToString() : string.Empty;
            Exception = result.Exception?.ToString() ?? string.Empty;
        }

        internal void CompleteLegacy(bool succeeded, object value, Exception exception, double durationMilliseconds)
        {
            CompletedAtUtc = DateTime.UtcNow;
            State = XCommandRecordState.Completed;
            Status = exception == null ? succeeded ? XCommandExecutionStatus.Succeeded : XCommandExecutionStatus.NotFound : XCommandExecutionStatus.Failed;
            DurationMilliseconds = durationMilliseconds;
            Message = exception == null ? succeeded ? "执行成功。" : "命令未被当前执行器处理。" : exception.Message;
            Output = succeeded ? value == null ? "OK" : value.ToString() : string.Empty;
            Exception = exception?.ToString() ?? string.Empty;
        }
    }

    public static class XCommandHub
    {
        public const int RecordLimit = 2000;
        public const int CommandHistoryLimit = 50;

        private static readonly List<XCommandRecord> s_Records = new List<XCommandRecord>();
        private static readonly List<string> s_CommandHistory = new List<string>();
        private static long s_NextRecordId = 1;
        private static XCommandSource s_CurrentExecutionSource = XCommandSource.Api;

        public static IReadOnlyList<XCommandRecord> Records => s_Records;
        public static IReadOnlyList<string> CommandHistory => s_CommandHistory;
        public static long LatestRecordId => s_NextRecordId - 1;

        public static event Action<XCommandRecord> RecordAdded;
        public static event Action<XCommandRecord> RecordUpdated;
        public static event Action<long> RecordsTrimmed;
        public static event Action CommandHistoryChanged;
        public static event Action<XCommandSource> DisplayClearRequested;
        public static event Action<XCommandSource> DisplayHistoryRequested;

        public static XCommandExecutionResult Execute(string commandLine, XCommandSource source = XCommandSource.Api)
        {
            string normalizedCommandLine = commandLine?.Trim() ?? string.Empty;
            if (normalizedCommandLine.Length == 0)
            {
                return XCommandRegistry.ExecuteCore(normalizedCommandLine);
            }

            bool recordExecution = !XCommandRegistry.TryGetCommand(normalizedCommandLine, out XCommandDescriptor command) || command.RecordExecution;
            XCommandRecord record = recordExecution ? BeginExecution(normalizedCommandLine, source) : null;
            XCommandSource previousSource = s_CurrentExecutionSource;
            s_CurrentExecutionSource = source;
            try
            {
                XCommandExecutionResult result = XCommandRegistry.ExecuteCore(normalizedCommandLine);
                if (recordExecution)
                {
                    record.Complete(result);
                    RecordUpdated?.Invoke(record);
                }
                return result;
            }
            finally
            {
                s_CurrentExecutionSource = previousSource;
            }
        }

        internal static void RequestDisplayClear()
        {
            DisplayClearRequested?.Invoke(s_CurrentExecutionSource);
        }

        internal static void RequestDisplayHistory()
        {
            DisplayHistoryRequested?.Invoke(s_CurrentExecutionSource);
        }

        public static void RestoreCommandHistory(IEnumerable<string> commandHistory)
        {
            s_CommandHistory.Clear();
            foreach (string commandLine in commandHistory)
            {
                if (s_CommandHistory.Count == CommandHistoryLimit)
                {
                    break;
                }
                if (!string.IsNullOrWhiteSpace(commandLine))
                {
                    s_CommandHistory.Add(commandLine.Trim());
                }
            }
            CommandHistoryChanged?.Invoke();
        }

        internal static bool ExecuteLegacy(string commandLine, XCommandSource source, CommandDelegate execute, out object value)
        {
            string normalizedCommandLine = commandLine?.Trim() ?? string.Empty;
            if (normalizedCommandLine.Length == 0)
            {
                value = null;
                return false;
            }

            XCommandRecord record = BeginExecution(normalizedCommandLine, source);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                bool succeeded = execute(normalizedCommandLine, out value);
                stopwatch.Stop();
                record.CompleteLegacy(succeeded, value, null, stopwatch.Elapsed.TotalMilliseconds);
                RecordUpdated?.Invoke(record);
                return succeeded;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                record.CompleteLegacy(false, null, exception, stopwatch.Elapsed.TotalMilliseconds);
                RecordUpdated?.Invoke(record);
                throw;
            }
        }

        private static XCommandRecord BeginExecution(string commandLine, XCommandSource source)
        {
            var record = new XCommandRecord(s_NextRecordId++, DateTime.UtcNow, source, commandLine);
            s_Records.Add(record);
            s_CommandHistory.Insert(0, commandLine);
            if (s_CommandHistory.Count > CommandHistoryLimit)
            {
                s_CommandHistory.RemoveAt(s_CommandHistory.Count - 1);
            }

            if (s_Records.Count > RecordLimit)
            {
                long removedRecordId = s_Records[0].Id;
                s_Records.RemoveAt(0);
                RecordsTrimmed?.Invoke(removedRecordId);
            }

            CommandHistoryChanged?.Invoke();
            RecordAdded?.Invoke(record);
            return record;
        }
    }
}
