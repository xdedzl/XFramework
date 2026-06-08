using System;
using UnityEditor;
using UnityEngine;
using XFramework.Resource;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public partial class AssetBundleEditor
    {
        private class BuildTab : SubWindow
        {
            private string outPutPath;

            private bool isBuildAB;
            private bool isDevelopment;

            // UIElements 控件引用
            private VisualElement root;
            private TextField outPutPathField;
            private Toggle isBuildABToggle;
            private Toggle isDevelopmentToggle;
            private Button buildButton;

            public override void OnEnable()
            {
                outPutPath = EditorPrefs.GetString("build_OutPut");
                isBuildAB = EditorPrefs.GetBool("build_IsBuildAB", true);
                isDevelopment = EditorPrefs.GetBool("build_IsDevelopment", true);
            }

            public override void OnDisable()
            {
                EditorPrefs.SetString("build_OutPut", outPutPath);
                EditorPrefs.SetBool("build_IsBuildAB", isBuildAB);
                EditorPrefs.SetBool("build_IsDevelopment", isDevelopment);
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
                        flexGrow = 1,
                        minHeight = 0
                    }
                };

                root.Add(BuildSettingsPanel());
                return root;
            }

            private VisualElement BuildSettingsPanel()
            {
                var panel = CreateSettingsPanel();
                panel.style.flexShrink = 0;
                panel.Add(CreatePanelTitle("项目构建"));

                var pathRow = CreateSettingsRow();
                outPutPathField = new TextField("输出路径")
                {
                    value = outPutPath,
                    style =
                    {
                        flexGrow = 1
                    }
                };
                ConfigureLabelWidth(outPutPathField);
                outPutPathField.RegisterValueChangedCallback(evt =>
                {
                    outPutPath = evt.newValue;
                });
                pathRow.Add(outPutPathField);

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
                    text = "...",
                    style =
                    {
                        width = 34,
                        marginLeft = 6
                    }
                };
                pathRow.Add(outFolderBtn);
                panel.Add(pathRow);

                var optionRow = CreateSettingsRow();
                isBuildABToggle = new Toggle
                {
                    text = "是否打包AB",
                    value = isBuildAB
                };
                isBuildABToggle.RegisterValueChangedCallback(evt =>
                {
                    isBuildAB = evt.newValue;
                });
                optionRow.Add(isBuildABToggle);
                
                isDevelopmentToggle = new Toggle
                {
                    text = "是否为Development",
                    value = isDevelopment
                };
                isDevelopmentToggle.RegisterValueChangedCallback(evt =>
                {
                    isDevelopment = evt.newValue;
                });
                isDevelopmentToggle.style.marginLeft = 12;
                optionRow.Add(isDevelopmentToggle);
                panel.Add(optionRow);

                var actionRow = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        justifyContent = Justify.FlexEnd,
                        marginTop = 5
                    }
                };
                buildButton = new Button(OnBuildClicked)
                {
                    text = "一键打包",
                    style =
                    {
                        width = 92
                    }
                };
                actionRow.Add(buildButton);
                panel.Add(actionRow);
                return panel;
            }

            // 保留 IMGUI 接口但不再绘制内容，避免重复 UI
            public override void OnGUI()
            {
                // 使用 UIElements 构建的界面，不在 IMGUI 中重复绘制
            }

            private static VisualElement CreateSettingsPanel()
            {
                return new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        paddingLeft = 10,
                        paddingRight = 10,
                        paddingTop = 10,
                        paddingBottom = 10,
                        backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f)
                    }
                };
            }

            private static Label CreatePanelTitle(string text)
            {
                return new Label(text)
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 14,
                        marginBottom = 8
                    }
                };
            }

            private static VisualElement CreateSettingsRow()
            {
                return new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 6
                    }
                };
            }

            private static void ConfigureLabelWidth(TextField field)
            {
                field.labelElement.style.minWidth = 0;
                field.labelElement.style.maxWidth = 84;
                field.labelElement.style.width = 84;
                field.labelElement.style.flexBasis = 84;
            }

            private void OnBuildClicked()
            {
                if (isBuildAB)
                {
                    var assetBundleBuildConfig = ReloadAssetBundleBuildConfig();
                    var builds = CreateAssetBundleBuilds(assetBundleBuildConfig);
                    var assetBundleOutputPath = Application.streamingAssetsPath + "/AssetBundles";
                    var buildAssetBundleOption = (BuildAssetBundleOptions)EditorPrefs.GetInt("BuildAssetBundleOptions", (int)BuildAssetBundleOptions.None);
                    BuildAssetBundlesWithManifest(assetBundleOutputPath, builds, buildAssetBundleOption);
                }

                var buildScenes = EditorBuildSettings.scenes;
                string exeName = Utility.Text.SplitPathName(Environment.CurrentDirectory)[1] + ".exe";

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
