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
            private class BuildData
            {
                public string path;
                public PackOption option;
                public AssetBundleBuild build;
            }

            private List<BuildData> m_BuildDatas;

            private List<AssetBundleBuild> m_Builds;

            private string m_OutPutPath;
            private BuildAssetBundleOptions buildAssetBundleOption;

            public override void OnEnable()
            {
                m_BuildDatas = new List<BuildData>();
                m_Builds = new List<AssetBundleBuild>();
                m_OutPutPath = EditorPrefs.GetString("ABOutPutPath", Application.streamingAssetsPath + "/AssetBundles");
            }

            public override void OnDisable()
            {
                EditorPrefs.SetString("ABOutPutPath", m_OutPutPath);
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
                        for (int i = 0; i < m_BuildDatas.Count; i++)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button(EditorIcon.Folder))
                                {
                                    string temp = EditorUtility.OpenFolderPanel("要打包的文件夹", Application.dataPath, "");

                                    if (!string.IsNullOrEmpty(temp))
                                    {
                                        int index = temp.IndexOf("Assets");
                                        if (index == -1)
                                        {
                                            Debug.LogError("选择的AB包文件夹必须在Assets文件夹下");
                                        }
                                        temp = temp.Substring(index, temp.Length - index);
                                        m_BuildDatas[i].path = temp;
                                    }
                                }

                                GUILayout.TextField(m_BuildDatas[i].path);
                                m_BuildDatas[i].option = (PackOption)EditorGUILayout.EnumPopup(m_BuildDatas[i].option);
                                if (GUILayout.Button(EditorIcon.Trash))
                                {
                                    m_BuildDatas.RemoveAt(i);
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
                        GUILayout.TextField(m_OutPutPath);
                        if (GUILayout.Button(EditorIcon.Folder))
                        {
                            string temp = EditorUtility.OpenFolderPanel("输出文件夹", Application.streamingAssetsPath, "");
                            if (!string.IsNullOrEmpty(temp))
                            {
                                m_OutPutPath = temp;
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
                    if (Selection.objects != null && Selection.objects.Length > 0)
                    {
                        foreach (var item in Selection.objects)
                        {
                            m_BuildDatas.Add(new BuildData()
                            {
                                path = AssetDatabase.GetAssetPath(item),
                                option = PackOption.AllFiles,
                            });
                        }
                    }
                    else
                    {
                        m_BuildDatas.Add(new BuildData()
                        {
                            path = "",
                            option = PackOption.AllFiles,
                        });
                    }
                }



                if (GUILayout.Button("刷新AssetBundleBuild"))
                {
                    m_Builds.Clear();

                    for (int i = 0; i < m_BuildDatas.Count; i++)
                    {
                        DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + m_BuildDatas[i].path);
                        MarkDirectory(info, m_BuildDatas[i].option);
                    }
                }
            }

            // AB包预览
            Vector2 m_ScrollPos;
            bool[] isOns = new bool[100];
            private void ABPreview()
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(m_ScrollPos))
                {
                    m_ScrollPos = scroll.scrollPosition;

                    for (int i = 0; i < m_Builds.Count; i++)
                    {
                        GUILayout.BeginVertical("box");
                        isOns[i] = EditorGUILayout.Toggle(m_Builds[i].assetBundleName + "." + m_Builds[i].assetBundleVariant, isOns[i]);
                        if (isOns[i])
                        {
                            foreach (var assetName in m_Builds[i].assetNames)
                            {
                                GUILayout.Label(assetName);
                            }
                        }
                        GUILayout.EndVertical();
                    }
                }
            }

            // 底边栏
            private void BottomMenu()
            {
                if (GUILayout.Button("删除AB包"))
                {
                    if (!Directory.Exists(m_OutPutPath))
                    {
                        // 删除之前的ab文件
                        FileInfo[] fs = new DirectoryInfo(m_OutPutPath).GetFiles("*", SearchOption.AllDirectories);
                        foreach (var f in fs)
                        {
                            f.Delete();
                        }
                    }
                }

                if (GUILayout.Button("打包"))
                {
                    if (!Directory.Exists(m_OutPutPath))
                    {
                        Directory.CreateDirectory(m_OutPutPath);
                    }
                    BuildPipeline.BuildAssetBundles(m_OutPutPath, m_Builds.ToArray(), buildAssetBundleOption, EditorUserBuildSettings.activeBuildTarget);
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