#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UEvent = UnityEngine.Event;
using XFramework.Animation;
using XFramework.Resource;
using XFramework.UI;
using static XFramework.Editor.XAnimationEditorParameterUtility;
using static XFramework.Editor.XAnimationEditorUi;

namespace XFramework.Editor
{
    public sealed partial class XAnimationPreviewWindow
    {
        public void CreateGUI()
        {
            BuildUI();
            ApplyDefaultSelections();
            SetStatus("拖入 prefab 和 .xasset，或打开已配置默认 prefab 的 XAnimationAsset。");
            ScheduleAutoReloadPreview();
            ApplyPendingOpenRequest();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 2;
            root.style.paddingRight = 2;
            root.style.paddingTop = 2;
            root.style.paddingBottom = 2;
            root.style.flexDirection = FlexDirection.Column;

            TwoPaneSplitView splitView = new(0, DebugPaneInitialWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            splitView.Add(BuildDebugPane());
            splitView.Add(BuildPreviewPane());
        }

        private void LoadPlaybackPrefs()
        {
            XAnimationPlaybackSettings settings = XAnimationPlaybackSettingsPrefs.Load();
            m_PlaybackSectionExpanded = settings.PlaybackSectionExpanded;
            m_PlayTransitionSectionExpanded = settings.TransitionSectionExpanded;
            m_PlayTargetChannelName = settings.ChannelName;
            m_PlaySpeed = Mathf.Approximately(settings.Speed, 0f) ? 1f : settings.Speed;
            m_ApplyTransitionRequestOverrides = settings.ApplyTransition;
            m_PlayFadeInOverride = Mathf.Max(0f, settings.FadeIn);
            m_PlayFadeOutOverride = Mathf.Max(0f, settings.FadeOut);
            m_PlayPriorityOverride = settings.Priority;
            m_PlayInterruptibleOverride = settings.Interruptible;
            m_PlayEnterTimeOverride = Mathf.Clamp01(settings.EnterTime);
            m_PlaybackPrefsLoaded = true;
        }

        private float GetPlaybackSpeed()
        {
            return Mathf.Approximately(m_PlaySpeed, 0f) ? 1f : m_PlaySpeed;
        }

        private float ClampPlaybackSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                return 1f;
            }

            return Mathf.Clamp(speed, PlaybackSpeedMin, PlaybackSpeedMax);
        }

        private void SetPlaybackSpeed(float speed, bool savePrefs = true, bool updateSession = true)
        {
            m_PlaySpeed = ClampPlaybackSpeed(speed);
            m_PlaySpeedSlider?.SetValueWithoutNotify(m_PlaySpeed);
            if (m_PlaySpeedValueLabel != null)
            {
                m_PlaySpeedValueLabel.text = $"{m_PlaySpeed:0.0}x";
            }

            if (updateSession && m_Session != null && m_Session.IsLoaded)
            {
                m_Session.SetTimeScale(m_PlaySpeed);
            }

            if (savePrefs)
            {
                SavePlaybackPrefs();
            }
        }

        private void SavePlaybackPrefs()
        {
            if (!m_PlaybackPrefsLoaded)
            {
                return;
            }

            m_PlaySpeed = ClampPlaybackSpeed(m_PlaySpeedSlider?.value ?? m_PlaySpeed);

            XAnimationPlaybackSettingsPrefs.Save(new XAnimationPlaybackSettings
            {
                PlaybackSectionExpanded = m_PlaybackSectionExpanded,
                TransitionSectionExpanded = m_PlayTransitionSectionExpanded,
                ChannelName = m_PlayTargetChannelName,
                Speed = m_PlaySpeed,
                ApplyTransition = m_ApplyTransitionRequestOverrides,
                FadeIn = m_PlayFadeInOverride,
                FadeOut = m_PlayFadeOutOverride,
                Priority = m_PlayPriorityOverride,
                Interruptible = m_PlayInterruptibleOverride,
                EnterTime = m_PlayEnterTimeOverride,
            });
        }

        private static Button CreateStyledButton(string label, Action onClick, Color bgColor, float marginLeft = 0f)
        {
            Button btn = new(onClick) { text = label };
            btn.tooltip = label switch
            {
                "重载" => "重新读取 Prefab 和 XAnimation 资源并刷新预览。",
                "重置位置" => "将预览对象位置和旋转恢复到初始状态。",
                "重置视角" => "将预览相机恢复到默认视角。",
                "■" => "停止所有正在播放的 channel。",
                "Ⅱ" => "暂停或继续当前预览播放。",
                "▶" => "暂停或继续当前预览播放。",
                "▸|" => "暂停状态下向后推进固定一帧（1/60s）。",
                "设为默认" => "用当前 Prefab 覆盖 XAnimationAsset 的 DefaultPrefabPath。",
                _ => label
            };
            btn.style.backgroundColor = bgColor;
            btn.style.color = Color.white;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.fontSize = BodyFontSize;
            btn.style.paddingLeft = 7;
            btn.style.paddingRight = 7;
            btn.style.paddingTop = 2;
            btn.style.paddingBottom = 2;
            if (marginLeft > 0f) btn.style.marginLeft = marginLeft;
            return btn;
        }

        private static void ConfigurePlaybackToolbarButton(Button button, float marginLeft = 0f)
        {
            if (button == null)
            {
                return;
            }

            button.style.width = PlaybackToolbarButtonSize;
            button.style.minWidth = PlaybackToolbarButtonSize;
            button.style.maxWidth = PlaybackToolbarButtonSize;
            button.style.height = PlaybackToolbarButtonSize;
            button.style.minHeight = PlaybackToolbarButtonSize;
            button.style.maxHeight = PlaybackToolbarButtonSize;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.marginLeft = marginLeft;
            button.style.flexShrink = 0;
        }

        private VisualElement CreatePlaybackScrubber()
        {
            VisualElement scrubber = new();
            scrubber.style.width = PlaybackScrubberWidth;
            scrubber.style.height = 18;
            scrubber.style.flexShrink = 0;
            scrubber.style.position = Position.Relative;
            scrubber.style.backgroundColor = new Color(0.08f, 0.08f, 0.085f, 1f);
            scrubber.style.borderTopWidth = 1;
            scrubber.style.borderBottomWidth = 1;
            scrubber.style.borderLeftWidth = 1;
            scrubber.style.borderRightWidth = 1;
            scrubber.style.borderTopColor = SectionDivider;
            scrubber.style.borderBottomColor = SectionDivider;
            scrubber.style.borderLeftColor = SectionDivider;
            scrubber.style.borderRightColor = SectionDivider;
            scrubber.tooltip = "播放进度。暂停时可拖动调整当前最高权重播放项的归一化时间。";

            m_PlaybackScrubberLine = new VisualElement();
            m_PlaybackScrubberLine.pickingMode = PickingMode.Ignore;
            m_PlaybackScrubberLine.style.position = Position.Absolute;
            m_PlaybackScrubberLine.style.top = 2;
            m_PlaybackScrubberLine.style.bottom = 2;
            m_PlaybackScrubberLine.style.left = 0;
            m_PlaybackScrubberLine.style.width = 2;
            m_PlaybackScrubberLine.style.backgroundColor = Color.white;
            scrubber.Add(m_PlaybackScrubberLine);

            scrubber.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || !CanScrubPlayback())
                {
                    return;
                }

