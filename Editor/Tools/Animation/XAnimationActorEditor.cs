#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;
using XFramework.Resource;

namespace XFramework.Editor
{
    [CustomEditor(typeof(XAnimationActor))]
    public sealed class XAnimationActorEditor : UnityEditor.Editor
    {
        private const float SectionTitleFontSize = 12f;
        private const float BodyFontSize = 11f;
        private const float PlaybackLabelWidth = 118f;

        private static readonly Color SectionDivider = new(0.28f, 0.28f, 0.30f, 1f);
        private static readonly Color AccentColor = new(0.30f, 0.55f, 0.95f, 1f);
        private static readonly Color DangerColor = new(0.75f, 0.25f, 0.25f, 1f);
        private static readonly Color TextMuted = new(0.60f, 0.60f, 0.62f, 1f);
        private static readonly Color TextNormal = new(0.85f, 0.85f, 0.87f, 1f);
        private static readonly Color HoverBg = new(0.24f, 0.24f, 0.26f, 1f);
        private static readonly Color ListGroupBg = new(0.17f, 0.18f, 0.20f, 1f);
        private static readonly Color ListRowEvenBg = new(0.16f, 0.16f, 0.17f, 1f);
        private static readonly Color ListRowOddBg = new(0.19f, 0.19f, 0.20f, 1f);
        private static readonly Color ListHeaderBg = new(0.22f, 0.23f, 0.25f, 1f);
        private static readonly Color PlayingBg = new(0.20f, 0.35f, 0.55f, 0.65f);

        private sealed class ChannelNameOption
        {
            public string Name;
            public string DisplayName;
            public int ChannelOrder;

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class ClipRowVisualState
        {
            public Color BaseColor;
            public bool Hovered;
            public bool Playing;
        }

        private sealed class FoldoutCard
        {
            public VisualElement Root;
            public VisualElement Content;
            public Action<bool> SetExpanded;
            public Action RefreshState;
        }

        private readonly Dictionary<string, VisualElement> m_StateRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_StateButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Color> m_StateBaseColorMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ClipRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_ClipButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ClipRowVisualState> m_ClipVisualStateMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> m_RuntimeFloatPreviewValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_RuntimeIntPreviewValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> m_RuntimeBoolPreviewValues = new(StringComparer.Ordinal);

        private FloatField m_PlaySpeedField;
        private DropdownField m_PlayTargetChannelField;
        private Toggle m_ApplyTargetToggle;
        private FloatField m_PlayFadeInField;
        private FloatField m_PlayFadeOutField;
        private IntegerField m_PlayPriorityField;
        private Toggle m_ApplyTransitionToggle;
        private Toggle m_PlayInterruptibleToggle;
        private FloatField m_PlayEnterTimeField;
        private Button m_PlayCommandButton;
        private VisualElement m_ParametersListView;
        private VisualElement m_StatesListView;
        private VisualElement m_ClipsListView;
        private Label m_StatusLabel;
        private IVisualElementScheduledItem m_RefreshItem;
        private VisualElement m_Root;
        private bool m_RuntimeViewsDirty = true;
        private bool m_LastPlayingState;
        private int m_LastAnimationAssetInstanceId;
        private int m_CachedSelectedAssetInstanceId = int.MinValue;
        private XAnimationAsset m_CachedAnimationAsset;
        private List<ChannelNameOption> m_CachedChannelOptions;
        private readonly Dictionary<string, AnimationClip> m_CachedClipObjectMap = new(StringComparer.Ordinal);

        private string m_PlayTargetChannelName;
        private bool m_ApplyTargetOverrides;
        private float m_PlayFadeInOverride;
        private float m_PlayFadeOutOverride;
        private int m_PlayPriorityOverride;
        private bool m_PlayInterruptibleOverride = true;
        private bool m_ApplyTransitionOverrides;
        private float m_PlayEnterTimeOverride;
        private float m_PlaySpeed = 1f;
        private bool m_PlaybackPrefsLoaded;

        [SerializeField] private bool m_PlaybackSectionExpanded = true;
        [SerializeField] private bool m_PlayTargetSectionExpanded = true;
        [SerializeField] private bool m_PlayTransitionSectionExpanded;
        [SerializeField] private bool m_ParametersSectionExpanded = true;
        [SerializeField] private bool m_StatesSectionExpanded = true;
        [SerializeField] private bool m_ClipsSectionExpanded = true;

        public override VisualElement CreateInspectorGUI()
        {
            LoadPlaybackPrefs();

            VisualElement root = new()
            {
                style =
                {
                    paddingTop = 4,
                    paddingBottom = 4,
                }
            };
            m_Root = root;
            m_RuntimeViewsDirty = true;

            PropertyField animationAssetField = AddProperty(root, "m_AnimationAsset");
            AddProperty(root, "m_Animator");
            AddProperty(root, "m_InitializeOnAwake");
            AddProperty(root, "m_PlayOnStart");

            VisualElement startStateKeyContainer = new();
            root.Add(startStateKeyContainer);
            RebuildStateKeyPopup(startStateKeyContainer, "m_StartStateKey", "Start State Key");

            AddProperty(root, "m_TimeScale");
            root.Add(BuildRuntimeInspector());

            animationAssetField?.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                RebuildStateKeyPopup(startStateKeyContainer, "m_StartStateKey", "Start State Key");
                MarkRuntimeViewsDirty();
                RefreshRuntimeViews();
            });

            root.RegisterCallback<AttachToPanelEvent>(_ => StartRefreshLoop());
            root.RegisterCallback<DetachFromPanelEvent>(_ => StopRefreshLoop());
            root.schedule.Execute(RefreshRuntimeViews).ExecuteLater(0);
            return root;
        }

        private VisualElement BuildRuntimeInspector()
        {
            VisualElement root = new();
            root.style.marginTop = 8;

            FoldoutCard playbackCard = CreateFoldoutCard("播放设置", m_PlaybackSectionExpanded, value =>
            {
                m_PlaybackSectionExpanded = value;
                SavePlaybackPrefs();
            });
            playbackCard.Content.Add(BuildPlaybackSettingsContent());
            root.Add(playbackCard.Root);

            FoldoutCard parametersCard = CreateFoldoutCard("Parameters", m_ParametersSectionExpanded, value => m_ParametersSectionExpanded = value);
            m_ParametersListView = new VisualElement();
            parametersCard.Content.Add(m_ParametersListView);
            root.Add(parametersCard.Root);

            FoldoutCard statesCard = CreateFoldoutCard("States", m_StatesSectionExpanded, value => m_StatesSectionExpanded = value);
            m_StatesListView = new VisualElement();
            statesCard.Content.Add(m_StatesListView);
            root.Add(statesCard.Root);

            FoldoutCard clipsCard = CreateFoldoutCard("Clips", m_ClipsSectionExpanded, value => m_ClipsSectionExpanded = value);
            m_ClipsListView = new VisualElement();
            clipsCard.Content.Add(m_ClipsListView);
            root.Add(clipsCard.Root);

            m_StatusLabel = new("Play Mode 下可直接调试播放和参数。")
            {
                style =
                {
                    marginTop = 6,
                    color = TextMuted,
                    fontSize = BodyFontSize,
                    whiteSpace = WhiteSpace.Normal,
                }
            };
            root.Add(m_StatusLabel);
            return root;
        }

