using System;
using UnityEngine;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        internal static class Path
        {
            internal static string ResolvePath(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("截图路径不能为空。", nameof(path));
                if (TryResolvePrefix(path, "persistent:/", Application.persistentDataPath, out string resolved) ||
                    TryResolvePrefix(path, "data:/", Application.dataPath, out resolved) ||
                    TryResolvePrefix(path, "temp:/", Application.temporaryCachePath, out resolved))
                    return System.IO.Path.GetFullPath(resolved);
                if (!System.IO.Path.IsPathRooted(path))
                    throw new ArgumentException("截图路径必须是绝对路径，或使用 persistent:/、data:/、temp:/ 前缀。", nameof(path));
                return System.IO.Path.GetFullPath(path);
            }

            private static bool TryResolvePrefix(string path, string prefix, string root, out string resolved)
            {
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    resolved = null;
                    return false;
                }
                string relative = path.Substring(prefix.Length).TrimStart('/', '\\');
                resolved = System.IO.Path.Combine(root, relative);
                return true;
            }
        }
    }
}
