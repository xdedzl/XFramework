#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor.Media;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XFramework.Command
{
    [Serializable]
    public sealed class XCommandOffscreenRecordRequest
    {
        public string path;
        public int fps;
        public int durationSeconds;
        public int frames;
        public int width;
        public int height;
        public bool frameStep;
        public bool noFallback;
    }

    [Serializable]
    public sealed class XCommandOffscreenRecordStatus
    {
        public bool started;
        public bool running;
        public bool stopRequested;
        public bool success;
        public string path;
        public int width;
        public int height;
        public int fps;
        public int frames;
        public int captured;
        public string encoder;
        public string mode;
        public float elapsedSeconds;
        public string error;
    }

    public static partial class XCommandUtility
    {
        /// <summary>
        /// 离屏逐帧录屏（编辑器专用）：按目标帧率抓取 GameView 渲染，直接编码为 MP4。
        /// 不写 PNG、不落盘中间帧，也不依赖物理桌面是否可见，因此桌面锁定时依然可以录制。
        /// 优先使用 Unity 内置 MediaEncoder（平台硬件 H.264）；不可用时回退到 ffmpeg rawvideo 管道。
        /// 需要在 Play 模式下运行，且 GameView 正在渲染。
        /// 采用「启动即返回 + 独立协程宿主」的实现，不占用 XCommand 操作队列，
        /// 因此可以在录制期间用 ui-input 注入输入（否则串行的操作队列会把输入堵到录制结束之后）。
        /// </summary>
        internal static class OffscreenRecord
        {
            internal const string Usage = "record-offscreen PATH [--fps N] [--duration S] [--frames N] [--width W --height H] [--frame-step] [--no-fallback]";
            internal const string StopUsage = "record-offscreen-stop";
            internal const string StatusUsage = "record-offscreen-status";

            private const int DefaultFps = 30;
            private const int DefaultDurationSeconds = 10;
            private const int MaxFps = 240;
            private const int MaxDurationSeconds = 600;
            private const int MinDimension = 16;
            private const int MaxDimension = 3840;

            private static XCommandOffscreenRecordRunner s_Runner;
            private static XCommandOffscreenRecordStatus s_Status;

            internal static object Execute(string argument)
            {
                XCommandOffscreenRecordRequest request = ParseArguments(argument);
                Prepare(request);

                if (s_Status != null && s_Status.running)
                {
                    throw new InvalidOperationException("已有离屏录制在进行中，先执行 record-offscreen-stop。");
                }

                StartJob(request);
                return JsonUtility.ToJson(s_Status);
            }

            internal static object ExecuteStop(string argument)
            {
                if (s_Status == null || !s_Status.running)
                {
                    throw new InvalidOperationException("当前没有进行中的离屏录制。");
                }

                s_Status.stopRequested = true;
                return JsonUtility.ToJson(s_Status);
            }

            internal static object ExecuteStatus(string argument)
            {
                if (s_Status == null)
                {
                    return JsonUtility.ToJson(new XCommandOffscreenRecordStatus { running = false, error = "尚未执行过 record-offscreen。" });
                }

                return JsonUtility.ToJson(s_Status);
            }

            private static XCommandOffscreenRecordRequest ParseArguments(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(
                    argument,
                    new[] { "fps", "duration", "frames", "width", "height" },
                    new[] { "frame-step", "no-fallback" });
                args.RequireValueCount(1, $"usage: {Usage}");

                return new XCommandOffscreenRecordRequest
                {
                    path = args.Values[0],
                    fps = args.GetInt("fps", 0),
                    durationSeconds = args.GetInt("duration", 0),
                    frames = args.GetInt("frames", 0),
                    width = args.GetInt("width", 0),
                    height = args.GetInt("height", 0),
                    frameStep = args.HasFlag("frame-step"),
                    noFallback = args.HasFlag("no-fallback"),
                };
            }

            /// <summary>同步校验并补全请求参数；校验失败抛异常，让命令立即返回错误。</summary>
            private static void Prepare(XCommandOffscreenRecordRequest request)
            {
                if (!Application.isPlaying)
                {
                    throw new InvalidOperationException("record-offscreen 需要在 Play 模式下运行（先执行 play-start）。");
                }

                request.path = XCommandUtility.Path.ResolvePath(request.path);
                if (!string.Equals(System.IO.Path.GetExtension(request.path), ".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("record-offscreen 输出路径必须以 .mp4 结尾。");
                }

                if (request.fps <= 0)
                {
                    request.fps = DefaultFps;
                }

                if (request.fps > MaxFps)
                {
                    throw new ArgumentException($"--fps 过大（上限 {MaxFps}），收到：{request.fps}");
                }

                if (request.frames <= 0)
                {
                    int duration = request.durationSeconds > 0 ? request.durationSeconds : DefaultDurationSeconds;
                    if (duration > MaxDurationSeconds)
                    {
                        throw new ArgumentException($"--duration 过大（上限 {MaxDurationSeconds} 秒），收到：{duration}");
                    }

                    request.frames = duration * request.fps;
                }

                if (request.frames <= 0)
                {
                    throw new ArgumentException("录制帧数必须大于 0。");
                }

                ResolveDimensions(request);
            }

            private static void StartJob(XCommandOffscreenRecordRequest request)
            {
                var status = new XCommandOffscreenRecordStatus
                {
                    started = true,
                    running = true,
                    path = request.path,
                    width = request.width,
                    height = request.height,
                    fps = request.fps,
                    frames = request.frames,
                    encoder = "pending",
                    mode = request.frameStep ? "frame-step" : "realtime",
                };

                var host = new UnityEngine.GameObject("[XCommandOffscreenRecord]") { hideFlags = UnityEngine.HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(host);
                s_Runner = host.AddComponent<XCommandOffscreenRecordRunner>();
                s_Runner.Begin(request, status, () =>
                {
                    if (s_Runner != null)
                    {
                        UnityEngine.Object.Destroy(s_Runner.gameObject);
                        s_Runner = null;
                    }
                });
                s_Status = status;
            }

            internal static void ResolveDimensions(XCommandOffscreenRecordRequest request)
            {
                if ((request.width > 0) != (request.height > 0))
                {
                    throw new ArgumentException("--width 与 --height 必须同时指定。");
                }

                int width = request.width > 0 ? request.width : Screen.width;
                int height = request.height > 0 ? request.height : Screen.height;
                int max = Mathf.Min(MaxDimension, SystemInfo.maxTextureSize);
                width = Mathf.Clamp(width, MinDimension, max);
                height = Mathf.Clamp(height, MinDimension, max);

                // H.264 要求宽高为偶数。
                request.width = width - width % 2;
                request.height = height - height % 2;
            }

            /// <summary>创建编码后端：优先 MediaEncoder，失败时按需回退 ffmpeg rawvideo。</summary>
            internal static void CreateBackend(string path, int fps, int width, int height, bool noFallback, ref MediaEncoder encoder, ref Process ffmpegProcess, ref Stream ffmpegInput, ref string encoderName)
            {
                if (TryCreateMediaEncoder(path, fps, width, height, out encoder, out string mediaEncoderError))
                {
                    return;
                }

                if (noFallback)
                {
                    throw new InvalidOperationException($"MediaEncoder 不可用：{mediaEncoderError}");
                }

                if (!TryStartFfmpegPipe(path, fps, width, height, out ffmpegProcess, out ffmpegInput, out string ffmpegError))
                {
                    throw new InvalidOperationException($"MediaEncoder 不可用（{mediaEncoderError}），ffmpeg 回退也失败：{ffmpegError}");
                }

                encoderName = "ffmpeg-rawvideo";
                Debug.LogWarning($"[OffscreenRecord] MediaEncoder 不可用（{mediaEncoderError}），回退到 ffmpeg rawvideo。");
            }

            internal static void WriteFrame(Texture2D frameTexture, bool useFfmpeg, MediaEncoder encoder, Stream ffmpegInput)
            {
                if (useFfmpeg)
                {
                    byte[] raw = frameTexture.GetRawTextureData();
                    ffmpegInput.Write(raw, 0, raw.Length);
                    return;
                }

                // 不传时间戳，由编码器按 frameRate 逐帧推进，得到恒定帧率的 MP4。
                encoder.AddFrame(frameTexture);
            }

            internal static void DisposeMediaEncoder(ref MediaEncoder encoder)
            {
                if (encoder == null)
                {
                    return;
                }

                try
                {
                    encoder.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[OffscreenRecord] 关闭 MediaEncoder 失败：{exception.Message}");
                }

                encoder = null;
            }

            private static bool TryCreateMediaEncoder(string path, int fps, int width, int height, out MediaEncoder encoder, out string error)
            {
                encoder = null;
                error = null;
                try
                {
                    var attributes = new VideoTrackAttributes
                    {
                        frameRate = new MediaRational(fps),
                        width = (uint)width,
                        height = (uint)height,
                        includeAlpha = false,
                    };
                    encoder = new MediaEncoder(path, attributes);
                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    encoder = null;
                    return false;
                }
            }

            internal static bool TryStartFfmpegPipe(string path, int fps, int width, int height, out Process process, out Stream input, out string error)
            {
                process = null;
                input = null;
                error = null;

                if (!XCommandUtility.ScreenRecord.TryResolveFfmpeg(out string ffmpegPath, out string ffmpegError))
                {
                    error = ffmpegError;
                    return false;
                }

                string directory = System.IO.Path.GetDirectoryName(path);
                string arguments =
                    $"-hide_banner -loglevel warning -y -f rawvideo -pixel_format rgba -video_size {width}x{height} -framerate {fps} -i pipe:0 " +
                    "-vf vflip,crop=trunc(iw/2)*2:trunc(ih/2)*2 -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p " +
                    "-movflags +frag_keyframe+empty_moov \"" + path.Replace("\"", "\\\"") + "\"";

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        WorkingDirectory = string.IsNullOrEmpty(directory) ? Application.dataPath : directory,
                    };
                    var started = new Process { StartInfo = startInfo };
                    started.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (!string.IsNullOrEmpty(eventArgs.Data))
                        {
                            Debug.LogWarning($"[OffscreenRecord][ffmpeg] {eventArgs.Data}");
                        }
                    };
                    started.Start();
                    started.BeginErrorReadLine();
                    process = started;
                    input = started.StandardInput.BaseStream;
                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    process = null;
                    input = null;
                    return false;
                }
            }
        }
    }
}
#endif