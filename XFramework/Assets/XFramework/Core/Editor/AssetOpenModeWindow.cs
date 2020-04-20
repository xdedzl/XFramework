using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace XFramework.Editor
{
    public class AssetOpenModeWindow : EditorWindow
    {
        private AssetOpenModeWindow m_Window;

        private static Dictionary<string, string> m_fileOpenType = new Dictionary<string, string>();

        private void OnEnable()
        {
            string keysJson = EditorPrefs.GetString("keys");
            string valuesJson = EditorPrefs.GetString("values");

            string[] keys = JsonUtility.FromJson<string[]>(keysJson);
            string[] values = JsonUtility.FromJson<string[]>(valuesJson);

            if(keys != null && values != null && keys.Length == values.Length)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    m_fileOpenType.Add(keys[i], values[i]);
                }
            }
        }

        private void OnDisable()
        {
            string keysJson = JsonUtility.ToJson(m_fileOpenType.Keys.ToArray());
            string valuesJson = JsonUtility.ToJson(m_fileOpenType.Values.ToArray());

            EditorPrefs.SetString("keys", keysJson);
            EditorPrefs.SetString("values", valuesJson);
        }

        private string inputFileType;

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    inputFileType = EditorGUILayout.TextField("文件类型", inputFileType);
                    if (GUILayout.Button(EditorIcon.Plus))
                    {
                        if (string.IsNullOrEmpty(inputFileType))
                        {
                            EditorUtility.DisplayDialog("提示", "请输入文件后缀名", "确定");
                        }
                        else if(m_fileOpenType.ContainsKey(inputFileType))
                        {
                            EditorUtility.DisplayDialog("提示", "请勿重复添加", "确定");
                        }
                        else
                        {
                            m_fileOpenType.Add(inputFileType, "");
                        }
                    }
                }

                using(new EditorGUILayout.VerticalScope("box"))
                {
                    string toRemoveKey = "";
                    string toChangeKey = "";
                    string toChangeValue = "";
                    foreach (var item in m_fileOpenType)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(item.Key, item.Value);

                            if (GUILayout.Button(EditorIcon.Folder))
                            {
                                string file = EditorUtility.OpenFilePanel("选择exe", item.Value, "exe");

                                if (!string.IsNullOrEmpty(file))
                                {
                                    toChangeKey = item.Key;
                                    toChangeValue = file;
                                    
                                }
                            }
                            if (GUILayout.Button(EditorIcon.Trash))
                            {
                                toRemoveKey = item.Key;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(toChangeKey))
                    {
                        m_fileOpenType[toChangeKey] = toChangeValue;
                    }
                    m_fileOpenType.Remove(toRemoveKey);
                }
            }
        }

        [MenuItem("XFramework/Asset/OpenMode")]
        private static void OpenWidown()
        {
            GetWindow(typeof(AssetOpenModeWindow));
        }

        [OnOpenAsset(1)]
        public static bool OpenAsset(int instanceID, int line)
        {
            string fileFullPath = System.IO.Directory.GetParent(Application.dataPath) + "/" + AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(instanceID));

            foreach (var item in m_fileOpenType)
            {
                if (fileFullPath.EndsWith(item.Key))
                {
                    OpenFileWithExe(item.Value, fileFullPath);
                    return true;
                }
            }

            return false;
        }

        private static void OpenFileWithExe(string filePath, string exePath)
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = exePath;
            startInfo.Arguments = "\"" + filePath + "\"";
            process.StartInfo = startInfo;
            process.Start();
            process.Dispose();
        }
    }
}