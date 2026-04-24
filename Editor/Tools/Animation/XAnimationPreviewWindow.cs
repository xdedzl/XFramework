#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;

namespace XFramework.Editor
{
    public sealed class XAnimationPreviewWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Tools/XAnimation Preview";
        private const string DefaultSampleAssetPath = "Assets/Animation/XAnimationSamples/XAnimationPreview_WolfLite.xasset";
        private const string DefaultPrefabPath = "Assets/ThirdParty/malbers-animations/Animal Controller/Wolf Lite/Wolf Lite.prefab";

        private readonly Dictionary<string, Label> m_ChannelStateLabels = new(StringComparer.Ordinal);

        private TextAsset m_PendingAsset;
        private GameObject m_PendingPrefab;
        private bool m_PendingAutoLoad;

        private ObjectField m_PrefabField;
        private ObjectField m_AssetField;
        private Image m_PreviewImage;
        private Label m_StatusLabel;
        private FloatField m_PlaySpeedField;
        private Toggle m_RootMotionToggle;
        private ScrollView m_ClipListView;
        private VisualElement m_ChannelControlsContainer;
        private VisualElement m_ChannelStatesContainer;
        private ScrollView m_CueLogContainer;

        private XAnimationEditorPreviewSession m_Session;
        private double m_LastEditorTime;
        private bool m_IsPreviewDragging;
        private Vector2 m_LastPreviewMousePosition;
        private int m_LastCueLogCount = -1;

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            XAnimationPreviewWindow window = GetWindow<XAnimationPreviewWindow>();
            window.titleContent = new GUIContent("XAnimation Preview");
            window.minSize = new Vector2(1180f, 680f);
            window.Show();
        }

        public static XAnimationPreviewWindow ShowWindow(TextAsset animationAsset, GameObject prefab = null, bool autoLoad = true)
        {
            XAnimationPreviewWindow window = GetWindow<XAnimationPreviewWindow>();
            window.titleContent = new GUIContent("XAnimation Preview");
            window.minSize = new Vector2(1180f, 680f);
            window.SetPendingOpenRequest(animationAsset, prefab, autoLoad);
            window.Show();
            window.Focus();
            return window;
        }

        [OnOpenAsset(10)]
        public static bool OpenAsset(int instanceID, int line)
        {
            UnityEngine.Object target = EditorUtility.InstanceIDToObject(instanceID);
            if (target is not TextAsset textAsset)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(textAsset);
            if (!string.Equals(Path.GetExtension(assetPath), ".xasset", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            XAnimationPreviewWindow existingWindow = HasOpenInstances<XAnimationPreviewWindow>()
                ? GetWindow<XAnimationPreviewWindow>()
                : null;
            GameObject prefab = existingWindow?.m_PrefabField?.value as GameObject;
            ShowWindow(textAsset, prefab, autoLoad: true);
            return true;
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
            m_LastEditorTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            DisposeSession();
        }

        public void CreateGUI()
        {
            BuildUI();
            ApplyDefaultSelections();
            SetStatus("拖入 prefab 和 .xasset，或直接加载默认样例。");
            ApplyPendingOpenRequest();
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
            root.style.flexDirection = FlexDirection.Column;

            root.Add(BuildToolbar());

            m_StatusLabel = new Label();
            m_StatusLabel.style.marginTop = 4;
            m_StatusLabel.style.marginBottom = 6;
            m_StatusLabel.style.color = new Color(0.8f, 0.8f, 0.8f);
            root.Add(m_StatusLabel);

            VisualElement body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Row;
            root.Add(body);

            body.Add(BuildResourcePane());
            body.Add(BuildPreviewPane());
            body.Add(BuildDebugPane());
        }

        private VisualElement BuildToolbar()
        {
            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;

            Button loadDefaultButton = new(LoadDefaultSample)
            {
                text = "加载默认样例"
            };
            toolbar.Add(loadDefaultButton);

            Button loadButton = new(LoadPreview)
            {
                text = "加载预览"
            };
            loadButton.style.marginLeft = 8;
            toolbar.Add(loadButton);

            Button reloadButton = new(LoadPreview)
            {
                text = "重载"
            };
            reloadButton.style.marginLeft = 8;
            toolbar.Add(reloadButton);

            Button clearButton = new(ClearPreview)
            {
                text = "清空"
            };
            clearButton.style.marginLeft = 8;
            toolbar.Add(clearButton);

            return toolbar;
        }

        private VisualElement BuildResourcePane()
        {
            VisualElement pane = CreatePane(280f);

            Label title = new("资源")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 8
                }
            };
            pane.Add(title);

            m_PrefabField = new ObjectField("Prefab")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            pane.Add(m_PrefabField);

            m_AssetField = new ObjectField("XAnimationAsset (.xasset)")
            {
                objectType = typeof(TextAsset),
                allowSceneObjects = false
            };
            m_AssetField.style.marginTop = 6;
            pane.Add(m_AssetField);

            Label help = new("`.xasset` 会以 TextAsset 形式拖入，再由预览工具解析为 XAnimationAsset。");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.marginTop = 8;
            help.style.color = new Color(0.75f, 0.75f, 0.75f);
            pane.Add(help);

            return pane;
        }

        private VisualElement BuildPreviewPane()
        {
            VisualElement pane = CreatePane();
            pane.style.marginLeft = 6;
            pane.style.marginRight = 6;

            Label title = new("预览")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 8
                }
            };
            pane.Add(title);

            m_PreviewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            m_PreviewImage.style.flexGrow = 1;
            m_PreviewImage.style.backgroundColor = new Color(0.11f, 0.11f, 0.12f, 1f);
            m_PreviewImage.style.borderTopWidth = 1;
            m_PreviewImage.style.borderBottomWidth = 1;
            m_PreviewImage.style.borderLeftWidth = 1;
            m_PreviewImage.style.borderRightWidth = 1;
            m_PreviewImage.style.borderTopColor = Color.gray;
            m_PreviewImage.style.borderBottomColor = Color.gray;
            m_PreviewImage.style.borderLeftColor = Color.gray;
            m_PreviewImage.style.borderRightColor = Color.gray;
            RegisterPreviewEvents();
            pane.Add(m_PreviewImage);

            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.marginTop = 8;
            controls.style.alignItems = Align.Center;
            pane.Add(controls);

            Button resetTransformButton = new(ResetPreviewTransform)
            {
                text = "重置位置"
            };
            controls.Add(resetTransformButton);

            Button resetCameraButton = new(ResetPreviewCamera)
            {
                text = "重置视角"
            };
            resetCameraButton.style.marginLeft = 6;
            controls.Add(resetCameraButton);

            Label hint = new("左键拖拽旋转，滚轮缩放。");
            hint.style.marginLeft = 10;
            hint.style.color = new Color(0.75f, 0.75f, 0.75f);
            controls.Add(hint);

            return pane;
        }

        private VisualElement BuildDebugPane()
        {
            VisualElement pane = CreatePane(320f);

            Label title = new("调试")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 8
                }
            };
            pane.Add(title);

            VisualElement topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;
            pane.Add(topRow);

            m_PlaySpeedField = new FloatField("播放速度")
            {
                value = 1f
            };
            m_PlaySpeedField.style.flexGrow = 1;
            topRow.Add(m_PlaySpeedField);

            m_RootMotionToggle = new Toggle("Root Motion")
            {
                value = false
            };
            m_RootMotionToggle.style.marginLeft = 8;
            m_RootMotionToggle.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_Session.SetRootMotionEnabled(evt.newValue);
                SetStatus(evt.newValue ? "已开启 Root Motion 预览。" : "已关闭 Root Motion，预览对象回到初始位置。");
            });
            topRow.Add(m_RootMotionToggle);

            pane.Add(CreateSectionTitle("Clips"));
            m_ClipListView = new ScrollView();
            m_ClipListView.style.height = 180;
            pane.Add(m_ClipListView);

            VisualElement clipButtons = new VisualElement();
            clipButtons.style.flexDirection = FlexDirection.Row;
            clipButtons.style.marginTop = 6;
            pane.Add(clipButtons);

            Button stopAllButton = new(StopAllClips)
            {
                text = "停止全部"
            };
            clipButtons.Add(stopAllButton);

            pane.Add(CreateSectionTitle("Channels"));
            m_ChannelControlsContainer = new VisualElement();
            pane.Add(m_ChannelControlsContainer);

            pane.Add(CreateSectionTitle("Channel States"));
            m_ChannelStatesContainer = new VisualElement();
            pane.Add(m_ChannelStatesContainer);

            pane.Add(CreateSectionTitle("Cue Log"));
            m_CueLogContainer = new ScrollView();
            m_CueLogContainer.style.flexGrow = 1;
            pane.Add(m_CueLogContainer);

            return pane;
        }

        private void RegisterPreviewEvents()
        {
            m_PreviewImage.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_IsPreviewDragging = true;
                m_LastPreviewMousePosition = evt.localMousePosition;
                m_PreviewImage.CaptureMouse();
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!m_IsPreviewDragging || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                Vector2 delta = evt.localMousePosition - m_LastPreviewMousePosition;
                m_LastPreviewMousePosition = evt.localMousePosition;
                m_Session.Orbit(delta);
                RenderPreview();
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                m_IsPreviewDragging = false;
                if (m_PreviewImage.HasMouseCapture())
                {
                    m_PreviewImage.ReleaseMouse();
                }
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<WheelEvent>(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_Session.Zoom(evt.delta.y);
                RenderPreview();
                evt.StopPropagation();
            });

            m_PreviewImage.RegisterCallback<GeometryChangedEvent>(_ => RenderPreview());
        }

        private void HandleEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - m_LastEditorTime);
            m_LastEditorTime = now;

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_Session.Update(deltaTime);
            RenderPreview();
            RefreshChannelStates();
            RefreshCueLogView();
            Repaint();
        }

        private void ApplyDefaultSelections()
        {
            if (m_PrefabField.value == null)
            {
                m_PrefabField.value = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);
            }

            if (m_AssetField.value == null)
            {
                m_AssetField.value = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultSampleAssetPath);
            }
        }

        private void SetPendingOpenRequest(TextAsset animationAsset, GameObject prefab, bool autoLoad)
        {
            m_PendingAsset = animationAsset;
            m_PendingPrefab = prefab;
            m_PendingAutoLoad = autoLoad;

            if (m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            ApplyPendingOpenRequest();
        }

        private void ApplyPendingOpenRequest()
        {
            if (m_AssetField == null || m_PrefabField == null)
            {
                return;
            }

            bool hasPendingRequest = m_PendingAsset != null || m_PendingPrefab != null || m_PendingAutoLoad;
            if (!hasPendingRequest)
            {
                return;
            }

            if (m_PendingAsset != null)
            {
                m_AssetField.value = m_PendingAsset;
            }

            if (m_PendingPrefab != null)
            {
                m_PrefabField.value = m_PendingPrefab;
            }
            else if (m_PrefabField.value == null)
            {
                m_PrefabField.value = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);
            }

            bool shouldAutoLoad = m_PendingAutoLoad && m_AssetField.value != null;

            m_PendingAsset = null;
            m_PendingPrefab = null;
            m_PendingAutoLoad = false;

            if (shouldAutoLoad)
            {
                LoadPreview();
            }
        }

        private void LoadDefaultSample()
        {
            ApplyDefaultSelections();
            LoadPreview();
        }

        private void LoadPreview()
        {
            try
            {
                GameObject prefab = m_PrefabField.value as GameObject;
                TextAsset assetText = m_AssetField.value as TextAsset;
                if (prefab == null)
                {
                    throw new XFrameworkException("请选择一个 prefab 资源。");
                }

                if (assetText == null)
                {
                    throw new XFrameworkException("请选择一个 .xasset 资源。");
                }

                string assetPath = AssetDatabase.GetAssetPath(assetText);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    throw new XFrameworkException("无法获取 XAnimationAsset 的资源路径。");
                }

                DisposeSession();
                m_Session = new XAnimationEditorPreviewSession();
                m_Session.Load(prefab, assetPath);
                m_RootMotionToggle.SetValueWithoutNotify(false);
                RebuildClipList();
                RebuildChannelControls();
                RefreshChannelStates();
                RefreshCueLogView(force: true);
                SetStatus("预览已加载。");
                RenderPreview();
            }
            catch (Exception ex)
            {
                DisposeSession();
                ClearDebugViews();
                SetStatus(ex.Message, true);
            }
        }

        private void ClearPreview()
        {
            DisposeSession();
            ClearDebugViews();
            SetStatus("预览已清空。");
        }

        private void StopAllClips()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_Session.StopAll();
            SetStatus("已停止全部通道。");
        }

        private void ResetPreviewTransform()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_Session.ResetTransform();
            SetStatus("预览对象已回到初始位置。");
        }

        private void ResetPreviewCamera()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            m_Session.ResetCamera();
            RenderPreview();
            SetStatus("预览视角已重置。");
        }

        private void RenderPreview()
        {
            if (m_Session == null || !m_Session.IsLoaded || m_PreviewImage == null)
            {
                if (m_PreviewImage != null)
                {
                    m_PreviewImage.image = null;
                }
                return;
            }

            Rect rect = m_PreviewImage.contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            m_Session.Render(rect.size);
            m_PreviewImage.image = m_Session.PreviewTexture;
        }

        private void RebuildClipList()
        {
            m_ClipListView.Clear();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            Dictionary<string, List<XAnimationCompiledClip>> clipsByChannel = new(StringComparer.Ordinal);
            for (int i = 0; i < clips.Count; i++)
            {
                XAnimationCompiledClip clip = (XAnimationCompiledClip)clips[i];
                if (!clipsByChannel.TryGetValue(clip.Config.defaultChannel, out List<XAnimationCompiledClip> channelClips))
                {
                    channelClips = new List<XAnimationCompiledClip>();
                    clipsByChannel.Add(clip.Config.defaultChannel, channelClips);
                }

                channelClips.Add(clip);
            }

            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = (XAnimationCompiledChannel)channels[i];
                if (!clipsByChannel.TryGetValue(channel.Name, out List<XAnimationCompiledClip> channelClips) || channelClips.Count == 0)
                {
                    continue;
                }

                VisualElement group = new VisualElement();
                group.style.marginBottom = 8;

                Label groupTitle = new(channel.Name);
                groupTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                groupTitle.style.marginBottom = 4;
                group.Add(groupTitle);

                for (int clipIndex = 0; clipIndex < channelClips.Count; clipIndex++)
                {
                    XAnimationCompiledClip clip = channelClips[clipIndex];
                    group.Add(CreateClipRow(clip));
                }

                m_ClipListView.Add(group);
            }
        }

        private VisualElement CreateClipRow(XAnimationCompiledClip clip)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            Label label = new(clip.Key);
            label.style.flexGrow = 1;
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);

            Button playButton = new(() =>
            {
                m_Session.Play(clip.Key, m_PlaySpeedField.value);
                SetStatus($"正在播放 {clip.Key}。");
            })
            {
                text = "播放"
            };
            row.Add(playButton);

            return row;
        }

        private void RebuildChannelControls()
        {
            m_ChannelControlsContainer.Clear();
            m_ChannelStateLabels.Clear();
            m_ChannelStatesContainer.Clear();

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = (XAnimationCompiledChannel)channels[i];

                VisualElement controlRow = new VisualElement();
                controlRow.style.flexDirection = FlexDirection.Column;
                controlRow.style.marginBottom = 8;

                Label channelLabel = new(channel.Name);
                channelLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                controlRow.Add(channelLabel);

                Slider weightSlider = new("Weight", 0f, 1.5f)
                {
                    value = channel.Config.defaultWeight
                };
                weightSlider.RegisterValueChangedCallback(evt =>
                {
                    m_Session.SetChannelWeight(channel.Name, evt.newValue);
                });
                controlRow.Add(weightSlider);

                FloatField timeScaleField = new("TimeScale")
                {
                    value = 1f
                };
                timeScaleField.RegisterValueChangedCallback(evt =>
                {
                    m_Session.SetChannelTimeScale(channel.Name, evt.newValue);
                });
                controlRow.Add(timeScaleField);

                m_ChannelControlsContainer.Add(controlRow);

                Label stateLabel = new($"{channel.Name}: idle");
                stateLabel.style.whiteSpace = WhiteSpace.Normal;
                stateLabel.style.marginBottom = 4;
                m_ChannelStatesContainer.Add(stateLabel);
                m_ChannelStateLabels[channel.Name] = stateLabel;
            }
        }

        private void RefreshChannelStates()
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                foreach (Label label in m_ChannelStateLabels.Values)
                {
                    label.text = "idle";
                }
                return;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = (XAnimationCompiledChannel)channels[i];
                XAnimationChannelState state = m_Session.GetChannelState(channel.Name);
                Label label = m_ChannelStateLabels[channel.Name];
                if (state == null)
                {
                    label.text = $"{channel.Name}: idle";
                    continue;
                }

                label.text = $"{channel.Name}: {state.clipKey}  t={state.normalizedTime:0.00}  w={state.weight:0.00}  speed={state.speed:0.00}";
            }
        }

        private void RefreshCueLogView(bool force = false)
        {
            if (m_CueLogContainer == null)
            {
                return;
            }

            int cueCount = m_Session?.CueLogs.Count ?? 0;
            if (!force && cueCount == m_LastCueLogCount)
            {
                return;
            }

            m_LastCueLogCount = cueCount;
            m_CueLogContainer.Clear();

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<string> cueLogs = m_Session.CueLogs;
            for (int i = 0; i < cueLogs.Count; i++)
            {
                Label label = new(cueLogs[i]);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.marginBottom = 2;
                m_CueLogContainer.Add(label);
            }
        }

        private void ClearDebugViews()
        {
            m_ClipListView?.Clear();
            m_ChannelControlsContainer?.Clear();
            m_ChannelStatesContainer?.Clear();
            m_CueLogContainer?.Clear();
            m_ChannelStateLabels.Clear();
            m_PreviewImage.image = null;
            m_LastCueLogCount = -1;
        }

        private void DisposeSession()
        {
            if (m_Session == null)
            {
                return;
            }

            m_Session.Dispose();
            m_Session = null;
            m_PreviewImage.image = null;
            m_LastCueLogCount = -1;
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            m_StatusLabel.text = message;
            m_StatusLabel.style.color = isError ? new Color(0.95f, 0.45f, 0.45f) : new Color(0.8f, 0.8f, 0.8f);
        }

        private static VisualElement CreatePane(float width = 0f)
        {
            VisualElement pane = new VisualElement();
            if (width > 0f)
            {
                pane.style.width = width;
                pane.style.flexShrink = 0;
            }
            else
            {
                pane.style.flexGrow = 1;
            }

            pane.style.flexDirection = FlexDirection.Column;
            pane.style.paddingLeft = 8;
            pane.style.paddingRight = 8;
            pane.style.paddingTop = 8;
            pane.style.paddingBottom = 8;
            pane.style.backgroundColor = new Color(0.15f, 0.15f, 0.16f, 0.9f);
            return pane;
        }

        private static Label CreateSectionTitle(string text)
        {
            Label label = new(text);
            label.style.marginTop = 10;
            label.style.marginBottom = 4;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }
    }
}
#endif
