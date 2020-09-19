using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace XFramework.Resource
{
    public partial class ResourceManager
    {
        public static void GeneratePathMap(string outPath, params string[] assetPaths)
        {
            Utility.IO.CreateFile(outPath);
            if (File.Exists(outPath))
            {
                Dictionary<string, string> pathMap = new Dictionary<string, string>();
                foreach (var assetPath in assetPaths)
                {
                    ConfigPathMap(assetPath, pathMap);
                }
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var item in pathMap)
                {
                    stringBuilder.Append(item.Key);
                    stringBuilder.Append(':');
                    stringBuilder.Append(item.Value);
                    stringBuilder.Append('\n');
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);

                File.WriteAllText(outPath, stringBuilder.ToString());
            }
        }

        private static void ConfigPathMap(string path, Dictionary<string, string> pathMap)
        {
            DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "") + path);
            foreach (var item in info.GetFiles("*", SearchOption.AllDirectories))
            {
                var fullName = item.FullName;
                if (item.Name.EndsWith(".meta"))
                    continue;

                int startIndex = fullName.IndexOf("Assets");
                string assetPath = fullName.Substring(startIndex);

                if (pathMap.ContainsKey(item.Name))
                {
                    throw new XFrameworkException($"tow asset have same name, name is {item.Name}");
                }
                pathMap.Add(item.Name, assetPath);
            }
        }
    }
}