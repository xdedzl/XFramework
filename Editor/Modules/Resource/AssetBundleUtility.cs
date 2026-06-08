using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XFramework.Resource;
using static XFramework.Editor.AssetBundleEditor;

namespace XFramework.Editor
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class AssetBundleBuildPreprocessAttribute : Attribute
    {
    }

    public interface IAssetBundleBuildPreprocessContext
    {
        IReadOnlyList<Asset2AB> Asset2Abs { get; }
        void Mark(string assetName, string abName);
    }

    public static class AssetBundleBuildPreprocessor
    {
        public static void Run(List<AssetBundleBuild> builds)
        {
            if (builds == null)
            {
                throw new ArgumentNullException(nameof(builds));
            }

            var context = new AssetBundleBuildPreprocessContext(builds);
            foreach (var method in TypeCache.GetMethodsWithAttribute<AssetBundleBuildPreprocessAttribute>()
                         .OrderBy(method => method.DeclaringType?.FullName, StringComparer.Ordinal)
                         .ThenBy(method => method.Name, StringComparer.Ordinal))
            {
                if (!IsValidPreprocessMethod(method))
                {
                    Debug.LogError($"[AssetBundle] Invalid preprocess method: {FormatMethod(method)}. Expected static void Method(IAssetBundleBuildPreprocessContext context).");
                    continue;
                }

                try
                {
                    method.Invoke(null, new object[] { context });
                }
                catch (TargetInvocationException ex)
                {
                    Exception exception = ex.InnerException ?? ex;
                    Debug.LogException(exception);
                    throw new InvalidOperationException($"[AssetBundle] Preprocess failed: {FormatMethod(method)}", exception);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    throw new InvalidOperationException($"[AssetBundle] Preprocess failed: {FormatMethod(method)}", ex);
                }
            }
        }

        private static bool IsValidPreprocessMethod(MethodInfo method)
        {
            if (method == null || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(IAssetBundleBuildPreprocessContext);
        }

        private static string FormatMethod(MethodInfo method)
        {
            return method == null ? "<null>" : $"{method.DeclaringType?.FullName}.{method.Name}";
        }
    }

    internal sealed class AssetBundleBuildPreprocessContext : IAssetBundleBuildPreprocessContext
    {
        private readonly List<AssetBundleBuild> m_Builds;
        private readonly List<Asset2AB> m_Asset2Abs = new();
        private readonly Dictionary<string, string> m_Asset2AbMap = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<Asset2AB> Asset2Abs => m_Asset2Abs;

        public AssetBundleBuildPreprocessContext(List<AssetBundleBuild> builds)
        {
            m_Builds = builds;
            RebuildAssetMap();
        }

        public void Mark(string assetName, string abName)
        {
            assetName = NormalizePath(assetName);
            abName = NormalizePath(abName);

            if (!IsUnityAssetPath(assetName))
            {
                Debug.LogWarning($"[AssetBundle] Mark skipped invalid asset path: {assetName}");
                return;
            }

            if (!TrySplitFullAbName(abName, out string assetBundleName, out string assetBundleVariant))
            {
                Debug.LogWarning($"[AssetBundle] Mark skipped invalid abName: {abName}. Expected full name with variant, such as Assets/Foo.ab.");
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetName) == null)
            {
                Debug.LogWarning($"[AssetBundle] Mark skipped missing asset: {assetName}");
                return;
            }

            if (m_Asset2AbMap.TryGetValue(assetName, out string existingAbName))
            {
                if (!string.Equals(existingAbName, abName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[AssetBundle] Mark skipped duplicated asset: {assetName} already belongs to {existingAbName}, requested {abName}.");
                }

                return;
            }

            AddAssetToBuild(assetName, assetBundleName, assetBundleVariant);
            AddAssetMap(assetName, abName);
        }

        private void RebuildAssetMap()
        {
            for (int i = 0; i < m_Builds.Count; i++)
            {
                var build = m_Builds[i];
                string abName = GetFullAbName(build);
                if (build.assetNames == null)
                {
                    continue;
                }

                for (int j = 0; j < build.assetNames.Length; j++)
                {
                    AddAssetMap(NormalizePath(build.assetNames[j]), abName);
                }
            }
        }

        private void AddAssetToBuild(string assetName, string assetBundleName, string assetBundleVariant)
        {
            string fullAbName = GetFullAbName(assetBundleName, assetBundleVariant);
            for (int i = 0; i < m_Builds.Count; i++)
            {
                var build = m_Builds[i];
                if (!string.Equals(GetFullAbName(build), fullAbName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var assetNames = build.assetNames ?? Array.Empty<string>();
                if (assetNames.Any(item => string.Equals(NormalizePath(item), assetName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                Array.Resize(ref assetNames, assetNames.Length + 1);
                assetNames[^1] = assetName;
                build.assetNames = assetNames;
                m_Builds[i] = build;
                return;
            }

            m_Builds.Add(new AssetBundleBuild
            {
                assetBundleName = assetBundleName,
                assetBundleVariant = assetBundleVariant,
                assetNames = new[] { assetName }
            });
        }

        private void AddAssetMap(string assetName, string abName)
        {
            if (string.IsNullOrEmpty(assetName) || string.IsNullOrEmpty(abName))
            {
                return;
            }

            m_Asset2Abs.Add(new Asset2AB
            {
                assetPath = assetName,
                abName = abName
            });
            m_Asset2AbMap.TryAdd(assetName, abName);
        }

        private static string GetFullAbName(AssetBundleBuild build)
        {
            return GetFullAbName(NormalizePath(build.assetBundleName), NormalizePath(build.assetBundleVariant));
        }

        private static string GetFullAbName(string assetBundleName, string assetBundleVariant)
        {
            return string.IsNullOrEmpty(assetBundleVariant)
                ? assetBundleName
                : $"{assetBundleName}.{assetBundleVariant}";
        }

        private static bool TrySplitFullAbName(string abName, out string assetBundleName, out string assetBundleVariant)
        {
            assetBundleName = string.Empty;
            assetBundleVariant = string.Empty;
            if (string.IsNullOrEmpty(abName))
            {
                return false;
            }

            int slashIndex = abName.LastIndexOf('/');
            int variantIndex = abName.LastIndexOf('.');
            if (variantIndex <= slashIndex || variantIndex == abName.Length - 1)
            {
                return false;
            }

            assetBundleName = abName[..variantIndex];
            assetBundleVariant = abName[(variantIndex + 1)..];
            return !string.IsNullOrEmpty(assetBundleName) && !string.IsNullOrEmpty(assetBundleVariant);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }

        private static bool IsUnityAssetPath(string assetPath)
        {
            return assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                   assetPath.StartsWith("Packages/", StringComparison.Ordinal);
        }
    }

    public static class AssetBundleUtility
    {
        /// <summary>
        /// 标记文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="packOption"></param>
        public static List<AssetBundleBuild> MarkDirectory(DirectoryInfo dirInfo, PackOption packOption, bool isRootNameEqualAsset = true)
        {
            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
            if (isRootNameEqualAsset)
            {
                MarkDirectory(dirInfo, packOption, "Assets", ref builds);
            }
            else
            {
                MarkDirectory(dirInfo, packOption, dirInfo.Name, ref builds);
            }

            return builds;
        }

        /// <summary>
        /// 标记文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="packOption"></param>
        private static void MarkDirectory(DirectoryInfo dirInfo, PackOption packOption, string markRootName, ref List<AssetBundleBuild> builds)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirectory;
            switch (packOption)
            {
                case PackOption.AllFiles:
                    files = dirInfo.GetFiles("*", SearchOption.AllDirectories);        // 取出所有文件
                    break;
                case PackOption.TopDirectory:
                    files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                    subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var item in subDirectory)
                    {
                        MarkDirectory(item, PackOption.AllFiles, markRootName, ref builds);
                    }
                    break;
                case PackOption.AllDirectory:
                    files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                    subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var item in subDirectory)
                    {
                        MarkDirectory(item, PackOption.AllDirectory, markRootName, ref builds);
                    }
                    break;
                case PackOption.TopFileOnly:
                    files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);        // 取出所有文件
                    break;
            }

            // 添加AssetBundleBuild
            List<string> assetNames = new List<string>();

            int total = files.Length;
            string abName = dirInfo.FullName.Substring(dirInfo.FullName.IndexOf(markRootName, StringComparison.Ordinal));
            for (int i = 0; i < total; i++)
            {
                var fileInfo = files[i];

                if (fileInfo.Name.EndsWith(".meta")) continue;

                EditorUtility.DisplayProgressBar(dirInfo.Name, $"正在标记资源{fileInfo.Name}...", (float)i / total);

                string filePath = fileInfo.FullName.Substring(fileInfo.FullName.IndexOf("Assets", StringComparison.Ordinal));       // 获取 "Assets"目录起的 文件名, 可不用转 "\\"

                assetNames.Add(filePath);
            }

            if (assetNames.Count > 0)
            {
                builds.Add(new AssetBundleBuild()
                {
                    assetBundleName = abName,
                    assetBundleVariant = "ab",
                    assetNames = assetNames.ToArray()
                });
            }

            EditorUtility.ClearProgressBar();
        }

    }
}
