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
        private void RebuildClipList()
        {
            m_ClipListView.Clear();
            m_ClipLabelMap.Clear();
            m_ClipGroupLabelMap.Clear();
            m_ClipRowMap.Clear();
            m_ClipGroupRowMap.Clear();
            m_ClipVisualStateMap.Clear();
            m_ClipButtonMap.Clear();
            m_CueRowMap.Clear();
            SetAddClipButtonEnabled(m_Session != null && m_Session.IsLoaded && !m_Session.IsOverrideAsset);
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            if (clips.Count == 0)
            {
                Label emptyLabel = new("No clips");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_ClipListView.Add(emptyLabel);
                TryBeginPendingRename();
                RefreshSearchIndex();
                return;
            }

            List<ClipGroupBucket> buckets = new();
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                XAnimationCompiledClip clip = clips[clipIndex];
                string groupName = NormalizeClipEditorGroupName(clip?.Config?.editorGroupName);
                ClipGroupBucket bucket = FindClipGroupBucket(buckets, groupName);
                if (bucket == null)
                {
                    bucket = new ClipGroupBucket(groupName);
                    buckets.Add(bucket);
                }

                bucket.Clips.Add(clip);
            }

            int rowIndex = 0;
            bool hasUngroupedBucket = false;
            for (int i = 0; i < buckets.Count; i++)
            {
                ClipGroupBucket bucket = buckets[i];
                if (bucket == null)
                {
                    continue;
                }

                if (bucket.IsUngrouped)
                {
                    hasUngroupedBucket = true;
                    VisualElement ungroupedContainer = new VisualElement();
                    ungroupedContainer.style.marginBottom = 3;
                    RegisterClipGroupDropTarget(ungroupedContainer, ungroupedContainer, string.Empty);
                    for (int clipIndex = 0; clipIndex < bucket.Clips.Count; clipIndex++)
                    {
                        XAnimationCompiledClip clip = bucket.Clips[clipIndex];
                        VisualElement row = CreateClipRow(clip, rowIndex++);
                        RegisterClipRowDropTarget(row, clip.Key, string.Empty);
                        ungroupedContainer.Add(row);
                    }

                    m_ClipListView.Add(ungroupedContainer);

                    continue;
                }

                m_ClipListView.Add(CreateClipEditorGroup(bucket, ref rowIndex));
            }

            if (!hasUngroupedBucket)
            {
                VisualElement ungroupedDropZone = new VisualElement();
                ungroupedDropZone.style.minHeight = 18;
                ungroupedDropZone.style.marginBottom = 3;
                ungroupedDropZone.style.borderTopWidth = 1;
                ungroupedDropZone.style.borderBottomWidth = 1;
                ungroupedDropZone.style.borderLeftWidth = 1;
                ungroupedDropZone.style.borderRightWidth = 1;
                ungroupedDropZone.style.borderTopColor = SectionDivider;
                ungroupedDropZone.style.borderBottomColor = SectionDivider;
                ungroupedDropZone.style.borderLeftColor = SectionDivider;
                ungroupedDropZone.style.borderRightColor = SectionDivider;
                Label dropLabel = new("Drop Here To Ungroup");
                dropLabel.style.color = TextMuted;
                dropLabel.style.fontSize = 10;
                dropLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                dropLabel.style.flexGrow = 1;
                ungroupedDropZone.Add(dropLabel);
                RegisterClipGroupDropTarget(ungroupedDropZone, ungroupedDropZone, string.Empty);
                m_ClipListView.Add(ungroupedDropZone);
            }

            TryBeginPendingRename();
            RefreshSearchIndex();
        }

        private void RebuildStateList()
        {
            m_StateListView.Clear();
            m_StateLabelMap.Clear();
            m_StateGroupLabelMap.Clear();
            m_StateRowMap.Clear();
            m_StateEditorMap.Clear();
            m_StateGroupRowMap.Clear();
            m_StateVisualStateMap.Clear();
            m_BlendSampleRowMap.Clear();
            m_StateButtonMap.Clear();
            m_StateChannelMap.Clear();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                RefreshGlobalBlendGraph();
                return;
            }

            if (m_ExpandedStateKeys.Count > 1)
            {
                TryGetExpandedStateKey(out string expandedStateKey);
                m_ExpandedStateKeys.Clear();
                if (!string.IsNullOrWhiteSpace(expandedStateKey))
                {
                    m_ExpandedStateKeys.Add(expandedStateKey);
                }
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            if (states.Count == 0)
            {
                Label emptyLabel = new("No states");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_StateListView.Add(emptyLabel);
                return;
            }

            IReadOnlyList<XAnimationCompiledChannel> channels = m_Session.CompiledAsset.Channels;
            Dictionary<string, List<StateGroupBucket>> statesByChannel = new(StringComparer.Ordinal);
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                string channelName = state.Config.channelName;
                if (!statesByChannel.TryGetValue(channelName, out List<StateGroupBucket> channelStates))
                {
                    channelStates = new List<StateGroupBucket>();
                    statesByChannel.Add(channelName, channelStates);
                }

                string groupName = NormalizeStateEditorGroupName(state.Config.editorGroupName);
                StateGroupBucket bucket = FindStateGroupBucket(channelStates, groupName);
                if (bucket == null)
                {
                    bucket = new StateGroupBucket(channelName, groupName);
                    channelStates.Add(bucket);
                }

                bucket.States.Add(state);
            }

            for (int i = 0; i < channels.Count; i++)
            {
                XAnimationCompiledChannel channel = channels[i];
                if (!statesByChannel.TryGetValue(channel.Name, out List<StateGroupBucket> channelStates))
                {
                    channelStates = new List<StateGroupBucket>();
                }

                VisualElement group = CreateStateChannelGroup(channel, channelStates);
                m_StateListView.Add(group);
            }

            TryBeginPendingRename();
            RebuildAutoTransitionEditor();
            RebuildDefaultTransitionsEditor();
            RefreshSearchIndex();
            RefreshGlobalBlendGraph();
        }

        private void RebuildParameterList()
        {
            m_ParameterListView.Clear();
            m_MainParameterPreviewView?.Clear();
            m_ParameterLabelMap.Clear();
            m_ParameterRowMap.Clear();
            SetAddParameterButtonEnabled(m_Session != null && m_Session.IsLoaded && !m_Session.IsOverrideAsset);

            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            IReadOnlyList<XAnimationCompiledParameter> parameters = m_Session.CompiledAsset.Parameters;
            if (parameters.Count == 0)
            {
                Label emptyLabel = new("No parameters");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_ParameterListView.Add(emptyLabel);
                AddEmptyParameterPreviewLabel();
                return;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                m_ParameterListView.Add(CreateParameterRow(parameters[i], i));
            }

            RebuildMainParameterPreview(parameters);
            TryBeginPendingRename();
            RefreshSearchIndex();
        }

        private void RebuildAutoTransitionEditor()
        {
            if (m_AutoTransitionEditorView == null)
            {
                return;
            }

            m_AutoTransitionEditorView.Clear();
            m_AutoTransitionRowMap.Clear();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                SetAutoTransitionButtonsEnabled(false);
                RefreshSearchIndex();
                return;
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            if (states.Count == 0)
            {
                SetAutoTransitionButtonsEnabled(false);
                Label emptyLabel = new("No states");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_AutoTransitionEditorView.Add(emptyLabel);
                RefreshSearchIndex();
                return;
            }

            IReadOnlyList<XAnimationCompiledAutoTransition> autoTransitions = m_Session.CompiledAsset.AutoTransitions;
            bool hasTransitions = autoTransitions.Count > 0;
            if (!hasTransitions)
            {
                m_SelectedAutoTransitionStateKey = string.Empty;
                SetAutoTransitionButtonsEnabled(CanAddAutoTransition());
                Label emptyLabel = new("No auto transitions");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_AutoTransitionEditorView.Add(emptyLabel);
                RefreshSearchIndex();
                return;
            }

            if (string.IsNullOrWhiteSpace(m_SelectedAutoTransitionStateKey) ||
                !HasAutoTransition(m_SelectedAutoTransitionStateKey))
            {
                m_SelectedAutoTransitionStateKey = GetDefaultAutoTransitionStateKey(states);
            }

            if (m_CollapsedAutoTransitionKeys.Count > 1)
            {
                string expandedPreStateKey = null;
                foreach (XAnimationCompiledAutoTransition transition in autoTransitions)
                {
                    if (transition == null)
                    {
                        continue;
                    }

                    if (m_CollapsedAutoTransitionKeys.Contains(transition.PreStateKey))
                    {
                        expandedPreStateKey = transition.PreStateKey;
                        break;
                    }
                }

                m_CollapsedAutoTransitionKeys.Clear();
                if (!string.IsNullOrWhiteSpace(expandedPreStateKey))
                {
                    m_CollapsedAutoTransitionKeys.Add(expandedPreStateKey);
                }
            }

            SetAutoTransitionButtonsEnabled(CanAddAutoTransition());

            int renderedCount = 0;
            for (int i = 0; i < autoTransitions.Count; i++)
            {
                XAnimationCompiledAutoTransition transition = autoTransitions[i];
                if (transition == null)
                {
                    continue;
                }

                m_AutoTransitionEditorView.Add(CreateAutoTransitionEditor(transition));
                renderedCount++;
            }

            if (renderedCount == 0)
            {
                m_SelectedAutoTransitionStateKey = string.Empty;
                Label emptyLabel = new("No auto transitions");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_AutoTransitionEditorView.Add(emptyLabel);
            }

            RefreshSearchIndex();
        }

        private void ScheduleAutoTransitionEditorRebuild()
        {
            if (rootVisualElement == null)
            {
                RebuildAutoTransitionEditor();
                return;
            }

            rootVisualElement.schedule.Execute(RebuildAutoTransitionEditor).StartingIn(0);
        }

        private void RebuildDefaultTransitionsEditor()
        {
            if (m_DefaultTransitionsEditorView == null)
            {
                return;
            }

            m_DefaultTransitionsEditorView.Clear();
            m_DefaultTransitionRowMap.Clear();
            if (m_Session == null || !m_Session.IsLoaded)
            {
                SetDefaultTransitionButtonsEnabled(false);
                RefreshSearchIndex();
                return;
            }

            bool hasEnoughStates = m_Session.CompiledAsset.States.Count >= 2;
            SetDefaultTransitionButtonsEnabled(!m_Session.IsOverrideAsset && hasEnoughStates);

            IReadOnlyList<XAnimationCompiledDefaultTransition> defaultTransitions = m_Session.CompiledAsset.DefaultTransitions;
            if (defaultTransitions.Count == 0)
            {
                m_SelectedDefaultTransitionIndex = -1;
                Label emptyLabel = new(hasEnoughStates ? "No default transitions" : "Default transitions require at least two states");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                m_DefaultTransitionsEditorView.Add(emptyLabel);
                RefreshSearchIndex();
                return;
            }

            if (m_SelectedDefaultTransitionIndex < 0 || m_SelectedDefaultTransitionIndex >= defaultTransitions.Count)
            {
                m_SelectedDefaultTransitionIndex = 0;
            }

            int collapsedCount = m_CollapsedDefaultTransitionIndices.Count;
            if (defaultTransitions.Count > 0 && collapsedCount < defaultTransitions.Count - 1)
            {
                int expandedIndex = -1;
                for (int i = 0; i < defaultTransitions.Count; i++)
                {
                    if (!m_CollapsedDefaultTransitionIndices.Contains(i))
                    {
                        expandedIndex = i;
                        break;
                    }
                }

                m_CollapsedDefaultTransitionIndices.Clear();
                for (int i = 0; i < defaultTransitions.Count; i++)
                {
                    if (i != expandedIndex)
                    {
                        m_CollapsedDefaultTransitionIndices.Add(i);
                    }
                }
            }

            for (int i = 0; i < defaultTransitions.Count; i++)
            {
                XAnimationCompiledDefaultTransition transition = defaultTransitions[i];
                if (transition != null)
                {
                    m_DefaultTransitionsEditorView.Add(CreateDefaultTransitionEditor(i, transition.Config));
                }
            }

            RefreshSearchIndex();
        }

        private void ScheduleDefaultTransitionsEditorRebuild()
        {
            if (rootVisualElement == null)
            {
                RebuildDefaultTransitionsEditor();
                return;
            }

            rootVisualElement.schedule.Execute(RebuildDefaultTransitionsEditor).StartingIn(0);
        }

        private string GetDefaultAutoTransitionStateKey(IReadOnlyList<XAnimationCompiledState> states)
        {
            if (m_Session != null && m_Session.IsLoaded)
            {
                IReadOnlyList<XAnimationCompiledAutoTransition> autoTransitions = m_Session.CompiledAsset.AutoTransitions;
                for (int i = 0; i < autoTransitions.Count; i++)
                {
                    XAnimationCompiledAutoTransition transition = autoTransitions[i];
                    if (transition != null && HasState(transition.PreStateKey))
                    {
                        return transition.PreStateKey;
                    }
                }
            }

            return states.Count > 0 ? states[0].Key : string.Empty;
        }

        private bool HasAutoTransition(string preStateKey)
        {
            return m_Session != null &&
                   m_Session.IsLoaded &&
                   !string.IsNullOrWhiteSpace(preStateKey) &&
                   m_Session.CompiledAsset.TryGetAutoTransition(preStateKey, out _);
        }

        private bool CanAddAutoTransition()
        {
            if (m_Session == null || !m_Session.IsLoaded || m_Session.IsOverrideAsset)
            {
                return false;
            }

            int eligibleStateCount = 0;
            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                if (!states[i].Config.loop)
                {
                    eligibleStateCount++;
                }
            }

            return m_Session.CompiledAsset.AutoTransitions.Count < eligibleStateCount;
        }

        private VisualElement CreateParameterRow(XAnimationCompiledParameter parameter, int rowIndex)
        {
            XAnimationParameterConfig config = parameter.Config;
            VisualElement container = new VisualElement();
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

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            container.Add(row);

            string parameterName = parameter.Name;
            m_ParameterRowMap[parameterName] = container;
            EditableLabel label = new(parameterName);
            ConfigureEditableNameLabel(label, 112f);
            label.tooltip = "右键 Rename 编辑参数名。";
            label.SetEditable(true, EditableLabelEditTrigger.ContextMenu);
            label.EditStarted += BeginNameEdit;
            label.EditEnded += EndNameEdit;
            label.ValueCommitted += (_, newValue) => RenameParameter(parameterName, newValue, label);
            m_ParameterLabelMap[parameterName] = label;
            row.Add(label);

            List<string> typeNames = new(Enum.GetNames(typeof(XAnimationParameterType)));
            DropdownField typeField = new(
                typeNames,
                Mathf.Max(0, typeNames.IndexOf(config.type.ToString())));
            ApplyDropdownFieldStyle(typeField);
            typeField.tooltip = "参数类型。Blend1D 和 2D directional blend 只能绑定 Float 参数。";
            typeField.style.width = 88;
            typeField.style.marginLeft = 4;
            typeField.RegisterValueChangedCallback(evt => ChangeParameterType(parameterName, evt.newValue, evt.previousValue, typeField));
            row.Add(typeField);

            VisualElement valueField = CreateParameterDefaultValueField(parameterName, config);
            valueField.style.flexGrow = 1;
            valueField.style.marginLeft = 6;
            row.Add(valueField);

            Button deleteButton = new(() => DeleteParameter(parameterName))
            {
                text = "⌫"
            };
            deleteButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能删除 parameter。"
                : "删除这个 parameter。";
            deleteButton.SetEnabled(m_Session != null && !m_Session.IsOverrideAsset);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.marginLeft = 4;
            row.Add(deleteButton);

            return container;
        }

        private void RebuildMainParameterPreview(IReadOnlyList<XAnimationCompiledParameter> parameters)
        {
            if (m_MainParameterPreviewView == null)
            {
                return;
            }

            m_MainParameterPreviewView.Clear();
            bool hasPreviewControl = false;
            for (int i = 0; i < parameters.Count; i++)
            {
                VisualElement previewEditor = CreateParameterPreviewEditor(parameters[i]);
                if (previewEditor == null)
                {
                    continue;
                }

                hasPreviewControl = true;
                m_MainParameterPreviewView.Add(previewEditor);
            }

            if (!hasPreviewControl)
            {
                AddEmptyParameterPreviewLabel();
            }
        }

        private void AddEmptyParameterPreviewLabel()
        {
            if (m_MainParameterPreviewView == null)
            {
                return;
            }

            Label emptyLabel = new("No preview parameters");
            emptyLabel.style.color = TextMuted;
            emptyLabel.style.fontSize = BodyFontSize;
            emptyLabel.style.marginLeft = 4;
            m_MainParameterPreviewView.Add(emptyLabel);
        }

        private VisualElement CreateParameterDefaultValueField(string parameterName, XAnimationParameterConfig config)
        {
            switch (config.type)
            {
                case XAnimationParameterType.Float:
                {
                    FloatField field = new("default")
                    {
                        value = ConvertParameterDefaultToFloat(config.defaultValue)
                    };
                    field.tooltip = "Float 参数默认值，会保存到资源。";
                    field.RegisterValueChangedCallback(evt => ChangeParameterDefaultValue(parameterName, evt.newValue));
                    return field;
                }
                case XAnimationParameterType.Bool:
                {
                    Toggle toggle = new("default")
                    {
                        value = ConvertParameterDefaultToBool(config.defaultValue)
                    };
                    toggle.tooltip = "Bool 参数默认值，会保存到资源。";
                    toggle.RegisterValueChangedCallback(evt => ChangeParameterDefaultValue(parameterName, evt.newValue));
                    return toggle;
                }
                case XAnimationParameterType.Int:
                {
                    IntegerField field = new("default")
                    {
                        value = ConvertParameterDefaultToInt(config.defaultValue)
                    };
                    field.tooltip = "Int 参数默认值，会保存到资源。";
                    field.RegisterValueChangedCallback(evt => ChangeParameterDefaultValue(parameterName, evt.newValue));
                    return field;
                }
                case XAnimationParameterType.Trigger:
                default:
                {
                    Label label = new("Trigger has no default value");
                    label.style.color = TextMuted;
                    label.style.fontSize = BodyFontSize;
                    return label;
                }
            }
        }

        private VisualElement CreateStateChannelGroup(XAnimationCompiledChannel channel, List<StateGroupBucket> channelStates)
        {
            VisualElement group = CreateListGroup();
            VisualElement groupHeader = CreateListHeader();

            Label groupTitle = CreateBoldLabel($"▾ {channel.Name}");
            groupTitle.style.flexGrow = 1;
            groupTitle.style.flexShrink = 1;
            groupTitle.style.minWidth = 0;
            groupTitle.tooltip = "点击展开/收起这个 channel 的 state 列表。";
            groupHeader.Add(groupTitle);

            int stateCount = CountStatesInBuckets(channelStates);
            int groupedCount = CountGroupedBuckets(channelStates);
            Label groupInfo = new(groupedCount > 0
                ? $"{channel.Config.layerType} | {stateCount} states | {groupedCount} groups"
                : $"{channel.Config.layerType} | {stateCount} states");
            groupInfo.style.color = TextMuted;
            groupInfo.style.fontSize = 10;
            groupInfo.style.flexShrink = 0;
            groupHeader.Add(groupInfo);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;
            actions.style.flexShrink = 0;
            actions.style.marginLeft = 6;

            Button addStateButton = new(() => AddState(channel.Name))
            {
                text = "+ State"
            };
            addStateButton.tooltip = m_Session.IsOverrideAsset
                ? "Override 资源不能新增 state。"
                : "在这个 channel 下新增一个未分组 state。";
            addStateButton.SetEnabled(!m_Session.IsOverrideAsset);
            ApplyClipIconButtonStyle(addStateButton, AccentColor);
            addStateButton.style.width = 62;
            addStateButton.style.minWidth = 62;
            addStateButton.style.flexShrink = 0;
            actions.Add(addStateButton);

            Button addGroupButton = new(() => AddStateGroup(channel.Name))
            {
                text = "+ Group"
            };
            addGroupButton.tooltip = m_Session.IsOverrideAsset
                ? "Override 资源不能新增 state group。"
                : "在这个 channel 下新建分组，并同时创建首个 state。";
            addGroupButton.SetEnabled(!m_Session.IsOverrideAsset);
            ApplyClipIconButtonStyle(addGroupButton, AccentColor);
            addGroupButton.style.width = 66;
            addGroupButton.style.minWidth = 66;
            addGroupButton.style.flexShrink = 0;
            addGroupButton.style.marginLeft = 4;
            actions.Add(addGroupButton);
            groupHeader.Add(actions);
            group.Add(groupHeader);
            RegisterStateChannelDropTarget(group, groupHeader, channel.Name, string.Empty);

            VisualElement statesContainer = new VisualElement();
            int rowIndex = 0;
            for (int stateIndex = 0; stateIndex < channelStates.Count; stateIndex++)
            {
                StateGroupBucket bucket = channelStates[stateIndex];
                if (bucket == null)
                {
                    continue;
                }

                if (bucket.IsUngrouped)
                {
                    for (int i = 0; i < bucket.States.Count; i++)
                    {
                        XAnimationCompiledState state = bucket.States[i];
                        VisualElement row = CreateStateRow(state, rowIndex++);
                        RegisterStateRowDropTarget(row, channel.Name, state.Key, string.Empty);
                        statesContainer.Add(row);
                    }

                    continue;
                }

                VisualElement subgroup = CreateStateEditorGroup(channel.Name, bucket, ref rowIndex);
                statesContainer.Add(subgroup);
            }

            groupTitle.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                bool expanded = statesContainer.style.display != DisplayStyle.None;
                statesContainer.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                groupTitle.text = expanded ? $"▸ {channel.Name}" : $"▾ {channel.Name}";
                evt.StopPropagation();
            });

            group.Add(statesContainer);
            return group;
        }

        private VisualElement CreateStateEditorGroup(string channelName, StateGroupBucket bucket, ref int rowIndex)
        {
            VisualElement group = CreateNestedListGroup();
            string groupKey = BuildStateGroupKey(channelName, bucket.GroupName);
            m_StateGroupRowMap[groupKey] = group;

            VisualElement header = CreateListHeader();
            Label foldoutLabel = CreateFoldoutGlyph(!IsStateGroupCollapsed(groupKey));
            header.Add(foldoutLabel);

            EditableLabel groupLabel = new(bucket.GroupName);
            ConfigureEditableNameLabel(groupLabel, 180f);
            groupLabel.tooltip = "单击展开/收起这个 state group；右键 Rename 编辑分组名。";
            groupLabel.SetEditable(!m_Session.IsOverrideAsset, EditableLabelEditTrigger.ContextMenu);
            groupLabel.EditStarted += BeginNameEdit;
            groupLabel.EditEnded += EndNameEdit;
            groupLabel.ValueCommitted += (_, newValue) => RenameStateGroup(channelName, bucket.GroupName, newValue, groupLabel);
            RegisterStateGroupContextMenu(groupLabel, channelName, bucket.GroupName);
            m_StateGroupLabelMap[groupKey] = groupLabel;
            header.Add(groupLabel);

            VisualElement spacer = new();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            Label info = CreateSmallInfoLabel($"{bucket.States.Count} states");
            header.Add(info);

            Button addStateButton = new(() => AddState(channelName, bucket.GroupName))
            {
                text = "+"
            };
            addStateButton.tooltip = m_Session.IsOverrideAsset
                ? "Override 资源不能新增 state。"
                : "在这个分组下新增一个 state。";
            addStateButton.SetEnabled(!m_Session.IsOverrideAsset);
            ApplyClipIconButtonStyle(addStateButton, AccentColor);
            addStateButton.style.marginLeft = 4;
            header.Add(addStateButton);
            group.Add(header);

            RegisterStateChannelDropTarget(group, header, channelName, bucket.GroupName);

            VisualElement content = new VisualElement();
            content.style.display = IsStateGroupCollapsed(groupKey) ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < bucket.States.Count; i++)
            {
                XAnimationCompiledState state = bucket.States[i];
                VisualElement row = CreateStateRow(state, rowIndex++);
                RegisterStateRowDropTarget(row, channelName, state.Key, bucket.GroupName);
                content.Add(row);
            }

            void Toggle()
            {
                bool expanded = content.style.display != DisplayStyle.None;
                content.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                foldoutLabel.text = expanded ? "▸" : "▾";
                SetStateGroupCollapsed(groupKey, expanded);
            }

            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || groupLabel.IsEditing)
                {
                    return;
                }

                Toggle();
                evt.StopPropagation();
            });

            group.Add(content);
            return group;
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

        private VisualElement CreateClipEditorGroup(ClipGroupBucket bucket, ref int rowIndex)
        {
            VisualElement group = CreateNestedListGroup();
            string groupKey = BuildClipGroupKey(bucket.GroupName);
            m_ClipGroupRowMap[groupKey] = group;

            VisualElement header = CreateListHeader();
            Label foldoutLabel = CreateFoldoutGlyph(!IsClipGroupCollapsed(groupKey));
            header.Add(foldoutLabel);

            EditableLabel groupLabel = new(bucket.GroupName);
            ConfigureEditableNameLabel(groupLabel, 180f);
            groupLabel.tooltip = "单击展开/收起这个 clip group；右键 Rename 编辑分组名。";
            groupLabel.SetEditable(!m_Session.IsOverrideAsset, EditableLabelEditTrigger.ContextMenu);
            groupLabel.EditStarted += BeginNameEdit;
            groupLabel.EditEnded += EndNameEdit;
            groupLabel.ValueCommitted += (_, newValue) => RenameClipGroup(bucket.GroupName, newValue, groupLabel);
            RegisterClipGroupContextMenu(groupLabel, bucket.GroupName);
            m_ClipGroupLabelMap[groupKey] = groupLabel;
            header.Add(groupLabel);

            VisualElement spacer = new();
            spacer.style.flexGrow = 1;
            header.Add(spacer);

            Label info = CreateSmallInfoLabel($"{bucket.Clips.Count} clips");
            header.Add(info);

            Button addClipButton = new(() => AddClip(bucket.GroupName))
            {
                text = "+"
            };
            addClipButton.tooltip = m_Session.IsOverrideAsset ? "Override 资源不能新增 clip。" : "在这个分组下新增一个 clip。";
            addClipButton.SetEnabled(!m_Session.IsOverrideAsset);
            ApplyClipIconButtonStyle(addClipButton, AccentColor);
            addClipButton.style.marginLeft = 4;
            header.Add(addClipButton);
            group.Add(header);

            RegisterClipGroupDropTarget(group, header, bucket.GroupName);

            VisualElement content = new VisualElement();
            content.style.display = IsClipGroupCollapsed(groupKey) ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < bucket.Clips.Count; i++)
            {
                XAnimationCompiledClip clip = bucket.Clips[i];
                VisualElement row = CreateClipRow(clip, rowIndex++);
                RegisterClipRowDropTarget(row, clip.Key, bucket.GroupName);
                content.Add(row);
            }

            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || groupLabel.IsEditing)
                {
                    return;
                }

                bool expanded = content.style.display != DisplayStyle.None;
                content.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                foldoutLabel.text = expanded ? "▸" : "▾";
                SetClipGroupCollapsed(groupKey, expanded);
                evt.StopPropagation();
            });

            group.Add(content);
            return group;
        }

        private static ClipGroupBucket FindClipGroupBucket(List<ClipGroupBucket> buckets, string groupName)
        {
            if (buckets == null)
            {
                return null;
            }

            groupName = NormalizeClipEditorGroupName(groupName);
            for (int i = 0; i < buckets.Count; i++)
            {
                ClipGroupBucket bucket = buckets[i];
                if (bucket != null &&
                    string.Equals(NormalizeClipEditorGroupName(bucket.GroupName), groupName, StringComparison.Ordinal))
                {
                    return bucket;
                }
            }

            return null;
        }

        private VisualElement CreateStateRow(XAnimationCompiledState state, int rowIndex)
        {
            VisualElement wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Column;
            wrapper.style.marginBottom = 1;

            VisualElement container = CreateInteractiveRowContainer(rowIndex);
            Color baseColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            m_StateRowMap[state.Key] = container;
            m_StateChannelMap[state.Key] = state.Config.channelName;

            VisualElement progressFill = CreateRowProgressFill();
            container.Add(progressFill);

            RowVisualState visualState = new()
            {
                BaseColor = baseColor,
                ProgressFill = progressFill,
            };
            m_StateVisualStateMap[state.Key] = visualState;
            container.RegisterCallback<MouseEnterEvent>(_ =>
            {
                visualState.Hovered = true;
                ApplyStateRowVisualState(state.Key);
            });
            container.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                visualState.Hovered = false;
                ApplyStateRowVisualState(state.Key);
            });

            VisualElement row = CreateRowContent();
            container.Add(row);

            VisualElement summaryRow = new VisualElement();
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.flexGrow = 1;
            summaryRow.style.flexShrink = 1;
            summaryRow.style.minWidth = 0;
            row.Add(summaryRow);

            string stateKey = state.Key;
            EditableLabel label = new(stateKey);
            ConfigureEditableNameLabel(label, 78f);
            label.tooltip = "单击展开/收起 state 配置；右键可 Rename，也可批量修改这个 state 用到的动画。";
            label.SetEditable(true, EditableLabelEditTrigger.None);
            label.EditStarted += BeginNameEdit;
            label.EditEnded += EndNameEdit;
            label.ValueCommitted += (_, newValue) => RenameState(stateKey, newValue, label);
            m_StateLabelMap[stateKey] = label;
            label.style.position = Position.Relative;
            RegisterStateLabelContextMenu(label, state);
            summaryRow.Add(label);

            List<string> stateTypeNames = new(Enum.GetNames(typeof(XAnimationStateType)));
            DropdownField stateTypeField = new(
                string.Empty,
                stateTypeNames,
                Mathf.Max(0, stateTypeNames.IndexOf(state.Config.stateType.ToString())));
            ApplyDropdownFieldStyle(stateTypeField);
            stateTypeField.style.width = 180;
            stateTypeField.style.minWidth = 120;
            stateTypeField.style.flexGrow = 2;
            stateTypeField.style.flexShrink = 1;
            stateTypeField.style.marginLeft = 6;
            stateTypeField.style.position = Position.Relative;
            stateTypeField.tooltip = "State 类型。";
            stateTypeField.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(evt.newValue, out XAnimationStateType stateType))
                {
                    return;
                }

                ChangeStateType(state.Key, stateType, evt.previousValue, stateTypeField);
            });
            summaryRow.Add(stateTypeField);

            if (state.Config.stateType == XAnimationStateType.Single)
            {
                XAnimationEditorSelectionField clipField = CreateClipSelectionField(string.Empty, state.Config.clipKey);
                clipField.style.width = 220;
                clipField.style.minWidth = 160;
                clipField.style.flexGrow = 3;
                clipField.style.flexShrink = 1;
                clipField.style.marginLeft = 6;
                clipField.style.position = Position.Relative;
                clipField.tooltip = "Single state 播放的 clipKey。";
                clipField.ValueChanged += (previousValue, newValue) => ChangeStateClipKey(state.Key, newValue, clipField, previousValue);
                AttachClipKeyPingButton(clipField, state.Config.clipKey, enabled: true);
                summaryRow.Add(clipField);
            }

            VisualElement editor = CreateStateEditor(state);
            m_StateEditorMap[stateKey] = editor;
            editor.style.display = m_ExpandedStateKeys.Contains(stateKey) ? DisplayStyle.Flex : DisplayStyle.None;
            RegisterStateNameInteractions(label, editor, stateKey);

            VisualElement actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.alignItems = Align.Center;
            actionsRow.style.flexShrink = 0;
            actionsRow.style.marginLeft = 6;
            row.Add(actionsRow);

            Button playButton = new(() => ToggleStatePlayback(state))
            {
                text = "▶"
            };
            playButton.tooltip = "播放或停止这个 state。Blend1D 和 2D directional blend 会读取绑定参数实时混合。";
            ApplyClipButtonStyle(playButton, false);
            playButton.style.flexShrink = 0;
            playButton.style.position = Position.Relative;
            actionsRow.Add(playButton);
            m_StateButtonMap[state.Key] = playButton;

            Button deleteButton = new(() => DeleteState(stateKey))
            {
                text = "⌫"
            };
            deleteButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能删除 state。"
                : "删除这个 state。";
            deleteButton.SetEnabled(m_Session != null && !m_Session.IsOverrideAsset);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.flexShrink = 0;
            deleteButton.style.marginLeft = 3;
            deleteButton.style.position = Position.Relative;
            actionsRow.Add(deleteButton);

            wrapper.Add(container);
            wrapper.Add(editor);
            return wrapper;
        }

        private static string GetStatePrimaryClipKey(XAnimationCompiledState state)
        {
            return state switch
            {
                XAnimationCompiledSingleState => state.Config.clipKey,
                XAnimationCompiledBlend1DState blendState when blendState.Samples.Count > 0 => blendState.Samples[0].Config.clipKey,
                XAnimationCompiledBlend2DSimpleDirectionalState directionalState when directionalState.Samples.Count > 0 => directionalState.Samples[0].Config.clipKey,
                XAnimationCompiledBlend2DFreeformDirectionalState directionalState when directionalState.Samples.Count > 0 => directionalState.Samples[0].Config.clipKey,
                _ => null,
            };
        }

        private void ToggleStatePlayback(XAnimationCompiledState state)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            MarkFreeformStateInteracted(state.Key);

            XAnimationChannelState channelState = m_Session.GetChannelState(state.Config.channelName);
            bool isPlaying = channelState != null && string.Equals(channelState.stateKey, state.Key, StringComparison.Ordinal);
            if (isPlaying)
            {
                m_Session.StopChannel(state.Config.channelName);
                RefreshPlaybackViews();
                SetStatus($"已停止 state {state.Key}。");
                return;
            }

            m_IsPaused = false;
            m_Session.SetPaused(false);
            SetPauseButtonState(true, false);
            SetStepForwardButtonEnabled(true);
            m_Session.SetTimeScale(GetPlaybackSpeed());
            m_Session.PlayState(state.Key, BuildPreviewTransitionOptions());
            if (!string.IsNullOrEmpty(state.Config.channelName))
            {
                m_Session.SetChannelTimeScale(state.Config.channelName, GetPlaybackSpeed());
            }
            RefreshPlaybackViews();
            SetStatus($"正在播放 state {state.Key}。");
        }

        private void PreviewBlendSample(string stateKey, float threshold)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            MarkFreeformStateInteracted(stateKey);

            XAnimationCompiledState state = m_Session.CompiledAsset.GetState(stateKey);
            if (state is not XAnimationCompiledBlend1DState blendState)
            {
                return;
            }

            string parameterName = blendState.Config.parameterName;
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                m_Session.SetPreviewParameter(parameterName, threshold);
            }

            PlayStateForSamplePreview(blendState, $"正在预览 {stateKey} sample，{parameterName} = {threshold:0.###}。");
        }

        private void PreviewDirectionalBlendSample(string stateKey, float positionX, float positionY)
        {
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            MarkFreeformStateInteracted(stateKey);

            XAnimationCompiledState state = m_Session.CompiledAsset.GetState(stateKey);
            if (!TryGetDirectionalBlendSamples(state, out _))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.Config.parameterXName))
            {
                m_Session.SetPreviewParameter(state.Config.parameterXName, positionX);
            }

            if (!string.IsNullOrWhiteSpace(state.Config.parameterYName))
            {
                m_Session.SetPreviewParameter(state.Config.parameterYName, positionY);
            }

            PlayStateForSamplePreview(
                state,
                $"正在预览 {stateKey} sample，({state.Config.parameterXName}, {state.Config.parameterYName}) = ({positionX:0.###}, {positionY:0.###})。");
        }

        private void PlayStateForSamplePreview(XAnimationCompiledState state, string statusMessage)
        {
            if (m_Session == null || !m_Session.IsLoaded || state == null)
            {
                return;
            }

            MarkFreeformStateInteracted(state.Key);

            m_IsPaused = false;
            m_Session.SetPaused(false);
            SetPauseButtonState(true, false);
            SetStepForwardButtonEnabled(true);
            m_Session.SetTimeScale(GetPlaybackSpeed());
            m_Session.PlayState(state.Key, BuildPreviewTransitionOptions());
            if (!string.IsNullOrEmpty(state.Config.channelName))
            {
                m_Session.SetChannelTimeScale(state.Config.channelName, GetPlaybackSpeed());
            }

            RebuildParameterList();
            RefreshPlaybackViews();
            SetStatus(statusMessage);
        }

        private VisualElement CreateCueRow(int cueIndex, XAnimationCueConfig cue, bool editable)
        {
            return CreateCueRow(new DisplayedCueEntry(
                cueIndex,
                cue != null ? cue.time : 0f,
                cue?.eventKey,
                cue?.payload,
                false), editable);
        }

        private VisualElement CreateCueRow(DisplayedCueEntry cue, bool editable)
        {
            VisualElement row = CreateSubBox();
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom = 3;
            row.tooltip = cue.IsReadOnlyDerived
                ? "这个 Cue 由 AnimationClip 上的 Animation Event 自动派生，只读显示。"
                : editable
                    ? "Cue 会在对应 clip 播放经过 normalized time 时触发。"
                    : "Override 资源只能预览 cue，不能编辑 base cue 配置。";
            row.userData = cue;

            VisualElement topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;
            topRow.style.marginBottom = 2;
            row.Add(topRow);

            Label indexLabel = new(cue.IsReadOnlyDerived ? "evt" : $"#{cue.CueIndex}");
            indexLabel.style.width = 28;
            indexLabel.style.flexShrink = 0;
            indexLabel.style.color = TextMuted;
            indexLabel.style.fontSize = BodyFontSize;
            indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            topRow.Add(indexLabel);

            FloatField timeField = new("time")
            {
                value = cue.Time
            };
            timeField.tooltip = "Cue 触发时间，范围是 clip normalized time [0, 1]。";
            timeField.SetEnabled(editable && !cue.IsReadOnlyDerived);
            timeField.style.flexGrow = 1;
            if (!cue.IsReadOnlyDerived)
            {
                timeField.RegisterValueChangedCallback(evt => ChangeCueTime(cue.CueIndex, evt.newValue, timeField));
            }
            topRow.Add(timeField);

            if (cue.IsReadOnlyDerived)
            {
                Label readOnlyLabel = new("Animation Event");
                readOnlyLabel.tooltip = "来自 AnimationClip.events 的派生 Cue，只读。";
                readOnlyLabel.style.marginLeft = 6;
                readOnlyLabel.style.color = TextMuted;
                readOnlyLabel.style.fontSize = BodyFontSize;
                topRow.Add(readOnlyLabel);
            }
            else
            {
                Button deleteButton = new(() => DeleteCue(cue.CueIndex))
                {
                    text = "⌫"
                };
                deleteButton.tooltip = editable ? "删除这个 cue。" : "Override 资源不能删除 cue。";
                deleteButton.SetEnabled(editable);
                ApplyTrashButtonIcon(deleteButton);
                ApplyClipIconButtonStyle(deleteButton);
                deleteButton.style.marginLeft = 4;
                topRow.Add(deleteButton);
            }

            TextField eventKeyField = new("eventKey")
            {
                value = cue.EventKey,
                isDelayed = true
            };
            eventKeyField.tooltip = "Cue 触发时派发的事件 key，不能为空。";
            eventKeyField.SetEnabled(editable && !cue.IsReadOnlyDerived);
            if (!cue.IsReadOnlyDerived)
            {
                eventKeyField.RegisterValueChangedCallback(evt => ChangeCueEventKey(cue.CueIndex, evt.newValue, eventKeyField, evt.previousValue));
            }
            row.Add(eventKeyField);

            TextField payloadField = new("payload")
            {
                value = cue.Payload,
                isDelayed = true
            };
            payloadField.tooltip = "Cue 触发时携带的字符串 payload。";
            payloadField.SetEnabled(editable && !cue.IsReadOnlyDerived);
            if (!cue.IsReadOnlyDerived)
            {
                payloadField.RegisterValueChangedCallback(evt => ChangeCuePayload(cue.CueIndex, evt.newValue));
            }
            row.Add(payloadField);

            return row;
        }

        private VisualElement CreateClipCueEditor(XAnimationCompiledClip clip)
        {
            VisualElement box = CreateSubBox();
            box.style.marginTop = 5;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 3;

            Label title = new("Cues");
            title.style.flexGrow = 1;
            title.style.color = TextNormal;
            title.style.fontSize = BodyFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            string clipKey = clip?.Key ?? string.Empty;
            bool editable = m_Session != null && !m_Session.IsOverrideAsset;
            Button addButton = new(() => AddCue(clipKey))
            {
                text = "+"
            };
            addButton.tooltip = editable ? "在这个 clip 下新增一个 cue。" : "Override 资源不能新增 cue。";
            addButton.SetEnabled(editable);
            ApplyClipIconButtonStyle(addButton, AccentColor);
            header.Add(addButton);
            box.Add(header);

            XAnimationCueConfig[] cues = m_Session?.CompiledAsset.Asset.cues ?? Array.Empty<XAnimationCueConfig>();
            bool hasCue = false;
            for (int i = 0; i < cues.Length; i++)
            {
                XAnimationCueConfig cue = cues[i];
                if (cue == null || !string.Equals(cue.clipKey, clipKey, StringComparison.Ordinal))
                {
                    continue;
                }

                hasCue = true;
                VisualElement cueRow = CreateCueRow(i, cue, editable);
                string cueKey = BuildCueSearchKey(clipKey, i);
                m_CueRowMap[cueKey] = cueRow;
                box.Add(cueRow);
            }

            List<DisplayedCueEntry> derivedCues = CollectDerivedClipCues(clip);
            if (derivedCues.Count > 0)
            {
                Label derivedLabel = new("Animation Events");
                derivedLabel.tooltip = "这些 Cue 由 AnimationClip.events 自动派生，只读显示。";
                derivedLabel.style.marginLeft = 4;
                derivedLabel.style.marginTop = hasCue ? 4 : 1;
                derivedLabel.style.marginBottom = 2;
                derivedLabel.style.color = TextMuted;
                derivedLabel.style.fontSize = BodyFontSize;
                derivedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.Add(derivedLabel);

                for (int i = 0; i < derivedCues.Count; i++)
                {
                    hasCue = true;
                    VisualElement cueRow = CreateCueRow(derivedCues[i], editable: false);
                    string cueKey = BuildDerivedCueSearchKey(clipKey, i);
                    m_CueRowMap[cueKey] = cueRow;
                    box.Add(cueRow);
                }
            }

            if (!hasCue)
            {
                Label emptyLabel = new("No cues");
                emptyLabel.style.color = TextMuted;
                emptyLabel.style.fontSize = BodyFontSize;
                emptyLabel.style.marginLeft = 4;
                emptyLabel.style.marginTop = 1;
                box.Add(emptyLabel);
            }

            return box;
        }

        private static List<DisplayedCueEntry> CollectDerivedClipCues(XAnimationCompiledClip clip)
        {
            List<DisplayedCueEntry> results = new();
            AnimationClip animationClip = clip?.Clip;
            if (animationClip == null)
            {
                return results;
            }

            AnimationEvent[] events = animationClip.events;
            if (events == null || events.Length == 0)
            {
                return results;
            }

            float clipLength = Mathf.Max(animationClip.length, 0.0001f);
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent animationEvent = events[i];
                if (animationEvent == null || string.IsNullOrWhiteSpace(animationEvent.functionName))
                {
                    continue;
                }

                results.Add(new DisplayedCueEntry(
                    -1,
                    Mathf.Clamp01(animationEvent.time / clipLength),
                    animationEvent.functionName,
                    ResolveAnimationEventPayload(animationEvent),
                    true));
            }

            return results;
        }

        private static string ResolveAnimationEventPayload(AnimationEvent animationEvent)
        {
            if (animationEvent == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(animationEvent.stringParameter))
            {
                return animationEvent.stringParameter;
            }

            if (animationEvent.intParameter != 0)
            {
                return animationEvent.intParameter.ToString();
            }

            if (!Mathf.Approximately(animationEvent.floatParameter, 0f))
            {
                return animationEvent.floatParameter.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (animationEvent.objectReferenceParameter != null)
            {
                return animationEvent.objectReferenceParameter.name ?? string.Empty;
            }

            return string.Empty;
        }

        private VisualElement CreateClipRow(XAnimationCompiledClip clip, int rowIndex)
        {
            VisualElement wrapper = new VisualElement();
            wrapper.style.flexDirection = FlexDirection.Column;
            wrapper.style.marginBottom = 1;

            VisualElement container = CreateInteractiveRowContainer(rowIndex);
            Color baseColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            m_ClipRowMap[clip.Key] = container;
            VisualElement progressFill = CreateRowProgressFill();
            container.Add(progressFill);
            ClipRowVisualState visualState = new()
            {
                BaseColor = baseColor,
                ProgressFill = progressFill,
            };
            m_ClipVisualStateMap[clip.Key] = visualState;
            container.RegisterCallback<MouseEnterEvent>(_ =>
            {
                visualState.Hovered = true;
                ApplyClipRowVisualState(clip.Key);
            });
            container.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                visualState.Hovered = false;
                ApplyClipRowVisualState(clip.Key);
            });

            VisualElement row = CreateRowContent();
            container.Add(row);

            string clipKey = clip.Key;

            EditableLabel label = new(clipKey);
            ConfigureEditableNameLabel(label, 78f);
            label.tooltip = "单击展开/收起 clip 配置；右键 Rename 编辑名称。";
            label.SetEditable(true, EditableLabelEditTrigger.ContextMenu);
            label.EditStarted += BeginNameEdit;
            label.EditEnded += EndNameEdit;
            label.ValueCommitted += (_, newValue) => RenameClip(clipKey, newValue, label);
            m_ClipLabelMap[clipKey] = label;
            label.style.position = Position.Relative;
            row.Add(label);

            VisualElement fileInfo = new VisualElement();
            fileInfo.style.flexGrow = 1;
            fileInfo.style.flexShrink = 1;
            fileInfo.style.minWidth = 140;
            fileInfo.style.marginLeft = 4;
            fileInfo.style.marginRight = 4;
            fileInfo.style.flexDirection = FlexDirection.Row;
            fileInfo.style.position = Position.Relative;
            row.Add(fileInfo);

            string activeClipPath = clip.Config.clipPath;

            ObjectField activeClipField = CreateClipObjectField(activeClipPath, editable: true);
            activeClipField.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "当前 Override 资源中的覆盖动画。可直接修改，不会写回 base 资源。"
                : "该 clip 对应的 AnimationClip 资源。可直接修改并保存到当前 XAnimation 文件。";
            activeClipField.style.flexGrow = 1;
            activeClipField.style.flexShrink = 1;
            activeClipField.style.minWidth = 120;
            activeClipField.style.maxWidth = 260;
            activeClipField.RegisterValueChangedCallback(evt => ChangeClipPath(clip, activeClipField, evt.previousValue as AnimationClip, evt.newValue as AnimationClip));
            fileInfo.Add(activeClipField);

            VisualElement editor = CreateClipEditor(clip);
            editor.style.display = m_ExpandedClipKeys.Contains(clipKey) ? DisplayStyle.Flex : DisplayStyle.None;
            RegisterClipNameInteractions(label, editor, clip);

            Button toggleButton = new(() =>
            {
                if (m_Session == null || !m_Session.IsLoaded) return;

                string channelName = m_PlayTargetChannelName;
                if (string.IsNullOrWhiteSpace(channelName))
                {
                    SetStatus("请先在 Target 中选择 channelName 后再调试播放 clip。", true);
                    return;
                }

                string playingChannelName = FindPlayingChannelName(clipKey);
                bool isPlaying = !string.IsNullOrWhiteSpace(playingChannelName);

                if (isPlaying)
                {
                    m_Session.StopChannel(playingChannelName, 0f);
                    RefreshPlaybackViews();
                    SetStatus($"已停止 {clipKey}。");
                }
                else
                {
                    m_IsPaused = false;
                    m_Session.SetPaused(false);
                    SetPauseButtonState(true, false);
                    SetStepForwardButtonEnabled(true);
                    m_Session.SetTimeScale(GetPlaybackSpeed());
                    m_Session.PlayClip(clipKey, channelName, BuildPreviewTransitionOptions());
                    if (!string.IsNullOrEmpty(channelName))
                    {
                        m_Session.SetChannelTimeScale(channelName, GetPlaybackSpeed());
                    }
                    RefreshPlaybackViews();
                    SetStatus($"正在 {channelName} 调试播放 {clipKey} ({clip.Clip.name})。");
                }
            })
            {
                text = "▶"
            };
            toggleButton.tooltip = "使用 Target.channelName 调试播放或停止这个 clip。";
            ApplyClipButtonStyle(toggleButton, false);
            toggleButton.style.flexShrink = 0;
            toggleButton.style.marginLeft = 4;
            toggleButton.style.position = Position.Relative;
            row.Add(toggleButton);

            Button deleteButton = new(() => DeleteClip(clipKey))
            {
                text = "⌫"
            };
            deleteButton.tooltip = m_Session != null && m_Session.IsOverrideAsset
                ? "Override 资源不能删除 clip 结构。"
                : "删除这个 clip。";
            deleteButton.SetEnabled(m_Session != null && !m_Session.IsOverrideAsset);
            ApplyTrashButtonIcon(deleteButton);
            ApplyClipIconButtonStyle(deleteButton);
            deleteButton.style.flexShrink = 0;
            deleteButton.style.marginLeft = 3;
            deleteButton.style.position = Position.Relative;
            row.Add(deleteButton);

            m_ClipButtonMap[clipKey] = toggleButton;
            wrapper.Add(container);
            wrapper.Add(editor);
            return wrapper;
        }

        private void RegisterClipNameInteractions(EditableLabel label, VisualElement editor, XAnimationCompiledClip clip)
        {
            bool isPressed = false;
            Vector2 startPosition = Vector2.zero;
            bool movedBeyondClickThreshold = false;
            bool dragStarted = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                if (label.IsEditing)
                {
                    ClearClipDragData();
                    isPressed = false;
                    movedBeyondClickThreshold = false;
                    dragStarted = false;
                    return;
                }

                isPressed = true;
                movedBeyondClickThreshold = false;
                dragStarted = false;
                startPosition = evt.mousePosition;
                ClearClipDragData();
                evt.StopPropagation();
            });
            label.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!isPressed || m_IsEditingName || label.IsEditing)
                {
                    return;
                }

                if (!movedBeyondClickThreshold && (evt.mousePosition - startPosition).sqrMagnitude >= 16f)
                {
                    movedBeyondClickThreshold = true;
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData(ClipDragDataKey, clip.Key);
                    DragAndDrop.StartDrag($"Move {clip.Key}");
                    dragStarted = true;
                    evt.StopPropagation();
                }
            });
            label.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!isPressed || evt.button != 0)
                {
                    return;
                }

                if (!movedBeyondClickThreshold && !label.IsEditing)
                {
                    bool expanded = editor.style.display == DisplayStyle.None;
                    editor.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                    if (expanded)
                    {
                        m_ExpandedClipKeys.Add(clip.Key);
                    }
                    else
                    {
                        m_ExpandedClipKeys.Remove(clip.Key);
                    }
                }

                if (!dragStarted)
                {
                    ClearClipDragData();
                }

                isPressed = false;
                movedBeyondClickThreshold = false;
                dragStarted = false;
                evt.StopPropagation();
            });
        }

        private void RegisterStateNameInteractions(EditableLabel label, VisualElement editor, string stateKey)
        {
            bool isPressed = false;
            Vector2 startPosition = Vector2.zero;
            bool movedBeyondClickThreshold = false;
            bool dragStarted = false;

            label.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 || m_Session == null || !m_Session.IsLoaded)
                {
                    return;
                }

                if (label.IsEditing)
                {
                    ClearStateDragData();
                    isPressed = false;
                    movedBeyondClickThreshold = false;
                    dragStarted = false;
                    return;
                }

                isPressed = true;
                movedBeyondClickThreshold = false;
                dragStarted = false;
                startPosition = evt.mousePosition;
                ClearStateDragData();
                evt.StopPropagation();
            });
            label.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!isPressed || m_IsEditingName || label.IsEditing)
                {
                    return;
                }

                if (!movedBeyondClickThreshold && (evt.mousePosition - startPosition).sqrMagnitude >= 16f)
                {
                    movedBeyondClickThreshold = true;
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData(StateDragDataKey, stateKey);
                    DragAndDrop.StartDrag($"Move {stateKey}");
                    dragStarted = true;
                    evt.StopPropagation();
                }
            });
            label.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!isPressed || evt.button != 0)
                {
                    return;
                }

                if (!movedBeyondClickThreshold && !label.IsEditing)
                {
                    bool expanded = editor.style.display == DisplayStyle.None;
                    SetStateExpanded(stateKey, expanded);
                }

                if (!dragStarted)
                {
                    ClearStateDragData();
                }

                isPressed = false;
                movedBeyondClickThreshold = false;
                dragStarted = false;
                evt.StopPropagation();
            });
        }

        private void BeginNameEdit()
        {
            m_IsEditingName = true;
            ClearStateDragData();
            ClearClipDragData();
        }

        private void EndNameEdit()
        {
            m_IsEditingName = false;
            ClearStateDragData();
            ClearClipDragData();
        }

        private static void ClearStateDragData()
        {
            DragAndDrop.SetGenericData(StateDragDataKey, null);
        }

        private void SetStateExpanded(string stateKey, bool expanded)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                return;
            }

            if (expanded)
            {
                if (m_ExpandedStateKeys.Count == 1 && m_ExpandedStateKeys.Contains(stateKey))
                {
                    if (m_StateEditorMap.TryGetValue(stateKey, out VisualElement currentEditor))
                    {
                        currentEditor.style.display = DisplayStyle.Flex;
                    }

                    RefreshGlobalBlendGraph();
                    return;
                }

                List<string> previouslyExpandedKeys = m_ExpandedStateKeys.Count > 0
                    ? new List<string>(m_ExpandedStateKeys)
                    : null;

                m_ExpandedStateKeys.Clear();
                m_ExpandedStateKeys.Add(stateKey);

                if (previouslyExpandedKeys != null)
                {
                    for (int i = 0; i < previouslyExpandedKeys.Count; i++)
                    {
                        string expandedKey = previouslyExpandedKeys[i];
                        if (string.Equals(expandedKey, stateKey, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (m_StateEditorMap.TryGetValue(expandedKey, out VisualElement expandedEditor))
                        {
                            expandedEditor.style.display = DisplayStyle.None;
                        }
                    }
                }

                if (m_StateEditorMap.TryGetValue(stateKey, out VisualElement nextEditor))
                {
                    nextEditor.style.display = DisplayStyle.Flex;
                }

                RefreshGlobalBlendGraph();
                return;
            }

            bool removed = m_ExpandedStateKeys.Remove(stateKey);
            if (m_StateEditorMap.TryGetValue(stateKey, out VisualElement editor))
            {
                editor.style.display = DisplayStyle.None;
            }

            if (removed)
            {
                RefreshGlobalBlendGraph();
            }
        }

        private bool TryGetExpandedStateKey(out string stateKey)
        {
            stateKey = null;
            if (m_ExpandedStateKeys.Count == 0)
            {
                return false;
            }

            foreach (string expandedStateKey in m_ExpandedStateKeys)
            {
                if (string.IsNullOrWhiteSpace(expandedStateKey))
                {
                    continue;
                }

                stateKey = expandedStateKey;
                return true;
            }

            return false;
        }

        private static void ClearClipDragData()
        {
            DragAndDrop.SetGenericData(ClipDragDataKey, null);
        }

        private void AddChannel()
        {
            try
            {
                string channelName = m_Session.AddChannel();
                m_PendingChannelRenameKey = channelName;
                RebuildStructureAndPlaybackViews();
                SetStatus($"已新增 Channel {channelName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteChannel(string channelName)
        {
            int stateCount = CountStatesInChannel(channelName);
            string message = stateCount > 0
                ? $"确定删除 Channel '{channelName}'？\n\n将同时移除该 Channel 下的 {stateCount} 个 State；Clip 资源不会被删除。"
                : $"确定删除 Channel '{channelName}'？";
            if (!EditorUtility.DisplayDialog("删除 Channel", message, "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteChannel(channelName);
                RebuildStatePresentation(includeChannelPresentation: true);
                RefreshClipPlayingStates();
                SetStatus($"已删除 Channel {channelName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddParameter()
        {
            try
            {
                string parameterName = m_Session.AddParameter();
                m_PendingParameterRenameKey = parameterName;
                RebuildParameterList();
                RebuildStateList();
                SetStatus($"已新增 Parameter {parameterName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteParameter(string parameterName)
        {
            if (!EditorUtility.DisplayDialog("删除 Parameter", $"确定删除 Parameter '{parameterName}'？\n\n引用它的 Blend1D / 2D directional blend state 会清空对应 parameter。", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteParameter(parameterName);
                RebuildParameterList();
                RebuildStatePresentation();
                SetStatus($"已删除 Parameter {parameterName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RenameParameter(string oldName, string newName, EditableLabel label)
        {
            newName = newName?.Trim();
            try
            {
                m_Session.RenameParameter(oldName, newName);
                SetStatus($"Parameter {oldName} 已重命名为 {newName}。");
                RebuildParameterList();
                RebuildStatePresentation();
            }
            catch (Exception ex)
            {
                label.text = oldName;
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeParameterType(string parameterName, string typeName, string previousValue, DropdownField field)
        {
            try
            {
                if (!Enum.TryParse(typeName, out XAnimationParameterType type))
                {
                    return;
                }

                m_Session.SetParameterType(parameterName, type);
                RebuildParameterList();
                RebuildStatePresentation();
                SetStatus($"{parameterName} type = {type}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeParameterDefaultValue(string parameterName, object value)
        {
            try
            {
                m_Session.SetParameterDefaultValue(parameterName, value);
                SetStatus($"{parameterName} defaultValue 已更新。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private int CountStatesInChannel(string channelName)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (state != null && string.Equals(state.Config.channelName, channelName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private void AddState(string channelName)
        {
            try
            {
                string stateKey = m_Session.AddState(channelName);
                m_PendingStateRenameKey = stateKey;
                RebuildStatePresentation(includeChannelPresentation: true);
                SetStatus($"已在 {channelName} 新增 State {stateKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddAutoTransition()
        {
            try
            {
                string preStateKey = m_Session.AddAutoTransition(m_SelectedAutoTransitionStateKey);
                m_SelectedAutoTransitionStateKey = preStateKey;
                SetAutoTransitionExpanded(preStateKey, true);
                RebuildAutoTransitionEditor();
                RefreshChannelStates();
                SetStatus($"已新增 Auto Transition {preStateKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddDefaultTransition()
        {
            try
            {
                int transitionIndex = m_Session.AddDefaultTransition();
                m_SelectedDefaultTransitionIndex = transitionIndex;
                SetDefaultTransitionExpanded(transitionIndex, true);
                RebuildDefaultTransitionsEditor();
                RefreshChannelStates();
                SetStatus($"已新增 Default Transition {transitionIndex + 1}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteDefaultTransition(int transitionIndex)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("删除 Default Transition", $"确定删除 Default Transition #{transitionIndex + 1}？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteDefaultTransition(transitionIndex);
                m_SelectedDefaultTransitionIndex = Mathf.Clamp(transitionIndex - 1, -1, (m_Session.CompiledAsset.DefaultTransitions.Count) - 1);
                NormalizeCollapsedDefaultTransitionIndicesAfterDelete(transitionIndex);
                RebuildDefaultTransitionsEditor();
                RefreshChannelStates();
                SetStatus($"已删除 Default Transition {transitionIndex + 1}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddDefaultTransitionPair(int transitionIndex)
        {
            try
            {
                m_Session.AddDefaultTransitionPair(transitionIndex);
                m_SelectedDefaultTransitionIndex = transitionIndex;
                RebuildDefaultTransitionsEditor();
                RefreshChannelStates();
                SetStatus($"已新增 Default Transition pair。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteDefaultTransitionPair(int transitionIndex, int pairIndex)
        {
            try
            {
                m_Session.DeleteDefaultTransitionPair(transitionIndex, pairIndex);
                m_SelectedDefaultTransitionIndex = transitionIndex;
                RebuildDefaultTransitionsEditor();
                RefreshChannelStates();
                SetStatus($"已删除 Default Transition pair。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeDefaultTransitionPair(
            int transitionIndex,
            int pairIndex,
            string preStateKey,
            string nextStateKey,
            XAnimationEditorSelectionField changedField,
            string previousValue)
        {
            try
            {
                m_Session.SetDefaultTransitionPair(transitionIndex, pairIndex, preStateKey, nextStateKey, save: false);
                ScheduleAssetSave();
                RebuildDefaultTransitionsEditor();
                RefreshChannelStates();
                SetStatus($"Default Transition pair = {preStateKey} -> {nextStateKey}。");
            }
            catch (Exception ex)
            {
                changedField?.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RenameDefaultTransition(int transitionIndex, string oldName, string newName, EditableLabel label)
        {
            try
            {
                m_Session.SetDefaultTransitionName(transitionIndex, newName, save: false);
                ScheduleAssetSave();
                SetStatus($"Default Transition {transitionIndex + 1} name = {newName}。");
                RefreshSearchIndex();
            }
            catch (Exception ex)
            {
                label.text = oldName;
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private bool PlayDefaultTransitionPairPre(string preStateKey, string nextStateKey)
        {
            if (m_Session == null || !m_Session.IsLoaded ||
                string.IsNullOrWhiteSpace(preStateKey) || string.IsNullOrWhiteSpace(nextStateKey))
            {
                return false;
            }

            XAnimationCompiledAsset compiled = m_Session.CompiledAsset;
            if (!compiled.TryGetStateIndex(preStateKey, out _) || !compiled.TryGetStateIndex(nextStateKey, out _))
            {
                SetStatus($"无法预览：state 不存在。", true);
                return false;
            }

            m_IsPaused = false;
            m_Session.SetPaused(false);
            SetPauseButtonState(true, false);
            SetStepForwardButtonEnabled(true);
            m_Session.SetTimeScale(GetPlaybackSpeed());

            m_Session.PlayState(preStateKey);
            string preChannel = FindStateChannelName(preStateKey);
            if (!string.IsNullOrWhiteSpace(preChannel))
            {
                m_Session.SetChannelTimeScale(preChannel, GetPlaybackSpeed());
            }

            RefreshPlaybackViews();
            SetStatus($"正在播放 {preStateKey}，点击 ⏭ 切换到 {nextStateKey}。");
            return true;
        }

        private void PlayDefaultTransitionPairNext(string preStateKey, string nextStateKey)
        {
            if (m_Session == null || !m_Session.IsLoaded ||
                string.IsNullOrWhiteSpace(nextStateKey))
            {
                return;
            }

            m_Session.PlayState(nextStateKey);
            RefreshPlaybackViews();
            SetStatus($"Default Transition 切换: {preStateKey} -> {nextStateKey}。");
        }

        private void DeleteAutoTransition(string preStateKey)
        {
            if (string.IsNullOrWhiteSpace(preStateKey) ||
                !HasAutoTransition(preStateKey))
            {
                SetStatus("当前没有可删除的 Auto Transition。", true);
                return;
            }

            if (!EditorUtility.DisplayDialog("删除 Auto Transition", $"确定删除 Auto Transition '{preStateKey}'？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteAutoTransition(preStateKey);
                if (string.Equals(m_SelectedAutoTransitionStateKey, preStateKey, StringComparison.Ordinal))
                {
                    m_SelectedAutoTransitionStateKey = null;
                }

                m_CollapsedAutoTransitionKeys.Remove(preStateKey);
                RebuildAutoTransitionEditor();
                RefreshChannelStates();
                SetStatus($"已删除 Auto Transition {preStateKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteState(string stateKey)
        {
            if (!EditorUtility.DisplayDialog("删除 State", $"确定删除 State '{stateKey}'？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteState(stateKey);
                if (string.Equals(m_SelectedAutoTransitionStateKey, stateKey, StringComparison.Ordinal))
                {
                    m_SelectedAutoTransitionStateKey = null;
                }

                m_CollapsedAutoTransitionKeys.Remove(stateKey);
                m_SelectedDefaultTransitionIndex = -1;
                SetStateExpanded(stateKey, false);
                RebuildStateList();
                RebuildDefaultTransitionsEditor();
                RefreshStatePlaybackViews();
                SetStatus($"已删除 State {stateKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddClip()
        {
            try
            {
                string clipKey = m_Session.AddClip();
                m_PendingClipRenameKey = clipKey;
                RebuildStructureAndPlaybackViews();
                SetStatus($"已新增 Clip {clipKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteClip(string clipKey)
        {
            if (!EditorUtility.DisplayDialog("删除 Clip", $"确定删除 Clip '{clipKey}'？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteClip(clipKey);
                m_ExpandedClipKeys.Remove(clipKey);
                RebuildStructureAndPlaybackViews();
                SetStatus($"已删除 Clip {clipKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddCue(string clipKey)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            try
            {
                int cueIndex = m_Session.AddCue(clipKey);
                RebuildClipList();
                SetStatus($"已在 {clipKey} 新增 Cue #{cueIndex}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteCue(int cueIndex)
        {
            if (!EditorUtility.DisplayDialog("删除 Cue", $"确定删除 Cue #{cueIndex}？", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.DeleteCue(cueIndex);
                RebuildClipList();
                SetStatus($"已删除 Cue #{cueIndex}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCueClipKey(int cueIndex, string clipKey, DropdownField field, string previousValue)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                field.SetValueWithoutNotify(previousValue);
                return;
            }

            try
            {
                m_Session.SetCueClipKey(cueIndex, clipKey);
                RebuildClipList();
                SetStatus($"Cue #{cueIndex} clipKey = {clipKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCueTime(int cueIndex, float time, FloatField field)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            float clampedTime = Mathf.Clamp01(time);
            if (!Mathf.Approximately(clampedTime, time))
            {
                field.SetValueWithoutNotify(clampedTime);
            }

            try
            {
                m_Session.SetCueTime(cueIndex, clampedTime, save: false);
                ScheduleAssetSave();
                SetStatus($"Cue #{cueIndex} time = {clampedTime:0.###}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCueEventKey(int cueIndex, string eventKey, TextField field, string previousValue)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                field.SetValueWithoutNotify(previousValue);
                return;
            }

            try
            {
                m_Session.SetCueEventKey(cueIndex, eventKey);
                SetStatus($"Cue #{cueIndex} eventKey = {eventKey?.Trim()}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeCuePayload(int cueIndex, string payload)
        {
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return;
            }

            try
            {
                m_Session.SetCuePayload(cueIndex, payload);
                SetStatus($"Cue #{cueIndex} payload 已更新。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RenameClip(string oldKey, string newKey, EditableLabel label)
        {
            newKey = newKey?.Trim();
            try
            {
                m_Session.RenameClip(oldKey, newKey);
                SetStatus($"Clip {oldKey} 已重命名为 {newKey}。");
                if (m_ExpandedClipKeys.Remove(oldKey) && !string.IsNullOrWhiteSpace(newKey))
                {
                    m_ExpandedClipKeys.Add(newKey.Trim());
                }
                RebuildStructureAndPlaybackViews();
            }
            catch (Exception ex)
            {
                label.text = oldKey;
                SetStatus(ex.Message);
                Debug.LogException(ex);
            }
        }

        private void RenameChannel(string oldName, string newName, EditableLabel label)
        {
            newName = newName?.Trim();
            try
            {
                m_Session.RenameChannel(oldName, newName);
                SetStatus($"Channel {oldName} 已重命名为 {newName}。");
                RebuildStatePresentation(includeChannelPresentation: true);
                RefreshClipPlayingStates();
            }
            catch (Exception ex)
            {
                label.text = oldName;
                SetStatus(ex.Message);
                Debug.LogException(ex);
            }
        }

        private void RenameState(string oldKey, string newKey, EditableLabel label)
        {
            newKey = newKey?.Trim();
            try
            {
                m_Session.RenameState(oldKey, newKey);
                if (string.Equals(m_SelectedAutoTransitionStateKey, oldKey, StringComparison.Ordinal))
                {
                    m_SelectedAutoTransitionStateKey = newKey;
                }

                bool autoTransitionWasExpanded = m_CollapsedAutoTransitionKeys.Remove(oldKey);
                if (autoTransitionWasExpanded && !string.IsNullOrWhiteSpace(newKey))
                {
                    m_CollapsedAutoTransitionKeys.Add(newKey.Trim());
                }

                SetStatus($"State {oldKey} 已重命名为 {newKey}。");
                bool wasExpanded = m_ExpandedStateKeys.Remove(oldKey);
                m_StateEditorMap.Remove(oldKey);
                if (wasExpanded && !string.IsNullOrWhiteSpace(newKey))
                {
                    m_ExpandedStateKeys.Add(newKey.Trim());
                }
                RebuildStateList();
                RebuildDefaultTransitionsEditor();
                RefreshStatePlaybackViews();
            }
            catch (Exception ex)
            {
                label.text = oldKey;
                SetStatus(ex.Message);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateType(string stateKey, XAnimationStateType stateType, string previousValue, DropdownField field)
        {
            try
            {
                m_Session.SetStateType(stateKey, stateType);
                RebuildStatePresentation();
                SetStatus($"{stateKey} stateType = {stateType}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateChannel(string stateKey, string channelName, DropdownField field, string previousValue)
        {
            try
            {
                m_Session.SetStateChannel(stateKey, channelName);
                RebuildStatePresentation();
                SetStatus($"{stateKey} channel = {channelName}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateClipKey(string stateKey, string clipKey, XAnimationEditorSelectionField field, string previousValue)
        {
            try
            {
                m_Session.SetStateClipKey(stateKey, clipKey);
                RebuildStateList();
                RestartStateIfPlaying(stateKey, null);
                RefreshStatePlaybackViews();
                SetStatus($"{stateKey} clipKey = {clipKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateBlendParameter(string stateKey, string parameterName, DropdownField field, string previousValue)
        {
            try
            {
                m_Session.SetStateBlendParameter(stateKey, parameterName);
                RebuildStatePresentation();
                SetStatus($"{stateKey} parameter = {parameterName}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateDirectionalBlendParameters(
            string stateKey,
            string parameterXName,
            string parameterYName,
            DropdownField parameterXField,
            DropdownField parameterYField,
            string previousXValue,
            string previousYValue)
        {
            try
            {
                m_Session.SetStateDirectionalBlendParameters(stateKey, parameterXName, parameterYName);
                MarkFreeformStateInteracted(stateKey);
                RebuildStatePresentation();
                SetStatus($"{stateKey} parameters = ({parameterXName}, {parameterYName})。");
            }
            catch (Exception ex)
            {
                parameterXField.SetValueWithoutNotify(previousXValue);
                parameterYField.SetValueWithoutNotify(previousYValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateRootMotionMode(string stateKey, XAnimationClipRootMotionMode mode, string previousValue, DropdownField field)
        {
            try
            {
                m_Session.SetStateRootMotionMode(stateKey, mode);
                RestartStateIfPlaying(stateKey, null);
                RefreshChannelStates();
                SetStatus($"{stateKey} rootMotionMode = {mode}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddClip(string groupName)
        {
            try
            {
                string clipKey = m_Session.AddClip(groupName);
                m_PendingClipRenameKey = clipKey;
                RebuildClipPresentation();
                SetStatus($"已在 {groupName} 新增 Clip {clipKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddClipGroup()
        {
            string[] clipOptions = BuildClipGroupCandidateOptions();
            if (!TryPromptForClipGroupSetup("新建 Clip Group", out string groupName, out string selectedClipKey, clipOptions))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedClipKey))
            {
                try
                {
                    m_Session.SetClipEditorGroup(selectedClipKey, groupName);
                    RebuildClipPresentation();
                    SetStatus($"已创建 Clip Group {groupName}，并加入 clip {selectedClipKey}。");
                }
                catch (Exception ex)
                {
                    SetStatus(ex.Message, true);
                    Debug.LogException(ex);
                }

                return;
            }

            AddClip(groupName);
        }

        private void RenameClipGroup(string oldName, string newName, EditableLabel label)
        {
            newName = NormalizeClipEditorGroupName(newName);
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new XFrameworkException("Clip group name cannot be empty.");
                }

                m_Session.RenameClipEditorGroup(oldName, newName);
                RebuildClipPresentation();
                SetStatus($"Clip Group {oldName} 已重命名为 {newName}。");
            }
            catch (Exception ex)
            {
                label.text = oldName;
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteClipGroup(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("删除 Clip Group", $"确定删除 Clip Group '{groupName}'？\n\n组内 clips 会保留，并回到未分组区域。", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.ClearClipEditorGroup(groupName);
                RebuildClipPresentation();
                SetStatus($"已删除 Clip Group {groupName}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RenameStateGroup(string channelName, string oldName, string newName, EditableLabel label)
        {
            newName = NormalizeStateEditorGroupName(newName);
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                {
                    throw new XFrameworkException("State group name cannot be empty.");
                }

                m_Session.RenameStateEditorGroup(channelName, oldName, newName);
                SetStatus($"State Group {oldName} 已重命名为 {newName}。");
                RebuildStatePresentation();
            }
            catch (Exception ex)
            {
                label.text = oldName;
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteStateGroup(string channelName, string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("删除 State Group", $"确定删除 State Group '{channelName} / {groupName}'？\n\n组内 states 会保留，并回到未分组区域。", "删除", "取消"))
            {
                return;
            }

            try
            {
                m_Session.ClearStateEditorGroupForChannel(channelName, groupName);
                SetStatus($"已删除 State Group {channelName} / {groupName}。");
                RebuildStatePresentation();
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddState(string channelName, string groupName)
        {
            try
            {
                string stateKey = m_Session.AddState(channelName, groupName);
                m_PendingStateRenameKey = stateKey;
                RebuildStatePresentation(includeChannelPresentation: true);
                SetStatus($"已在 {channelName} / {groupName} 新增 State {stateKey}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddStateGroup(string channelName)
        {
            string[] stateOptions = BuildChannelStateGroupCandidateOptions(channelName);
            if (!TryPromptForStateGroupSetup("新建 State Group", channelName, out string groupName, out string selectedStateKey, stateOptions))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedStateKey))
            {
                try
                {
                    m_Session.SetStateEditorGroup(selectedStateKey, groupName);
                    RebuildStatePresentation();
                    SetStatus($"已创建 State Group {channelName} / {groupName}，并加入 state {selectedStateKey}。");
                }
                catch (Exception ex)
                {
                    SetStatus(ex.Message, true);
                    Debug.LogException(ex);
                }

                return;
            }

            AddState(channelName, groupName);
        }

        private bool TryPromptForStateGroupName(string title, string channelName, string currentGroupName, out string groupName)
        {
            groupName = null;
            string message = string.IsNullOrWhiteSpace(currentGroupName)
                ? $"为 Channel '{channelName}' 输入新的 State Group 名称："
                : $"为 Channel '{channelName}' 输入新的 State Group 名称：";
            if (!StringInputPromptWindow.ShowPrompt(title, message, currentGroupName, out string input))
            {
                return false;
            }

            input = NormalizeStateEditorGroupName(input);
            if (string.IsNullOrWhiteSpace(input))
            {
                SetStatus("State group name cannot be empty.", true);
                return false;
            }

            if (HasStateGroup(channelName, input) &&
                !string.Equals(NormalizeStateEditorGroupName(currentGroupName), input, StringComparison.Ordinal))
            {
                SetStatus($"Channel '{channelName}' 中已存在 State Group '{input}'。", true);
                return false;
            }

            groupName = input;
            return true;
        }

        private bool TryPromptForStateGroupSetup(string title, string channelName, out string groupName, out string selectedStateKey, string[] stateOptions)
        {
            groupName = null;
            selectedStateKey = null;
            string message = $"为 Channel '{channelName}' 输入新的 State Group 名称：";
            if (!StringInputPromptWindow.ShowPrompt(
                    title,
                    message,
                    string.Empty,
                    "加入现有 State",
                    stateOptions,
                    stateOptions != null && stateOptions.Length > 0 ? stateOptions[0] : string.Empty,
                    out string input,
                    out string selected))
            {
                return false;
            }

            input = NormalizeStateEditorGroupName(input);
            if (string.IsNullOrWhiteSpace(input))
            {
                SetStatus("State group name cannot be empty.", true);
                return false;
            }

            if (HasStateGroup(channelName, input))
            {
                SetStatus($"Channel '{channelName}' 中已存在 State Group '{input}'。", true);
                return false;
            }

            groupName = input;
            selectedStateKey = NormalizeStateGroupSelectedState(selected);
            return true;
        }

        private bool TryPromptForClipGroupSetup(string title, out string groupName, out string selectedClipKey, string[] clipOptions)
        {
            groupName = null;
            selectedClipKey = null;
            if (!StringInputPromptWindow.ShowPrompt(
                    title,
                    "输入新的 Clip Group 名称：",
                    string.Empty,
                    "加入现有 Clip",
                    clipOptions,
                    clipOptions != null && clipOptions.Length > 0 ? clipOptions[0] : string.Empty,
                    out string input,
                    out string selected))
            {
                return false;
            }

            input = NormalizeClipEditorGroupName(input);
            if (string.IsNullOrWhiteSpace(input))
            {
                SetStatus("Clip group name cannot be empty.", true);
                return false;
            }

            if (HasClipGroup(input))
            {
                SetStatus($"已存在 Clip Group '{input}'。", true);
                return false;
            }

            groupName = input;
            selectedClipKey = NormalizeClipGroupSelectedClip(selected);
            return true;
        }

        private string[] BuildClipGroupCandidateOptions()
        {
            List<string> options = new() { "<新建一个 clip>" };
            if (m_Session == null || !m_Session.IsLoaded)
            {
                return options.ToArray();
            }

            IReadOnlyList<XAnimationCompiledClip> clips = m_Session.CompiledAsset.Clips;
            for (int i = 0; i < clips.Count; i++)
            {
                XAnimationCompiledClip clip = clips[i];
                if (clip != null)
                {
                    options.Add(clip.Key);
                }
            }

            return options.ToArray();
        }

        private static string NormalizeClipGroupSelectedClip(string value)
        {
            return string.Equals(value, "<新建一个 clip>", StringComparison.Ordinal) ? string.Empty : value ?? string.Empty;
        }

        private string[] BuildChannelStateGroupCandidateOptions(string channelName)
        {
            List<string> options = new() { "<新建一个 state>" };
            if (m_Session == null || !m_Session.IsLoaded || string.IsNullOrWhiteSpace(channelName))
            {
                return options.ToArray();
            }

            IReadOnlyList<XAnimationCompiledState> states = m_Session.CompiledAsset.States;
            for (int i = 0; i < states.Count; i++)
            {
                XAnimationCompiledState state = states[i];
                if (state == null || !string.Equals(state.Config.channelName, channelName, StringComparison.Ordinal))
                {
                    continue;
                }

                options.Add(state.Key);
            }

            return options.ToArray();
        }

        private static string NormalizeStateGroupSelectedState(string value)
        {
            return string.Equals(value, "<新建一个 state>", StringComparison.Ordinal) ? string.Empty : value ?? string.Empty;
        }

        private void AddStateAllowedNextState(string stateKey)
        {
            try
            {
                m_Session.AddStateAllowedNextState(stateKey);
                RebuildStateList();
                SetStatus($"{stateKey} allowedNextStateKeys += 1。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddStateAllowedPreviousState(string stateKey)
        {
            try
            {
                m_Session.AddStateAllowedPreviousState(stateKey);
                RebuildStateList();
                SetStatus($"{stateKey} allowedPreviousStateKeys += 1。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteStateAllowedNextState(string stateKey, int index)
        {
            try
            {
                m_Session.DeleteStateAllowedNextState(stateKey, index);
                RebuildStateList();
                SetStatus($"{stateKey} 删除 allowedNextStateKeys[{index}]。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteStateAllowedPreviousState(string stateKey, int index)
        {
            try
            {
                m_Session.DeleteStateAllowedPreviousState(stateKey, index);
                RebuildStateList();
                SetStatus($"{stateKey} 删除 allowedPreviousStateKeys[{index}]。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateAllowedNextState(string stateKey, int index, string targetStateKey, XAnimationEditorSelectionField field, string previousValue)
        {
            try
            {
                m_Session.SetStateAllowedNextState(stateKey, index, targetStateKey);
                RebuildStateList();
                SetStatus($"{stateKey} allowedNextStateKeys[{index}] = {targetStateKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeStateAllowedPreviousState(string stateKey, int index, string sourceStateKey, XAnimationEditorSelectionField field, string previousValue)
        {
            try
            {
                m_Session.SetStateAllowedPreviousState(stateKey, index, sourceStateKey);
                RebuildStateList();
                SetStatus($"{stateKey} allowedPreviousStateKeys[{index}] = {sourceStateKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddBlendSample(string stateKey)
        {
            try
            {
                m_Session.AddBlendSample(stateKey);
                RebuildStatePresentation();
                SetStatus($"{stateKey} 已新增 Blend1D sample。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void AddDirectionalBlendSample(string stateKey)
        {
            try
            {
                m_Session.AddDirectionalBlendSample(stateKey);
                RebuildStatePresentation();
                SetStatus($"{stateKey} 已新增 2D directional blend sample。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteBlendSample(string stateKey, int sampleIndex)
        {
            try
            {
                m_Session.DeleteBlendSample(stateKey, sampleIndex);
                RebuildStatePresentation();
                SetStatus($"{stateKey} 已删除 Blend1D sample #{sampleIndex}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void DeleteDirectionalBlendSample(string stateKey, int sampleIndex)
        {
            try
            {
                m_Session.DeleteDirectionalBlendSample(stateKey, sampleIndex);
                RebuildStatePresentation();
                SetStatus($"{stateKey} 已删除 2D directional blend sample #{sampleIndex}。");
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeBlendSampleClipKey(string stateKey, int sampleIndex, string clipKey, XAnimationEditorSelectionField field, string previousValue)
        {
            try
            {
                m_Session.SetBlendSampleClipKey(stateKey, sampleIndex, clipKey);
                RebuildStatePresentation();
                SetStatus($"{stateKey} sample #{sampleIndex} clip = {clipKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeBlendSampleThreshold(string stateKey, int sampleIndex, float threshold, FloatField field, float previousValue)
        {
            try
            {
                m_Session.SetBlendSampleThreshold(stateKey, sampleIndex, threshold);
                RebuildStatePresentation();
                SetStatus($"{stateKey} sample #{sampleIndex} threshold = {threshold:0.###}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeDirectionalBlendSampleClipKey(string stateKey, int sampleIndex, string clipKey, XAnimationEditorSelectionField field, string previousValue)
        {
            try
            {
                m_Session.SetDirectionalBlendSampleClipKey(stateKey, sampleIndex, clipKey);
                MarkFreeformStateInteracted(stateKey);
                RebuildStatePresentation();
                SetStatus($"{stateKey} directional sample #{sampleIndex} clip = {clipKey}。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(previousValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void ChangeDirectionalBlendSamplePosition(
            string stateKey,
            int sampleIndex,
            float positionX,
            float positionY,
            FloatField field,
            float previousXValue,
            float previousYValue,
            bool isX)
        {
            try
            {
                XAnimationStateConfig stateConfig = m_Session.CompiledAsset.GetState(stateKey).Config;
                XAnimationBlend2DSimpleDirectionalSampleConfig sample =
                    stateConfig.directionalSamples[sampleIndex];
                float newX = isX ? positionX : sample.positionX;
                float newY = isX ? sample.positionY : positionY;
                m_Session.SetDirectionalBlendSamplePosition(stateKey, sampleIndex, newX, newY);
                MarkFreeformStateInteracted(stateKey);
                RebuildStatePresentation();
                SetStatus($"{stateKey} directional sample #{sampleIndex} position = ({newX:0.###}, {newY:0.###})。");
            }
            catch (Exception ex)
            {
                field.SetValueWithoutNotify(isX ? previousXValue : previousYValue);
                SetStatus(ex.Message, true);
                Debug.LogException(ex);
            }
        }

        private void RegisterStateChannelDropTarget(VisualElement group, VisualElement groupHeader, string channelName, string groupName)
        {
            group.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName, groupName))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                groupHeader.style.backgroundColor = AccentColor;
                evt.StopPropagation();
            });
            group.RegisterCallback<DragLeaveEvent>(_ => groupHeader.style.backgroundColor = ListHeaderBg);
            group.RegisterCallback<DragPerformEvent>(evt =>
            {
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName, groupName))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                groupHeader.style.backgroundColor = ListHeaderBg;
                MoveState(stateKey, channelName, insertBeforeStateKey: null, groupName);
                evt.StopPropagation();
            });
        }

        private void RegisterStateRowDropTarget(VisualElement row, string channelName, string insertBeforeStateKey, string groupName)
        {
            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName, groupName, insertBeforeStateKey))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                row.style.borderTopColor = AccentColor;
                row.style.borderTopWidth = 2;
                evt.StopPropagation();
            });
            row.RegisterCallback<DragLeaveEvent>(_ =>
            {
                row.style.borderTopColor = Color.clear;
                row.style.borderTopWidth = 0;
            });
            row.RegisterCallback<DragPerformEvent>(evt =>
            {
                string stateKey = DragAndDrop.GetGenericData(StateDragDataKey) as string;
                if (!CanDropState(stateKey, channelName, groupName, insertBeforeStateKey))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                row.style.borderTopColor = Color.clear;
                row.style.borderTopWidth = 0;
                MoveState(stateKey, channelName, insertBeforeStateKey, groupName);
                evt.StopPropagation();
            });
        }

        private void RegisterClipGroupDropTarget(VisualElement group, VisualElement groupHeader, string groupName)
        {
            group.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, groupName))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                groupHeader.style.backgroundColor = AccentColor;
                evt.StopPropagation();
            });
            group.RegisterCallback<DragLeaveEvent>(_ => groupHeader.style.backgroundColor = ListHeaderBg);
            group.RegisterCallback<DragPerformEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, groupName))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                groupHeader.style.backgroundColor = ListHeaderBg;
                MoveClipToGroup(clipKey, groupName);
                evt.StopPropagation();
            });
        }

        private void RegisterClipRowDropTarget(VisualElement row, string insertBeforeClipKey, string groupName)
        {
            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, groupName, insertBeforeClipKey))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                row.style.borderTopColor = AccentColor;
                row.style.borderTopWidth = 2;
                evt.StopPropagation();
            });
            row.RegisterCallback<DragLeaveEvent>(_ =>
            {
                row.style.borderTopColor = Color.clear;
                row.style.borderTopWidth = 0;
            });
            row.RegisterCallback<DragPerformEvent>(evt =>
            {
                string clipKey = DragAndDrop.GetGenericData(ClipDragDataKey) as string;
                if (!CanDropClip(clipKey, groupName, insertBeforeClipKey))
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                row.style.borderTopColor = Color.clear;
                row.style.borderTopWidth = 0;
                MoveClipToGroup(clipKey, groupName);
                evt.StopPropagation();
            });
        }

        private bool CanDropState(string stateKey, string channelName, string groupName, string insertBeforeStateKey = null)
        {
            if (m_IsEditingName ||
                m_Session == null || !m_Session.IsLoaded ||
                string.IsNullOrWhiteSpace(stateKey) ||
                string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            if (!m_StateChannelMap.TryGetValue(stateKey, out string currentChannel))
            {
                return false;
            }

            string currentGroupName = NormalizeStateEditorGroupName(m_Session.CompiledAsset.GetState(stateKey).Config.editorGroupName);
            string targetGroupName = NormalizeStateEditorGroupName(groupName);
            return !string.Equals(stateKey, insertBeforeStateKey, StringComparison.Ordinal) ||
                !string.Equals(currentChannel, channelName, StringComparison.Ordinal) ||
                !string.Equals(currentGroupName, targetGroupName, StringComparison.Ordinal);
        }

        private bool CanDropClip(string clipKey, string groupName, string insertBeforeClipKey = null)
        {
            if (m_IsEditingName ||
                m_Session == null || !m_Session.IsLoaded ||
                m_Session.IsOverrideAsset ||
                string.IsNullOrWhiteSpace(clipKey))
            {
                return false;
            }

            XAnimationCompiledClip clip = m_Session.CompiledAsset.GetClip(clipKey);
            if (clip == null)
            {
                return false;
            }

            string currentGroupName = NormalizeClipEditorGroupName(clip.Config.editorGroupName);
            string targetGroupName = NormalizeClipEditorGroupName(groupName);
            return !string.Equals(clipKey, insertBeforeClipKey, StringComparison.Ordinal) ||
                !string.Equals(currentGroupName, targetGroupName, StringComparison.Ordinal);
        }

        private void MoveState(string stateKey, string channelName, string insertBeforeStateKey = null, string groupName = null)
        {
            string normalizedGroup = NormalizeStateEditorGroupName(groupName);
            m_Session.MoveState(stateKey, channelName, insertBeforeStateKey, normalizedGroup);
            m_StateChannelMap[stateKey] = channelName;
            RebuildStatePresentation();
            SetStatus(string.IsNullOrWhiteSpace(normalizedGroup)
                ? $"{stateKey} 已移动到 {channelName}。"
                : $"{stateKey} 已移动到 {channelName} / {normalizedGroup}。");
        }

        private void TryBeginPendingRename()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(m_PendingClipRenameKey) &&
                m_ClipLabelMap.TryGetValue(m_PendingClipRenameKey, out EditableLabel clipLabel))
            {
                string clipKey = m_PendingClipRenameKey;
                m_PendingClipRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    if (clipLabel != null)
                    {
                        m_ExpandedClipKeys.Remove(clipKey);
                        clipLabel.BeginEdit();
                    }
                }).StartingIn(0);
            }

            if (!string.IsNullOrWhiteSpace(m_PendingStateRenameKey) &&
                m_StateLabelMap.TryGetValue(m_PendingStateRenameKey, out EditableLabel stateLabel))
            {
                string stateKey = m_PendingStateRenameKey;
                m_PendingStateRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    if (stateLabel != null)
                    {
                        SetStateExpanded(stateKey, false);
                        stateLabel.BeginEdit();
                    }
                }).StartingIn(0);
            }

            if (!string.IsNullOrWhiteSpace(m_PendingParameterRenameKey) &&
                m_ParameterLabelMap.TryGetValue(m_PendingParameterRenameKey, out EditableLabel parameterLabel))
            {
                m_PendingParameterRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    parameterLabel?.BeginEdit();
                }).StartingIn(0);
            }

            if (!string.IsNullOrWhiteSpace(m_PendingChannelRenameKey) &&
                m_ChannelLabelMap.TryGetValue(m_PendingChannelRenameKey, out EditableLabel channelLabel))
            {
                m_PendingChannelRenameKey = null;
                rootVisualElement.schedule.Execute(() =>
                {
                    channelLabel?.BeginEdit();
                }).StartingIn(0);
            }
        }

    }
}
#endif
