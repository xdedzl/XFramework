#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEditor.Media;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XFramework.Command
{
    /// <summary>
    /// 离屏录制的协程宿主。独立于 XCommand 操作队列运行，
    /// 以便录制期间仍可用 ui-input 注入输入（操作队列是串行的）。
    /// </summary>
    internal sealed class XCommandOffscreenRecordRunner : MonoBehaviour
    {
        private XCommandOffscreenRecordRequest m_Request;
        private XCommandOffscreenRecordStatus m_Status;
        private Action m_OnCompleted;

        internal void Begin(
            XCommandOffscreenRecordRequest request,
            XCommandOffscreenRecordStatus status,
            Action onCompleted)
        {
            m_Request = request;
            m_Status = status;
            m_OnCompleted = onCompleted;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            XCommandOffscreenRecordRequest request = m_Request;
            XCommandOffscreenRecordStatus status = m_Status;

            int fps = request.fps;
            int totalFrames = request.frames;
            int width = request.width;
            int height = request.height;
            string path = request.path;
            bool frameStep = request.frameStep;

            int previousCaptureFramerate = Time.captureFramerate;
            if (frameStep)
            {
                Time.captureFramerate = fps;
            }

            RenderTexture renderTexture = null;
            RenderTexture flipTexture = null;
            Texture2D frameTexture = null;
            MediaEncoder encoder = null;
            Process ffmpegProcess = null;
            Stream ffmpegInput = null;
            bool useFfmpeg = false;
            string encoderName = "media-encoder";
            string error = null;
            int captured = 0;
            double startedAt = Time.realtimeSinceStartup;
            var readRect = new Rect(0, 0, width, height);
            float captureInterval = 1f / fps;
            float cadence = 0f;

            try
            {
                try
                {
                    string directory = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    XCommandUtility.OffscreenRecord.CreateBackend(
                        path, fps, width, height, request.noFallback,
                        ref encoder, ref ffmpegProcess, ref ffmpegInput, ref encoderName);

                    renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    {
                        name = "[XCommandOffscreenRecord]",
                    };
                    renderTexture.Create();
                    flipTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    {
                        name = "[XCommandOffscreenRecordFlip]",
                    };
                    flipTexture.Create();
                    frameTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                    useFfmpeg = ffmpegInput != null;
                }
                catch (Exception exception)
                {
                    error = exception.ToString();
                }

                while (error == null && captured < totalFrames && !status.stopRequested)
                {
                    yield return new WaitForEndOfFrame();

                    try
                    {
                        if (!frameStep)
                        {
                            // 实时模式：按目标帧率节流，避免游戏帧率高于目标帧率时产出过快。
                            cadence += Time.unscaledDeltaTime;
                            if (cadence < captureInterval)
                            {
                                continue;
                            }

                            cadence -= captureInterval;
                            if (cadence > captureInterval * 4f)
                            {
                                cadence = 0f;
                            }
                        }

                        // CaptureScreenshotIntoRenderTexture 与 Graphics.Blit 都会把目标 RT 设为 RenderTexture.active，
                        // 这里先记录进入前的 active，最终统一还原，避免退出时 Release 仍处于 active 的 RT 触发 Unity 警告。
                        RenderTexture previousActive = RenderTexture.active;

                        ScreenCapture.CaptureScreenshotIntoRenderTexture(renderTexture);

                        // CaptureScreenshotIntoRenderTexture 写入的 RT 在 D3D 下是上下颠倒的，
                        // 用负 Y 缩放的 Blit 翻正，保证 MediaEncoder 与 ffmpeg 两个后端拿到的都是正常朝向。
                        Graphics.Blit(renderTexture, flipTexture, new Vector2(1f, -1f), new Vector2(0f, 1f));

                        try
                        {
                            RenderTexture.active = flipTexture;
                            frameTexture.ReadPixels(readRect, 0, 0, false);
                        }
                        finally
                        {
                            RenderTexture.active = previousActive;
                        }

                        frameTexture.Apply(false, false);

                        try
                        {
                            XCommandUtility.OffscreenRecord.WriteFrame(frameTexture, useFfmpeg, encoder, ffmpegInput);
                        }
                        catch (Exception exception) when (!useFfmpeg && !request.noFallback && captured == 0)
                        {
                            // MediaEncoder 建好了却在写第一帧时失败：切到 ffmpeg 重写这一帧。
                            Debug.LogWarning($"[OffscreenRecord] MediaEncoder 写入失败（{exception.Message}），回退到 ffmpeg rawvideo。");
                            XCommandUtility.OffscreenRecord.DisposeMediaEncoder(ref encoder);
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }

                            if (!XCommandUtility.OffscreenRecord.TryStartFfmpegPipe(
                                    path, fps, width, height, out ffmpegProcess, out ffmpegInput, out string ffmpegError))
                            {
                                throw new InvalidOperationException($"MediaEncoder 写入失败（{exception.Message}），ffmpeg 回退也失败：{ffmpegError}");
                            }

                            useFfmpeg = true;
                            encoderName = "ffmpeg-rawvideo";
                            XCommandUtility.OffscreenRecord.WriteFrame(frameTexture, true, null, ffmpegInput);
                        }

                        captured++;
                        status.captured = captured;
                    }
                    catch (Exception exception)
                    {
                        error = exception.ToString();
                    }
                }
            }
            finally
            {
                if (frameStep)
                {
                    Time.captureFramerate = previousCaptureFramerate;
                }

                XCommandUtility.OffscreenRecord.DisposeMediaEncoder(ref encoder);

                if (ffmpegInput != null)
                {
                    try
                    {
                        ffmpegInput.Flush();
                        ffmpegInput.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[OffscreenRecord] 关闭 ffmpeg 输入失败：{exception.Message}");
                    }
                }

                if (ffmpegProcess != null)
                {
                    try
                    {
                        if (!ffmpegProcess.HasExited && !ffmpegProcess.WaitForExit(15000))
                        {
                            ffmpegProcess.Kill();
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[OffscreenRecord] 结束 ffmpeg 失败：{exception.Message}");
                    }

                    ffmpegProcess.Dispose();
                }

                if (flipTexture != null)
                {
                    flipTexture.Release();
                    UnityEngine.Object.Destroy(flipTexture);
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.Destroy(renderTexture);
                }

                if (frameTexture != null)
                {
                    UnityEngine.Object.Destroy(frameTexture);
                }
            }

            long size = File.Exists(path) ? new FileInfo(path).Length : 0L;
            if (error == null && size == 0L)
            {
                error = "录制结束但未生成有效文件。";
            }

            status.running = false;
            status.success = error == null;
            status.captured = captured;
            status.encoder = encoderName;
            status.elapsedSeconds = (float)(Time.realtimeSinceStartup - startedAt);
            status.error = error;

            Debug.Log(error == null
                ? $"[OffscreenRecord] 录制完成：{status.path}（{captured} 帧 / {status.elapsedSeconds:F2}s，{encoderName}，{status.mode}）"
                : $"[OffscreenRecord] 录制结束但失败：{error}");

            m_OnCompleted?.Invoke();
        }
    }
}
#endif