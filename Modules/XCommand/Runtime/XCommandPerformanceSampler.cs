using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace XFramework.Command
{
    /// <summary>
    /// 帧性能采样的运行时宿主（perf-*）：独立于 XCommand 操作队列逐帧采样，
    /// 记录帧间隔、GC、FrameTiming 与 Profiler 标记，结束时生成单行 JSON 报告。
    /// </summary>
    internal sealed class XCommandPerformanceSampler : MonoBehaviour
    {
        private const int MaxSamples = 30000;

        /// <summary>默认采集的引擎级 Profiler 标记：key 为报告字段名，name 为 Profiler 标记名，isTime 表示耗时（微秒）还是计数。</summary>
        private static readonly (string key, string name, bool isTime)[] DefaultMarkers = {
            ("mainThread", "Main Thread", true),
            ("playerLoop", "PlayerLoop", true),
            ("editorLoop", "EditorLoop", true),
            ("scriptUpdate", "Update.ScriptRunBehaviourUpdate", true),
            ("scriptLateUpdate", "PreLateUpdate.ScriptRunBehaviourUpdate", true),
            ("renderLoopDraw", "RenderLoop.Draw", true),
            ("finishFrameRendering", "PostLateUpdate.FinishFrameRendering", true),
            ("gfxPresentWait", "Gfx.WaitForPresentOnGfxThread", true),
            ("waitForTargetFps", "WaitForTargetFPS", true),
            ("gcAlloc", "GC.Alloc", false),
            ("physicsSimulate", "Physics.Simulate", true),
            ("animatorsUpdate", "Animators.Update", true),
            ("renderThread", "Render Thread", true),
            ("cameraRender", "Camera.Render", true),
            ("behaviourUpdate", "BehaviourUpdate", true),
            ("canvasWillRender", "Canvas.SendWillRenderCanvases", true),
            ("updatePreloading", "Loading.UpdatePreloading", true),
        };

        private sealed class MarkerBinding
        {
            public string key;
            public string markerName;
            public float toMilliseconds;
            public ProfilerRecorder recorder;
            public bool valid;
            public List<float> values;
            public float max;
            public double sum;
        }

        private static XCommandPerformanceSampler s_Instance;

        private readonly List<float> m_Ms = new List<float>(MaxSamples);
        private readonly List<int> m_Frame = new List<int>(MaxSamples);
        private readonly List<int> m_Gc0Delta = new List<int>(MaxSamples);
        private readonly List<long> m_ManagedDelta = new List<long>(MaxSamples);
        private readonly List<Vector3> m_Position = new List<Vector3>(MaxSamples);
        private readonly List<MarkerBinding> m_Markers = new List<MarkerBinding>();
        private readonly FrameTiming[] m_Timings = new FrameTiming[32];

        private bool m_Running;
        private bool m_Timeline;
        private float m_StopRealtime;
        private float m_ThresholdMs = 11.11f;
        private string m_Label = string.Empty;
        private string m_TrackTag = string.Empty;
        private int m_Gc0Total;
        private int m_Gc0Frames;
        private long m_TimingsCount;
        private double m_TimingsCpuMainThread;
        private double m_TimingsCpuRenderThread;
        private double m_TimingsGpu;
        private double m_TimingsPresentWait;
        private int m_TimingsGpuMax;
        private int m_TimingsPresentWaitMax;
        private float m_StartRealtime;
        private long m_LastManagedBytes;
        private Transform m_TrackedTransform;
        private int m_TrackedRefreshCountdown;
        private string m_SessionId = string.Empty;

        private string TrackTagText
        {
            get { return string.IsNullOrEmpty(m_TrackTag) ? "<无>" : m_TrackTag; }
        }

        internal static object Begin(float thresholdMs, string label, bool timeline, float durationSeconds, string markers, string trackTag)
        {
            if (s_Instance == null)
            {
                var go = new GameObject("[XCommand Performance Sampler]") { hideFlags = HideFlags.HideAndDontSave };
                s_Instance = go.AddComponent<XCommandPerformanceSampler>();
            }

            s_Instance.StartSession(thresholdMs, label, timeline, durationSeconds, markers, trackTag);
            return $"采样已开始：session={s_Instance.m_SessionId} 阈值={thresholdMs}ms 时长={durationSeconds}s 采样标记={s_Instance.m_Markers.Count} 追踪={s_Instance.TrackTagText} targetFrameRate={Application.targetFrameRate} vSync={QualitySettings.vSyncCount} 屏幕={Screen.width}x{Screen.height}";
        }

        internal static object Status()
        {
            if (s_Instance == null)
            {
                throw new InvalidOperationException("尚未开始采样。");
            }

            float elapsed = s_Instance.m_StartRealtime > 0f ? Time.realtimeSinceStartup - s_Instance.m_StartRealtime : 0f;
            return $"采样中={s_Instance.m_Running} 样本={s_Instance.m_Ms.Count} 时长={elapsed:0.0}s 超阈值帧数={s_Instance.CountOverThreshold()} 追踪={s_Instance.TrackTagText}";
        }

        internal static object End()
        {
            if (s_Instance == null || s_Instance.m_Ms.Count == 0)
            {
                throw new InvalidOperationException("尚未开始采样。");
            }

            return s_Instance.BuildReport();
        }

        private void StartSession(float thresholdMs, string label, bool timeline, float durationSeconds, string markers, string trackTag)
        {
            m_Running = true;
            m_StopRealtime = durationSeconds > 0f ? Time.realtimeSinceStartup + durationSeconds : 0f;
            m_Ms.Clear();
            m_Frame.Clear();
            m_Gc0Delta.Clear();
            m_ManagedDelta.Clear();
            m_Position.Clear();
            m_Gc0Total = 0;
            m_Gc0Frames = 0;
            m_TimingsCount = 0;
            m_TimingsCpuMainThread = 0d;
            m_TimingsCpuRenderThread = 0d;
            m_TimingsGpu = 0d;
            m_TimingsPresentWait = 0d;
            m_TimingsGpuMax = 0;
            m_TimingsPresentWaitMax = 0;
            m_Label = label ?? string.Empty;
            m_TrackTag = string.IsNullOrWhiteSpace(trackTag) ? string.Empty : trackTag.Trim();
            m_Timeline = timeline;
            m_ThresholdMs = thresholdMs > 0f ? thresholdMs : 11.11f;
            m_SessionId = DateTime.UtcNow.ToString("O");

            DisposeMarkers();
            CreateMarkers(markers);

            m_TrackedTransform = null;
            m_TrackedRefreshCountdown = 0;
            m_LastManagedBytes = GC.GetTotalMemory(false);
            m_StartRealtime = Time.realtimeSinceStartup;
        }

        private void DisposeMarkers()
        {
            for (int i = 0; i < m_Markers.Count; i++)
            {
                MarkerBinding binding = m_Markers[i];
                if (binding.recorder.Valid)
                {
                    binding.recorder.Dispose();
                }
            }

            m_Markers.Clear();
        }

        private void CreateMarkers(string markersText)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < DefaultMarkers.Length; i++)
            {
                (string key, string name, bool isTime) = DefaultMarkers[i];
                AddMarker(key, name, isTime, seen);
            }

            if (string.IsNullOrEmpty(markersText))
            {
                return;
            }

            string[] customMarkers = markersText.Split(',');
            for (int i = 0; i < customMarkers.Length; i++)
            {
                string markerName = customMarkers[i].Trim();
                if (markerName.Length == 0)
                {
                    continue;
                }

                AddMarker(markerName, markerName, true, seen);
            }
        }

        private void AddMarker(string key, string markerName, bool isTime, HashSet<string> seen)
        {
            if (!seen.Add(markerName))
            {
                return;
            }

            bool valid = TryCreateRecorder(markerName, out ProfilerRecorder recorder);
            m_Markers.Add(new MarkerBinding {
                key = key,
                markerName = markerName,
                toMilliseconds = isTime ? 1e-6f : 1f,
                recorder = recorder,
                valid = valid,
                values = new List<float>(MaxSamples),
            });
        }

        private static bool TryCreateRecorder(string markerName, out ProfilerRecorder recorder)
        {
            ProfilerCategory[] categories = {
                ProfilerCategory.Internal,
                ProfilerCategory.Scripts,
                ProfilerCategory.Physics,
                ProfilerCategory.Animation,
                ProfilerCategory.Memory,
                ProfilerCategory.Gui,
                ProfilerCategory.Lighting,
            };
            for (int i = 0; i < categories.Length; i++)
            {
                try
                {
                    ProfilerRecorder candidate = ProfilerRecorder.StartNew(categories[i], markerName, 1);
                    if (candidate.Valid)
                    {
                        recorder = candidate;
                        return true;
                    }

                    candidate.Dispose();
                }
                catch (Exception)
                {
                    // 该分类下不存在此标记，继续尝试下一分类。
                }
            }

            recorder = default;
            return false;
        }

        private void Update()
        {
            if (!m_Running)
            {
                return;
            }

            if (m_Ms.Count >= MaxSamples)
            {
                m_Running = false;
                return;
            }

            if (m_StopRealtime > 0f && Time.realtimeSinceStartup >= m_StopRealtime)
            {
                m_Running = false;
                return;
            }

            float ms = Time.unscaledDeltaTime * 1000f;
            int gc0Now = GC.CollectionCount(0);
            long managedNow = GC.GetTotalMemory(false);

            int gcDelta = gc0Now - m_Gc0Total;
            m_Gc0Total = gc0Now;
            if (gcDelta > 0)
            {
                m_Gc0Frames++;
            }

            long managedDelta = managedNow - m_LastManagedBytes;
            m_LastManagedBytes = managedNow;

            m_Ms.Add(ms);
            m_Frame.Add(Time.frameCount);
            m_Gc0Delta.Add(gcDelta);
            m_ManagedDelta.Add(managedDelta);
            if (m_TrackTag.Length > 0)
            {
                m_Position.Add(ResolveTrackedPosition());
            }

            for (int i = 0; i < m_Markers.Count; i++)
            {
                MarkerBinding binding = m_Markers[i];
                float value = 0f;
                if (binding.valid && binding.recorder.Valid)
                {
                    long raw = Math.Max(0L, binding.recorder.LastValue);
                    value = raw * binding.toMilliseconds;
                }

                binding.values.Add(value);
                binding.sum += value;
                if (value > binding.max)
                {
                    binding.max = value;
                }
            }

            CollectFrameTimings();
        }

        private Vector3 ResolveTrackedPosition()
        {
            if (m_TrackedTransform == null && m_TrackedRefreshCountdown-- <= 0)
            {
                m_TrackedRefreshCountdown = 60;
                try
                {
                    GameObject tracked = GameObject.FindGameObjectWithTag(m_TrackTag);
                    m_TrackedTransform = tracked != null ? tracked.transform : null;
                }
                catch (UnityException)
                {
                    // 标签未在 Tag Manager 中定义：保持无追踪对象，避免反复抛异常。
                    m_TrackedTransform = null;
                }
            }

            return m_TrackedTransform != null ? m_TrackedTransform.position : Vector3.zero;
        }

        private void CollectFrameTimings()
        {
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings((uint)m_Timings.Length, m_Timings);
            if (count == 0)
            {
                return;
            }

            for (uint i = 0; i < count; i++)
            {
                FrameTiming timing = m_Timings[i];
                m_TimingsCpuMainThread += timing.cpuMainThreadFrameTime;
                m_TimingsCpuRenderThread += timing.cpuRenderThreadFrameTime;
                m_TimingsGpu += timing.gpuFrameTime;
                m_TimingsPresentWait += timing.cpuMainThreadPresentWaitTime;
                m_TimingsGpuMax = Mathf.Max(m_TimingsGpuMax, (int)timing.gpuFrameTime);
                m_TimingsPresentWaitMax = Mathf.Max(m_TimingsPresentWaitMax, (int)timing.cpuMainThreadPresentWaitTime);
            }

            m_TimingsCount += count;
        }

        private int CountOverThreshold()
        {
            int count = 0;
            for (int i = 0; i < m_Ms.Count; i++)
            {
                if (m_Ms[i] > m_ThresholdMs)
                {
                    count++;
                }
            }

            return count;
        }

        private object BuildReport()
        {
            m_Running = false;
            var sb = new StringBuilder(1 << 16);
            int count = m_Ms.Count;
            float elapsed = Time.realtimeSinceStartup - m_StartRealtime;

            sb.Append('{');
            AppendString(sb, "sessionId", m_SessionId);
            sb.Append(',');
            AppendString(sb, "label", m_Label);
            sb.Append(',');
            AppendString(sb, "reportedAt", DateTime.UtcNow.ToString("O"));
            sb.Append(',');
            AppendString(sb, "trackTag", m_TrackTag);
            sb.Append(',').Append("\"samples\":").Append(count);
            sb.Append(',').Append("\"elapsedRealSeconds\":").Append(F(elapsed));
            sb.Append(',').Append("\"avgFps\":").Append(F(elapsed > 0.0001f ? count / elapsed : 0f));
            sb.Append(',').Append("\"thresholdMs\":").Append(F(m_ThresholdMs));
            sb.Append(',').Append("\"vSyncCount\":").Append(QualitySettings.vSyncCount);
            sb.Append(',').Append("\"targetFrameRate\":").Append(Application.targetFrameRate);
            sb.Append(',').Append("\"screen\":").Append(Quote($"{Screen.width}x{Screen.height}"));
            sb.Append(',').Append("\"isFocused\":").Append(Application.isFocused ? "true" : "false");

            AppendMsStats(sb, count);
            AppendHistogram(sb, count);
            AppendSpikes(sb, count);
            AppendGc(sb, count);
            AppendGpu(sb);
            AppendMarkers(sb, count);
            AppendWorst(sb, count);
            if (m_Timeline)
            {
                AppendTimeline(sb, count);
            }

            sb.Append('}');

            for (int i = 0; i < m_Markers.Count; i++)
            {
                MarkerBinding binding = m_Markers[i];
                if (binding.valid && binding.recorder.Valid)
                {
                    binding.recorder.Dispose();
                }

                binding.valid = false;
            }

            m_Ms.Clear();
            return sb.ToString();
        }

        private void AppendMsStats(StringBuilder sb, int count)
        {
            sb.Append(',').Append("\"frameMs\":{");
            if (count == 0)
            {
                sb.Append('}');
                return;
            }

            var sorted = new List<float>(m_Ms);
            sorted.Sort();
            double sum = 0d;
            for (int i = 0; i < count; i++)
            {
                sum += m_Ms[i];
            }

            double avg = sum / count;
            sb.Append("\"avg\":").Append(F(avg));
            sb.Append(",\"median\":").Append(F(Percentile(sorted, 0.5f)));
            sb.Append(",\"p90\":").Append(F(Percentile(sorted, 0.9f)));
            sb.Append(",\"p95\":").Append(F(Percentile(sorted, 0.95f)));
            sb.Append(",\"p99\":").Append(F(Percentile(sorted, 0.99f)));
            sb.Append(",\"min\":").Append(F(sorted[0]));
            sb.Append(",\"max\":").Append(F(sorted[count - 1]));
            sb.Append(",\"avgFpsFromMs\":").Append(F(avg > 0.0001d ? 1000d / avg : 0d));
            sb.Append(",\"p99FpsFromMs\":").Append(F(Percentile(sorted, 0.99f) > 0.0001f ? 1000f / Percentile(sorted, 0.99f) : 0f));
            sb.Append('}');
        }

        private static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp((int)Mathf.Round((sorted.Count - 1) * p), 0, sorted.Count - 1);
            return sorted[index];
        }

        private void AppendHistogram(StringBuilder sb, int count)
        {
            int[] buckets = new int[6];
            for (int i = 0; i < count; i++)
            {
                float ms = m_Ms[i];
                if (ms < 6.67f)
                {
                    buckets[0]++;
                }
                else if (ms < 8.33f)
                {
                    buckets[1]++;
                }
                else if (ms < 11.11f)
                {
                    buckets[2]++;
                }
                else if (ms < 16.67f)
                {
                    buckets[3]++;
                }
                else if (ms < 33.34f)
                {
                    buckets[4]++;
                }
                else
                {
                    buckets[5]++;
                }
            }

            sb.Append(',').Append("\"histogram\":{");
            sb.Append("\"under150fps_6_67ms\":").Append(buckets[0]);
            sb.Append(",\"6_67to8_33ms(120-150fps)\":").Append(buckets[1]);
            sb.Append(",\"8_33to11_11ms(90-120fps)\":").Append(buckets[2]);
            sb.Append(",\"11_11to16_67ms(60-90fps)\":").Append(buckets[3]);
            sb.Append(",\"16_67to33_3ms(30-60fps)\":").Append(buckets[4]);
            sb.Append(",\"over33_3ms\":").Append(buckets[5]);
            sb.Append('}');
        }

        private void AppendSpikes(StringBuilder sb, int count)
        {
            var spikeFrames = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (m_Ms[i] > m_ThresholdMs)
                {
                    spikeFrames.Add(i);
                }
            }

            sb.Append(',').Append("\"spikes\":{");
            sb.Append("\"count\":").Append(spikeFrames.Count);
            if (spikeFrames.Count > 1)
            {
                double sumGapFrames = 0d;
                int minGap = int.MaxValue;
                int maxGap = 0;
                for (int i = 1; i < spikeFrames.Count; i++)
                {
                    int gap = spikeFrames[i] - spikeFrames[i - 1];
                    sumGapFrames += gap;
                    if (gap < minGap)
                    {
                        minGap = gap;
                    }

                    if (gap > maxGap)
                    {
                        maxGap = gap;
                    }
                }

                sb.Append(",\"avgGapFrames\":").Append(F(sumGapFrames / (spikeFrames.Count - 1)));
                sb.Append(",\"minGapFrames\":").Append(minGap);
                sb.Append(",\"maxGapFrames\":").Append(maxGap);
            }

            bool tracking = m_TrackTag.Length > 0;
            sb.Append(",\"worstFrameIndices\":[");
            for (int i = 0; i < spikeFrames.Count && i < 60; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(spikeFrames[i]);
            }

            sb.Append("],\"spikeSamples\":[");
            for (int i = 0; i < spikeFrames.Count && i < 60; i++)
            {
                int index = spikeFrames[i];
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('{').Append("\"i\":").Append(index);
                sb.Append(",\"ms\":").Append(F(m_Ms[index]));
                if (tracking)
                {
                    sb.Append(",\"p\":").Append(Quote(FmtPos(m_Position[index])));
                }

                sb.Append('}');
            }

            sb.Append("]}");
        }

        private static string FmtPos(Vector3 position)
        {
            return $"{F(position.x)},{F(position.y)},{F(position.z)}";
        }

        private float ElapsedSecondsOfSession()
        {
            return m_StartRealtime > 0f ? Time.realtimeSinceStartup - m_StartRealtime : 0f;
        }

        private void AppendGc(StringBuilder sb, int count)
        {
            long alloc = 0;
            for (int i = 0; i < count; i++)
            {
                if (m_ManagedDelta[i] > 0)
                {
                    alloc += m_ManagedDelta[i];
                }
            }

            float elapsed = ElapsedSecondsOfSession();
            sb.Append(',').Append("\"gc\":{");
            sb.Append("\"gen0Collections\":").Append(m_Gc0Total);
            sb.Append(",\"framesWithCollection\":").Append(m_Gc0Frames);
            sb.Append(",\"managedAllocMb\":").Append(F(alloc / 1048576d));
            sb.Append(",\"managedAllocMbPerSecond\":").Append(F(elapsed > 0.0001f ? alloc / 1048576d / elapsed : 0d));
            sb.Append('}');
        }

        private void AppendGpu(StringBuilder sb)
        {
            sb.Append(',').Append("\"frameTiming\":{");
            if (m_TimingsCount == 0)
            {
                sb.Append("\"available\":false}");
                return;
            }

            sb.Append("\"available\":true");
            sb.Append(",\"count\":").Append(m_TimingsCount);
            sb.Append(",\"cpuMainThreadAvgMs\":").Append(F(m_TimingsCpuMainThread / m_TimingsCount));
            sb.Append(",\"cpuRenderThreadAvgMs\":").Append(F(m_TimingsCpuRenderThread / m_TimingsCount));
            sb.Append(",\"gpuAvgMs\":").Append(F(m_TimingsGpu / m_TimingsCount));
            sb.Append(",\"gpuMaxMs\":").Append(m_TimingsGpuMax);
            sb.Append(",\"presentWaitAvgMs\":").Append(F(m_TimingsPresentWait / m_TimingsCount));
            sb.Append(",\"presentWaitMaxMs\":").Append(m_TimingsPresentWaitMax);
            sb.Append('}');
        }

        private void AppendMarkers(StringBuilder sb, int count)
        {
            sb.Append(',').Append("\"markers\":{");
            bool first = true;
            for (int i = 0; i < m_Markers.Count; i++)
            {
                MarkerBinding binding = m_Markers[i];
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append(Quote(binding.key)).Append(':').Append('{');
                sb.Append("\"marker\":").Append(Quote(binding.markerName));
                sb.Append(",\"resolved\":").Append(binding.valid ? "true" : "false");
                sb.Append(",\"avg\":").Append(F(count > 0 ? binding.sum / count : 0d));
                sb.Append(",\"max\":").Append(F(binding.max));
                sb.Append('}');
            }

            sb.Append('}');
        }

        private void AppendWorst(StringBuilder sb, int count)
        {
            var indices = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                indices.Add(i);
            }

            indices.Sort((a, b) => m_Ms[b].CompareTo(m_Ms[a]));

            bool tracking = m_TrackTag.Length > 0;
            sb.Append(',').Append("\"worstFrames\":[");
            int limit = Mathf.Min(20, indices.Count);
            for (int i = 0; i < limit; i++)
            {
                int index = indices[i];
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('{');
                sb.Append("\"frame\":").Append(m_Frame[index]);
                sb.Append(",\"ms\":").Append(F(m_Ms[index]));
                sb.Append(",\"gc0\":").Append(m_Gc0Delta[index]);
                sb.Append(",\"managedDeltaMb\":").Append(F(m_ManagedDelta[index] / 1048576d));
                if (tracking)
                {
                    Vector3 position = m_Position[index];
                    sb.Append(",\"trackedPosition\":").Append(Quote($"{F(position.x)},{F(position.y)},{F(position.z)}"));
                }

                for (int j = 0; j < m_Markers.Count; j++)
                {
                    MarkerBinding binding = m_Markers[j];
                    sb.Append(',').Append(Quote(binding.key)).Append(':').Append(F(binding.values[index]));
                }

                sb.Append('}');
            }

            sb.Append(']');
        }

        private void AppendTimeline(StringBuilder sb, int count)
        {
            sb.Append(',').Append("\"timeline\":{");
            sb.Append("\"startFrame\":").Append(count > 0 ? m_Frame[0] : 0);
            sb.Append(",\"ms\":[");
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(F(m_Ms[i]));
            }

            sb.Append("]}");
        }

        private static string F(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string F(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append(Quote(key)).Append(':').Append(Quote(value ?? string.Empty));
        }

        private static string Quote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }
    }
}
