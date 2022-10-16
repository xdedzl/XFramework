using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;

namespace XFramework.Editor
{
    public partial class AssetBundleEditor
    {
        private class DependenceTab : SubWindow
        {
            private int prefabCount;
            private List<GameObject> m_Prefabs = new List<GameObject>();
            private string m_PrefabABPath = Application.streamingAssetsPath + "/ABRes/Unit";

            public override void OnGUI()
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("打包依赖资源及预制体");

                    prefabCount = EditorGUILayout.IntField("数量", prefabCount);
                    if (prefabCount > 20) prefabCount = 20;
                    else if (prefabCount < 0) prefabCount = 0;

                    int differ = prefabCount - m_Prefabs.Count;
                    if (differ > 0)
                    {
                        for (int i = 0; i < differ; i++)
                        {
                            m_Prefabs.Add(null);
                        }
                    }
                    else if (differ < 0)
                    {
                        for (int i = 0; i < -differ; i++)
                        {
                            m_Prefabs.RemoveAt(m_Prefabs.Count - 1);
                        }
                    }
                    for (int i = 0; i < prefabCount; i++)
                    {
                        m_Prefabs[i] = EditorGUILayout.ObjectField((i + 1).ToString(), m_Prefabs[i], typeof(GameObject), false) as GameObject;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("输出路径");
                        GUILayout.TextField(m_PrefabABPath);
                        if (GUILayout.Button(EditorIcon.Folder))
                        {
                            string temp = EditorUtility.OpenFolderPanel("输出文件夹", Application.streamingAssetsPath, "");
                            if (!string.IsNullOrEmpty(temp))
                            {
                                m_PrefabABPath = temp;
                            }
                        }
                    }

                    GUILayout.Space(10);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("添加所有选中的预制体"))
                        {
                            AddPrefab();
                        }

                        if (GUILayout.Button("打包预制体及依赖"))
                        {
                            Build();
                        }
                    }
                }
            }

            /// <summary>
            /// 添加所有选中的预制体
            /// </summary>
            private void AddPrefab()
            {
                var objs = Selection.gameObjects;
                if (objs != null)
                {
                    foreach (var item in objs)
                    {
                        if (!m_Prefabs.Contains(item))
                        {
                            m_Prefabs.Add(item);
                            prefabCount++;
                        }
                    }
                }
            }

            /// <summary>
            /// 打包预制体及依赖
            /// </summary>
            private void Build()
            {
                List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
                
                foreach (var prefab in m_Prefabs)
                {
                    if (prefab != null)
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(prefab);
                        List<string> assetNames = new List<string>() { prefabPath };

                        int total = m_Prefabs.Count;
                        int index = 0;
                        var dependencies = EditorUtility.CollectDependencies(new Object[] { prefab });
                        foreach (var item in dependencies)
                        {
                            string path = AssetDatabase.GetAssetPath(item);
                            if (path.StartsWith("Assets") && path != prefabPath && !assetNames.Contains(path))
                            {
                                assetNames.Add(path);
                            }

                            index++;
                            EditorUtility.DisplayProgressBar("进度", item.name, index / total);
                        }

                        builds.Add(new AssetBundleBuild
                        {
                            assetBundleName = prefab.name,
                            assetBundleVariant = "asset",
                            assetNames = assetNames.ToArray()
                        });

                        // TODO 拷贝单位信息的json文件到输出目录

                    }
                }

                EditorUtility.ClearProgressBar();
                if (!Directory.Exists(m_PrefabABPath))
                {
                    Directory.CreateDirectory(m_PrefabABPath);
                }

                foreach (var item in builds)
                {
                    BuildPipeline.BuildAssetBundles(m_PrefabABPath, new AssetBundleBuild[] { item}, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
                }

                AssetDatabase.Refresh();
            }
        }
    }
}
