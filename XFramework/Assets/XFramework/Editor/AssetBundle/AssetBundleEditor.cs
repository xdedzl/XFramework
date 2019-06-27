using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 打包 AssetBundle
/// </summary>
public class AssetBundleEditor : EditorWindow
{
    public enum PackOption
    {
        AllFiles,       // 所有文件一个包 
        TopDirectiony,  // 一级子文件夹单独打包
        AllDirectiony,  // 所有子文件夹单独打包
    }

    [MenuItem("XFramework/ABWindiow")]
    static void BuildAssetBundle()
    {
        GetWindow(typeof(AssetBundleEditor)).Show();
    }


    private List<string> m_Paths;
    private List<PackOption> m_Options;
    private string OutPutPath;

    private void Awake()
    {
        m_Paths = new List<string>();
        m_Options = new List<PackOption>();
        OutPutPath = Application.streamingAssetsPath + "/AssetBundles";
    }

    private void OnGUI()
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
                        if (GUILayout.Button("删除"))
                        {
                            m_Paths.RemoveAt(i);
                            m_Options.RemoveAt(i);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // 底边栏
                BottomMenu();
            }
        }
    }

    // 菜单栏
    private void MenuBar()
    {
        if (GUILayout.Button("添加AB包"))
        {
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath("Assets/Terrains"))
            {
                Debug.Log(item);
            }

            if (Selection.objects != null)
            {
                foreach (var item in Selection.objects)
                {
                    m_Paths.Add(AssetDatabase.GetAssetPath(item));
                    m_Options.Add(PackOption.AllFiles);
                }
            }
            else
            {
                Debug.LogWarning("请选择要添加的文件夹");
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
            // 强制删除所有AssetBundle名称  
            string[] abNames = AssetDatabase.GetAllAssetBundleNames();
            for (int i = 0; i < abNames.Length; i++)
            {
                AssetDatabase.RemoveAssetBundleName(abNames[i], true);
            }

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
            BuildPipeline.BuildAssetBundles(OutPutPath, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
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
        DirectoryInfo[] subDirectory = null;
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
        }



        // 标记
        int total = files.Length;
        string abName = dirInfo.FullName.Substring(dirInfo.FullName.IndexOf("Assets", StringComparison.Ordinal));
        for (int i = 0; i < total; i++)
        {
            var fileInfo = files[i];

            if (fileInfo.Name.EndsWith(".mata")) continue;

            EditorUtility.DisplayProgressBar(dirInfo.Name, $"正在标记资源{fileInfo.Name}...", (float)i / total);

            string filePath = fileInfo.FullName.Substring(fileInfo.FullName.IndexOf("Assets", StringComparison.Ordinal));       // 获取 "Assets"目录起的 文件名, 可不用转 "\\"

            AssetImporter importer = AssetImporter.GetAtPath(filePath);     // 拿到该文件的 AssetImporter
            if (importer)
            {
                importer.assetBundleName = abName;
                importer.assetBundleVariant = "ab";
                //importer.SaveAndReimport();
            }
        }
        EditorUtility.ClearProgressBar();
    }
}