using UnityEngine;
using UnityEditor;
using System.IO;

public class ImporterSettingWindow : EditorWindow
{

    private string filePath = "";
    private int size = 256;


    private void OnGUI()
    {
        GUILayout.BeginVertical();

        GUILayout.Space(10);
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUILayout.Label("Importer Setting");
        GUILayout.Space(10);

        GUILayout.MaxWidth(100);
        filePath = EditorGUILayout.TextField("路径", filePath);
        size = EditorGUILayout.IntField("MaxSize", size);

        UnityEngine.Object[] objs = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);
        if (objs != null && objs.Length > 0)
        { 
            filePath = AssetDatabase.GetAssetPath(objs[0]);
            filePath = filePath.Replace("Assets/", "");
        }

        if (GUILayout.Button("ChangeTextureMaxSize"))
        {
            ChangeTextureMaxSize(filePath, size);
        }

        GUILayout.EndVertical();
    }


    [MenuItem("XFramework/AssetImporter/ImporterSettingWindow")]
    public static void ImporterSet()
    {
        EditorWindow.GetWindow(typeof(ImporterSettingWindow));
    }

    /// <summary>
    /// 改变文件下所有的TextureImporter的MaxSize
    /// </summary>
    public static void ChangeTextureMaxSize(string filePath, int maxSize)
    {
        string[] paths = Directory.GetFiles(Application.dataPath + "/" + filePath, "*", SearchOption.AllDirectories);

        for (int i = 0,length = paths.Length; i < length; i++)
        {
            string tempPath = paths[i].Replace(@"\", "/");
            tempPath = tempPath.Substring(tempPath.IndexOf("Assets"));
            TextureImporter tex = AssetImporter.GetAtPath(tempPath) as TextureImporter;           // 获取对应路径的贴图设置
            EditorUtility.DisplayProgressBar("ChangeTerture", "progress", (float)(i+1)/length);   // 进度条显示
            if (tex != null)
            {
                tex.maxTextureSize = maxSize;
            }
        }
        EditorUtility.ClearProgressBar();   // 修改完成后结束进度条显示
    }
}
