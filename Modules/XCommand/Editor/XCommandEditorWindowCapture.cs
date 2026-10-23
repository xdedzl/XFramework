using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using XFramework.Command;
using Object = UnityEngine.Object;

namespace XFramework.Editor
{
    internal static class XCommandEditorWindowCapture
    {
        internal const string ListUsage = "editor-window-list [--query TEXT]";
        internal const string ScreenshotUsage = "editor-window-screenshot PNG_PATH --window TITLE_OR_TYPE [--width W --height H]";

        private const int MaxCaptureDimension = 8192;
        private static readonly string[] ListValueOptions = { "query" };
        private static readonly string[] ScreenshotValueOptions = { "window", "width", "height" };
        private static readonly Type GuiViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");
        private static readonly FieldInfo ParentField = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo GrabPixelsMethod = GuiViewType == null ? null : GuiViewType.GetMethod("GrabPixels", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo RepaintImmediatelyMethod = typeof(EditorWindow).GetMethod("RepaintImmediately", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        internal static object ExecuteList(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, ListValueOptions, Array.Empty<string>());
            args.RequireValueCount(0, $"usage: {ListUsage}");

            string query = args.GetString("query");
            List<EditorWindow> windows = CollectWindows(query);
            windows.Sort(CompareWindows);

            if (windows.Count == 0)
            {
                return string.IsNullOrWhiteSpace(query)
                    ? "当前没有打开的 EditorWindow。"
                    : $"没有匹配的 EditorWindow：{query}";
            }

            var builder = new StringBuilder();
            builder.Append("已打开的 EditorWindow：").Append(windows.Count).AppendLine();
            for (int i = 0; i < windows.Count; i++)
            {
                builder.AppendLine(FormatWindowLine(windows[i]));
            }

            return builder.ToString().TrimEnd();
        }

        internal static object ExecuteScreenshot(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, ScreenshotValueOptions, Array.Empty<string>());
            args.RequireValueCount(1, $"usage: {ScreenshotUsage}");

            string query = args.GetString("window");
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException($"--window 不能为空。usage: {ScreenshotUsage}");
            }

            int width = args.GetInt("width", 0);
            int height = args.GetInt("height", 0);
            ValidateCaptureDimensions(width, height);

            string outputPath = ResolvePngPath(args.Values[0]);
            EditorWindow window = ResolveWindow(query, out string error);
            if (window == null)
            {
                return error;
            }

            return CaptureWindow(window, outputPath, width, height);
        }

        private static List<EditorWindow> CollectWindows(string query)
        {
            var result = new List<EditorWindow>();
            EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < allWindows.Length; i++)
            {
                EditorWindow window = allWindows[i];
                if (window != null && MatchesQuery(window, query))
                {
                    result.Add(window);
                }
            }

