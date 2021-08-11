using System.IO;
using UnityEngine;

namespace XFramework
{
    public static class XApplication
    {
        public static readonly string dataPath;
        public static string CachePath
        {
            get
            {
                string path = $"{Directory.GetCurrentDirectory()}/Library/XFramework";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        public static string localDataPath
        {
            get
            {
                return $"{dataPath}/LocalData";
            }
        }

        public static string configPath 
        {
            get
            {
                return $"{dataPath}/Configs";
            }
        }

        static XApplication()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
            {
                dataPath = System.Environment.CurrentDirectory;
            }
            else
            {
                dataPath = Application.persistentDataPath;
            }
        }
    }
}
