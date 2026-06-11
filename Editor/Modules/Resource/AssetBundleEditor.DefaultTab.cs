using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using XFramework.Resource;

namespace XFramework.Editor
{
    public partial class AssetBundleEditor
    {
        private static AssetBundleBuildConfig LoadOrCreateAssetBundleBuildConfig()
        {
            var configPath = ResourceManager.BuildConfigAssetPath;
            var filePath = Path.Combine(XApplication.projectPath, configPath);
            if (!File.Exists(filePath))
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

            return AssetDatabase.LoadAssetAtPath<AssetBundleBuildConfig>(configPath);
        }

        private static AssetBundleBuildConfig ReloadAssetBundleBuildConfig()
        {
            var config = LoadOrCreateAssetBundleBuildConfig();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ResourceManager.BuildConfigAssetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AssetBundleBuildConfig>(ResourceManager.BuildConfigAssetPath) ?? config;
        }

        private static List<AssetBundleBuild> CreateAssetBundleBuilds(AssetBundleBuildConfig buildConfig)
        {
            var builds = new List<AssetBundleBuild>();
            var markedAssets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var usedAssetBundleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pathConfigs = buildConfig.pathConfigs ?? Array.Empty<PathConfig>();
            for (int i = 0; i < pathConfigs.Length; i++)
            {
                var config = pathConfigs[i];
                DirectoryInfo info = new DirectoryInfo(Application.dataPath.Replace("Assets", "/") + config.path);
                var tempBuilds = AssetBundleUtility.MarkDirectory(info, config.buildType);
                foreach (var build in tempBuilds)
                {
                    AddAssetBundleBuild(builds, markedAssets, usedAssetBundleNames, build, false);
                }
            }

            var singleAssetConfigs = buildConfig.singleAssetConfigs ?? Array.Empty<AssetConfig>();
            foreach (var config in singleAssetConfigs)
            {
                string assetPath = NormalizeAssetPath(config.assetPath);
                if (!IsValidExplicitAssetPath(assetPath))
                {
                    continue;
                }

                AddAssetBundleBuild(builds, markedAssets, usedAssetBundleNames, CreateExplicitAssetBuild(assetPath, new[] { assetPath }), false);
            }

            var groupAssetConfigs = buildConfig.groupAssetConfigs ?? Array.Empty<GroupAssetConfig>();
            foreach (var config in groupAssetConfigs)
            {
                var assetPaths = GetValidExplicitAssetPaths(config.assetPaths);
                if (assetPaths.Count == 0)
                {
                    continue;
                }

                AddAssetBundleBuild(builds, markedAssets, usedAssetBundleNames, CreateExplicitGroupAssetBuild(config.abName, assetPaths), true);
            }

            AssetBundleBuildPreprocessor.Run(builds);
            return builds;
        }

        private static AssetBundleBuild CreateExplicitGroupAssetBuild(string configuredAbName, List<string> assetNames)
        {
            string assetBundleName = GetRootAssetBundleName(configuredAbName);
            if (string.IsNullOrEmpty(assetBundleName) && assetNames.Count > 0)
            {
                assetBundleName = GetRootAssetBundleName(Path.GetFileNameWithoutExtension(assetNames[0]));
            }

            return new AssetBundleBuild
            {
                assetBundleName = assetBundleName,
                assetBundleVariant = "ab",
                assetNames = assetNames.ToArray()
            };
        }

        private static AssetBundleBuild CreateExplicitAssetBuild(string nameAssetPath, string[] assetNames)
        {
            return new AssetBundleBuild
            {
                assetBundleName = Path.ChangeExtension(NormalizeAssetPath(nameAssetPath), null),
                assetBundleVariant = "ab",
                assetNames = assetNames
            };
        }

        private static void AddAssetBundleBuild(
            List<AssetBundleBuild> builds,
            Dictionary<string, string> markedAssets,
            HashSet<string> usedAssetBundleNames,
            AssetBundleBuild build,
            bool ensureUniqueAssetBundleName)
        {
            if (build.assetNames == null || build.assetNames.Length == 0)
            {
                return;
            }

            build.assetBundleName = NormalizeAssetPath(build.assetBundleName);
            build.assetBundleVariant = NormalizeAssetPath(build.assetBundleVariant);
            if (ensureUniqueAssetBundleName)
            {
                build.assetBundleName = GetUniqueAssetBundleName(build.assetBundleName, build.assetBundleVariant, usedAssetBundleNames);
            }

            string skippedAbName = NormalizeAssetPath(GetAssetBundleDisplayName(build));
            var assetNames = new List<string>(build.assetNames.Length);
            foreach (var assetName in build.assetNames)
            {
                string normalizedAssetPath = NormalizeAssetPath(assetName);
                if (string.IsNullOrEmpty(normalizedAssetPath))
                {
                    continue;
                }

                if (markedAssets.TryGetValue(normalizedAssetPath, out string firstAbName))
                {
                    Debug.LogWarning($"[AssetBundleEditor] 资源已被标记，保留首次归属。Asset: {normalizedAssetPath}, First AB: {firstAbName}, Skipped AB: {skippedAbName}");
                    continue;
                }

                markedAssets.Add(normalizedAssetPath, skippedAbName);
                assetNames.Add(normalizedAssetPath);
            }

            if (assetNames.Count == 0)
            {
                return;
            }

            build.assetNames = assetNames.ToArray();
            usedAssetBundleNames.Add(GetAssetBundleDisplayName(build));
            builds.Add(build);
        }

