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
        private const float CanvasPadding = 28f;
        private const float NodeWidth = 198f;
        private const float NodeHeight = 76f;
        private const float ColumnPitch = 268f;
        private const float RowPitch = 104f;
        private const float DetailsWidth = 310f;
        private const float EdgeLabelWidth = 86f;
        private const float EdgeLabelHeight = 18f;
        private const float MinZoom = 0.35f;
        private const float MaxZoom = 2.5f;
        private const float WheelZoomBase = 1.12f;

        private static readonly Color PaneBg = new(0.15f, 0.15f, 0.16f, 1f);
        private static readonly Color CanvasBg = new(0.105f, 0.11f, 0.12f, 1f);
        private static readonly Color CanvasGrid = new(0.76f, 0.77f, 0.75f, 0.08f);
        private static readonly Color CanvasGridMajor = new(0.76f, 0.77f, 0.75f, 0.13f);
        private static readonly Color SectionDivider = new(0.28f, 0.28f, 0.30f, 1f);
        private static readonly Color ToolbarBg = new(0.14f, 0.14f, 0.15f, 1f);
        private static readonly Color NodeBg = new(0.20f, 0.21f, 0.23f, 0.98f);
        private static readonly Color NodeBgInactive = new(0.17f, 0.17f, 0.18f, 0.96f);
        private static readonly Color NodeBorder = new(0.34f, 0.35f, 0.38f, 1f);
        private static readonly Color SelectedBorder = new(0.48f, 0.74f, 1f, 1f);
        private static readonly Color ActiveAccent = new(0.34f, 0.78f, 0.42f, 1f);
        private static readonly Color InactiveAccent = new(0.45f, 0.45f, 0.48f, 1f);
        private static readonly Color TextMuted = new(0.60f, 0.60f, 0.62f, 1f);
        private static readonly Color TextNormal = new(0.86f, 0.86f, 0.88f, 1f);
        private static readonly Color EdgeActive = new(0.45f, 0.78f, 1f, 1f);
        private static readonly Color EdgeInactive = new(0.62f, 0.62f, 0.66f, 1f);

        private readonly Func<XAnimationDebugGraphSnapshot> m_SnapshotProvider;
        private readonly List<NodeLayout> m_Layouts = new();
        private readonly List<EdgeLayout> m_Edges = new();
        private readonly List<NodeLayout> m_RootLayouts = new();
        private readonly Dictionary<int, VisualElement> m_NodeElements = new();

        private Toggle m_OnlyActiveToggle;
        private Toggle m_ShowZeroWeightToggle;
        private TextField m_SearchField;
        private Label m_HeaderLabel;
        private ScrollView m_GraphScrollView;
        private ScrollView m_DetailsScrollView;
        private VisualElement m_Canvas;
        private VisualElement m_EdgeCanvas;
        private VisualElement m_EdgeLabelLayer;
        private VisualElement m_NodeLayer;
        private VisualElement m_DetailsContainer;
        private XAnimationDebugGraphSnapshot m_Snapshot;
        private XAnimationDebugNodeSnapshot m_SelectedNode;
        private float m_NextLeafY;
        private float m_CanvasWidth;
        private float m_CanvasHeight;
        private float m_CanvasVisualWidth;
        private float m_CanvasVisualHeight;
        private Vector2 m_CanvasOrigin;
        private float m_Zoom = 1f;
        private bool m_IsPanning;
        private bool m_CanvasScrollInitialized;
        private int m_PanPointerId = PointerId.invalidPointerId;
        private Vector2 m_PanStartPointer;
        private Vector2 m_PanStartScrollOffset;

        private sealed class NodeLayout
        {
            public XAnimationDebugNodeSnapshot Node;
            public NodeLayout Parent;
            public readonly List<NodeLayout> Children = new();
            public int Depth;
            public Vector2 Position;
            public Rect Rect => new(Position.x, Position.y, NodeWidth, NodeHeight);
        }

        private readonly struct EdgeLayout
        {
            public EdgeLayout(NodeLayout parent, NodeLayout child)
            {
                Parent = parent;
                Child = child;
            }

            public NodeLayout Parent { get; }
            public NodeLayout Child { get; }
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
            RebuildGraph();
            RefreshDetails();
        }

        private void BuildUi()
        {
            VisualElement toolbar = new();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 6;
            toolbar.style.paddingRight = 6;
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
            m_OnlyActiveToggle.RegisterValueChangedCallback(_ => RebuildGraph());
            toolbar.Add(m_OnlyActiveToggle);

            m_ShowZeroWeightToggle = new Toggle("Show Zero") { value = true };
            ConfigureToolbarToggle(m_ShowZeroWeightToggle);
            m_ShowZeroWeightToggle.RegisterValueChangedCallback(_ => RebuildGraph());
            toolbar.Add(m_ShowZeroWeightToggle);

            m_SearchField = new TextField();
            m_SearchField.tooltip = "搜索节点类型、channel、state、clip、详情。";
            m_SearchField.style.width = 190;
            m_SearchField.style.marginLeft = 8;
            m_SearchField.RegisterValueChangedCallback(_ => RebuildGraph());
            toolbar.Add(m_SearchField);

            VisualElement split = new();
            split.style.flexDirection = FlexDirection.Row;
            split.style.flexGrow = 1;
            split.style.minHeight = 0;
            Add(split);

            m_GraphScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            m_GraphScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            m_GraphScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_GraphScrollView.style.flexGrow = 1;
            m_GraphScrollView.style.minWidth = 420;
            m_GraphScrollView.style.minHeight = 0;
            m_GraphScrollView.RegisterCallback<GeometryChangedEvent>(_ => RefreshCanvasViewport());
            split.Add(m_GraphScrollView);

            m_Canvas = new VisualElement();
            m_Canvas.style.position = Position.Relative;
            m_Canvas.style.backgroundColor = CanvasBg;
            m_Canvas.focusable = true;
            m_Canvas.RegisterCallback<WheelEvent>(OnGraphWheel);
            m_Canvas.RegisterCallback<PointerDownEvent>(OnGraphPointerDown);
            m_Canvas.RegisterCallback<PointerMoveEvent>(OnGraphPointerMove);
            m_Canvas.RegisterCallback<PointerUpEvent>(OnGraphPointerUp);
            m_Canvas.RegisterCallback<PointerCancelEvent>(OnGraphPointerCancel);
            m_Canvas.RegisterCallback<PointerCaptureOutEvent>(_ => EndPan());
            m_GraphScrollView.Add(m_Canvas);

            m_EdgeCanvas = new VisualElement();
            m_EdgeCanvas.style.position = Position.Absolute;
            m_EdgeCanvas.style.left = 0;
            m_EdgeCanvas.style.top = 0;
            m_EdgeCanvas.pickingMode = PickingMode.Ignore;
            m_EdgeCanvas.generateVisualContent += OnGenerateGraphVisualContent;
            m_Canvas.Add(m_EdgeCanvas);

            m_EdgeLabelLayer = new VisualElement();
            m_EdgeLabelLayer.style.position = Position.Absolute;
            m_EdgeLabelLayer.style.left = 0;
            m_EdgeLabelLayer.style.top = 0;
            m_EdgeLabelLayer.pickingMode = PickingMode.Ignore;
            m_Canvas.Add(m_EdgeLabelLayer);

            m_NodeLayer = new VisualElement();
            m_NodeLayer.style.position = Position.Absolute;
            m_NodeLayer.style.left = 0;
            m_NodeLayer.style.top = 0;
            m_Canvas.Add(m_NodeLayer);

            VisualElement divider = new();
            divider.style.width = 1;
            divider.style.backgroundColor = SectionDivider;
            split.Add(divider);

            m_DetailsScrollView = new ScrollView();
            m_DetailsScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            m_DetailsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            m_DetailsScrollView.style.width = DetailsWidth;
            m_DetailsScrollView.style.minWidth = 250;
            m_DetailsScrollView.style.minHeight = 0;
            split.Add(m_DetailsScrollView);

            m_DetailsContainer = new VisualElement();
            m_DetailsContainer.style.paddingLeft = 8;
            m_DetailsContainer.style.paddingRight = 8;
            m_DetailsContainer.style.paddingTop = 8;
            m_DetailsContainer.style.paddingBottom = 8;
            m_DetailsScrollView.Add(m_DetailsContainer);
        }

        private static void ConfigureToolbarToggle(Toggle toggle)
        {
            toggle.style.marginLeft = 8;
            toggle.style.marginRight = 0;
            toggle.style.color = TextMuted;
            toggle.style.fontSize = 11;
        }

        private void RebuildGraph()
        {
            m_RootLayouts.Clear();
            m_Layouts.Clear();
            m_Edges.Clear();
            m_NodeElements.Clear();
            m_EdgeLabelLayer.Clear();
            m_NodeLayer.Clear();
            m_NextLeafY = CanvasPadding;

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
                NodeLayout root = BuildVisibleLayout(m_Snapshot.rootNodes[i], null, 0, query);
                if (root != null)
                {
                    m_RootLayouts.Add(root);
                }
            }

            if (m_RootLayouts.Count == 0)
            {
                ShowEmpty("No graph nodes match the current filters.");
                return;
            }

            for (int i = 0; i < m_RootLayouts.Count; i++)
            {
                AssignY(m_RootLayouts[i]);
                m_NextLeafY += RowPitch * 0.35f;
            }

            m_CanvasWidth = CanvasPadding * 2f + (GetMaxDepth() + 1) * NodeWidth + GetMaxDepth() * (ColumnPitch - NodeWidth);
            m_CanvasHeight = Mathf.Max(260f, m_NextLeafY + CanvasPadding);
            ApplyCanvasSize(m_CanvasWidth, m_CanvasHeight);

            for (int i = 0; i < m_Layouts.Count; i++)
            {
                CreateNodeElement(m_Layouts[i]);
            }

            CreateEdgeLabels();

            if (m_SelectedNode == null || !ContainsLayout(m_SelectedNode.id))
            {
                m_SelectedNode = m_Layouts.Count > 0 ? m_Layouts[0].Node : null;
            }

            ApplySelectionVisuals();
            m_EdgeCanvas.MarkDirtyRepaint();
        }

        private NodeLayout BuildVisibleLayout(
            XAnimationDebugNodeSnapshot node,
            NodeLayout parent,
            int depth,
            string query)
        {
            if (node == null)
            {
                return null;
            }

            NodeLayout layout = new()
            {
                Node = node,
                Parent = parent,
                Depth = depth,
                Position = new Vector2(CanvasPadding + depth * ColumnPitch, CanvasPadding),
            };

            XAnimationDebugNodeSnapshot[] children = node.children ?? Array.Empty<XAnimationDebugNodeSnapshot>();
            for (int i = 0; i < children.Length; i++)
            {
                NodeLayout child = BuildVisibleLayout(children[i], layout, depth + 1, query);
                if (child != null)
                {
                    layout.Children.Add(child);
                    m_Edges.Add(new EdgeLayout(layout, child));
                }
            }

            bool visible = PassesSelfFilters(node, query) || layout.Children.Count > 0;
            if (!visible)
            {
                return null;
            }

            m_Layouts.Add(layout);
            return layout;
        }

        private void AssignY(NodeLayout layout)
        {
            if (layout.Children.Count == 0)
            {
                layout.Position = new Vector2(CanvasPadding + layout.Depth * ColumnPitch, m_NextLeafY);
                m_NextLeafY += RowPitch;
                return;
            }

            for (int i = 0; i < layout.Children.Count; i++)
            {
                AssignY(layout.Children[i]);
            }

            float y = 0f;
            for (int i = 0; i < layout.Children.Count; i++)
            {
                y += layout.Children[i].Position.y;
            }

            y /= layout.Children.Count;
            layout.Position = new Vector2(CanvasPadding + layout.Depth * ColumnPitch, y);
        }

        private bool PassesSelfFilters(XAnimationDebugNodeSnapshot node, string query)
        {
            bool showZero = m_ShowZeroWeightToggle == null || m_ShowZeroWeightToggle.value;
            bool onlyActive = m_OnlyActiveToggle != null && m_OnlyActiveToggle.value;
            bool visible = (!onlyActive || node.isActive) &&
                           (showZero || node.effectiveWeight > 0.0001f || node.inputWeight > 0.0001f);

            if (!string.IsNullOrWhiteSpace(query))
            {
                visible &= NodeMatchesQuery(node, query);
            }

            return visible;
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

        private void ApplyCanvasSize(float width, float height)
        {
            float scaledWidth = Scale(width);
            float scaledHeight = Scale(height);
            Vector2 viewportSize = GetGraphViewportSize();
            Vector2 previousOrigin = m_CanvasOrigin;
            Vector2 previousOffset = GetScrollOffset();
            m_CanvasOrigin = GetCanvasOrigin(viewportSize);
            m_CanvasVisualWidth = Mathf.Max(scaledWidth + m_CanvasOrigin.x * 2f, viewportSize.x);
            m_CanvasVisualHeight = Mathf.Max(scaledHeight + m_CanvasOrigin.y * 2f, viewportSize.y);

            m_Canvas.style.width = m_CanvasVisualWidth;
            m_Canvas.style.height = m_CanvasVisualHeight;
            m_Canvas.style.minWidth = m_CanvasVisualWidth;
            m_Canvas.style.minHeight = m_CanvasVisualHeight;

            m_EdgeCanvas.style.width = m_CanvasVisualWidth;
            m_EdgeCanvas.style.height = m_CanvasVisualHeight;
            m_EdgeLabelLayer.style.width = m_CanvasVisualWidth;
            m_EdgeLabelLayer.style.height = m_CanvasVisualHeight;
            m_NodeLayer.style.width = m_CanvasVisualWidth;
            m_NodeLayer.style.height = m_CanvasVisualHeight;

            if (m_CanvasScrollInitialized)
            {
                SetScrollOffset(ClampScrollOffset(previousOffset + m_CanvasOrigin - previousOrigin));
                return;
            }

            m_CanvasScrollInitialized = true;
            SetScrollOffset(ClampScrollOffset(m_CanvasOrigin));
        }

        private void RefreshCanvasViewport()
        {
            if (m_CanvasWidth <= 0f || m_CanvasHeight <= 0f)
            {
                return;
            }

            float previousWidth = m_CanvasVisualWidth;
            float previousHeight = m_CanvasVisualHeight;
            ApplyCanvasSize(m_CanvasWidth, m_CanvasHeight);
            SetScrollOffset(ClampScrollOffset(GetScrollOffset()));

            if (!Mathf.Approximately(previousWidth, m_CanvasVisualWidth) ||
                !Mathf.Approximately(previousHeight, m_CanvasVisualHeight))
            {
                m_EdgeCanvas.MarkDirtyRepaint();
            }
        }

        private int GetMaxDepth()
        {
            int maxDepth = 0;
            for (int i = 0; i < m_Layouts.Count; i++)
            {
                maxDepth = Mathf.Max(maxDepth, m_Layouts[i].Depth);
            }

            return maxDepth;
        }

        private void ShowEmpty(string text)
        {
            m_CanvasWidth = 520f;
            m_CanvasHeight = 240f;
            ApplyCanvasSize(m_CanvasWidth, m_CanvasHeight);
            Label label = new(text);
            label.style.position = Position.Absolute;
            Vector2 labelPosition = ToCanvasPoint(new Vector2(18f, 16f));
            label.style.left = labelPosition.x;
            label.style.top = labelPosition.y;
            label.style.color = TextMuted;
            label.style.fontSize = ScaleFont(12f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.width = Scale(460f);
            m_NodeLayer.Add(label);
            m_SelectedNode = null;
            m_EdgeCanvas.MarkDirtyRepaint();
            RefreshDetails();
        }

        private void CreateNodeElement(NodeLayout layout)
        {
            XAnimationDebugNodeSnapshot node = layout.Node;
            Vector2 cardPosition = ToCanvasPoint(layout.Position);
            VisualElement card = new();
            card.style.position = Position.Absolute;
            card.style.left = cardPosition.x;
            card.style.top = cardPosition.y;
            card.style.width = Scale(NodeWidth);
            card.style.height = Scale(NodeHeight);
            card.style.paddingLeft = Scale(8f);
            card.style.paddingRight = Scale(8f);
            card.style.paddingTop = Scale(6f);
            card.style.paddingBottom = Scale(6f);
            card.style.backgroundColor = node.isActive ? NodeBg : NodeBgInactive;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = NodeBorder;
            card.style.borderBottomColor = NodeBorder;
            card.style.borderLeftColor = NodeBorder;
            card.style.borderRightColor = NodeBorder;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.userData = node.id;

            VisualElement accent = new();
            accent.style.position = Position.Absolute;
            accent.style.left = 0;
            accent.style.top = 0;
            accent.style.bottom = 0;
            accent.style.width = Scale(4f);
            accent.style.backgroundColor = GetNodeAccent(node);
            accent.style.borderTopLeftRadius = 6;
            accent.style.borderBottomLeftRadius = 6;
            card.Add(accent);

            Label title = new(BuildNodeTitle(node));
            title.style.color = TextNormal;
            title.style.fontSize = ScaleFont(11f);
            title.style.unityFontStyleAndWeight = node.isActive ? FontStyle.Bold : FontStyle.Normal;
            title.style.whiteSpace = WhiteSpace.NoWrap;
            card.Add(title);

            Label type = new(BuildNodeSubtitle(node));
            type.style.color = TextMuted;
            type.style.fontSize = ScaleFont(10f);
            type.style.whiteSpace = WhiteSpace.NoWrap;
            type.style.marginTop = Scale(2f);
            card.Add(type);

            VisualElement spacer = new();
            spacer.style.flexGrow = 1;
            card.Add(spacer);

            VisualElement weightRow = new();
            weightRow.style.height = Scale(12f);
            weightRow.style.flexDirection = FlexDirection.Row;
            weightRow.style.alignItems = Align.Center;
            card.Add(weightRow);

            VisualElement track = new();
            track.style.height = Scale(4f);
            track.style.flexGrow = 1;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            track.style.borderTopLeftRadius = 2;
            track.style.borderTopRightRadius = 2;
            track.style.borderBottomLeftRadius = 2;
            track.style.borderBottomRightRadius = 2;
            weightRow.Add(track);

            VisualElement fill = new();
            fill.style.height = Scale(4f);
            fill.style.width = Length.Percent(Mathf.Clamp01(GetNodeVisualWeight(node)) * 100f);
            fill.style.backgroundColor = node.isActive ? ActiveAccent : InactiveAccent;
            fill.style.borderTopLeftRadius = 2;
            fill.style.borderTopRightRadius = 2;
            fill.style.borderBottomLeftRadius = 2;
            fill.style.borderBottomRightRadius = 2;
            track.Add(fill);

            Label weight = new(BuildNodeWeight(node));
            weight.style.width = Scale(70f);
            weight.style.marginLeft = Scale(5f);
            weight.style.color = TextMuted;
            weight.style.fontSize = ScaleFont(10f);
            weight.style.unityTextAlign = TextAnchor.MiddleRight;
            weightRow.Add(weight);

            card.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    evt.StopPropagation();
                }
            });
            card.RegisterCallback<MouseDownEvent>(evt =>
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

            m_NodeLayer.Add(card);
            m_NodeElements[node.id] = card;
        }

        private void CreateEdgeLabels()
        {
            for (int i = 0; i < m_Edges.Count; i++)
            {
                EdgeLayout edge = m_Edges[i];
                XAnimationDebugNodeSnapshot child = edge.Child.Node;
                if (child.inputIndex < 0)
                {
                    continue;
                }

                Rect from = edge.Parent.Rect;
                Rect to = edge.Child.Rect;
                Vector2 middle = new((from.xMax + to.xMin) * 0.5f, (from.center.y + to.center.y) * 0.5f);

                Label label = new($"w {child.inputWeight:0.##} / {child.effectiveWeight:0.##}");
                Vector2 labelPosition = ToCanvasPoint(new Vector2(middle.x - EdgeLabelWidth * 0.5f, middle.y - EdgeLabelHeight * 0.5f));
                label.style.position = Position.Absolute;
                label.style.left = labelPosition.x;
                label.style.top = labelPosition.y;
                label.style.width = Scale(EdgeLabelWidth);
                label.style.height = Scale(EdgeLabelHeight);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.fontSize = ScaleFont(9f);
                label.style.color = TextMuted;
                label.style.backgroundColor = new Color(0.06f, 0.065f, 0.07f, 0.78f);
                label.style.borderTopLeftRadius = 8;
                label.style.borderTopRightRadius = 8;
                label.style.borderBottomLeftRadius = 8;
                label.style.borderBottomRightRadius = 8;
                m_EdgeLabelLayer.Add(label);
            }
        }

        private void ApplySelectionVisuals()
        {
            foreach (KeyValuePair<int, VisualElement> pair in m_NodeElements)
            {
                bool selected = m_SelectedNode != null && pair.Key == m_SelectedNode.id;
                pair.Value.style.borderTopColor = selected ? SelectedBorder : NodeBorder;
                pair.Value.style.borderBottomColor = selected ? SelectedBorder : NodeBorder;
                pair.Value.style.borderLeftColor = selected ? SelectedBorder : NodeBorder;
                pair.Value.style.borderRightColor = selected ? SelectedBorder : NodeBorder;
                pair.Value.style.borderTopWidth = selected ? 2 : 1;
                pair.Value.style.borderBottomWidth = selected ? 2 : 1;
                pair.Value.style.borderLeftWidth = selected ? 2 : 1;
                pair.Value.style.borderRightWidth = selected ? 2 : 1;
            }
        }

        private void OnGenerateGraphVisualContent(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            DrawGrid(
                painter,
                visibleRect: GetVisibleCanvasRect(),
                gridSize: Scale(32f),
                origin: m_CanvasOrigin);

            for (int i = 0; i < m_Edges.Count; i++)
            {
                DrawEdge(painter, m_Edges[i], m_Zoom);
            }
        }

        private static void DrawGrid(Painter2D painter, Rect visibleRect, float gridSize, Vector2 origin)
        {
            if (visibleRect.width <= 0f || visibleRect.height <= 0f || gridSize <= 0.01f)
            {
                return;
            }

            DrawGridLines(painter, visibleRect, gridSize, origin, CanvasGrid, 1f);
            DrawGridLines(painter, visibleRect, gridSize * 5f, origin, CanvasGridMajor, 1.15f);
        }

        private static void DrawGridLines(Painter2D painter, Rect visibleRect, float gridSize, Vector2 origin, Color color, float lineWidth)
        {
            float startX = origin.x + Mathf.Floor((visibleRect.xMin - origin.x) / gridSize) * gridSize;
            float startY = origin.y + Mathf.Floor((visibleRect.yMin - origin.y) / gridSize) * gridSize;
            float endX = visibleRect.xMax;
            float endY = visibleRect.yMax;
            painter.strokeColor = color;
            painter.lineWidth = lineWidth;
            for (float x = startX; x <= endX; x += gridSize)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, visibleRect.yMin));
                painter.LineTo(new Vector2(x, endY));
                painter.Stroke();
            }

            for (float y = startY; y <= endY; y += gridSize)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(visibleRect.xMin, y));
                painter.LineTo(new Vector2(endX, y));
                painter.Stroke();
            }
        }

        private void DrawEdge(Painter2D painter, EdgeLayout edge, float zoom)
        {
            XAnimationDebugNodeSnapshot node = edge.Child.Node;
            float weight = GetNodeVisualWeight(node);
            Color color = node.isActive ? EdgeActive : EdgeInactive;
            color.a = Mathf.Lerp(0.18f, 0.92f, Mathf.Clamp01(weight));

            Vector2 from = ToCanvasPoint(new Vector2(edge.Parent.Rect.xMax, edge.Parent.Rect.center.y));
            Vector2 to = ToCanvasPoint(new Vector2(edge.Child.Rect.xMin, edge.Child.Rect.center.y));
            float tangent = Mathf.Clamp((to.x - from.x) * 0.45f, 44f, 112f);
            Vector2 c1 = from + new Vector2(tangent, 0f);
            Vector2 c2 = to - new Vector2(tangent, 0f);

            painter.lineWidth = Mathf.Lerp(1.2f, 5.5f, Mathf.Clamp01(weight)) * Mathf.Clamp(zoom, 0.55f, 1.5f);
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(from);
            for (int i = 1; i <= 18; i++)
            {
                float t = i / 18f;
                painter.LineTo(EvaluateCubic(from, c1, c2, to, t));
            }
            painter.Stroke();

            DrawFilledCircle(painter, from, 3.2f * Mathf.Clamp(zoom, 0.55f, 1.5f), color);
            DrawFilledCircle(painter, to, 3.2f * Mathf.Clamp(zoom, 0.55f, 1.5f), color);
        }

        private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 +
                   3f * u * u * t * p1 +
                   3f * u * t * t * p2 +
                   t * t * t * p3;
        }

        private void OnGraphWheel(WheelEvent evt)
        {
            if (m_GraphScrollView == null || m_CanvasWidth <= 0f || m_CanvasHeight <= 0f)
            {
                return;
            }

            float previousZoom = m_Zoom;
            float zoomFactor = Mathf.Pow(WheelZoomBase, -evt.delta.y);
            float nextZoom = Mathf.Clamp(previousZoom * zoomFactor, MinZoom, MaxZoom);
            if (Mathf.Approximately(previousZoom, nextZoom))
            {
                evt.StopPropagation();
                return;
            }

            Vector2 scrollOffset = GetScrollOffset();
            Vector2 viewportPoint = m_GraphScrollView.WorldToLocal(evt.mousePosition);
            Vector2 graphPoint = (scrollOffset + viewportPoint - m_CanvasOrigin) / previousZoom;
            m_Zoom = nextZoom;

            RebuildGraph();

            Vector2 nextOffset = graphPoint * nextZoom + m_CanvasOrigin - viewportPoint;
            SetScrollOffset(ClampScrollOffset(nextOffset));
            evt.StopPropagation();
        }

        private void OnGraphPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 2)
            {
                return;
            }

            m_IsPanning = true;
            m_PanPointerId = evt.pointerId;
            m_PanStartPointer = new Vector2(evt.position.x, evt.position.y);
            m_PanStartScrollOffset = GetScrollOffset();
            m_Canvas.CapturePointer(evt.pointerId);
            m_Canvas.Focus();
            evt.StopPropagation();
        }

        private void OnGraphPointerMove(PointerMoveEvent evt)
        {
            if (!m_IsPanning || m_PanPointerId != evt.pointerId || !m_Canvas.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            Vector2 pointerPosition = new(evt.position.x, evt.position.y);
            Vector2 delta = pointerPosition - m_PanStartPointer;
            SetScrollOffset(ClampScrollOffset(m_PanStartScrollOffset - delta));
            evt.StopPropagation();
        }

        private void OnGraphPointerUp(PointerUpEvent evt)
        {
            if (!m_IsPanning || m_PanPointerId != evt.pointerId)
            {
                return;
            }

            if (m_Canvas.HasPointerCapture(evt.pointerId))
            {
                m_Canvas.ReleasePointer(evt.pointerId);
            }

            EndPan();
            evt.StopPropagation();
        }

        private void OnGraphPointerCancel(PointerCancelEvent evt)
        {
            if (m_Canvas.HasPointerCapture(evt.pointerId))
            {
                m_Canvas.ReleasePointer(evt.pointerId);
            }

            EndPan();
        }

        private void EndPan()
        {
            m_IsPanning = false;
            m_PanPointerId = PointerId.invalidPointerId;
            m_PanStartPointer = Vector2.zero;
            m_PanStartScrollOffset = Vector2.zero;
        }

        private Vector2 GetScrollOffset()
        {
            if (m_GraphScrollView == null)
            {
                return Vector2.zero;
            }

            return new Vector2(
                m_GraphScrollView.horizontalScroller.value,
                m_GraphScrollView.verticalScroller.value);
        }

        private void SetScrollOffset(Vector2 offset)
        {
            if (m_GraphScrollView == null)
            {
                return;
            }

            m_GraphScrollView.horizontalScroller.value = offset.x;
            m_GraphScrollView.verticalScroller.value = offset.y;
            m_EdgeCanvas?.MarkDirtyRepaint();
        }

        private Vector2 ClampScrollOffset(Vector2 offset)
        {
            Vector2 viewportSize = GetGraphViewportSize();
            float maxX = Mathf.Max(0f, m_CanvasVisualWidth - viewportSize.x);
            float maxY = Mathf.Max(0f, m_CanvasVisualHeight - viewportSize.y);
            return new Vector2(
                Mathf.Clamp(offset.x, 0f, maxX),
                Mathf.Clamp(offset.y, 0f, maxY));
        }

        private Vector2 GetGraphViewportSize()
        {
            if (m_GraphScrollView == null)
            {
                return Vector2.zero;
            }

            Rect viewport = m_GraphScrollView.contentViewport?.layout ?? Rect.zero;
            if (viewport.width > 0f && viewport.height > 0f)
            {
                return viewport.size;
            }

            Rect scrollViewLayout = m_GraphScrollView.layout;
            return new Vector2(Mathf.Max(0f, scrollViewLayout.width), Mathf.Max(0f, scrollViewLayout.height));
        }

        private static Vector2 GetCanvasOrigin(Vector2 viewportSize)
        {
            return new Vector2(Mathf.Max(0f, viewportSize.x), Mathf.Max(0f, viewportSize.y));
        }

        private Rect GetVisibleCanvasRect()
        {
            Vector2 offset = GetScrollOffset();
            Vector2 viewportSize = GetGraphViewportSize();
            float width = viewportSize.x > 0f ? viewportSize.x : m_CanvasVisualWidth;
            float height = viewportSize.y > 0f ? viewportSize.y : m_CanvasVisualHeight;
            return new Rect(
                offset.x,
                offset.y,
                Mathf.Min(width, Mathf.Max(0f, m_CanvasVisualWidth - offset.x)),
                Mathf.Min(height, Mathf.Max(0f, m_CanvasVisualHeight - offset.y)));
        }

        private float Scale(float value)
        {
            return value * m_Zoom;
        }

        private Vector2 ToCanvasPoint(Vector2 value)
        {
            return m_CanvasOrigin + value * m_Zoom;
        }

        private float ScaleFont(float value)
        {
            return Mathf.Max(7f, value * Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(m_Zoom)));
        }

        private static void DrawFilledCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
        }

        private static string BuildNodeTitle(XAnimationDebugNodeSnapshot node)
        {
            string name = string.IsNullOrWhiteSpace(node.displayName) ? node.playableType : node.displayName;
            if (node.inputIndex >= 0)
            {
                name = $"[{node.inputIndex}] {name}";
            }

            return name;
        }

        private static string BuildNodeSubtitle(XAnimationDebugNodeSnapshot node)
        {
            List<string> parts = new();
            if (!string.IsNullOrWhiteSpace(node.playableType))
            {
                parts.Add(node.playableType);
            }

            if (!string.IsNullOrWhiteSpace(node.stateKey))
            {
                parts.Add($"state {node.stateKey}");
            }

            if (!string.IsNullOrWhiteSpace(node.clipKey))
            {
                parts.Add($"clip {node.clipKey}");
            }

            if (node.playbackId > 0)
            {
                parts.Add($"id {node.playbackId}");
            }

            return string.Join(" | ", parts);
        }

        private static string BuildNodeWeight(XAnimationDebugNodeSnapshot node)
        {
            if (node.inputIndex < 0 && node.effectiveWeight <= 0f)
            {
                return node.isActive ? "active" : "idle";
            }

            return $"eff {node.effectiveWeight:0.##}";
        }

        private static Color GetNodeAccent(XAnimationDebugNodeSnapshot node)
        {
            string type = node.playableType ?? string.Empty;
            if (type.IndexOf("Graph", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.56f, 0.68f, 1f, 1f);
            }

            if (type.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.30f, 0.82f, 0.76f, 1f);
            }

            if (type.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.94f, 0.67f, 0.28f, 1f);
            }

            if (type.IndexOf("Mixer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.82f, 0.54f, 0.96f, 1f);
            }

            if (type.IndexOf("Clip", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(0.34f, 0.78f, 0.42f, 1f);
            }

            return node.isActive ? ActiveAccent : InactiveAccent;
        }

        private static float GetNodeVisualWeight(XAnimationDebugNodeSnapshot node)
        {
            if (node == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Max(node.effectiveWeight, node.inputWeight));
        }

        private bool ContainsLayout(int nodeId)
        {
            for (int i = 0; i < m_Layouts.Count; i++)
            {
                if (m_Layouts[i].Node.id == nodeId)
                {
                    return true;
                }
            }

            return false;
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
            key.style.width = 108;
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
    }
}
#endif