        private void LoadPlaybackPrefs()
        {
            XAnimationPlaybackSettings settings = XAnimationPlaybackSettingsPrefs.Load();
            m_PlaybackSectionExpanded = settings.PlaybackSectionExpanded;
            m_PlayTargetSectionExpanded = settings.TargetSectionExpanded;
            m_PlayTransitionSectionExpanded = settings.TransitionSectionExpanded;
            m_PlayTargetChannelName = settings.ChannelName;
            m_ApplyTargetOverrides = settings.ApplyTarget;
            m_PlaySpeed = Mathf.Approximately(settings.Speed, 0f) ? 1f : settings.Speed;
            m_ApplyTransitionOverrides = settings.ApplyTransition;
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

        private void SavePlaybackPrefs()
        {
            if (!m_PlaybackPrefsLoaded)
            {
                return;
            }

            float speed = m_PlaySpeedField?.value ?? m_PlaySpeed;
            m_PlaySpeed = Mathf.Approximately(speed, 0f) ? 1f : speed;

            XAnimationPlaybackSettingsPrefs.Save(new XAnimationPlaybackSettings
            {
                PlaybackSectionExpanded = m_PlaybackSectionExpanded,
                TargetSectionExpanded = m_PlayTargetSectionExpanded,
                TransitionSectionExpanded = m_PlayTransitionSectionExpanded,
                ChannelName = m_PlayTargetChannelName,
                ApplyTarget = m_ApplyTargetOverrides,
                Speed = m_PlaySpeed,
                ApplyTransition = m_ApplyTransitionOverrides,
                FadeIn = m_PlayFadeInOverride,
                FadeOut = m_PlayFadeOutOverride,
                Priority = m_PlayPriorityOverride,
                Interruptible = m_PlayInterruptibleOverride,
                EnterTime = m_PlayEnterTimeOverride,
            });
        }

        private VisualElement BuildPlaybackSettingsContent()
        {
            VisualElement content = new();

            VisualElement speedRow = new();
            speedRow.style.flexDirection = FlexDirection.Row;
            speedRow.style.alignItems = Align.Center;
            content.Add(speedRow);

            m_PlaySpeedField = new FloatField { value = GetPlaybackSpeed() };
            ConfigureCompactPlaybackField(m_PlaySpeedField, 66);
            m_PlaySpeedField.RegisterValueChangedCallback(_ => SavePlaybackPrefs());
            VisualElement speedFieldRow = CreatePlaybackFieldContainer("speed", m_PlaySpeedField, PlaybackLabelWidth);
            speedFieldRow.style.flexGrow = 1;
            speedRow.Add(speedFieldRow);

            m_PlayCommandButton = new Button(PlayConfiguredCommand)
            {
                text = "Play"
            };
            m_PlayCommandButton.style.marginLeft = 8;
            speedRow.Add(m_PlayCommandButton);

            m_ApplyTargetToggle = CreateHeaderApplyToggle(m_ApplyTargetOverrides, "播放 state 时是否应用 target.channelName 覆盖；播放 clip 时始终会应用。");
            m_ApplyTargetToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyTargetOverrides = evt.newValue;
                SavePlaybackPrefs();
            });

            FoldoutCard targetCard = CreateSectionFoldoutCard("Target", m_PlayTargetSectionExpanded, value =>
            {
                m_PlayTargetSectionExpanded = value;
                SavePlaybackPrefs();
            }, m_ApplyTargetToggle);
            targetCard.Root.style.marginTop = 4;

            m_PlayTargetChannelField = new DropdownField();
            m_PlayTargetChannelField.tooltip = "播放 target.channelName。用于 clip 调试播放，也可在 Apply Target 开启时覆盖 state 的默认 channel。";
            m_PlayTargetChannelField.style.flexGrow = 1;
            m_PlayTargetChannelField.style.minWidth = 0;
            m_PlayTargetChannelField.RegisterValueChangedCallback(evt =>
            {
                m_PlayTargetChannelName = NormalizeChannelOptionValue(evt.newValue) ?? string.Empty;
                SavePlaybackPrefs();
            });
            targetCard.Content.Add(CreatePlaybackFieldContainer("channelName", m_PlayTargetChannelField, PlaybackLabelWidth));
            content.Add(targetCard.Root);

            m_ApplyTransitionToggle = CreateHeaderApplyToggle(m_ApplyTransitionOverrides, "是否应用 command.transition。关闭时本分区会自动收起。");
            FoldoutCard transitionCard = CreateSectionFoldoutCard("Transition", m_PlayTransitionSectionExpanded, value =>
            {
                m_PlayTransitionSectionExpanded = value;
                SavePlaybackPrefs();
            }, m_ApplyTransitionToggle, () => m_ApplyTransitionOverrides);
            transitionCard.Root.style.marginTop = 4;
            m_ApplyTransitionToggle.RegisterValueChangedCallback(evt =>
            {
                m_ApplyTransitionOverrides = evt.newValue;
                if (!evt.newValue)
                {
                    transitionCard.SetExpanded?.Invoke(false);
                }
                transitionCard.RefreshState?.Invoke();
                SavePlaybackPrefs();
            });

            m_PlayFadeInField = new FloatField { value = m_PlayFadeInOverride };
            ConfigureCompactPlaybackField(m_PlayFadeInField, 66);
            m_PlayFadeInField.RegisterValueChangedCallback(evt =>
            {
                m_PlayFadeInOverride = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(m_PlayFadeInOverride, evt.newValue))
                {
                    m_PlayFadeInField.SetValueWithoutNotify(m_PlayFadeInOverride);
                }

                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackFieldContainer("fadeIn", m_PlayFadeInField, PlaybackLabelWidth));

            m_PlayFadeOutField = new FloatField { value = m_PlayFadeOutOverride };
            ConfigureCompactPlaybackField(m_PlayFadeOutField, 66);
            m_PlayFadeOutField.RegisterValueChangedCallback(evt =>
            {
                m_PlayFadeOutOverride = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(m_PlayFadeOutOverride, evt.newValue))
                {
                    m_PlayFadeOutField.SetValueWithoutNotify(m_PlayFadeOutOverride);
                }

                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackFieldContainer("fadeOut", m_PlayFadeOutField, PlaybackLabelWidth));

