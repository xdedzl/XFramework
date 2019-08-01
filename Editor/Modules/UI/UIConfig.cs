using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace XFramework.Editor
{
    public class UIConfig
    {
        [MenuItem("XFramework/UI/CreateConfig")]
        public static void CreateConfig()
        {
            UIPathConfig config = ScriptableObject.CreateInstance<UIPathConfig>();

            string relPath = "Assets/Resources";
            string path = Application.dataPath.Replace("Assets", "") + relPath;
            Debug.Log(path);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            AssetDatabase.CreateAsset(config, relPath + "/UIPathConfig.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}