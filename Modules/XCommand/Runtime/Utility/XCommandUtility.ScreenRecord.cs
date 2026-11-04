#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        /// <summary>
        /// 屏幕录制（编辑器专用）：调用外部 ffmpeg 的 gdigrab 录制屏幕，无需进入 Play 模式。
        /// </summary>
        internal static class ScreenRecord
        {
            internal const string Usage = "record PATH [--duration S] [--fps N] [--crf N] [--region X,Y,W,H] [--full]";
            internal const string StartUsage = "record-start PATH [--fps N] [--crf N] [--region X,Y,W,H] [--full]";
            internal const string StopUsage = "record-stop";

            private const int DefaultDuration = 10;
            private const int DefaultFps = 30;
            private const int DefaultCrf = 23;

            private static readonly string[] RecordValueOptions = { "duration", "fps", "crf", "region" };
            private static readonly string[] FlagOptions = { "full" };

            /// <summary>当前进行中的录制会话；同一时间只允许一个。</summary>
            private static RecordingSession s_Session;

            internal static object Execute(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(argument, RecordValueOptions, FlagOptions);
                args.RequireValueCount(1, $"usage: {Usage}");
                int duration = args.GetInt("duration", DefaultDuration);
                if (duration <= 0)
                {
                    throw new ArgumentException($"--duration 需要正整数秒，收到：{duration}");
                }

                return StartSession(args, duration);
            }

            internal static object ExecuteStart(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(argument, RecordValueOptions, FlagOptions);
                if (args.GetString("duration") != null)
                {
                    throw new ArgumentException($"record-start 不支持 --duration，录制固定时长请用 record。usage: {StartUsage}");
                }

                args.RequireValueCount(1, $"usage: {StartUsage}");
                return StartSession(args, 0);
            }

            internal static object ExecuteStop(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), Array.Empty<string>());
                args.RequireValueCount(0, $"usage: {StopUsage}");
                if (s_Session == null)
                {
                    return "当前没有进行中的录制。";
                }

                return StopSession(s_Session);
            }

            private static object StartSession(XCommandArguments args, int autoStopSeconds)
            {
                if (s_Session != null && !s_Session.Process.HasExited)
                {
                    return $"已有录制在进行中（{s_Session.OutputPath}），请先执行 record-stop。";
                }

                if (!TryResolveFfmpeg(out string ffmpegPath, out string ffmpegError))
                {
                    return ffmpegError;
                }

                int fps = args.GetInt("fps", 0);
                if (fps <= 0)
                {
                    fps = DefaultFps;
                }

                int crf = args.GetInt("crf", -1);
                if (crf < 0)
                {
                    crf = DefaultCrf;
                }

                if (crf > 51)
                {
                    throw new ArgumentException($"--crf 取值范围 0-51，收到：{crf}");
                }

                if (!TryResolveRect(args, out CaptureRect rect, out string rectNote, out string rectError))
                {
                    return rectError;
                }

                string outputPath = XCommandUtility.Path.ResolvePath(args.Values[0]);
                if (string.IsNullOrEmpty(System.IO.Path.GetExtension(outputPath)))
                {
                    outputPath = System.IO.Path.ChangeExtension(outputPath, ".mp4");
                }

                string directory = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var session = new RecordingSession
                {
                    OutputPath = outputPath,
                    Rect = rect,
                    Fps = fps,
                    AutoStopAt = autoStopSeconds > 0 ? EditorApplication.timeSinceStartup + autoStopSeconds : 0d,
                };

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = BuildArguments(fps, crf, rect, outputPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WorkingDirectory = string.IsNullOrEmpty(directory) ? Application.dataPath : directory,
                };

                try
                {
                    var process = new Process { StartInfo = startInfo };
                    process.ErrorDataReceived += (_, eventArgs) =>
                    {
                        if (!string.IsNullOrEmpty(eventArgs.Data))
                        {
                            session.AppendLog(eventArgs.Data);
                        }
                    };
                    process.Start();
                    process.BeginErrorReadLine();
                    session.Process = process;
                }
                catch (Exception exception)
                {
                    return $"启动 ffmpeg 失败：{exception.Message}";
                }

                s_Session = session;
                if (session.AutoStopAt > 0d)
                {
                    EditorApplication.update += OnEditorUpdate;
                }

                string stopHint = autoStopSeconds > 0
                    ? $"将在 {autoStopSeconds} 秒后自动停止"
                    : "请执行 record-stop 结束录制";

                return $"已开始录制：{outputPath}\n" +
                       $"区域：{rectNote} {rect.Width}x{rect.Height} @({rect.X},{rect.Y})　帧率：{fps}　{stopHint}\n" +
                       "（系统级录屏：录制期间请保持 Unity 窗口可见，不要最小化）";
            }

            private static void OnEditorUpdate()
            {
                RecordingSession session = s_Session;
                if (session == null)
                {
                    EditorApplication.update -= OnEditorUpdate;
                    return;
                }

                if (session.AutoStopAt > 0d && EditorApplication.timeSinceStartup >= session.AutoStopAt)
                {
                    StopSession(session);
                }
            }

            private static object StopSession(RecordingSession session)
            {
                EditorApplication.update -= OnEditorUpdate;
                s_Session = null;

                Process process = session.Process;
                if (process == null)
                {
                    return "录制会话状态异常，未找到 ffmpeg 进程。";
                }

                if (!process.HasExited)
                {
                    // ffmpeg 在前台读取 stdin，写入 q 可让它优雅收尾（写 moov / trailer）。
                    try
                    {
                        process.StandardInput.WriteLine("q");
                        process.StandardInput.Flush();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[ScreenRecord] 发送停止信号失败：{exception.Message}");
                    }

                    if (!process.WaitForExit(15000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception exception)
                        {
                            Debug.LogWarning($"[ScreenRecord] 强制结束 ffmpeg 失败：{exception.Message}");
                        }

                        process.WaitForExit(5000);
                    }
                }

                long size = 0L;
                if (File.Exists(session.OutputPath))
                {
                    size = new FileInfo(session.OutputPath).Length;
                }

                if (size == 0L)
                {
                    return $"录制结束但未生成有效文件：{session.OutputPath}\n{session.GetTail()}";
                }

                if (session.OutputPath.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.Ordinal))
                {
                    AssetDatabase.Refresh();
                }

                return $"录制完成：{session.OutputPath}\n" +
                       $"大小：{size / 1024d / 1024d:0.00} MB　区域：{session.Rect.Width}x{session.Rect.Height} @({session.Rect.X},{session.Rect.Y})　帧率：{session.Fps}";
            }

            private static string BuildArguments(int fps, int crf, CaptureRect rect, string outputPath)
            {
                var builder = new StringBuilder();
                Append(builder, "-hide_banner -loglevel warning -y");
                Append(builder, $"-f gdigrab -framerate {fps} -offset_x {rect.X} -offset_y {rect.Y} -video_size {rect.Width}x{rect.Height} -i desktop");
                // yuv420p 要求宽高为偶数，这里兜底裁到偶数避免编码失败。
                Append(builder, "-vf crop=trunc(iw/2)*2:trunc(ih/2)*2");
                Append(builder, $"-c:v libx264 -preset veryfast -crf {crf} -pix_fmt yuv420p");
                // 分片 MP4：即便进程被强杀，已写入的分片仍可播放，避免 moov 缺失导致文件损坏。
                Append(builder, "-movflags +frag_keyframe+empty_moov");
                Append(builder, Quote(outputPath));
                return builder.ToString();
            }

            private static void Append(StringBuilder builder, string text)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(text);
            }

            private static string Quote(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return "\"\"";
                }

                if (value.IndexOf(' ') < 0 && value.IndexOf('"') < 0)
                {
                    return value;
                }

                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            internal static bool TryResolveFfmpeg(out string exePath, out string error)
            {
                exePath = null;
                error = null;

                string onPath = FindOnPath("ffmpeg.exe");
                if (!string.IsNullOrEmpty(onPath))
                {
                    exePath = onPath;
                    return true;
                }

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string wingetLink = System.IO.Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "ffmpeg.exe");
                if (File.Exists(wingetLink))
                {
                    exePath = wingetLink;
                    return true;
                }

                // winget 有时不会生成 Links 软链（尤其绿色/便携安装），这里直接到包目录里找。
                string packaged = FindUnder(System.IO.Path.Combine(localAppData, "Microsoft", "WinGet", "Packages"), "ffmpeg.exe");
                if (!string.IsNullOrEmpty(packaged))
                {
                    exePath = packaged;
                    return true;
                }

                error = "未找到 ffmpeg。录屏依赖外部 ffmpeg，请先安装：winget install --id Gyan.FFmpeg -e（装完后可能需要重启 Unity 让新 PATH 生效），或把 ffmpeg 加入 PATH。";
                return false;
            }

            /// <summary>在指定目录（含子目录）下查找目标文件，用于定位 winget 安装包内的 ffmpeg。</summary>
            private static string FindUnder(string root, string fileName)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                {
                    return null;
                }

                try
                {
                    foreach (string directory in Directory.EnumerateDirectories(root))
                    {
                        string candidate = System.IO.Path.Combine(directory, "bin", fileName);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }

                        string nested = FindUnder(directory, fileName);
                        if (!string.IsNullOrEmpty(nested))
                        {
                            return nested;
                        }
                    }
                }
                catch (Exception)
                {
                    // 包目录可能有权限或符号链接问题，忽略后继续回退。
                }

                return null;
            }

            private static string FindOnPath(string fileName)
            {
                string path = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                foreach (string directory in path.Split(System.IO.Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        continue;
                    }

                    try
                    {
                        string candidate = System.IO.Path.Combine(directory.Trim(), fileName);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略非法 PATH 项。
                    }
                }

                return null;
            }

            private static bool TryResolveRect(XCommandArguments args, out CaptureRect rect, out string note, out string error)
            {
                rect = null;
                note = null;
                error = null;

                string region = args.GetString("region");
                if (!string.IsNullOrEmpty(region))
                {
                    if (!TryParseRegion(region, out rect))
                    {
                        error = $"无法解析 --region，格式应为 X,Y,W,H，收到：{region}";
                        return false;
                    }

                    note = "自定义区域";
                    return true;
                }

                if (args.HasFlag("full"))
                {
                    rect = FullScreenRect();
                    note = "整个屏幕";
                    return true;
                }

                if (TryGetGameViewRect(out rect))
                {
                    note = "GameView 窗口";
                    return true;
                }

                rect = FullScreenRect();
                note = "整个屏幕（未能定位 GameView，已回退）";
                return true;
            }

            private static CaptureRect FullScreenRect()
            {
                return new CaptureRect
                {
                    X = 0,
                    Y = 0,
                    Width = Mathf.Max(1, Screen.currentResolution.width),
                    Height = Mathf.Max(1, Screen.currentResolution.height),
                };
            }

            private static bool TryParseRegion(string value, out CaptureRect rect)
            {
                rect = null;
                string[] parts = value.Split(new[] { ',', '，', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 4)
                {
                    return false;
                }

                if (!int.TryParse(parts[0], out int x) ||
                    !int.TryParse(parts[1], out int y) ||
                    !int.TryParse(parts[2], out int width) ||
                    !int.TryParse(parts[3], out int height))
                {
                    return false;
                }

                if (width <= 0 || height <= 0)
                {
                    return false;
                }

                rect = new CaptureRect { X = x, Y = y, Width = width, Height = height };
                return true;
            }

            /// <summary>
            /// 尽力定位 GameView 窗口在屏幕上的物理像素矩形。停靠状态下 EditorWindow.position 相对主窗口客户区，
            /// 需要叠加客户区在屏幕上的原点；像素换算用 EditorGUIUtility.pixelsPerPoint。
            /// </summary>
            private static bool TryGetGameViewRect(out CaptureRect rect)
            {
                rect = null;
                try
                {
                    Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                    if (gameViewType == null)
                    {
                        return false;
                    }

                    UnityEngine.Object[] found = Resources.FindObjectsOfTypeAll(gameViewType);
                    if (found == null || found.Length == 0)
                    {
                        return false;
                    }

                    if (!(found[0] is EditorWindow gameView))
                    {
                        return false;
                    }

                    IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                    {
                        return false;
                    }

                    var origin = new POINT { X = 0, Y = 0 };
                    if (!ClientToScreen(hwnd, ref origin))
                    {
                        return false;
                    }

                    float scale = EditorGUIUtility.pixelsPerPoint;
                    Rect position = gameView.position;
                    rect = new CaptureRect
                    {
                        X = origin.X + Mathf.RoundToInt(position.x * scale),
                        Y = origin.Y + Mathf.RoundToInt(position.y * scale),
                        Width = Mathf.RoundToInt(position.width * scale),
                        Height = Mathf.RoundToInt(position.height * scale),
                    };

                    return rect.Width > 0 && rect.Height > 0;
                }
                catch (Exception)
                {
                    rect = null;
                    return false;
                }
            }

            [DllImport("user32.dll")]
            private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

            [StructLayout(LayoutKind.Sequential)]
            private struct POINT
            {
                public int X;
                public int Y;
            }

            private sealed class CaptureRect
            {
                public int X;
                public int Y;
                public int Width;
                public int Height;
            }

            private sealed class RecordingSession
            {
                private const int MaxLogLines = 40;

                private readonly Queue<string> m_Log = new Queue<string>();

                public Process Process;
                public string OutputPath;
                public CaptureRect Rect;
                public int Fps;
                public double AutoStopAt;

                public void AppendLog(string line)
                {
                    m_Log.Enqueue(line);
                    while (m_Log.Count > MaxLogLines)
                    {
                        m_Log.Dequeue();
                    }
                }

                public string GetTail()
                {
                    return m_Log.Count == 0 ? "（ffmpeg 无输出）" : string.Join("\n", m_Log);
                }
            }
        }
    }
}
#endif
