using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using XFramework.Command;
using ViewModes = UnityEditor.Profiling.HierarchyFrameDataView.ViewModes;

namespace XFramework.Editor
{
    /// <summary>
    /// Profiler 采样控制与帧报告（prof-*，仅编辑器）：开启 / 停止 Profiler、标记帧区间，
    /// 并按自身耗时或 GC 分配统计最差帧与热点函数。
    /// </summary>
    internal static class XCommandProfiler
    {
        internal const string EnableUsage = "prof-on [--deep] [--editor] [--reset]";
        internal const string DisableUsage = "prof-off";
        internal const string MarkUsage = "prof-mark [--label TEXT]";
        internal const string ReportUsage = "prof-report [--mode time|gc|both] [--worst N] [--scan N] [--lines N] [--min-ms MS] [--min-kb KB] [--filter TEXT] [--from X] [--to Y] [--stack] [--merge] [--hide-editor]";

        #region 采集开关

        private static readonly List<(string label, int frameIndex, bool enabledAtMark)> s_Marks = new List<(string, int, bool)>();

        internal static object ExecuteEnable(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "deep", "editor", "reset" });
            args.RequireValueCount(0, $"usage: {EnableUsage}");
            if (args.HasFlag("reset"))
            {
                ProfilerDriver.ClearAllFrames();
            }