        private static string GetUniqueAssetBundleName(string assetBundleName, string assetBundleVariant, HashSet<string> usedAssetBundleNames)
        {
            string normalizedName = NormalizeAssetPath(assetBundleName);
            string normalizedVariant = NormalizeAssetPath(assetBundleVariant);
            string displayName = string.IsNullOrEmpty(normalizedVariant)
                ? normalizedName
                : $"{normalizedName}.{normalizedVariant}";

            if (!usedAssetBundleNames.Contains(displayName))
            {
                return normalizedName;
            }

            for (int i = 1; ; i++)
            {
                string candidateName = $"{normalizedName}{i:00}";
                string candidateDisplayName = string.IsNullOrEmpty(normalizedVariant)
                    ? candidateName
                    : $"{candidateName}.{normalizedVariant}";
                if (!usedAssetBundleNames.Contains(candidateDisplayName))
                {
                    return candidateName;
                }
            }
        }

        private static string GetRootAssetBundleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            string normalizedName = NormalizeAssetPath(name.Trim());
            if (normalizedName.EndsWith(".ab", StringComparison.OrdinalIgnoreCase))
            {
                normalizedName = normalizedName[..^3];
            }

            return Path.GetFileName(normalizedName);
        }

        private static List<string> GetValidExplicitAssetPaths(AssetConfig[] assetConfigs)
        {
            var assetPaths = new List<string>();
            if (assetConfigs == null)
            {
                return assetPaths;
            }

            foreach (var config in assetConfigs)
            {
                string assetPath = NormalizeAssetPath(config.assetPath);
                if (IsValidExplicitAssetPath(assetPath))
                {
                    assetPaths.Add(assetPath);
                }
            }

            return assetPaths;
        }

