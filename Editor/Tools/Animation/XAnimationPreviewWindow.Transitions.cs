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
        private VisualElement CreateDefaultTransitionEditor(int transitionIndex, XAnimationDefaultTransitionConfig config)
        {
            bool editable = m_Session != null && !m_Session.IsOverrideAsset;
            bool expanded = IsDefaultTransitionExpanded(transitionIndex);
            VisualElement wrapper = new();
            wrapper.style.marginBottom = 4;
            wrapper.style.backgroundColor = ListGroupBg;
            wrapper.style.borderTopWidth = 1;
            wrapper.style.borderBottomWidth = 1;
            wrapper.style.borderLeftWidth = 1;
            wrapper.style.borderRightWidth = 1;
            wrapper.style.borderTopColor = SectionDivider;
            wrapper.style.borderBottomColor = SectionDivider;
            wrapper.style.borderLeftColor = SectionDivider;
            wrapper.style.borderRightColor = SectionDivider;
            wrapper.style.borderTopLeftRadius = 3;
            wrapper.style.borderTopRightRadius = 3;
            wrapper.style.borderBottomLeftRadius = 3;
            wrapper.style.borderBottomRightRadius = 3;

            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 4;
            header.style.paddingRight = 4;
            header.style.paddingTop = 3;
            header.style.paddingBottom = 3;
            header.style.backgroundColor = ListHeaderBg;

            Label foldLabel = new(expanded ? "▾" : "▸");
            foldLabel.style.width = 16;
            foldLabel.style.color = TextMuted;
            header.Add(foldLabel);

            string displayName = string.IsNullOrWhiteSpace(config.editorName)
                ? $"Default Transition {transitionIndex + 1}"
                : config.editorName;
            EditableLabel nameLabel = new(displayName);
            ConfigureEditableNameLabel(nameLabel, 150f);
            nameLabel.SetEditable(editable, EditableLabelEditTrigger.DoubleClick);
            nameLabel.EditStarted += BeginNameEdit;
            nameLabel.EditEnded += EndNameEdit;
            nameLabel.ValueCommitted += (oldName, newName) => RenameDefaultTransition(transitionIndex, oldName, newName, nameLabel);
            nameLabel.tooltip = editable ? "双击重命名这个编辑器分组。" : "Override 资源不能重命名 Default Transition。";
            header.Add(nameLabel);

            Label summaryLabel = new($"{FormatDefaultTransitionPairSummary(config)} | {config.pairs?.Length ?? 0} pairs");
            summaryLabel.style.flexGrow = 1;
            summaryLabel.style.color = TextMuted;
            summaryLabel.style.fontSize = BodyFontSize;
            summaryLabel.style.overflow = Overflow.Hidden;
            summaryLabel.style.textOverflow = TextOverflow.Ellipsis;
            header.Add(summaryLabel);

            Button deleteButton = new(() => DeleteDefaultTransition(transitionIndex));
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.tooltip = editable ? "删除这个 Default Transition 分组。" : "Override 资源不能删除 Default Transition。";
            deleteButton.SetEnabled(editable);
            header.Add(deleteButton);

            VisualElement content = new();
            content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            content.style.paddingLeft = 6;
            content.style.paddingRight = 6;
            content.style.paddingBottom = 6;

            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 ||
                    (evt.target is VisualElement target &&
                     (ReferenceEquals(target, deleteButton) || deleteButton.Contains(target) || ReferenceEquals(target, nameLabel) || nameLabel.Contains(target))))
                {
                    return;
                }

                bool newExpanded = content.style.display == DisplayStyle.None;
                content.style.display = newExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                foldLabel.text = newExpanded ? "▾" : "▸";
                SetDefaultTransitionExpanded(transitionIndex, newExpanded);
                ScheduleDefaultTransitionsEditorRebuild();
                evt.StopPropagation();
            });

            content.Add(CreateDefaultTransitionOptionsEditor(transitionIndex, config, editable));
            content.Add(CreateDefaultTransitionPairsEditor(transitionIndex, config, editable));
            wrapper.Add(header);
            wrapper.Add(content);
            m_DefaultTransitionRowMap[transitionIndex] = wrapper;
            return wrapper;
        }

        private VisualElement CreateDefaultTransitionOptionsEditor(int transitionIndex, XAnimationDefaultTransitionConfig config, bool editable)
        {
            VisualElement container = new();
            container.style.marginTop = 6;
            container.style.paddingBottom = 5;
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = SectionDivider;

            VisualElement row1 = CreateDefaultTransitionOptionRow();
            FloatField fadeInField = CreateDefaultTransitionFloatField("fadeIn", config.fadeIn, editable);
            FloatField fadeOutField = CreateDefaultTransitionFloatField("fadeOut", config.fadeOut, editable);
            FloatField enterTimeField = CreateDefaultTransitionFloatField("enterTime", config.enterTime, editable);
            row1.Add(fadeInField);
            row1.Add(fadeOutField);
            row1.Add(enterTimeField);
            container.Add(row1);

            VisualElement row2 = CreateDefaultTransitionOptionRow();
            IntegerField priorityField = new("priority") { value = config.priority };
            ConfigureDefaultTransitionField(priorityField, editable);
            Toggle interruptibleToggle = new("interruptible") { value = config.interruptible };
            ConfigureDefaultTransitionToggleField(interruptibleToggle, editable);
            row2.Add(priorityField);
            row2.Add(interruptibleToggle);
            container.Add(row2);

            void ApplyOptions()
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                m_Session.SetDefaultTransitionOptions(
                    transitionIndex,
                    fadeInField.value,
                    fadeOutField.value,
                    enterTimeField.value,
                    priorityField.value,
                    interruptibleToggle.value,
                    save: false);
                ScheduleAssetSave();
                RefreshChannelStates();
                SetStatus($"Default Transition {transitionIndex + 1} 参数已更新。");
            }

            fadeInField.RegisterValueChangedCallback(evt =>
            {
                float value = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(value, evt.newValue))
                {
                    fadeInField.SetValueWithoutNotify(value);
                }

                ApplyOptions();
            });
            fadeOutField.RegisterValueChangedCallback(evt =>
            {
                float value = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(value, evt.newValue))
                {
                    fadeOutField.SetValueWithoutNotify(value);
                }

                ApplyOptions();
            });
            enterTimeField.RegisterValueChangedCallback(evt =>
            {
                float value = Mathf.Clamp01(evt.newValue);
                if (!Mathf.Approximately(value, evt.newValue))
                {
                    enterTimeField.SetValueWithoutNotify(value);
                }

                ApplyOptions();
            });
            priorityField.RegisterValueChangedCallback(_ => ApplyOptions());
            interruptibleToggle.RegisterValueChangedCallback(_ => ApplyOptions());
            return container;
        }

        private VisualElement CreateDefaultTransitionPairsEditor(int transitionIndex, XAnimationDefaultTransitionConfig config, bool editable)
        {
            VisualElement container = new();
            container.style.marginTop = 5;

            VisualElement titleRow = new();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            Label title = new("Pairs");
            title.style.flexGrow = 1;
            title.style.color = TextMuted;
            title.style.fontSize = BodyFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(title);

            Button addPairButton = CreateStyledButton("+ Pair", () => AddDefaultTransitionPair(transitionIndex), AccentColor);
            addPairButton.tooltip = editable ? "新增一组 preState / nextState。" : "Override 资源不能新增 pair。";
            addPairButton.SetEnabled(editable);
            titleRow.Add(addPairButton);
            container.Add(titleRow);

            XAnimationTransitionPairConfig[] pairs = config.pairs ?? Array.Empty<XAnimationTransitionPairConfig>();
            for (int pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
            {
                container.Add(CreateDefaultTransitionPairRow(transitionIndex, pairIndex, pairs[pairIndex], editable, pairs.Length));
            }

            return container;
        }

        private VisualElement CreateDefaultTransitionPairRow(
            int transitionIndex,
            int pairIndex,
            XAnimationTransitionPairConfig pair,
            bool editable,
            int pairCount)
        {
            pair ??= new XAnimationTransitionPairConfig();
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;

            VisualElement summaryRow = new();
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.flexGrow = 1;
            summaryRow.style.flexShrink = 1;
            summaryRow.style.flexBasis = 0;
            summaryRow.style.minWidth = 0;
            row.Add(summaryRow);

            XAnimationEditorSelectionField preStateField = CreateStateSelectionField(string.Empty, pair.preStateKey);
            XAnimationEditorSelectionField nextStateField = CreateStateSelectionField(string.Empty, pair.nextStateKey, pair.preStateKey);
            preStateField.style.width = 180f;
            preStateField.style.minWidth = 120f;
            preStateField.style.flexGrow = 1;
            preStateField.style.flexShrink = 1;
            nextStateField.style.width = 180f;
            nextStateField.style.minWidth = 120f;
            nextStateField.style.flexGrow = 1;
            nextStateField.style.flexShrink = 1;
            preStateField.SetEnabled(editable);
            nextStateField.SetEnabled(editable);
            summaryRow.Add(preStateField);

            Label arrow = new("->");
            arrow.style.width = 22;
            arrow.style.color = TextMuted;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            summaryRow.Add(arrow);
            summaryRow.Add(nextStateField);

            bool pairIsWaitingSwitch = false;
            Button playButton = new() { text = "▶" };
            playButton.tooltip = "播放 preState，进入待切换状态。";
            ApplyClipIconButtonStyle(playButton);
            playButton.style.flexShrink = 0;
            playButton.SetEnabled(m_Session != null && m_Session.IsLoaded);
            playButton.clicked += () =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                if (!pairIsWaitingSwitch)
                {
                    if (PlayDefaultTransitionPairPre(preStateField.value, nextStateField.value))
                    {
                        pairIsWaitingSwitch = true;
                        playButton.text = "⏭";
                        playButton.tooltip = "切换到 nextState（使用 Default Transition 参数）。";
                        ApplyClipIconButtonStyle(playButton, AccentColor);
                    }
                }
                else
                {
                    PlayDefaultTransitionPairNext(preStateField.value, nextStateField.value);
                    pairIsWaitingSwitch = false;
                    playButton.text = "▶";
                    playButton.tooltip = "播放 preState，进入待切换状态。";
                    ApplyClipIconButtonStyle(playButton);
                }
            };

            Button deleteButton = new(() => DeleteDefaultTransitionPair(transitionIndex, pairIndex));
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.tooltip = editable && pairCount > 1 ? "删除这组 pair。" : "Default Transition 至少保留一组 pair。";
            deleteButton.SetEnabled(editable && pairCount > 1);
            deleteButton.style.flexShrink = 0;

            VisualElement actionsRow = new();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.alignItems = Align.Center;
            actionsRow.style.justifyContent = Justify.FlexEnd;
            actionsRow.style.flexShrink = 0;
            actionsRow.style.marginLeft = 6;
            row.Add(actionsRow);
            actionsRow.Add(playButton);
            actionsRow.Add(deleteButton);

            preStateField.ValueChanged += (previousValue, newValue) =>
            {
                string nextStateKey = nextStateField.value;
                if (string.Equals(newValue, nextStateKey, StringComparison.Ordinal))
                {
                    nextStateKey = GetFallbackNextState(newValue);
                }

                ChangeDefaultTransitionPair(transitionIndex, pairIndex, newValue, nextStateKey, preStateField, previousValue);
            };
            nextStateField.ValueChanged += (previousValue, newValue) =>
                ChangeDefaultTransitionPair(transitionIndex, pairIndex, preStateField.value, newValue, nextStateField, previousValue);
            return row;
        }

        private VisualElement CreateStateEditor(XAnimationCompiledState state)
        {
            XAnimationStateConfig config = state.Config;
            VisualElement editor = CreateFoldoutRowEditor();
            VisualElement configBox = CreateStateConfigSection();
            editor.Add(configBox);

            List<string> stateTypeNames = new(Enum.GetNames(typeof(XAnimationStateType)));
            DropdownField stateTypeField = new(
                "stateType",
                stateTypeNames,
                Mathf.Max(0, stateTypeNames.IndexOf(config.stateType.ToString())));
            ApplyDropdownFieldStyle(stateTypeField);
            stateTypeField.tooltip = "State 类型。Single 播放一个 clip；Blend1D 根据 float 参数混合采样点；2D directional blend 根据二维方向参数混合采样点。";
            stateTypeField.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(evt.newValue, out XAnimationStateType stateType))
                {
                    return;
                }

                ChangeStateType(state.Key, stateType, evt.previousValue, stateTypeField);
            });
            configBox.Add(stateTypeField);

            DropdownField channelField = CreateChannelDropdown("channel", config.channelName);
            channelField.tooltip = "State 默认播放 channel。";
            AttachDropdownInspectorButton(
                channelField,
                () => channelField?.value ?? config.channelName,
                () => HasChannel(channelField?.value ?? config.channelName),
                () => FocusChannelInInspector(channelField?.value ?? config.channelName),
                "定位到 Channels 面板里当前 channel 对应的条目。");
            channelField.RegisterValueChangedCallback(evt => ChangeStateChannel(state.Key, evt.newValue, channelField, evt.previousValue));
            configBox.Add(channelField);

            DropdownField parameterField = null;
            DropdownField parameterXField = null;
            DropdownField parameterYField = null;
            VisualElement deferredTypeSpecificEditor = null;
            if (config.stateType == XAnimationStateType.Blend1D)
            {
                parameterField = CreateFloatParameterDropdown("parameter", config.parameterName);
                parameterField.tooltip = "Blend1D 绑定的 Float 参数。";
                parameterField.RegisterValueChangedCallback(evt => ChangeStateBlendParameter(state.Key, evt.newValue, parameterField, evt.previousValue));
                deferredTypeSpecificEditor = CreateBlendSampleEditor(state.Key, config, parameterField);
            }
            else if (IsDirectionalBlendStateType(config.stateType))
            {
                parameterXField = CreateFloatParameterDropdown("parameterX", config.parameterXName);
                parameterXField.tooltip = $"{config.stateType} 的 X 方向 Float 参数。";
                parameterYField = CreateFloatParameterDropdown("parameterY", config.parameterYName);
                parameterYField.tooltip = $"{config.stateType} 的 Y 方向 Float 参数。";
                parameterXField.RegisterValueChangedCallback(evt =>
                    ChangeStateDirectionalBlendParameters(
                        state.Key,
                        evt.newValue,
                        parameterYField.value,
                        parameterXField,
                        parameterYField,
                        evt.previousValue,
                        parameterYField.value));
                parameterYField.RegisterValueChangedCallback(evt =>
                    ChangeStateDirectionalBlendParameters(
                        state.Key,
                        parameterXField.value,
                        evt.newValue,
                        parameterXField,
                        parameterYField,
                        parameterXField.value,
                        evt.previousValue));
                deferredTypeSpecificEditor = CreateDirectionalBlendSampleEditor(state.Key, config, parameterXField, parameterYField);
            }

            Toggle loopField = new("loop") { value = config.loop };
            loopField.tooltip = "State 是否循环。";
            loopField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                m_Session.SetStateLoop(state.Key, evt.newValue);
                RebuildStateList();
                RestartStateIfPlaying(state.Key, config.channelName);
                SetStatus($"{state.Key} loop = {evt.newValue}。");
            });

            List<string> rootMotionModeNames = new(Enum.GetNames(typeof(XAnimationClipRootMotionMode)));
            DropdownField rootMotionModeField = new(
                "rootMotionMode",
                rootMotionModeNames,
                Mathf.Max(0, rootMotionModeNames.IndexOf(config.rootMotionMode.ToString())));
            ApplyDropdownFieldStyle(rootMotionModeField);
            rootMotionModeField.tooltip = "State 的 Root Motion 策略：继承 channel、强制开启或强制关闭。";
            rootMotionModeField.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(evt.newValue, out XAnimationClipRootMotionMode mode))
                {
                    return;
                }

                ChangeStateRootMotionMode(state.Key, mode, evt.previousValue, rootMotionModeField);
            });
            configBox.Add(rootMotionModeField);
            configBox.Add(loopField);

            FloatField speedField = new("speed") { value = config.speed };
            speedField.tooltip = "State 默认速度。0 会按 1 处理。";
            speedField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float speed = Mathf.Approximately(evt.newValue, 0f) ? 1f : evt.newValue;
                if (!Mathf.Approximately(speed, evt.newValue))
                {
                    speedField.SetValueWithoutNotify(speed);
                }

                m_Session.SetStateSpeed(state.Key, speed, save: false);
                ScheduleAssetSave();
                SetStatus($"{state.Key} speed = {speed:0.###}。");
            });
            configBox.Add(speedField);

            VisualElement fadeRow = new VisualElement();
            fadeRow.style.flexDirection = FlexDirection.Row;
            fadeRow.style.alignItems = Align.Center;

            FloatField fadeInField = new("fadeIn") { value = config.fadeIn };
            fadeInField.tooltip = "State 默认淡入时间。";
            fadeInField.style.flexGrow = 1;
            fadeInField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float fadeIn = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(fadeIn, evt.newValue))
                {
                    fadeInField.SetValueWithoutNotify(fadeIn);
                }

                m_Session.SetStateFade(state.Key, fadeIn, config.fadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{state.Key} fadeIn = {fadeIn:0.###}。");
            });
            fadeRow.Add(fadeInField);

            FloatField fadeOutField = new("fadeOut") { value = config.fadeOut };
            fadeOutField.tooltip = "State 默认淡出时间。";
            fadeOutField.style.flexGrow = 1;
            fadeOutField.style.marginLeft = 8;
            fadeOutField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                float fadeOut = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(fadeOut, evt.newValue))
                {
                    fadeOutField.SetValueWithoutNotify(fadeOut);
                }

                m_Session.SetStateFade(state.Key, config.fadeIn, fadeOut, save: false);
                ScheduleAssetSave();
                SetStatus($"{state.Key} fadeOut = {fadeOut:0.###}。");
            });
            fadeRow.Add(fadeOutField);
            configBox.Add(fadeRow);

            if (config.stateType == XAnimationStateType.Single)
            {
                XAnimationEditorSelectionField clipField = CreateClipSelectionField("clipKey", config.clipKey);
                clipField.tooltip = "Single state 播放的 clip。";
                clipField.ValueChanged += (previousValue, newValue) => ChangeStateClipKey(state.Key, newValue, clipField, previousValue);
                AttachClipKeyPingButton(clipField, config.clipKey, enabled: true);
                editor.Add(clipField);
            }
            else if (deferredTypeSpecificEditor != null)
            {
                editor.Add(deferredTypeSpecificEditor);
            }

            editor.Add(CreateStateGateEditor(state.Key, "Allowed Next States", config.allowedNextStateKeys, addPreviousGate: false));
            editor.Add(CreateStateGateEditor(state.Key, "Allowed Previous States", config.allowedPreviousStateKeys, addPreviousGate: true));
            return editor;
        }

        private VisualElement CreateStateGateEditor(string stateKey, string title, string[] values, bool addPreviousGate)
        {
            VisualElement box = CreateSubBox();
            box.style.marginTop = 5;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 3;

            Label label = new(title);
            label.style.flexGrow = 1;
            label.style.color = TextNormal;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);

            bool editable = m_Session != null && !m_Session.IsOverrideAsset;
            Button addButton = new(() =>
            {
                if (addPreviousGate)
                {
                    AddStateAllowedPreviousState(stateKey);
                }
                else
                {
                    AddStateAllowedNextState(stateKey);
                }
            })
            {
                text = "+"
            };
            addButton.tooltip = editable ? "新增一条 state 门禁配置。" : "Override 资源不能编辑 state 门禁。";
            addButton.SetEnabled(editable);
            ApplyClipIconButtonStyle(addButton, AccentColor);
            header.Add(addButton);
            box.Add(header);

            string[] gateValues = values ?? Array.Empty<string>();
            for (int i = 0; i < gateValues.Length; i++)
            {
                box.Add(CreateStateGateRow(stateKey, gateValues[i], i, editable, addPreviousGate));
            }

            if (gateValues.Length == 0)
            {
                Label emptyLabel = new("Unrestricted");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                box.Add(emptyLabel);
            }

            return box;
        }

        private VisualElement CreateStateGateRow(string stateKey, string targetStateKey, int index, bool editable, bool previousGate)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2;

            XAnimationEditorSelectionField stateField = CreateStateSelectionField(string.Empty, targetStateKey, stateKey);
            stateField.style.flexGrow = 1;
            stateField.tooltip = previousGate ? "允许哪些 state 切到当前 state。" : "当前 state 允许切到哪些 state。";
            stateField.SetEnabled(editable);
            stateField.ValueChanged += (previousValue, newValue) =>
            {
                if (previousGate)
                {
                    ChangeStateAllowedPreviousState(stateKey, index, newValue, stateField, previousValue);
                }
                else
                {
                    ChangeStateAllowedNextState(stateKey, index, newValue, stateField, previousValue);
                }
            };
            row.Add(stateField);

            Button deleteButton = new(() =>
            {
                if (previousGate)
                {
                    DeleteStateAllowedPreviousState(stateKey, index);
                }
                else
                {
                    DeleteStateAllowedNextState(stateKey, index);
                }
            })
            {
                text = "⌫"
            };
            deleteButton.tooltip = editable ? "删除这条 state 门禁配置。" : "Override 资源不能编辑 state 门禁。";
            deleteButton.SetEnabled(editable);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;
            row.Add(deleteButton);
            return row;
        }

        private VisualElement CreateAutoTransitionEditor(XAnimationCompiledAutoTransition transition)
        {
            XAnimationAutoTransitionConfig transitionConfig = transition.Config;
            string preStateKey = transition.PreStateKey;
            XAnimationCompiledState preState = m_Session.CompiledAsset.GetState(preStateKey);
            XAnimationStateConfig config = preState.Config;
            bool loopEnabled = config.loop;
            bool editable = m_Session != null && m_Session.IsLoaded && !m_Session.IsOverrideAsset;
            string nextStateKey = transitionConfig.nextStateKey ?? string.Empty;
            string currentNextStateKey = nextStateKey;
            float currentExitTime = transitionConfig.exitTime;
            float currentTransitionDuration = transitionConfig.transitionDuration;
            float currentEnterTime = transitionConfig.enterTime;
            bool suppressTimingCallbacks = false;
            float lastPreStateDurationSeconds = 0f;
            float lastNextStateDurationSeconds = 0f;
            float lastNextStateStartSeconds = 0f;
            float lastNextStateEndSeconds = 0f;
            float lastTransitionStartSeconds = 0f;
            float lastAxisDurationSeconds = 0.1f;

            DropdownField preStateField = CreateAutoTransitionPreStateDropdown(string.Empty, preStateKey);
            ConfigureAutoTransitionHeaderDropdown(preStateField, 120f);
            preStateField.tooltip = "当前 Auto Transition 的源状态。";
            preStateField.SetEnabled(editable);
            AttachDropdownInspectorButton(
                preStateField,
                () => preStateField?.value ?? preStateKey,
                () => HasState(preStateField?.value ?? preStateKey),
                () => FocusStateInInspector(preStateField?.value ?? preStateKey),
                "定位到 States 面板里当前 state 对应的条目。");

            Label arrowLabel = new("->");
            arrowLabel.style.marginLeft = 4;
            arrowLabel.style.marginRight = 4;
            arrowLabel.style.color = TextMuted;
            arrowLabel.style.fontSize = BodyFontSize;
            arrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            XAnimationEditorSelectionField nextStateField = CreateStateSelectionField(string.Empty, nextStateKey, preStateKey, includeNone: true);
            nextStateField.style.width = 180f;
            nextStateField.style.minWidth = 120f;
            nextStateField.style.flexGrow = 1;
            nextStateField.style.flexShrink = 1;
            nextStateField.tooltip = "非循环 state 播放完成后自动切到的目标 state。None 表示关闭自动切换。";
            nextStateField.SetEnabled(!loopEnabled && editable);

            Button deleteButton = new(() => DeleteAutoTransition(preStateKey))
            {
                text = "⌫"
            };
            deleteButton.tooltip = editable ? "删除这个 Auto Transition。" : "Override 资源不能删除 Auto Transition。";
            deleteButton.SetEnabled(editable);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;

            Button playButton = new(() => ToggleStatePlayback(preState))
            {
                text = "▶"
            };
            playButton.tooltip = "播放或停止这个 Auto Transition 对应的 preState。";
            ApplyClipButtonStyle(playButton, false);

            VisualElement headerActions = new();
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.alignItems = Align.Center;
            headerActions.style.flexGrow = 1;
            headerActions.style.minWidth = 0;

            VisualElement summaryRow = new();
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.flexGrow = 1;
            summaryRow.style.flexShrink = 1;
            summaryRow.style.flexBasis = 0;
            summaryRow.style.minWidth = 0;
            headerActions.Add(summaryRow);

            preStateField.style.width = 180f;
            preStateField.style.minWidth = 120f;
            preStateField.style.flexGrow = 1;
            preStateField.style.flexShrink = 1;
            summaryRow.Add(preStateField);
            summaryRow.Add(arrowLabel);
            summaryRow.Add(nextStateField);

            VisualElement actionsRow = new();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.alignItems = Align.Center;
            actionsRow.style.justifyContent = Justify.FlexEnd;
            actionsRow.style.flexShrink = 0;
            actionsRow.style.marginLeft = 6;
            headerActions.Add(actionsRow);
            playButton.style.flexShrink = 0;
            deleteButton.style.flexShrink = 0;
            actionsRow.Add(playButton);
            actionsRow.Add(deleteButton);

            string autoTransitionHeaderTooltip = loopEnabled
                ? "仅非循环状态可自动切换。点击空白区域可展开或收起这一项。"
                : "在 ExitTime 触发自动切换，TransitionDuration 为共用过渡时长，EnterTime 决定目标状态的起播点。点击空白区域可展开或收起这一项。";

            FoldoutCard card = CreateSectionFoldoutCard(
                string.Empty,
                IsAutoTransitionExpanded(preStateKey),
                value =>
                {
                    SetAutoTransitionExpanded(preStateKey, value);
                    ScheduleAutoTransitionEditorRebuild();
                },
                headerActions,
                headerTooltip: autoTransitionHeaderTooltip,
                allowActionAreaBackgroundToggle: true);
            m_AutoTransitionRowMap[preStateKey] = card.Root;

            VisualElement content = card.Content;

            VisualElement timelineBox = CreateSubBox();
            Color nextStateInactiveColor = new(0.20f, 0.34f, 0.58f, 0.32f);
            Color nextStateActiveColor = new(0.24f, 0.50f, 0.92f, 0.72f);
            Color transitionMarkerColor = new(0.95f, 0.82f, 0.24f, 1f);

            VisualElement rulerRow = CreateAutoTransitionTimelineRow("Sec", out _, out VisualElement rulerTrack);
            rulerTrack.style.height = 26;
            rulerTrack.style.overflow = Overflow.Hidden;
            VisualElement rulerTicksLayer = new();
            rulerTicksLayer.style.position = Position.Absolute;
            rulerTicksLayer.style.left = 0;
            rulerTicksLayer.style.right = 0;
            rulerTicksLayer.style.top = 0;
            rulerTicksLayer.style.bottom = 0;
            rulerTrack.Add(rulerTicksLayer);
            VisualElement rulerTransitionStartLine = CreateAutoTransitionTimelineMarkerLine(transitionMarkerColor);
            VisualElement rulerTransitionEndLine = CreateAutoTransitionTimelineMarkerLine(transitionMarkerColor);
            rulerTrack.Add(rulerTransitionStartLine);
            rulerTrack.Add(rulerTransitionEndLine);
            timelineBox.Add(rulerRow);

            VisualElement preStateRow = CreateAutoTransitionTimelineRow(preStateKey, out _, out VisualElement preStateTrack);
            preStateTrack.style.overflow = Overflow.Hidden;
            VisualElement preStateFill = CreateAutoTransitionTimelineFill(AccentColor, 0.72f);
            VisualElement preTransitionStartLine = CreateAutoTransitionTimelineMarkerLine(transitionMarkerColor);
            VisualElement preTransitionEndLine = CreateAutoTransitionTimelineMarkerLine(transitionMarkerColor);
            preStateTrack.Add(preStateFill);
            preStateTrack.Add(preTransitionStartLine);
            preStateTrack.Add(preTransitionEndLine);
            timelineBox.Add(preStateRow);

            VisualElement nextStateRow = CreateAutoTransitionTimelineRow(GetAutoTransitionTimelineStateLabel(currentNextStateKey), out Label nextStateLabel, out VisualElement nextStateTrack);
            nextStateTrack.style.overflow = Overflow.Hidden;
            VisualElement nextStateFill = CreateAutoTransitionTimelineFill(nextStateInactiveColor, 1f);
            VisualElement nextStatePlayedFill = CreateAutoTransitionTimelineFill(nextStateActiveColor, 1f);
            VisualElement nextTransitionStartLine = CreateAutoTransitionTimelineMarkerLine(transitionMarkerColor);
            VisualElement nextTransitionEndLine = CreateAutoTransitionTimelineMarkerLine(transitionMarkerColor);
            nextStateTrack.Add(nextStateFill);
            nextStateTrack.Add(nextStatePlayedFill);
            nextStateTrack.Add(nextTransitionStartLine);
            nextStateTrack.Add(nextTransitionEndLine);
            timelineBox.Add(nextStateRow);
            content.Add(timelineBox);

            FloatField exitTimeField = new("ExitTime") { value = currentExitTime };
            Slider exitTimeSlider = new(0f, 1f) { value = currentExitTime };
            VisualElement exitTimeRow = CreateAutoTransitionTimingRow(exitTimeField, exitTimeSlider);
            exitTimeField.tooltip = "当前状态播到哪个 normalized time 时开始自动切换。范围 [0, 1]。";
            exitTimeSlider.tooltip = exitTimeField.tooltip;
            exitTimeField.SetEnabled(!loopEnabled && editable);
            exitTimeSlider.SetEnabled(!loopEnabled && editable);
            content.Add(exitTimeRow);

            FloatField transitionDurationField = new("TransitionDuration") { value = currentTransitionDuration };
            Slider transitionDurationSlider = new(0f, GetAutoTransitionDurationSliderMax(0f))
            {
                value = currentTransitionDuration
            };
            VisualElement transitionDurationRow = CreateAutoTransitionTimingRow(transitionDurationField, transitionDurationSlider);
            transitionDurationField.tooltip = "<= 0 表示回退到目标 state 默认 fadeIn / fadeOut。";
            transitionDurationSlider.tooltip = "拖动调节过渡时长。";
            transitionDurationField.SetEnabled(!loopEnabled && editable);
            transitionDurationSlider.SetEnabled(!loopEnabled && editable);
            content.Add(transitionDurationRow);

            FloatField enterTimeField = new("EnterTime") { value = currentEnterTime };
            Slider enterTimeSlider = new(0f, 1f) { value = currentEnterTime };
            VisualElement enterTimeRow = CreateAutoTransitionTimingRow(enterTimeField, enterTimeSlider);
            enterTimeField.tooltip = "目标状态从哪个 normalized time 开始播放。范围 [0, 1]。";
            enterTimeSlider.tooltip = enterTimeField.tooltip;
            enterTimeField.SetEnabled(!loopEnabled && editable);
            enterTimeSlider.SetEnabled(!loopEnabled && editable);
            content.Add(enterTimeRow);

            void SyncTimelinePreview()
            {
                float preStateDurationSeconds = 0f;
                bool hasPreDuration = m_Session != null &&
                                      m_Session.IsLoaded &&
                                      m_Session.CompiledAsset.TryGetStateDuration(preStateKey, out preStateDurationSeconds);

                float nextStateDurationSeconds = 0f;
                bool hasNextDuration = m_Session != null &&
                                       m_Session.IsLoaded &&
                                       !string.IsNullOrWhiteSpace(currentNextStateKey) &&
                                       m_Session.CompiledAsset.TryGetStateDuration(currentNextStateKey, out nextStateDurationSeconds);

                float transitionStartSeconds = preStateDurationSeconds * currentExitTime;
                float transitionEndSeconds = transitionStartSeconds + currentTransitionDuration;
                float nextStateStartSeconds = hasNextDuration
                    ? transitionStartSeconds - (nextStateDurationSeconds * currentEnterTime)
                    : 0f;
                float nextStateEndSeconds = hasNextDuration
                    ? nextStateStartSeconds + nextStateDurationSeconds
                    : 0f;
                float axisDurationSeconds = Mathf.Max(
                    Mathf.Max(0.1f, preStateDurationSeconds),
                    Mathf.Max(transitionEndSeconds, nextStateEndSeconds));

                lastPreStateDurationSeconds = hasPreDuration ? preStateDurationSeconds : 0f;
                lastNextStateDurationSeconds = hasNextDuration ? nextStateDurationSeconds : 0f;
                lastNextStateStartSeconds = hasNextDuration ? nextStateStartSeconds : 0f;
                lastNextStateEndSeconds = hasNextDuration ? nextStateEndSeconds : 0f;
                lastTransitionStartSeconds = transitionStartSeconds;
                lastAxisDurationSeconds = axisDurationSeconds;

                nextStateLabel.text = GetAutoTransitionTimelineStateLabel(currentNextStateKey);
                RebuildAutoTransitionTimelineRuler(rulerTicksLayer, axisDurationSeconds);
                UpdateAutoTransitionTimelineSegment(preStateFill, 0f, preStateDurationSeconds, axisDurationSeconds);
                UpdateAutoTransitionTimelineSegment(nextStateFill, nextStateStartSeconds, nextStateEndSeconds, axisDurationSeconds);
                UpdateAutoTransitionTimelineSegment(nextStatePlayedFill, transitionStartSeconds, nextStateEndSeconds, axisDurationSeconds);

                UpdateAutoTransitionTimelineMarker(rulerTransitionStartLine, transitionStartSeconds, axisDurationSeconds);
                UpdateAutoTransitionTimelineMarker(rulerTransitionEndLine, transitionEndSeconds, axisDurationSeconds);
                UpdateAutoTransitionTimelineMarker(preTransitionStartLine, transitionStartSeconds, axisDurationSeconds);
                UpdateAutoTransitionTimelineMarker(preTransitionEndLine, transitionEndSeconds, axisDurationSeconds);
                UpdateAutoTransitionTimelineMarker(nextTransitionStartLine, transitionStartSeconds, axisDurationSeconds);
                UpdateAutoTransitionTimelineMarker(nextTransitionEndLine, transitionEndSeconds, axisDurationSeconds);
            }

            void ApplyTimelineDrag(AutoTransitionTimelineDragMode mode, VisualElement track, Vector2 pointerPosition)
            {
                if (!editable || loopEnabled || m_Session == null || !m_Session.IsLoaded || track == null)
                {
                    return;
                }

                Rect trackBounds = track.worldBound;
                if (trackBounds.width <= 0f)
                {
                    return;
                }

                float pointerX = Mathf.Clamp(pointerPosition.x - trackBounds.xMin, 0f, trackBounds.width);
                float axisSeconds = Mathf.Max(0.0001f, lastAxisDurationSeconds);
                float targetSeconds = (pointerX / trackBounds.width) * axisSeconds;

                switch (mode)
                {
                    case AutoTransitionTimelineDragMode.ExitTime:
                    {
                        float exitTime = lastPreStateDurationSeconds > 0f
                            ? Mathf.Clamp01(targetSeconds / lastPreStateDurationSeconds)
                            : 0f;
                        ApplyTimingChange(
                            exitTime,
                            currentTransitionDuration,
                            currentEnterTime,
                            $"ExitTime = {exitTime:0.###}。");
                        break;
                    }
                    case AutoTransitionTimelineDragMode.TransitionDuration:
                    {
                        float duration = Mathf.Max(0f, targetSeconds - lastTransitionStartSeconds);
                        ApplyTimingChange(
                            currentExitTime,
                            duration,
                            currentEnterTime,
                            $"TransitionDuration = {duration:0.###}。");
                        break;
                    }
                    case AutoTransitionTimelineDragMode.EnterTime:
                    {
                        float enterTime = lastNextStateDurationSeconds > 0f
                            ? Mathf.Clamp01((lastTransitionStartSeconds - targetSeconds) / lastNextStateDurationSeconds)
                            : 0f;
                        ApplyTimingChange(
                            currentExitTime,
                            currentTransitionDuration,
                            enterTime,
                            $"EnterTime = {enterTime:0.###}。");
                        break;
                    }
                }
            }

            bool TryGetTrackTime(VisualElement track, Vector2 pointerPosition, out float targetSeconds)
            {
                targetSeconds = 0f;
                if (track == null)
                {
                    return false;
                }

                Rect trackBounds = track.worldBound;
                if (trackBounds.width <= 0f)
                {
                    return false;
                }

                float pointerX = Mathf.Clamp(pointerPosition.x - trackBounds.xMin, 0f, trackBounds.width);
                float axisSeconds = Mathf.Max(0.0001f, lastAxisDurationSeconds);
                targetSeconds = (pointerX / trackBounds.width) * axisSeconds;
                return true;
            }

            void RegisterTimelineDragHandle(VisualElement element, VisualElement track, AutoTransitionTimelineDragMode mode, string tooltip)
            {
                if (element == null)
                {
                    return;
                }

                element.tooltip = tooltip;
                if (!editable || loopEnabled)
                {
                    return;
                }

                int activePointerId = PointerId.invalidPointerId;

                element.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    activePointerId = evt.pointerId;
                    element.CapturePointer(activePointerId);
                    ApplyTimelineDrag(mode, track, evt.position);
                    evt.StopPropagation();
                });

                element.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (activePointerId != evt.pointerId || !element.HasPointerCapture(evt.pointerId))
                    {
                        return;
                    }

                    ApplyTimelineDrag(mode, track, evt.position);
                    evt.StopPropagation();
                });

                element.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (activePointerId != evt.pointerId)
                    {
                        return;
                    }

                    if (element.HasPointerCapture(evt.pointerId))
                    {
                        element.ReleasePointer(evt.pointerId);
                    }

                    activePointerId = PointerId.invalidPointerId;
                    evt.StopPropagation();
                });

                element.RegisterCallback<PointerCaptureOutEvent>(_ =>
                {
                    activePointerId = PointerId.invalidPointerId;
                });
            }

            void RegisterEnterTimeTrackDragHandle(VisualElement track, string tooltip)
            {
                if (track == null)
                {
                    return;
                }

                track.tooltip = tooltip;
                if (!editable || loopEnabled)
                {
                    return;
                }

                int activePointerId = PointerId.invalidPointerId;
                bool dragging = false;

                track.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                    {
                        return;
                    }

                    if (!TryGetTrackTime(track, evt.position, out float targetSeconds))
                    {
                        return;
                    }

                    if (lastNextStateDurationSeconds <= 0f ||
                        targetSeconds < lastNextStateStartSeconds ||
                        targetSeconds > lastNextStateEndSeconds)
                    {
                        return;
                    }

                    dragging = true;
                    activePointerId = evt.pointerId;
                    track.CapturePointer(activePointerId);
                    ApplyTimelineDrag(AutoTransitionTimelineDragMode.EnterTime, track, evt.position);
                    evt.StopPropagation();
                });

                track.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (!dragging || activePointerId != evt.pointerId || !track.HasPointerCapture(evt.pointerId))
                    {
                        return;
                    }

                    ApplyTimelineDrag(AutoTransitionTimelineDragMode.EnterTime, track, evt.position);
                    evt.StopPropagation();
                });

                track.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!dragging || activePointerId != evt.pointerId)
                    {
                        return;
                    }

                    if (track.HasPointerCapture(evt.pointerId))
                    {
                        track.ReleasePointer(evt.pointerId);
                    }

                    dragging = false;
                    activePointerId = PointerId.invalidPointerId;
                    evt.StopPropagation();
                });

                track.RegisterCallback<PointerCaptureOutEvent>(_ =>
                {
                    dragging = false;
                    activePointerId = PointerId.invalidPointerId;
                });
            }

            void SyncTimingControls()
            {
                suppressTimingCallbacks = true;
                exitTimeField.SetValueWithoutNotify(currentExitTime);
                exitTimeSlider.SetValueWithoutNotify(currentExitTime);
                transitionDurationField.SetValueWithoutNotify(currentTransitionDuration);
                transitionDurationSlider.highValue = GetAutoTransitionDurationSliderMax(lastNextStateDurationSeconds);
                transitionDurationSlider.SetValueWithoutNotify(Mathf.Min(currentTransitionDuration, transitionDurationSlider.highValue));
                enterTimeField.SetValueWithoutNotify(currentEnterTime);
                enterTimeSlider.SetValueWithoutNotify(currentEnterTime);
                SyncTimelinePreview();
                suppressTimingCallbacks = false;
            }

            void ApplyTimingChange(float exitTime, float transitionDuration, float enterTime, string statusText)
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                currentExitTime = Mathf.Clamp01(exitTime);
                currentTransitionDuration = Mathf.Max(0f, transitionDuration);
                currentEnterTime = Mathf.Clamp01(enterTime);
                m_Session.SetAutoTransitionTiming(preStateKey, currentExitTime, currentTransitionDuration, currentEnterTime, save: false);
                ScheduleAssetSave();
                RefreshChannelStates();
                SyncTimingControls();
                SetStatus($"{preStateKey} {statusText}");
            }

            preStateField.RegisterValueChangedCallback(evt =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                string newPreStateKey = evt.newValue ?? string.Empty;
                bool wasExpanded = IsAutoTransitionExpanded(preStateKey);
                m_Session.SetAutoTransitionPreState(preStateKey, newPreStateKey, save: false);
                m_SelectedAutoTransitionStateKey = newPreStateKey;
                SetAutoTransitionExpanded(preStateKey, true);
                if (!string.IsNullOrWhiteSpace(newPreStateKey))
                {
                    SetAutoTransitionExpanded(newPreStateKey, wasExpanded);
                }

                m_CollapsedAutoTransitionKeys.Remove(preStateKey);
                ScheduleAssetSave();
                RebuildAutoTransitionEditor();
                RefreshChannelStates();
                SetStatus($"{preStateKey} auto transition preState = {newPreStateKey}。");
            });

            nextStateField.ValueChanged += (_, newValue) =>
            {
                if (m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                string newNextStateKey = NormalizeOptionalStateDropdownValue(newValue);
                currentNextStateKey = newNextStateKey;
                m_Session.SetAutoTransitionNextState(preStateKey, newNextStateKey, save: false);
                ScheduleAssetSave();
                RefreshChannelStates();
                SyncTimelinePreview();
                SetStatus(string.IsNullOrWhiteSpace(newNextStateKey)
                    ? $"{preStateKey} auto next = None。"
                    : $"{preStateKey} auto next = {newNextStateKey}。");
            };

            exitTimeField.RegisterValueChangedCallback(evt =>
            {
                if (suppressTimingCallbacks)
                {
                    return;
                }

                ApplyTimingChange(evt.newValue, currentTransitionDuration, currentEnterTime, $"ExitTime = {Mathf.Clamp01(evt.newValue):0.###}。");
            });
            exitTimeSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressTimingCallbacks)
                {
                    return;
                }

                ApplyTimingChange(evt.newValue, currentTransitionDuration, currentEnterTime, $"ExitTime = {evt.newValue:0.###}。");
            });

            transitionDurationField.RegisterValueChangedCallback(evt =>
            {
                if (suppressTimingCallbacks)
                {
                    return;
                }

                ApplyTimingChange(currentExitTime, evt.newValue, currentEnterTime, $"TransitionDuration = {Mathf.Max(0f, evt.newValue):0.###}。");
            });
            transitionDurationSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressTimingCallbacks)
                {
                    return;
                }

                ApplyTimingChange(currentExitTime, evt.newValue, currentEnterTime, $"TransitionDuration = {evt.newValue:0.###}。");
            });

            enterTimeField.RegisterValueChangedCallback(evt =>
            {
                if (suppressTimingCallbacks)
                {
                    return;
                }

                ApplyTimingChange(currentExitTime, currentTransitionDuration, evt.newValue, $"EnterTime = {Mathf.Clamp01(evt.newValue):0.###}。");
            });
            enterTimeSlider.RegisterValueChangedCallback(evt =>
            {
                if (suppressTimingCallbacks)
                {
                    return;
                }

                ApplyTimingChange(currentExitTime, currentTransitionDuration, evt.newValue, $"EnterTime = {evt.newValue:0.###}。");
            });

            const string exitMarkerTooltip = "左右拖拽，直接调整 ExitTime。";
            const string durationMarkerTooltip = "左右拖拽，直接调整 TransitionDuration。";
            const string enterRegionTooltip = "左右拖拽目标 state 区块，直接调整 EnterTime。";
            RegisterTimelineDragHandle(rulerTransitionStartLine, rulerTrack, AutoTransitionTimelineDragMode.ExitTime, exitMarkerTooltip);
            RegisterTimelineDragHandle(preTransitionStartLine, preStateTrack, AutoTransitionTimelineDragMode.ExitTime, exitMarkerTooltip);
            RegisterTimelineDragHandle(nextTransitionStartLine, nextStateTrack, AutoTransitionTimelineDragMode.ExitTime, exitMarkerTooltip);
            RegisterTimelineDragHandle(rulerTransitionEndLine, rulerTrack, AutoTransitionTimelineDragMode.TransitionDuration, durationMarkerTooltip);
            RegisterTimelineDragHandle(preTransitionEndLine, preStateTrack, AutoTransitionTimelineDragMode.TransitionDuration, durationMarkerTooltip);
            RegisterTimelineDragHandle(nextTransitionEndLine, nextStateTrack, AutoTransitionTimelineDragMode.TransitionDuration, durationMarkerTooltip);
            nextStateFill.tooltip = enterRegionTooltip;
            nextStatePlayedFill.tooltip = enterRegionTooltip;
            RegisterEnterTimeTrackDragHandle(nextStateTrack, enterRegionTooltip);

            SyncTimingControls();
            return card.Root;
        }

        private bool IsAutoTransitionExpanded(string preStateKey)
        {
            return m_CollapsedAutoTransitionKeys.Contains(preStateKey);
        }

        private void SetAutoTransitionExpanded(string preStateKey, bool expanded)
        {
            if (string.IsNullOrWhiteSpace(preStateKey))
            {
                return;
            }

            if (expanded)
            {
                if (m_CollapsedAutoTransitionKeys.Count > 0)
                {
                    m_CollapsedAutoTransitionKeys.Clear();
                }

                m_CollapsedAutoTransitionKeys.Add(preStateKey);
                return;
            }

            m_CollapsedAutoTransitionKeys.Remove(preStateKey);
        }

        private bool IsDefaultTransitionExpanded(int transitionIndex)
        {
            return !m_CollapsedDefaultTransitionIndices.Contains(transitionIndex);
        }

        private void SetDefaultTransitionExpanded(int transitionIndex, bool expanded)
        {
            int transitionCount = m_Session?.CompiledAsset?.DefaultTransitions?.Count ?? 0;
            if (transitionIndex < 0 || (transitionCount > 0 && transitionIndex >= transitionCount))
            {
                return;
            }

            if (expanded)
            {
                m_CollapsedDefaultTransitionIndices.Clear();
                for (int i = 0; i < transitionCount; i++)
                {
                    if (i != transitionIndex)
                    {
                        m_CollapsedDefaultTransitionIndices.Add(i);
                    }
                }
                return;
            }

            m_CollapsedDefaultTransitionIndices.Add(transitionIndex);
        }

        private void NormalizeCollapsedDefaultTransitionIndicesAfterDelete(int deletedIndex)
        {
            HashSet<int> normalized = new();
            foreach (int index in m_CollapsedDefaultTransitionIndices)
            {
                if (index == deletedIndex)
                {
                    continue;
                }

                normalized.Add(index > deletedIndex ? index - 1 : index);
            }

            int remainingCount = Math.Max(0, (m_Session?.CompiledAsset?.DefaultTransitions?.Count ?? 0) - 1);
            if (remainingCount > 0 && normalized.Count < remainingCount - 1)
            {
                int expandedIndex = -1;
                for (int index = 0; index < remainingCount; index++)
                {
                    if (!normalized.Contains(index))
                    {
                        expandedIndex = index;
                        break;
                    }
                }

                normalized.Clear();
                for (int index = 0; index < remainingCount; index++)
                {
                    if (index != expandedIndex)
                    {
                        normalized.Add(index);
                    }
                }
            }

            m_CollapsedDefaultTransitionIndices.Clear();
            foreach (int index in normalized)
            {
                m_CollapsedDefaultTransitionIndices.Add(index);
            }
        }

        private static string FormatDefaultTransitionPairSummary(XAnimationDefaultTransitionConfig config)
        {
            XAnimationTransitionPairConfig[] pairs = config?.pairs ?? Array.Empty<XAnimationTransitionPairConfig>();
            if (pairs.Length == 0)
            {
                return "0 pairs";
            }

            XAnimationTransitionPairConfig firstPair = pairs[0];
            string first = firstPair == null
                ? "invalid"
                : $"{firstPair.preStateKey} -> {firstPair.nextStateKey}";
            return pairs.Length == 1 ? first : $"{first} +{pairs.Length - 1}";
        }

        private static void ConfigureAutoTransitionHeaderDropdown(DropdownField field, float minWidth)
        {
            if (field.labelElement != null)
            {
                field.labelElement.style.display = DisplayStyle.None;
            }

            field.style.minWidth = minWidth;
            field.style.flexGrow = 1;
            field.style.marginLeft = 2;
            field.style.marginRight = 2;
        }

        private static VisualElement CreateAutoTransitionTimelineRow(string labelText, out Label label, out VisualElement track)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;

            label = new(labelText);
            label.style.width = 56;
            label.style.minWidth = 56;
            label.style.maxWidth = 56;
            label.style.flexShrink = 0;
            label.style.marginRight = 6;
            label.style.color = TextMuted;
            label.style.fontSize = 10;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.tooltip = labelText;
            row.Add(label);

            track = new VisualElement();
            track.style.position = Position.Relative;
            track.style.flexGrow = 1;
            track.style.height = 16;
            track.style.backgroundColor = new Color(0.10f, 0.10f, 0.11f, 1f);
            track.style.borderTopWidth = 1;
            track.style.borderBottomWidth = 1;
            track.style.borderLeftWidth = 1;
            track.style.borderRightWidth = 1;
            track.style.borderTopColor = SectionDivider;
            track.style.borderBottomColor = SectionDivider;
            track.style.borderLeftColor = SectionDivider;
            track.style.borderRightColor = SectionDivider;
            track.style.borderTopLeftRadius = 2;
            track.style.borderTopRightRadius = 2;
            track.style.borderBottomLeftRadius = 2;
            track.style.borderBottomRightRadius = 2;
            row.Add(track);
            return row;
        }

        private static VisualElement CreateAutoTransitionTimingRow(FloatField field, Slider slider)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;

            field.style.width = 170;
            field.style.minWidth = 170;
            field.style.maxWidth = 170;
            row.Add(field);

            slider.style.flexGrow = 1;
            slider.style.marginLeft = 8;
            row.Add(slider);
            return row;
        }

        private static VisualElement CreateDefaultTransitionOptionRow()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6;
            row.style.minWidth = 0;
            return row;
        }

        private static FloatField CreateDefaultTransitionFloatField(string label, float value, bool editable)
        {
            FloatField field = new(label) { value = value };
            ConfigureDefaultTransitionField(field, editable);
            return field;
        }

        private static void ConfigureDefaultTransitionField(BaseField<float> field, bool editable)
        {
            field.style.width = 150;
            field.style.minWidth = 150;
            field.style.maxWidth = 150;
            field.style.flexShrink = 0;
            field.style.marginRight = 8;
            ConfigureDefaultTransitionFieldLabel(field.labelElement);
            field.SetEnabled(editable);
        }

        private static void ConfigureDefaultTransitionField(IntegerField field, bool editable)
        {
            field.style.width = 150;
            field.style.minWidth = 150;
            field.style.maxWidth = 150;
            field.style.flexShrink = 0;
            field.style.marginRight = 8;
            ConfigureDefaultTransitionFieldLabel(field.labelElement);
            field.SetEnabled(editable);
        }

        private static void ConfigureDefaultTransitionToggleField(Toggle toggle, bool editable)
        {
            toggle.SetEnabled(editable);
            toggle.style.width = 150;
            toggle.style.minWidth = 150;
            toggle.style.maxWidth = 150;
            toggle.style.flexShrink = 0;
            toggle.style.marginRight = 8;
            ConfigureDefaultTransitionFieldLabel(toggle.labelElement, 88);
        }

        private static void ConfigureDefaultTransitionFieldLabel(VisualElement labelElement, float width = 66)
        {
            if (labelElement == null)
            {
                return;
            }

            labelElement.style.minWidth = width;
            labelElement.style.width = width;
            labelElement.style.maxWidth = width;
            labelElement.style.marginRight = 4;
        }

        private static float GetAutoTransitionDurationSliderMax(float duration)
        {
            return Mathf.Max(1f, Mathf.Ceil(Mathf.Max(0f, duration) * 10f) / 10f);
        }

        private static string GetAutoTransitionTimelineStateLabel(string stateKey)
        {
            return string.IsNullOrWhiteSpace(stateKey) ? "None" : stateKey;
        }

        private string GetFallbackNextState(string preStateKey)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return string.Empty;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                string stateKey = states[i].Key;
                if (!string.Equals(stateKey, preStateKey, StringComparison.Ordinal))
                {
                    return stateKey;
                }
            }

            return string.Empty;
        }

        private static VisualElement CreateAutoTransitionTimelineFill(Color color, float opacity)
        {
            VisualElement fill = new();
            fill.style.position = Position.Absolute;
            fill.style.top = 2;
            fill.style.bottom = 2;
            fill.style.backgroundColor = color;
            fill.style.opacity = opacity;
            fill.style.borderTopLeftRadius = 2;
            fill.style.borderTopRightRadius = 2;
            fill.style.borderBottomLeftRadius = 2;
            fill.style.borderBottomRightRadius = 2;
            return fill;
        }

        private static VisualElement CreateAutoTransitionTimelineMarkerLine(Color color)
        {
            VisualElement line = new();
            line.style.position = Position.Absolute;
            line.style.top = 0;
            line.style.bottom = 0;
            line.style.width = 2;
            line.style.backgroundColor = color;
            return line;
        }

        private static void UpdateAutoTransitionTimelineSegment(
            VisualElement segment,
            float startSeconds,
            float endSeconds,
            float axisDurationSeconds)
        {
            if (segment == null)
            {
                return;
            }

            float safeAxisDuration = Mathf.Max(0.0001f, axisDurationSeconds);
            float clampedStart = Mathf.Clamp(startSeconds, 0f, safeAxisDuration);
            float clampedEnd = Mathf.Clamp(endSeconds, 0f, safeAxisDuration);
            if (clampedEnd <= clampedStart)
            {
                segment.style.display = DisplayStyle.None;
                return;
            }

            segment.style.display = DisplayStyle.Flex;
            segment.style.left = Length.Percent((clampedStart / safeAxisDuration) * 100f);
            segment.style.width = Length.Percent(((clampedEnd - clampedStart) / safeAxisDuration) * 100f);
        }

        private static void UpdateAutoTransitionTimelineMarker(
            VisualElement marker,
            float timeSeconds,
            float axisDurationSeconds)
        {
            if (marker == null)
            {
                return;
            }

            float safeAxisDuration = Mathf.Max(0.0001f, axisDurationSeconds);
            float clampedTime = Mathf.Clamp(timeSeconds, 0f, safeAxisDuration);
            marker.style.left = Length.Percent((clampedTime / safeAxisDuration) * 100f);
        }

        private static void RebuildAutoTransitionTimelineRuler(VisualElement rulerTicksLayer, float durationSeconds)
        {
            if (rulerTicksLayer == null)
            {
                return;
            }

            rulerTicksLayer.Clear();
            float safeDuration = Mathf.Max(0.1f, durationSeconds);
            float step = GetAutoTransitionRulerStep(safeDuration);
            int tickCount = Mathf.Max(1, Mathf.CeilToInt(safeDuration / step));
            for (int i = 0; i <= tickCount; i++)
            {
                float time = Mathf.Min(i * step, safeDuration);
                float ratio = time / safeDuration;

                VisualElement tick = new();
                tick.style.position = Position.Absolute;
                tick.style.left = Length.Percent(ratio * 100f);
                tick.style.top = 0;
                tick.style.width = 1;
                tick.style.height = 10;
                tick.style.backgroundColor = SectionDivider;
                rulerTicksLayer.Add(tick);

                Label tickLabel = new($"{time:0.##}s");
                tickLabel.style.position = Position.Absolute;
                tickLabel.style.left = Length.Percent(ratio * 100f);
                tickLabel.style.top = 10;
                tickLabel.style.width = 32;
                tickLabel.style.marginLeft = -16;
                tickLabel.style.fontSize = 9;
                tickLabel.style.color = TextMuted;
                tickLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                rulerTicksLayer.Add(tickLabel);
            }
        }

        private static float GetAutoTransitionRulerStep(float durationSeconds)
        {
            float roughStep = Mathf.Max(0.1f, durationSeconds / 8f);
            float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(roughStep)));
            float normalized = roughStep / magnitude;
            float stepBase = normalized <= 1f
                ? 1f
                : normalized <= 2f
                    ? 2f
                    : normalized <= 5f
                        ? 5f
                        : 10f;
            return stepBase * magnitude;
        }

    }
}
#endif
