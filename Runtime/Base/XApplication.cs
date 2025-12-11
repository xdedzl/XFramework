using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace XFramework
{
    public static class XApplication
    {
        public static readonly string dataPath;
        public static string cachePath
        {
            get
            {
                string path = $"{Directory.GetCurrentDirectory()}/Library/XFramework";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }
        
        public static string projectPath => Application.dataPath.Replace("Assets", "");

        public static string localDataPath => $"{dataPath}/LocalData";

        public static string configPath => $"{dataPath}/Configs";

        public static string streamingAssetsPath
        {
            get
            {
                return Application.platform == RuntimePlatform.Android ? Path.Combine(Application.persistentDataPath, "StreamingAssets") : Application.streamingAssetsPath;
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

        /// <summary>
        /// ��¼streamingAssets�µ��ļ�
        /// </summary>
        public static void RecordAllStremingAssetsPath()
        {
            string assetListPath = Path.Combine(Application.streamingAssetsPath, "assetList");
            if (File.Exists(assetListPath))
            {
                File.Delete(assetListPath);
            }
            StringBuilder stringBuilder = new StringBuilder();
            DirectoryInfo directoryInfo = new DirectoryInfo(Application.streamingAssetsPath);
            if (directoryInfo.Exists)
            {
                FileInfo[] fileInfos = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
                foreach (var file in fileInfos)
                {
                    string path = Utility.Text.GetAfterStr(file.FullName, "StreamingAssets\\", false);
                    if (path.EndsWith(".meta"))
                        continue;
                    stringBuilder.Append(path);
                    stringBuilder.Append('\n');
                }

                string assetListText = stringBuilder.ToString().Replace('\\', '/');
                if (!string.IsNullOrEmpty(assetListText))
                {
                    File.WriteAllText(assetListPath, assetListText);
                }
            }
        }

        /// <summary>
        /// 将streamingAssets中的文件copy到persistentDataPath中
        /// </summary>
        /// <param name="force"></param>
        public static void CopyToPersistentDataPath(bool force = false)
        {
            if (Directory.Exists(streamingAssetsPath) && !force)
            {
                return;
            }

            if (Directory.Exists(streamingAssetsPath))
                Directory.Delete(streamingAssetsPath, true);
            Directory.CreateDirectory(streamingAssetsPath);

            var uri = new System.Uri(Path.Combine(Application.streamingAssetsPath, "assetList"));
            UnityWebRequest request = UnityWebRequest.Get(uri);
            request.SendWebRequest();
            if (request.error == null)
            {
                while (true)
                {
                    if (request.downloadHandler.isDone)
                    {
                        string[] paths = request.downloadHandler.text.Trim().Split('\n');
                        foreach (var path in paths)
                        {
                            Debug.Log(path);
                            CopyToPersistentDataPath(path);
                        }
                        break;
                    }
                }
            }
        }

        private static void CopyToPersistentDataPath(string path)
        {
            var uri = new System.Uri(Path.Combine(Application.streamingAssetsPath, path));
            UnityWebRequest request = UnityWebRequest.Get(uri);
            request.SendWebRequest();
            if (request.error == null)
            {
                while (true)
                {
                    if (request.downloadHandler.isDone)
                    {
                        string filePath = Path.Combine(streamingAssetsPath, path);
                        string dirPath = Path.GetDirectoryName(filePath);

                        Debug.Log(filePath);
                        Debug.Log(dirPath);
                        Debug.Log(Path.GetFileName(filePath));
                        Debug.Log("");

                        if (!Directory.Exists(dirPath))
                            Directory.CreateDirectory(dirPath);
                        File.WriteAllBytes(filePath, request.downloadHandler.data);
                        break;
                    }
                }
            }
        }
    }
}