        private static bool IsValidExplicitAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                && !AssetDatabase.IsValidFolder(assetPath)
                && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace("\\", "/");
        }

        private static void BuildAssetBundlesWithManifest(string outputPath, List<AssetBundleBuild> builds, BuildAssetBundleOptions buildAssetBundleOption)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            BuildPipeline.BuildAssetBundles(outputPath, builds.ToArray(), buildAssetBundleOption, EditorUserBuildSettings.activeBuildTarget);
            AssetDatabase.Refresh();

            string dependencyAb = Utility.Text.SplitPathName(outputPath)[1];
            AssetBundle manifestAB = AssetBundle.LoadFromFile(outputPath + "/" + dependencyAb);
            var manifest = manifestAB.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            var dependencies = DependencyUtility.Manifest2Dependence(manifest);

            WriteAssetManifest(outputPath, "AssetManifest.json", builds, dependencies);

            Debug.Log("BuildAssetBundles Complete");
        }

        private static void WriteAssetManifest(
            string outputPath,
            string fileName,
            List<AssetBundleBuild> builds,
            SingleDependenciesData[] dependencies)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var asset2AbsList = new List<Asset2AB>();
            foreach (var build in builds)
            {
                foreach (var assetPath in build.assetNames)
                {
                    asset2AbsList.Add(new Asset2AB
                    {
                        assetPath = assetPath.Replace("\\", "/"),
                        abName = (string.IsNullOrEmpty(build.assetBundleVariant)
                            ? build.assetBundleName
                            : $"{build.assetBundleName}.{build.assetBundleVariant}").Replace("\\", "/")
                    });
                }
            }

            var assetManifest = new AssetManifest
            {
                dependencies = dependencies ?? Array.Empty<SingleDependenciesData>(),
                asset2Abs = asset2AbsList.ToArray(),
            };

            string json = JsonUtility.ToJson(assetManifest, true);
            File.WriteAllText(Path.Combine(outputPath, fileName), json);
        }

        private static string GetAssetBundleDisplayName(AssetBundleBuild build)
        {
            return string.IsNullOrEmpty(build.assetBundleVariant)
                ? build.assetBundleName
                : $"{build.assetBundleName}.{build.assetBundleVariant}";
        }

        private class DefaultTab : SubWindow
        {
            private List<AssetBundleBuild> m_Builds;

            private string m_OutPutPath;
            private BuildAssetBundleOptions m_buildAssetBundleOption;
            /// <summary>
            /// 是否为增量打包
            /// </summary>
            private bool m_incrementalPackaging;

            // UIElements root for this tab
            private VisualElement _root;
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
                        flexGrow = 1,
                        flexShrink = 1,
                        minHeight = 0
                    }
                };

                m_AssetBundleBuildConfig = LoadOrCreateAssetBundleBuildConfig();
                _root.Add(BuildSummaryLabel());
                _root.Add(BuildConfigPanel());
                _root.Add(BuildOutputPanel());
                return _root;
            }

            private Label BuildSummaryLabel()
            {
                int pathConfigCount = m_AssetBundleBuildConfig?.pathConfigs?.Length ?? 0;
                int singleAssetCount = m_AssetBundleBuildConfig?.singleAssetConfigs?.Length ?? 0;
                int groupAssetCount = m_AssetBundleBuildConfig?.groupAssetConfigs?.Length ?? 0;
                var summary = new Label($"目录规则: {pathConfigCount} | 单资源: {singleAssetCount} | 合包: {groupAssetCount} | 输出路径: {m_OutPutPath}")
                {
                    style =
                    {
                        marginTop = 4,
                        marginBottom = 6,
                        color = new Color(0.75f, 0.75f, 0.75f),
                        whiteSpace = WhiteSpace.Normal
                    }
                };
                return summary;
            }

            private VisualElement BuildConfigPanel()
            {
                var panel = CreateSettingsPanel();
                panel.style.flexGrow = 1;
                panel.style.minHeight = 0;
                panel.Add(CreatePanelTitle("AssetBundle 配置"));

                var objectField = new ObjectField("配置")
                {
                    objectType = typeof(AssetBundleBuildConfig),
                    allowSceneObjects = false,
                    value = m_AssetBundleBuildConfig,
                    style =
                    {
                        marginBottom = 6
                    }
                };
                ConfigureLabelWidth(objectField);
                panel.Add(objectField);

                var scrollView = new ScrollView
                {
                    style =
                    {
                        flexGrow = 1,
                        minHeight = 0
                    }
                };
                _inspectorElement = new InspectorElement(m_AssetBundleBuildConfig);
                _inspectorElement.style.marginLeft = -12;
                scrollView.Add(_inspectorElement);
                panel.Add(scrollView);
                return panel;
            }

            private VisualElement BuildOutputPanel()
            {
                var panel = CreateSettingsPanel();
                panel.style.flexShrink = 0;
                panel.style.marginTop = 6;
                panel.Add(CreatePanelTitle("输出与打包"));

                var outputRow = CreateSettingsRow();
                _outputPathField = new TextField
                {
                    label = "输出路径",
                    value = m_OutPutPath, 
                    style =
                    {
                        flexGrow = 1
                    }
                };
                ConfigureLabelWidth(_outputPathField);
                
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
                }) { text = "..." };
                outputFolderBtn.style.width = 34;
                outputFolderBtn.style.marginLeft = 6;
                outputRow.Add(outputFolderBtn);
                panel.Add(outputRow);

                var optionRow = CreateSettingsRow();
                _optionsEnumField = new EnumField(m_buildAssetBundleOption);
                _optionsEnumField.RegisterValueChangedCallback(evt => m_buildAssetBundleOption = (BuildAssetBundleOptions)evt.newValue);
                _optionsEnumField.style.width = 260;
                optionRow.Add(CreateInlineLabel("Build Options"));
                optionRow.Add(_optionsEnumField);

                _incrementalToggle = new Toggle("增量包") { value = m_incrementalPackaging };
                _incrementalToggle.RegisterValueChangedCallback(evt => m_incrementalPackaging = evt.newValue);
                _incrementalToggle.style.flexGrow = 0;
                _incrementalToggle.style.flexShrink = 1;
                _incrementalToggle.style.marginLeft = 8;
                optionRow.Add(_incrementalToggle);
                panel.Add(optionRow);

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
                bottomBar.Add(CreateActionButton("清空输出目录", ClearOutputDir, 110));
                bottomBar.Add(CreateActionButton("生成临时 Manifest", BuildTempManifest, 130));
                bottomBar.Add(CreateActionButton("打包", Build, 76));
                panel.Add(bottomBar);
                return panel;
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

            private static Label CreateInlineLabel(string text)
            {
                return new Label(text)
                {
                    style =
                    {
                        width = 84,
                        minWidth = 84,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        color = new Color(0.75f, 0.75f, 0.75f)
                    }
                };
            }

            private static Button CreateActionButton(string text, Action callback, float width)
            {
                var button = new Button(callback)
                {
                    text = text
                };
                button.style.width = width;
                button.style.marginLeft = 8;
                return button;
            }

            private static void ConfigureLabelWidth(TextField field)
            {
                field.labelElement.style.minWidth = 0;
                field.labelElement.style.maxWidth = 84;
                field.labelElement.style.width = 84;
                field.labelElement.style.flexBasis = 84;
            }

            private static void ConfigureLabelWidth(ObjectField field)
            {
                field.labelElement.style.minWidth = 0;
                field.labelElement.style.maxWidth = 84;
                field.labelElement.style.width = 84;
                field.labelElement.style.flexBasis = 84;
            }

            // 底边栏：清空输出目录
            private void ClearOutputDir()
            {
                if (EditorUtility.DisplayDialog("警告", "是否要删除输出目录下的所有文件", "确认", "取消"))
                {
                    Utility.IO.ClearDirectory(m_OutPutPath);
                }
            }

            // 刷新要打包的ab包内容
            private void RefreshAssetBundleBuild()
            {
                m_AssetBundleBuildConfig = ReloadAssetBundleBuildConfig();
                m_Builds.Clear();
                m_Builds.AddRange(CreateAssetBundleBuilds(m_AssetBundleBuildConfig));
            }

            private void BuildTempManifest()
            {
                RefreshAssetBundleBuild();
                WriteAssetManifest(
                    m_OutPutPath,
                    "AssetManifestTemp.json",
                    m_Builds,
                    Array.Empty<SingleDependenciesData>());
                AssetDatabase.Refresh();
                Debug.Log("Build AssetManifestTemp Complete");
            }

            // 打包
            private void Build()
            {
                if (m_incrementalPackaging)
                {
                    if (EditorUtility.DisplayDialog("提示","输出路径中无依赖文件，无法进行增量打包,是否进行非增量打包","确认","取消"))
                    {
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
                
                RefreshAssetBundleBuild();
                BuildAssetBundlesWithManifest(m_OutPutPath, m_Builds, m_buildAssetBundleOption);
            }
        }

        private class PreviewTab : SubWindow
        {
            private const string AllExtensionOption = "全部";
            private const float LeftPaneWidth = 660f;
            private const float BundleColumnWidth = 220f;
            private const float AssetCountColumnWidth = 64f;
            private const float ExtensionCountColumnWidth = 64f;

            private readonly List<AssetBundleBuild> m_Builds = new();
            private readonly List<AssetBundleBuild> m_FilteredBuilds = new();

            private AssetBundleBuildConfig m_AssetBundleBuildConfig;
            private VisualElement m_Root;
            private TextField m_BundleSearchField;
            private TextField m_AssetSearchField;
            private DropdownField m_ExtensionFilterField;
            private Label m_SummaryLabel;
            private ListView m_BundleListView;
            private ScrollView m_DetailPane;

            private string m_BundleSearch = string.Empty;
            private string m_AssetSearch = string.Empty;
            private string m_ExtensionFilter = AllExtensionOption;
            private string m_SelectedBundleName = string.Empty;

            public override VisualElement BuildUI()
            {
                if (m_Root != null)
                {
                    return m_Root;
                }

                m_AssetBundleBuildConfig = LoadOrCreateAssetBundleBuildConfig();
                m_Root = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        flexGrow = 1,
                        minHeight = 0
                    }
                };

                m_Root.Add(BuildToolbar());

                m_SummaryLabel = new Label();
                m_SummaryLabel.style.marginTop = 4;
                m_SummaryLabel.style.marginBottom = 6;
                m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
                m_Root.Add(m_SummaryLabel);

                var splitView = new TwoPaneSplitView(0, LeftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
                splitView.style.flexGrow = 1;
                m_Root.Add(splitView);

                splitView.Add(BuildBundleListPanel());
                splitView.Add(BuildDetailPanel());

                RefreshPreview();
                return m_Root;
            }

            private VisualElement BuildToolbar()
            {
                var toolbar = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center
                    }
                };

                m_BundleSearchField = new TextField("AB包搜索")
                {
                    style =
                    {
                        flexGrow = 1,
                        minWidth = 180
                    }
                };
                m_BundleSearchField.tooltip = "按AB包名或资源路径搜索";
                m_BundleSearchField.RegisterValueChangedCallback(evt =>
                {
                    m_BundleSearch = evt.newValue ?? string.Empty;
                    ApplyBundleFilter();
                });
                toolbar.Add(m_BundleSearchField);

                m_ExtensionFilterField = new DropdownField("扩展名", new List<string> { AllExtensionOption }, 0)
                {
                    style =
                    {
                        width = 150,
                        marginLeft = 8,
                        flexShrink = 0
                    }
                };
                m_ExtensionFilterField.tooltip = "只显示包含指定扩展名资源的AB包";
                m_ExtensionFilterField.RegisterValueChangedCallback(evt =>
                {
                    m_ExtensionFilter = evt.newValue ?? AllExtensionOption;
                    ApplyBundleFilter();
                });
                toolbar.Add(m_ExtensionFilterField);

                var refreshButton = new Button(RefreshPreview)
                {
                    text = "刷新"
                };
                refreshButton.style.marginLeft = 8;
                refreshButton.style.width = 64;
                refreshButton.tooltip = "重新生成AB包预览数据";
                toolbar.Add(refreshButton);
                return toolbar;
            }

            private VisualElement BuildBundleListPanel()
            {
                var panel = new VisualElement
                {
                    style =
                    {
                        flexGrow = 1,
                        flexDirection = FlexDirection.Column,
                        marginRight = 4,
                        paddingLeft = 4,
                        paddingRight = 4,
                        paddingTop = 4,
                        paddingBottom = 4,
                        backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f)
                    }
                };

                panel.Add(BuildListHeader());

                m_BundleListView = new ListView
                {
                    itemsSource = m_FilteredBuilds,
                    fixedItemHeight = 24,
                    selectionType = SelectionType.Single,
                    makeItem = MakeBundleItem,
                    bindItem = BindBundleItem,
                    style =
                    {
                        flexGrow = 1,
                        marginTop = 4
                    }
                };
                m_BundleListView.onSelectionChange += OnBundleSelectionChanged;
                panel.Add(m_BundleListView);
                return panel;
            }

            private VisualElement BuildListHeader()
            {
                var header = CreateRow(new Color(0.2f, 0.2f, 0.2f), 22);
                header.Add(CreateHeaderLabel("AB包", BundleColumnWidth));
                header.Add(CreateHeaderLabel("资源", AssetCountColumnWidth));
                header.Add(CreateHeaderLabel("类型", ExtensionCountColumnWidth));

                var samplePath = CreateHeaderLabel("示例路径", 0);
                samplePath.style.flexGrow = 1;
                header.Add(samplePath);
                return header;
            }

            private VisualElement BuildDetailPanel()
            {
                m_DetailPane = new ScrollView
                {
                    style =
                    {
                        flexGrow = 1,
                        marginLeft = 4,
                        paddingLeft = 10,
                        paddingRight = 10,
                        paddingTop = 10,
                        paddingBottom = 10,
                        backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f)
                    }
                };
                return m_DetailPane;
            }

            private VisualElement MakeBundleItem()
            {
                var row = CreateRow(Color.clear, 24);
                row.Add(CreateCellLabel(BundleColumnWidth));
                row.Add(CreateCellLabel(AssetCountColumnWidth));
                row.Add(CreateCellLabel(ExtensionCountColumnWidth));

                var samplePath = CreateCellLabel(0);
                samplePath.style.flexGrow = 1;
                row.Add(samplePath);
                return row;
            }

            private void BindBundleItem(VisualElement elem, int index)
            {
                var build = m_FilteredBuilds[index];
                elem.style.backgroundColor = index % 2 == 0
                    ? new Color(0.25f, 0.25f, 0.25f, 0.12f)
                    : new Color(0.3f, 0.3f, 0.3f, 0.2f);

                var labels = elem.Query<Label>().ToList();
                labels[0].text = GetAssetBundleDisplayName(build);
                labels[1].text = build.assetNames.Length.ToString();
                labels[2].text = GetExtensionCounts(build.assetNames).Count.ToString();
                labels[3].text = GetSampleAssetPath(build.assetNames);
            }

            private void OnBundleSelectionChanged(IEnumerable<object> selectedItems)
            {
                foreach (var item in selectedItems)
                {
                    if (item is AssetBundleBuild build)
                    {
                        m_SelectedBundleName = GetAssetBundleDisplayName(build);
                        RefreshDetail();
                        return;
                    }
                }

                m_SelectedBundleName = string.Empty;
                RefreshDetail();
            }

            private void RefreshPreview()
            {
                m_AssetBundleBuildConfig = ReloadAssetBundleBuildConfig();
                m_Builds.Clear();
                m_Builds.AddRange(CreateAssetBundleBuilds(m_AssetBundleBuildConfig));
                RefreshExtensionFilterOptions();
                ApplyBundleFilter();
            }

            private void ApplyBundleFilter()
            {
                m_FilteredBuilds.Clear();

                foreach (var build in m_Builds)
                {
                    var displayName = GetAssetBundleDisplayName(build);
                    if (MatchesBundleFilter(build, displayName))
                    {
                        m_FilteredBuilds.Add(build);
                    }
                }

                if (string.IsNullOrEmpty(m_SelectedBundleName) ||
                    FindFilteredBuildIndex(m_SelectedBundleName) < 0)
                {
                    m_SelectedBundleName = m_FilteredBuilds.Count > 0
                        ? GetAssetBundleDisplayName(m_FilteredBuilds[0])
                        : string.Empty;
                }

                m_BundleListView.itemsSource = m_FilteredBuilds;
                m_BundleListView.Rebuild();

                int selectedIndex = FindFilteredBuildIndex(m_SelectedBundleName);
                if (selectedIndex >= 0)
                {
                    m_BundleListView.SetSelectionWithoutNotify(new[] { selectedIndex });
                    m_BundleListView.ScrollToItem(selectedIndex);
                }
                else
                {
                    m_BundleListView.ClearSelection();
                }

                RefreshDetail();
                RefreshSummary();
            }

            private int FindFilteredBuildIndex(string bundleName)
            {
                for (int i = 0; i < m_FilteredBuilds.Count; i++)
                {
                    if (GetAssetBundleDisplayName(m_FilteredBuilds[i]) == bundleName)
                    {
                        return i;
                    }
                }

                return -1;
            }

            private void RefreshDetail()
            {
                m_DetailPane.Clear();
                var selectedBuildIndex = FindBuildIndex(m_SelectedBundleName);
                if (selectedBuildIndex < 0)
                {
                    m_DetailPane.Add(CreateTitleLabel("未选择AB包"));
                    m_DetailPane.Add(CreateMutedLabel("从左侧选择一个AB包查看详情。"));
                    return;
                }

                var build = m_Builds[selectedBuildIndex];
                m_DetailPane.Add(CreateTitleLabel(m_SelectedBundleName));
                m_DetailPane.Add(BuildActionRow(build));
                m_DetailPane.Add(BuildBundleInfoSection(build));
                m_DetailPane.Add(BuildAssetSearchField());

                var groups = BuildAssetGroups(build.assetNames);
                var listSection = CreateSection("资源列表");
                if (groups.Count == 0)
                {
                    listSection.Add(CreateMutedLabel("当前过滤条件下没有资源。"));
                    m_DetailPane.Add(listSection);
                    return;
                }

                foreach (var group in groups)
                {
                    listSection.Add(MakeAssetGroup(group.Key, group.Value));
                }

                m_DetailPane.Add(listSection);
            }

            private int FindBuildIndex(string bundleName)
            {
                for (int i = 0; i < m_Builds.Count; i++)
                {
                    if (GetAssetBundleDisplayName(m_Builds[i]) == bundleName)
                    {
                        return i;
                    }
                }

                return -1;
            }

            private List<KeyValuePair<string, List<string>>> BuildAssetGroups(string[] assetNames)
            {
                var groupMap = new Dictionary<string, List<string>>();
                foreach (var assetName in assetNames)
                {
                    var extension = NormalizeExtension(Path.GetExtension(assetName));
                    if (!MatchesExtensionFilter(extension))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(m_AssetSearch) &&
                        assetName.IndexOf(m_AssetSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (!groupMap.TryGetValue(extension, out var groupAssets))
                    {
                        groupAssets = new List<string>();
                        groupMap.Add(extension, groupAssets);
                    }

                    groupAssets.Add(assetName);
                }

                var groups = new List<KeyValuePair<string, List<string>>>();
                foreach (var pair in groupMap)
                {
                    pair.Value.Sort(StringComparer.OrdinalIgnoreCase);
                    groups.Add(pair);
                }

                groups.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase));
                return groups;
            }

            private VisualElement MakeAssetGroup(string extension, List<string> assetNames)
            {
                var group = new VisualElement
                {
                    style =
                    {
                        marginTop = 8
                    }
                };

                var header = CreateRow(new Color(0.2f, 0.2f, 0.2f), 22);
                var title = CreateHeaderLabel($"{extension} ({assetNames.Count})", 0);
                title.style.flexGrow = 1;
                header.Add(title);
                group.Add(header);

                foreach (var assetName in assetNames)
                {
                    var row = CreateRow(Color.clear, 24);
                    row.style.paddingLeft = 8;

                    row.Add(CreateAssetObjectField(assetName));

                    var pathLabel = CreateCellLabel(0);
                    pathLabel.style.flexGrow = 1;
                    pathLabel.style.marginLeft = 8;
                    pathLabel.text = assetName;
                    row.Add(pathLabel);
                    group.Add(row);
                }

                return group;
            }

            private static ObjectField CreateAssetObjectField(string assetPath)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                var field = new ObjectField
                {
                    objectType = typeof(UnityEngine.Object),
                    allowSceneObjects = false,
                    value = asset,
                    tooltip = assetPath
                };
                field.style.width = 240;
                field.style.flexShrink = 0;
                field.RegisterValueChangedCallback(_ => field.SetValueWithoutNotify(asset));
                field.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0 || asset == null)
                    {
                        return;
                    }

                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                    if (evt.clickCount >= 2)
                    {
                        AssetDatabase.OpenAsset(asset);
                    }

                    evt.StopImmediatePropagation();
                }, TrickleDown.TrickleDown);
                return field;
            }

            private bool MatchesBundleFilter(AssetBundleBuild build, string displayName)
            {
                if (!MatchesExtensionFilter(build.assetNames))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(m_BundleSearch))
                {
                    return true;
                }

                if (displayName.IndexOf(m_BundleSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                foreach (var assetName in build.assetNames)
                {
                    if (assetName.IndexOf(m_BundleSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool MatchesExtensionFilter(string[] assetNames)
            {
                if (m_ExtensionFilter == AllExtensionOption)
                {
                    return true;
                }

                foreach (var assetName in assetNames)
                {
                    if (MatchesExtensionFilter(NormalizeExtension(Path.GetExtension(assetName))))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool MatchesExtensionFilter(string extension)
            {
                return m_ExtensionFilter == AllExtensionOption || extension == m_ExtensionFilter;
            }

            private void RefreshExtensionFilterOptions()
            {
                var extensions = new HashSet<string>();
                foreach (var build in m_Builds)
                {
                    foreach (var assetName in build.assetNames)
                    {
                        extensions.Add(NormalizeExtension(Path.GetExtension(assetName)));
                    }
                }

                var options = new List<string> { AllExtensionOption };
                var sortedExtensions = new List<string>(extensions);
                sortedExtensions.Sort(StringComparer.OrdinalIgnoreCase);
                options.AddRange(sortedExtensions);

                m_ExtensionFilterField.choices = options;
                if (!options.Contains(m_ExtensionFilter))
                {
                    m_ExtensionFilter = AllExtensionOption;
                }

                m_ExtensionFilterField.SetValueWithoutNotify(m_ExtensionFilter);
            }

            private void RefreshSummary()
            {
                int assetCount = 0;
                foreach (var build in m_Builds)
                {
                    assetCount += build.assetNames.Length;
                }

                int filteredAssetCount = 0;
                foreach (var build in m_FilteredBuilds)
                {
                    filteredAssetCount += CountFilteredAssets(build.assetNames);
                }

                m_SummaryLabel.text = $"AB包: {m_Builds.Count} | 资源: {assetCount} | 当前过滤: {m_FilteredBuilds.Count} 包 / {filteredAssetCount} 资源";
            }

            private int CountFilteredAssets(string[] assetNames)
            {
                int count = 0;
                foreach (var assetName in assetNames)
                {
                    if (MatchesExtensionFilter(NormalizeExtension(Path.GetExtension(assetName))))
                    {
                        count++;
                    }
                }

                return count;
            }

            private VisualElement BuildActionRow(AssetBundleBuild build)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        marginBottom = 8
                    }
                };

                var copyBundleButton = new Button(() => EditorGUIUtility.systemCopyBuffer = GetAssetBundleDisplayName(build))
                {
                    text = "复制包名"
                };
                copyBundleButton.style.width = 88;
                row.Add(copyBundleButton);

                var copyAssetsButton = new Button(() => EditorGUIUtility.systemCopyBuffer = string.Join(Environment.NewLine, GetFilteredAssetNames(build.assetNames)))
                {
                    text = "复制资源路径"
                };
                copyAssetsButton.style.width = 110;
                copyAssetsButton.style.marginLeft = 8;
                row.Add(copyAssetsButton);
                return row;
            }

            private VisualElement BuildBundleInfoSection(AssetBundleBuild build)
            {
                var section = CreateSection("AB包信息");
                section.Add(CreateInfoRow("包名", build.assetBundleName));
                section.Add(CreateInfoRow("Variant", string.IsNullOrEmpty(build.assetBundleVariant) ? "-" : build.assetBundleVariant));
                section.Add(CreateInfoRow("资源数", build.assetNames.Length.ToString()));
                section.Add(CreateInfoRow("扩展名统计", BuildExtensionSummary(build.assetNames)));
                return section;
            }

            private VisualElement BuildAssetSearchField()
            {
                var section = CreateSection("资源过滤");
                m_AssetSearchField = new TextField("搜索");
                m_AssetSearchField.value = m_AssetSearch;
                m_AssetSearchField.tooltip = "只过滤右侧当前AB包资源路径";
                m_AssetSearchField.RegisterValueChangedCallback(evt =>
                {
                    m_AssetSearch = evt.newValue ?? string.Empty;
                    RefreshDetail();
                    RefreshSummary();
                });
                section.Add(m_AssetSearchField);
                return section;
            }

            private List<string> GetFilteredAssetNames(string[] assetNames)
            {
                var result = new List<string>();
                foreach (var assetName in assetNames)
                {
                    if (!MatchesExtensionFilter(NormalizeExtension(Path.GetExtension(assetName))))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(m_AssetSearch) &&
                        assetName.IndexOf(m_AssetSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    result.Add(assetName);
                }

                result.Sort(StringComparer.OrdinalIgnoreCase);
                return result;
            }

            private static string NormalizeExtension(string extension)
            {
                return string.IsNullOrEmpty(extension) ? "No Extension" : extension;
            }

            private static Dictionary<string, int> GetExtensionCounts(string[] assetNames)
            {
                var counts = new Dictionary<string, int>();
                foreach (var assetName in assetNames)
                {
                    var extension = NormalizeExtension(Path.GetExtension(assetName));
                    counts.TryGetValue(extension, out int count);
                    counts[extension] = count + 1;
                }

                return counts;
            }

            private static string GetSampleAssetPath(string[] assetNames)
            {
                if (assetNames.Length == 0)
                {
                    return "-";
                }

                var sortedAssets = new List<string>(assetNames);
                sortedAssets.Sort(StringComparer.OrdinalIgnoreCase);
                return sortedAssets[0];
            }

            private static string BuildExtensionSummary(string[] assetNames)
            {
                var parts = new List<string>();
                foreach (var pair in GetSortedExtensionCounts(assetNames))
                {
                    parts.Add($"{pair.Key}: {pair.Value}");
                }

                return parts.Count == 0 ? "-" : string.Join(" | ", parts);
            }

            private static List<KeyValuePair<string, int>> GetSortedExtensionCounts(string[] assetNames)
            {
                var counts = new List<KeyValuePair<string, int>>(GetExtensionCounts(assetNames));
                counts.Sort((left, right) =>
                {
                    int countCompare = right.Value.CompareTo(left.Value);
                    return countCompare != 0
                        ? countCompare
                        : string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase);
                });
                return counts;
            }

            private static VisualElement CreateSection(string title)
            {
                var section = new VisualElement
                {
                    style =
                    {
                        marginBottom = 10
                    }
                };

                var titleLabel = new Label(title)
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        marginBottom = 4
                    }
                };
                section.Add(titleLabel);
                return section;
            }

            private static VisualElement CreateInfoRow(string title, string value)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        minHeight = 22,
                        alignItems = Align.Center
                    }
                };

                var titleLabel = new Label(title)
                {
                    style =
                    {
                        width = 90,
                        minWidth = 90,
                        color = new Color(0.75f, 0.75f, 0.75f)
                    }
                };
                row.Add(titleLabel);

                var valueLabel = new Label(value)
                {
                    style =
                    {
                        flexGrow = 1,
                        whiteSpace = WhiteSpace.Normal,
                        color = new Color(0.86f, 0.86f, 0.86f)
                    }
                };
                row.Add(valueLabel);
                return row;
            }

            private static Label CreateTitleLabel(string text)
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

            private static Label CreateMutedLabel(string text)
            {
                return new Label(text)
                {
                    style =
                    {
                        color = new Color(0.75f, 0.75f, 0.75f),
                        whiteSpace = WhiteSpace.Normal,
                        marginTop = 4
                    }
                };
            }

            private static VisualElement CreateRow(Color backgroundColor, float height)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        height = height,
                        alignItems = Align.Center,
                        paddingLeft = 4,
                        paddingRight = 4,
                        backgroundColor = backgroundColor
                    }
                };

                return row;
            }

            private static Label CreateHeaderLabel(string text, float width)
            {
                var label = CreateCellLabel(width);
                label.text = text;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                return label;
            }

            private static Label CreateCellLabel(float width)
            {
                var label = new Label
                {
                    style =
                    {
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        whiteSpace = WhiteSpace.NoWrap,
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };

                if (width > 0)
                {
                    label.style.width = width;
                    label.style.minWidth = width;
                    label.style.maxWidth = width;
                }

                return label;
            }
        }
    }
}
