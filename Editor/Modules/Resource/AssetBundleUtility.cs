using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static XFramework.Editor.AssetBundleEditor;

namespace XFramework.Editor
{
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
                case PackOption.TopDirectiony:
                    files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                    subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var item in subDirectory)
                    {
                        MarkDirectory(item, PackOption.AllFiles, markRootName, ref builds);
                    }
                    break;
                case PackOption.AllDirectiony:
                    files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                    subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var item in subDirectory)
                    {
                        MarkDirectory(item, PackOption.AllDirectiony, markRootName, ref builds);
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