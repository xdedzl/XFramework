using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Xml;
using System.IO;
using System.Xml.Linq;

namespace XFramework.Editor
{
    public class AssetOpenModeWindow : EditorWindow
    {
        private readonly static string FILE_PATH = $"{XApplication.cachePath}/FileOpenInfo.xml";

        private static Dictionary<string, string> s_fileOpenType;

        private static Dictionary<string, string> FileOpenType
        {
            get
            {
                if (s_fileOpenType == null)
                {
                    s_fileOpenType = new Dictionary<string, string>();
                    if (File.Exists(FILE_PATH))
                    {
                        XElement root = XElement.Load(FILE_PATH);
                        foreach (var item in root.Elements("FileInfo"))
                        {
                            s_fileOpenType.Add(item.Element("suffix").Value, item.Element("applink").Value);
                        }
                    }
                }
                return s_fileOpenType;
            }
        }

        private void OnEnable()
        {
            
        }

        private void OnDisable()
        {
            XmlDocument xmlDoc = new XmlDocument();
            //创建类型声明节点  
            XmlNode node = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", "");
            xmlDoc.AppendChild(node);
            //创建根节点  
            XmlNode root = xmlDoc.CreateElement("Root");
            xmlDoc.AppendChild(root);

            foreach (var item in FileOpenType)
            {
                XmlNode fileInfoNode = xmlDoc.CreateElement("FileInfo");
                root.AppendChild(fileInfoNode);
                CreateNode(xmlDoc, fileInfoNode, "suffix", item.Key);
                CreateNode(xmlDoc, fileInfoNode, "applink", item.Value);
            }

            xmlDoc.Save(FILE_PATH);
        }

        /// <summary>    
        /// 创建节点    
        /// </summary>    
        /// <param name="xmldoc"></param>  xml文档  
        /// <param name="parentnode"></param>父节点    
        /// <param name="name"></param>  节点名  
        /// <param name="value"></param>  节点值  
        ///   
        public void CreateNode(XmlDocument xmlDoc, XmlNode parentNode, string name, string value)
        {
            XmlNode node = xmlDoc.CreateNode(XmlNodeType.Element, name, null);
            node.InnerText = value;
            parentNode.AppendChild(node);
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
                        else if(EditorPrefs.HasKey(inputFileType))
                        {
                            EditorUtility.DisplayDialog("提示", "请勿重复添加", "确定");
                        }
                        else
                        {
                            FileOpenType.Add(inputFileType, "");
                        }
                    }
                }

                using(new EditorGUILayout.VerticalScope("box"))
                {
                    string toRemoveKey = "";
                    string toChangeKey = "";
                    string toChangeValue = "";
                    foreach (var item in FileOpenType)
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
                        FileOpenType[toChangeKey] = toChangeValue;
                    }
                    FileOpenType.Remove(toRemoveKey);
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

            var strs = fileFullPath.Split('.');

            if (strs.Length > 1)
            {
                if(FileOpenType.TryGetValue(strs.End(), out string openType))
                {
                    OpenFileWithExe(openType, fileFullPath);
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