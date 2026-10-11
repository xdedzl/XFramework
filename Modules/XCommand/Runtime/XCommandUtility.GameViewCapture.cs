#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        /// <summary>
        /// GameView 截图（编辑器专用）：把当前场景的视图相机离屏渲染成 PNG，不需要进入 Play 模式。
        /// </summary>
        internal static class GameViewCapture
        {
            internal const string Usage = "gameview-screenshot PATH [--width W --height H] [--camera NAME]";

            private static readonly string[] ValueOptions = { "width", "height", "camera" };

            internal static object Execute(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(argument, ValueOptions, Array.Empty<string>());
                args.RequireValueCount(1, $"usage: {Usage}");

                int width = args.GetInt("width", 0);
                int height = args.GetInt("height", 0);
                if (width > 0 != height > 0)
                    throw new ArgumentException("--width 与 --height 必须同时指定。");
                if (width <= 0)
                {
                    width = Mathf.Max(1, Screen.width);
                    height = Mathf.Max(1, Screen.height);
                }

                if (!TryResolveCamera(args.GetString("camera"), out Camera camera, out string cameraError))
                    return cameraError;

                string outputPath = XCommandUtility.Path.ResolvePath(args.Values[0]);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture previousTarget = camera.targetTexture;
                RenderTexture target = null;
                Texture2D screenshot = null;
                try
                {
                    target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;

                    screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    screenshot.Apply();
                    File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    if (target != null)
                        RenderTexture.ReleaseTemporary(target);
                    if (screenshot != null)
                        Object.DestroyImmediate(screenshot);
                }

                if (outputPath.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.Ordinal))
                    AssetDatabase.Refresh();

                return $"已保存 GameView 截图：{outputPath}\n相机：{camera.name}　尺寸：{width}x{height}\n（仅含相机渲染内容，Screen Space Overlay 的 UI 不包含在内）";
            }

            private static bool TryResolveCamera(string name, out Camera camera, out string error)
            {
                camera = null;
                error = null;
                Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                if (!string.IsNullOrEmpty(name))
                {
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        if (string.Equals(cameras[i].name, name, StringComparison.Ordinal))
                        {
                            camera = cameras[i];
                            break;
                        }
                    }

                    if (camera == null)
                    {
                        error = $"未找到名为 {name} 的启用相机。";
                        return false;
                    }

                    return true;
                }

                if (Camera.main != null && Camera.main.enabled)
                {
                    camera = Camera.main;
                    return true;
                }

                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera candidate = cameras[i];
                    if (!candidate.enabled || candidate.targetTexture != null || candidate.cameraType != CameraType.Game)
                        continue;
                    if (camera == null || candidate.depth > camera.depth)
                        camera = candidate;
                }

                if (camera == null)
                {
                    error = "当前场景没有可用的 Game 相机（enabled、无 targetTexture）。可用 --camera NAME 指定。";
                    return false;
                }

                return true;
            }
        }
    }
}
#endif
