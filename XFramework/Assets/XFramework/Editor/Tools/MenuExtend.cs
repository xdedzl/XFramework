using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework;
using System.Collections.Generic;

namespace XFramework.Editor
{
    /// <summary>
    /// 菜单栏扩展
    /// </summary>
    public static class MenuExtend
    {
        [MenuItem("GameObject/CreateParent", priority = 0)]
        public static void CreateParent()
        {
            Vector3 avg_pos = Vector3.zero;
            foreach (var trans in Selection.transforms)
            {
                avg_pos += trans.position;
            }
            avg_pos /= Selection.transforms.Length;

            Transform parent = new GameObject("new parent").transform;
            parent.position = avg_pos;

            foreach (var trans in Selection.transforms)
            {
                trans.SetParent(parent, true);
            }
        }

        [MenuItem("XFramework/GenerateScriptsGUIDFile")]
        public static void GenerateScriptsGUIDFile()
        {

            string fullPath = Path.Combine(XApplication.dataPath, "Assets", "XFramework");

            Dictionary<string, string> infos = new Dictionary<string, string>();
            foreach (var item in Utility.IO.Foreach(fullPath))
            {
                if (item.FullName.EndsWith(".cs"))
                {
                    string metaPath = item.FullName + ".meta";
                    foreach (string line in File.ReadAllLines(metaPath))
                    {
                        var values = line.Split(':');
                        if (values.Length == 2 && values[0].Trim() == "guid")
                        {
                            infos[item.Name] = values[1].Trim();
                            break;
                        }
                    } 
                }
            }
            List<string> file2guid = new List<string>();
            foreach (var item in infos)
            {
                file2guid.Add(item.Key + ":" + item.Value);
            }
            string savePath = Path.Combine(XApplication.dataPath, "ProjectSettings", "ScriptsGuid.txt");
            File.WriteAllLines(savePath, file2guid);
        }

        [MenuItem("XFramework/UpdatePrefabScriptGUID")]
        public static void UpdatePrefabScriptGUID()
        {

        }
    }
}
