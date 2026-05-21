using System;
using UnityEngine;
using System.Reflection;
using XFramework.Json;
using XFramework.Resource;

namespace XFramework.Data
{
    public partial class XDataTable
    {
#if UNITY_EDITOR
        [UnityEditor.MenuItem("XFramework/Data/Create Data Assets")]
        public static void CreateMissingDataAssets()
        {
            XJson.SetUnityDefaultSetting();
            var types = Utility.Reflection.GetGenericTypes(typeof(XDataTable<>), 5, "Assembly-CSharp", "XFrameworkRuntime");
            var createdCount = 0;
            foreach (var tableType in types)
            {
                var dataResourcePathAttr = tableType.GetCustomAttribute<DataResourcePath>(false);
                if (dataResourcePathAttr == null)
                {
                    continue;
                }

                foreach (string path in dataResourcePathAttr.GetPaths())
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }
                
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null)
                    {
                        continue;
                    }
                
                    // Create directory if not exists
                    var directory = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                    {
                        System.IO.Directory.CreateDirectory(directory);
                        UnityEditor.AssetDatabase.Refresh();
                    }

                    var instance = (XTextAsset)Activator.CreateInstance(tableType);
                    instance.SetAssetPath(path);
                    var json = instance.Serialize();
                    System.IO.File.WriteAllText(path, json);
                    UnityEditor.AssetDatabase.Refresh();
                        
                    asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    Debug.Log($"[DataManager] Created missing JSON asset at {path} for type {tableType.Name}", asset);
                    createdCount++;
                }
            }
            
            if (createdCount > 0)
            {
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
                Debug.Log($"[DataManager] Total created {createdCount} assets.");
            }
            else
            {
                Debug.Log("[DataManager] All data assets already exist.");
            }
        }
#endif
    }
}

