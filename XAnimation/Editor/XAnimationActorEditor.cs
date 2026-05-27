#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;
using static XFramework.Editor.XAnimationEditorParameterUtility;
using static XFramework.Editor.XAnimationEditorUi;

namespace XFramework.Editor
{
    [CustomEditor(typeof(XAnimationActor))]
    public sealed class XAnimationActorEditor : UnityEditor.Editor
    {
        private const float PlaybackLabelWidth = 118f;
        private const long RuntimeRefreshIntervalMs = 33;
        private const float PlaybackSpeedMin = 0.1f;

        private sealed class StateGroupBucket
        {
            public StateGroupBucket(string channelName, string groupName)
            {
                ChannelName = channelName ?? string.Empty;
                GroupName = groupName ?? string.Empty;
                States = new List<XAnimationStateConfig>();
            }

            public string ChannelName { get; }
            public string GroupName { get; }
            public List<XAnimationStateConfig> States { get; }
            public bool IsUngrouped => string.IsNullOrWhiteSpace(GroupName);
        }

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

        private readonly Dictionary<string, VisualElement> m_StateRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_StateButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RowVisualState> m_StateVisualStateMap = new(StringComparer.Ordinal);
        private readonly HashSet<string> m_CollapsedStateGroupKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VisualElement> m_ClipRowMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> m_ClipButtonMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ClipRowVisualState> m_ClipVisualStateMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> m_RuntimeFloatPreviewValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> m_RuntimeIntPreviewValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> m_RuntimeBoolPreviewValues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FloatField> m_RuntimeFloatFields = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IntegerField> m_RuntimeIntFields = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Toggle> m_RuntimeBoolFields = new(StringComparer.Ordinal);

        private FloatField m_PlaySpeedField;
        private DropdownField m_PlayTargetChannelField;
        private FloatField m_PlayFadeInField;
        private FloatField m_PlayFadeOutField;
        private IntegerField m_PlayPriorityField;
        private Toggle m_ApplyTransitionToggle;
        private Toggle m_PlayInterruptibleToggle;
        private FloatField m_PlayEnterTimeField;
        private Button m_PlayCommandButton;
        private VisualElement m_ParametersListView;
        private VisualElement m_StatesListView;
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
        private float m_PlayFadeInOverride;
        private float m_PlayFadeOutOverride;
        private int m_PlayPriorityOverride;
        private bool m_PlayInterruptibleOverride = true;
        private bool m_ApplyTransitionOverrides;
        private float m_PlayEnterTimeOverride;
        private float m_PlaySpeed = 1f;
        private bool m_PlaybackPrefsLoaded;

        [SerializeField] private bool m_PlaybackSectionExpanded = true;
        [SerializeField] private bool m_PlayTransitionSectionExpanded;
        [SerializeField] private bool m_ParametersSectionExpanded = true;
        [SerializeField] private bool m_StatesSectionExpanded = true;

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
            AddProperty(root, "m_UpdateMode");
            AddProperty(root, "m_UnityAnimationEventsEnabled");
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
            m_PlayTransitionSectionExpanded = settings.TransitionSectionExpanded;
            m_PlayTargetChannelName = settings.ChannelName;
            m_PlaySpeed = ClampPlaybackSpeed(settings.Speed);
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
            return ClampPlaybackSpeed(m_PlaySpeed);
        }

