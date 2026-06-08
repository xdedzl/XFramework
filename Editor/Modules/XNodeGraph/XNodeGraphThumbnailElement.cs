using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.NodeKit.Editor
{
    public sealed class XNodeGraphThumbnailElement : VisualElement
    {
        private const float Height = 120f;
        private const float Padding = 8f;
        private const float SourceNodeWidth = 150f;
        private const float SourceNodeHeight = 56f;
        private const float FallbackColumnGap = 210f;
        private const float FallbackRowGap = 116f;

        private static readonly Color BackgroundColor = new(0.10f, 0.11f, 0.13f, 1f);
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.035f);
        private static readonly Color BorderColor = new(0.28f, 0.30f, 0.34f, 1f);
        private static readonly Color EdgeColor = new(0.78f, 0.82f, 0.88f, 0.58f);
        private static readonly Color EdgeEndpointColor = new(0.92f, 0.94f, 0.98f, 0.72f);
        private static readonly Color NodeOutlineColor = new(1f, 1f, 1f, 0.22f);
        private static readonly Color EntryColor = new(0.22f, 0.58f, 0.28f, 1f);
        private static readonly Color ProcessColor = new(0.24f, 0.42f, 0.68f, 1f);
        private static readonly Color SwitchColor = new(0.68f, 0.47f, 0.20f, 1f);
        private static readonly Color ValueColor = new(0.45f, 0.30f, 0.68f, 1f);
        private static readonly Color GraphRefColor = new(0.58f, 0.34f, 0.29f, 1f);
        private static readonly Color OtherColor = new(0.34f, 0.37f, 0.42f, 1f);

        private readonly List<ThumbnailNode> m_Nodes = new();
        private readonly List<ThumbnailEdge> m_Edges = new();
        private readonly List<Label> m_NodeLabels = new();
        private readonly Label m_EmptyLabel;

        private XNodeGraphAsset m_Graph;

        public XNodeGraphThumbnailElement()
        {
            style.height = Height;
            style.minHeight = Height;
            style.maxHeight = Height;
            style.marginTop = 5f;
            style.flexGrow = 1f;
            style.alignSelf = Align.Stretch;
            style.position = Position.Relative;
            style.overflow = Overflow.Hidden;
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = BorderColor;
            style.borderBottomColor = BorderColor;
            style.borderLeftColor = BorderColor;
            style.borderRightColor = BorderColor;
            style.backgroundColor = BackgroundColor;
            style.display = DisplayStyle.None;

            m_EmptyLabel = new Label("空图")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0f,
                    right = 0f,
                    top = 0f,
                    bottom = 0f,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new Color(0.66f, 0.68f, 0.72f, 1f),
                    fontSize = 10
                }
            };
            Add(m_EmptyLabel);

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<GeometryChangedEvent>(_ => UpdateLabelLayout());
        }

        public void SetGraph(XNodeGraphAsset graph)
        {
            m_Graph = graph;
            m_Nodes.Clear();
            m_Edges.Clear();

            ClearNodeLabels();

            if (m_Graph == null)
            {
                tooltip = "未配置目标图";
                style.display = DisplayStyle.None;
                m_EmptyLabel.style.display = DisplayStyle.None;
                MarkDirtyRepaint();
                return;
            }

            style.display = DisplayStyle.Flex;

            try
            {
                BuildModel(m_Graph);
                tooltip = $"节点: {m_Nodes.Count}, 连线: {m_Edges.Count}";
                m_EmptyLabel.text = m_Nodes.Count == 0 ? "空图" : string.Empty;
            }
            catch (Exception exception)
            {
                m_Nodes.Clear();
                m_Edges.Clear();
                tooltip = $"缩略图生成失败: {exception.Message}";
                m_EmptyLabel.text = "缩略图生成失败";
            }

            m_EmptyLabel.style.display = m_Nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            RebuildNodeLabels();
            UpdateLabelLayout();
            MarkDirtyRepaint();
        }

        private void BuildModel(XNodeGraphAsset graph)
        {
            IXNode[] graphNodes = graph.nodes;
            if (graphNodes == null || graphNodes.Length == 0)
            {
                return;
            }

            IDictionary<string, Vector2> positionDict = graph.GetNodePositionDict();
            int fallbackColumns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(graphNodes.Length)));
            var nodeIds = new HashSet<string>();

            for (int i = 0; i < graphNodes.Length; i++)
            {
                IXNode runtimeNode = graphNodes[i];
                if (runtimeNode == null)
                {
                    continue;
                }

                string nodeId = runtimeNode.GetId();
                if (string.IsNullOrEmpty(nodeId) || !nodeIds.Add(nodeId))
                {
                    continue;
                }

                Vector2 position = positionDict.TryGetValue(nodeId, out Vector2 savedPosition)
                    ? savedPosition
                    : GetFallbackPosition(i, fallbackColumns);

                m_Nodes.Add(new ThumbnailNode(
                    runtimeNode,
                    nodeId,
                    GetShortNodeTypeName(runtimeNode.GetType()),
                    position,
                    GetNodeColor(runtimeNode)));
            }

            var edgeKeys = new HashSet<string>();
            foreach (ThumbnailNode node in m_Nodes)
            {
                AddEdgesForNode(node, nodeIds, edgeKeys);
            }
        }

        private void AddEdgesForNode(ThumbnailNode fromNode, HashSet<string> nodeIds, HashSet<string> edgeKeys)
        {
            foreach (FieldInfo field in GetInstanceFields(fromNode.RuntimeNode.GetType()))
            {
                object fieldValue = field.GetValue(fromNode.RuntimeNode);
                if (fieldValue == null)
                {
                    continue;
                }

                string fieldName = field.Name;
                if (field.FieldType == typeof(string) && IsNodeIdField(fieldName))
                {
                    AddEdge(fromNode.Id, fieldValue as string, IsValueReferenceField(fieldName), nodeIds, edgeKeys);
                    continue;
                }

                if (IsNodeIdsField(fieldName))
                {
                    AddEdgesFromStringEnumerable(fromNode.Id, fieldValue, IsValueReferenceField(fieldName), nodeIds, edgeKeys);
                    continue;
                }

                if (fieldValue is IEnumerable enumerable && fieldValue is not string)
                {
                    AddEdgesFromNestedCollection(fromNode.Id, enumerable, nodeIds, edgeKeys);
                }
            }
        }

        private void AddEdgesFromNestedCollection(
            string fromNodeId,
            IEnumerable enumerable,
            HashSet<string> nodeIds,
            HashSet<string> edgeKeys)
        {
            foreach (object item in enumerable)
            {
                if (item == null || item is string)
                {
                    continue;
                }

                foreach (FieldInfo nestedField in GetInstanceFields(item.GetType()))
                {
                    object nestedValue = nestedField.GetValue(item);
                    if (nestedValue == null)
                    {
                        continue;
                    }

                    if (nestedField.FieldType == typeof(string) && IsNodeIdField(nestedField.Name))
                    {
                        AddEdge(fromNodeId, nestedValue as string, IsValueReferenceField(nestedField.Name), nodeIds, edgeKeys);
                    }
                    else if (IsNodeIdsField(nestedField.Name))
                    {
                        AddEdgesFromStringEnumerable(fromNodeId, nestedValue, IsValueReferenceField(nestedField.Name), nodeIds, edgeKeys);
                    }
                }
            }
        }

        private void AddEdgesFromStringEnumerable(
            string fromNodeId,
            object value,
            bool reverseDirection,
            HashSet<string> nodeIds,
            HashSet<string> edgeKeys)
        {
            if (value is not IEnumerable enumerable || value is string)
            {
                return;
            }

            foreach (object item in enumerable)
            {
                if (item is string targetNodeId)
                {
                    AddEdge(fromNodeId, targetNodeId, reverseDirection, nodeIds, edgeKeys);
                }
            }
        }

        private void AddEdge(
            string fromNodeId,
            string targetNodeId,
            bool reverseDirection,
            HashSet<string> nodeIds,
            HashSet<string> edgeKeys)
        {
            if (string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(targetNodeId) || !nodeIds.Contains(targetNodeId))
            {
                return;
            }

            string edgeFrom = reverseDirection ? targetNodeId : fromNodeId;
            string edgeTo = reverseDirection ? fromNodeId : targetNodeId;
            string edgeKey = $"{edgeFrom}->{edgeTo}";
            if (!edgeKeys.Add(edgeKey))
            {
                return;
            }

            m_Edges.Add(new ThumbnailEdge(edgeFrom, edgeTo));
        }

        private void RebuildNodeLabels()
        {
            for (int i = 0; i < m_Nodes.Count; i++)
            {
                ThumbnailNode node = m_Nodes[i];
                var label = new Label(node.ShortTypeName)
                {
                    tooltip = $"{node.ShortTypeName}\n{node.Id}",
                    pickingMode = PickingMode.Ignore,
                    style =
                    {
                        position = Position.Absolute,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        whiteSpace = WhiteSpace.NoWrap,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        color = Color.white,
                        fontSize = 9,
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                };
                m_NodeLabels.Add(label);
                Add(label);
            }
        }

        private void ClearNodeLabels()
        {
            for (int i = 0; i < m_NodeLabels.Count; i++)
            {
                m_NodeLabels[i].RemoveFromHierarchy();
            }

            m_NodeLabels.Clear();
        }

        private void UpdateLabelLayout()
        {
            if (m_NodeLabels.Count == 0)
            {
                return;
            }

            if (!TryCreateLayout(contentRect, out ThumbnailLayout layout))
            {
                for (int i = 0; i < m_NodeLabels.Count; i++)
                {
                    m_NodeLabels[i].style.display = DisplayStyle.None;
                }

                return;
            }

            for (int i = 0; i < m_NodeLabels.Count; i++)
            {
                Rect nodeRect = GetNodeRect(m_Nodes[i], layout);
                Label label = m_NodeLabels[i];
                if (nodeRect.width < 28f || nodeRect.height < 12f)
                {
                    label.style.display = DisplayStyle.None;
                    continue;
                }

                label.style.display = DisplayStyle.Flex;
                label.style.left = nodeRect.xMin;
                label.style.top = nodeRect.yMin;
                label.style.width = nodeRect.width;
                label.style.height = nodeRect.height;
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            DrawBackground(painter, rect);

            if (m_Graph == null || m_Nodes.Count == 0 || !TryCreateLayout(rect, out ThumbnailLayout layout))
            {
                return;
            }

            var nodeRectById = new Dictionary<string, Rect>(m_Nodes.Count);
            for (int i = 0; i < m_Nodes.Count; i++)
            {
                ThumbnailNode node = m_Nodes[i];
                nodeRectById[node.Id] = GetNodeRect(node, layout);
            }

            for (int i = 0; i < m_Edges.Count; i++)
            {
                ThumbnailEdge edge = m_Edges[i];
                if (!nodeRectById.TryGetValue(edge.FromNodeId, out Rect fromRect)
                    || !nodeRectById.TryGetValue(edge.ToNodeId, out Rect toRect))
                {
                    continue;
                }

                DrawEdge(painter, fromRect, toRect);
            }

            for (int i = 0; i < m_Nodes.Count; i++)
            {
                ThumbnailNode node = m_Nodes[i];
                DrawNode(painter, nodeRectById[node.Id], node.Color);
            }
        }

        private bool TryCreateLayout(Rect rect, out ThumbnailLayout layout)
        {
            layout = default;
            if (m_Nodes.Count == 0)
            {
                return false;
            }

            Rect graphRect = new Rect(
                rect.xMin + Padding,
                rect.yMin + Padding,
                Mathf.Max(1f, rect.width - Padding * 2f),
                Mathf.Max(1f, rect.height - Padding * 2f));

            Rect bounds = ComputeSourceBounds(m_Nodes);
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return false;
            }

            float scale = Mathf.Min(graphRect.width / bounds.width, graphRect.height / bounds.height);
            scale = Mathf.Min(1f, Mathf.Max(0.01f, scale));
            float contentWidth = bounds.width * scale;
            float contentHeight = bounds.height * scale;
            Vector2 offset = new(
                graphRect.xMin + (graphRect.width - contentWidth) * 0.5f,
                graphRect.yMin + (graphRect.height - contentHeight) * 0.5f);

            layout = new ThumbnailLayout(bounds, offset, scale);
            return true;
        }

        private static Rect ComputeSourceBounds(IReadOnlyList<ThumbnailNode> nodes)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 position = nodes[i].SourcePosition;
                minX = Mathf.Min(minX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxX = Mathf.Max(maxX, position.x + SourceNodeWidth);
                maxY = Mathf.Max(maxY, position.y + SourceNodeHeight);
            }

            if (minX == float.MaxValue)
            {
                return new Rect(0f, 0f, SourceNodeWidth, SourceNodeHeight);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Rect GetNodeRect(ThumbnailNode node, ThumbnailLayout layout)
        {
            float x = layout.Offset.x + (node.SourcePosition.x - layout.SourceBounds.xMin) * layout.Scale;
            float y = layout.Offset.y + (node.SourcePosition.y - layout.SourceBounds.yMin) * layout.Scale;
            float width = Mathf.Max(8f, SourceNodeWidth * layout.Scale);
            float height = Mathf.Max(6f, SourceNodeHeight * layout.Scale);
            return new Rect(x, y, width, height);
        }

        private static Vector2 GetFallbackPosition(int index, int columns)
        {
            int column = index % columns;
            int row = index / columns;
            return new Vector2(column * FallbackColumnGap, row * FallbackRowGap);
        }

        private static IEnumerable<FieldInfo> GetInstanceFields(Type type)
        {
            for (Type currentType = type; currentType != null && currentType != typeof(object); currentType = currentType.BaseType)
            {
                FieldInfo[] fields = currentType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    yield return fields[i];
                }
            }
        }

        private static bool IsNodeIdField(string fieldName)
        {
            return string.Equals(fieldName, "nextNodeId", StringComparison.OrdinalIgnoreCase)
                   || fieldName.EndsWith("NodeId", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNodeIdsField(string fieldName)
        {
            return string.Equals(fieldName, "nextNodeIds", StringComparison.OrdinalIgnoreCase)
                   || fieldName.EndsWith("NodeIds", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValueReferenceField(string fieldName)
        {
            return fieldName.IndexOf("valueNodeId", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetShortNodeTypeName(Type nodeType)
        {
            string typeName = nodeType.Name;
            int genericIndex = typeName.IndexOf('`');
            if (genericIndex >= 0)
            {
                typeName = typeName[..genericIndex];
            }

            return typeName.EndsWith("Node", StringComparison.Ordinal) ? typeName[..^4] : typeName;
        }

        private static Color GetNodeColor(IXNode node)
        {
            string typeName = node.GetType().Name;
            if (typeName.IndexOf("Entry", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return EntryColor;
            }

            if (typeName.IndexOf("GraphRef", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return GraphRefColor;
            }

            if (node is ISwitchNode)
            {
                return SwitchColor;
            }

            if (node is IValueNode)
            {
                return ValueColor;
            }

            if (node is IProcessNode)
            {
                return ProcessColor;
            }

            return OtherColor;
        }

        private static void DrawBackground(Painter2D painter, Rect rect)
        {
            DrawFilledRect(painter, rect, BackgroundColor);

            painter.lineWidth = 1f;
            painter.strokeColor = GridColor;
            for (float x = rect.xMin + 24f; x < rect.xMax; x += 24f)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, rect.yMin));
                painter.LineTo(new Vector2(x, rect.yMax));
                painter.Stroke();
            }

            for (float y = rect.yMin + 24f; y < rect.yMax; y += 24f)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, y));
                painter.LineTo(new Vector2(rect.xMax, y));
                painter.Stroke();
            }
        }

        private static void DrawEdge(Painter2D painter, Rect fromRect, Rect toRect)
        {
            Vector2 fromCenter = fromRect.center;
            Vector2 toCenter = toRect.center;
            if ((toCenter - fromCenter).sqrMagnitude < 1f)
            {
                return;
            }

            Vector2 from = GetRectConnectionPoint(fromRect, toCenter);
            Vector2 to = GetRectConnectionPoint(toRect, fromCenter);
            float middleX = (from.x + to.x) * 0.5f;

            painter.lineWidth = 1.35f;
            painter.strokeColor = EdgeColor;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(new Vector2(middleX, from.y));
            painter.LineTo(new Vector2(middleX, to.y));
            painter.LineTo(to);
            painter.Stroke();

            DrawFilledCircle(painter, to, 2.1f, EdgeEndpointColor);
        }

        private static Vector2 GetRectConnectionPoint(Rect rect, Vector2 target)
        {
            Vector2 center = rect.center;
            Vector2 delta = target - center;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return new Vector2(delta.x >= 0f ? rect.xMax : rect.xMin, center.y);
            }

            return new Vector2(center.x, delta.y >= 0f ? rect.yMax : rect.yMin);
        }

        private static void DrawNode(Painter2D painter, Rect rect, Color fillColor)
        {
            DrawFilledRect(painter, rect, fillColor);
            DrawRectOutline(painter, rect, NodeOutlineColor, 1f);
        }

        private static void DrawFilledRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static void DrawRectOutline(Painter2D painter, Rect rect, Color color, float lineWidth)
        {
            painter.lineWidth = lineWidth;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Stroke();
        }

        private static void DrawFilledCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
        }

        private readonly struct ThumbnailNode
        {
            public ThumbnailNode(IXNode runtimeNode, string id, string shortTypeName, Vector2 sourcePosition, Color color)
            {
                RuntimeNode = runtimeNode;
                Id = id;
                ShortTypeName = shortTypeName;
                SourcePosition = sourcePosition;
                Color = color;
            }

            public IXNode RuntimeNode { get; }
            public string Id { get; }
            public string ShortTypeName { get; }
            public Vector2 SourcePosition { get; }
            public Color Color { get; }
        }

        private readonly struct ThumbnailEdge
        {
            public ThumbnailEdge(string fromNodeId, string toNodeId)
            {
                FromNodeId = fromNodeId;
                ToNodeId = toNodeId;
            }

            public string FromNodeId { get; }
            public string ToNodeId { get; }
        }

        private readonly struct ThumbnailLayout
        {
            public ThumbnailLayout(Rect sourceBounds, Vector2 offset, float scale)
            {
                SourceBounds = sourceBounds;
                Offset = offset;
                Scale = scale;
            }

            public Rect SourceBounds { get; }
            public Vector2 Offset { get; }
            public float Scale { get; }
        }
    }
}