            m_PlayPriorityField = new IntegerField { value = m_PlayPriorityOverride };
            ConfigureCompactPlaybackElement(m_PlayPriorityField, 66);
            m_PlayPriorityField.RegisterValueChangedCallback(evt =>
            {
                m_PlayPriorityOverride = evt.newValue;
                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackFieldContainer("priority", m_PlayPriorityField, PlaybackLabelWidth));

            m_PlayInterruptibleToggle = new Toggle { value = m_PlayInterruptibleOverride };
            m_PlayInterruptibleToggle.RegisterValueChangedCallback(evt =>
            {
                m_PlayInterruptibleOverride = evt.newValue;
                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackToggleRow("interruptible", m_PlayInterruptibleToggle, PlaybackLabelWidth));

            m_PlayEnterTimeField = new FloatField { value = m_PlayEnterTimeOverride };
            ConfigureCompactPlaybackField(m_PlayEnterTimeField, 72);
            m_PlayEnterTimeField.RegisterValueChangedCallback(evt =>
            {
                m_PlayEnterTimeOverride = Mathf.Clamp01(evt.newValue);
                if (!Mathf.Approximately(m_PlayEnterTimeOverride, evt.newValue))
                {
                    m_PlayEnterTimeField.SetValueWithoutNotify(m_PlayEnterTimeOverride);
                }

                SavePlaybackPrefs();
            });
            transitionCard.Content.Add(CreatePlaybackFieldContainer("enterTime", m_PlayEnterTimeField, PlaybackLabelWidth));
            content.Add(transitionCard.Root);

            return content;
        }

        private void StartRefreshLoop()
        {
            StopRefreshLoop();
            if (m_Root == null)
            {
                return;
            }

            m_RefreshItem = m_Root.schedule.Execute(RefreshRuntimeLoop).Every(200);
        }

        private void StopRefreshLoop()
        {
            m_RefreshItem?.Pause();
            m_RefreshItem = null;
        }

        private void RefreshRuntimeViews()
        {
            RefreshRuntimeViewState();
            RefreshPlayButtonState();
            if (m_RuntimeViewsDirty)
            {
                RefreshChannelChoices();
                RebuildParameterList();
                RebuildStateList();
                RebuildClipList();
                m_RuntimeViewsDirty = false;
            }

            RefreshStatePlayingStates();
            RefreshClipPlayingStates();
        }

        private void RefreshRuntimeLoop()
        {
            RefreshRuntimeViews();
        }

        private void RefreshRuntimeViewState()
        {
            int currentAssetInstanceId = GetCurrentAnimationAssetInstanceId();
            bool isPlaying = Application.isPlaying;
            if (currentAssetInstanceId != m_LastAnimationAssetInstanceId || isPlaying != m_LastPlayingState)
            {
                m_LastAnimationAssetInstanceId = currentAssetInstanceId;
                m_LastPlayingState = isPlaying;
                m_RuntimeViewsDirty = true;
            }
        }

        private void MarkRuntimeViewsDirty()
        {
            m_RuntimeViewsDirty = true;
            InvalidateAnimationAssetCache();
        }

        private int GetCurrentAnimationAssetInstanceId()
        {
            SerializedProperty assetProperty = serializedObject.FindProperty("m_AnimationAsset");
            return assetProperty?.objectReferenceValue != null ? assetProperty.objectReferenceValue.GetInstanceID() : 0;
        }

        private void RefreshPlayButtonState()
        {
            if (m_PlayCommandButton != null)
            {
                m_PlayCommandButton.SetEnabled(Application.isPlaying);
            }
        }

        private void RefreshChannelChoices()
        {
            if (m_PlayTargetChannelField == null)
            {
                return;
            }

            List<string> choices = new();
            List<ChannelNameOption> options = GetChannelOptions();
            for (int i = 0; i < options.Count; i++)
            {
                choices.Add(options[i].DisplayName);
            }

            string selected = string.IsNullOrWhiteSpace(m_PlayTargetChannelName) ? FindFirstChannelName(options) : FindChannelDisplayName(options, m_PlayTargetChannelName);
            if (string.IsNullOrWhiteSpace(selected) && choices.Count > 0)
            {
                selected = choices[0];
            }

            m_PlayTargetChannelField.choices = choices;
            m_PlayTargetChannelField.SetValueWithoutNotify(selected ?? string.Empty);
            m_PlayTargetChannelName = NormalizeChannelOptionValue(selected);
            m_PlayTargetChannelField.SetEnabled(choices.Count > 0);
        }

        private void RebuildParameterList()
        {
            m_ParametersListView?.Clear();
            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (m_ParametersListView == null || asset?.parameters == null || asset.parameters.Length == 0)
            {
                m_RuntimeFloatPreviewValues.Clear();
                m_RuntimeIntPreviewValues.Clear();
                m_RuntimeBoolPreviewValues.Clear();
                AddEmptyLabel(m_ParametersListView, "No parameters");
                return;
            }

            HashSet<string> validFloatKeys = new(StringComparer.Ordinal);
            HashSet<string> validIntKeys = new(StringComparer.Ordinal);
            HashSet<string> validBoolKeys = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.parameters.Length; i++)
            {
                XAnimationParameterConfig parameter = asset.parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.name))
                {
                    continue;
                }

                switch (parameter.type)
                {
                    case XAnimationParameterType.Float:
                        validFloatKeys.Add(parameter.name);
                        if (!m_RuntimeFloatPreviewValues.ContainsKey(parameter.name))
                        {
                            m_RuntimeFloatPreviewValues[parameter.name] = ConvertParameterDefaultToFloat(parameter.defaultValue);
                        }
                        break;
                    case XAnimationParameterType.Int:
                        validIntKeys.Add(parameter.name);
                        if (!m_RuntimeIntPreviewValues.ContainsKey(parameter.name))
                        {
                            m_RuntimeIntPreviewValues[parameter.name] = ConvertParameterDefaultToInt(parameter.defaultValue);
                        }
                        break;
                    case XAnimationParameterType.Bool:
                        validBoolKeys.Add(parameter.name);
                        if (!m_RuntimeBoolPreviewValues.ContainsKey(parameter.name))
                        {
                            m_RuntimeBoolPreviewValues[parameter.name] = ConvertParameterDefaultToBool(parameter.defaultValue);
                        }
                        break;
                }

