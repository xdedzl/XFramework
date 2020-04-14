using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.Editor
{
    public partial class AssetBundleEditor
    {
        private class DefaultTab : SubWindow
        {
            private class BuildData
            {
                public string path;
                public PackOption option;
#pragma warning disable 0649
                public AssetBundleBuild build;
            }

            private List<BuildData> m_BuildDatas;

            private List<AssetBundleBuild> m_Builds;

            private string m_OutPutPath;
            private BuildAssetBundleOptions m_buildAssetBundleOption;
            /// <summary>
            /// 是否显示ab内容
            /// </summary>
            //private bool isShowPreview;
            /// <summary>
            /// 是否为增量打包
            /// </summary>
            private bool m_incrementalPackaging;

            public override void OnEnable()
            {
                m_BuildDatas = new List<BuildData>();
                m_Builds = new List<AssetBundleBuild>();

                m_OutPutPath = EditorPrefs.GetString("ABOutPutPath", Application.streamingAssetsPath + "/AssetBundles");
                m_buildAssetBundleOption = (BuildAssetBundleOptions)EditorPrefs.GetInt("BuildAssetBundleOptions", (int)BuildAssetBundleOptions.None);
                m_incrementalPackaging = EditorPrefs.GetBool("IncrementalPackaging", false);
            }

            public override void OnDisable()
            {
                EditorPrefs.SetString("ABOutPutPath", m_OutPutPath);
                EditorPrefs.SetInt("BuildAssetBundleOptions", (int)m_buildAssetBundleOption);
                EditorPrefs.SetBool("IncrementalPackaging", m_incrementalPackaging);
            }

            public override void OnGUI()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 菜单栏
                        MenuBar();
                    }

                    using (new EditorGUILayout.VerticalScope())
                    {
                        for (int i = 0; i < m_BuildDatas.Count; i++)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                if (GUILayout.Button(EditorIcon.Folder))
                                {
                                    string temp = EditorUtility.OpenFolderPanel("要打包的文件夹", Application.dataPath, "");

                                    if (!string.IsNullOrEmpty(temp))
                                    {
                                        int index = temp.IndexOf("Assets");
                                        if (index == -1)
                                        {
                                            Debug.LogError("选择的AB包文件夹必须在Assets文件夹下");
                                        }
                                        temp = temp.Substring(index, temp.Length - index);
                                        m_BuildDatas[i].path = temp;
                                    }
                                }

                                GUILayout.TextField(m_BuildDatas[i].path);
                                m_BuildDatas[i].option = (PackOption)EditorGUILayout.EnumPopup(m_BuildDatas[i].option);
                                if (GUILayout.Button(EditorIcon.Trash))
                                {
                                    m_BuildDatas.RemoveAt(i);
                                }
                            }
                        }

                        ABPreview();
                    }

                    GUILayout.Space(10);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("输出路径");
                        GUILayout.TextField(m_OutPutPath);
                        if (GUILayout.Button(EditorIcon.Folder))
                        {
                            string temp = EditorUtility.OpenFolderPanel("输出文件夹", Application.streamingAssetsPath, "");
                            if (!string.IsNullOrEmpty(temp))
                            {
                                m_OutPutPath = temp;
                            }
                        }
                        m_buildAssetBundleOption = (BuildAssetBundleOptions)EditorGUILayout.EnumPopup(m_buildAssetBundleOption);
                        m_incrementalPackaging = GUILayout.Toggle(m_incrementalPackaging, "增量包");
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        // 底边栏
                        BottomMenu();
                    }
                }
            }

            // 菜单栏
            private void MenuBar()
            {
                if (GUILayout.Button("添加文件夹"))
                {
                    if (Selection.objects != null && Selection.objects.Length > 0)
                    {
                        foreach (var item in Selection.objects)
                        {
                            m_BuildDatas.Add(new BuildData()
                            {
                                path = AssetDatabase.GetAssetPath(item),
                                option = PackOption.AllDirectiony,
                            });
                        }
                    }
                    else
                    {
                        m_BuildDatas.Add(new BuildData()
                        {
                            path = "",
                            option = PackOption.AllDirectiony,
                        });
                    }
                }

                if (GUILayout.Button("刷新预览"))
                {
                    RefreshAssetBundleBuild();
                }
            }

            // AB包预览
            Vector2 m_ScrollPos;
            bool[] isOns = new bool[100];
            private void ABPreview()
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(m_ScrollPos))
                {
                    m_ScrollPos = scroll.scrollPosition;

                    for (int i = 0; i < m_Builds.Count; i++)
                    {
                        GUILayout.BeginVertical("box");
                        isOns[i] = EditorGUILayout.Toggle(m_Builds[i].assetBundleName + "." + m_Builds[i].assetBundleVariant, isOns[i]);
                        if (isOns[i])
                        {
                            foreach (var assetName in m_Builds[i].assetNames)
                            {
                                GUILayout.Label(assetName);
                            }
                        }
                        GUILayout.EndVertical();
                    }
                }
            }

            // 底边栏
            private void BottomMenu()
            {
                if (GUILayout.Button("清空输出目录"))
                {
                    if (EditorUtility.DisplayDialog("警告", "是否要删除输出目录下的所有文件", "确认", "取消"))
                    {
                        Utility.IO.CleraDirectory(m_OutPutPath);
                    }
                }

                if (GUILayout.Button("打包"))
                {
                    Build();
                }
            }

            // 刷新要打包的ab包内容
            private void RefreshAssetBundleBuild()
            {
                m_Builds.Clear();

                for (int i = 0; i < m_BuildDatas.Count; i++)
                {
                    DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + m_BuildDatas[i].path);
                    var tempBuilds = AssetBundleUtility.MarkDirectory(info, m_BuildDatas[i].option);
                    m_Builds.AddRange(tempBuilds);
                }
            }

            // 打包
            private void Build()
            {
                RefreshAssetBundleBuild();

                DependenciesData dependence;

                string jsonPath = m_OutPutPath + "/depenencies.json";
                // 增量打包时，融合原有依赖文件
                if (m_incrementalPackaging)
                {
                    if (File.Exists(jsonPath))
                    {
                        string readJson = File.ReadAllText(jsonPath);
                        DependenciesData oldDependencies = JsonUtility.FromJson<DependenciesData>(readJson);
                        DependenciesData newDependencies = BuildAssetBundle();
                        dependence = DependenceUtility.ConbineDependence(new DependenciesData[]
                        {
                            oldDependencies,
                            newDependencies
                        });
                    }
                    else
                    {
                        if (EditorUtility.DisplayDialog("提示","输出路径中无依赖文件，无法进行增量打包,是否进行非增量打包","确认","取消"))
                        {
                            dependence = BuildAssetBundle();
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                else
                {
                    dependence = BuildAssetBundle();
                }

                string json = JsonUtility.ToJson(dependence, true);
                File.WriteAllText(jsonPath, json);

                Debug.Log("BuildAssetBundles Complete");
            }

            private DependenciesData BuildAssetBundle()
            {
                if (!Directory.Exists(m_OutPutPath))
                {
                    Directory.CreateDirectory(m_OutPutPath);
                }
                BuildPipeline.BuildAssetBundles(m_OutPutPath, m_Builds.ToArray(), m_buildAssetBundleOption, EditorUserBuildSettings.activeBuildTarget);
                AssetDatabase.Refresh();

                string dependenctAb = Utility.Text.SplitPathName(m_OutPutPath)[1];
                AssetBundle mainfestAB = AssetBundle.LoadFromFile(m_OutPutPath + "/" + dependenctAb);
                var mainfest = mainfestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                var dependence = DependenceUtility.Manifest2Dependence(mainfest);
                return dependence;
            }
        }
    }
}