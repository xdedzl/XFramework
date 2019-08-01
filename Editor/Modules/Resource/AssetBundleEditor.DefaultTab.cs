﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace XFramework.Editor
{
    public partial class AssetBundleEditor
    {
        private class DefaultTab : SubWindow
        {
            private enum PackOption
            {
                AllFiles,       // 所有文件一个包 
                TopDirectiony,  // 一级子文件夹单独打包
                AllDirectiony,  // 所有子文件夹单独打包
                TopFileOnly,    // 只打包当前文件夹的文件
            }

            private class BuildData
            {
                public string path;
                public PackOption option;
                public AssetBundleBuild build;
            }

            private List<BuildData> m_BuildDatas;

            private List<string> m_Paths;
            private List<PackOption> m_Options;
            private List<AssetBundleBuild> m_Builds;

            private string OutPutPath;
            private BuildAssetBundleOptions buildAssetBundleOption;

            private bool m_IsShowAB;

            public override void OnEnable()
            {
                m_BuildDatas = new List<BuildData>();
                m_Paths = new List<string>();
                m_Options = new List<PackOption>();
                m_Builds = new List<AssetBundleBuild>();
                OutPutPath = Application.streamingAssetsPath + "/AssetBundles";

                m_IsShowAB = false;
            }

            public override void OnGUI()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 菜单栏
                        MenuBar();
                    }

                    using (new EditorGUILayout.VerticalScope())
                    {
                        for (int i = 0; i < m_Paths.Count; i++)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                m_Paths[i] = EditorGUILayout.TextField("", m_Paths[i]);
                                m_Options[i] = (PackOption)EditorGUILayout.EnumPopup(m_Options[i]);
                                if (GUILayout.Button(EditorIcon.Trash))
                                {
                                    m_Paths.RemoveAt(i);
                                    m_Options.RemoveAt(i);
                                }
                            }
                        }

                        ABPreview();
                    }

                    GUILayout.Space(10);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 底边栏
                        BottomMenu();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("输出路径");
                        GUILayout.TextField(OutPutPath);
                        if (GUILayout.Button(EditorIcon.Folder))
                        {
                            string temp = EditorUtility.OpenFolderPanel("输出文件夹", Application.streamingAssetsPath, "");
                            if (!string.IsNullOrEmpty(temp))
                            {
                                OutPutPath = temp;
                            }
                        }
                        buildAssetBundleOption = (BuildAssetBundleOptions)EditorGUILayout.EnumPopup(buildAssetBundleOption);
                    }
                }
            }

            // 菜单栏
            private void MenuBar()
            {
                if (GUILayout.Button("添加文件夹"))
                {
                    if (Selection.objects != null)
                    {
                        foreach (var item in Selection.objects)
                        {
                            m_BuildDatas.Add(new BuildData()
                            {
                                path = AssetDatabase.GetAssetPath(item),
                                option = PackOption.AllFiles,
                            });

                            m_Paths.Add(AssetDatabase.GetAssetPath(item));
                            m_Options.Add(PackOption.AllFiles);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("请选择要添加的文件夹");
                    }
                }
                m_IsShowAB = GUILayout.Toggle(m_IsShowAB, "显示Ab");
            }

            // AB包预览
            Vector2 m_ScrollPos;
            bool[] isOns = new bool[100];
            private void ABPreview()
            {
                if (m_IsShowAB)
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(m_ScrollPos/*, GUILayout.Height(200)*/))
                    {
                        for (int i = 0; i < m_Builds.Count; i++)
                        {
                            m_ScrollPos = scroll.scrollPosition;
                            isOns[i] = EditorGUILayout.Toggle(m_Builds[i].assetBundleName + "." + m_Builds[i].assetBundleVariant, isOns[i]);
                            if (isOns[i])
                            {
                                foreach (var assetName in m_Builds[i].assetNames)
                                {
                                    GUILayout.Label(assetName);
                                }
                            }
                        }
                    }
                }
            }

            // 底边栏
            private void BottomMenu()
            {
                if (GUILayout.Button("删除AB包"))
                {
                    if (!Directory.Exists(OutPutPath))
                    {
                        // 删除之前的ab文件
                        FileInfo[] fs = new DirectoryInfo(OutPutPath).GetFiles("*", SearchOption.AllDirectories);
                        foreach (var f in fs)
                        {
                            f.Delete();
                        }
                    }
                }

                if (GUILayout.Button("标记"))
                {
                    m_Builds.Clear();

                    for (int i = 0; i < m_Paths.Count; i++)
                    {
                        DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + m_Paths[i]);
                        MarkDirectory(info, m_Options[i]);
                    }
                }

                if (GUILayout.Button("打包"))
                {
                    if (!Directory.Exists(OutPutPath))
                    {
                        Directory.CreateDirectory(OutPutPath);
                    }
                    BuildPipeline.BuildAssetBundles(OutPutPath, m_Builds.ToArray(), buildAssetBundleOption, EditorUserBuildSettings.activeBuildTarget);
                    AssetDatabase.Refresh();
                    Debug.Log("BuildAssetBundles Complete");
                }
            }

            /// <summary>
            /// 标记文件
            /// </summary>
            /// <param name="path"></param>
            /// <param name="packOption"></param>
            private void MarkDirectory(DirectoryInfo dirInfo, PackOption packOption)
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
                            MarkDirectory(item, PackOption.AllFiles);
                        }
                        break;
                    case PackOption.AllDirectiony:
                        files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件
                        subDirectory = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                        foreach (var item in subDirectory)
                        {
                            MarkDirectory(item, PackOption.AllDirectiony);
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

                if(assetNames.Count > 0)
                {
                    m_Builds.Add(new AssetBundleBuild()
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
}