            return result;
        }

        private static bool MatchesQuery(EditorWindow window, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string title = GetWindowTitle(window);
            string typeName = window.GetType().Name;
            return title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ValidateCaptureDimensions(int width, int height)
        {
            if (width < 0 || height < 0)
            {
                throw new ArgumentException("--width 与 --height 不能为负数。");
            }

            if ((width == 0) != (height == 0))
            {
                throw new ArgumentException("--width 与 --height 必须同时指定。");
            }

            if (width > MaxCaptureDimension || height > MaxCaptureDimension)
            {
                throw new ArgumentException($"--width 与 --height 不能超过 {MaxCaptureDimension}。");
            }
        }

        private static string ResolvePngPath(string path)
        {
            string outputPath = XCommandUtility.Path.ResolvePath(path);
            string extension = Path.GetExtension(outputPath);
            if (string.IsNullOrEmpty(extension))
            {
                outputPath = Path.ChangeExtension(outputPath, ".png");
                extension = ".png";
            }

            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"编辑器窗口截图只支持 PNG，收到扩展名：{extension}");
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return Path.GetFullPath(outputPath);
        }

        private static EditorWindow ResolveWindow(string query, out string error)
        {
            error = null;
            EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var exactTitles = new List<EditorWindow>();
            var exactTypes = new List<EditorWindow>();
            var partialMatches = new List<EditorWindow>();

            for (int i = 0; i < allWindows.Length; i++)
            {
                EditorWindow window = allWindows[i];
                if (window == null)
                {
                    continue;
                }

                string title = GetWindowTitle(window);
                string typeName = window.GetType().Name;
                if (string.Equals(title, query, StringComparison.OrdinalIgnoreCase))
                {
                    exactTitles.Add(window);
                }
                else if (string.Equals(typeName, query, StringComparison.OrdinalIgnoreCase))
                {
                    exactTypes.Add(window);
                }
                else if (title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || typeName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    partialMatches.Add(window);
                }
            }

            EditorWindow match = SelectMatch(exactTitles, query, out error);
            if (match != null || !string.IsNullOrEmpty(error))
            {
                return match;
            }

            match = SelectMatch(exactTypes, query, out error);
            if (match != null || !string.IsNullOrEmpty(error))
            {
                return match;
            }

            match = SelectMatch(partialMatches, query, out error);
            if (match != null || !string.IsNullOrEmpty(error))
            {
                return match;
            }

            error = $"未找到 EditorWindow：{query}\n可用窗口：\n{BuildWindowCandidates(allWindows, 30)}";
            return null;
        }

        private static EditorWindow SelectMatch(List<EditorWindow> matches, string query, out string error)
        {
            error = null;
            if (matches.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i].hasFocus)
                {
                    return matches[i];
                }
            }

            if (matches.Count == 1)
            {
                return matches[0];
            }

            var builder = new StringBuilder();
            builder.Append("窗口匹配不唯一：").Append(query).AppendLine();
            for (int i = 0; i < matches.Count; i++)
            {
                EditorWindow window = matches[i];
                builder.Append(GetWindowTitle(window))
                    .Append(" | ")
                    .Append(window.GetType().Name)
                    .Append(" | ")
                    .AppendLine(window.GetInstanceID().ToString());
            }

            error = builder.ToString().TrimEnd();
            return null;
        }

        private static string BuildWindowCandidates(EditorWindow[] windows, int limit)
        {
            var builder = new StringBuilder();
            int count = Math.Min(windows.Length, limit);
            for (int i = 0; i < count; i++)
            {
                EditorWindow window = windows[i];
                if (window == null)
                {
                    continue;
                }

                builder.Append(GetWindowTitle(window))
                    .Append(" | ")
                    .AppendLine(window.GetType().Name);
            }

            if (windows.Length > count)
            {
                builder.Append("其余窗口数量：").Append(windows.Length - count).AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatWindowLine(EditorWindow window)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            int width = Mathf.Max(1, Mathf.RoundToInt(window.position.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(window.position.height * scale));
            string focus = window.hasFocus ? "[聚焦]" : "[未聚焦]";
            return $"{focus} {GetWindowTitle(window)} | {window.GetType().Name} | docked={window.docked} | {width}x{height} | id={window.GetInstanceID()}";
        }

        private static int CompareWindows(EditorWindow left, EditorWindow right)
        {
            int titleCompare = string.Compare(GetWindowTitle(left), GetWindowTitle(right), StringComparison.OrdinalIgnoreCase);
            if (titleCompare != 0)
            {
                return titleCompare;
            }

            int typeCompare = string.Compare(left.GetType().Name, right.GetType().Name, StringComparison.OrdinalIgnoreCase);
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private static string GetWindowTitle(EditorWindow window)
        {
            string title = window.titleContent == null ? null : window.titleContent.text;
            return string.IsNullOrWhiteSpace(title) ? "<无标题>" : title;
        }

        private static object CaptureWindow(EditorWindow window, string outputPath, int requestedWidth, int requestedHeight)
        {
            EnsureCaptureSupported();

            window.Show();
            window.Focus();

            object parent = ParentField.GetValue(window);
            if (parent == null || !GuiViewType.IsInstanceOfType(parent))
            {
                throw new InvalidOperationException($"窗口没有可用的 GUIView：{GetWindowTitle(window)} ({window.GetType().Name})");
            }

            float scale = EditorGUIUtility.pixelsPerPoint;
            int nativeWidth = Mathf.Clamp(Mathf.RoundToInt(window.position.width * scale), 16, MaxCaptureDimension);
            int nativeHeight = Mathf.Clamp(Mathf.RoundToInt(window.position.height * scale), 16, MaxCaptureDimension);

            RenderTexture grabTexture = null;
            Texture2D sourceTexture = null;
            Texture2D outputTexture = null;
            try
            {
                RepaintImmediatelyMethod.Invoke(window, null);

                grabTexture = new RenderTexture(nativeWidth, nativeHeight, 0, RenderTextureFormat.ARGB32);
                grabTexture.Create();
                GrabPixelsMethod.Invoke(parent, new object[] {
                    grabTexture,
                    new Rect(0, 0, nativeWidth, nativeHeight),
                });

                sourceTexture = ReadRenderTexture(grabTexture);
                outputTexture = requestedWidth > 0
                    ? ResizeTexture(sourceTexture, requestedWidth, requestedHeight)
                    : sourceTexture;

                File.WriteAllBytes(outputPath, outputTexture.EncodeToPNG());
                if (outputPath.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.Ordinal))
                {
                    AssetDatabase.Refresh();
                }

                return $"已保存编辑器窗口截图：{outputPath}\n窗口：{GetWindowTitle(window)}\n类型：{window.GetType().Name}\n尺寸：{outputTexture.width}x{outputTexture.height}\n范围：窗口内容区（不含 Dock 标签栏和窗口标题栏）";
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                throw new InvalidOperationException($"EditorWindow 截图失败：{GetWindowTitle(window)} ({window.GetType().Name})：{inner.Message}", inner);
            }
            finally
            {
                if (outputTexture != null && outputTexture != sourceTexture)
                {
                    Object.DestroyImmediate(outputTexture);
                }

                if (sourceTexture != null)
                {
                    Object.DestroyImmediate(sourceTexture);
                }

                if (grabTexture != null)
                {
                    grabTexture.Release();
                    Object.DestroyImmediate(grabTexture);
                }
            }
        }

        private static void EnsureCaptureSupported()
        {
            if (GuiViewType == null
                || ParentField == null
                || GrabPixelsMethod == null
                || RepaintImmediatelyMethod == null)
            {
                throw new InvalidOperationException("当前 Unity 版本不支持 EditorWindow 离屏截图，缺少 GUIView.GrabPixels、EditorWindow.m_Parent 或 EditorWindow.RepaintImmediately。");
            }
        }

        private static Texture2D ReadRenderTexture(RenderTexture source)
        {
            RenderTexture previousActive = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                RenderTexture.active = source;
                texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                texture.Apply(false, false);
                FlipVertically(texture);
                return texture;
            }
            catch
            {
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                throw;
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private static void FlipVertically(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int rowLength = texture.width;
            var rowBuffer = new Color32[rowLength];
            for (int y = 0; y < texture.height / 2; y++)
            {
                int topOffset = y * rowLength;
                int bottomOffset = (texture.height - 1 - y) * rowLength;
                Array.Copy(pixels, topOffset, rowBuffer, 0, rowLength);
                Array.Copy(pixels, bottomOffset, pixels, topOffset, rowLength);
                Array.Copy(rowBuffer, 0, pixels, bottomOffset, rowLength);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private static Texture2D ResizeTexture(Texture source, int width, int height)
        {
            RenderTexture resizeTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D result = null;
            try
            {
                resizeTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, resizeTexture);
                RenderTexture.active = resizeTexture;
                result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                result.Apply(false, false);
                return result;
            }
            catch
            {
                if (result != null)
                {
                    Object.DestroyImmediate(result);
                }

                throw;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (resizeTexture != null)
                {
                    RenderTexture.ReleaseTemporary(resizeTexture);
                }
            }
        }
    }
}
