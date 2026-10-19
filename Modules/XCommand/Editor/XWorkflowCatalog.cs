using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace XFramework.Editor
{
    internal sealed class XWorkflowEntry
    {
        public string Name;
        public string RelativeDirectoryPath;
        public string DirectoryPath;
        public string ReadmePath;
    }

    internal static class XWorkflowCatalog
    {
        /// <summary>
        /// Workflow 目录相对 Unity 工程根目录（Application.dataPath 的上级目录）的位置。
        /// 该目录独立于本仓库存在，可能缺失；缺失时 Workflow Tab 只显示提示，不影响其它功能。
        /// </summary>
        internal const string WorkflowsRelativePath = "../../x-framework-workflow";

        private const string ReadmeFileName = "README.md";

        internal static List<XWorkflowEntry> Discover(out string message)
        {
            var entries = new List<XWorkflowEntry>();
            string relativeRoot = NormalizeRelativePath(WorkflowsRelativePath);
            string rootPath = ResolveRootPath(relativeRoot);
            if (string.IsNullOrEmpty(rootPath))
            {
                message = "无法定位 Unity 工程根目录，无法解析 Workflow 目录。";
                return entries;
            }

            if (!Directory.Exists(rootPath))
            {
                message = $"未找到 Workflow 目录（相对 Unity 工程根目录：{relativeRoot}）。该目录可不提供，缺失时不影响其它功能。";
                return entries;
            }

            try
            {
                string[] directories = Directory.GetDirectories(rootPath);
                for (int i = 0; i < directories.Length; i++)
                {
                    string name = Path.GetFileName(directories[i]);
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    string readmePath = Path.Combine(directories[i], ReadmeFileName);
                    if (!File.Exists(readmePath))
                    {
                        continue;
                    }

                    entries.Add(new XWorkflowEntry
                    {
                        Name = name,
                        RelativeDirectoryPath = relativeRoot + "/" + name,
                        DirectoryPath = directories[i],
                        ReadmePath = readmePath
                    });
                }
            }
            catch (Exception exception)
            {
                entries.Clear();
                message = $"扫描 Workflow 目录失败（{relativeRoot}）：{exception.Message}";
                return entries;
            }

            entries.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            message = entries.Count == 0
                ? $"目录 {relativeRoot} 下没有 Workflow。每个 Workflow 需要一个独立文件夹并包含 {ReadmeFileName}。"
                : "单击条目查看对应 README.md 原文。";
            return entries;
        }

        internal static string ReadReadme(XWorkflowEntry entry, out string error)
        {
            error = string.Empty;
            if (entry == null)
            {
                error = "未选择 Workflow。";
                return string.Empty;
            }

            try
            {
                if (!File.Exists(entry.ReadmePath))
                {
                    error = $"未找到 {entry.RelativeDirectoryPath}/{ReadmeFileName}";
                    return string.Empty;
                }

                return File.ReadAllText(entry.ReadmePath, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                error = $"读取 README.md 失败：{exception.Message}";
                return string.Empty;
            }
        }

        private static string ResolveRootPath(string relativeRoot)
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(projectRoot.FullName, relativeRoot));
        }

        private static string NormalizeRelativePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
