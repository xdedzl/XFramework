#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;

namespace XFramework.Editor
{
    internal sealed class XAnimationGraphDebugView : VisualElement
    {
        private static readonly Color PaneBg = new(0.18f, 0.18f, 0.19f, 1f);
        private static readonly Color SectionDivider = new(0.28f, 0.28f, 0.30f, 1f);
        private static readonly Color ToolbarBg = new(0.14f, 0.14f, 0.15f, 1f);
        private static readonly Color ListRowEvenBg = new(0.16f, 0.16f, 0.17f, 1f);
        private static readonly Color ListRowOddBg = new(0.19f, 0.19f, 0.20f, 1f);
        private static readonly Color HoverBg = new(0.24f, 0.24f, 0.26f, 1f);
        private static readonly Color SelectedBg = new(0.20f, 0.35f, 0.55f, 0.85f);
        private static readonly Color ActiveAccent = new(0.30f, 0.72f, 0.34f, 1f);
        private static readonly Color InactiveAccent = new(0.45f, 0.45f, 0.48f, 1f);
        private static readonly Color TextMuted = new(0.60f, 0.60f, 0.62f, 1f);
        private static readonly Color TextNormal = new(0.85f, 0.85f, 0.87f, 1f);

        private readonly Func<XAnimationDebugGraphSnapshot> m_SnapshotProvider;
        private readonly List<RowEntry> m_Rows = new();

        private Toggle m_OnlyActiveToggle;
        private Toggle m_ShowZeroWeightToggle;
        private TextField m_SearchField;
        private Label m_HeaderLabel;
        private Label m_EmptyLabel;
        private ScrollView m_TreeScrollView;
        private ScrollView m_DetailsScrollView;
        private VisualElement m_TreeContainer;
        private VisualElement m_DetailsContainer;
        private XAnimationDebugGraphSnapshot m_Snapshot;
        private XAnimationDebugNodeSnapshot m_SelectedNode;
        private int m_RowIndex;

        private sealed class RowEntry
        {
            public XAnimationDebugNodeSnapshot Node;
            public VisualElement Row;
        }

        public XAnimationGraphDebugView(Func<XAnimationDebugGraphSnapshot> snapshotProvider)
        {
            m_SnapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            style.flexDirection = FlexDirection.Column;
            style.flexGrow = 1;
            style.minHeight = 0;
            style.backgroundColor = PaneBg;
            BuildUi();
        }

        public void Refresh()
        {
            m_Snapshot = m_SnapshotProvider();
            RebuildTree();
            RefreshDetails();
        }

        private void BuildUi()
        {
            VisualElement toolbar = new();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 4;
            toolbar.style.paddingRight = 4;
            toolbar.style.paddingTop = 3;
            toolbar.style.paddingBottom = 3;
            toolbar.style.backgroundColor = ToolbarBg;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = SectionDivider;
            toolbar.style.flexShrink = 0;
            Add(toolbar);

            m_HeaderLabel = new("Graph Debugger");
            m_HeaderLabel.style.color = TextNormal;
            m_HeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_HeaderLabel.style.fontSize = 12;
            m_HeaderLabel.style.flexGrow = 1;
            toolbar.Add(m_HeaderLabel);

            m_OnlyActiveToggle = new Toggle("Only Active") { value = false };
            ConfigureToolbarToggle(m_OnlyActiveToggle);
            m_OnlyActiveToggle.RegisterValueChangedCallback(_ => RebuildTree());
            toolbar.Add(m_OnlyActiveToggle);

            m_ShowZeroWeightToggle = new Toggle("Show Zero") { value = true };
            ConfigureToolbarToggle(m_ShowZeroWeightToggle);
            m_ShowZeroWeightToggle.RegisterValueChangedCallback(_ => RebuildTree());
            toolbar.Add(m_ShowZeroWeightToggle);

            m_SearchField = new TextField();
            m_SearchField.tooltip = "搜索节点类型、channel、state、clip、详情。";
            m_SearchField.style.width = 180;
            m_SearchField.style.marginLeft = 8;
            m_SearchField.RegisterValueChangedCallback(_ => RebuildTree());
            toolbar.Add(m_SearchField);

            VisualElement split = new();
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            split.style.minHeight = 0;
            Add(split);

            m_TreeScrollView = new ScrollView();
            m_TreeScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_TreeScrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            m_TreeScrollView.style.flexGrow = 1;
            m_TreeScrollView.style.minWidth = 320;
            m_TreeScrollView.style.minHeight = 0;
            split.Add(m_TreeScrollView);

            m_TreeContainer = new VisualElement();
            m_TreeScrollView.Add(m_TreeContainer);

            VisualElement divider = new();
            divider.style.width = 1;
            divider.style.backgroundColor = SectionDivider;
            split.Add(divider);

            m_DetailsScrollView = new ScrollView();
            m_DetailsScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_DetailsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_DetailsScrollView.style.width = 300;
            m_DetailsScrollView.style.minWidth = 240;
            m_DetailsScrollView.style.minHeight = 0;
            split.Add(m_DetailsScrollView);

            m_DetailsContainer = new VisualElement();
            m_DetailsContainer.style.paddingLeft = 6;
            m_DetailsContainer.style.paddingRight = 6;
            m_DetailsContainer.style.paddingTop = 6;
            m_DetailsContainer.style.paddingBottom = 6;
            m_DetailsScrollView.Add(m_DetailsContainer);

            m_EmptyLabel = new();
            m_EmptyLabel.style.color = TextMuted;
            m_EmptyLabel.style.fontSize = 12;
            m_EmptyLabel.style.whiteSpace = WhiteSpace.Normal;
            m_EmptyLabel.style.paddingLeft = 8;
            m_EmptyLabel.style.paddingRight = 8;
            m_EmptyLabel.style.paddingTop = 8;
            m_EmptyLabel.style.paddingBottom = 8;
        }

