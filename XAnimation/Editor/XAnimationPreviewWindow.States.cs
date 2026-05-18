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
using static XFramework.Editor.XAnimationEditorParameterUtility;
using static XFramework.Editor.XAnimationEditorUi;

namespace XFramework.Editor
{
    public sealed partial class XAnimationPreviewWindow
    {
        private static VisualElement CreateFoldoutRowEditor()
        {
            VisualElement editor = new VisualElement();
            editor.style.marginLeft = 4;
            editor.style.marginRight = 4;
            editor.style.marginTop = 1;
            editor.style.marginBottom = 3;
            editor.style.paddingLeft = 6;
            editor.style.paddingRight = 6;
            editor.style.paddingTop = 4;
            editor.style.paddingBottom = 4;
            editor.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 1f);
            editor.style.borderTopWidth = 1;
            editor.style.borderBottomWidth = 1;
            editor.style.borderLeftWidth = 1;
            editor.style.borderRightWidth = 1;
            editor.style.borderTopColor = SectionDivider;
            editor.style.borderBottomColor = SectionDivider;
            editor.style.borderLeftColor = SectionDivider;
            editor.style.borderRightColor = SectionDivider;
            editor.style.borderTopLeftRadius = 3;
            editor.style.borderTopRightRadius = 3;
            editor.style.borderBottomLeftRadius = 3;
            editor.style.borderBottomRightRadius = 3;
            return editor;
        }

        private static VisualElement CreateStateConfigSection()
        {
            VisualElement box = CreateSubBox();
            box.style.marginTop = 5;
            return box;
        }

