using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace XFramework.Editor
{
    public partial class AssetBundleEditor
    {
        private class BuildTab : SubWindow
        {
            private const float titleW = 80;
            private const float textFieldW = 250;


            private string outPutPath;
            private string abResPath;
            private string abRelaPath;

            private bool isBuildAB;
            private bool isDevelopment;

            public override void OnEnable()
            {
                outPutPath = EditorPrefs.GetString("build_OutPut");
                abResPath = EditorPrefs.GetString("build_ABRes", "Assets/ABRes");
                abRelaPath = EditorPrefs.GetString("build_ABPath", "DSTData/AssetBundles");
            }

            public override void OnDisable()
            {
                EditorPrefs.SetString("build_OutPut", outPutPath);
                EditorPrefs.SetString("build_ABRes", abResPath);
                EditorPrefs.SetString("build_ABPath", abRelaPath);
            }

            public override void OnGUI()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("输出路径", GUILayout.Width(titleW)); ;
                        GUILayout.TextField(outPutPath, GUILayout.Width(textFieldW));

                        if (GUILayout.Button(EditorIcon.Folder))
                        {
                            string temp = EditorUtility.OpenFolderPanel("输出路径", Application.dataPath, "");

                            if (!string.IsNullOrEmpty(temp))
                            {
                                int index = temp.IndexOf("Assets");
                                outPutPath = temp;
                            }
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("AB资源根目录", GUILayout.Width(titleW)); ;
                        GUILayout.TextField(abResPath, GUILayout.Width(textFieldW));

                        if (GUILayout.Button(EditorIcon.Folder))
                        {
                            string temp = EditorUtility.OpenFolderPanel("AB资源根目录", Application.dataPath, "");

                            if (!string.IsNullOrEmpty(temp))
                            {
                                int index = temp.IndexOf("Assets");
                                if (index == -1)
                                {
                                    Debug.LogError("选择的AB包文件夹必须在Assets文件夹下");
                                }
                                temp = temp.Substring(index, temp.Length - index);
                                abResPath = temp;
                            }
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("AB包相对路径", GUILayout.Width(titleW));
                        abRelaPath = GUILayout.TextField(abRelaPath, GUILayout.Width(textFieldW));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("是否打包AB", GUILayout.Width(titleW));
                        isBuildAB = EditorGUILayout.Toggle(isBuildAB);

                        GUILayout.Label("是否为Development", GUILayout.Width(titleW));
                        isDevelopment = EditorGUILayout.Toggle(isDevelopment);

                        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
                        if (GUILayout.Button("一键打包", GUILayout.Width(60)))
                        {
                            if (isBuildAB)
                            {
                                string abOutPath = outPutPath + "/" + abRelaPath;
                                if (!Directory.Exists(abOutPath))
                                {
                                    Directory.CreateDirectory(abOutPath);
                                }

                                DirectoryInfo abResInfo = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + abResPath);
                                builds = AssetBundleUtility.MarkDirectory(abResInfo, PackOption.AllDirectiony);

                                BuildPipeline.BuildAssetBundles(abOutPath, builds.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
                                AssetDatabase.Refresh();
                                Debug.Log("BuildAssetBundles Complete");

                                string dependenctAb = Utility.Text.SplitPathName(abOutPath)[1];
                                AssetBundle mainfestAB = AssetBundle.LoadFromFile(abOutPath + "/" + dependenctAb);
                                var mainfest = mainfestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                                var dependence = GenerateDependence(mainfest);

                                string json = JsonUtility.ToJson(dependence, true);
                                File.WriteAllText(abOutPath + "/depenencies.json", json);
                            }

                            var buildScenes = EditorBuildSettings.scenes;

                            string exeName = Utility.Text.SplitPathName(System.Environment.CurrentDirectory)[1] + ".exe";


                            BuildOptions buildOptions = BuildOptions.None;

                            if (isDevelopment)
                            {
                                buildOptions |= BuildOptions.Development;
                            }

                            BuildPipeline.BuildPlayer(buildScenes, outPutPath + "/" + exeName, EditorUserBuildSettings.activeBuildTarget, buildOptions);
                            Application.OpenURL(outPutPath);
                            Debug.Log("Build Complete");
                        }
                    }
                }
            }
        }
    }
}