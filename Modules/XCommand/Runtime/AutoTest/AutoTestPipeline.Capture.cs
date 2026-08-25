using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace XFramework.AutoTest
{
    internal static class AutoTestCapture
    {
        [Serializable]
        private sealed class CaptureReceipt
        {
            public bool success;
            public string path;
            public int width;
            public int height;
        }

        internal static IEnumerator Capture(AutoTestScreenshotRequest request, AutoTestOperationContext context)
        {
            if ((request.width > 0) != (request.height > 0))
                throw new ArgumentException("width 和 height 必须同时指定。");
            string path = ResolvePath(request.path);
            float startedAt = Time.realtimeSinceStartup;
            UICameraCaptureScope uiCameraScope = request.uiOnly ? new UICameraCaptureScope() : null;
            try
            {
                yield return new WaitForEndOfFrame();
                if (Time.realtimeSinceStartup - startedAt > Mathf.Max(0.1f, request.timeoutSeconds))
                    throw new TimeoutException($"截图超时：{request.timeoutSeconds.ToString(CultureInfo.InvariantCulture)} 秒。");
                CaptureResult result = CaptureScreenshot(path, request.width, request.height, request.region);
                context.SetOutput(JsonUtility.ToJson(new CaptureReceipt {
                    success = true,
                    path = path.Replace('\\', '/'),
                    width = result.Width,
                    height = result.Height,
                }));
            }
            finally
            {
                uiCameraScope?.Dispose();
            }
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("截图路径不能为空。", nameof(path));
            if (TryResolvePrefix(path, "persistent:/", Application.persistentDataPath, out string resolved) ||
                TryResolvePrefix(path, "data:/", Application.dataPath, out resolved) ||
                TryResolvePrefix(path, "temp:/", Application.temporaryCachePath, out resolved))
                return Path.GetFullPath(resolved);
            if (!Path.IsPathRooted(path))
                throw new ArgumentException("截图路径必须是绝对路径，或使用 persistent:/、data:/、temp:/ 前缀。", nameof(path));
            return Path.GetFullPath(path);
        }

        private static bool TryResolvePrefix(string path, string prefix, string root, out string resolved)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                resolved = null;
                return false;
            }
            string relative = path.Substring(prefix.Length).TrimStart('/', '\\');
            resolved = Path.Combine(root, relative);
            return true;
        }

        private static CaptureResult CaptureScreenshot(string path, int targetWidth, int targetHeight, RectInt region)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            Texture2D sourceTexture = null;
            Texture2D outputTexture = null;
            try
            {
                sourceTexture = ScreenCapture.CaptureScreenshotAsTexture();
                if (sourceTexture == null)
                    throw new InvalidOperationException("CaptureScreenshotAsTexture returned null.");
                outputTexture = ApplyRegion(sourceTexture, region);
                if (targetWidth > 0 && targetHeight > 0 && (outputTexture.width != targetWidth || outputTexture.height != targetHeight))
                {
                    Texture2D resized = ResizeTexture(outputTexture, targetWidth, targetHeight);
                    if (outputTexture != sourceTexture)
                        UnityEngine.Object.Destroy(outputTexture);
                    outputTexture = resized;
                }
                File.WriteAllBytes(path, outputTexture.EncodeToPNG());
                return new CaptureResult(outputTexture.width, outputTexture.height);
            }
            finally
            {
                if (outputTexture != null && outputTexture != sourceTexture)
                    UnityEngine.Object.Destroy(outputTexture);
                if (sourceTexture != null)
                    UnityEngine.Object.Destroy(sourceTexture);
            }
        }

        private static Texture2D ApplyRegion(Texture2D source, RectInt region)
        {
            if (region.width <= 0 || region.height <= 0)
                return source;
            if (region.x < 0 || region.y < 0 || region.xMax > source.width || region.yMax > source.height)
                throw new ArgumentOutOfRangeException(nameof(region), $"截图区域 {region.x},{region.y},{region.width},{region.height} 超出画面 {source.width}x{source.height}。");
            var cropped = new Texture2D(region.width, region.height, TextureFormat.RGB24, false, true);
            cropped.SetPixels(source.GetPixels(region.x, region.y, region.width, region.height));
            cropped.Apply(false, false);
            return cropped;
        }

        private static Texture2D ResizeTexture(Texture source, int targetWidth, int targetHeight)
        {
            RenderTexture renderTexture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                var resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0, false);
                resized.Apply(false, false);
                return resized;
            }
            finally
            {
                RenderTexture.active = previous;
                if (renderTexture != null)
                    RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private readonly struct CaptureResult
        {
            public CaptureResult(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }
            public int Height { get; }
        }

        private sealed class UICameraCaptureScope : IDisposable
        {
            private sealed class CameraState
            {
                public Camera Camera;
                public int CullingMask;
                public CameraClearFlags ClearFlags;
                public Color BackgroundColor;
            }

            private readonly List<CameraState> m_CameraStates = new List<CameraState>();
            private readonly GameObject m_ClearCameraObject;
            private bool m_Disposed;

            public UICameraCaptureScope()
            {
                var uiCameras = new HashSet<Camera>();
                foreach (Canvas canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (canvas.isActiveAndEnabled && canvas.isRootCanvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
                        uiCameras.Add(canvas.worldCamera);
                }
                Camera[] nonUICameras = Camera.allCameras.Where(camera => camera.enabled && !uiCameras.Contains(camera)).OrderBy(camera => camera.depth).ToArray();
                Camera backgroundCamera = nonUICameras.FirstOrDefault();
                for (int i = 0; i < nonUICameras.Length; i++)
                {
                    Camera camera = nonUICameras[i];
                    m_CameraStates.Add(new CameraState {
                        Camera = camera,
                        CullingMask = camera.cullingMask,
                        ClearFlags = camera.clearFlags,
                        BackgroundColor = camera.backgroundColor,
                    });
                    camera.cullingMask = 0;
                    camera.clearFlags = camera == backgroundCamera ? CameraClearFlags.SolidColor : CameraClearFlags.Nothing;
                    camera.backgroundColor = Color.clear;
                }
                if (backgroundCamera == null)
                {
                    m_ClearCameraObject = new GameObject("[XFrameworkAutoTestUIClearCamera]") { hideFlags = HideFlags.HideAndDontSave };
                    Camera clearCamera = m_ClearCameraObject.AddComponent<Camera>();
                    clearCamera.clearFlags = CameraClearFlags.SolidColor;
                    clearCamera.backgroundColor = Color.clear;
                    clearCamera.cullingMask = 0;
                    clearCamera.depth = uiCameras.Count > 0 ? uiCameras.Min(camera => camera.depth) - 1f : -10000f;
                }
            }

            public void Dispose()
            {
                if (m_Disposed)
                    return;
                m_Disposed = true;
                for (int i = 0; i < m_CameraStates.Count; i++)
                {
                    CameraState state = m_CameraStates[i];
                    if (state.Camera == null)
                        continue;
                    state.Camera.cullingMask = state.CullingMask;
                    state.Camera.clearFlags = state.ClearFlags;
                    state.Camera.backgroundColor = state.BackgroundColor;
                }
                if (m_ClearCameraObject != null)
                    UnityEngine.Object.Destroy(m_ClearCameraObject);
            }
        }
    }
}