        private static float ClampPlaybackSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                return 1f;
            }

            return Mathf.Max(PlaybackSpeedMin, speed);
        }

        private void SavePlaybackPrefs()
        {
            if (!m_PlaybackPrefsLoaded)
            {
                return;
            }

            float speed = m_PlaySpeedField?.value ?? m_PlaySpeed;
            m_PlaySpeed = ClampPlaybackSpeed(speed);

            XAnimationPlaybackSettingsPrefs.Save(new XAnimationPlaybackSettings
            {
                PlaybackSectionExpanded = m_PlaybackSectionExpanded,
                TransitionSectionExpanded = m_PlayTransitionSectionExpanded,
                ChannelName = m_PlayTargetChannelName,
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
            m_PlaySpeedField.RegisterValueChangedCallback(evt =>
            {
                m_PlaySpeed = ClampPlaybackSpeed(evt.newValue);
                if (!Mathf.Approximately(m_PlaySpeed, evt.newValue))
                {
                    m_PlaySpeedField.SetValueWithoutNotify(m_PlaySpeed);
                }

                SavePlaybackPrefs();
                ApplyPlaybackSpeedToPlayingChannels();
            });
            VisualElement speedFieldRow = CreatePlaybackFieldContainer("speed", m_PlaySpeedField, PlaybackLabelWidth);
            speedRow.Add(speedFieldRow);

            m_PlayTargetChannelField = new DropdownField();
            m_PlayTargetChannelField.tooltip = "clip 调试播放使用的 channelName。state 播放始终使用 state 自己配置的 channel。";
            m_PlayTargetChannelField.style.flexGrow = 1;
            m_PlayTargetChannelField.style.minWidth = 0;
            m_PlayTargetChannelField.RegisterValueChangedCallback(evt =>
            {
                m_PlayTargetChannelName = NormalizeChannelOptionValue(evt.newValue) ?? string.Empty;
                SavePlaybackPrefs();
            });
            VisualElement channelFieldRow = CreatePlaybackFieldContainer("channelName", m_PlayTargetChannelField, PlaybackLabelWidth);
            channelFieldRow.tooltip = "用于 clip 调试播放的目标 channel。";
            channelFieldRow.style.flexGrow = 1;
            speedRow.Add(channelFieldRow);

            m_PlayCommandButton = new Button(PlayConfiguredCommand)
            {
                text = "Play"
            };
            m_PlayCommandButton.style.marginLeft = 8;
            speedRow.Add(m_PlayCommandButton);

            m_ApplyTransitionToggle = CreateHeaderApplyToggle(m_ApplyTransitionOverrides, "是否应用 Transition 覆盖。关闭时本分区会自动收起。");
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

            m_RefreshItem = m_Root.schedule.Execute(RefreshRuntimeLoop).Every(RuntimeRefreshIntervalMs);
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
                m_RuntimeViewsDirty = false;
            }

            RefreshRuntimeParameterValues();
            RefreshStatePlayingStates();
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
                m_PlayCommandButton.SetEnabled(target is XAnimationActor);
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
            m_RuntimeFloatFields.Clear();
            m_RuntimeIntFields.Clear();
            m_RuntimeBoolFields.Clear();
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

            RemoveStaleValues(m_RuntimeFloatPreviewValues, validFloatKeys);
            RemoveStaleValues(m_RuntimeIntPreviewValues, validIntKeys);
            RemoveStaleValues(m_RuntimeBoolPreviewValues, validBoolKeys);
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
                    m_RuntimeFloatFields[parameter.name] = field;
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
                    m_RuntimeBoolFields[parameter.name] = toggle;
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
                    m_RuntimeIntFields[parameter.name] = field;
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

        private void RefreshRuntimeParameterValues()
        {
            XAnimationActor actor = target as XAnimationActor;
            if (actor == null || !Application.isPlaying)
            {
                return;
            }

            foreach (KeyValuePair<string, FloatField> kvp in m_RuntimeFloatFields)
            {
                if (kvp.Value == null || !actor.TryGetParameter(kvp.Key, out float value))
                {
                    continue;
                }

                m_RuntimeFloatPreviewValues[kvp.Key] = value;
                if (!Mathf.Approximately(kvp.Value.value, value))
                {
                    kvp.Value.SetValueWithoutNotify(value);
                }
            }

            foreach (KeyValuePair<string, IntegerField> kvp in m_RuntimeIntFields)
            {
                if (kvp.Value == null || !actor.TryGetParameter(kvp.Key, out int value))
                {
                    continue;
                }

                m_RuntimeIntPreviewValues[kvp.Key] = value;
                if (kvp.Value.value != value)
                {
                    kvp.Value.SetValueWithoutNotify(value);
                }
            }

            foreach (KeyValuePair<string, Toggle> kvp in m_RuntimeBoolFields)
            {
                if (kvp.Value == null || !actor.TryGetParameter(kvp.Key, out bool value))
                {
                    continue;
                }

                m_RuntimeBoolPreviewValues[kvp.Key] = value;
                if (kvp.Value.value != value)
                {
                    kvp.Value.SetValueWithoutNotify(value);
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

        private void RebuildStateList()
        {
            m_StatesListView?.Clear();
            m_StateRowMap.Clear();
            m_StateButtonMap.Clear();
            m_StateVisualStateMap.Clear();

            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (m_StatesListView == null || asset?.states == null || asset.states.Length == 0 || asset.channels == null)
            {
                AddEmptyLabel(m_StatesListView, "No states");
                return;
            }

            Dictionary<string, List<StateGroupBucket>> statesByChannel = new(StringComparer.Ordinal);
            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channel = asset.channels[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                {
                    continue;
                }

                statesByChannel[channel.name] = new List<StateGroupBucket>();
            }

            for (int i = 0; i < asset.states.Length; i++)
            {
                XAnimationStateConfig state = asset.states[i];
                if (state == null || string.IsNullOrWhiteSpace(state.key))
                {
                    continue;
                }

                string channelName = state.channelName ?? string.Empty;
                if (!statesByChannel.TryGetValue(channelName, out List<StateGroupBucket> channelStates))
                {
                    channelStates = new List<StateGroupBucket>();
                    statesByChannel[channelName] = channelStates;
                }

                string groupName = NormalizeStateEditorGroupName(state.editorGroupName);
                StateGroupBucket bucket = FindStateGroupBucket(channelStates, groupName);
                if (bucket == null)
                {
                    bucket = new StateGroupBucket(channelName, groupName);
                    channelStates.Add(bucket);
                }

                bucket.States.Add(state);
            }

            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channel = asset.channels[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                {
                    continue;
                }

                statesByChannel.TryGetValue(channel.name, out List<StateGroupBucket> channelStates);
                m_StatesListView.Add(CreateStateChannelGroup(channel, channelStates ?? new List<StateGroupBucket>()));
            }
        }

        private VisualElement CreateStateChannelGroup(XAnimationChannelConfig channel, List<StateGroupBucket> channelStates)
        {
            VisualElement group = CreateListGroup();
            VisualElement header = CreateListHeader();
            group.Add(header);

            Label title = CreateBoldLabel(channel.name);
            title.style.flexGrow = 1;
            header.Add(title);

            int stateCount = CountStatesInBuckets(channelStates);
            int groupedCount = CountGroupedBuckets(channelStates);
            Label info = CreateSmallInfoLabel(groupedCount > 0
                ? $"{channel.layerType} | {stateCount} states | {groupedCount} groups"
                : $"{channel.layerType} | {stateCount} states");
            header.Add(info);

            int rowIndex = 0;
            for (int i = 0; i < channelStates.Count; i++)
            {
                StateGroupBucket bucket = channelStates[i];
                if (bucket == null)
                {
                    continue;
                }

                if (bucket.IsUngrouped)
                {
                    for (int stateIndex = 0; stateIndex < bucket.States.Count; stateIndex++)
                    {
                        group.Add(CreateStateRow(bucket.States[stateIndex], rowIndex++));
                    }

                    continue;
                }

                group.Add(CreateStateEditorGroup(channel.name, bucket, ref rowIndex));
            }

            if (stateCount == 0)
            {
                AddEmptyLabel(group, "No states");
            }

            return group;
        }

        private VisualElement CreateStateEditorGroup(string channelName, StateGroupBucket bucket, ref int rowIndex)
        {
            VisualElement group = CreateNestedListGroup();
            string groupKey = BuildStateGroupKey(channelName, bucket.GroupName);

            VisualElement header = CreateListHeader();
            Label foldoutLabel = CreateFoldoutGlyph(!IsStateGroupCollapsed(groupKey));
            header.Add(foldoutLabel);

            Label title = CreateBoldLabel(bucket.GroupName);
            title.style.flexGrow = 1;
            title.style.minWidth = 0;
            header.Add(title);

            Label info = CreateSmallInfoLabel($"{bucket.States.Count} states");
            header.Add(info);
            group.Add(header);

            VisualElement content = new VisualElement();
            content.style.display = IsStateGroupCollapsed(groupKey) ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < bucket.States.Count; i++)
            {
                content.Add(CreateStateRow(bucket.States[i], rowIndex++));
            }

            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                bool expanded = content.style.display != DisplayStyle.None;
                content.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                foldoutLabel.text = expanded ? "▸" : "▾";
                SetStateGroupCollapsed(groupKey, expanded);
                evt.StopPropagation();
            });

            group.Add(content);
            return group;
        }

        private VisualElement CreateStateRow(XAnimationStateConfig state, int rowIndex)
        {
            VisualElement container = CreateRowContainer(rowIndex);
            VisualElement progressFill = CreateRowProgressFill();
            container.Add(progressFill);
            RowVisualState visualState = new()
            {
                BaseColor = RowBaseColor(rowIndex),
                ProgressFill = progressFill,
            };
            m_StateVisualStateMap[state.key] = visualState;
            container.RegisterCallback<MouseEnterEvent>(_ =>
            {
                visualState.Hovered = true;
                ApplyStateRowVisualState(state.key);
            });
            container.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                visualState.Hovered = false;
                ApplyStateRowVisualState(state.key);
            });
            VisualElement row = CreateRowContent();
            container.Add(row);

            Label nameLabel = new(state.key);
            nameLabel.style.width = 140;
            nameLabel.style.flexShrink = 0;
            nameLabel.style.color = TextNormal;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.position = Position.Relative;
            row.Add(nameLabel);

            Label stateTypeLabel = new(GetStateTypeDisplayText(state));
            stateTypeLabel.style.flexGrow = 1;
            stateTypeLabel.style.flexShrink = 1;
            stateTypeLabel.style.minWidth = 0;
            stateTypeLabel.style.marginLeft = 6;
            stateTypeLabel.style.color = TextMuted;
            stateTypeLabel.style.fontSize = BodyFontSize;
            stateTypeLabel.style.position = Position.Relative;
            row.Add(stateTypeLabel);

            Button locateButton = new(() => OpenPreviewAndFocusState(target as XAnimationActor, state.key))
            {
                text = "↗"
            };
            locateButton.tooltip = "在预览窗口中定位到这个 state。";
            ApplyClipIconButtonStyle(locateButton);
            locateButton.style.marginLeft = 6;
            locateButton.style.position = Position.Relative;
            locateButton.SetEnabled(true);
            row.Add(locateButton);

            Button playButton = new(() => ToggleStatePlayback(state))
            {
                text = "▶"
            };
            playButton.tooltip = "播放或停止这个 state。";
            ApplyClipIconButtonStyle(playButton);
            playButton.style.marginLeft = 6;
            playButton.style.position = Position.Relative;
            playButton.SetEnabled(true);
            row.Add(playButton);

            m_StateRowMap[state.key] = container;
            m_StateButtonMap[state.key] = playButton;
            return container;
        }

        private void ToggleStatePlayback(XAnimationStateConfig state)
        {
            XAnimationActor actor = target as XAnimationActor;
            if (actor == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                OpenPreviewAndPlayState(actor, state.key);
                return;
            }

            try
            {
                string channelName = FindPlayingChannelForState(actor, state.key) ?? state.channelName;
                XAnimationChannelState channelState = string.IsNullOrWhiteSpace(channelName) ? null : actor.GetChannelState(channelName);
                bool isPlaying = channelState != null && string.Equals(channelState.stateKey, state.key, StringComparison.Ordinal);
                if (isPlaying)
                {
                    actor.Stop(channelName, 0f);
                    SetStatus($"已停止 state {state.key}。");
                }
                else
                {
                    actor.PlayState(state.key, BuildTransitionOptions());
                    actor.SetChannelTimeScale(state.channelName, GetPlaybackSpeed());
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

        private void ApplyPlaybackSpeedToPlayingChannels()
        {
            XAnimationActor actor = target as XAnimationActor;
            if (actor == null || !Application.isPlaying)
            {
                return;
            }

            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (asset?.channels == null)
            {
                return;
            }

            float speed = GetPlaybackSpeed();
            for (int i = 0; i < asset.channels.Length; i++)
            {
                XAnimationChannelConfig channel = asset.channels[i];
                if (channel == null || string.IsNullOrWhiteSpace(channel.name))
                {
                    continue;
                }

                try
                {
                    if (actor.GetChannelState(channel.name) != null)
                    {
                        actor.SetChannelTimeScale(channel.name, speed);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, actor);
                    SetStatus(ex.Message, true);
                    return;
                }
            }
        }

        private void RefreshStatePlayingStates()
        {
            XAnimationActor actor = target as XAnimationActor;
            HashSet<string> playingStateKeys = null;
            Dictionary<string, float> stateProgressByKey = null;
            if (actor != null && Application.isPlaying)
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
                            stateProgressByKey ??= new Dictionary<string, float>(StringComparer.Ordinal);
                            stateProgressByKey[state.stateKey] = Mathf.Clamp01(state.normalizedTime);
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, VisualElement> kvp in m_StateRowMap)
            {
                bool isPlaying = playingStateKeys != null && playingStateKeys.Contains(kvp.Key);
                if (m_StateVisualStateMap.TryGetValue(kvp.Key, out RowVisualState visualState))
                {
                    visualState.Playing = isPlaying;
                    visualState.Progress = isPlaying && stateProgressByKey != null && stateProgressByKey.TryGetValue(kvp.Key, out float progress)
                        ? progress
                        : 0f;
                    ApplyStateRowVisualState(kvp.Key);
                }
                if (m_StateButtonMap.TryGetValue(kvp.Key, out Button button))
                {
                    ApplyClipIconButtonStyle(button, isPlaying ? AccentColor : null);
                    button.text = isPlaying ? "■" : "▶";
                }
            }
        }

        private void PlayConfiguredCommand()
        {
            XAnimationActor actor = target as XAnimationActor;
            if (actor == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                string startStateKey = serializedObject.FindProperty("m_StartStateKey")?.stringValue;
                if (string.IsNullOrWhiteSpace(startStateKey))
                {
                    SetStatus("请先配置 Start State Key，或直接使用下方 state/clip 行内播放按钮。", true);
                    return;
                }

                OpenPreviewAndPlayState(actor, startStateKey);
                return;
            }

            try
            {
                string startStateKey = serializedObject.FindProperty("m_StartStateKey")?.stringValue;
                if (string.IsNullOrWhiteSpace(startStateKey))
                {
                    throw new XAnimationException("请先配置 Start State Key，或直接使用下方 state/clip 行内播放按钮。");
                }

                actor.PlayState(startStateKey, BuildTransitionOptions());
                XAnimationStateConfig state = FindStateConfig(startStateKey);
                if (state != null && !string.IsNullOrWhiteSpace(state.channelName))
                {
                    actor.SetChannelTimeScale(state.channelName, GetPlaybackSpeed());
                }

                SetStatus($"已播放 Start State {startStateKey}。");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, actor);
                SetStatus(ex.Message, true);
            }
        }

        private XAnimationTransitionOptions BuildTransitionOptions()
        {
            XAnimationTransitionOptions transition = new()
            {
                interruptible = true,
            };

            if (m_ApplyTransitionOverrides)
            {
                transition.fadeIn = Mathf.Max(0f, m_PlayFadeInOverride);
                transition.fadeOut = Mathf.Max(0f, m_PlayFadeOutOverride);
                transition.priority = m_PlayPriorityOverride;
                transition.interruptible = m_PlayInterruptibleOverride;
                transition.enterTime = Mathf.Clamp01(m_PlayEnterTimeOverride);
            }

            return transition;
        }

        private XAnimationStateConfig FindStateConfig(string stateKey)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                return null;
            }

            XAnimationAsset asset = LoadCurrentAnimationAsset();
            XAnimationStateConfig[] states = asset?.states ?? Array.Empty<XAnimationStateConfig>();
            for (int i = 0; i < states.Length; i++)
            {
                XAnimationStateConfig state = states[i];
                if (state != null && string.Equals(state.key, stateKey, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private void OpenPreviewAndPlayState(XAnimationActor actor, string stateKey)
        {
            if (!TryGetPreviewSelection(actor, out TextAsset animationAsset, out GameObject prefab))
            {
                return;
            }

            try
            {
                XAnimationPreviewWindow.ShowWindowAndPlayState(
                    animationAsset,
                    prefab,
                    stateKey,
                    GetPlaybackSpeed(),
                    BuildTransitionOptions());
                SetStatus($"已在预览窗口打开并播放 state {stateKey}。");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, actor);
                SetStatus(ex.Message, true);
            }
        }

        private void OpenPreviewAndFocusState(XAnimationActor actor, string stateKey)
        {
            if (!TryGetPreviewSelection(actor, out TextAsset animationAsset, out GameObject prefab))
            {
                return;
            }

            try
            {
                XAnimationPreviewWindow.ShowWindowAndFocusState(
                    animationAsset,
                    prefab,
                    stateKey);
                SetStatus($"已在预览窗口定位 state {stateKey}。");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, actor);
                SetStatus(ex.Message, true);
            }
        }

        private bool TryGetPreviewSelection(XAnimationActor actor, out TextAsset animationAsset, out GameObject prefab)
        {
            animationAsset = actor?.AnimationAsset;
            prefab = null;

            if (animationAsset == null)
            {
                SetStatus("当前 XAnimationActor 没有绑定 animation asset。", true);
                return false;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(actor.gameObject))
            {
                prefab = actor.gameObject;
            }
            else
            {
                prefab = PrefabUtility.GetCorrespondingObjectFromSource(actor.gameObject);
                if (prefab == null)
                {
                    prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(actor.gameObject);
                }
            }

            if (prefab == null)
            {
                SetStatus("非运行时播放需要当前对象关联到一个 prefab asset，才能在预览窗口中打开。", true);
                return false;
            }

            return true;
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

            XAnimationOverrideAsset overrideAsset = textAsset.ToXAnimationAsset<XAnimationOverrideAsset>();
            if (overrideAsset != null && !string.IsNullOrWhiteSpace(overrideAsset.baseAssetPath))
            {
                TextAsset baseTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(overrideAsset.baseAssetPath);
                m_CachedAnimationAsset = baseTextAsset == null ? null : baseTextAsset.ToXAnimationAsset<XAnimationAsset>();
                return m_CachedAnimationAsset;
            }

            m_CachedAnimationAsset = textAsset.ToXAnimationAsset<XAnimationAsset>();
            return m_CachedAnimationAsset;
        }

        private TextAsset GetSelectedAnimationTextAsset()
        {
            SerializedProperty assetProperty = serializedObject.FindProperty("m_AnimationAsset");
            return assetProperty?.objectReferenceValue as TextAsset;
        }

        private void SaveCurrentAnimationAsset()
        {
            XAnimationAsset asset = LoadCurrentAnimationAsset();
            if (asset == null)
            {
                throw new XAnimationException("当前没有选中的 XAnimationAsset。");
            }

            asset.SaveAsset();
        }

        private void ApplyStateType(XAnimationStateConfig state, XAnimationStateType stateType)
        {
            if (state == null)
            {
                throw new XAnimationException("State 配置不能为空。");
            }

            if (state.stateType == stateType)
            {
                return;
            }

            ApplyMigratedStateType(state, stateType);
        }

        private void ApplyMigratedStateType(XAnimationStateConfig state, XAnimationStateType stateType)
        {
            XAnimationStateType sourceType = state.stateType;
            string nextClipKey;
            string nextParameterName;
            string nextParameterXName;
            string nextParameterYName;
            XAnimationBlend1DSampleConfig[] nextSamples;
            XAnimationBlend2DSimpleDirectionalSampleConfig[] nextDirectionalSamples;

            if (stateType == XAnimationStateType.Single)
            {
                nextClipKey = ResolvePreferredSingleClipKey(state);
                nextParameterName = string.Empty;
                nextParameterXName = string.Empty;
                nextParameterYName = string.Empty;
                nextSamples = Array.Empty<XAnimationBlend1DSampleConfig>();
                nextDirectionalSamples = Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            }
            else if (stateType == XAnimationStateType.Blend1D)
            {
                nextClipKey = string.Empty;
                nextParameterName = sourceType switch
                {
                    XAnimationStateType.Blend1D when !string.IsNullOrWhiteSpace(state.parameterName) => state.parameterName,
                    XAnimationStateType.Blend2DSimpleDirectional or XAnimationStateType.Blend2DFreeformDirectional
                        when !string.IsNullOrWhiteSpace(state.parameterXName) => state.parameterXName,
                    _ => EnsureFloatParameter(),
                };
                nextParameterXName = string.Empty;
                nextParameterYName = string.Empty;
                nextSamples = BuildMigratedBlendSamples(state);
                nextDirectionalSamples = Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            }
            else if (IsDirectionalBlendStateType(stateType))
            {
                nextClipKey = string.Empty;
                nextParameterName = string.Empty;
                bool sourceDirectional = IsDirectionalBlendStateType(sourceType);
                bool sourceBlend1D = sourceType == XAnimationStateType.Blend1D;
                nextParameterXName = sourceDirectional && !string.IsNullOrWhiteSpace(state.parameterXName)
                    ? state.parameterXName
                    : sourceBlend1D && !string.IsNullOrWhiteSpace(state.parameterName)
                        ? state.parameterName
                        : EnsureFloatParameter("blendX");
                nextParameterYName = sourceDirectional && !string.IsNullOrWhiteSpace(state.parameterYName)
                    ? state.parameterYName
                    : sourceBlend1D && !string.IsNullOrWhiteSpace(state.parameterName)
                        ? state.parameterName
                        : EnsureFloatParameter("blendY");
                nextSamples = Array.Empty<XAnimationBlend1DSampleConfig>();
                nextDirectionalSamples = BuildMigratedDirectionalSamples(state);
            }
            else
            {
                throw new XAnimationException($"XAnimation stateType '{stateType}' is not supported.");
            }

            state.stateType = stateType;
            state.clipKey = nextClipKey;
            state.parameterName = nextParameterName;
            state.parameterXName = nextParameterXName;
            state.parameterYName = nextParameterYName;
            state.samples = nextSamples;
            state.directionalSamples = nextDirectionalSamples;
        }

        private string FindTemplateClipKey()
        {
            XAnimationClipConfig[] clips = LoadCurrentAnimationAsset()?.clips ?? Array.Empty<XAnimationClipConfig>();
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip != null && !string.IsNullOrWhiteSpace(clip.key))
                {
                    return clip.key;
                }
            }

            return string.Empty;
        }

        private string EnsureFloatParameter(string prefix = "blend")
        {
            XAnimationAsset asset = LoadCurrentAnimationAsset();
            XAnimationParameterConfig[] parameters = asset?.parameters ?? Array.Empty<XAnimationParameterConfig>();
            for (int i = 0; i < parameters.Length; i++)
            {
                XAnimationParameterConfig parameter = parameters[i];
                if (parameter != null && parameter.type == XAnimationParameterType.Float && !string.IsNullOrWhiteSpace(parameter.name))
                {
                    if (string.IsNullOrWhiteSpace(prefix) || parameter.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return parameter.name;
                    }
                }
            }

            string parameterName = CreateUniqueParameterName(prefix);
            List<XAnimationParameterConfig> orderedParameters = new(parameters)
            {
                new()
                {
                    name = parameterName,
                    type = XAnimationParameterType.Float,
                    defaultValue = 0f,
                }
            };
            if (asset != null)
            {
                asset.parameters = orderedParameters.ToArray();
            }

            return parameterName;
        }

        private string CreateUniqueParameterName(string prefix)
        {
            return CreateUniqueName(prefix, name =>
            {
                XAnimationParameterConfig[] parameters = LoadCurrentAnimationAsset()?.parameters ?? Array.Empty<XAnimationParameterConfig>();
                for (int i = 0; i < parameters.Length; i++)
                {
                    XAnimationParameterConfig parameter = parameters[i];
                    if (parameter != null && string.Equals(parameter.name, name, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        private static string CreateUniqueName(string prefix, Func<string, bool> exists)
        {
            string safePrefix = string.IsNullOrWhiteSpace(prefix) ? "New" : prefix.Trim();
            if (!exists(safePrefix))
            {
                return safePrefix;
            }

            for (int i = 1; i < 1000; i++)
            {
                string candidate = $"{safePrefix}{i}";
                if (!exists(candidate))
                {
                    return candidate;
                }
            }

            throw new XAnimationException($"Unable to create unique name with prefix '{safePrefix}'.");
        }

        private XAnimationBlend1DSampleConfig[] CreateDefaultBlendSamples()
        {
            XAnimationClipConfig[] clips = LoadCurrentAnimationAsset()?.clips ?? Array.Empty<XAnimationClipConfig>();
            List<string> clipKeys = new(2);
            for (int i = 0; i < clips.Length && clipKeys.Count < 2; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip != null && !string.IsNullOrWhiteSpace(clip.key) && !clipKeys.Contains(clip.key))
                {
                    clipKeys.Add(clip.key);
                }
            }

            if (clipKeys.Count < 2)
            {
                throw new XAnimationException("Cannot create Blend1D state because at least two clips are required.");
            }

            return new[]
            {
                new XAnimationBlend1DSampleConfig
                {
                    clipKey = clipKeys[0],
                    threshold = 0f,
                },
                new XAnimationBlend1DSampleConfig
                {
                    clipKey = clipKeys[1],
                    threshold = 1f,
                }
            };
        }

        private XAnimationBlend2DSimpleDirectionalSampleConfig[] CreateDefaultDirectionalBlendSamples()
        {
            XAnimationClipConfig[] clips = LoadCurrentAnimationAsset()?.clips ?? Array.Empty<XAnimationClipConfig>();
            if (clips.Length < 2)
            {
                throw new XAnimationException("Cannot create Blend2DSimpleDirectional state because at least two clips are required.");
            }

            string idleClipKey = FindTemplateClipKey();
            string directionalClipKey = idleClipKey;
            for (int i = 0; i < clips.Length; i++)
            {
                XAnimationClipConfig clip = clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.key) || string.Equals(clip.key, idleClipKey, StringComparison.Ordinal))
                {
                    continue;
                }

                directionalClipKey = clip.key;
                break;
            }

            return new[]
            {
                new XAnimationBlend2DSimpleDirectionalSampleConfig
                {
                    clipKey = idleClipKey,
                    positionX = 0f,
                    positionY = 0f,
                },
                new XAnimationBlend2DSimpleDirectionalSampleConfig
                {
                    clipKey = directionalClipKey,
                    positionX = 0f,
                    positionY = 1f,
                }
            };
        }

        private string ResolvePreferredSingleClipKey(XAnimationStateConfig state)
        {
            if (state == null)
            {
                return FindTemplateClipKey();
            }

            if (!string.IsNullOrWhiteSpace(state.clipKey))
            {
                return state.clipKey;
            }

            if (state.stateType == XAnimationStateType.Blend1D)
            {
                return GetFirstBlendSampleClipKey(state) ?? FindTemplateClipKey();
            }

            if (IsDirectionalBlendStateType(state.stateType))
            {
                return GetIdleDirectionalClipKey(state) ??
                       GetFirstDirectionalClipKey(state) ??
                       FindTemplateClipKey();
            }

            return FindTemplateClipKey();
        }

        private XAnimationBlend1DSampleConfig[] BuildMigratedBlendSamples(XAnimationStateConfig state)
        {
            if (state == null)
            {
                return CreateDefaultBlendSamples();
            }

            if (state.stateType == XAnimationStateType.Blend1D && (state.samples?.Length ?? 0) >= 2)
            {
                return CloneBlendSamples(state.samples);
            }

            XAnimationBlend1DSampleConfig[] samples = CreateDefaultBlendSamples();
            List<string> seedClipKeys = GetBlendSeedClipKeys(state);
            for (int i = 0; i < samples.Length && i < seedClipKeys.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(seedClipKeys[i]))
                {
                    samples[i].clipKey = seedClipKeys[i];
                }
            }

            return samples;
        }

        private XAnimationBlend2DSimpleDirectionalSampleConfig[] BuildMigratedDirectionalSamples(XAnimationStateConfig state)
        {
            if (state == null)
            {
                return CreateDefaultDirectionalBlendSamples();
            }

            if (IsDirectionalBlendStateType(state.stateType) && (state.directionalSamples?.Length ?? 0) >= 2)
            {
                return CloneDirectionalSamples(state.directionalSamples);
            }

            XAnimationBlend2DSimpleDirectionalSampleConfig[] samples = CreateDefaultDirectionalBlendSamples();
            List<string> seedClipKeys = GetDirectionalSeedClipKeys(state);
            for (int i = 0; i < samples.Length && i < seedClipKeys.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(seedClipKeys[i]))
                {
                    samples[i].clipKey = seedClipKeys[i];
                }
            }

            return samples;
        }

        private static XAnimationBlend1DSampleConfig[] CloneBlendSamples(XAnimationBlend1DSampleConfig[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<XAnimationBlend1DSampleConfig>();
            }

            XAnimationBlend1DSampleConfig[] cloned = new XAnimationBlend1DSampleConfig[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                XAnimationBlend1DSampleConfig sample = samples[i];
                cloned[i] = sample == null
                    ? null
                    : new XAnimationBlend1DSampleConfig
                    {
                        clipKey = sample.clipKey,
                        threshold = sample.threshold,
                    };
            }

            return cloned;
        }

        private static XAnimationBlend2DSimpleDirectionalSampleConfig[] CloneDirectionalSamples(XAnimationBlend2DSimpleDirectionalSampleConfig[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            }

            XAnimationBlend2DSimpleDirectionalSampleConfig[] cloned = new XAnimationBlend2DSimpleDirectionalSampleConfig[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                XAnimationBlend2DSimpleDirectionalSampleConfig sample = samples[i];
                cloned[i] = sample == null
                    ? null
                    : new XAnimationBlend2DSimpleDirectionalSampleConfig
                    {
                        clipKey = sample.clipKey,
                        positionX = sample.positionX,
                        positionY = sample.positionY,
                    };
            }

            return cloned;
        }

        private List<string> GetBlendSeedClipKeys(XAnimationStateConfig state)
        {
            List<string> seedClipKeys = new(2);
            if (state == null)
            {
                return seedClipKeys;
            }

            if (state.stateType == XAnimationStateType.Single)
            {
                AddOrderedClipKey(seedClipKeys, state.clipKey);
                return seedClipKeys;
            }

            if (IsDirectionalBlendStateType(state.stateType))
            {
                string idleClipKey = GetIdleDirectionalClipKey(state);
                AddOrderedClipKey(seedClipKeys, idleClipKey);
                XAnimationBlend2DSimpleDirectionalSampleConfig[] samples = state.directionalSamples ?? Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
                for (int i = 0; i < samples.Length && seedClipKeys.Count < 2; i++)
                {
                    XAnimationBlend2DSimpleDirectionalSampleConfig sample = samples[i];
                    if (sample == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(idleClipKey) &&
                        Mathf.Approximately(sample.positionX, 0f) &&
                        Mathf.Approximately(sample.positionY, 0f))
                    {
                        continue;
                    }

                    AddOrderedClipKey(seedClipKeys, sample.clipKey);
                }
            }

            return seedClipKeys;
        }

        private List<string> GetDirectionalSeedClipKeys(XAnimationStateConfig state)
        {
            List<string> seedClipKeys = new(2);
            if (state == null)
            {
                return seedClipKeys;
            }

            if (state.stateType == XAnimationStateType.Single)
            {
                AddOrderedClipKey(seedClipKeys, state.clipKey);
                return seedClipKeys;
            }

            if (state.stateType == XAnimationStateType.Blend1D)
            {
                XAnimationBlend1DSampleConfig[] samples = state.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
                if (samples.Length > 0)
                {
                    AddOrderedClipKey(seedClipKeys, samples[0]?.clipKey);
                }

                if (samples.Length > 1)
                {
                    AddOrderedClipKey(seedClipKeys, samples[1]?.clipKey);
                }
            }

            return seedClipKeys;
        }

        private static string GetFirstBlendSampleClipKey(XAnimationStateConfig state)
        {
            XAnimationBlend1DSampleConfig[] samples = state?.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
            for (int i = 0; i < samples.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(samples[i]?.clipKey))
                {
                    return samples[i].clipKey;
                }
            }

            return null;
        }

        private static string GetIdleDirectionalClipKey(XAnimationStateConfig state)
        {
            XAnimationBlend2DSimpleDirectionalSampleConfig[] samples = state?.directionalSamples ?? Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            for (int i = 0; i < samples.Length; i++)
            {
                XAnimationBlend2DSimpleDirectionalSampleConfig sample = samples[i];
                if (sample != null &&
                    Mathf.Approximately(sample.positionX, 0f) &&
                    Mathf.Approximately(sample.positionY, 0f) &&
                    !string.IsNullOrWhiteSpace(sample.clipKey))
                {
                    return sample.clipKey;
                }
            }

            return null;
        }

        private static string GetFirstDirectionalClipKey(XAnimationStateConfig state)
        {
            XAnimationBlend2DSimpleDirectionalSampleConfig[] samples = state?.directionalSamples ?? Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            for (int i = 0; i < samples.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(samples[i]?.clipKey))
                {
                    return samples[i].clipKey;
                }
            }

            return null;
        }

        private static void AddOrderedClipKey(List<string> clipKeys, string clipKey)
        {
            if (!string.IsNullOrWhiteSpace(clipKey))
            {
                clipKeys.Add(clipKey);
            }
        }

        private static bool IsDirectionalBlendStateType(XAnimationStateType stateType)
        {
            return stateType == XAnimationStateType.Blend2DSimpleDirectional ||
                   stateType == XAnimationStateType.Blend2DFreeformDirectional;
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

        private void SetStatus(string message, bool isError = false)
        {
            if (m_StatusLabel == null)
            {
                return;
            }

            m_StatusLabel.text = message;
            m_StatusLabel.style.color = isError ? DangerColor : TextMuted;
        }

        private void ApplyStateRowVisualState(string stateKey)
        {
            if (!m_StateRowMap.TryGetValue(stateKey, out VisualElement row) ||
                !m_StateVisualStateMap.TryGetValue(stateKey, out RowVisualState visualState))
            {
                return;
            }

            ApplyRowVisualState(row, visualState);
        }

        private static string NormalizeStateEditorGroupName(string groupName)
        {
            groupName = groupName?.Trim();
            return string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName;
        }

        private static string BuildStateGroupKey(string channelName, string groupName)
        {
            return $"{channelName ?? string.Empty}::{NormalizeStateEditorGroupName(groupName)}";
        }

        private bool IsStateGroupCollapsed(string groupKey)
        {
            return !string.IsNullOrWhiteSpace(groupKey) && m_CollapsedStateGroupKeys.Contains(groupKey);
        }

        private void SetStateGroupCollapsed(string groupKey, bool collapsed)
        {
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                return;
            }

            if (collapsed)
            {
                m_CollapsedStateGroupKeys.Add(groupKey);
            }
            else
            {
                m_CollapsedStateGroupKeys.Remove(groupKey);
            }
        }

        private static StateGroupBucket FindStateGroupBucket(List<StateGroupBucket> buckets, string groupName)
        {
            if (buckets == null)
            {
                return null;
            }

            groupName = NormalizeStateEditorGroupName(groupName);
            for (int i = 0; i < buckets.Count; i++)
            {
                StateGroupBucket bucket = buckets[i];
                if (bucket != null &&
                    string.Equals(NormalizeStateEditorGroupName(bucket.GroupName), groupName, StringComparison.Ordinal))
                {
                    return bucket;
                }
            }

            return null;
        }

        private static int CountStatesInBuckets(List<StateGroupBucket> buckets)
        {
            int count = 0;
            if (buckets == null)
            {
                return count;
            }

            for (int i = 0; i < buckets.Count; i++)
            {
                count += buckets[i]?.States?.Count ?? 0;
            }

            return count;
        }

        private static int CountGroupedBuckets(List<StateGroupBucket> buckets)
        {
            int count = 0;
            if (buckets == null)
            {
                return count;
            }

            for (int i = 0; i < buckets.Count; i++)
            {
                if (buckets[i] != null && !buckets[i].IsUngrouped)
                {
                    count++;
                }
            }

            return count;
        }

        private static string GetStateTypeDisplayText(XAnimationStateConfig state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            return state.stateType switch
            {
                XAnimationStateType.Blend1D => "Blend1D",
                XAnimationStateType.Blend2DSimpleDirectional => "Blend2DSimpleDirectional",
                XAnimationStateType.Blend2DFreeformDirectional => "Blend2DFreeformDirectional",
                _ => "Single",
            };
        }
    }
}
#endif