        private VisualElement CreateBlendSampleEditor(string stateKey, XAnimationStateConfig config, VisualElement parameterField = null)
        {
            XAnimationBlend1DSampleConfig[] samples = config.samples ?? Array.Empty<XAnimationBlend1DSampleConfig>();
            return CreateSamplesSection(
                stateKey,
                "Samples",
                "右键批量修改这个 Blend1D state 用到的所有动画。",
                !m_CollapsedBlendSampleStateKeys.Contains(stateKey),
                collapsed => SetCollapsed(m_CollapsedBlendSampleStateKeys, stateKey, collapsed),
                () => AddBlendSample(stateKey),
                "为这个 Blend1D state 新增采样点。",
                parameterField,
                samples.Length,
                (content, editable) =>
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        content.Add(CreateBlendSampleRow(stateKey, i, samples[i], editable));
                    }
                });
        }

        private VisualElement CreateDirectionalBlendSampleEditor(
            string stateKey,
            XAnimationStateConfig config,
            VisualElement parameterXField = null,
            VisualElement parameterYField = null)
        {
            XAnimationBlend2DSimpleDirectionalSampleConfig[] samples =
                config.directionalSamples ?? Array.Empty<XAnimationBlend2DSimpleDirectionalSampleConfig>();
            VisualElement parameterRow = null;
            if (parameterXField != null || parameterYField != null)
            {
                parameterRow = new VisualElement();
                parameterRow.style.flexDirection = FlexDirection.Row;
                parameterRow.style.alignItems = Align.Center;
                parameterRow.style.marginBottom = 4;

                if (parameterXField != null)
                {
                    parameterXField.style.flexGrow = 1;
                    parameterXField.style.flexShrink = 1;
                    parameterRow.Add(parameterXField);
                }

                if (parameterYField != null)
                {
                    parameterYField.style.flexGrow = 1;
                    parameterYField.style.flexShrink = 1;
                    parameterYField.style.marginLeft = 8;
                    parameterRow.Add(parameterYField);
                }
            }

            return CreateSamplesSection(
                stateKey,
                "Directional Samples",
                "右键批量修改这个 directional state 用到的所有动画。",
                !m_CollapsedDirectionalSampleStateKeys.Contains(stateKey),
                collapsed => SetCollapsed(m_CollapsedDirectionalSampleStateKeys, stateKey, collapsed),
                () => AddDirectionalBlendSample(stateKey),
                $"为这个 {config.stateType} state 新增采样点。",
                parameterRow,
                samples.Length,
                (content, editable) =>
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        content.Add(CreateDirectionalBlendSampleRow(stateKey, i, samples[i], editable));
                    }
                });
        }

        private VisualElement CreateSamplesSection(
            string stateKey,
            string titleText,
            string titleTooltip,
            bool expanded,
            Action<bool> setCollapsed,
            Action addSample,
            string addTooltip,
            VisualElement leadingContent,
            int sampleCount,
            Action<VisualElement, bool> buildRows)
        {
            VisualElement box = CreateSubBox();
            box.style.marginTop = 5;
            VisualElement header = CreateListHeader(3);
            Label foldoutLabel = CreateFoldoutGlyph(expanded);
            Label title = CreateSectionTitleLabel(titleText);
            title.tooltip = titleTooltip;
            RegisterBatchEditStateClipsContextMenu(title, stateKey);
            header.Add(foldoutLabel);
            header.Add(title);

            bool editable = m_Session != null && !m_Session.IsOverrideAsset;
            Button addButton = new(addSample) { text = "+" };
            addButton.tooltip = editable ? addTooltip : "Override 资源不能新增采样点。";
            addButton.SetEnabled(editable);
            ApplyClipIconButtonStyle(addButton, AccentColor);
            header.Add(addButton);

            if (leadingContent != null)
            {
                leadingContent.style.marginBottom = 4;
                box.Add(leadingContent);
            }

            box.Add(header);

            VisualElement content = new() { style = { display = expanded ? DisplayStyle.Flex : DisplayStyle.None } };
            box.Add(content);
            buildRows(content, editable);
            if (sampleCount == 0)
            {
                AddEmptyLabel(content, "No samples");
            }

            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || addButton.worldBound.Contains(evt.mousePosition))
                {
                    return;
                }

                bool isExpanded = content.style.display != DisplayStyle.None;
                bool nextExpanded = !isExpanded;
                content.style.display = nextExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                foldoutLabel.text = nextExpanded ? "▾" : "▸";
                setCollapsed(!nextExpanded);
                evt.StopPropagation();
            });
            return box;
        }

        private static void SetCollapsed(HashSet<string> set, string key, bool collapsed)
        {
            if (collapsed)
            {
                set.Add(key);
            }
            else
            {
                set.Remove(key);
            }
        }
        private void RegisterBatchEditStateClipsContextMenu(VisualElement target, string stateKey)
        {
            if (target == null || string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            target.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "Batch Edit State Clips",
                    _ => OpenBatchClipSettingsForState(stateKey),
                    _ => CollectAnimationClipsForState(stateKey).Count > 0
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
            }));
        }

        private void RegisterStateLabelContextMenu(EditableLabel label, XAnimationCompiledState state)
        {
            if (label == null || state == null)
            {
                return;
            }

            label.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "Rename",
                    _ => label.BeginEdit(),
                    _ => label.IsEditing ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

                evt.menu.AppendSeparator();

                evt.menu.AppendAction(
                    "Batch Edit State Clips",
                    _ => OpenBatchClipSettingsForState(state.Key),
                    _ => CollectAnimationClipsForState(state.Key).Count > 0
                        ? DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Disabled);
            }));
        }

        private void RegisterStateGroupContextMenu(EditableLabel label, string channelName, string groupName)
        {
            if (label == null || string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            label.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "Rename",
                    _ => label.BeginEdit(),
                    _ => label.IsEditing || m_Session == null || m_Session.IsOverrideAsset
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);

                evt.menu.AppendAction(
                    "Delete Group",
                    _ => DeleteStateGroup(channelName, groupName),
                    _ => m_Session == null || m_Session.IsOverrideAsset
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }));
        }

        private void RegisterClipGroupContextMenu(EditableLabel label, string groupName)
        {
            if (label == null || string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            label.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction(
                    "Rename",
                    _ => label.BeginEdit(),
                    _ => label.IsEditing || m_Session == null || m_Session.IsOverrideAsset
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);

                evt.menu.AppendAction(
                    "Delete Group",
                    _ => DeleteClipGroup(groupName),
                    _ => m_Session == null || m_Session.IsOverrideAsset
                        ? DropdownMenuAction.Status.Disabled
                        : DropdownMenuAction.Status.Normal);
            }));
        }

        private void OpenBatchClipSettingsForState(string stateKey)
        {
            List<AnimationClip> clips = CollectAnimationClipsForState(stateKey);
            if (clips.Count == 0)
            {
                SetStatus($"state {stateKey} 没有可用于批量设置的动画。", true);
                return;
            }

            XAnimationClipBatchSettingsWindow.ShowWindowWithClips(clips);
        }

        private List<AnimationClip> CollectAnimationClipsForState(string stateKey)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return clips;
            }

            XAnimationAsset asset = m_Session?.CompiledAsset?.Asset;
            if (asset?.clips == null || asset.clips.Length == 0)
            {
                return clips;
            }

            XAnimationCompiledState state = m_Session.CompiledAsset.GetState(stateKey);
            if (state == null)
            {
                return clips;
            }

            Dictionary<string, XAnimationClipConfig> clipConfigByKey = new Dictionary<string, XAnimationClipConfig>(StringComparer.Ordinal);
            for (int i = 0; i < asset.clips.Length; i++)
            {
                XAnimationClipConfig clipConfig = asset.clips[i];
                if (clipConfig == null || string.IsNullOrWhiteSpace(clipConfig.key))
                {
                    continue;
                }

                clipConfigByKey[clipConfig.key] = clipConfig;
            }

            HashSet<string> addedClipPaths = new HashSet<string>(StringComparer.Ordinal);

            void TryAddClipByKey(string clipKey)
            {
                if (string.IsNullOrWhiteSpace(clipKey) || !clipConfigByKey.TryGetValue(clipKey, out XAnimationClipConfig clipConfig))
                {
                    return;
                }

                AnimationClip clip = XAnimationEditorAssetResolver.ResolveAnimationClip(clipConfig.clipPath);
                if (clip == null)
                {
                    return;
                }

                string resolvedClipPath = XAnimationEditorAssetResolver.BuildClipPath(clip);
                if (!addedClipPaths.Add(resolvedClipPath))
                {
                    return;
                }

                clips.Add(clip);
            }

            switch (state)
            {
                case XAnimationCompiledSingleState singleState:
                    TryAddClipByKey(singleState.Config.clipKey);
                    break;
                case XAnimationCompiledBlend1DState blend1DState:
                    for (int i = 0; i < blend1DState.Samples.Count; i++)
                    {
                        TryAddClipByKey(blend1DState.Samples[i].Config.clipKey);
                    }
                    break;
                case XAnimationCompiledBlend2DSimpleDirectionalState directionalState:
                    for (int i = 0; i < directionalState.Samples.Count; i++)
                    {
                        TryAddClipByKey(directionalState.Samples[i].Config.clipKey);
                    }
                    break;
                case XAnimationCompiledBlend2DFreeformDirectionalState freeformDirectionalState:
                    for (int i = 0; i < freeformDirectionalState.Samples.Count; i++)
                    {
                        TryAddClipByKey(freeformDirectionalState.Samples[i].Config.clipKey);
                    }
                    break;
            }

            return clips;
        }

        private VisualElement CreateParameterPreviewEditor(XAnimationCompiledParameter parameter)
        {
            if (parameter == null)
            {
                return null;
            }

            switch (parameter.Type)
            {
                case XAnimationParameterType.Float:
                {
                    float previewValue = GetPreviewFloatParameterValue(parameter);
                    if (TryGetBlend1DPreviewRange(parameter.Name, out float min, out float max) ||
                        TryGetDirectionalPreviewRange(parameter.Name, out min, out max))
                    {
                        return CreateFloatPreviewParameterRow(parameter.Name, previewValue, min, max, useSlider: true);
                    }

                    return CreateFloatPreviewParameterRow(parameter.Name, previewValue, previewValue, previewValue, useSlider: false);
                }
                case XAnimationParameterType.Bool:
                    return CreateBoolPreviewParameterRow(parameter.Name, GetPreviewBoolParameterValue(parameter));
                case XAnimationParameterType.Int:
                    return CreateIntPreviewParameterRow(parameter.Name, GetPreviewIntParameterValue(parameter));
                default:
                    return null;
            }
        }

        private static bool IsDirectionalBlendStateType(XAnimationStateType stateType)
        {
            return stateType == XAnimationStateType.Blend2DSimpleDirectional ||
                   stateType == XAnimationStateType.Blend2DFreeformDirectional;
        }

        private static bool TryGetDirectionalBlendSamples(
            XAnimationCompiledState state,
            out IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> samples)
        {
            switch (state)
            {
                case XAnimationCompiledBlend2DSimpleDirectionalState simpleState:
                    samples = simpleState.Samples;
                    return true;
                case XAnimationCompiledBlend2DFreeformDirectionalState freeformState:
                    samples = freeformState.Samples;
                    return true;
                default:
                    samples = null;
                    return false;
            }
        }

        private bool TryGetBlend1DPreviewRange(string parameterName, out float min, out float max)
        {
            min = 0f;
            max = 0f;
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            bool found = false;
            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] is not XAnimationCompiledBlend1DState blendState)
                {
                    continue;
                }

                if (!string.Equals(blendState.Config.parameterName, parameterName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (blendState.Samples.Count == 0)
                {
                    continue;
                }

                float stateMin = blendState.Samples[0].Threshold;
                float stateMax = blendState.Samples[0].Threshold;
                for (int sampleIndex = 1; sampleIndex < blendState.Samples.Count; sampleIndex++)
                {
                    float threshold = blendState.Samples[sampleIndex].Threshold;
                    stateMin = Mathf.Min(stateMin, threshold);
                    stateMax = Mathf.Max(stateMax, threshold);
                }

                if (!found)
                {
                    min = stateMin;
                    max = stateMax;
                    found = true;
                }
                else
                {
                    min = Mathf.Min(min, stateMin);
                    max = Mathf.Max(max, stateMax);
                }
            }

            if (!found)
            {
                return false;
            }

            if (Mathf.Approximately(min, max))
            {
                max = min + 1f;
            }

            return true;
        }

        private bool TryGetDirectionalPreviewRange(string parameterName, out float min, out float max)
        {
            min = 0f;
            max = 0f;
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            bool found = false;
            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (!TryGetDirectionalBlendSamples(state, out IReadOnlyList<XAnimationCompiledBlend2DSimpleDirectionalSample> samples))
                {
                    continue;
                }

                bool matchesX = string.Equals(state.Config.parameterXName, parameterName, StringComparison.Ordinal);
                bool matchesY = string.Equals(state.Config.parameterYName, parameterName, StringComparison.Ordinal);
                if (!matchesX && !matchesY)
                {
                    continue;
                }

                if (samples.Count == 0)
                {
                    continue;
                }

                for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
                {
                    float sampleValue = matchesX ? samples[sampleIndex].Config.positionX : samples[sampleIndex].Config.positionY;
                    if (!found)
                    {
                        min = sampleValue;
                        max = sampleValue;
                        found = true;
                    }
                    else
                    {
                        min = Mathf.Min(min, sampleValue);
                        max = Mathf.Max(max, sampleValue);
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            if (Mathf.Approximately(min, max))
            {
                max = min + 1f;
            }

            return true;
        }

        private VisualElement CreateFloatPreviewParameterRow(
            string parameterName,
            float defaultValue,
            float min,
            float max,
            bool useSlider)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            Label label = new(parameterName);
            label.style.width = 82;
            label.style.flexShrink = 0;
            label.style.color = TextMuted;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);

            FloatField valueField = new()
            {
                value = defaultValue
            };
            valueField.tooltip = "预览参数值，只影响当前 Preview Session，不保存到资源。";
            ConfigureCompactNumberField(valueField);

            if (useSlider)
            {
                Slider slider = new(min, max)
                {
                    value = defaultValue
                };
                slider.tooltip = $"Blend 参数范围来自 samples: [{min:0.###}, {max:0.###}]。";
                slider.style.flexGrow = 1;
                slider.RegisterValueChangedCallback(evt =>
                {
                    valueField.SetValueWithoutNotify(evt.newValue);
                    SetPreviewFloatParameter(parameterName, evt.newValue);
                });
                valueField.RegisterValueChangedCallback(evt =>
                {
                    slider.SetValueWithoutNotify(Mathf.Clamp(evt.newValue, min, max));
                    SetPreviewFloatParameter(parameterName, evt.newValue);
                });
                row.Add(slider);
                row.Add(valueField);
            }
            else
            {
                valueField.style.flexGrow = 1;
                valueField.style.width = StyleKeyword.Auto;
                valueField.style.minWidth = 64;
                valueField.style.maxWidth = StyleKeyword.None;
                valueField.RegisterValueChangedCallback(evt => SetPreviewFloatParameter(parameterName, evt.newValue));
                row.Add(valueField);
            }

            Button zeroButton = new(() =>
            {
                valueField.SetValueWithoutNotify(0f);
                SetPreviewFloatParameter(parameterName, 0f);
            })
            {
                text = "0"
            };
            zeroButton.tooltip = "把这个预览参数重置为 0。";
            ApplyClipIconButtonStyle(zeroButton);
            zeroButton.style.marginLeft = 4;
            row.Add(zeroButton);

            return row;
        }

        private List<XAnimationCompiledParameter> GetFloatParameters()
        {
            List<XAnimationCompiledParameter> parameters = new();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return parameters;
            }

            IReadOnlyList<XAnimationCompiledParameter> compiledParameters = m_Session.CompiledAsset.Parameters;
            for (int i = 0; i < compiledParameters.Count; i++)
            {
                XAnimationCompiledParameter parameter = compiledParameters[i];
                if (parameter.Type == XAnimationParameterType.Float)
                {
                    parameters.Add(parameter);
                }
            }

            return parameters;
        }

        private void SetPreviewFloatParameter(string parameterName, float value)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            if (TrySetPreviewParameter(parameterName, value))
            {
                RefreshPreviewAfterParameterChanged();
            }
        }

        private bool TrySetPreviewParameter(string parameterName, float value)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            try
            {
                m_Session.SetPreviewParameter(parameterName, value);
                SetStatus($"Preview parameter {parameterName} = {value:0.###}。");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
                return false;
            }
        }

        private bool TrySetPreviewParameter(string parameterName, bool value)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            try
            {
                m_Session.SetPreviewParameter(parameterName, value);
                SetStatus($"Preview parameter {parameterName} = {value}。");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
                return false;
            }
        }

        private bool TrySetPreviewParameter(string parameterName, int value)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            try
            {
                m_Session.SetPreviewParameter(parameterName, value);
                SetStatus($"Preview parameter {parameterName} = {value}。");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
                return false;
            }
        }

        private void RefreshPreviewAfterParameterChanged(bool rebuildParameterList = false)
        {
            if (rebuildParameterList)
            {
                RebuildParameterList();
            }

            m_Session?.SyncPreviewFrame();
            RefreshStatePlayingStates();
            RefreshChannelStates();
            RenderPreview();
            Repaint();
        }

        private VisualElement CreateBoolPreviewParameterRow(string parameterName, bool value)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            Label label = new(parameterName);
            label.style.width = 82;
            label.style.flexShrink = 0;
            label.style.color = TextMuted;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);

            Toggle toggle = new("value")
            {
                value = value
            };
            toggle.tooltip = "预览参数值，只影响当前 Preview Session，不保存到资源。";
            toggle.style.flexGrow = 1;
            toggle.RegisterValueChangedCallback(evt => SetPreviewBoolParameter(parameterName, evt.newValue));
            row.Add(toggle);

            return row;
        }

        private void SetPreviewBoolParameter(string parameterName, bool value)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            if (TrySetPreviewParameter(parameterName, value))
            {
                RefreshPreviewAfterParameterChanged();
            }
        }

        private VisualElement CreateIntPreviewParameterRow(string parameterName, int value)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            Label label = new(parameterName);
            label.style.width = 82;
            label.style.flexShrink = 0;
            label.style.color = TextMuted;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(label);

            IntegerField valueField = new("value")
            {
                value = value
            };
            valueField.tooltip = "预览参数值，只影响当前 Preview Session，不保存到资源。";
            valueField.style.flexGrow = 1;
            valueField.RegisterValueChangedCallback(evt => SetPreviewIntParameter(parameterName, evt.newValue));
            row.Add(valueField);

            Button zeroButton = new(() =>
            {
                valueField.SetValueWithoutNotify(0);
                SetPreviewIntParameter(parameterName, 0);
            })
            {
                text = "0"
            };
            zeroButton.tooltip = "把这个预览参数重置为 0。";
            ApplyClipIconButtonStyle(zeroButton);
            zeroButton.style.marginLeft = 4;
            row.Add(zeroButton);

            return row;
        }

        private void SetPreviewIntParameter(string parameterName, int value)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            if (TrySetPreviewParameter(parameterName, value))
            {
                RefreshPreviewAfterParameterChanged();
            }
        }

        private float GetPreviewFloatParameterValue(XAnimationCompiledParameter parameter)
        {
            if (parameter == null)
            {
                return 0f;
            }

            if (m_Session != null && m_Session.TryGetPreviewParameter(parameter.Name, out float value))
            {
                return value;
            }

            return ConvertParameterDefaultToFloat(parameter.Config.defaultValue);
        }

        private bool GetPreviewBoolParameterValue(XAnimationCompiledParameter parameter)
        {
            if (parameter == null)
            {
                return false;
            }

            if (m_Session != null && m_Session.TryGetPreviewParameter(parameter.Name, out bool value))
            {
                return value;
            }

            return ConvertParameterDefaultToBool(parameter.Config.defaultValue);
        }

        private int GetPreviewIntParameterValue(XAnimationCompiledParameter parameter)
        {
            if (parameter == null)
            {
                return 0;
            }

            if (m_Session != null && m_Session.TryGetPreviewParameter(parameter.Name, out int value))
            {
                return value;
            }

            return ConvertParameterDefaultToInt(parameter.Config.defaultValue);
        }

        private VisualElement CreateBlendSampleRow(string stateKey, int sampleIndex, XAnimationBlend1DSampleConfig sample, bool editable)
        {
            VisualElement row = CreateSubBox();
            string sampleClipKey = sample?.clipKey ?? string.Empty;
            string rowKey = BuildBlendSampleRuntimeKey(stateKey, sampleIndex);
            VisualElement weightFill = CreateProgressFill(BlendWeightFillBg);
            row.Add(weightFill);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;
            row.style.position = Position.Relative;
            row.style.overflow = Overflow.Hidden;
            m_BlendSampleRowMap[rowKey] = new RowVisualState
            {
                BaseColor = new Color(0.14f, 0.14f, 0.15f, 1f),
                ProgressFill = weightFill,
            };

            Label indexLabel = new($"#{sampleIndex}");
            indexLabel.style.width = 28;
            indexLabel.style.flexShrink = 0;
            indexLabel.style.color = TextMuted;
            indexLabel.style.fontSize = BodyFontSize;
            indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            indexLabel.style.position = Position.Relative;
            row.Add(indexLabel);

            XAnimationEditorSelectionField clipField = CreateClipSelectionField(string.Empty, sampleClipKey);
            clipField.SetEnabled(editable);
            clipField.style.flexGrow = 1;
            clipField.style.position = Position.Relative;
            clipField.ValueChanged += (previousValue, newValue) => ChangeBlendSampleClipKey(stateKey, sampleIndex, newValue, clipField, previousValue);
            AttachClipKeyPingButton(clipField, sampleClipKey, editable);
            row.Add(clipField);

            Label thresholdLabel = new("threshold");
            thresholdLabel.style.marginLeft = 6;
            thresholdLabel.style.marginRight = 4;
            thresholdLabel.style.flexShrink = 0;
            thresholdLabel.style.color = TextMuted;
            thresholdLabel.style.fontSize = 10;
            thresholdLabel.style.whiteSpace = WhiteSpace.NoWrap;
            thresholdLabel.style.position = Position.Relative;
            row.Add(thresholdLabel);

            FloatField thresholdField = new()
            {
                value = sample?.threshold ?? 0f
            };
            thresholdField.SetEnabled(editable);
            thresholdField.tooltip = "一维 Blend 轴上的采样位置，必须保持严格递增。";
            ConfigureCompactNumberField(thresholdField);
            thresholdField.style.width = 76;
            thresholdField.style.minWidth = 76;
            thresholdField.style.maxWidth = 76;
            thresholdField.style.position = Position.Relative;
            thresholdField.RegisterValueChangedCallback(evt => ChangeBlendSampleThreshold(stateKey, sampleIndex, evt.newValue, thresholdField, evt.previousValue));
            row.Add(thresholdField);

            Button previewButton = new(() => PreviewBlendSample(stateKey, thresholdField.value))
            {
                text = "▶"
            };
            previewButton.tooltip = "预览这个 Blend1D 采样点，并把绑定参数设置到当前 threshold。";
            ApplyClipButtonStyle(previewButton, false);
            previewButton.style.marginLeft = 4;
            previewButton.style.position = Position.Relative;
            row.Add(previewButton);

            Button deleteButton = new(() => DeleteBlendSample(stateKey, sampleIndex))
            {
                text = "⌫"
            };
            deleteButton.tooltip = editable ? "删除这个采样点。" : "Override 资源不能删除采样点。";
            deleteButton.SetEnabled(editable);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;
            deleteButton.style.position = Position.Relative;
            row.Add(deleteButton);

            return row;
        }

        private VisualElement CreateDirectionalBlendSampleRow(
            string stateKey,
            int sampleIndex,
            XAnimationBlend2DSimpleDirectionalSampleConfig sample,
            bool editable)
        {
            VisualElement row = CreateSubBox();
            string sampleClipKey = sample?.clipKey ?? string.Empty;
            string rowKey = BuildBlendSampleRuntimeKey(stateKey, sampleIndex);
            VisualElement weightFill = CreateProgressFill(BlendWeightFillBg);
            row.Add(weightFill);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;
            row.style.position = Position.Relative;
            row.style.overflow = Overflow.Hidden;
            m_BlendSampleRowMap[rowKey] = new RowVisualState
            {
                BaseColor = new Color(0.14f, 0.14f, 0.15f, 1f),
                ProgressFill = weightFill,
            };

            Label indexLabel = new($"#{sampleIndex}");
            indexLabel.style.width = 28;
            indexLabel.style.flexShrink = 0;
            indexLabel.style.color = TextMuted;
            indexLabel.style.fontSize = BodyFontSize;
            indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            indexLabel.style.position = Position.Relative;
            row.Add(indexLabel);

            XAnimationEditorSelectionField clipField = CreateClipSelectionField(string.Empty, sampleClipKey);
            clipField.SetEnabled(editable);
            clipField.style.flexGrow = 1;
            clipField.style.position = Position.Relative;
            clipField.ValueChanged += (previousValue, newValue) =>
                ChangeDirectionalBlendSampleClipKey(stateKey, sampleIndex, newValue, clipField, previousValue);
            AttachClipKeyPingButton(clipField, sampleClipKey, editable);
            row.Add(clipField);

            Label xLabel = new("x");
            xLabel.style.marginLeft = 6;
            xLabel.style.marginRight = 4;
            xLabel.style.flexShrink = 0;
            xLabel.style.color = TextMuted;
            xLabel.style.fontSize = 10;
            xLabel.style.position = Position.Relative;
            row.Add(xLabel);

            FloatField xField = new() { value = sample?.positionX ?? 0f };
            xField.SetEnabled(editable);
            ConfigureCompactNumberField(xField);
            xField.style.width = 60;
            xField.style.minWidth = 60;
            xField.style.maxWidth = 60;
            xField.style.position = Position.Relative;
            xField.RegisterValueChangedCallback(evt =>
                ChangeDirectionalBlendSamplePosition(
                    stateKey,
                    sampleIndex,
                    evt.newValue,
                    sample?.positionY ?? 0f,
                    xField,
                    evt.previousValue,
                    sample?.positionY ?? 0f,
                    true));
            row.Add(xField);

            Label yLabel = new("y");
            yLabel.style.marginLeft = 6;
            yLabel.style.marginRight = 4;
            yLabel.style.flexShrink = 0;
            yLabel.style.color = TextMuted;
            yLabel.style.fontSize = 10;
            yLabel.style.position = Position.Relative;
            row.Add(yLabel);

            FloatField yField = new() { value = sample?.positionY ?? 0f };
            yField.SetEnabled(editable);
            ConfigureCompactNumberField(yField);
            yField.style.width = 60;
            yField.style.minWidth = 60;
            yField.style.maxWidth = 60;
            yField.style.position = Position.Relative;
            yField.RegisterValueChangedCallback(evt =>
                ChangeDirectionalBlendSamplePosition(
                    stateKey,
                    sampleIndex,
                    sample?.positionX ?? 0f,
                    evt.newValue,
                    yField,
                    sample?.positionX ?? 0f,
                    evt.previousValue,
                    false));
            row.Add(yField);

            Button previewButton = new(() => PreviewDirectionalBlendSample(stateKey, xField.value, yField.value))
            {
                text = "▶"
            };
            previewButton.tooltip = "预览这个二维采样点，并把绑定参数设置到当前 (x, y)。";
            ApplyClipButtonStyle(previewButton, false);
            previewButton.style.marginLeft = 4;
            previewButton.style.position = Position.Relative;
            row.Add(previewButton);

            Button deleteButton = new(() => DeleteDirectionalBlendSample(stateKey, sampleIndex))
            {
                text = "⌫"
            };
            deleteButton.tooltip = editable ? "删除这个采样点。" : "Override 资源不能删除采样点。";
            deleteButton.SetEnabled(editable);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;
            deleteButton.style.position = Position.Relative;
            row.Add(deleteButton);

            return row;
        }

        private DropdownField CreateChannelDropdown(string label, string currentValue)
        {
            List<string> choices = new();
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    choices.Add(channels[i].Name);
                }
            }

            EnsureDropdownChoice(choices, currentValue);
            DropdownField field = new(label, choices, Mathf.Max(0, choices.IndexOf(currentValue ?? string.Empty)));
            ApplyDropdownFieldStyle(field);
            return field;
        }

        private void RefreshPlayTargetChannelChoices()
        {
            if (m_PlayTargetChannelField == null)
            {
                return;
            }

            List<string> choices = new();
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
                for (int i = 0; i < channels.Count; i++)
                {
                    choices.Add(channels[i].Name);
                }
            }

            string selected = !string.IsNullOrWhiteSpace(m_PlayTargetChannelName) && choices.Contains(m_PlayTargetChannelName)
                ? m_PlayTargetChannelName
                : choices.Count > 0
                    ? choices[0]
                    : string.Empty;

            m_PlayTargetChannelField.choices = choices;
            m_PlayTargetChannelField.SetValueWithoutNotify(selected);
            m_PlayTargetChannelField.SetEnabled(choices.Count > 0);
            m_PlayTargetChannelName = selected;
        }

        private XAnimationTransitionOptions BuildPreviewTransitionOptions()
        {
            if (!m_ApplyTransitionRequestOverrides)
            {
                return null;
            }

            return new XAnimationTransitionOptions
            {
                fadeIn = Mathf.Max(0f, m_PlayFadeInOverride),
                fadeOut = Mathf.Max(0f, m_PlayFadeOutOverride),
                priority = m_PlayPriorityOverride,
                interruptible = m_PlayInterruptibleOverride,
                enterTime = Mathf.Clamp01(m_PlayEnterTimeOverride),
            };
        }

        private XAnimationEditorSelectionField CreateClipSelectionField(string label, string currentValue)
        {
            XAnimationEditorSelectionField field = new(label, currentValue, _ => { });
            void ShowMenu(XAnimationEditorSelectionField target)
            {
                List<ClipSelectionItem> items = CollectSelectableClips();
                List<SearchableSelectionItem> entries = BuildClipSelectionEntries(items);
                SearchableSelectionWindow.Show(
                    GetSelectionActivatorRect(target),
                    "Select Clip",
                    target.value,
                    entries,
                    selected => target.value = selected);
            }

            field = new XAnimationEditorSelectionField(label, currentValue, ShowMenu);
            return field;
        }

        private void AttachClipKeyPingButton(XAnimationEditorSelectionField clipField, string clipKey, bool enabled)
        {
            if (clipField == null)
            {
                return;
            }

            Button clipItemButton = CreateEmbeddedDropdownButton(
                "↗",
                "定位到 Clips 面板里当前 clipKey 对应的条目。",
                enabled && HasClipAsset(clipField?.value ?? clipKey),
                () => FocusClipInInspector(clipField?.value ?? clipKey),
                marginLeft: 4,
                marginRight: 2);

            Button pingButton = CreateEmbeddedDropdownButton(
                "◎",
                "定位当前 clipKey 对应的 AnimationClip 资源。",
                enabled && HasClipAsset(clipField?.value ?? clipKey),
                () => PingClipAsset(clipField?.value ?? clipKey),
                marginLeft: 2,
                marginRight: 4);

            clipField.ValueChanged += (_, newValue) =>
            {
                bool canLocate = enabled && HasClipAsset(newValue);
                clipItemButton.SetEnabled(canLocate);
                pingButton.SetEnabled(canLocate);
            };

            clipField.AddTrailingElement(clipItemButton);
            clipField.AddTrailingElement(pingButton);
        }

        private void AttachStatePingButton(XAnimationEditorSelectionField stateField, string stateKey, bool enabled)
        {
            if (stateField == null)
            {
                return;
            }

            Button stateItemButton = CreateEmbeddedDropdownButton(
                "↗",
                "定位到 States 面板里当前 stateKey 对应的条目。",
                enabled && HasState(stateField?.value ?? stateKey),
                () => FocusStateInInspector(stateField?.value ?? stateKey),
                marginLeft: 4,
                marginRight: 4);

            stateField.ValueChanged += (_, newValue) =>
            {
                stateItemButton.SetEnabled(enabled && HasState(newValue));
            };

            stateField.AddTrailingElement(stateItemButton);
        }

        private void AttachDropdownInspectorButton(
            DropdownField dropdown,
            Func<string> currentValueGetter,
            Func<bool> canLocate,
            Action onLocate,
            string tooltip)
        {
            if (dropdown == null)
            {
                return;
            }

            Button locateButton = CreateEmbeddedDropdownButton(
                "↗",
                tooltip,
                canLocate(),
                onLocate,
                marginLeft: 4,
                marginRight: 4);

            dropdown.RegisterValueChangedCallback(_ =>
            {
                locateButton.SetEnabled(canLocate());
            });

            AttachDropdownButtons(dropdown, locateButton);
        }

        private static Button CreateEmbeddedDropdownButton(
            string text,
            string tooltip,
            bool enabled,
            Action onClick,
            int marginLeft,
            int marginRight)
        {
            Button button = new(onClick)
            {
                text = text
            };
            button.tooltip = tooltip;
            button.SetEnabled(enabled);
            ApplyClipIconButtonStyle(button);
            button.style.marginLeft = marginLeft;
            button.style.marginRight = marginRight;
            button.style.flexShrink = 0;
            return button;
        }

        private static void AttachDropdownButtons(DropdownField dropdown, params Button[] buttons)
        {
            if (dropdown == null || buttons == null || buttons.Length == 0)
            {
                return;
            }

            void TryAttach()
            {
                VisualElement input = dropdown.Q<VisualElement>(className: "unity-base-field__input");
                if (input == null)
                {
                    return;
                }

                VisualElement arrow = input.Q<VisualElement>(className: "unity-base-popup-field__arrow");
                if (arrow == null)
                {
                    return;
                }

                for (int i = 0; i < buttons.Length; i++)
                {
                    Button button = buttons[i];
                    if (button == null || button.parent != null)
                    {
                        continue;
                    }

                    int arrowIndex = input.IndexOf(arrow);
                    input.Insert(Mathf.Max(0, arrowIndex), button);
                }
            }

            TryAttach();
            dropdown.RegisterCallback<AttachToPanelEvent>(_ => TryAttach());
        }

        private bool HasClipAsset(string clipKey)
        {
            return TryGetClipAsset(clipKey, out _);
        }

        private void PingClipAsset(string clipKey)
        {
            if (!TryGetClipAsset(clipKey, out AnimationClip clip))
            {
                SetStatus(string.IsNullOrWhiteSpace(clipKey)
                    ? "当前没有可定位的 clipKey。"
                    : $"没有找到 clipKey '{clipKey}' 对应的 AnimationClip 资源。", true);
                return;
            }

            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            SetStatus($"已定位动画资源: {clip.name}。");
        }

        private bool TryGetClipAsset(string clipKey, out AnimationClip clip)
        {
            clip = null;
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(clipKey))
            {
                return false;
            }

            try
            {
                clip = m_Session.CompiledAsset.GetClip(clipKey).Clip;
            }
            catch (Exception)
            {
                clip = null;
            }

            return clip != null;
        }

        private DropdownField CreateStateKeyDropdown(string label, string currentValue, string excludeStateKey = null, bool includeNone = false)
        {
            const string noneChoice = "None";
            List<string> choices = new();
            if (includeNone)
            {
                choices.Add(noneChoice);
            }

            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
                for (int i = 0; i < states.Count; i++)
                {
                    string stateKey = states[i].Key;
                    if (string.Equals(stateKey, excludeStateKey, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    choices.Add(stateKey);
                }
            }

            string selected = string.IsNullOrWhiteSpace(currentValue) && includeNone ? noneChoice : currentValue ?? string.Empty;
            EnsureDropdownChoice(choices, selected);
            DropdownField field = new(label, choices, Mathf.Max(0, choices.IndexOf(selected)));
            ApplyDropdownFieldStyle(field);
            return field;
        }

        private DropdownField CreateAutoTransitionPreStateDropdown(string label, string currentValue)
        {
            List<string> choices = new();
            HashSet<string> occupiedPreStates = new(StringComparer.Ordinal);
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledAutoTransition> autoTransitions = m_Session.CompiledAsset.AutoTransitions;
                for (int i = 0; i < autoTransitions.Count; i++)
                {
                    XAnimationCompiledAutoTransition transition = autoTransitions[i];
                    if (transition == null ||
                        string.IsNullOrWhiteSpace(transition.PreStateKey) ||
                        string.Equals(transition.PreStateKey, currentValue, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    occupiedPreStates.Add(transition.PreStateKey);
                }

                IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
                for (int i = 0; i < states.Count; i++)
                {
                    string stateKey = states[i].Key;
                    if (!states[i].Config.loop &&
                        !occupiedPreStates.Contains(stateKey))
                    {
                        choices.Add(stateKey);
                    }
                }
            }

            EnsureDropdownChoice(choices, currentValue);
            DropdownField field = new(label, choices, Mathf.Max(0, choices.IndexOf(currentValue ?? string.Empty)));
            ApplyDropdownFieldStyle(field);
            return field;
        }

        private bool HasState(string stateKey)
        {
            return m_Session != null &&
                   m_Session.IsLoaded &&
                   !string.IsNullOrWhiteSpace(stateKey) &&
                   m_Session.CompiledAsset.TryGetStateIndex(stateKey, out _);
        }

        private bool HasChannel(string channelName)
        {
            return m_Session != null &&
                   m_Session.IsLoaded &&
                   !string.IsNullOrWhiteSpace(channelName) &&
                   m_Session.CompiledAsset.TryGetChannelIndex(channelName, out _);
        }

        private DropdownField CreateFloatParameterDropdown(string label, string currentValue)
        {
            List<string> choices = new();
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledParameter> parameters = m_Session.CompiledAsset.Parameters;
                for (int i = 0; i < parameters.Count; i++)
                {
                    XAnimationCompiledParameter parameter = parameters[i];
                    if (parameter.Type == XAnimationParameterType.Float)
                    {
                        choices.Add(parameter.Name);
                    }
                }
            }

            EnsureDropdownChoice(choices, currentValue);
            DropdownField field = new(label, choices, Mathf.Max(0, choices.IndexOf(currentValue ?? string.Empty)));
            ApplyDropdownFieldStyle(field);
            return field;
        }

        private static void EnsureDropdownChoice(List<string> choices, string currentValue)
        {
            currentValue ??= string.Empty;
            if (choices.Count == 0 || !choices.Contains(currentValue))
            {
                choices.Insert(0, currentValue);
            }
        }

        private static string NormalizeOptionalStateDropdownValue(string value)
        {
            return string.Equals(value, "None", StringComparison.Ordinal) ? string.Empty : value ?? string.Empty;
        }

        private static string NormalizeStateEditorGroupName(string groupName)
        {
            groupName = groupName?.Trim();
            return string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName;
        }

        private static string NormalizeClipEditorGroupName(string groupName)
        {
            groupName = groupName?.Trim();
            return string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName;
        }

        private static string BuildStateGroupKey(string channelName, string groupName)
        {
            return $"{channelName ?? string.Empty}::{NormalizeStateEditorGroupName(groupName)}";
        }

        private static string BuildClipGroupKey(string groupName)
        {
            return NormalizeClipEditorGroupName(groupName);
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

        private void ExpandStateGroupForState(string stateKey)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            XAnimationCompiledState state = m_Session.CompiledAsset.GetState(stateKey);
            string groupName = NormalizeStateEditorGroupName(state?.Config?.editorGroupName);
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                SetStateGroupCollapsed(BuildStateGroupKey(state.Config.channelName, groupName), false);
            }
        }

        private bool IsClipGroupCollapsed(string groupKey)
        {
            return !string.IsNullOrWhiteSpace(groupKey) && m_CollapsedClipGroupKeys.Contains(groupKey);
        }

        private void SetClipGroupCollapsed(string groupKey, bool collapsed)
        {
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                return;
            }

            if (collapsed)
            {
                m_CollapsedClipGroupKeys.Add(groupKey);
            }
            else
            {
                m_CollapsedClipGroupKeys.Remove(groupKey);
            }
        }

        private void ExpandClipGroupForClip(string clipKey)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(clipKey))
            {
                return;
            }

            XAnimationCompiledClip clip = m_Session.CompiledAsset.GetClip(clipKey);
            string groupName = NormalizeClipEditorGroupName(clip?.Config?.editorGroupName);
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                SetClipGroupCollapsed(BuildClipGroupKey(groupName), false);
            }
        }

        private bool HasClipGroup(string groupName)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            groupName = NormalizeClipEditorGroupName(groupName);
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            for (int i = 0; i < clips.Count; i++)
            {
                XAnimationCompiledClip clip = clips[i];
                if (clip != null &&
                    string.Equals(NormalizeClipEditorGroupName(clip.Config.editorGroupName), groupName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasStateGroup(string channelName, string groupName)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return false;
            }

            groupName = NormalizeStateEditorGroupName(groupName);
            if (string.IsNullOrWhiteSpace(channelName) || string.IsNullOrWhiteSpace(groupName))
            {
                return false;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (state != null &&
                    string.Equals(state.Config.channelName, channelName, StringComparison.Ordinal) &&
                    string.Equals(NormalizeStateEditorGroupName(state.Config.editorGroupName), groupName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private List<StateSelectionItem> CollectSelectableStates(string excludeStateKey = null, bool includeNone = false)
        {
            List<StateSelectionItem> items = new();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return items;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (state == null || string.Equals(state.Key, excludeStateKey, StringComparison.Ordinal))
                {
                    continue;
                }

                items.Add(new StateSelectionItem(state.Key, state.Config.channelName, state.Config.editorGroupName));
            }

            return items;
        }

        private List<ClipSelectionItem> CollectSelectableClips()
        {
            List<ClipSelectionItem> items = new();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return items;
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            for (int i = 0; i < clips.Count; i++)
            {
                XAnimationCompiledClip clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                items.Add(new ClipSelectionItem(clip.Key, clip.Config.editorGroupName));
            }

            return items;
        }

        private static List<SearchableSelectionItem> BuildStateSelectionEntries(
            List<StateSelectionItem> items,
            bool includeNone)
        {
            List<SearchableSelectionItem> entries = new();
            if (includeNone)
            {
                entries.Add(new SearchableSelectionItem(string.Empty, "None", "Clear selection", "none clear empty"));
            }

            if (items == null)
            {
                return entries;
            }

            for (int i = 0; i < items.Count; i++)
            {
                StateSelectionItem item = items[i];
                string title = item.IsGrouped
                    ? $"{item.ChannelName} - {item.GroupName} / {item.StateKey}"
                    : $"{item.ChannelName} - {item.StateKey}";
                string detail = item.IsGrouped
                    ? $"group={item.GroupName}"
                    : string.Empty;
                string searchText = $"{item.StateKey} {item.ChannelName} {item.GroupName} {title}";
                string groupKey = item.IsGrouped ? $"{item.ChannelName} - {item.GroupName}" : string.Empty;
                entries.Add(new SearchableSelectionItem(item.StateKey, title, detail, searchText, groupKey));
            }

            return entries;
        }

        private static List<SearchableSelectionItem> BuildClipSelectionEntries(List<ClipSelectionItem> items)
        {
            List<SearchableSelectionItem> entries = new();
            if (items == null)
            {
                return entries;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ClipSelectionItem item = items[i];
                string title = item.IsGrouped
                    ? $"{item.GroupName} / {item.ClipKey}"
                    : item.ClipKey;
                string detail = item.IsGrouped
                    ? $"group={item.GroupName}"
                    : "ungrouped";
                string searchText = $"{item.ClipKey} {item.GroupName} {title}";
                entries.Add(new SearchableSelectionItem(item.ClipKey, title, detail, searchText, item.GroupName));
            }

            return entries;
        }

        private static Rect GetSelectionActivatorRect(VisualElement element)
        {
            Rect world = element.worldBound;
            return GUIUtility.GUIToScreenRect(new Rect(world.xMin, world.yMin, world.width, world.height));
        }

        private XAnimationEditorSelectionField CreateStateSelectionField(string label, string currentValue, string excludeStateKey = null, bool includeNone = false)
        {
            XAnimationEditorSelectionField field = new(label, string.IsNullOrWhiteSpace(currentValue) && includeNone ? string.Empty : currentValue, _ => { });
            void ShowMenu(XAnimationEditorSelectionField target)
            {
                List<StateSelectionItem> items = CollectSelectableStates(excludeStateKey, includeNone);
                List<SearchableSelectionItem> entries = BuildStateSelectionEntries(items, includeNone);
                SearchableSelectionWindow.Show(
                    GetSelectionActivatorRect(target),
                    "Select State",
                    target.value,
                    entries,
                    selected => target.value = selected);
            }

            field = new XAnimationEditorSelectionField(label, string.IsNullOrWhiteSpace(currentValue) && includeNone ? string.Empty : currentValue, ShowMenu);
            AttachStatePingButton(field, currentValue, enabled: true);
            return field;
        }

    }
}
#endif
