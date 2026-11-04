using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Command
{
    [Serializable]
    internal sealed class XCommandLogWaitRequest : IXCommandWaitRequest
    {
        public long since = -1;
        public string level = "all";
        public string contains;
        public int count = 1;
        public int messageLimit = 2048;
        public int stackLimit = 4096;
        public bool includeStack;
        public float timeoutSeconds = 30f;
        public bool compact;

        private int minimumSeverity;
        private long startCursor;
        private int matchedCount;

        float IXCommandWaitRequest.TimeoutSeconds => timeoutSeconds;
        bool IXCommandWaitRequest.Compact => compact;

        string IXCommandWaitRequest.TimeoutMessage => $"wait-for-log 等待 {count} 条匹配日志超时（{timeoutSeconds.ToString("0.###")} 秒，起始游标 {startCursor}）。";

        void IXCommandWaitRequest.Validate()
        {
            XCommandUtility.Logs.EnsureCapture();
            if (count < 1 || count > 1000)
                throw new ArgumentOutOfRangeException(nameof(count), "count 必须在 1 到 1000 之间。");
            minimumSeverity = XCommandUtility.Logs.ParseMinimumSeverity(level);
            startCursor = since >= 0 ? since : XCommandUtility.Logs.GetLatestSequence();
            matchedCount = 0;
        }

        bool IXCommandWaitRequest.Evaluate()
        {
            matchedCount = XCommandUtility.Logs.CountMatches(startCursor, minimumSeverity, contains);
            return matchedCount >= count;
        }

        string IXCommandWaitRequest.CreateOutput(bool success, float elapsedSeconds)
        {
            var receipt = new LogWaitReceipt {
                success = success,
                error = success ? string.Empty : ((IXCommandWaitRequest)this).TimeoutMessage,
                level = level,
                contains = contains,
                requiredCount = count,
                matchedCount = matchedCount,
                elapsedSeconds = elapsedSeconds,
                cursor = startCursor,
                latestSequence = XCommandUtility.Logs.GetLatestSequence(),
                entries = XCommandUtility.Logs.CaptureMatches(startCursor, minimumSeverity, contains, count, messageLimit, stackLimit, includeStack),
            };
            return JsonUtility.ToJson(receipt, !compact);
        }

        [Serializable]
        private sealed class LogWaitReceipt
        {
            public bool success;
            public string error;
            public string level;
            public string contains;
            public int requiredCount;
            public int matchedCount;
            public float elapsedSeconds;
            public long cursor;
            public long latestSequence;
            public List<XCommandUtility.Logs.RuntimeLogEntry> entries = new List<XCommandUtility.Logs.RuntimeLogEntry>();
        }
    }
}