                m_IsDraggingPlaybackScrubber = true;
                m_PlaybackScrubberDragStartX = evt.localPosition.x;
                m_PlaybackScrubberDragStartProgress = m_PlaybackScrubberProgress;
                scrubber.CapturePointer(evt.pointerId);
                UpdatePlaybackScrubberFromDrag(evt.localPosition.x);
                SeekDominantPlayback(m_PlaybackScrubberProgress);
                evt.StopPropagation();
            });
            scrubber.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!m_IsDraggingPlaybackScrubber || !scrubber.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                UpdatePlaybackScrubberFromDrag(evt.localPosition.x);
                SeekDominantPlayback(m_PlaybackScrubberProgress);
                evt.StopPropagation();
            });
            scrubber.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!m_IsDraggingPlaybackScrubber)
                {
                    return;
                }

                m_IsDraggingPlaybackScrubber = false;
                if (scrubber.HasPointerCapture(evt.pointerId))
                {
                    scrubber.ReleasePointer(evt.pointerId);
                }

                UpdatePlaybackScrubberFromDrag(evt.localPosition.x);
                SeekDominantPlayback(m_PlaybackScrubberProgress);
                evt.StopPropagation();
            });
            scrubber.RegisterCallback<PointerCancelEvent>(evt =>
            {
                m_IsDraggingPlaybackScrubber = false;
                m_PlaybackScrubberDragStartX = 0f;
                m_PlaybackScrubberDragStartProgress = m_PlaybackScrubberProgress;
                if (scrubber.HasPointerCapture(evt.pointerId))
                {
                    scrubber.ReleasePointer(evt.pointerId);
                }
            });

            UpdatePlaybackScrubber(0f, enabled: false);
            return scrubber;
        }

        private VisualElement BuildStatusRow()
        {
            VisualElement statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 4;

            VisualElement statusBar = new VisualElement();
            statusBar.style.width = 2;
            statusBar.style.height = 12;
            statusBar.style.backgroundColor = AccentColor;
            statusBar.style.borderTopLeftRadius = 2;
            statusBar.style.borderTopRightRadius = 2;
            statusBar.style.borderBottomLeftRadius = 2;
            statusBar.style.borderBottomRightRadius = 2;
            statusBar.style.marginRight = 4;
            statusRow.Add(statusBar);

            m_StatusLabel = new Label();
            m_StatusLabel.style.color = TextNormal;
            m_StatusLabel.style.fontSize = BodyFontSize;
            m_StatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            statusRow.Add(m_StatusLabel);

            return statusRow;
        }

        private VisualElement BuildPreviewPane()
        {
            VisualElement pane = CreatePane();
            pane.style.minWidth = PreviewPaneMinWidth;

            VisualElement previewSurface = new VisualElement();
            previewSurface.style.position = Position.Relative;
            previewSurface.style.flexGrow = 1;
            previewSurface.style.minHeight = 0;
            pane.Add(previewSurface);

            m_PreviewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            m_PreviewImage.style.position = Position.Absolute;
            m_PreviewImage.style.left = 0;
            m_PreviewImage.style.right = 0;
            m_PreviewImage.style.top = 0;
            m_PreviewImage.style.bottom = 0;
            m_PreviewImage.style.backgroundColor = new Color(0.11f, 0.11f, 0.12f, 1f);
            m_PreviewImage.style.borderTopWidth = 1;
            m_PreviewImage.style.borderBottomWidth = 1;
            m_PreviewImage.style.borderLeftWidth = 1;
            m_PreviewImage.style.borderRightWidth = 1;
            m_PreviewImage.style.borderTopColor = PaneBorder;
            m_PreviewImage.style.borderBottomColor = PaneBorder;
            m_PreviewImage.style.borderLeftColor = PaneBorder;
            m_PreviewImage.style.borderRightColor = PaneBorder;
            m_PreviewImage.style.borderTopLeftRadius = 4;
            m_PreviewImage.style.borderTopRightRadius = 4;
            m_PreviewImage.style.borderBottomLeftRadius = 4;
            m_PreviewImage.style.borderBottomRightRadius = 4;
            RegisterPreviewEvents();
            previewSurface.Add(m_PreviewImage);

            m_PlaybackOverlayCard = BuildPlaybackSettingsCard();
            m_PlaybackOverlayCard.style.position = Position.Absolute;
            m_PlaybackOverlayCard.style.left = m_PlaybackOverlayPosition.x;
            m_PlaybackOverlayCard.style.top = m_PlaybackOverlayPosition.y;
            m_PlaybackOverlayCard.style.minWidth = PlaybackOverlayMinWidth;
            m_PlaybackOverlayCard.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 0.94f);
            m_PlaybackOverlayCard.style.marginBottom = 0;
            m_PlaybackOverlayCard.style.alignSelf = Align.FlexStart;
            previewSurface.Add(m_PlaybackOverlayCard);
            m_PlaybackOverlayCard.BringToFront();

            m_FreeformBlendGraphOverlay = BuildFreeformBlendGraphOverlay();
            previewSurface.Add(m_FreeformBlendGraphOverlay);
            m_FreeformBlendGraphOverlay.BringToFront();
            previewSurface.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                ClampPlaybackOverlayPosition();
                ClampFreeformBlendGraphOverlayPosition();
            });

            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.marginTop = 3;
            controls.style.alignItems = Align.Center;
            pane.Add(controls);

            controls.Add(CreateStyledButton("重置位置", ResetPreviewTransform, AccentColor));
            controls.Add(CreateStyledButton("重置视角", ResetPreviewCamera, AccentColor, 6));

            Label hint = new("右键拖拽旋转，WASD 移动，QE 升降，滚轮缩放。");
            hint.style.marginLeft = 4;
            hint.style.color = TextMuted;
            hint.style.fontSize = BodyFontSize;
            controls.Add(hint);

            m_GridToggle = new Toggle("网格") { value = true };
            m_GridToggle.tooltip = "显示或隐藏预览地面网格，只影响当前预览。";
            m_GridToggle.style.marginLeft = 12;
            m_GridToggle.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;
                m_Session.SetGridVisible(evt.newValue);
                RenderPreview();
            });
            controls.Add(m_GridToggle);

            return pane;
        }

        private VisualElement BuildDebugPane()
        {
            VisualElement pane = BuildDebugPaneShell();
            VisualElement inspectorPane = CreateDebugInspectorPane();
            BuildDebugTabContainers();
            ComposeMainTab();
            ComposeClipTab();
            ComposeChannelsTab();
            ComposeParametersTab();

            Button clearCueLogButton = CreateStyledButton("Clear", ClearCueLog, DangerColor);
            clearCueLogButton.tooltip = "清空当前 Preview Session 的 Log。";
            VisualElement cueCard = CreateCard("Log", clearCueLogButton);
            m_CueLogContainer = new ScrollView();
            m_CueLogContainer.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_CueLogContainer.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_CueLogContainer.style.flexGrow = 1;
            m_CueLogContainer.style.minHeight = 0;
            cueCard.Add(m_CueLogContainer);
            ConfigureConsoleSection(cueCard);

            ApplyDebugToolbarGroup();

            pane.Add(BuildInspectorConsoleSplit(inspectorPane, cueCard));

            VisualElement statusSpacer = new VisualElement();
            statusSpacer.style.height = 4;
            statusSpacer.style.flexShrink = 0;
            pane.Add(statusSpacer);
            pane.Add(BuildStatusRow());

            return pane;
        }

        private VisualElement BuildDebugPaneShell()
        {
            VisualElement pane = new VisualElement();
            pane.style.minWidth = DebugPaneMinWidth;
            pane.style.flexGrow = 1;
            pane.style.minHeight = 0;
            pane.style.flexDirection = FlexDirection.Column;
            pane.style.paddingLeft = 3;
            pane.style.paddingRight = 3;
            pane.style.paddingTop = 3;
            pane.style.paddingBottom = 3;
            pane.style.backgroundColor = PaneBg;
            pane.style.borderTopLeftRadius = 6;
            pane.style.borderTopRightRadius = 6;
            pane.style.borderBottomLeftRadius = 6;
            pane.style.borderBottomRightRadius = 6;
            pane.style.borderTopWidth = 1;
            pane.style.borderBottomWidth = 1;
            pane.style.borderLeftWidth = 1;
            pane.style.borderRightWidth = 1;
            pane.style.borderTopColor = PaneBorder;
            pane.style.borderBottomColor = PaneBorder;
            pane.style.borderLeftColor = PaneBorder;
            pane.style.borderRightColor = PaneBorder;
            return pane;
        }

        private VisualElement CreateDebugInspectorPane()
        {
            VisualElement inspectorPane = new VisualElement();
            inspectorPane.style.position = Position.Relative;
            inspectorPane.style.flexDirection = FlexDirection.Column;
            inspectorPane.style.flexGrow = 1;
            inspectorPane.style.minHeight = 0;

            VisualElement toolbar = BuildDebugToolbar();
            toolbar.style.flexShrink = 0;
            inspectorPane.Add(toolbar);

            m_InspectorScrollView = new ScrollView();
            m_InspectorScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_InspectorScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_InspectorScrollView.style.flexGrow = 1;
            m_InspectorScrollView.style.minHeight = 0;
            inspectorPane.Add(m_InspectorScrollView);

            m_InspectorOverlayLayer = new VisualElement();
            m_InspectorOverlayLayer.style.position = Position.Absolute;
            m_InspectorOverlayLayer.style.left = 0;
            m_InspectorOverlayLayer.style.right = 0;
            m_InspectorOverlayLayer.style.top = 0;
            m_InspectorOverlayLayer.style.bottom = 0;
            m_InspectorOverlayLayer.pickingMode = PickingMode.Ignore;
            inspectorPane.Add(m_InspectorOverlayLayer);
            if (m_SearchResultsPopup != null)
            {
                m_InspectorOverlayLayer.Add(m_SearchResultsPopup);
                m_SearchResultsPopup.BringToFront();
            }

            return inspectorPane;
        }

        private void BuildDebugTabContainers()
        {
            m_MainGroupContainer = CreateDebugTabContainer();
            m_InspectorScrollView.Add(m_MainGroupContainer);

            m_ClipGroupContainer = CreateDebugTabContainer();
            m_InspectorScrollView.Add(m_ClipGroupContainer);

            m_ChannelsGroupContainer = CreateDebugTabContainer();
            m_InspectorScrollView.Add(m_ChannelsGroupContainer);

            m_ParametersGroupContainer = CreateDebugTabContainer();
            m_InspectorScrollView.Add(m_ParametersGroupContainer);
        }

        private static VisualElement CreateDebugTabContainer()
        {
            VisualElement container = new VisualElement();
            container.style.minHeight = 0;
            return container;
        }

        private void ComposeMainTab()
        {
            m_MainGroupContainer.Add(CreateStatesSection().Root);
            m_MainGroupContainer.Add(CreateAutoTransitionsSection().Root);
            m_MainGroupContainer.Add(CreateDefaultTransitionsSection().Root);
        }

        private void ComposeClipTab()
        {
            m_ClipGroupContainer.Add(CreateClipsSection().Root);
        }

        private void ComposeChannelsTab()
        {
            m_ChannelsGroupContainer.Add(CreateChannelsSection().Root);
        }

        private void ComposeParametersTab()
        {
            m_ParametersGroupContainer.Add(CreateParametersSection().Root);
        }

        private static void ApplyToolbarButtonIcon(Button button, params string[] iconNames)
        {
            if (button == null || iconNames == null || iconNames.Length == 0)
            {
                return;
            }

            Texture icon = null;
            for (int i = 0; i < iconNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(iconNames[i]))
                {
                    continue;
                }

                icon = EditorGUIUtility.IconContent(iconNames[i]).image;
                if (icon != null)
                {
                    break;
                }
            }

            if (icon == null)
            {
                return;
            }

            button.text = string.Empty;
            button.Clear();
            Image image = new() { image = icon };
            image.tintColor = TextNormal;
            image.style.width = 13;
            image.style.height = 13;
            image.style.alignSelf = Align.Center;
            image.style.flexShrink = 0;
            button.Add(image);
        }

        private static Button CreateAssetToolbarIconButton(string text, string tooltip, Action onClick, Color? bgColor = null, params string[] iconNames)
        {
            Button button = new(onClick)
            {
                text = text
            };
            button.tooltip = tooltip;
            ApplyClipIconButtonStyle(button, bgColor ?? AccentColor);
            button.style.flexShrink = 0;
            ApplyToolbarButtonIcon(button, iconNames);
            return button;
        }

        private VisualElement CreateAssetsSection()
        {
            VisualElement assetsBar = CreateSubBox();
            assetsBar.style.marginBottom = 4;
            assetsBar.style.marginTop = 0;

            VisualElement assetsRow = new();
            assetsRow.style.flexDirection = FlexDirection.Row;
            assetsRow.style.alignItems = Align.Center;
            assetsBar.Add(assetsRow);

            m_PrefabField = new ObjectField()
            {
                objectType = typeof(GameObject),
                allowSceneObjects = false
            };
            m_PrefabField.tooltip = "用于预览动画的角色 Prefab，必须包含 Animator。";
            m_PrefabField.RegisterValueChangedCallback(evt =>
            {
                m_SelectedPrefab = evt.newValue as GameObject;
                RefreshAssetsToolbarButtons();
                if (evt.newValue != null && m_AssetField?.value != null)
                {
                    LoadPreview();
                }
            });
            if (m_PrefabField.labelElement != null)
            {
                m_PrefabField.labelElement.style.display = DisplayStyle.None;
            }
            m_PrefabField.style.marginBottom = 4;

            m_PrefabField.style.flexGrow = 1;
            m_PrefabField.style.flexBasis = 0;
            m_PrefabField.style.minWidth = 0;
            m_PrefabField.style.marginBottom = 0;
            assetsRow.Add(m_PrefabField);

            m_SaveCurrentPrefabAsDefaultButton = CreateAssetToolbarIconButton(
                "✓",
                "把当前 Prefab 设为这个 XAnimation 的默认模型。",
                SaveCurrentPrefabAsDefault);
            m_SaveCurrentPrefabAsDefaultButton.style.marginLeft = 6;
            assetsRow.Add(m_SaveCurrentPrefabAsDefaultButton);

            m_ResetPrefabToDefaultButton = CreateAssetToolbarIconButton(
                "↺",
                "把当前模型恢复成默认 Prefab，并重新加载预览。",
                ResetPrefabToDefault,
                ListHeaderBg);
            m_ResetPrefabToDefaultButton.style.marginLeft = 4;
            assetsRow.Add(m_ResetPrefabToDefaultButton);

            m_AssetField = new ObjectField()
            {
                objectType = typeof(TextAsset),
                allowSceneObjects = false
            };
            m_AssetField.tooltip = "要加载和编辑的 XAnimation .xasset 或 Override Asset。";
            m_AssetField.RegisterValueChangedCallback(evt =>
            {
                m_SelectedAsset = evt.newValue as TextAsset;
                RefreshAssetsToolbarButtons();
            });
            if (m_AssetField.labelElement != null)
            {
                m_AssetField.labelElement.style.display = DisplayStyle.None;
            }
            m_AssetField.style.marginBottom = 4;

            m_AssetField.style.flexGrow = 1;
            m_AssetField.style.flexBasis = 0;
            m_AssetField.style.minWidth = 0;
            m_AssetField.style.marginBottom = 0;
            m_AssetField.style.marginLeft = 6;
            assetsRow.Add(m_AssetField);

            m_ReloadPreviewButton = CreateAssetToolbarIconButton(
                "⟳",
                "重新读取 Prefab 和 XAnimation 资源并刷新预览。",
                LoadPreview,
                iconNames: new[] { "d_Refresh", "Refresh", "d_TreeEditor.Refresh", "TreeEditor.Refresh" });
            m_ReloadPreviewButton.style.marginLeft = 6;
            assetsRow.Add(m_ReloadPreviewButton);

            RefreshAssetsToolbarButtons();
            return assetsBar;
        }

        private FoldoutCard CreateStatesSection()
        {
            m_StatesCard = CreateFoldoutCard("States", m_StatesSectionExpanded, value => m_StatesSectionExpanded = value);
            m_StateListView = new VisualElement();
            m_StatesCard.Content.Add(m_StateListView);
            return m_StatesCard;
        }

        private FoldoutCard CreateAutoTransitionsSection()
        {
            m_AddAutoTransitionButton = CreateStyledButton("+", AddAutoTransition, AccentColor);
            m_AddAutoTransitionButton.tooltip = "新增一个 Auto Transition。";
            SetAutoTransitionButtonsEnabled(false);

            VisualElement autoTransitionActions = new VisualElement();
            autoTransitionActions.style.flexDirection = FlexDirection.Row;
            autoTransitionActions.style.alignItems = Align.Center;
            autoTransitionActions.Add(m_AddAutoTransitionButton);

            m_AutoTransitionCard = CreateFoldoutCard("Auto Transition", m_AutoTransitionSectionExpanded, value => m_AutoTransitionSectionExpanded = value, autoTransitionActions);
            m_AutoTransitionEditorView = new VisualElement();
            m_AutoTransitionCard.Content.Add(m_AutoTransitionEditorView);
            return m_AutoTransitionCard;
        }

        private FoldoutCard CreateDefaultTransitionsSection()
        {
            m_AddDefaultTransitionButton = CreateStyledButton("+", AddDefaultTransition, AccentColor);
            m_AddDefaultTransitionButton.tooltip = "新增一个 Default Transition 分组。";
            SetDefaultTransitionButtonsEnabled(false);

            VisualElement defaultTransitionActions = new VisualElement();
            defaultTransitionActions.style.flexDirection = FlexDirection.Row;
            defaultTransitionActions.style.alignItems = Align.Center;
            defaultTransitionActions.Add(m_AddDefaultTransitionButton);

            m_DefaultTransitionsCard = CreateFoldoutCard("Default Transitions", m_DefaultTransitionsSectionExpanded, value => m_DefaultTransitionsSectionExpanded = value, defaultTransitionActions);
            m_DefaultTransitionsEditorView = new VisualElement();
            m_DefaultTransitionsCard.Content.Add(m_DefaultTransitionsEditorView);
            return m_DefaultTransitionsCard;
        }

        private FoldoutCard CreateClipsSection()
        {
            m_AddClipButton = CreateStyledButton("+", AddClip, AccentColor);
            m_AddClipButton.tooltip = "新增一个全局 clip 资源叶子。";
            m_AddClipGroupButton = CreateStyledButton("+ Group", AddClipGroup, AccentColor);
            m_AddClipGroupButton.tooltip = "新建一个 clip group。";
            SetAddClipButtonEnabled(false);

            VisualElement clipActions = new VisualElement();
            clipActions.style.flexDirection = FlexDirection.Row;
            clipActions.style.alignItems = Align.Center;
            clipActions.Add(m_AddClipButton);
            m_AddClipGroupButton.style.marginLeft = 4;
            clipActions.Add(m_AddClipGroupButton);

            m_ClipsCard = CreateFoldoutCard("Clips", m_ClipsSectionExpanded, value => m_ClipsSectionExpanded = value, clipActions);
            m_ClipListView = new VisualElement();
            m_ClipsCard.Content.Add(m_ClipListView);
            return m_ClipsCard;
        }

        private FoldoutCard CreateChannelsSection()
        {
            m_AddChannelButton = CreateStyledButton("+", AddChannel, AccentColor);
            m_AddChannelButton.tooltip = "新增一个 channel。";
            SetAddChannelButtonEnabled(false);

            m_ChannelsCard = CreateFoldoutCard("Channels", m_ChannelsSectionExpanded, value => m_ChannelsSectionExpanded = value, m_AddChannelButton);
            m_ChannelControlsContainer = new VisualElement();
            m_ChannelsCard.Content.Add(m_ChannelControlsContainer);
            return m_ChannelsCard;
        }

        private FoldoutCard CreateParametersSection()
        {
            m_AddParameterButton = CreateStyledButton("+", AddParameter, AccentColor);
            m_AddParameterButton.tooltip = "新增一个 XAnimation 参数。";
            SetAddParameterButtonEnabled(false);

            m_ParametersCard = CreateFoldoutCard("Parameters", m_ParametersSectionExpanded, value => m_ParametersSectionExpanded = value, m_AddParameterButton);
            m_ParameterListView = new VisualElement();
            m_ParametersCard.Content.Add(m_ParameterListView);
            return m_ParametersCard;
        }

        private VisualElement BuildFreeformBlendGraphOverlay()
        {
            VisualElement overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = m_FreeformBlendGraphOverlayPosition.x;
            overlay.style.bottom = m_FreeformBlendGraphOverlayPosition.y;
            overlay.style.width = FreeformBlendGraphOverlayWidth;
            overlay.style.paddingLeft = 6;
            overlay.style.paddingRight = 6;
            overlay.style.paddingTop = 6;
            overlay.style.paddingBottom = 6;
            overlay.style.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 0.92f);
            overlay.style.borderTopLeftRadius = 6;
            overlay.style.borderTopRightRadius = 6;
            overlay.style.borderBottomLeftRadius = 6;
            overlay.style.borderBottomRightRadius = 6;
            overlay.style.borderTopWidth = 1;
            overlay.style.borderBottomWidth = 1;
            overlay.style.borderLeftWidth = 1;
            overlay.style.borderRightWidth = 1;
            overlay.style.borderTopColor = PaneBorder;
            overlay.style.borderBottomColor = PaneBorder;
            overlay.style.borderLeftColor = PaneBorder;
            overlay.style.borderRightColor = PaneBorder;
            overlay.style.display = DisplayStyle.None;

            VisualElement headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = BlendGraphOverlayHeaderMarginBottomExpanded;
            headerRow.AddToClassList("xanim-freeform-graph-overlay-drag-handle");
            headerRow.tooltip = "拖拽标题栏可以移动这个 Blend Graph HUD，点击标题栏可展开或收起。";
            overlay.Add(headerRow);
            m_FreeformBlendGraphOverlayHeader = headerRow;

            m_FreeformBlendGraphTitleLabel = new Label();
            m_FreeformBlendGraphTitleLabel.style.color = TextNormal;
            m_FreeformBlendGraphTitleLabel.style.fontSize = BodyFontSize;
            m_FreeformBlendGraphTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_FreeformBlendGraphTitleLabel.style.flexGrow = 1f;
            headerRow.Add(m_FreeformBlendGraphTitleLabel);

            m_FreeformBlendGraphOverlayContent = new VisualElement();
            m_FreeformBlendGraphOverlayContent.style.flexDirection = FlexDirection.Column;
            overlay.Add(m_FreeformBlendGraphOverlayContent);

            m_FreeformBlendGraphElement = new XAnimationDirectionalBlendGraphElement();
            m_FreeformBlendGraphElement.tooltip = "蓝点是 sample，红点是当前 2D 参数值，圆圈大小表示实时 weight。拖动红点可预览 freeform directional blend。";
            m_FreeformBlendGraphElement.style.display = DisplayStyle.None;
            m_FreeformBlendGraphOverlayContent.Add(m_FreeformBlendGraphElement);

            m_Blend1DGraphElement = new XAnimationBlend1DGraphElement();
            m_Blend1DGraphElement.tooltip = "蓝色包络表示 Blend1D sample weight，红线与红点表示当前参数值。拖动红点可预览 Blend1D。";
            m_Blend1DGraphElement.style.display = DisplayStyle.None;
            m_FreeformBlendGraphOverlayContent.Add(m_Blend1DGraphElement);

            m_FreeformBlendGraphHintLabel = new Label();
            m_FreeformBlendGraphHintLabel.style.color = TextMuted;
            m_FreeformBlendGraphHintLabel.style.fontSize = BodyFontSize;
            m_FreeformBlendGraphHintLabel.style.whiteSpace = WhiteSpace.Normal;
            m_FreeformBlendGraphHintLabel.style.display = DisplayStyle.None;
            m_FreeformBlendGraphOverlayContent.Add(m_FreeformBlendGraphHintLabel);

            RegisterFreeformBlendGraphOverlayDrag(overlay, headerRow, () => SetBlendGraphOverlayExpanded(!m_FreeformBlendGraphOverlayExpanded));
            SetBlendGraphOverlayExpanded(m_FreeformBlendGraphOverlayExpanded);

            return overlay;
        }

        private VisualElement BuildPlaybackSettingsCard()
        {
            VisualElement playbackActions = new VisualElement();
            playbackActions.style.flexDirection = FlexDirection.Row;
            playbackActions.style.alignItems = Align.Center;
            playbackActions.style.flexWrap = Wrap.NoWrap;
            playbackActions.style.minWidth = 0;

            m_PlaybackScrubber = CreatePlaybackScrubber();
            playbackActions.Add(m_PlaybackScrubber);

            VisualElement speedControls = new VisualElement();
            speedControls.style.flexDirection = FlexDirection.Row;
            speedControls.style.alignItems = Align.Center;
            speedControls.style.width = PlaybackSpeedControlWidth;
            speedControls.style.flexShrink = 0;
            speedControls.style.marginLeft = 6;
            speedControls.tooltip = "本次预览播放使用的时间缩放倍率。";

            m_PlaySpeedSlider = new Slider(PlaybackSpeedMin, PlaybackSpeedMax)
            {
                value = m_PlaybackPrefsLoaded ? ClampPlaybackSpeed(GetPlaybackSpeed()) : 1f
            };
            m_PlaySpeedSlider.style.flexGrow = 1;
            m_PlaySpeedSlider.style.flexShrink = 1;
            m_PlaySpeedSlider.style.minWidth = 56;
            m_PlaySpeedSlider.tooltip = "拖动调整 request.speed，只影响当前预览请求。";
            m_PlaySpeedSlider.RegisterValueChangedCallback(evt => SetPlaybackSpeed(evt.newValue));
            speedControls.Add(m_PlaySpeedSlider);

            m_PlaySpeedValueLabel = new Label();
            m_PlaySpeedValueLabel.style.width = 34;
            m_PlaySpeedValueLabel.style.minWidth = 34;
            m_PlaySpeedValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            m_PlaySpeedValueLabel.style.color = TextNormal;
            m_PlaySpeedValueLabel.style.fontSize = BodyFontSize;
            m_PlaySpeedValueLabel.style.marginLeft = 4;
            speedControls.Add(m_PlaySpeedValueLabel);

            SetPlaybackSpeed(m_PlaybackPrefsLoaded ? GetPlaybackSpeed() : 1f, savePrefs: false, updateSession: false);
            playbackActions.Add(speedControls);

            m_PauseButton = CreateStyledButton("Ⅱ", TogglePause, AccentColor);
            SetPauseButtonState(false, false);
            ConfigurePlaybackToolbarButton(m_PauseButton, 6f);
            playbackActions.Add(m_PauseButton);

            m_StepForwardButton = CreateStyledButton("▸|", StepForward, AccentColor);
            SetStepForwardButtonEnabled(false);
            ConfigurePlaybackToolbarButton(m_StepForwardButton, 4f);
            playbackActions.Add(m_StepForwardButton);

            m_StopAllButton = CreateStyledButton("■", StopAllClips, DangerColor);
            SetStopAllButtonEnabled(false);
            ConfigurePlaybackToolbarButton(m_StopAllButton, 4f);
            playbackActions.Add(m_StopAllButton);

            FoldoutCard playbackCard = CreateFoldoutCard(string.Empty, m_PlaybackSectionExpanded, value =>
            {
                m_PlaybackSectionExpanded = value;
                SavePlaybackPrefs();
            }, playbackActions);
            RegisterPlaybackOverlayDrag(playbackCard.Root, () => playbackCard.SetExpanded?.Invoke(!m_PlaybackSectionExpanded));

            VisualElement assetsBar = CreateAssetsSection();
            playbackCard.Content.Add(assetsBar);

            VisualElement playbackFields = new VisualElement();
            playbackFields.style.flexDirection = FlexDirection.Column;
            playbackFields.style.alignItems = Align.Stretch;

            m_PlayTargetChannelField = CreateChannelDropdown(string.Empty, m_PlayTargetChannelName);
            m_PlayTargetChannelField.tooltip = "clip 调试播放使用的 channelName。state 播放始终使用 state 自己配置的 channel。";
            m_PlayTargetChannelField.style.flexGrow = 1;
            m_PlayTargetChannelField.style.minWidth = 0;
            AttachDropdownInspectorButton(
                m_PlayTargetChannelField,
                () => m_PlayTargetChannelField?.value ?? m_PlayTargetChannelName,
                () => HasChannel(m_PlayTargetChannelField?.value ?? m_PlayTargetChannelName),
                () => FocusChannelInInspector(m_PlayTargetChannelField?.value ?? m_PlayTargetChannelName),
                "定位到 Channels 面板里当前 channel 对应的条目。");
            m_PlayTargetChannelField.RegisterValueChangedCallback(evt =>
            {
                m_PlayTargetChannelName = evt.newValue ?? string.Empty;
                SavePlaybackPrefs();
            });

            m_RootMotionToggle = new Toggle { value = m_PreviewRootMotionEnabled };
            m_RootMotionToggle.tooltip = "控制当前预览 session 是否应用 Root Motion。关闭后会将预览实例复位到初始位置。";
            m_RootMotionToggle.RegisterValueChangedCallback(evt =>
            {
                m_PreviewRootMotionEnabled = evt.newValue;
                if (m_Session != null && m_Session.IsLoaded)
                {
                    m_Session.SetRootMotionEnabled(evt.newValue);
                    RenderPreview();
                }
            });
            VisualElement channelAndRootMotionRow = CreatePlaybackFieldPairRow(
                "channelName",
                m_PlayTargetChannelField,
                "rootMotion",
                m_RootMotionToggle,
                PlaybackMainFieldLabelWidth,
                PlaybackMainFieldValueWidth);
            channelAndRootMotionRow.tooltip = "channelName 用于 clip 调试播放；rootMotion 仅影响当前预览窗口。";
            VisualElement channelAndRootMotionBox = CreateSubBox();
            channelAndRootMotionBox.tooltip = channelAndRootMotionRow.tooltip;
            channelAndRootMotionBox.Add(channelAndRootMotionRow);
            playbackFields.Add(channelAndRootMotionBox);

            playbackCard.Content.Add(playbackFields);

            m_ApplyTransitionRequestToggle = CreateHeaderApplyToggle(m_ApplyTransitionRequestOverrides, "是否应用 Transition 覆盖。关闭时本分区会自动收起。");
            FoldoutCard transitionCard = CreateSectionFoldoutCard("Transition", m_PlayTransitionSectionExpanded, value =>
            {
                m_PlayTransitionSectionExpanded = value;
                SavePlaybackPrefs();
            }, m_ApplyTransitionRequestToggle, () => m_ApplyTransitionRequestOverrides);
            transitionCard.Root.style.marginTop = 4;

            m_ApplyTransitionRequestToggle.tooltip = "只应用 fadeIn / fadeOut / priority / enterTime 覆盖。";
            m_ApplyTransitionRequestToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyTransitionRequestOverrides = evt.newValue;
                if (!evt.newValue)
                {
                    transitionCard.SetExpanded?.Invoke(false);
                }
                transitionCard.RefreshState?.Invoke();
                SavePlaybackPrefs();
            });

            m_PlayFadeInField = new FloatField { value = m_PlayFadeInOverride };
            m_PlayFadeInField.tooltip = "0 表示使用 state/channel 默认值；大于 0 时写入 request.fadeIn。";
            ConfigureCompactPlaybackField(m_PlayFadeInField, "fadeIn", TransitionFieldValueWidth);
            m_PlayFadeInField.RegisterValueChangedCallback(evt =>
            {
                m_PlayFadeInOverride = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(m_PlayFadeInOverride, evt.newValue))
                {
                    m_PlayFadeInField.SetValueWithoutNotify(m_PlayFadeInOverride);
                }

                SavePlaybackPrefs();
            });

            m_PlayFadeOutField = new FloatField { value = m_PlayFadeOutOverride };
            m_PlayFadeOutField.tooltip = "0 表示使用 state/channel 默认值；大于 0 时写入 request.fadeOut。";
            ConfigureCompactPlaybackField(m_PlayFadeOutField, "fadeOut", TransitionFieldValueWidth);
            m_PlayFadeOutField.RegisterValueChangedCallback(evt =>
            {
                m_PlayFadeOutOverride = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(m_PlayFadeOutOverride, evt.newValue))
                {
                    m_PlayFadeOutField.SetValueWithoutNotify(m_PlayFadeOutOverride);
                }

                SavePlaybackPrefs();
            });

            m_PlayPriorityField = new IntegerField { value = m_PlayPriorityOverride };
            m_PlayPriorityField.tooltip = "request.priority。";
            ConfigureCompactPlaybackElement(m_PlayPriorityField, TransitionFieldValueWidth);
            m_PlayPriorityField.RegisterValueChangedCallback(evt =>
            {
                m_PlayPriorityOverride = evt.newValue;
                SavePlaybackPrefs();
            });

            m_PlayEnterTimeField = new FloatField { value = m_PlayEnterTimeOverride };
            m_PlayEnterTimeField.tooltip = "transition.enterTime，会被夹到 [0, 1]。";
            ConfigureCompactPlaybackField(m_PlayEnterTimeField, "enterTime", TransitionFieldValueWidth);
            m_PlayEnterTimeField.RegisterValueChangedCallback(evt =>
            {
                m_PlayEnterTimeOverride = Mathf.Clamp01(evt.newValue);
                if (!Mathf.Approximately(m_PlayEnterTimeOverride, evt.newValue))
                {
                    m_PlayEnterTimeField.SetValueWithoutNotify(m_PlayEnterTimeOverride);
                }

                SavePlaybackPrefs();
            });
            VisualElement fadeRow = CreatePlaybackFieldPairRow(
                "fadeIn",
                m_PlayFadeInField,
                "fadeOut",
                m_PlayFadeOutField,
                TransitionFieldLabelWidth,
                TransitionFieldValueWidth);
            fadeRow.tooltip = "fadeIn / fadeOut 覆盖。0 表示继续使用资源里的默认值。";
            transitionCard.Content.Add(fadeRow);

            VisualElement timingRow = CreatePlaybackFieldPairRow(
                "enterTime",
                m_PlayEnterTimeField,
                "priority",
                m_PlayPriorityField,
                TransitionFieldLabelWidth,
                TransitionFieldValueWidth);
            timingRow.tooltip = "enterTime / priority 覆盖。";
            transitionCard.Content.Add(timingRow);

            playbackCard.Content.Add(transitionCard.Root);

            FoldoutCard previewParametersCard = CreateSectionFoldoutCard("Preview Parameters", m_PreviewParametersSectionExpanded, value =>
            {
                m_PreviewParametersSectionExpanded = value;
            });
            previewParametersCard.Root.style.marginTop = 4;
            m_MainParameterPreviewView = new VisualElement();
            previewParametersCard.Content.Add(m_MainParameterPreviewView);
            playbackCard.Content.Add(previewParametersCard.Root);

            return playbackCard.Root;
        }

        private void RegisterPlaybackOverlayDrag(VisualElement card, Action toggleExpanded)
        {
            if (card == null)
            {
                return;
            }

            VisualElement titleRow = card.childCount > 0 ? card[0] : null;
            Label toggleLabel = titleRow?.Q<Label>();
            if (titleRow == null || toggleLabel == null)
            {
                return;
            }

            toggleLabel.AddToClassList("xanim-playback-overlay-drag-handle");
            toggleLabel.tooltip = "拖拽左侧三角可以移动这个悬浮面板，鼠标抬起时如果位置没变化则展开/收起。";

            toggleLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                m_IsDraggingPlaybackOverlay = true;
                m_PlaybackOverlayDragMoved = false;
                m_PlaybackOverlayDragStartPointer = new Vector2(evt.position.x, evt.position.y);
                m_PlaybackOverlayDragStartPosition = m_PlaybackOverlayPosition;
                toggleLabel.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            toggleLabel.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!m_IsDraggingPlaybackOverlay || !toggleLabel.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
                Vector2 delta = pointerPosition - m_PlaybackOverlayDragStartPointer;
                if (!m_PlaybackOverlayDragMoved && delta.sqrMagnitude > PlaybackOverlayClickThreshold * PlaybackOverlayClickThreshold)
                {
                    m_PlaybackOverlayDragMoved = true;
                }
                m_PlaybackOverlayPosition = m_PlaybackOverlayDragStartPosition + delta;
                ClampPlaybackOverlayPosition();
                evt.StopPropagation();
            });

            void EndDrag(IPointerEvent evt, bool canToggle)
            {
                if (!m_IsDraggingPlaybackOverlay)
                {
                    return;
                }

                bool shouldToggle = canToggle && !m_PlaybackOverlayDragMoved;
                m_IsDraggingPlaybackOverlay = false;
                m_PlaybackOverlayDragMoved = false;
                if (toggleLabel.HasPointerCapture(evt.pointerId))
                {
                    toggleLabel.ReleasePointer(evt.pointerId);
                }

                ClampPlaybackOverlayPosition();
                if (shouldToggle)
                {
                    toggleExpanded?.Invoke();
                }
            }

            toggleLabel.RegisterCallback<PointerUpEvent>(evt =>
            {
                EndDrag(evt, canToggle: true);
                evt.StopPropagation();
            });

            toggleLabel.RegisterCallback<PointerCancelEvent>(evt =>
            {
                EndDrag(evt, canToggle: false);
                evt.StopPropagation();
            });
        }

        private void ClampPlaybackOverlayPosition()
        {
            if (m_PlaybackOverlayCard == null || m_PlaybackOverlayCard.parent == null)
            {
                return;
            }

            Rect parentBounds = m_PlaybackOverlayCard.parent.contentRect;
            float cardWidth = Mathf.Max(PlaybackOverlayMinWidth, m_PlaybackOverlayCard.resolvedStyle.width);
            float cardHeight = Mathf.Max(0f, m_PlaybackOverlayCard.resolvedStyle.height);
            float maxX = Mathf.Max(0f, parentBounds.width - cardWidth);
            float maxY = Mathf.Max(0f, parentBounds.height - cardHeight);
            m_PlaybackOverlayPosition = new Vector2(
                Mathf.Clamp(m_PlaybackOverlayPosition.x, 0f, maxX),
                Mathf.Clamp(m_PlaybackOverlayPosition.y, 0f, maxY));

            m_PlaybackOverlayCard.style.left = m_PlaybackOverlayPosition.x;
            m_PlaybackOverlayCard.style.top = m_PlaybackOverlayPosition.y;
        }

        private void RegisterFreeformBlendGraphOverlayDrag(VisualElement card, VisualElement dragHandle, Action toggleExpanded)
        {
            if (card == null || dragHandle == null)
            {
                return;
            }

            dragHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                m_IsDraggingFreeformBlendGraphOverlay = true;
                m_FreeformBlendGraphOverlayDragMoved = false;
                m_FreeformBlendGraphOverlayDragStartPointer = new Vector2(evt.position.x, evt.position.y);
                m_FreeformBlendGraphOverlayDragStartPosition = m_FreeformBlendGraphOverlayPosition;
                dragHandle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            dragHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!m_IsDraggingFreeformBlendGraphOverlay || !dragHandle.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
                Vector2 delta = pointerPosition - m_FreeformBlendGraphOverlayDragStartPointer;
                if (!m_FreeformBlendGraphOverlayDragMoved && delta.sqrMagnitude > PlaybackOverlayClickThreshold * PlaybackOverlayClickThreshold)
                {
                    m_FreeformBlendGraphOverlayDragMoved = true;
                }

                m_FreeformBlendGraphOverlayPosition = new Vector2(
                    m_FreeformBlendGraphOverlayDragStartPosition.x + delta.x,
                    m_FreeformBlendGraphOverlayDragStartPosition.y - delta.y);
                ClampFreeformBlendGraphOverlayPosition();
                evt.StopPropagation();
            });

            void EndDrag(IPointerEvent evt, bool canToggle)
            {
                if (!m_IsDraggingFreeformBlendGraphOverlay)
                {
                    return;
                }

                bool shouldToggle = canToggle && !m_FreeformBlendGraphOverlayDragMoved;
                m_IsDraggingFreeformBlendGraphOverlay = false;
                m_FreeformBlendGraphOverlayDragMoved = false;
                if (dragHandle.HasPointerCapture(evt.pointerId))
                {
                    dragHandle.ReleasePointer(evt.pointerId);
                }

                ClampFreeformBlendGraphOverlayPosition();
                if (shouldToggle)
                {
                    toggleExpanded?.Invoke();
                }
            }

            dragHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                EndDrag(evt, canToggle: true);
                evt.StopPropagation();
            });

            dragHandle.RegisterCallback<PointerCancelEvent>(evt =>
            {
                EndDrag(evt, canToggle: false);
                evt.StopPropagation();
            });
        }

        private void SetBlendGraphOverlayExpanded(bool expanded)
        {
            float visibleContentHeight = GetResolvedElementHeight(m_FreeformBlendGraphOverlayContent);
            if (visibleContentHeight > 0f)
            {
                m_FreeformBlendGraphLastExpandedContentHeight = visibleContentHeight;
            }

            float heightDelta = 0f;
            if (expanded == m_FreeformBlendGraphOverlayExpanded)
            {
                if (m_FreeformBlendGraphOverlayHeader != null)
                {
                    m_FreeformBlendGraphOverlayHeader.style.marginBottom = expanded ? BlendGraphOverlayHeaderMarginBottomExpanded : 0f;
                }
            }
            else if (expanded)
            {
                heightDelta = -(Mathf.Max(m_FreeformBlendGraphLastExpandedContentHeight, visibleContentHeight) + BlendGraphOverlayHeaderMarginBottomExpanded);
            }
            else
            {
                heightDelta = visibleContentHeight + BlendGraphOverlayHeaderMarginBottomExpanded;
            }

            m_FreeformBlendGraphOverlayExpanded = expanded;
            if (m_FreeformBlendGraphOverlayHeader != null)
            {
                m_FreeformBlendGraphOverlayHeader.style.marginBottom = expanded ? BlendGraphOverlayHeaderMarginBottomExpanded : 0f;
            }

            if (m_FreeformBlendGraphOverlayContent != null)
            {
                m_FreeformBlendGraphOverlayContent.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!Mathf.Approximately(heightDelta, 0f))
            {
                m_FreeformBlendGraphOverlayPosition = new Vector2(
                    m_FreeformBlendGraphOverlayPosition.x,
                    m_FreeformBlendGraphOverlayPosition.y + heightDelta);
            }

            RefreshBlendGraphOverlayTitle();
            ClampFreeformBlendGraphOverlayPosition();
        }

        private static float GetResolvedElementHeight(VisualElement element)
        {
            if (element == null)
            {
                return 0f;
            }

            float resolvedHeight = element.resolvedStyle.height;
            if (!float.IsNaN(resolvedHeight) && resolvedHeight > 0f)
            {
                return resolvedHeight;
            }

            Rect layout = element.layout;
            return layout.height > 0f ? layout.height : 0f;
        }

        private void SetBlendGraphOverlayTitle(string title)
        {
            m_FreeformBlendGraphTitleText = string.IsNullOrWhiteSpace(title) ? "Blend Graph" : title;
            RefreshBlendGraphOverlayTitle();
        }

        private void RefreshBlendGraphOverlayTitle()
        {
            if (m_FreeformBlendGraphTitleLabel == null)
            {
                return;
            }

            m_FreeformBlendGraphTitleLabel.text = m_FreeformBlendGraphOverlayExpanded
                ? $"▾ {m_FreeformBlendGraphTitleText}"
                : $"▸ {m_FreeformBlendGraphTitleText}";
        }

        private void ClampFreeformBlendGraphOverlayPosition()
        {
            if (m_FreeformBlendGraphOverlay == null || m_FreeformBlendGraphOverlay.parent == null)
            {
                return;
            }

            Rect parentBounds = m_FreeformBlendGraphOverlay.parent.contentRect;
            float cardWidth = Mathf.Max(FreeformBlendGraphOverlayWidth, m_FreeformBlendGraphOverlay.resolvedStyle.width);
            float cardHeight = Mathf.Max(0f, m_FreeformBlendGraphOverlay.resolvedStyle.height);
            float maxX = Mathf.Max(0f, parentBounds.width - cardWidth);
            float maxBottom = Mathf.Max(0f, parentBounds.height - cardHeight);
            m_FreeformBlendGraphOverlayPosition = new Vector2(
                Mathf.Clamp(m_FreeformBlendGraphOverlayPosition.x, 0f, maxX),
                Mathf.Clamp(m_FreeformBlendGraphOverlayPosition.y, 0f, maxBottom));

            m_FreeformBlendGraphOverlay.style.left = m_FreeformBlendGraphOverlayPosition.x;
            m_FreeformBlendGraphOverlay.style.bottom = m_FreeformBlendGraphOverlayPosition.y;
        }

        private static void ConfigureConsoleSection(VisualElement section)
        {
            section.style.minHeight = CueLogSectionMinHeight;
            section.style.flexGrow = 1;
            section.style.flexShrink = 1;
            section.style.overflow = Overflow.Hidden;
        }

        private static VisualElement BuildInspectorConsoleSplit(VisualElement inspectorPane, VisualElement cueCard)
        {
            inspectorPane.style.minHeight = InspectorMinHeight;
            inspectorPane.style.flexGrow = 1;
            inspectorPane.style.flexShrink = 1;

            TwoPaneSplitView splitView = new(1, CueLogInitialHeight, TwoPaneSplitViewOrientation.Vertical);
            splitView.style.flexGrow = 1;
            splitView.style.minHeight = 0;
            splitView.Add(inspectorPane);
            splitView.Add(cueCard);
            return splitView;
        }

    }
}
#endif
