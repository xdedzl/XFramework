using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace XFramework.AutoTest
{
    internal static class AutoTestLogs
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
        private sealed class RuntimeLogEntry
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
        private static readonly object s_LogLock = new object();
        private static readonly Queue<RuntimeLogEntry> s_Logs = new Queue<RuntimeLogEntry>();
        private static long s_NextSequence = 1;
        private static long s_BufferBytes;
        private static bool s_Initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCapture()
        {
            Application.logMessageReceivedThreaded -= OnLogReceived;
            lock (s_LogLock)
            {
                s_Logs.Clear();
                s_NextSequence = 1;
                s_BufferBytes = 0;
            }
            s_Initialized = false;
            InitializeCapture();
        }

        internal static void InitializeCapture()
        {
            EnsureCapture();
        }

        internal static string Query(AutoTestLogQuery query)
        {
            EnsureCapture();
            int minimumSeverity = ParseMinimumSeverity(query.level);
            int limit = Mathf.Clamp(query.limit, 1, 1000);
            int messageLimit = Mathf.Clamp(query.messageLimit, 0, 65536);
            int stackLimit = Mathf.Clamp(query.stackLimit, 0, 65536);
            List<RuntimeLogEntry> captured;
            long latestSequence;
            long oldestSequence;
            long bufferBytes;
            lock (s_LogLock)
            {
                captured = s_Logs.Where(entry => entry.sequence > query.since)
                    .Where(entry => GetSeverity(entry.level) >= minimumSeverity)
                    .Where(entry => string.IsNullOrEmpty(query.contains) || ContainsIgnoreCase(entry.message, query.contains))
                    .Take(limit + 1)
                    .ToList();
                latestSequence = s_NextSequence - 1;
                oldestSequence = s_Logs.Count > 0 ? s_Logs.Peek().sequence : s_NextSequence;
                bufferBytes = s_BufferBytes;
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

        internal static IEnumerator Wait(AutoTestLogWaitRequest request, AutoTestOperationContext context)
        {
            EnsureCapture();
            if (request.count < 1 || request.count > 1000)
                throw new ArgumentOutOfRangeException(nameof(request.count), "count 必须在 1 到 1000 之间。");
            if (request.timeoutSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(request.timeoutSeconds), "timeout 必须大于 0。");
            int minimumSeverity = ParseMinimumSeverity(request.level);
            long since = request.since >= 0 ? request.since : GetLatestSequence();
            float started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started <= request.timeoutSeconds)
            {
                if (CountMatches(since, minimumSeverity, request.contains) >= request.count)
                {
                    context.SetOutput(Query(new AutoTestLogQuery {
                        since = since,
                        level = request.level,
                        contains = request.contains,
                        limit = request.count,
                        messageLimit = request.messageLimit,
                        stackLimit = request.stackLimit,
                        includeStack = request.includeStack,
                        compact = request.compact,
                    }));
                    yield break;
                }
                yield return null;
            }
            throw new TimeoutException($"wait-for log 等待 {request.count} 条匹配日志超时（{request.timeoutSeconds.ToString("0.###")} 秒，起始游标 {since}）。");
        }

        private static long GetLatestSequence()
        {
            lock (s_LogLock)
                return s_NextSequence - 1;
        }

        private static int CountMatches(long since, int minimumSeverity, string contains)
        {
            lock (s_LogLock)
            {
                return s_Logs.Count(entry => entry.sequence > since && GetSeverity(entry.level) >= minimumSeverity && (string.IsNullOrEmpty(contains) || ContainsIgnoreCase(entry.message, contains)));
            }
        }

        private static void EnsureCapture()
        {
            if (s_Initialized)
                return;
            Application.logMessageReceivedThreaded -= OnLogReceived;
            Application.logMessageReceivedThreaded += OnLogReceived;
            s_Initialized = true;
        }

        private static void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            var entry = new RuntimeLogEntry {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                threadId = Thread.CurrentThread.ManagedThreadId,
                level = type.ToString(),
            };
            entry.message = Truncate(condition, 65536, out entry.messageTruncated);
            entry.stackTrace = Truncate(stackTrace, 131072, out entry.stackTraceTruncated);
            entry.storageBytes = EntryOverheadBytes + sizeof(char) * (entry.timestampUtc.Length + entry.level.Length + entry.message.Length + entry.stackTrace.Length);
            lock (s_LogLock)
            {
                entry.sequence = s_NextSequence++;
                s_Logs.Enqueue(entry);
                s_BufferBytes += entry.storageBytes;
                while (s_BufferBytes > CapacityBytes && s_Logs.Count > 0)
                    s_BufferBytes -= s_Logs.Dequeue().storageBytes;
            }
        }

        private static RuntimeLogEntry CloneEntry(RuntimeLogEntry source, int messageLimit, int stackLimit, bool includeStack)
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

        private static int ParseMinimumSeverity(string level)
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

        private static int GetSeverity(string level)
        {
            if (string.Equals(level, LogType.Warning.ToString(), StringComparison.Ordinal))
                return 2;
            if (string.Equals(level, LogType.Error.ToString(), StringComparison.Ordinal) || string.Equals(level, LogType.Exception.ToString(), StringComparison.Ordinal) || string.Equals(level, LogType.Assert.ToString(), StringComparison.Ordinal))
                return 3;
            return 1;
        }

        private static bool ContainsIgnoreCase(string value, string expected)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