        private static void ConfigureToolbarToggle(Toggle toggle)
        {
            toggle.style.marginLeft = 8;
            toggle.style.marginRight = 0;
            toggle.style.color = TextMuted;
            toggle.style.fontSize = 11;
        }

        private void RebuildTree()
        {
            m_TreeContainer.Clear();
            m_Rows.Clear();
            m_RowIndex = 0;

            if (m_Snapshot == null)
            {
                m_HeaderLabel.text = "Graph Debugger";
                ShowEmpty("No snapshot provider result.");
                return;
            }

            string graphName = string.IsNullOrWhiteSpace(m_Snapshot.graphName) ? "<invalid graph>" : m_Snapshot.graphName;
            string state = m_Snapshot.isValid ? m_Snapshot.isPlaying ? "Playing" : "Stopped" : "Invalid";
            m_HeaderLabel.text = $"{graphName} | {state} | Animator: {EmptyAsDash(m_Snapshot.animatorName)}";

            if (!m_Snapshot.isValid)
            {
                ShowEmpty(string.IsNullOrWhiteSpace(m_Snapshot.message) ? "PlayableGraph is invalid." : m_Snapshot.message);
                return;
            }

            if (m_Snapshot.rootNodes == null || m_Snapshot.rootNodes.Length == 0)
            {
                ShowEmpty("Graph snapshot has no nodes.");
                return;
            }

            string query = m_SearchField?.value?.Trim() ?? string.Empty;
            for (int i = 0; i < m_Snapshot.rootNodes.Length; i++)
            {
                AddNodeRecursive(m_Snapshot.rootNodes[i], 0, query);
            }

            if (m_Rows.Count == 0)
            {
                ShowEmpty("No graph nodes match the current filters.");
                return;
            }

            if (m_SelectedNode == null || !ContainsNode(m_Snapshot.rootNodes, m_SelectedNode.id))
            {
                m_SelectedNode = m_Rows[0].Node;
            }

            ApplySelectionVisuals();
        }

        private void ShowEmpty(string text)
        {
            m_TreeContainer.Clear();
            m_EmptyLabel.text = text;
            m_TreeContainer.Add(m_EmptyLabel);
            m_SelectedNode = null;
            RefreshDetails();
        }

        private void AddNodeRecursive(XAnimationDebugNodeSnapshot node, int depth, string query)
        {
            if (node == null || !PassesFilters(node, query))
            {
                return;
            }

            VisualElement row = CreateNodeRow(node, depth, m_RowIndex++);
            m_TreeContainer.Add(row);
            m_Rows.Add(new RowEntry { Node = node, Row = row });

            XAnimationDebugNodeSnapshot[] children = node.children ?? Array.Empty<XAnimationDebugNodeSnapshot>();
            for (int i = 0; i < children.Length; i++)
            {
                AddNodeRecursive(children[i], depth + 1, query);
            }
        }

