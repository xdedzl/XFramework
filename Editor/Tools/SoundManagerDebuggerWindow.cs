using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace XFramework.Editor
{
    public class SoundManagerDebuggerWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Debug/SoundManagerDebugger";
        private const string FilterAll = "全部";
        private const string FilterCurrent = "当前生效";
        private const string FilterActive = "已激活";
        private const string FilterPlayerInside = "玩家在内";
        private const string FilterNoBgm = "无有效 BGM";
        private const string FilterColliderNotTrigger = "Collider 非 Trigger";
        private const string FilterDisabled = "禁用对象";
        private const float BgmControlButtonSize = 20f;
        private const float InspectorLabelWidth = 112f;

        private readonly List<VolumeEntry> m_AllEntries = new();
        private readonly List<VolumeEntry> m_FilteredEntries = new();

        private TextField m_SearchField;
        private DropdownField m_FilterField;
        private Label m_SummaryLabel;
        private Label m_AutoRefreshLabel;
        private ListView m_ListView;
        private ScrollView m_DetailPane;

        private double m_LastRefreshTime;
        private VolumeEntry m_SelectedEntry;
        private SoundManagerDebugSnapshot? m_SoundSnapshot;
        private bool m_IsSoundManagerLoaded;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            SoundManagerDebuggerWindow window = GetWindow<SoundManagerDebuggerWindow>();
            window.titleContent = new GUIContent("SoundManagerDebugger");
            window.minSize = new Vector2(980f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
            RefreshData();
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
        }

        public void CreateGUI()
        {
            BuildUI();
            RefreshView();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;

            root.Add(BuildToolbar());

            m_SummaryLabel = new Label();
            m_SummaryLabel.style.marginTop = 4;
            m_SummaryLabel.style.marginBottom = 6;
            m_SummaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            m_SummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(m_SummaryLabel);

            TwoPaneSplitView splitView = new(0, 620, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            splitView.Add(BuildListPane());
            splitView.Add(BuildDetailPane());
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            m_SearchField = new TextField("搜索");
            m_SearchField.style.flexGrow = 1;
            m_SearchField.style.minWidth = 180;
            m_SearchField.tooltip = "按对象名、场景名或 BGM 路径过滤";
            m_SearchField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_SearchField);

            m_FilterField = new DropdownField("状态", new List<string>
            {
                FilterAll,
                FilterCurrent,
                FilterActive,
                FilterPlayerInside,
                FilterNoBgm,
                FilterColliderNotTrigger,
                FilterDisabled
            }, 0);
            m_FilterField.style.width = 180;
            m_FilterField.style.marginLeft = 8;
            m_FilterField.tooltip = "筛选 AreaBgmVolume 的运行或配置状态";
            m_FilterField.RegisterValueChangedCallback(_ => RefreshView());
            toolbar.Add(m_FilterField);

            Button refreshButton = new(RefreshData)
            {
                text = "刷新"
            };
            refreshButton.style.marginLeft = 8;
            refreshButton.style.width = 64;
            refreshButton.tooltip = "重新扫描当前场景中的 AreaBgmVolume";
            toolbar.Add(refreshButton);

            m_AutoRefreshLabel = new Label("自动刷新: 0.5s");
            m_AutoRefreshLabel.style.marginLeft = 10;
            m_AutoRefreshLabel.style.color = new Color(0.70f, 0.70f, 0.70f);
            toolbar.Add(m_AutoRefreshLabel);

            return toolbar;
        }

        private VisualElement BuildListPane()
        {
            VisualElement pane = new()
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

            pane.Add(BuildListHeader());

            m_ListView = new ListView
            {
                itemsSource = m_FilteredEntries,
                fixedItemHeight = 26,
                selectionType = SelectionType.Single,
                makeItem = MakeListItem,
                bindItem = BindListItem
            };
            m_ListView.style.flexGrow = 1;
            m_ListView.style.marginTop = 4;
            m_ListView.onSelectionChange += OnSelectionChanged;
            pane.Add(m_ListView);
            return pane;
        }

        private VisualElement BuildListHeader()
        {
            VisualElement header = CreateRow(new Color(0.20f, 0.20f, 0.20f), 22);
            header.Add(CreateHeaderLabel("状态", 82));
            header.Add(CreateHeaderLabel("对象", 170));
            header.Add(CreateHeaderLabel("Prio", 44));
            header.Add(CreateHeaderLabel("BGM", 48));
            header.Add(CreateHeaderLabel("玩家", 44));
            header.Add(CreateHeaderLabel("启用", 44));
            header.Add(CreateHeaderLabel("Trigger", 58));

            Label sceneLabel = CreateHeaderLabel("场景", 0);
            sceneLabel.style.flexGrow = 1;
            header.Add(sceneLabel);
            return header;
        }

        private VisualElement BuildDetailPane()
        {
            m_DetailPane = new ScrollView();
            m_DetailPane.style.flexGrow = 1;
            m_DetailPane.style.paddingLeft = 10;
            m_DetailPane.style.paddingRight = 10;
            m_DetailPane.style.paddingTop = 10;
            m_DetailPane.style.paddingBottom = 10;
            m_DetailPane.style.marginLeft = 4;
            m_DetailPane.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.75f);
            return m_DetailPane;
        }

        private void HandleEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - m_LastRefreshTime < 0.5d)
            {
                return;
            }

            m_LastRefreshTime = EditorApplication.timeSinceStartup;
            RefreshData();
        }

        private void RefreshData()
        {
            RefreshSoundSnapshot();
            RefreshVolumeEntries();
            RefreshView(true);
        }

        private void RefreshSoundSnapshot()
        {
            m_IsSoundManagerLoaded = Application.isPlaying && GameEntry.IsModuleLoaded<SoundManager>();
            m_SoundSnapshot = m_IsSoundManagerLoaded ? SoundManager.Instance.GetDebugSnapshot() : null;
        }

        private void RefreshVolumeEntries()
        {
            AreaBgmVolume selectedVolume = m_SelectedEntry?.Volume;
            m_AllEntries.Clear();

            AreaBgmVolume currentVolume = m_SoundSnapshot?.CurrentBgmVolume;
            HashSet<AreaBgmVolume> activeVolumes = new();
            if (m_SoundSnapshot?.ActiveBgmVolumes != null)
            {
                foreach (AreaBgmVolume volume in m_SoundSnapshot.Value.ActiveBgmVolumes)
                {
                    if (volume != null)
                    {
                        activeVolumes.Add(volume);
                    }
                }
            }

            foreach (AreaBgmVolume volume in Object.FindObjectsByType<AreaBgmVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume == null || !volume.gameObject.scene.IsValid())
                {
                    continue;
                }

                m_AllEntries.Add(VolumeEntry.Create(volume, activeVolumes.Contains(volume), volume == currentVolume));
            }

            m_AllEntries.Sort(CompareEntries);
            m_SelectedEntry = selectedVolume != null ? m_AllEntries.Find(entry => entry.Volume == selectedVolume) : null;
        }

        private void RefreshView(bool rebuildDetail = false)
        {
            RefreshFilteredEntries();
            RefreshSummary();
            RefreshList();
            if (rebuildDetail)
            {
                RefreshDetail();
            }
        }

        private void RefreshFilteredEntries()
        {
            m_FilteredEntries.Clear();
            string search = m_SearchField != null ? m_SearchField.value?.Trim() : string.Empty;
            string filter = m_FilterField != null ? m_FilterField.value : FilterAll;

            for (int i = 0; i < m_AllEntries.Count; i++)
            {
                VolumeEntry entry = m_AllEntries[i];
                if (!MatchesSearch(entry, search) || !MatchesFilter(entry, filter))
                {
                    continue;
                }

                m_FilteredEntries.Add(entry);
            }
        }

        private void RefreshSummary()
        {
            if (m_SummaryLabel == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                m_SummaryLabel.text = $"Edit Mode | 场景 Volume: {m_AllEntries.Count} | 进入 Play Mode 后显示 SoundManager 运行状态。";
                return;
            }

            if (!m_IsSoundManagerLoaded || m_SoundSnapshot == null)
            {
                m_SummaryLabel.text = $"Play Mode | SoundManager 未加载 | 场景 Volume: {m_AllEntries.Count}";
                return;
            }

            SoundManagerDebugSnapshot snapshot = m_SoundSnapshot.Value;
            string currentVolume = snapshot.CurrentBgmVolume != null ? snapshot.CurrentBgmVolume.name : "无";
            string clipName = string.IsNullOrEmpty(snapshot.CurrentBgmClipName) ? "无" : snapshot.CurrentBgmClipName;
            m_SummaryLabel.text =
                $"Play Mode | BGM开关: {(snapshot.BgmPaused ? "暂停" : "开启")} | 音量: {snapshot.BgmVolume:0.00} | BGM: {(snapshot.IsPlayingBgm ? "播放中" : "停止")} | 手动覆盖: {FormatBool(snapshot.ManualBgmOverride)} | " +
                $"Clip: {clipName} | 当前区域: {currentVolume} | 激活区域: {snapshot.ActiveBgmVolumes.Count} | 缓存音频: {snapshot.CachedAudioClipCount}";
        }

        private void RefreshList()
        {
            if (m_ListView == null)
            {
                return;
            }

            m_ListView.itemsSource = m_FilteredEntries;
            m_ListView.Rebuild();
        }

        private void RefreshDetail()
        {
            if (m_DetailPane == null)
            {
                return;
            }

            m_SelectedEntry = ResolveCurrentSelectedEntry();

            m_DetailPane.Clear();
            m_DetailPane.Add(BuildSoundManagerSection());

            if (m_SelectedEntry == null || m_SelectedEntry.Volume == null)
            {
                Label emptyLabel = new("请选择一个 AreaBgmVolume。");
                emptyLabel.style.marginTop = 12;
                emptyLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
                m_DetailPane.Add(emptyLabel);
                return;
            }

            m_DetailPane.Add(BuildVolumeDetailSection(m_SelectedEntry));
        }

        private VisualElement BuildSoundManagerSection()
        {
            VisualElement section = CreateSection("SoundManager");

            if (!Application.isPlaying)
            {
                section.Add(CreateInfoRow("运行状态", "Edit Mode"));
                section.Add(CreateInfoRow("说明", "进入 Play Mode 后显示 SoundManager 运行快照。"));
                return section;
            }

            if (!m_IsSoundManagerLoaded || m_SoundSnapshot == null)
            {
                section.Add(CreateInfoRow("运行状态", "SoundManager 未加载"));
                return section;
            }

            SoundManagerDebugSnapshot snapshot = m_SoundSnapshot.Value;
            section.Add(BuildBgmControlRow(snapshot));
            section.Add(BuildBgmVolumeRow(snapshot));
            section.Add(CreateInfoRow("BGM 开关", snapshot.BgmPaused ? "暂停" : "开启"));
            section.Add(CreateInfoRow("BGM 音量", snapshot.BgmVolume.ToString("0.00")));
            section.Add(CreateInfoRow("BGM", snapshot.IsPlayingBgm ? "播放中" : "停止"));
            section.Add(CreateInfoRow("手动覆盖", FormatBool(snapshot.ManualBgmOverride)));
            section.Add(CreateInfoRow("当前 Clip", string.IsNullOrEmpty(snapshot.CurrentBgmClipName) ? "无" : snapshot.CurrentBgmClipName));
            section.Add(CreateInfoRow("当前区域", snapshot.CurrentBgmVolume != null ? snapshot.CurrentBgmVolume.name : "无"));
            section.Add(CreateInfoRow("激活区域数", snapshot.ActiveBgmVolumes.Count.ToString()));
            section.Add(CreateInfoRow("缓存音频数", snapshot.CachedAudioClipCount.ToString()));
            return section;
        }

        private VisualElement BuildVolumeDetailSection(VolumeEntry entry)
        {
            VisualElement section = CreateSection("AreaBgmVolume");
            AreaBgmVolumeDebugSnapshot snapshot = entry.DebugSnapshot;

            ObjectField objectField = new()
            {
                objectType = typeof(GameObject),
                value = entry.Volume.gameObject
            };
            objectField.SetEnabled(false);
            ConfigureInspectorField(objectField);
            section.Add(CreateInspectorFieldRow("对象", objectField));

            ObjectField colliderField = new()
            {
                objectType = typeof(Collider),
                value = entry.Collider
            };
            colliderField.SetEnabled(false);
            ConfigureInspectorField(colliderField);
            section.Add(CreateInspectorFieldRow("Collider", colliderField));

            IntegerField priorityField = new()
            {
                value = snapshot.Priority
            };
            ConfigureInspectorField(priorityField);
            priorityField.tooltip = "区域背景音乐优先级，数值越大越优先。";
            priorityField.RegisterValueChangedCallback(evt => SetAreaBgmPriority(entry.Volume, evt.newValue));
            section.Add(CreateInspectorFieldRow("Priority", priorityField));

            Toggle randomOnEnterToggle = new()
            {
                value = snapshot.RandomOnEnter
            };
            randomOnEnterToggle.tooltip = "成为当前区域时是否随机选择一首背景音乐。";
            randomOnEnterToggle.RegisterValueChangedCallback(evt => SetAreaBgmRandomOnEnter(entry.Volume, evt.newValue));
            section.Add(CreateInspectorFieldRow("Random On Enter", randomOnEnterToggle));

            section.Add(CreateInspectorReadonlyRow("运行状态", BuildStatusText(entry)));
            section.Add(CreateInspectorReadonlyRow("玩家 Collider", snapshot.PlayerColliderCount.ToString()));
            section.Add(CreateInspectorReadonlyRow("有效 BGM", $"{snapshot.ValidPathCount} / {snapshot.BgmPaths.Count}"));
            section.Add(CreateInspectorReadonlyRow("Trigger", entry.IsTrigger ? "是" : "否"));
            section.Add(CreateInspectorReadonlyRow("场景", entry.SceneName));

            VisualElement buttonRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 4,
                    marginBottom = 8
                }
            };

            Button selectButton = new(() => Selection.activeObject = entry.Volume.gameObject)
            {
                text = "选中对象"
            };
            selectButton.tooltip = "在 Hierarchy 中选中该 AreaBgmVolume 所在对象";
            buttonRow.Add(selectButton);

            Button pingButton = new(() => EditorGUIUtility.PingObject(entry.Volume.gameObject))
            {
                text = "Ping 对象"
            };
            pingButton.style.marginLeft = 6;
            pingButton.tooltip = "在 Hierarchy 中高亮该对象";
            buttonRow.Add(pingButton);
            section.Add(buttonRow);

            Label clipTitle = new("BGM 路径列表");
            clipTitle.style.marginTop = 10;
            clipTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(clipTitle);

            section.Add(BuildBgmClipPathList(entry.Volume));

            return section;
        }

        private VisualElement BuildBgmClipPathList(AreaBgmVolume volume)
        {
            VisualElement container = new()
            {
                style =
                {
                    marginTop = 4
                }
            };

            SerializedObject serializedObject = new(volume);
            SerializedProperty clipsProperty = serializedObject.FindProperty("bgmClips");
            if (clipsProperty == null || !clipsProperty.isArray)
            {
                container.Add(CreateMutedLabel("找不到 bgmClips 字段。"));
                return container;
            }

            if (clipsProperty.arraySize == 0)
            {
                container.Add(CreateInspectorReadonlyRow("BGM", "无"));
            }
            else
            {
                for (int i = 0; i < clipsProperty.arraySize; i++)
                {
                    container.Add(BuildBgmClipPathField(volume, i));
                }
            }

            Button addButton = new(() => AddBgmClipPath(volume))
            {
                text = "添加 BGM"
            };
            addButton.tooltip = "向该 AreaBgmVolume 的 bgmClips 数组末尾添加一个空槽位";
            addButton.style.marginTop = 6;
            addButton.style.width = 88;
            container.Add(CreateInspectorFieldRow(string.Empty, addButton));

            return container;
        }

        private VisualElement BuildBgmClipPathField(AreaBgmVolume volume, int index)
        {
            SerializedObject serializedObject = new(volume);
            SerializedProperty clipsProperty = serializedObject.FindProperty("bgmClips");
            SerializedProperty elementProperty = clipsProperty.GetArrayElementAtIndex(index);
            string path = elementProperty.stringValue;
            AudioClip clip = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            VisualElement row = CreateInspectorRow($"BGM {index}");

            ObjectField clipField = new()
            {
                objectType = typeof(AudioClip),
                allowSceneObjects = false,
                value = clip
            };
            clipField.tooltip = "修改后会同步写回该 AreaBgmVolume 的 bgmClips 路径";
            ConfigureInspectorField(clipField);
            row.Add(clipField);

            Button removeButton = new(() => RemoveBgmClipPath(volume, index))
            {
                text = "-"
            };
            removeButton.tooltip = "从该 AreaBgmVolume 的 bgmClips 数组中删除这一项";
            removeButton.style.width = 24;
            removeButton.style.marginLeft = 6;
            row.Add(removeButton);

            if (!string.IsNullOrWhiteSpace(path) && clip == null)
            {
                Label missingLabel = CreateMutedLabel("Missing");
                missingLabel.tooltip = path;
                missingLabel.style.width = 54;
                missingLabel.style.marginLeft = 6;
                row.Add(missingLabel);
            }

            clipField.RegisterValueChangedCallback(evt =>
            {
                AudioClip newClip = evt.newValue as AudioClip;
                SetBgmClipPath(volume, index, newClip != null ? AssetDatabase.GetAssetPath(newClip) : string.Empty);
            });

            return row;
        }

        private VisualElement BuildBgmControlRow(SoundManagerDebugSnapshot snapshot)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 2,
                    marginBottom = 8
                }
            };

            bool shouldResume = snapshot.BgmPaused || !snapshot.IsPlayingBgm;
            Button bgmToggleButton = new(() => ToggleBgmFromDebugger(shouldResume))
            {
                text = shouldResume ? "▶" : "■"
            };
            bgmToggleButton.tooltip = shouldResume
                ? "调用 SoundManager.ResumeBgm()，恢复当前应生效的区域 BGM"
                : "调用 SoundManager.StopBgm()，暂停并淡出停止所有 BGM";
            bgmToggleButton.style.backgroundColor = shouldResume
                ? new Color(0.24f, 0.50f, 0.85f, 1f)
                : new Color(0.78f, 0.22f, 0.22f, 1f);
            bgmToggleButton.style.color = Color.white;
            bgmToggleButton.style.width = BgmControlButtonSize;
            bgmToggleButton.style.minWidth = BgmControlButtonSize;
            bgmToggleButton.style.maxWidth = BgmControlButtonSize;
            bgmToggleButton.style.height = BgmControlButtonSize;
            bgmToggleButton.style.minHeight = BgmControlButtonSize;
            bgmToggleButton.style.maxHeight = BgmControlButtonSize;
            bgmToggleButton.style.fontSize = shouldResume ? 11f : 12f;
            bgmToggleButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            bgmToggleButton.style.paddingLeft = 0;
            bgmToggleButton.style.paddingRight = 0;
            bgmToggleButton.style.paddingTop = 0;
            bgmToggleButton.style.paddingBottom = 0;
            bgmToggleButton.style.flexShrink = 0;
            bgmToggleButton.SetEnabled(snapshot.BgmPaused || snapshot.IsPlayingBgm);
            bgmToggleButton.style.opacity = snapshot.BgmPaused || snapshot.IsPlayingBgm ? 1f : 0.45f;
            row.Add(bgmToggleButton);

            return row;
        }

        private VisualElement BuildBgmVolumeRow(SoundManagerDebugSnapshot snapshot)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            Label title = new("音量");
            title.style.width = 36;
            title.style.color = new Color(0.72f, 0.72f, 0.72f);
            row.Add(title);

            Slider slider = new(0f, 1f)
            {
                value = snapshot.BgmVolume
            };
            slider.style.flexGrow = 1;
            slider.style.minWidth = 100;
            slider.tooltip = "仅修改当前 Play Mode 运行时的 BGM 音量，不写入场景或资源配置。";
            row.Add(slider);

            Label valueLabel = new(snapshot.BgmVolume.ToString("0.00"));
            valueLabel.style.width = 42;
            valueLabel.style.marginLeft = 6;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = SetBgmVolumeFromDebugger(evt.newValue).ToString("0.00");
            });

            return row;
        }

        private void ToggleBgmFromDebugger(bool shouldResume)
        {
            if (!Application.isPlaying || !GameEntry.IsModuleLoaded<SoundManager>())
            {
                return;
            }

            if (shouldResume)
            {
                SoundManager.Instance.ResumeBgm();
            }
            else
            {
                SoundManager.Instance.StopBgm();
            }

            RefreshData();
        }

        private float SetBgmVolumeFromDebugger(float volume)
        {
            if (!Application.isPlaying || !GameEntry.IsModuleLoaded<SoundManager>())
            {
                return Mathf.Clamp01(volume);
            }

            SoundManager.Instance.SetBgmVolume(volume);
            RefreshSoundSnapshot();
            RefreshSummary();
            return SoundManager.Instance.GetBgmVolume();
        }

        private void SetBgmClipPath(AreaBgmVolume volume, int index, string path)
        {
            if (volume == null)
            {
                return;
            }

            SerializedObject serializedObject = new(volume);
            SerializedProperty clipsProperty = serializedObject.FindProperty("bgmClips");
            if (clipsProperty == null || !clipsProperty.isArray || index < 0 || index >= clipsProperty.arraySize)
            {
                return;
            }

            Undo.RecordObject(volume, "Set Area BGM Clip");
            clipsProperty.GetArrayElementAtIndex(index).stringValue = path;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(volume);
            SyncRuntimeAreaBgmVolume(volume);
            RefreshData();
        }

        private void SetAreaBgmPriority(AreaBgmVolume volume, int priority)
        {
            if (volume == null)
            {
                return;
            }

            SerializedObject serializedObject = new(volume);
            SerializedProperty priorityProperty = serializedObject.FindProperty("priority");
            if (priorityProperty == null)
            {
                return;
            }

            if (priorityProperty.intValue == priority)
            {
                return;
            }

            Undo.RecordObject(volume, "Set Area BGM Priority");
            priorityProperty.intValue = priority;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(volume);
            SyncRuntimeAreaBgmVolume(volume);
            RefreshData();
        }

        private void SetAreaBgmRandomOnEnter(AreaBgmVolume volume, bool randomOnEnter)
        {
            if (volume == null)
            {
                return;
            }

            SerializedObject serializedObject = new(volume);
            SerializedProperty randomOnEnterProperty = serializedObject.FindProperty("randomOnEnter");
            if (randomOnEnterProperty == null)
            {
                return;
            }

            if (randomOnEnterProperty.boolValue == randomOnEnter)
            {
                return;
            }

            Undo.RecordObject(volume, "Set Area BGM Random On Enter");
            randomOnEnterProperty.boolValue = randomOnEnter;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(volume);
            SyncRuntimeAreaBgmVolume(volume);
            RefreshData();
        }

        private void AddBgmClipPath(AreaBgmVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            SerializedObject serializedObject = new(volume);
            SerializedProperty clipsProperty = serializedObject.FindProperty("bgmClips");
            if (clipsProperty == null || !clipsProperty.isArray)
            {
                return;
            }

            Undo.RecordObject(volume, "Add Area BGM Clip");
            int index = clipsProperty.arraySize;
            clipsProperty.InsertArrayElementAtIndex(index);
            clipsProperty.GetArrayElementAtIndex(index).stringValue = string.Empty;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(volume);
            SyncRuntimeAreaBgmVolume(volume);
            RefreshData();
        }

        private void RemoveBgmClipPath(AreaBgmVolume volume, int index)
        {
            if (volume == null)
            {
                return;
            }

            SerializedObject serializedObject = new(volume);
            SerializedProperty clipsProperty = serializedObject.FindProperty("bgmClips");
            if (clipsProperty == null || !clipsProperty.isArray || index < 0 || index >= clipsProperty.arraySize)
            {
                return;
            }

            Undo.RecordObject(volume, "Remove Area BGM Clip");
            clipsProperty.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(volume);
            SyncRuntimeAreaBgmVolume(volume);
            RefreshData();
        }

        private void SyncRuntimeAreaBgmVolume(AreaBgmVolume volume)
        {
            if (!Application.isPlaying || volume == null || !GameEntry.IsModuleLoaded<SoundManager>())
            {
                return;
            }

            SoundManager.Instance.RefreshBgmVolume(volume);
        }

        private VisualElement MakeListItem()
        {
            VisualElement row = CreateRow(Color.clear, 24);
            row.Add(CreateCellLabel("status", 82));
            row.Add(CreateCellLabel("name", 170));
            row.Add(CreateCellLabel("priority", 44));
            row.Add(CreateCellLabel("bgm", 48));
            row.Add(CreateCellLabel("player", 44));
            row.Add(CreateCellLabel("enabled", 44));
            row.Add(CreateCellLabel("trigger", 58));

            Label scene = CreateCellLabel("scene", 0);
            scene.style.flexGrow = 1;
            row.Add(scene);
            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            VolumeEntry entry = m_FilteredEntries[index];
            element.style.backgroundColor = entry == m_SelectedEntry
                ? new Color(0.24f, 0.42f, 0.72f, 0.45f)
                : index % 2 == 0
                    ? new Color(0.24f, 0.24f, 0.24f, 0.10f)
                    : new Color(0.31f, 0.31f, 0.31f, 0.18f);
            element.tooltip = entry.Volume != null ? entry.Volume.gameObject.name : string.Empty;

            List<Label> labels = element.Query<Label>().ToList();
            labels[0].text = BuildStatusText(entry);
            labels[0].style.color = entry.IsCurrent ? new Color(0.40f, 0.90f, 0.45f) : entry.IsActive ? new Color(0.85f, 0.80f, 0.35f) : Color.white;
            labels[1].text = entry.Volume != null ? entry.Volume.name : "<Missing>";
            labels[2].text = entry.DebugSnapshot.Priority.ToString();
            labels[3].text = entry.DebugSnapshot.ValidPathCount.ToString();
            labels[4].text = entry.DebugSnapshot.PlayerColliderCount.ToString();
            labels[5].text = entry.IsEnabled ? "是" : "否";
            labels[6].text = entry.IsTrigger ? "是" : "否";
            labels[7].text = entry.SceneName;
        }

        private void OnSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                m_SelectedEntry = item as VolumeEntry;
                RefreshView(false);
                RefreshDetail();
                return;
            }

            m_SelectedEntry = null;
            RefreshView(false);
            RefreshDetail();
        }

        private static int CompareEntries(VolumeEntry left, VolumeEntry right)
        {
            int currentResult = right.IsCurrent.CompareTo(left.IsCurrent);
            if (currentResult != 0)
            {
                return currentResult;
            }

            int activeResult = right.IsActive.CompareTo(left.IsActive);
            if (activeResult != 0)
            {
                return activeResult;
            }

            int priorityResult = right.DebugSnapshot.Priority.CompareTo(left.DebugSnapshot.Priority);
            return priorityResult != 0
                ? priorityResult
                : string.Compare(left.Volume != null ? left.Volume.name : string.Empty, right.Volume != null ? right.Volume.name : string.Empty, StringComparison.Ordinal);
        }

        private static bool MatchesSearch(VolumeEntry entry, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            if (entry.Volume != null && entry.Volume.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (entry.SceneName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            foreach (string path in entry.DebugSnapshot.BgmPaths)
            {
                if (!string.IsNullOrEmpty(path) && path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesFilter(VolumeEntry entry, string filter)
        {
            return filter switch
            {
                FilterCurrent => entry.IsCurrent,
                FilterActive => entry.IsActive,
                FilterPlayerInside => entry.DebugSnapshot.PlayerColliderCount > 0,
                FilterNoBgm => !entry.DebugSnapshot.HasBgmClip,
                FilterColliderNotTrigger => !entry.IsTrigger,
                FilterDisabled => !entry.IsEnabled,
                _ => true
            };
        }

        private VolumeEntry ResolveCurrentSelectedEntry()
        {
            if (m_SelectedEntry == null || m_SelectedEntry.Volume == null)
            {
                return null;
            }

            return m_AllEntries.Find(entry => entry.Volume == m_SelectedEntry.Volume);
        }

        private static VisualElement CreateSection(string title)
        {
            VisualElement section = new()
            {
                style =
                {
                    marginBottom = 14,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = new Color(0.11f, 0.11f, 0.11f, 0.50f)
                }
            };

            Label titleLabel = new(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 6;
            section.Add(titleLabel);
            return section;
        }

        private static VisualElement CreateInfoRow(string labelText, string valueText)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    minHeight = 22,
                    alignItems = Align.Center
                }
            };

            Label label = new(labelText);
            label.style.width = 110;
            label.style.color = new Color(0.72f, 0.72f, 0.72f);
            row.Add(label);

            Label value = new(valueText);
            value.style.flexGrow = 1;
            value.style.whiteSpace = WhiteSpace.Normal;
            row.Add(value);
            return row;
        }

        private static VisualElement CreateInspectorFieldRow(string labelText, VisualElement field)
        {
            VisualElement row = CreateInspectorRow(labelText);
            field.style.flexGrow = 1;
            row.Add(field);
            return row;
        }

        private static VisualElement CreateInspectorReadonlyRow(string labelText, string valueText)
        {
            Label value = new(valueText);
            value.style.flexGrow = 1;
            value.style.whiteSpace = WhiteSpace.Normal;
            value.style.color = new Color(0.86f, 0.86f, 0.86f);
            return CreateInspectorFieldRow(labelText, value);
        }

        private static VisualElement CreateInspectorRow(string labelText)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    minHeight = 24,
                    alignItems = Align.Center,
                    marginTop = 1,
                    marginBottom = 1
                }
            };

            Label label = new(labelText);
            label.style.width = InspectorLabelWidth;
            label.style.minWidth = InspectorLabelWidth;
            label.style.maxWidth = InspectorLabelWidth;
            label.style.marginRight = 6;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.color = new Color(0.72f, 0.72f, 0.72f);
            row.Add(label);
            return row;
        }

        private static void ConfigureInspectorField(VisualElement field)
        {
            field.style.flexGrow = 1;
            field.style.minWidth = 0;
            field.style.marginLeft = 0;
            field.style.marginRight = 0;
        }

        private static VisualElement CreateRow(Color color, float height)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = height,
                    paddingLeft = 4,
                    paddingRight = 4,
                    backgroundColor = color
                }
            };
            return row;
        }

        private static Label CreateHeaderLabel(string text, float width)
        {
            Label label = CreateCellLabel(text, width);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.82f, 0.82f, 0.82f);
            return label;
        }

        private static Label CreateCellLabel(string text, float width)
        {
            Label label = new(text);
            label.style.width = width;
            label.style.overflow = Overflow.Hidden;
            label.style.marginRight = 4;
            return label;
        }

        private static Label CreateMutedLabel(string text)
        {
            Label label = new(text);
            label.style.color = new Color(0.70f, 0.70f, 0.70f);
            return label;
        }

        private static string FormatBool(bool value)
        {
            return value ? "是" : "否";
        }

        private static string BuildStatusText(VolumeEntry entry)
        {
            if (entry.IsCurrent)
            {
                return "当前";
            }

            if (entry.IsActive)
            {
                return "激活";
            }

            if (entry.DebugSnapshot.PlayerColliderCount > 0)
            {
                return "玩家在内";
            }

            if (!entry.DebugSnapshot.HasBgmClip)
            {
                return "无 BGM";
            }

            if (!entry.IsEnabled)
            {
                return "禁用";
            }

            return "待机";
        }

        private sealed class VolumeEntry
        {
            private VolumeEntry(
                AreaBgmVolume volume,
                Collider collider,
                AreaBgmVolumeDebugSnapshot debugSnapshot,
                bool isActive,
                bool isCurrent,
                bool isEnabled,
                bool isTrigger,
                string sceneName)
            {
                Volume = volume;
                Collider = collider;
                DebugSnapshot = debugSnapshot;
                IsActive = isActive;
                IsCurrent = isCurrent;
                IsEnabled = isEnabled;
                IsTrigger = isTrigger;
                SceneName = sceneName;
            }

            public AreaBgmVolume Volume { get; }
            public Collider Collider { get; }
            public AreaBgmVolumeDebugSnapshot DebugSnapshot { get; }
            public bool IsActive { get; }
            public bool IsCurrent { get; }
            public bool IsEnabled { get; }
            public bool IsTrigger { get; }
            public string SceneName { get; }

            public static VolumeEntry Create(AreaBgmVolume volume, bool isActive, bool isCurrent)
            {
                Collider collider = volume.GetComponent<Collider>();
                Scene scene = volume.gameObject.scene;
                return new VolumeEntry(
                    volume,
                    collider,
                    volume.GetDebugSnapshot(),
                    isActive,
                    isCurrent,
                    volume.gameObject.activeInHierarchy && volume.enabled,
                    collider != null && collider.isTrigger,
                    scene.IsValid() ? scene.name : "<No Scene>");
            }
        }
    }
}