                m_ParametersListView.Add(CreateParameterRow(parameter, i));
            }

            RemoveStaleRuntimePreviewValues(m_RuntimeFloatPreviewValues, validFloatKeys);
            RemoveStaleRuntimePreviewValues(m_RuntimeIntPreviewValues, validIntKeys);
            RemoveStaleRuntimePreviewValues(m_RuntimeBoolPreviewValues, validBoolKeys);
        }

        private VisualElement CreateParameterRow(XAnimationParameterConfig parameter, int rowIndex)
        {
            VisualElement container = CreateRowContainer(rowIndex);
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            container.Add(row);

            Label nameLabel = new(parameter.name);
            nameLabel.style.width = 140;
            nameLabel.style.flexShrink = 0;
            nameLabel.style.color = TextNormal;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(nameLabel);

            Label typeLabel = new(parameter.type.ToString());
            typeLabel.style.width = 72;
            typeLabel.style.flexShrink = 0;
            typeLabel.style.color = TextMuted;
            typeLabel.style.marginLeft = 6;
            row.Add(typeLabel);

            VisualElement field = CreateRuntimeParameterField(parameter);
            field.style.flexGrow = 1;
            field.style.marginLeft = 8;
            row.Add(field);
            return container;
        }

        private VisualElement CreateRuntimeParameterField(XAnimationParameterConfig parameter)
        {
            XAnimationActor actor = target as XAnimationActor;
            switch (parameter.type)
            {
                case XAnimationParameterType.Float:
                {
                    FloatField field = new("value")
                    {
                        value = GetRuntimeFloatPreviewValue(parameter)
                    };
                    field.SetEnabled(Application.isPlaying);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        m_RuntimeFloatPreviewValues[parameter.name] = evt.newValue;
                        if (!Application.isPlaying || actor == null)
                        {
                            return;
                        }

                        try
                        {
                            actor.SetParameter(parameter.name, evt.newValue);
                            SetStatus($"{parameter.name} = {evt.newValue:0.###}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex, actor);
                            SetStatus(ex.Message, true);
                        }
                    });
                    return field;
                }
                case XAnimationParameterType.Bool:
                {
                    Toggle toggle = new("value")
                    {
                        value = GetRuntimeBoolPreviewValue(parameter)
                    };
                    toggle.SetEnabled(Application.isPlaying);
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        m_RuntimeBoolPreviewValues[parameter.name] = evt.newValue;
                        if (!Application.isPlaying || actor == null)
                        {
                            return;
                        }

                        try
                        {
                            actor.SetParameter(parameter.name, evt.newValue);
                            SetStatus($"{parameter.name} = {evt.newValue}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex, actor);
                            SetStatus(ex.Message, true);
                        }
                    });
                    return toggle;
                }
                case XAnimationParameterType.Int:
                {
                    IntegerField field = new("value")
                    {
                        value = GetRuntimeIntPreviewValue(parameter)
                    };
                    field.SetEnabled(Application.isPlaying);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        m_RuntimeIntPreviewValues[parameter.name] = evt.newValue;
                        if (!Application.isPlaying || actor == null)
                        {
                            return;
                        }

                        try
                        {
                            actor.SetParameter(parameter.name, evt.newValue);
                            SetStatus($"{parameter.name} = {evt.newValue}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex, actor);
                            SetStatus(ex.Message, true);
                        }
                    });
                    return field;
                }
                case XAnimationParameterType.Trigger:
                default:
                {
                    Button button = new(() =>
                    {
                        if (!Application.isPlaying || actor == null)
                        {
                            return;
                        }

                        try
                        {
                            actor.SetTrigger(parameter.name);
                            SetStatus($"Trigger {parameter.name} 已触发。");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex, actor);
                            SetStatus(ex.Message, true);
                        }
                    })
                    {
                        text = "Trigger"
                    };
                    button.SetEnabled(Application.isPlaying);
                    return button;
                }
            }
        }

        private float GetRuntimeFloatPreviewValue(XAnimationParameterConfig parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.name))
            {
                return 0f;
            }

            return m_RuntimeFloatPreviewValues.TryGetValue(parameter.name, out float value)
                ? value
                : ConvertParameterDefaultToFloat(parameter.defaultValue);
        }

        private bool GetRuntimeBoolPreviewValue(XAnimationParameterConfig parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.name))
            {
                return false;
            }

            return m_RuntimeBoolPreviewValues.TryGetValue(parameter.name, out bool value)
                ? value
                : ConvertParameterDefaultToBool(parameter.defaultValue);
        }

        private int GetRuntimeIntPreviewValue(XAnimationParameterConfig parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.name))
            {
                return 0;
            }

            return m_RuntimeIntPreviewValues.TryGetValue(parameter.name, out int value)
                ? value
                : ConvertParameterDefaultToInt(parameter.defaultValue);
        }

        private static void RemoveStaleRuntimePreviewValues<T>(Dictionary<string, T> cache, HashSet<string> validKeys)
        {
            if (cache.Count == 0)
            {
                return;
            }

            List<string> removedKeys = null;
            foreach (string key in cache.Keys)
            {
                if (validKeys.Contains(key))
                {
                    continue;
                }

                removedKeys ??= new List<string>();
                removedKeys.Add(key);
            }

            if (removedKeys == null)
            {
                return;
            }

            for (int i = 0; i < removedKeys.Count; i++)
            {
                cache.Remove(removedKeys[i]);
            }
        }

        private void RebuildStateList()
        {
            m_StatesListView?.Clear();
            m_StateRowMap.Clear();
            m_StateButtonMap.Clear();
            m_StateBaseColorMap.Clear();

            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (m_StatesListView == null || asset?.states == null || asset.states.Length == 0 || asset.channels == null)
            {
                AddEmptyLabel(m_StatesListView, "No states");
                return;
            }

            Dictionary<string, List<XAnimationStateConfig>> statesByChannel = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channel = asset.channels[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                {
                    continue;
                }

                statesByChannel[channel.name] = new List<XAnimationStateConfig>();
            }

            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state == null || string.IsNullOrWhiteSpace(state.key))
                {
                    continue;
                }

                if (!statesByChannel.TryGetValue(state.channelName ?? string.Empty, out List<XAnimationStateConfig> channelStates))
                {
                    channelStates = new List<XAnimationStateConfig>();
                    statesByChannel[state.channelName ?? string.Empty] = channelStates;
                }

                channelStates.Add(state);
            }

            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channel = asset.channels[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                {
                    continue;
                }

                statesByChannel.TryGetValue(channel.name, out List<XAnimationStateConfig> channelStates);
                m_StatesListView.Add(CreateStateChannelGroup(channel, channelStates ?? new List<XAnimationStateConfig>()));
            }
        }

        private VisualElement CreateStateChannelGroup(XAnimationChannelConfig channel, List<XAnimationStateConfig> channelStates)
        {
            VisualElement group = new();
            group.style.marginBottom = 3;
            group.style.paddingLeft = 3;
            group.style.paddingRight = 3;
            group.style.paddingTop = 3;
            group.style.paddingBottom = 2;
            group.style.backgroundColor = ListGroupBg;
            group.style.borderTopWidth = 1;
            group.style.borderBottomWidth = 1;
            group.style.borderLeftWidth = 1;
            group.style.borderRightWidth = 1;
            group.style.borderTopColor = SectionDivider;
            group.style.borderBottomColor = SectionDivider;
            group.style.borderLeftColor = SectionDivider;
            group.style.borderRightColor = SectionDivider;
            group.style.borderTopLeftRadius = 3;
            group.style.borderTopRightRadius = 3;
            group.style.borderBottomLeftRadius = 3;
            group.style.borderBottomRightRadius = 3;

            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 2;
            header.style.paddingLeft = 3;
            header.style.paddingRight = 3;
            header.style.paddingTop = 2;
            header.style.paddingBottom = 2;
            header.style.backgroundColor = ListHeaderBg;
            header.style.borderTopLeftRadius = 3;
            header.style.borderTopRightRadius = 3;
            header.style.borderBottomLeftRadius = 3;
            header.style.borderBottomRightRadius = 3;
            group.Add(header);

            Label title = new(channel.name);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextNormal;
            title.style.flexGrow = 1;
            header.Add(title);

            Label info = new($"{channel.layerType} | {channelStates.Count} states");
            info.style.color = TextMuted;
            info.style.fontSize = 10;
            header.Add(info);

            for (int i = 0; i < channelStates.Count; i++)
            {
                XAnimationStateConfig state = channelStates[i];
                group.Add(CreateStateRow(state, i));
            }

            if (channelStates.Count == 0)
            {
                AddEmptyLabel(group, "No states");
            }

            return group;
        }

        private VisualElement CreateStateRow(XAnimationStateConfig state, int rowIndex)
        {
            VisualElement row = CreateRowContainer(rowIndex);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            m_StateBaseColorMap[state.key] = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;

            Label nameLabel = new(state.key);
            nameLabel.style.width = 140;
            nameLabel.style.flexShrink = 0;
            nameLabel.style.color = TextNormal;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(nameLabel);

            Label infoLabel = new(BuildStateInfoText(state));
            infoLabel.style.flexGrow = 1;
            infoLabel.style.color = TextMuted;
            infoLabel.style.fontSize = BodyFontSize;
            row.Add(infoLabel);

            Button playButton = new(() => ToggleStatePlayback(state))
            {
                text = "▶"
            };
            ApplyIconButtonStyle(playButton, false);
            playButton.style.marginLeft = 6;
            playButton.SetEnabled(Application.isPlaying);
            row.Add(playButton);

            m_StateRowMap[state.key] = row;
            m_StateButtonMap[state.key] = playButton;
            return row;
        }

        private void ToggleStatePlayback(XAnimationStateConfig state)
        {
            XAnimationActor actor = target as XAnimationActor;
            if (actor == null || !Application.isPlaying)
            {
                return;
            }

            try
            {
                string channelName = FindPlayingChannelForState(actor, state.key) ?? GetStateTargetChannelName(state) ?? state.channelName;
                XAnimationChannelState channelState = string.IsNullOrWhiteSpace(channelName) ? null : actor.GetChannelState(channelName);
                bool isPlaying = channelState != null && string.Equals(channelState.stateKey, state.key, StringComparison.Ordinal);
                if (isPlaying)
                {
                    actor.Stop(channelName, 0f);
                    SetStatus($"已停止 state {state.key}。");
                }
                else
                {
                    actor.Play(BuildPlayCommand(stateKey: state.key, clipKey: null, channelName: GetStateTargetChannelName(state)));
                    SetStatus($"正在播放 state {state.key}。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, actor);
                SetStatus(ex.Message, true);
            }

            RefreshRuntimeViews();
        }

        private void RebuildClipList()
        {
            m_ClipsListView?.Clear();
            m_ClipRowMap.Clear();
            m_ClipButtonMap.Clear();
            m_ClipVisualStateMap.Clear();

            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (m_ClipsListView == null || asset?.clips == null || asset.clips.Length == 0)
            {
                AddEmptyLabel(m_ClipsListView, "No clips");
                return;
            }

            for (int i = 0; i < asset.clips.Length; i++)
            {
                XAnimationClipConfig clip = asset.clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.key))
                {
                    continue;
                }

                m_ClipsListView.Add(CreateClipRow(clip, i));
            }
        }

        private VisualElement CreateClipRow(XAnimationClipConfig clip, int rowIndex)
        {
            VisualElement row = CreateRowContainer(rowIndex);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            m_ClipRowMap[clip.key] = row;

            if (m_ClipVisualStateMap.TryGetValue(clip.key, out ClipRowVisualState existingState))
            {
                existingState.BaseColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            }
            else
            {
                m_ClipVisualStateMap[clip.key] = new ClipRowVisualState
                {
                    BaseColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg
                };
            }

            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (m_ClipVisualStateMap.TryGetValue(clip.key, out ClipRowVisualState state))
                {
                    state.Hovered = true;
                    ApplyClipRowVisualState(clip.key);
                }
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (m_ClipVisualStateMap.TryGetValue(clip.key, out ClipRowVisualState state))
                {
                    state.Hovered = false;
                    ApplyClipRowVisualState(clip.key);
                }
            });

            Label nameLabel = new(clip.key);
            nameLabel.style.width = 140;
            nameLabel.style.flexShrink = 0;
            nameLabel.style.color = TextNormal;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(nameLabel);

            ObjectField clipField = new()
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
            };
            clipField.SetValueWithoutNotify(GetClipObject(clip));
            clipField.SetEnabled(false);
            clipField.style.opacity = 1f;
            clipField.style.flexGrow = 1;
            clipField.style.marginRight = 4;
            row.Add(clipField);

            Button playButton = new(() => ToggleClipPlayback(clip))
            {
                text = "▶"
            };
            ApplyIconButtonStyle(playButton, false);
            playButton.style.marginLeft = 6;
            playButton.SetEnabled(Application.isPlaying);
            row.Add(playButton);

            m_ClipButtonMap[clip.key] = playButton;
            return row;
        }

        private void ToggleClipPlayback(XAnimationClipConfig clip)
        {
            XAnimationActor actor = target as XAnimationActor;
            if (actor == null || !Application.isPlaying)
            {
                return;
            }

            string channelName = m_PlayTargetChannelName;
            if (string.IsNullOrWhiteSpace(channelName))
            {
                SetStatus("请先在 Target 中选择 channelName 后再调试播放 clip。", true);
                return;
            }

            try
            {
                XAnimationChannelState state = actor.GetChannelState(channelName);
                bool isPlaying = state != null && string.Equals(state.clipKey, clip.key, StringComparison.Ordinal);
                if (isPlaying)
                {
                    actor.Stop(channelName, 0f);
                    SetStatus($"已停止 clip {clip.key}。");
                }
                else
                {
                    actor.Play(BuildPlayCommand(stateKey: null, clipKey: clip.key, channelName: channelName));
                    SetStatus($"正在 {channelName} 播放 clip {clip.key}。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, actor);
                SetStatus(ex.Message, true);
            }

            RefreshRuntimeViews();
        }

        private void RefreshStatePlayingStates()
        {
            XAnimationActor actor = target as XAnimationActor;
            HashSet<string> playingStateKeys = null;
            if (actor != null && Application.isPlaying && actor.IsInitialized)
            {
                XAnimationAsset asset = LoadCurrentAnimationAsset();
                if (asset?.channels != null)
                {
                    for (int i = 0; i < asset.channels.Length; i++)
                    {
                        XAnimationChannelConfig channel = asset.channels[i];
                        if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                        {
                            continue;
                        }

                        XAnimationChannelState state = actor.GetChannelState(channel.name);
                        if (state != null && !string.IsNullOrWhiteSpace(state.stateKey))
                        {
                            playingStateKeys ??= new HashSet<string>(StringComparer.Ordinal);
                            playingStateKeys.Add(state.stateKey);
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, VisualElement> kvp in m_StateRowMap)
            {
                bool isPlaying = playingStateKeys != null && playingStateKeys.Contains(kvp.Key);
                kvp.Value.style.backgroundColor = isPlaying
                    ? PlayingBg
                    : m_StateBaseColorMap.TryGetValue(kvp.Key, out Color baseColor)
                        ? baseColor
                        : ListRowEvenBg;
                if (m_StateButtonMap.TryGetValue(kvp.Key, out Button button))
                {
                    ApplyIconButtonStyle(button, isPlaying);
                }
            }
        }

        private void RefreshClipPlayingStates()
        {
            XAnimationActor actor = target as XAnimationActor;
            HashSet<string> playingClipKeys = null;
            if (actor != null && Application.isPlaying && actor.IsInitialized)
            {
                XAnimationAsset asset = LoadCurrentAnimationAsset();
                if (asset?.channels != null)
                {
                    for (int i = 0; i < asset.channels.Length; i++)
                    {
                        XAnimationChannelConfig channel = asset.channels[i];
                        if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                        {
                            continue;
                        }

                        XAnimationChannelState state = actor.GetChannelState(channel.name);
                        if (state != null && !string.IsNullOrWhiteSpace(state.clipKey))
                        {
                            playingClipKeys ??= new HashSet<string>(StringComparer.Ordinal);
                            playingClipKeys.Add(state.clipKey);
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, VisualElement> kvp in m_ClipRowMap)
            {
                bool isPlaying = playingClipKeys != null && playingClipKeys.Contains(kvp.Key);
                if (m_ClipVisualStateMap.TryGetValue(kvp.Key, out ClipRowVisualState visualState))
                {
                    visualState.Playing = isPlaying;
                    ApplyClipRowVisualState(kvp.Key);
                }

                if (m_ClipButtonMap.TryGetValue(kvp.Key, out Button button))
                {
                    ApplyIconButtonStyle(button, isPlaying);
                }
            }
        }

        private void ApplyClipRowVisualState(string clipKey)
        {
            if (!m_ClipRowMap.TryGetValue(clipKey, out VisualElement row) ||
                !m_ClipVisualStateMap.TryGetValue(clipKey, out ClipRowVisualState visualState))
            {
                return;
            }

            row.style.backgroundColor = visualState.Playing
                ? PlayingBg
                : visualState.Hovered
                    ? HoverBg
                    : visualState.BaseColor;
        }

        private void PlayConfiguredCommand()
        {
            XAnimationActor actor = target as XAnimationActor;
            if (actor == null || !Application.isPlaying)
            {
                return;
            }

            try
            {
                actor.PlayConfiguredRequest();
                SetStatus("已执行当前播放命令。");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, actor);
                SetStatus(ex.Message, true);
            }
        }

        private XAnimationPlayCommand BuildPlayCommand(string stateKey, string clipKey, string channelName)
        {
            bool isClipPlayback = !string.IsNullOrWhiteSpace(clipKey);
            bool shouldApplyTarget = isClipPlayback || m_ApplyTargetOverrides;
            XAnimationPlayCommand command = new()
            {
                target = new XAnimationPlayTarget
                {
                    stateKey = stateKey,
                    clipKey = clipKey,
                    channelName = shouldApplyTarget ? channelName : null,
                },
                transition = new XAnimationTransitionOptions
                {
                    interruptible = true,
                },
            };

            // TODO: Apply speed via SetChannelTimeScale
            if (m_ApplyTransitionOverrides)
            {
                command.transition.fadeIn = Mathf.Max(0f, m_PlayFadeInOverride);
                command.transition.fadeOut = Mathf.Max(0f, m_PlayFadeOutOverride);
                command.transition.priority = m_PlayPriorityOverride;
                command.transition.interruptible = m_PlayInterruptibleOverride;
                command.transition.enterTime = Mathf.Clamp01(m_PlayEnterTimeOverride);
            }

            return command;
        }

        private string GetStateTargetChannelName(XAnimationStateConfig state)
        {
            if (!m_ApplyTargetOverrides || string.IsNullOrWhiteSpace(m_PlayTargetChannelName))
            {
                return null;
            }

            return m_PlayTargetChannelName;
        }

        private string FindPlayingChannelForState(XAnimationActor actor, string stateKey)
        {
            if (actor == null || string.IsNullOrWhiteSpace(stateKey))
            {
                return null;
            }

            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (asset?.channels == null)
            {
                return null;
            }

            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channel = asset.channels[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                {
                    continue;
                }

                XAnimationChannelState state = actor.GetChannelState(channel.name);
                if (state != null && string.Equals(state.stateKey, stateKey, StringComparison.Ordinal))
                {
                    return channel.name;
                }
            }

            return null;
        }

        private PropertyField AddProperty(VisualElement root, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return null;
            }

            PropertyField field = new(property);
            root.Add(field);
            return field;
        }

        private void RebuildStateKeyPopup(VisualElement container, string propertyPath, string label)
        {
            container.Clear();
            serializedObject.Update();

            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }

            List<string> stateKeys = new();
            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (asset?.states != null)
            {
                for (int i = 0; i < asset.states.Length; i++)
                {
                    XAnimationStateConfig state = asset.states[i];
                    if (state != null && !string.IsNullOrWhiteSpace(state.key))
                    {
                        stateKeys.Add(state.key);
                    }
                }
            }

            if (stateKeys.Count == 0)
            {
                container.Add(new PropertyField(property, label));
                return;
            }

            if (!stateKeys.Contains(property.stringValue))
            {
                stateKeys.Insert(0, property.stringValue);
            }

            PopupField<string> popup = new(label, stateKeys, Mathf.Max(0, stateKeys.IndexOf(property.stringValue)));
            popup.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty targetProperty = serializedObject.FindProperty(propertyPath);
                if (targetProperty == null)
                {
                    return;
                }

                targetProperty.stringValue = evt.newValue ?? string.Empty;
                serializedObject.ApplyModifiedProperties();
            });
            container.Add(popup);
        }

        private List<ChannelNameOption> GetChannelOptions()
        {
            if (m_CachedChannelOptions != null)
            {
                return m_CachedChannelOptions;
            }

            List<ChannelNameOption> options = new();
            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (asset?.channels == null)
            {
                m_CachedChannelOptions = options;
                return m_CachedChannelOptions;
            }

            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channel = asset.channels[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                {
                    continue;
                }

                options.Add(new ChannelNameOption
                {
                    Name = channel.name,
                    DisplayName = $"{channel.name}    [{channel.layerType}]",
                    ChannelOrder = i,
                });
            }

            m_CachedChannelOptions = options;
            return m_CachedChannelOptions;
        }

        private XAnimationAsset LoadCurrentAnimationAsset()
        {
            TextAsset textAsset = GetSelectedAnimationTextAsset();
            int instanceId = textAsset != null ? textAsset.GetInstanceID() : 0;
            if (m_CachedAnimationAsset != null && m_CachedSelectedAssetInstanceId == instanceId)
            {
                return m_CachedAnimationAsset;
            }

            m_CachedSelectedAssetInstanceId = instanceId;
            m_CachedAnimationAsset = null;
            m_CachedClipObjectMap.Clear();
            m_CachedChannelOptions = null;
            if (textAsset == null)
            {
                return null;
            }

            XAnimationOverrideAsset overrideAsset = textAsset.ToXTextAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                TextAsset baseTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(overrideAsset.baseAssetPath);
                m_CachedAnimationAsset = baseTextAsset == null ? null : baseTextAsset.ToXTextAsset<XAnimationAsset>();
                return m_CachedAnimationAsset;
            }

            m_CachedAnimationAsset = textAsset.ToXTextAsset<XAnimationAsset>();
            return m_CachedAnimationAsset;
        }

        private TextAsset GetSelectedAnimationTextAsset()
        {
            SerializedProperty assetProperty = serializedObject.FindProperty("m_AnimationAsset");
            return assetProperty?.objectReferenceValue as TextAsset;
        }

        private AnimationClip GetClipObject(XAnimationClipConfig clip)
        {
            if (clip == null || string.IsNullOrWhiteSpace(clip.key))
            {
                return null;
            }

            if (m_CachedClipObjectMap.TryGetValue(clip.key, out AnimationClip existingClip))
            {
                return existingClip;
            }

            AnimationClip clipObject = string.IsNullOrWhiteSpace(clip.clipPath)
                ? null
                : XAnimationEditorAssetResolver.ResolveAnimationClip(clip.clipPath);
            m_CachedClipObjectMap[clip.key] = clipObject;
            return clipObject;
        }

        private void InvalidateAnimationAssetCache()
        {
            m_CachedSelectedAssetInstanceId = int.MinValue;
            m_CachedAnimationAsset = null;
            m_CachedChannelOptions = null;
            m_CachedClipObjectMap.Clear();
        }

        private static string FindChannelDisplayName(List<ChannelNameOption> options, string channelName)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].Name, channelName, StringComparison.Ordinal))
                {
                    return options[i].DisplayName;
                }
            }

            return null;
        }

        private static string NormalizeChannelOptionValue(string displayValue)
        {
            if (string.IsNullOrWhiteSpace(displayValue))
            {
                return null;
            }

            int markerIndex = displayValue.IndexOf("    [", StringComparison.Ordinal);
            return markerIndex >= 0 ? displayValue[..markerIndex] : displayValue;
        }

        private static string FindFirstChannelName(List<ChannelNameOption> options)
        {
            return options != null && options.Count > 0 ? options[0].DisplayName : null;
        }

        private static float ConvertParameterDefaultToFloat(object value)
        {
            return value switch
            {
                float f => f,
                double d => (float)d,
                long l => l,
                int i => i,
                _ => 0f,
            };
        }

        private static bool ConvertParameterDefaultToBool(object value)
        {
            return value switch
            {
                bool b => b,
                _ => false,
            };
        }

        private static int ConvertParameterDefaultToInt(object value)
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                float f => Mathf.RoundToInt(f),
                double d => (int)Math.Round(d),
                _ => 0,
            };
        }

        private static string BuildStateInfoText(XAnimationStateConfig state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            return state.stateType switch
            {
                XAnimationStateType.Blend1D => BuildBlend1DStateInfoText(state),
                _ => $"Single | {state.clipKey}",
            };
        }

        private static string BuildBlend1DStateInfoText(XAnimationStateConfig state)
        {
            string parameterText = string.IsNullOrWhiteSpace(state.parameterName) ? "<No Parameter>" : state.parameterName;
            if (state.samples == null || state.samples.Length == 0)
            {
                return $"Blend1D | {parameterText} | No samples";
            }

            List<string> sampleTexts = new(state.samples.Length);
            for (int i = 0; i < state.samples.Length; i++)
            {
                XAnimationBlend1DSampleConfig sample = state.samples[i];
                if (sample == null)
                {
                    continue;
                }

                string clipKey = string.IsNullOrWhiteSpace(sample.clipKey) ? "<Missing Clip>" : sample.clipKey;
                sampleTexts.Add($"{sample.threshold:0.###}:{clipKey}");
            }

            return $"Blend1D | {parameterText} | {string.Join(" / ", sampleTexts)}";
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            m_StatusLabel.text = message;
            m_StatusLabel.style.color = isError ? DangerColor : TextMuted;
        }

        private static VisualElement CreateRowContainer(int rowIndex)
        {
            VisualElement container = new();
            container.style.marginBottom = 2;
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.paddingTop = 3;
            container.style.paddingBottom = 3;
            container.style.borderTopLeftRadius = 2;
            container.style.borderTopRightRadius = 2;
            container.style.borderBottomLeftRadius = 2;
            container.style.borderBottomRightRadius = 2;
            container.style.backgroundColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            return container;
        }

        private static void AddEmptyLabel(VisualElement root, string text)
        {
            if (root == null)
            {
                return;
            }

            Label label = new(text);
            label.style.color = TextMuted;
            label.style.fontSize = BodyFontSize;
            label.style.marginLeft = 4;
            root.Add(label);
        }

        private static FoldoutCard CreateFoldoutCard(string titleText, bool expanded, Action<bool> setExpanded)
        {
            VisualElement card = CreateCard(titleText);
            VisualElement titleRow = card[0];
            Label label = titleRow.Q<Label>();
            VisualElement content = new();
            content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(content);

            void ApplyExpanded(bool value)
            {
                expanded = value;
                setExpanded?.Invoke(value);
                content.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                label.text = value ? $"▾ {titleText}" : $"▸ {titleText}";
            }

            ApplyExpanded(expanded);
            titleRow.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                ApplyExpanded(!expanded);
                evt.StopPropagation();
            });

            return new FoldoutCard
            {
                Root = card,
                Content = content,
            };
        }

        private static FoldoutCard CreateSectionFoldoutCard(
            string titleText,
            bool expanded,
            Action<bool> setExpanded,
            VisualElement titleAction = null,
            Func<bool> canToggle = null)
        {
            VisualElement root = CreateSubBox();
            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            Label label = new();
            label.style.color = TextNormal;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexGrow = 1;
            header.Add(label);
            if (titleAction != null)
            {
                titleAction.style.flexShrink = 0;
                titleAction.style.marginLeft = 6;
                header.Add(titleAction);
            }

            VisualElement content = new();
            content.style.marginTop = 4;
            root.Add(header);
            root.Add(content);

            void RefreshState()
            {
                bool toggleable = canToggle?.Invoke() ?? true;
                bool isExpanded = toggleable && expanded;
                label.text = isExpanded ? $"▾ {titleText}" : $"▸ {titleText}";
                label.style.color = toggleable ? TextNormal : TextMuted;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void ApplyExpanded(bool value)
            {
                expanded = value;
                setExpanded?.Invoke(value);
                RefreshState();
            }

            RefreshState();
            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                if (titleAction != null &&
                    evt.target is VisualElement target &&
                    (ReferenceEquals(target, titleAction) || titleAction.Contains(target)))
                {
                    return;
                }

                if (!(canToggle?.Invoke() ?? true))
                {
                    return;
                }

                ApplyExpanded(!expanded);
                evt.StopPropagation();
            });

            return new FoldoutCard
            {
                Root = root,
                Content = content,
                SetExpanded = ApplyExpanded,
                RefreshState = RefreshState,
            };
        }

        private static VisualElement CreateCard(string titleText)
        {
            VisualElement card = new();
            card.style.marginBottom = 2;
            card.style.paddingLeft = 3;
            card.style.paddingRight = 3;
            card.style.paddingTop = 3;
            card.style.paddingBottom = 3;
            card.style.backgroundColor = new Color(0.15f, 0.15f, 0.16f, 1f);
            card.style.borderTopLeftRadius = 3;
            card.style.borderTopRightRadius = 3;
            card.style.borderBottomLeftRadius = 3;
            card.style.borderBottomRightRadius = 3;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = SectionDivider;
            card.style.borderBottomColor = SectionDivider;
            card.style.borderLeftColor = SectionDivider;
            card.style.borderRightColor = SectionDivider;

            VisualElement titleRow = new();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 2;
            titleRow.style.paddingBottom = 2;
            titleRow.style.borderBottomWidth = 1;
            titleRow.style.borderBottomColor = SectionDivider;

            VisualElement accent = new();
            accent.style.width = 2;
            accent.style.height = 11;
            accent.style.backgroundColor = AccentColor;
            accent.style.marginRight = 4;
            titleRow.Add(accent);

            Label label = new(titleText);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = SectionTitleFontSize;
            label.style.color = TextNormal;
            label.style.flexGrow = 1;
            titleRow.Add(label);
            card.Add(titleRow);
            return card;
        }

        private static VisualElement CreateSubBox()
        {
            VisualElement box = new();
            box.style.marginTop = 3;
            box.style.paddingLeft = 4;
            box.style.paddingRight = 4;
            box.style.paddingTop = 4;
            box.style.paddingBottom = 4;
            box.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f, 1f);
            box.style.borderTopWidth = 1;
            box.style.borderBottomWidth = 1;
            box.style.borderLeftWidth = 1;
            box.style.borderRightWidth = 1;
            box.style.borderTopColor = SectionDivider;
            box.style.borderBottomColor = SectionDivider;
            box.style.borderLeftColor = SectionDivider;
            box.style.borderRightColor = SectionDivider;
            box.style.borderTopLeftRadius = 3;
            box.style.borderTopRightRadius = 3;
            box.style.borderBottomLeftRadius = 3;
            box.style.borderBottomRightRadius = 3;
            return box;
        }

        private static Toggle CreateHeaderApplyToggle(bool value, string tooltip)
        {
            Toggle toggle = new("Apply") { value = value };
            toggle.tooltip = tooltip;
            toggle.style.flexShrink = 0;
            toggle.style.unityFontStyleAndWeight = FontStyle.Normal;
            return toggle;
        }

        private static void ConfigureCompactPlaybackField(BaseField<float> field, float valueWidth)
        {
            field.label = string.Empty;
            field.style.width = valueWidth;
            field.style.minWidth = valueWidth;
            field.style.maxWidth = valueWidth;
            field.style.flexShrink = 0;
            field.style.alignSelf = Align.Center;
        }

        private static void ConfigureCompactPlaybackElement(VisualElement field, float valueWidth)
        {
            field.style.width = valueWidth;
            field.style.minWidth = valueWidth;
            field.style.maxWidth = valueWidth;
            field.style.flexShrink = 0;
            field.style.alignSelf = Align.Center;
        }

        private static VisualElement CreatePlaybackFieldContainer(string labelText, VisualElement field, float labelWidth)
        {
            VisualElement container = new();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            container.style.minWidth = 0;

            Label label = new(labelText);
            label.style.width = labelWidth;
            label.style.minWidth = labelWidth;
            label.style.maxWidth = labelWidth;
            label.style.flexShrink = 0;
            label.style.fontSize = 10;
            label.style.color = TextMuted;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginRight = 6;
            container.Add(label);
            container.Add(field);
            return container;
        }

        private static VisualElement CreatePlaybackToggleRow(string labelText, Toggle toggle, float labelWidth)
        {
            toggle.label = string.Empty;
            toggle.style.flexShrink = 0;
            toggle.style.marginLeft = 0;
            return CreatePlaybackFieldContainer(labelText, toggle, labelWidth);
        }

        private static void ApplyIconButtonStyle(Button button, bool isPlaying)
        {
            button.text = isPlaying ? "■" : "▶";
            button.style.width = 28;
            button.style.minWidth = 28;
            button.style.height = 22;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.color = Color.white;
            button.style.backgroundColor = isPlaying ? DangerColor : AccentColor;
        }
    }
}
#endif
