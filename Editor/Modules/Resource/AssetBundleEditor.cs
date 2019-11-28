using System;
using System.Collections.Generic;
using System.IO;
using XFramework.Resource;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    /// <summary>
    /// AssetBundle窗口
    /// </summary>
    public partial class AssetBundleEditor : EditorWindow
    {
        private enum TabMode
        {
            Default,
            Dependence,
            Mainfest2Json,
            BuildProject,
        }

        private enum PackOption
        {
            AllFiles,       // 所有文件一个包 
            TopDirectiony,  // 一级子文件夹单独打包
            AllDirectiony,  // 所有子文件夹单独打包
            TopFileOnly,    // 只打包当前文件夹的文件
        }


        [MenuItem("XFramework/Resource/AssetBundleWindow")]
        static void OpenWindow()
        {
            var window = GetWindow(typeof(AssetBundleEditor));
            window.titleContent = new GUIContent("AssetBundle");
            window.Show();
            window.minSize = new Vector2(400, 100);
        }

        private TabMode m_TabMode = TabMode.BuildProject;

        public SubWindow[] m_SubWindows = new SubWindow[]
        {
            new DefaultTab(),
            new DependenceTab(),
            new Mainfest2Json(),
            new BuildTab(),
        };

        private void OnEnable()
        {
            foreach (var item in m_SubWindows)
            {
                item.OnEnable();
            }
        } 

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    m_TabMode = (TabMode)GUILayout.Toolbar((int)m_TabMode, Enum.GetNames(typeof(TabMode)));
                }

                GUILayout.Space(10);
                m_SubWindows[(int)m_TabMode].OnGUI();
            }
        } 

        private void OnDisable()
        {
            foreach (var item in m_SubWindows)
            {
                item.OnDisable();
            }
        }

        #region 

        /// <summary>
        /// 标记文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="packOption"></param>
        private static void MarkDirectory(DirectoryInfo dirInfo, PackOption packOption, ref List<AssetBundleBuild> builds)
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
                        MarkDirectory(item, PackOption.AllFiles, ref builds);
                    }
                    break;
                case PackOption.AllDirectiony:
                    files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                    subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var item in subDirectory)
                    {
                        MarkDirectory(item, PackOption.AllDirectiony, ref builds);
                    }
                    break;
                case PackOption.TopFileOnly:
                    files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);        // 取出所有文件
                    break;
            }

            // 添加AssetBundleBuild
            List<string> assetNames = new List<string>();

            int total = files.Length;
            string abName = dirInfo.FullName.Substring(dirInfo.FullName.IndexOf("Assets", StringComparison.Ordinal));
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

        /// <summary>
        /// 将unity依赖转为自己的
        /// </summary>
        /// <param name="mainfest"></param>
        /// <returns></returns>
        private static DependenciesData GenerateDependence(AssetBundleManifest mainfest)
        {
            string[] abNames = mainfest.GetAllAssetBundles();

            List<SingleDepenciesData> singleDatas = new List<SingleDepenciesData>();

            for (int j = 0; j < abNames.Length; j++)
            {
                var dpNames = mainfest.GetDirectDependencies(abNames[j]);
                if (dpNames.Length <= 0)
                {
                    continue;
                }
                singleDatas.Add(new SingleDepenciesData(abNames[j], dpNames));
            }
            var data = new DependenciesData(singleDatas.ToArray());
            return data;
        }

        #endregion

    }
}