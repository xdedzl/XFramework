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
        Hunter,
        Web
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
            XCommand.Registry.TryGetCommand(CommandLine, out XCommandDescriptor command);
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

    public sealed class XCommandHub
    {
        internal XCommandHub()
        {
        }

        private const int RECORD_LIMIT = 2000;
        private const int COMMAND_HISTORY_LIMIT = 50;

        /// <summary>Hub 保留的命令执行记录上限。</summary>
        public int RecordLimit => RECORD_LIMIT;

        /// <summary>Hub 保留的命令输入历史上限。</summary>
        public int CommandHistoryLimit => COMMAND_HISTORY_LIMIT;

        private readonly List<XCommandRecord> s_Records = new List<XCommandRecord>();
        private readonly List<string> s_CommandHistory = new List<string>();
        private readonly string s_HistoryEpoch = Guid.NewGuid().ToString("N");
        private long s_NextRecordId = 1;
        private long s_HistoryRevision;
        private XCommandSource s_CurrentExecutionSource = XCommandSource.Api;

        public IReadOnlyList<XCommandRecord> Records => s_Records;
        public IReadOnlyList<string> CommandHistory => s_CommandHistory;
        public long LatestRecordId => s_NextRecordId - 1;
        public string HistoryVersion => s_HistoryEpoch + ":" + s_HistoryRevision;

        public event Action<XCommandRecord> RecordAdded;
        public event Action<XCommandRecord> RecordUpdated;
        public event Action<long> RecordsTrimmed;
        public event Action CommandHistoryChanged;
        public event Action<XCommandSource> DisplayClearRequested;
        public event Action<XCommandSource> DisplayHistoryRequested;
        internal event Action CommandRuntimeResetRequested;

        public XCommandExecutionResult Execute(string commandLine, XCommandSource source = XCommandSource.Api)
        {
            return Execute(commandLine, source, out _);
        }

        public XCommandExecutionResult Execute(string commandLine, XCommandSource source, out long recordId)
        {
            string normalizedCommandLine = commandLine?.Trim() ?? string.Empty;
            recordId = 0;
            if (normalizedCommandLine.Length == 0)
            {
                return XCommand.Registry.ExecuteCore(normalizedCommandLine);
            }

            bool commandFound = XCommand.Registry.TryGetCommand(normalizedCommandLine, out XCommandDescriptor command);
            if (!XCommand.IsEnabled)
            {
                return new XCommandExecutionResult(XCommandExecutionStatus.Unavailable, normalizedCommandLine, command, null, null, 0d, "XCommand 已关闭。");
            }

            bool recordExecution = !commandFound || command.RecordExecution;
            XCommandRecord record = recordExecution ? BeginExecution(normalizedCommandLine, source) : null;
            recordId = record?.Id ?? 0;
            XCommandSource previousSource = s_CurrentExecutionSource;
            s_CurrentExecutionSource = source;
            try
            {
                XCommandExecutionResult result = XCommand.Registry.ExecuteCore(normalizedCommandLine);
                if (recordExecution)
                {
                    record.Complete(result);
                    s_HistoryRevision++;
                    RecordUpdated?.Invoke(record);
                }
                return result;
            }
            finally
            {
                s_CurrentExecutionSource = previousSource;
            }
        }

        internal void RequestCommandRuntimeReset()
        {
            CommandRuntimeResetRequested?.Invoke();
        }

        internal void RequestDisplayClear()
        {
            DisplayClearRequested?.Invoke(s_CurrentExecutionSource);
        }

        internal void RequestDisplayHistory()
        {
            DisplayHistoryRequested?.Invoke(s_CurrentExecutionSource);
        }

        public void RestoreCommandHistory(IEnumerable<string> commandHistory)
        {
            s_CommandHistory.Clear();
            foreach (string commandLine in commandHistory)
            {
                if (s_CommandHistory.Count == COMMAND_HISTORY_LIMIT)
                {
                    break;
                }
                if (!string.IsNullOrWhiteSpace(commandLine))
                {
                    s_CommandHistory.Add(commandLine.Trim());
                }
            }
            s_HistoryRevision++;
            CommandHistoryChanged?.Invoke();
        }

        public IReadOnlyList<XCommandRecordSnapshot> CaptureRecordSnapshots()
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

        public void RestoreRecords(IEnumerable<XCommandRecordSnapshot> snapshots)
        {
            s_Records.Clear();
            long latestRecordId = 0;
            foreach (XCommandRecordSnapshot snapshot in snapshots)
            {
                var record = new XCommandRecord(snapshot);
                s_Records.Add(record);
                if (s_Records.Count > RECORD_LIMIT)
                {
                    s_Records.RemoveAt(0);
                }
                if (record.Id > latestRecordId)
                {
                    latestRecordId = record.Id;
                }
            }
            s_NextRecordId = latestRecordId + 1;
            s_HistoryRevision++;
        }

        internal bool ExecuteHandler(string commandLine, XCommandSource source, CommandDelegate execute, out object value)
        {
            if (!XCommand.IsEnabled)
            {
                value = "XCommand 已关闭。";
                return false;
            }

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
                s_HistoryRevision++;
                RecordUpdated?.Invoke(record);
                return succeeded;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                record.CompleteLegacy(false, null, exception, stopwatch.Elapsed.TotalMilliseconds);
                s_HistoryRevision++;
                RecordUpdated?.Invoke(record);
                throw;
            }
        }

        private XCommandRecord BeginExecution(string commandLine, XCommandSource source)
        {
            var record = new XCommandRecord(s_NextRecordId++, DateTime.UtcNow, source, commandLine);
            s_Records.Add(record);
            s_CommandHistory.Insert(0, commandLine);
            if (s_CommandHistory.Count > COMMAND_HISTORY_LIMIT)
            {
                s_CommandHistory.RemoveAt(s_CommandHistory.Count - 1);
            }

            if (s_Records.Count > RECORD_LIMIT)
            {
                long removedRecordId = s_Records[0].Id;
                s_Records.RemoveAt(0);
                RecordsTrimmed?.Invoke(removedRecordId);
            }

            s_HistoryRevision++;
            CommandHistoryChanged?.Invoke();
            RecordAdded?.Invoke(record);
            return record;
        }
    }
}
