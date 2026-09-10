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

    [Serializable]
    public sealed class XCommandRecordSnapshot
    {
        public long Id;
        public long ExecutedAtUtcTicks;
        public long CompletedAtUtcTicks;
        public XCommandSource Source;
        public XCommandRecordState State;
        public string CommandLine;
        public XCommandExecutionStatus Status;
        public double DurationMilliseconds;
        public string Message;
        public string Output;
        public string Exception;
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

        internal XCommandRecord(XCommandRecordSnapshot snapshot)
        {
            Id = snapshot.Id;
            ExecutedAtUtc = new DateTime(snapshot.ExecutedAtUtcTicks, DateTimeKind.Utc);
            CompletedAtUtc = snapshot.CompletedAtUtcTicks == 0 ? null : new DateTime(snapshot.CompletedAtUtcTicks, DateTimeKind.Utc);
            Source = snapshot.Source;
            State = snapshot.State;
            CommandLine = snapshot.CommandLine;
            Status = snapshot.Status;
            DurationMilliseconds = snapshot.DurationMilliseconds;
            Message = snapshot.Message;
            Output = snapshot.Output;
            Exception = snapshot.Exception;
            XCommandRegistry.TryGetCommand(CommandLine, out XCommandDescriptor command);
            Command = command;

            if (State == XCommandRecordState.Running)
            {
                CompletedAtUtc = DateTime.UtcNow;
                State = XCommandRecordState.Completed;
                Status = XCommandExecutionStatus.Failed;
                DurationMilliseconds = (CompletedAtUtc.Value - ExecutedAtUtc).TotalMilliseconds;
                Message = "命令执行因脚本重载中断。";
                Output = string.Empty;
                Exception = string.Empty;
            }
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

        public static IReadOnlyList<XCommandRecordSnapshot> CaptureRecordSnapshots()
        {
            var snapshots = new List<XCommandRecordSnapshot>(s_Records.Count);
            for (int i = 0; i < s_Records.Count; i++)
            {
                XCommandRecord record = s_Records[i];
                snapshots.Add(new XCommandRecordSnapshot
                {
                    Id = record.Id,
                    ExecutedAtUtcTicks = record.ExecutedAtUtc.Ticks,
                    CompletedAtUtcTicks = record.CompletedAtUtc?.Ticks ?? 0,
                    Source = record.Source,
                    State = record.State,
                    CommandLine = record.CommandLine,
                    Status = record.Status,
                    DurationMilliseconds = record.DurationMilliseconds,
                    Message = record.Message,
                    Output = record.Output,
                    Exception = record.Exception
                });
            }
            return snapshots;
        }

        public static void RestoreRecords(IEnumerable<XCommandRecordSnapshot> snapshots)
        {
            s_Records.Clear();
            long latestRecordId = 0;
            foreach (XCommandRecordSnapshot snapshot in snapshots)
            {
                var record = new XCommandRecord(snapshot);
                s_Records.Add(record);
                if (s_Records.Count > RecordLimit)
                {
                    s_Records.RemoveAt(0);
                }
                if (record.Id > latestRecordId)
                {
                    latestRecordId = record.Id;
                }
            }
            s_NextRecordId = latestRecordId + 1;
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
