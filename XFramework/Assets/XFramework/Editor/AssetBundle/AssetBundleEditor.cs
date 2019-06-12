using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 打包 AssetBundle
/// </summary>
public class AssetBundleEditor : MonoBehaviour
{
    [MenuItem("Tools/BuildAssetBundle")]
    static void BuildAssetBundle()
    {
        string outputPath = Application.streamingAssetsPath + "/AssetBundles";
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // 强制删除所有AssetBundle名称  
        string[] abNames = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0; i < abNames.Length; i++)
        {
            AssetDatabase.RemoveAssetBundleName(abNames[i], true);
        }

        // 删除之前的ab文件
        FileInfo[] fs = new DirectoryInfo(outputPath).GetFiles("*", SearchOption.AllDirectories);
        foreach (var f in fs)
        {
            f.Delete();
        }

        //foreach (UnityEngine.Object selected in Selection.objects)
        //{
        //返回所有对象相对于工程目录的存储路径如 Assets/Scenes/Main.unity, 减去 Assets 6个字节
        FindMoudles(Application.dataPath + "/ResourcesAB");
        //}

        BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
        AssetDatabase.Refresh();

        print("BuildAssetBundles Complete");
    }

    /// <summary>
    /// 按模块组织
    /// </summary>
    /// <param name="path"></param>
    private static void FindMoudles(string path)
    {
        DirectoryInfo dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            Debug.LogError("无效路径");
            return;
        }

        foreach (var item in dir.GetDirectories("*", SearchOption.TopDirectoryOnly))
        {
            AssetImport(item, "");
        }
    }

    /// <summary>
    /// 标记某个文件夹下所有文件为文件夹名
    /// </summary>
    /// <param name="dirInfo">文件夹名</param>
    private static void AssetImport(DirectoryInfo dirInfo, string parentPath = "")
    {
        FileInfo[] files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);      // 取出第一层文件

        string selfPath = string.IsNullOrEmpty(parentPath) ? dirInfo.Name : parentPath + "/" + dirInfo.Name;

        foreach (FileInfo fileInfo in files)
        {
            if (fileInfo.Name.EndsWith(".mata")) continue;

            string filePath = fileInfo.FullName.Substring(fileInfo.FullName.IndexOf("Assets", StringComparison.Ordinal));       // 获取 "Assets"目录起的 文件名, 可不用转 "\\"
            AssetImporter importer = AssetImporter.GetAtPath(filePath);     // 拿到该文件的 AssetImporter
            if (importer && importer.assetBundleName != selfPath)
            {
                importer.assetBundleName = selfPath;
                importer.assetBundleVariant = "ab"; 
                importer.SaveAndReimport();
            }
        }

        // 递归设置下一层目录文件信息
        DirectoryInfo[] folders = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);

        // 递归的跳出条件
        if (folders == null || folders.Length == 0)
        {
            return;
        }

        foreach (DirectoryInfo dInfo in folders)
        {
            AssetImport(dInfo, selfPath);
        }
    }
}