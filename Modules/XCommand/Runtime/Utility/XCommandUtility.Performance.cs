using System;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        /// <summary>
        /// 帧性能采样（perf-*）：逐帧记录帧间隔、GC、FrameTiming 与 Profiler 标记，
        /// 结束时输出 JSON 报告，用于定位掉帧与分配尖峰。
        /// </summary>
        internal static class Performance
        {
            internal const string StartUsage = "perf-start [--threshold-ms N] [--label TEXT] [--timeline] [--duration-seconds N] [--markers NAME1,NAME2] [--track-tag TAG]";
            internal const string StatusUsage = "perf-status";
            internal const string StopUsage = "perf-stop";

            internal static object ExecuteStart(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(argument,
                    new[] { "threshold-ms", "label", "duration-seconds", "markers", "track-tag" },
                    new[] { "timeline" });
                args.RequireValueCount(0, $"usage: {StartUsage}");

                float thresholdMs = args.GetFloat("threshold-ms", 11.11f);
                if (thresholdMs <= 0f)
                {
                    throw new ArgumentException($"--threshold-ms 需要正数，收到：{thresholdMs}");
                }

                float durationSeconds = args.GetFloat("duration-seconds", 0f);
                if (durationSeconds < 0f)
                {
                    throw new ArgumentException($"--duration-seconds 不能为负数，收到：{durationSeconds}");
                }

                return XCommandPerformanceSampler.Begin(
                    thresholdMs,
                    args.GetString("label", string.Empty),
                    args.HasFlag("timeline"),
                    durationSeconds,
                    args.GetString("markers"),
                    args.GetString("track-tag"));
            }

            internal static object ExecuteStatus(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), Array.Empty<string>());
                args.RequireValueCount(0, $"usage: {StatusUsage}");
                return XCommandPerformanceSampler.Status();
            }

            internal static object ExecuteStop(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), Array.Empty<string>());
                args.RequireValueCount(0, $"usage: {StopUsage}");
                return XCommandPerformanceSampler.End();
            }
        }
    }
}