        private bool PassesFilters(XAnimationDebugNodeSnapshot node, string query)
        {
            bool showZero = m_ShowZeroWeightToggle == null || m_ShowZeroWeightToggle.value;
            bool onlyActive = m_OnlyActiveToggle != null && m_OnlyActiveToggle.value;
            bool selfVisible = (!onlyActive || node.isActive) &&
                               (showZero || node.effectiveWeight > 0.0001f || node.inputWeight > 0.0001f || node.children?.Length > 0);

            if (!string.IsNullOrWhiteSpace(query))
            {
                selfVisible &= NodeMatchesQuery(node, query);
            }

            if (selfVisible)
            {
                return true;
            }

            XAnimationDebugNodeSnapshot[] children = node.children ?? Array.Empty<XAnimationDebugNodeSnapshot>();
            for (int i = 0; i < children.Length; i++)
            {
                if (PassesFilters(children[i], query))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NodeMatchesQuery(XAnimationDebugNodeSnapshot node, string query)
        {
            return Contains(node.displayName, query) ||
                   Contains(node.playableType, query) ||
                   Contains(node.channelName, query) ||
                   Contains(node.stateKey, query) ||
                   Contains(node.clipKey, query) ||
                   Contains(node.details, query);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement CreateNodeRow(XAnimationDebugNodeSnapshot node, int depth, int rowIndex)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 24;
            row.style.paddingLeft = 4 + depth * 16;
            row.style.paddingRight = 4;
            row.style.paddingTop = 2;
            row.style.paddingBottom = 2;
            row.style.backgroundColor = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = SectionDivider;
            row.userData = rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;

            VisualElement accent = new();
            accent.style.width = 3;
            accent.style.height = 16;
            accent.style.marginRight = 5;
            accent.style.backgroundColor = node.isActive ? ActiveAccent : InactiveAccent;
            row.Add(accent);

            Label title = new(BuildNodeTitle(node));
            title.style.color = TextNormal;
            title.style.fontSize = 11;
            title.style.unityFontStyleAndWeight = node.isActive ? FontStyle.Bold : FontStyle.Normal;
            title.style.flexGrow = 1;
            title.style.minWidth = 0;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            row.Add(title);

            Label meta = new(BuildNodeMeta(node));
            meta.style.color = TextMuted;
            meta.style.fontSize = 10;
            meta.style.unityTextAlign = TextAnchor.MiddleRight;
            meta.style.flexShrink = 0;
            row.Add(meta);

            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (m_SelectedNode == null || m_SelectedNode.id != node.id)
                {
                    row.style.backgroundColor = HoverBg;
                }
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (m_SelectedNode == null || m_SelectedNode.id != node.id)
                {
                    row.style.backgroundColor = (Color)row.userData;
                }
            });
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                m_SelectedNode = node;
                ApplySelectionVisuals();
                RefreshDetails();
                evt.StopPropagation();
            });

