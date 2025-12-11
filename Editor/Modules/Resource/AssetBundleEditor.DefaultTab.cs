using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using XFramework.Resource;
using Object = UnityEngine.Object;

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

            private List<AssetBundleBuild> m_Builds;

            private string m_OutPutPath;
            private BuildAssetBundleOptions m_buildAssetBundleOption;
            /// <summary>
            /// 是否为增量打包
            /// </summary>
            private bool m_incrementalPackaging;

            // UIElements root for this tab
            private VisualElement _root;
            private ListView _buildListView;
            private ListView _previewListView;
            private Toggle _incrementalToggle;
            private EnumField _optionsEnumField;
            private TextField _outputPathField;
            private InspectorElement _inspectorElement;
            
            
            private AssetBundleBuildConfig m_AssetBundleBuildConfig;

            public override void OnEnable()
            {
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

            // 构建 UIElements 界面（无 UXML/USS）
            public override VisualElement BuildUI()
            {
                if (_root != null)
                {
                    return _root;
                }

                _root = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        flexGrow = 1
                    }
                };

                var box = new XBox();
                var configPath = ResourceManager.BuildConfigAssetPath;
                var filePath = Path.Combine(XApplication.projectPath, configPath);
                if (!File.Exists(Path.Combine(XApplication.projectPath, configPath)))
                {
                    var dirPath = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }
                    var asset = ScriptableObject.CreateInstance<AssetBundleBuildConfig>();
                    AssetDatabase.CreateAsset(asset, configPath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                m_AssetBundleBuildConfig = AssetDatabase.LoadAssetAtPath<AssetBundleBuildConfig>(configPath);
                var objectField = new ObjectField("AssetBundleBuildConfig")
                {
                    objectType = typeof(AssetBundleBuildConfig),
                    allowSceneObjects = false,
                    value = m_AssetBundleBuildConfig
                };
                _inspectorElement = new InspectorElement(m_AssetBundleBuildConfig);
                
                box.Add(objectField);
                box.Add(_inspectorElement);
                
                _root.Add(box);

                // 预览区

                var _previewBox = new XBox();
                var previewHeader = new Label("AB包预览")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 }
                };
                _previewListView = new ListView(
                    m_Builds,
                    itemHeight: -1, // 支持动态高度
                    makeItem: MakePreviewItem,
                    bindItem: BindPreviewItem)
                {
                    selectionType = SelectionType.None,
                    style = { flexGrow = 1 },
                    virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight // 关键：动态高度
                };
                _previewBox.Add(previewHeader);
                _previewBox.Add(_previewListView);
                _root.Add(_previewBox);

                // 输出设置行
                var outputRow = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        marginTop = 6
                    }
                };
                _outputPathField = new TextField
                {
                    label = "输出路径",
                    value = m_OutPutPath, 
                    style =
                    {
                        flexGrow = 1
                    }
                };
                // 缩小标签占位宽度，让标签更贴近输入框
                _outputPathField.labelElement.style.minWidth = 0;
                _outputPathField.labelElement.style.maxWidth = 80;
                _outputPathField.labelElement.style.flexBasis = 0;
                
                _outputPathField.RegisterValueChangedCallback(evt => m_OutPutPath = evt.newValue);
                outputRow.Add(_outputPathField);

                var outputFolderBtn = new Button(() =>
                {
                    string temp = EditorUtility.OpenFolderPanel("选择输出文件夹", Application.streamingAssetsPath, "");
                    if (!string.IsNullOrEmpty(temp))
                    {
                        m_OutPutPath = temp;
                        _outputPathField.SetValueWithoutNotify(m_OutPutPath);
                    }
                }) { text = "📁" };
                outputRow.Add(outputFolderBtn);

                _optionsEnumField = new EnumField(m_buildAssetBundleOption);
                _optionsEnumField.RegisterValueChangedCallback(evt => m_buildAssetBundleOption = (BuildAssetBundleOptions)evt.newValue);
                outputRow.Add(_optionsEnumField);

                _incrementalToggle = new Toggle("增量包") { value = m_incrementalPackaging };
                _incrementalToggle.RegisterValueChangedCallback(evt => m_incrementalPackaging = evt.newValue);
                _incrementalToggle.style.flexGrow = 0;
                _incrementalToggle.style.flexShrink = 1;
                _incrementalToggle.style.marginLeft = 8;
                // _incrementalToggle.style.width = 80;
                outputRow.Add(_incrementalToggle);

                _root.Add(outputRow);

                // 底部菜单
                var bottomBar = new VisualElement()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        marginTop = 5,
                        marginBottom = 5,
                        justifyContent = Justify.FlexEnd,
                    }
                };
                bottomBar.Add(new Button(ClearOutputDir) { text = "清空输出目录" });
                bottomBar.Add(new Button(Build) { text = "打包" });
                bottomBar.Add(new Button(RefreshAssetBundleBuild) { text = "刷新预览" });
                
                var bottom = new XBox();
                bottom.Add(outputRow);
                bottom.Add(bottomBar);
                
                _root.Add(bottom);
                return _root;
            }

            // 预览项 UI
            private VisualElement MakePreviewItem()
            {
                var container = new XItemBox();
                var foldout = new Foldout
                {
                    name = "foldout",
                    value = false,
                    style =
                    {
                        marginBottom = 4
                    }
                };
                // 子容器用于显示 assetNames
                var inner = new VisualElement { name = "inner", style = { flexDirection = FlexDirection.Column, marginLeft = 12 } };
                foldout.Add(inner);
                container.Add(foldout);
                return container;
            }

            private void BindPreviewItem(VisualElement elem, int index)
            {
                var foldout = (Foldout)elem.Q<VisualElement>("foldout");
                var build = m_Builds[index];
                foldout.text = build.assetBundleName + "." + build.assetBundleVariant;

                var inner = foldout.Q<VisualElement>("inner");
                inner.Clear();
                foreach (var assetName in build.assetNames)
                {
                    inner.Add(new Label(assetName));
                }
                
                // 设置交错背景色 - 更明显的对比
                elem.style.backgroundColor = index % 2 == 0 ?
                    new Color(0.25f, 0.25f, 0.25f, 0.15f) :
                    new Color(0.3f, 0.3f, 0.3f, 0.25f);
            }

            // 底边栏：清空输出目录
            private void ClearOutputDir()
            {
                if (EditorUtility.DisplayDialog("警告", "是否要删除输出目录下的所有文件", "确认", "取消"))
                {
                    Utility.IO.CleraDirectory(m_OutPutPath);
                }
            }

            // 刷新要打包的ab包内容
            private void RefreshAssetBundleBuild()
            {
                m_Builds.Clear();
                var abConfig = m_AssetBundleBuildConfig;

                for (int i = 0; i < abConfig.pathConfigs.Length; i++)
                {
                    var config = abConfig.pathConfigs[i];
                    DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + config.path);
                    var tempBuilds = AssetBundleUtility.MarkDirectory(info, config.buildType);
                    m_Builds.AddRange(tempBuilds);
                }

                _previewListView.itemsSource = m_Builds;
                _previewListView.Rebuild();
            }

            // 打包
            private void Build()
            {
                RefreshAssetBundleBuild();

                DependenciesData dependence;

                string jsonPath = m_OutPutPath + "/dependencies.json";
                // 增量打包时，融合原有依赖文件
                if (m_incrementalPackaging)
                {
                    if (File.Exists(jsonPath))
                    {
                        string readJson = File.ReadAllText(jsonPath);
                        DependenciesData oldDependencies = JsonUtility.FromJson<DependenciesData>(readJson);
                        DependenciesData newDependencies = BuildAssetBundle();
                        dependence = DependencyUtility.CombineDependence(new DependenciesData[]
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

                string dependencyAb = Utility.Text.SplitPathName(m_OutPutPath)[1];
                AssetBundle manifestAB = AssetBundle.LoadFromFile(m_OutPutPath + "/" + dependencyAb);
                var manifest = manifestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                var dependence = DependencyUtility.Manifest2Dependence(manifest);
                return dependence;
            }
        }
    }
}