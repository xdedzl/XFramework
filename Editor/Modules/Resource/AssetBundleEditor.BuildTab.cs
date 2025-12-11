using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using XFramework.Resource;
using UnityEngine.UIElements;

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

            // UIElements 控件引用
            private VisualElement root;
            private TextField outPutPathField;
            private TextField abResPathField;
            private TextField abRelaPathField;
            private Toggle isBuildABToggle;
            private Toggle isDevelopmentToggle;
            private Button buildButton;

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

            // 使用 UIElements 构建界面（纯 C#，不使用 UXML/USS）
            public override VisualElement BuildUI()
            {
                if (root != null)
                {
                    return root;
                }

                root = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        flexGrow = 1
                    }
                };
                
                // 根容器纵向布局
                root.style.flexDirection = FlexDirection.Column;
                root.style.paddingLeft = 6;
                root.style.paddingRight = 6;
                root.style.paddingTop = 6;
                root.style.paddingBottom = 6;
                root.style.flexGrow = 1;

                // 行构造助手
                VisualElement Row()
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 4;
                    return row;
                }

                Label MakeLabel(string text)
                {
                    var label = new Label(text);
                    label.style.width = titleW;
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    return label;
                }

                // 输出路径行
                var rowOut = Row();
                rowOut.Add(MakeLabel("输出路径"));
                outPutPathField = new TextField
                {
                    value = outPutPath,
                    style =
                    {
                        flexGrow = 1, // 自适应扩展
                        minWidth = textFieldW
                    }
                };
                outPutPathField.RegisterValueChangedCallback(evt =>
                {
                    outPutPath = evt.newValue;
                });
                rowOut.Add(outPutPathField);
                var outFolderBtn = new Button(() =>
                {
                    string temp = EditorUtility.OpenFolderPanel("输出路径", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(temp))
                    {
                        outPutPath = temp;
                        outPutPathField.SetValueWithoutNotify(outPutPath);
                    }
                })
                {
                    text = "📁",
                    style =
                    {
                        width = 60
                    }
                };
                rowOut.Add(outFolderBtn);
                root.Add(rowOut);

                // AB资源根目录行
                var rowRes = Row();
                rowRes.Add(MakeLabel("AB资源根目录"));
                abResPathField = new TextField
                {
                    value = abResPath,
                    style =
                    {
                        flexGrow = 1,
                        minWidth = textFieldW
                    }
                };
                abResPathField.RegisterValueChangedCallback(evt =>
                {
                    abResPath = evt.newValue;
                });
                rowRes.Add(abResPathField);
                var resFolderBtn = new Button(() =>
                {
                    string temp = EditorUtility.OpenFolderPanel("AB资源根目录", Application.dataPath, "");
                    if (!string.IsNullOrEmpty(temp))
                    {
                        int index = temp.IndexOf("Assets", StringComparison.Ordinal);
                        if (index == -1)
                        {
                            Debug.LogError("选择的AB包文件夹必须在Assets文件夹下");
                            return;
                        }
                        temp = temp.Substring(index, temp.Length - index);
                        abResPath = temp;
                        abResPathField.SetValueWithoutNotify(abResPath);
                    }
                })
                {
                    text = "📁",
                    style =
                    {
                        width = 60
                    }
                };
                rowRes.Add(resFolderBtn);
                root.Add(rowRes);

                // AB包相对路径行
                var rowRel = Row();
                rowRel.Add(MakeLabel("AB包相对路径"));
                abRelaPathField = new TextField
                {
                    value = abRelaPath,
                    style =
                    {
                        flexGrow = 1,
                        minWidth = textFieldW
                    }
                };
                abRelaPathField.RegisterValueChangedCallback(evt =>
                {
                    abRelaPath = evt.newValue;
                });
                rowRel.Add(abRelaPathField);
                root.Add(rowRel);

                // 选项与一键打包行
                var rowOpts = Row();
                isBuildABToggle = new Toggle
                {
                    text = "是否打包AB",
                    value = isBuildAB
                };
                isBuildABToggle.RegisterValueChangedCallback(evt => { isBuildAB = evt.newValue; });
                rowOpts.Add(isBuildABToggle);
                
                isDevelopmentToggle = new Toggle
                {
                    text = "是否为Development",
                    value = isDevelopment
                };
                isDevelopmentToggle.RegisterValueChangedCallback(evt => { isDevelopment = evt.newValue; });
                rowOpts.Add(isDevelopmentToggle);

                buildButton = new Button(OnBuildClicked)
                {
                    text = "一键打包",
                    style =
                    {
                        width = 80
                    }
                };
                rowOpts.Add(buildButton);
                root.Add(rowOpts);
                
                return root;
            }

            // 保留 IMGUI 接口但不再绘制内容，避免重复 UI
            public override void OnGUI()
            {
                // 使用 UIElements 构建的界面，不在 IMGUI 中重复绘制
            }

            private void OnBuildClicked()
            {
                List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
                if (isBuildAB)
                {
                    string abOutPath = outPutPath + "/" + abRelaPath;
                    if (!Directory.Exists(abOutPath))
                    {
                        Directory.CreateDirectory(abOutPath);
                    }

                    DirectoryInfo abResInfo = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + abResPath);
                    builds = AssetBundleUtility.MarkDirectory(abResInfo, PackOption.AllDirectory);

                    BuildPipeline.BuildAssetBundles(abOutPath, builds.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
                    AssetDatabase.Refresh();
                    Debug.Log("BuildAssetBundles Complete");

                    string dependenctAb = Utility.Text.SplitPathName(abOutPath)[1];
                    AssetBundle mainfestAB = AssetBundle.LoadFromFile(abOutPath + "/" + dependenctAb);
                    var mainfest = mainfestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                    var dependence = DependencyUtility.Manifest2Dependence(mainfest);

                    string json = JsonUtility.ToJson(dependence, true);
                    File.WriteAllText(abOutPath + "/dependencies.json", json);
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