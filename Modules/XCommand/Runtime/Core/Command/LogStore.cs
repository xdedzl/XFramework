using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace XFramework.Command
{
    [Serializable]
    public sealed class XCommandLogQuery
    {
        public long since;
        public string level = "all";
        public string contains;
        public int limit = 200;
        public int messageLimit = 2048;
        public int stackLimit = 4096;
        public bool includeStack;
        public bool compact;
    }

    /// <summary>XCommand 运行时日志采集与查询的唯一实例。</summary>
    public sealed class LogStore
    {
        [Serializable]
        private sealed class RuntimeLogList
        {
            public long cursor;
            public long latestSequence;
            public long oldestSequence;
            public bool dropped;
            public bool hasMore;
            public int count;
            public long bufferBytes;
            public long capacityBytes;
            public List<RuntimeLogEntry> entries = new List<RuntimeLogEntry>();
        }

        [Serializable]
        public sealed class RuntimeLogEntry
        {
            public long sequence;
            public string timestampUtc;
            public int threadId;
            public string level;
            public string message;
            public bool messageTruncated;
            public string stackTrace;
            public bool stackTraceTruncated;

            [NonSerialized]
            public int storageBytes;
        }

        private const long CapacityBytes = 50L * 1024 * 1024;
        private const int EntryOverheadBytes = 64;
        private readonly object logLock = new object();
        private readonly Queue<RuntimeLogEntry> logQueue = new Queue<RuntimeLogEntry>();
        private long nextSequence = 1;
        private long bufferedBytes;

        internal LogStore()
        {
        }

        internal void Start()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        internal void Stop()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
        }

        internal void Reset()
        {
            Stop();
            lock (logLock)
            {
                logQueue.Clear();
                nextSequence = 1;
                bufferedBytes = 0;
            }
            Start();
        }

        internal string Query(XCommandLogQuery query)
        {
            int minimumSeverity = ParseMinimumSeverity(query.level);
            int limit = Mathf.Clamp(query.limit, 1, 1000);
            int messageLimit = Mathf.Clamp(query.messageLimit, 0, 65536);
            int stackLimit = Mathf.Clamp(query.stackLimit, 0, 65536);
            List<RuntimeLogEntry> captured;
            long latestSequence;
            long oldestSequence;
            long bufferBytes;
            lock (logLock)
            {
                captured = logQueue.Where(entry => entry.sequence > query.since)
                    .Where(entry => GetSeverity(entry.level) >= minimumSeverity)
                    .Where(entry => string.IsNullOrEmpty(query.contains) || XCommandUtility.Text.ContainsIgnoreCase(entry.message, query.contains))
                    .Take(limit + 1)
                    .ToList();
                latestSequence = nextSequence - 1;
                oldestSequence = logQueue.Count > 0 ? logQueue.Peek().sequence : nextSequence;
                bufferBytes = bufferedBytes;
            }

            bool hasMore = captured.Count > limit;
            captured = captured.Take(limit).Select(entry => CloneEntry(entry, messageLimit, stackLimit, query.includeStack)).ToList();
            var result = new RuntimeLogList {
                cursor = captured.Count > 0 ? captured[captured.Count - 1].sequence : query.since,
                latestSequence = latestSequence,
                oldestSequence = oldestSequence,
                dropped = query.since < oldestSequence - 1,
                hasMore = hasMore,
                count = captured.Count,
                bufferBytes = bufferBytes,
                capacityBytes = CapacityBytes,
                entries = captured,
            };
            return JsonUtility.ToJson(result, !query.compact);
        }

        public long GetLatestSequence()
        {
            lock (logLock)
                return nextSequence - 1;
        }

        public int CountMatches(long since, int minimumSeverity, string contains)
        {
            lock (logLock)
            {
                return logQueue.Count(entry => entry.sequence > since && GetSeverity(entry.level) >= minimumSeverity && (string.IsNullOrEmpty(contains) || XCommandUtility.Text.ContainsIgnoreCase(entry.message, contains)));
            }
        }

        /// <summary>抓取指定游标之后匹配条件的前 limit 条日志，供等待回执复用。</summary>
        public List<RuntimeLogEntry> CaptureMatches(long since, int minimumSeverity, string contains, int limit, int messageLimit, int stackLimit, bool includeStack)
        {
            lock (logLock)
            {
                return logQueue
                    .Where(entry => entry.sequence > since)
                    .Where(entry => GetSeverity(entry.level) >= minimumSeverity)
                    .Where(entry => string.IsNullOrEmpty(contains) || XCommandUtility.Text.ContainsIgnoreCase(entry.message, contains))
                    .Take(limit)
                    .Select(entry => CloneEntry(entry, messageLimit, stackLimit, includeStack))
                    .ToList();
            }
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            var entry = new RuntimeLogEntry {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                threadId = Thread.CurrentThread.ManagedThreadId,
                level = type.ToString(),
            };
            entry.message = Truncate(condition, 65536, out entry.messageTruncated);
            entry.stackTrace = Truncate(stackTrace, 131072, out entry.stackTraceTruncated);
            entry.storageBytes = EntryOverheadBytes + sizeof(char) * (entry.timestampUtc.Length + entry.level.Length + entry.message.Length + entry.stackTrace.Length);
            lock (logLock)
            {
                entry.sequence = nextSequence++;
                logQueue.Enqueue(entry);
                bufferedBytes += entry.storageBytes;
                while (bufferedBytes > CapacityBytes && logQueue.Count > 0)
                    bufferedBytes -= logQueue.Dequeue().storageBytes;
            }
        }

        public RuntimeLogEntry CloneEntry(RuntimeLogEntry source, int messageLimit, int stackLimit, bool includeStack)
        {
            string message = Truncate(source.message, messageLimit, out bool messageTruncated);
            bool stackTruncated = false;
            string stackTrace = includeStack ? Truncate(source.stackTrace, stackLimit, out stackTruncated) : string.Empty;
            return new RuntimeLogEntry {
                sequence = source.sequence,
                timestampUtc = source.timestampUtc,
                threadId = source.threadId,
                level = source.level,
                message = message,
                messageTruncated = source.messageTruncated || messageTruncated,
                stackTrace = stackTrace,
                stackTraceTruncated = includeStack && (source.stackTraceTruncated || stackTruncated),
            };
        }

        private static string Truncate(string value, int limit, out bool truncated)
        {
            value = value ?? string.Empty;
            truncated = value.Length > limit;
            return truncated ? value.Substring(0, limit) : value;
        }

        public int ParseMinimumSeverity(string level)
        {
            switch ((level ?? string.Empty).ToLowerInvariant())
            {
                case "":
                case "all":
                case "log":
                case "info": return 1;
                case "warn":
                case "warning": return 2;
                case "error":
                case "exception":
                case "assert": return 3;
                default: throw new ArgumentException($"未知日志级别：{level}");
            }
        }

        public int GetSeverity(string level)
        {
            if (string.Equals(level, LogType.Warning.ToString(), StringComparison.Ordinal))
                return 2;
            if (string.Equals(level, LogType.Error.ToString(), StringComparison.Ordinal) || string.Equals(level, LogType.Exception.ToString(), StringComparison.Ordinal) || string.Equals(level, LogType.Assert.ToString(), StringComparison.Ordinal))
                return 3;
            return 1;
        }
    }
}