            return row;
        }

        private static string BuildNodeTitle(XAnimationDebugNodeSnapshot node)
        {
            string name = string.IsNullOrWhiteSpace(node.displayName) ? node.playableType : node.displayName;
            string input = node.inputIndex >= 0 ? $"[{node.inputIndex}] " : string.Empty;
            string state = string.IsNullOrWhiteSpace(node.stateKey) ? string.Empty : $" | state={node.stateKey}";
            string clip = string.IsNullOrWhiteSpace(node.clipKey) ? string.Empty : $" | clip={node.clipKey}";
            return $"{input}{name} ({node.playableType}){state}{clip}";
        }

        private static string BuildNodeMeta(XAnimationDebugNodeSnapshot node)
        {
            List<string> parts = new();
            if (node.inputIndex >= 0)
            {
                parts.Add($"w={node.inputWeight:0.###}");
            }

            if (node.effectiveWeight > 0f || node.inputIndex >= 0)
            {
                parts.Add($"eff={node.effectiveWeight:0.###}");
            }

            if (node.playbackId > 0)
            {
                parts.Add($"id={node.playbackId}");
                parts.Add($"t={node.normalizedTime:0.###}");
            }

            return string.Join("  ", parts);
        }

        private void ApplySelectionVisuals()
        {
            for (int i = 0; i < m_Rows.Count; i++)
            {
                RowEntry entry = m_Rows[i];
                bool selected = m_SelectedNode != null && entry.Node.id == m_SelectedNode.id;
                entry.Row.style.backgroundColor = selected ? SelectedBg : (Color)entry.Row.userData;
            }
        }

        private void RefreshDetails()
        {
            if (m_DetailsContainer == null)
            {
                return;
            }

            m_DetailsContainer.Clear();
            if (m_SelectedNode == null)
            {
                AddDetailsLabel("Select a graph node to inspect details.", TextMuted, FontStyle.Normal);
                if (m_Snapshot?.channels != null && m_Snapshot.channels.Length > 0)
                {
                    AddDetailsSpacer();
                    AddDetailsLabel("Channels", TextNormal, FontStyle.Bold);
                    for (int i = 0; i < m_Snapshot.channels.Length; i++)
                    {
                        AddChannelSummary(m_Snapshot.channels[i]);
                    }
                }
                return;
            }

            AddDetailsLabel(m_SelectedNode.displayName, TextNormal, FontStyle.Bold, 13);
            AddDetailsRow("Type", m_SelectedNode.playableType);
            AddDetailsRow("Input", m_SelectedNode.inputIndex >= 0 ? m_SelectedNode.inputIndex.ToString() : "-");
            AddDetailsRow("Connected", m_SelectedNode.isConnected ? "Yes" : "No");
            AddDetailsRow("Active", m_SelectedNode.isActive ? "Yes" : "No");
            AddDetailsRow("Input Weight", FormatFloat(m_SelectedNode.inputWeight));
            AddDetailsRow("Effective Weight", FormatFloat(m_SelectedNode.effectiveWeight));
            AddDetailsRow("Channel", EmptyAsDash(m_SelectedNode.channelName));
            AddDetailsRow("State", EmptyAsDash(m_SelectedNode.stateKey));
            AddDetailsRow("Clip", EmptyAsDash(m_SelectedNode.clipKey));
            AddDetailsRow("Playback Id", m_SelectedNode.playbackId > 0 ? m_SelectedNode.playbackId.ToString() : "-");
            AddDetailsRow("Normalized Time", FormatFloat(m_SelectedNode.normalizedTime));
            AddDetailsRow("Total Time", FormatFloat(m_SelectedNode.totalNormalizedTime));
            AddDetailsRow("Speed", FormatFloat(m_SelectedNode.speed));
            AddDetailsRow("Loop", m_SelectedNode.isLooping ? "Yes" : "No");
            AddDetailsRow("Fading", m_SelectedNode.isFading ? "Yes" : "No");
            AddDetailsRow("Transition", m_SelectedNode.isTransitioning ? m_SelectedNode.transitionSource.ToString() : "-");
            AddDetailsRow("Root Motion", m_SelectedNode.drivesRootMotion ? "Drives" : m_SelectedNode.canDriveRootMotion ? "Allowed" : "-");
            AddDetailsRow("Avatar Mask", m_SelectedNode.hasAvatarMask ? EmptyAsDash(m_SelectedNode.avatarMaskName) : "-");
            AddDetailsRow("Last Reject", m_SelectedNode.lastRejectReason == XAnimationTransitionRejectReason.None ? "-" : m_SelectedNode.lastRejectReason.ToString());

            if (!string.IsNullOrWhiteSpace(m_SelectedNode.details))
            {
                AddDetailsSpacer();
                AddDetailsLabel(m_SelectedNode.details, TextMuted, FontStyle.Normal);
            }
        }

        private void AddChannelSummary(XAnimationDebugChannelSnapshot channel)
        {
            if (channel == null)
            {
                return;
            }

            AddDetailsRow(
                channel.name,
                $"{channel.layerType} | layer={channel.layerWeight:0.###} | channel={channel.channelWeight:0.###} | current={EmptyAsDash(channel.currentStateKey)}");
        }

        private void AddDetailsRow(string name, string value)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 3;
            m_DetailsContainer.Add(row);

            Label key = new(name);
            key.style.width = 104;
            key.style.flexShrink = 0;
            key.style.color = TextMuted;
            key.style.fontSize = 11;
            row.Add(key);

            Label val = new(value ?? string.Empty);
            val.style.flexGrow = 1;
            val.style.color = TextNormal;
            val.style.fontSize = 11;
            val.style.whiteSpace = WhiteSpace.Normal;
            row.Add(val);
        }

        private void AddDetailsLabel(string text, Color color, FontStyle fontStyle, int fontSize = 11)
        {
            Label label = new(text);
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.fontSize = fontSize;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            m_DetailsContainer.Add(label);
        }

        private void AddDetailsSpacer()
        {
            VisualElement spacer = new();
            spacer.style.height = 8;
            m_DetailsContainer.Add(spacer);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###");
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static bool ContainsNode(XAnimationDebugNodeSnapshot[] nodes, int nodeId)
        {
            if (nodes == null)
            {
                return false;
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                XAnimationDebugNodeSnapshot node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (node.id == nodeId || ContainsNode(node.children, nodeId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