            ProfilerDriver.deepProfiling = args.HasFlag("deep");
            ProfilerDriver.profileEditor = args.HasFlag("editor");
            ProfilerDriver.enabled = true;
            return $"Profiler 已开启：deep={ProfilerDriver.deepProfiling} profileEditor={ProfilerDriver.profileEditor} isPlaying={EditorApplication.isPlaying}";
        }

        internal static object ExecuteDisable(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), Array.Empty<string>());
            args.RequireValueCount(0, $"usage: {DisableUsage}");
            ProfilerDriver.enabled = false;
            return $"Profiler 已停止：帧范围 {ProfilerDriver.firstFrameIndex}~{ProfilerDriver.lastFrameIndex}（共 {ProfilerDriver.lastFrameIndex - ProfilerDriver.firstFrameIndex + 1} 帧）deep={ProfilerDriver.deepProfiling}";
        }

        internal static object ExecuteMark(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "label" }, Array.Empty<string>());
            args.RequireValueCount(0, $"usage: {MarkUsage}");
            string label = args.GetString("label", s_Marks.Count == 0 ? "start" : "end");
            s_Marks.Add((label, ProfilerDriver.lastFrameIndex, ProfilerDriver.enabled));
            return $"已标记 label={label} frame={ProfilerDriver.lastFrameIndex} enabled={ProfilerDriver.enabled} 标记数={s_Marks.Count}";
        }

        #endregion

        #region 报告

        internal static object ExecuteReport(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument,
                new[] { "worst", "scan", "lines", "min-ms", "min-kb", "filter", "from", "to", "mode" },
                new[] { "stack", "merge", "hide-editor" });
            args.RequireValueCount(0, $"usage: {ReportUsage}");
            int worstCount = args.GetInt("worst", 3);
            int scanFrames = args.GetInt("scan", 3000);
            int maxLines = args.GetInt("lines", 20);
            float minSelfMs = args.GetFloat("min-ms", 0.5f);
            double minGcBytes = args.GetFloat("min-kb", 1f) * 1024.0;
            string filter = args.GetString("filter", string.Empty);
            string mode = args.GetString("mode", "both").ToLowerInvariant();
            bool withStack = args.HasFlag("stack");
            bool merge = args.HasFlag("merge");
            bool hideEditor = args.HasFlag("hide-editor");

            int firstFrame = ProfilerDriver.firstFrameIndex;
            int lastFrame = ProfilerDriver.lastFrameIndex;
            if (lastFrame < firstFrame)
            {
                return "Profiler 中没有可用帧。请先执行 prof-on 并运行一段时间。";
            }

            var sb = new StringBuilder();
            int fromFrame = args.GetInt("from", -1);
            int toFrame = args.GetInt("to", -1);
            if (fromFrame < 0 && toFrame < 0 && s_Marks.Count >= 2)
            {
                (string firstLabel, int markedFrom, _) = s_Marks[s_Marks.Count - 2];
                (string secondLabel, int markedTo, _) = s_Marks[s_Marks.Count - 1];
                fromFrame = markedFrom;
                toFrame = markedTo;
                sb.Append($"使用标记区间 {firstLabel}(frame={markedFrom}) ~ {secondLabel}(frame={markedTo})").Append(Environment.NewLine);
            }

            if (toFrame < 0)
            {
                toFrame = lastFrame;
            }

            if (fromFrame < 0)
            {
                fromFrame = Math.Max(firstFrame, toFrame - scanFrames + 1);
            }

            int startFrame = Math.Max(firstFrame, fromFrame);
            int endFrame = Math.Min(lastFrame, toFrame);
            if (endFrame < startFrame)
            {
                return $"区间无效：{startFrame}~{endFrame}（可用 {firstFrame}~{lastFrame}）。";
            }

            // 硬上限：区间过大时会创建成千上万个帧视图，极易把 Editor 的 Profiler 原生内存撑爆
            const int MaxScanFrames = 2000;
            if (endFrame - startFrame + 1 > MaxScanFrames)
            {
                startFrame = endFrame - MaxScanFrames + 1;
                sb.Append($"区间过大，仅分析最近 {MaxScanFrames} 帧（{startFrame}~{endFrame}）。").Append(Environment.NewLine);
            }

            if (!EnsureColumns())
            {
                return "无法解析 HierarchyFrameDataView 的列索引（columnSelfTime 等），当前 Unity 版本可能不兼容。";
            }

            // ViewModes 是否为 [Flags] 不确定，这里只选集中的一种模式，避免按位或产生非法值
            ViewModes viewMode = ViewModes.Default;
            if (hideEditor)
            {
                viewMode = ViewModes.HideEditorOnlySamples;
            }
            else if (merge)
            {
                viewMode = ViewModes.MergeSamplesWithTheSameName;
            }

            sb.Append($"分析帧区间 {startFrame}~{endFrame}（可用 {firstFrame}~{lastFrame}）；deepProfiling={ProfilerDriver.deepProfiling} viewMode={viewMode}").Append(Environment.NewLine);

            var frames = new List<FrameRow>();
            int threadIndex = 0;
            for (int frameIndex = startFrame; frameIndex <= endFrame; frameIndex++)
            {
                if (!TryOpenFrame(frameIndex, viewMode, out HierarchyFrameDataView view, out int thread))
                {
                    continue;
                }

                threadIndex = thread;
                double gc = -1;
                int root = view.GetRootItemID();
                if (root >= 0)
                {
                    gc = SafeColumn(view, root, s_ColGcMemory);
                }

                frames.Add(new FrameRow {
                    FrameIndex = frameIndex,
                    ThreadIndex = thread,
                    ThreadName = view.threadName,
                    FrameMs = view.frameTimeMs,
                    GcBytes = gc,
                });

                DisposeView(view);
            }

            if (frames.Count == 0)
            {
                return "未能取到任何帧数据（Profiler 可能没有采集到主线程数据）。";
            }

            float totalMs = 0f;
            double totalGc = 0.0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalMs += frames[i].FrameMs;
                if (frames[i].GcBytes > 0)
                {
                    totalGc += frames[i].GcBytes;
                }
            }

            sb.Append($"有效帧 {frames.Count}，主线程平均 {F(totalMs / frames.Count)}ms，GC 合计 {Kb(totalGc)}").Append(Environment.NewLine);

            bool wantTime = mode == "time" || mode == "both";
            bool wantGc = mode == "gc" || mode == "both";

            if (wantTime)
            {
                var byTime = new List<FrameRow>(frames);
                byTime.Sort((a, b) => b.FrameMs.CompareTo(a.FrameMs));
                AppendSection(sb, "最慢帧（按主线程帧耗时）", byTime, worstCount, minSelfMs, minGcBytes, maxLines, filter, withStack, viewMode, false, threadIndex);
            }

            if (wantGc)
            {
                var byGc = new List<FrameRow>(frames);
                byGc.Sort((a, b) => b.GcBytes.CompareTo(a.GcBytes));
                AppendSection(sb, "分配最多帧（按帧内 GC 总量）", byGc, worstCount, minSelfMs, minGcBytes, maxLines, filter, withStack, viewMode, true, threadIndex);
            }

            return sb.ToString();
        }

        private static void AppendSection(StringBuilder sb, string title, List<FrameRow> sorted, int worstCount,
            float minSelfMs, double minGcBytes, int maxLines, string filter, bool withStack, ViewModes viewMode, bool sortByGc, int defaultThread)
        {
            sb.Append(Environment.NewLine).Append($"### {title}").Append(Environment.NewLine);
            int emitted = 0;
            for (int i = 0; i < sorted.Count && i < worstCount; i++)
            {
                FrameRow row = sorted[i];
                if (sortByGc && row.GcBytes <= 0)
                {
                    break;
                }

                emitted++;
                sb.Append($"frame={row.FrameIndex} thread={row.ThreadName}[{row.ThreadIndex}] frameTime={F(row.FrameMs)}ms gc={Kb(row.GcBytes)}").Append(Environment.NewLine);

                HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(row.FrameIndex, row.ThreadIndex, viewMode, s_ColSelfTime, false);
                if (view.valid)
                {
                    DumpFrameItems(sb, view, minSelfMs, minGcBytes, maxLines, filter, withStack, sortByGc);
                }
                else
                {
                    sb.Append("  <该帧数据已失效>").Append(Environment.NewLine);
                }

                DisposeView(view);
            }

            if (emitted == 0)
            {
                sb.Append("  <无匹配帧>").Append(Environment.NewLine);
            }
        }

        private static void DumpFrameItems(StringBuilder sb, HierarchyFrameDataView view, float minSelfMs, double minGcBytes,
            int maxLines, string filter, bool withStack, bool sortByGc)
        {
            int root = view.GetRootItemID();
            if (root < 0)
            {
                sb.Append("  <无根节点>").Append(Environment.NewLine);
                return;
            }

            sb.Append($"  样本数={view.sampleCount} 最大深度={view.maxDepth} fps={F(view.frameFps)}").Append(Environment.NewLine);

            var rows = new List<ItemRow>();
            var pending = new Stack<int>();
            var children = new List<int>();
            pending.Push(root);
            int visited = 0;
            while (pending.Count > 0 && visited < 300000)
            {
                int id = pending.Pop();
                visited++;

                double self = SafeColumn(view, id, s_ColSelfTime);
                double gc = SafeColumn(view, id, s_ColGcMemory);
                if (self >= minSelfMs || gc >= minGcBytes)
                {
                    string path = view.GetItemPath(id);
                    if (string.IsNullOrEmpty(filter) || path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rows.Add(new ItemRow {
                            Id = id,
                            Path = path,
                            Name = view.GetItemName(id),
                            ObjectName = SafeColumnString(view, id, s_ColObjectName),
                            Depth = view.GetItemDepth(id),
                            SelfMs = (float)self,
                            TotalMs = (float)SafeColumn(view, id, s_ColTotalTime),
                            GcBytes = gc,
                            Calls = (long)SafeColumn(view, id, s_ColCalls),
                        });
                    }
                }

                children.Clear();
                view.GetItemChildren(id, children);
                for (int i = 0; i < children.Count; i++)
                {
                    pending.Push(children[i]);
                }
            }

            sb.Append($"  已遍历节点 {visited}，命中 {rows.Count}").Append(Environment.NewLine);

            if (sortByGc)
            {
                rows.Sort((a, b) => b.GcBytes.CompareTo(a.GcBytes));
            }
            else
            {
                rows.Sort((a, b) => b.SelfMs.CompareTo(a.SelfMs));
            }

            int emitted = 0;
            for (int i = 0; i < rows.Count && emitted < maxLines; i++)
            {
                ItemRow item = rows[i];
                emitted++;
                sb.Append($"  self={F(item.SelfMs)}ms total={F(item.TotalMs)}ms gc={Kb(item.GcBytes)} calls={item.Calls} d={item.Depth}");
                if (!string.IsNullOrEmpty(item.ObjectName))
                {
                    sb.Append($" obj={item.ObjectName}");
                }

                sb.Append("  ").Append(item.Path);
                if (withStack)
                {
                    sb.Append(Environment.NewLine).Append("      @ ").Append(Compact(view.ResolveItemCallstack(item.Id)));
                }

                sb.Append(Environment.NewLine);
            }

            if (emitted == 0)
            {
                sb.Append($"  <没有自身耗时 >= {F(minSelfMs)}ms 或分配 >= {Kb(minGcBytes)} 的样本>").Append(Environment.NewLine);
            }
        }

        #endregion

        #region 数据结构与辅助

        private sealed class FrameRow
        {
            public int FrameIndex;
            public int ThreadIndex;
            public string ThreadName;
            public float FrameMs;
            public double GcBytes;
        }

        private sealed class ItemRow
        {
            public int Id;
            public string Path;
            public string Name;
            public string ObjectName;
            public int Depth;
            public float SelfMs;
            public float TotalMs;
            public double GcBytes;
            public long Calls;
        }

        private static int s_ColSelfTime = -1;
        private static int s_ColTotalTime = -1;
        private static int s_ColGcMemory = -1;
        private static int s_ColCalls = -1;
        private static int s_ColObjectName = -1;

        // 已确认的主线程线程索引，命中后不再每帧重复试探 6 个候选线程
        private static int s_MainThreadIndex = -1;

        private static readonly MethodInfo s_ViewDisposeMethod = FindViewDispose();

        /// <summary>
        /// HierarchyFrameDataView 持有原生帧数据，用完必须释放。
        /// 否则每扫一帧都会残留一份原生内存，反复调用会把 Editor 的 ALLOC_PROFILER_EDITOR 撑到 OOM。
        /// </summary>
        private static void DisposeView(HierarchyFrameDataView view)
        {
            if (s_ViewDisposeMethod == null)
            {
                return;
            }

            try
            {
                s_ViewDisposeMethod.Invoke(view, null);
            }
            catch
            {
                // 释放失败不影响报告输出，忽略。
            }
        }

        private static MethodInfo FindViewDispose()
        {
            return typeof(HierarchyFrameDataView).GetMethod("Dispose",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        }

        private static bool EnsureColumns()
        {
            if (s_ColSelfTime >= 0 && s_ColTotalTime >= 0 && s_ColGcMemory >= 0 && s_ColCalls >= 0)
            {
                return true;
            }

            s_ColSelfTime = ReadColumnField("columnSelfTime");
            s_ColTotalTime = ReadColumnField("columnTotalTime");
            s_ColGcMemory = ReadColumnField("columnGcMemory");
            s_ColCalls = ReadColumnField("columnCalls");
            s_ColObjectName = ReadColumnField("columnObjectName");
            return s_ColSelfTime >= 0 && s_ColTotalTime >= 0 && s_ColGcMemory >= 0 && s_ColCalls >= 0;
        }

        private static int ReadColumnField(string name)
        {
            FieldInfo field = typeof(HierarchyFrameDataView).GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (field == null)
            {
                return -1;
            }

            object value = field.IsStatic ? field.GetValue(null) : null;
            if (value is int intValue)
            {
                return intValue;
            }

            return -1;
        }

        private static bool TryOpenFrame(int frameIndex, ViewModes viewMode, out HierarchyFrameDataView view, out int threadIndex)
        {
            // 已锁定主线程索引时只开一个 view，避免每帧试探 6 个候选线程造成大量原生内存占用
            if (s_MainThreadIndex >= 0)
            {
                HierarchyFrameDataView cached = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, s_MainThreadIndex, viewMode, s_ColSelfTime, false);
                if (cached.valid && cached.sampleCount > 0 && IsMainThread(cached.threadName))
                {
                    view = cached;
                    threadIndex = s_MainThreadIndex;
                    return true;
                }

                DisposeView(cached);
                s_MainThreadIndex = -1;
            }

            HierarchyFrameDataView best = default;
            int bestThread = -1;
            float bestMs = float.MinValue;
            for (int candidate = 0; candidate < 6; candidate++)
            {
                HierarchyFrameDataView current = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, candidate, viewMode, s_ColSelfTime, false);
                if (!current.valid || current.sampleCount <= 0)
                {
                    DisposeView(current);
                    continue;
                }

                if (IsMainThread(current.threadName))
                {
                    if (bestThread >= 0)
                    {
                        DisposeView(best);
                    }

                    s_MainThreadIndex = candidate;
                    view = current;
                    threadIndex = candidate;
                    return true;
                }

                if (current.frameTimeMs > bestMs)
                {
                    if (bestThread >= 0)
                    {
                        DisposeView(best);
                    }

                    best = current;
                    bestThread = candidate;
                    bestMs = current.frameTimeMs;
                }
                else
                {
                    DisposeView(current);
                }
            }

            view = best;
            threadIndex = bestThread;
            return bestThread >= 0;
        }

        private static bool IsMainThread(string threadName)
        {
            return !string.IsNullOrEmpty(threadName) && threadName.Contains("Main Thread");
        }

        private static double SafeColumn(HierarchyFrameDataView view, int id, int column)
        {
            if (column < 0)
            {
                return -1.0;
            }

            try
            {
                return view.GetItemColumnDataAsDouble(id, column);
            }
            catch
            {
                return -1.0;
            }
        }

        private static string SafeColumnString(HierarchyFrameDataView view, int id, int column)
        {
            if (column < 0)
            {
                return string.Empty;
            }

            try
            {
                return view.GetItemColumnData(id, column);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string Compact(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "<空调用栈>";
            }

            return text.Replace("\r", " ").Replace("\n", " ; ");
        }

        private static string Kb(double bytes)
        {
            if (bytes <= 0)
            {
                return "0";
            }

            return (bytes / 1024.0).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "KB";
        }

        private static string F(float value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
