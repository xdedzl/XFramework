#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Animation;

namespace XFramework.Editor
{
    internal sealed class XAnimationDirectionalBlendGraphElement : VisualElement
    {
        private const float MinSize = 220f;
        private const float Padding = 18f;
        private const float GridLineWidth = 1f;
        private const float SampleRadius = 4f;
        private const float CurrentPointRadius = 5f;
        private const float MinWeightRingRadius = 8f;
        private const float MaxWeightRingRadius = 30f;

        private static readonly Color BackgroundColor = new(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color BorderColor = new(0.32f, 0.36f, 0.44f, 1f);
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.05f);
        private static readonly Color AxisColor = new(1f, 1f, 1f, 0.14f);
        private static readonly Color SampleColor = new(0.47f, 0.74f, 1f, 1f);
        private static readonly Color SampleInnerColor = new(0.88f, 0.95f, 1f, 0.95f);
        private static readonly Color CurrentPointColor = new(1f, 0.37f, 0.31f, 1f);
        private static readonly Color CurrentPointInnerColor = new(1f, 0.82f, 0.76f, 0.95f);
        private static readonly Color WeightRingColor = new(0.78f, 0.90f, 1f, 0.92f);

        internal readonly struct SampleViewData
        {
            public SampleViewData(string clipKey, float positionX, float positionY, float weight)
            {
                ClipKey = clipKey ?? string.Empty;
                PositionX = positionX;
                PositionY = positionY;
                Weight = Mathf.Clamp01(weight);
            }

            public string ClipKey { get; }
            public float PositionX { get; }
            public float PositionY { get; }
            public float Weight { get; }
        }

        internal readonly struct GraphData
        {
            public GraphData(
                IReadOnlyList<SampleViewData> samples,
                Vector2 currentPosition,
                bool dragEnabled,
                Action onDragStarted,
                Action<Vector2> onCurrentPositionChanged)
            {
                Samples = samples;
                CurrentPosition = currentPosition;
                DragEnabled = dragEnabled;
                OnDragStarted = onDragStarted;
                OnCurrentPositionChanged = onCurrentPositionChanged;
            }

            public IReadOnlyList<SampleViewData> Samples { get; }
            public Vector2 CurrentPosition { get; }
            public bool DragEnabled { get; }
            public Action OnDragStarted { get; }
            public Action<Vector2> OnCurrentPositionChanged { get; }
        }

        private GraphData m_Data;
        private bool m_IsDragging;
        private int m_ActivePointerId = PointerId.invalidPointerId;
        private Rect m_GraphRect;
        private Rect m_WorldBounds;

        public XAnimationDirectionalBlendGraphElement()
        {
            style.height = MinSize;
            style.minHeight = MinSize;
            style.maxHeight = MinSize;
            style.marginBottom = 6f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopWidth = 1f;
            style.borderBottomColor = BorderColor;
            style.borderLeftColor = BorderColor;
            style.borderRightColor = BorderColor;
            style.borderTopColor = BorderColor;
            style.borderBottomLeftRadius = 4f;
            style.borderBottomRightRadius = 4f;
            style.borderTopLeftRadius = 4f;
            style.borderTopRightRadius = 4f;
            style.backgroundColor = BackgroundColor;

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                m_IsDragging = false;
                m_ActivePointerId = PointerId.invalidPointerId;
            });
        }

        public void SetData(GraphData data)
        {
            m_Data = data;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            Rect currentContentRect = contentRect;
            if (currentContentRect.width <= 0f || currentContentRect.height <= 0f)
            {
                return;
            }

            m_GraphRect = BuildGraphRect(currentContentRect);
            m_WorldBounds = ComputeWorldBounds(m_Data.Samples);

            DrawGrid(painter, m_GraphRect, m_WorldBounds);
            DrawWeightRings(painter, m_GraphRect, m_WorldBounds, m_Data.Samples);
            DrawSamples(painter, m_GraphRect, m_WorldBounds, m_Data.Samples);
            DrawCurrentPoint(painter, m_GraphRect, m_WorldBounds, m_Data.CurrentPosition, m_Data.DragEnabled);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !m_Data.DragEnabled)
            {
                return;
            }

            if (!TryMapLocalPointToWorld(evt.localPosition, out Vector2 worldPosition))
            {
                return;
            }

            m_IsDragging = true;
            m_ActivePointerId = evt.pointerId;
            this.CapturePointer(m_ActivePointerId);
            m_Data.OnDragStarted?.Invoke();
            m_Data.OnCurrentPositionChanged?.Invoke(worldPosition);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!m_IsDragging || m_ActivePointerId != evt.pointerId || !this.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            if (!TryMapLocalPointToWorld(evt.localPosition, out Vector2 worldPosition))
            {
                return;
            }

            m_Data.OnCurrentPositionChanged?.Invoke(worldPosition);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (m_ActivePointerId != evt.pointerId)
            {
                return;
            }

            if (this.HasPointerCapture(evt.pointerId))
            {
                this.ReleasePointer(evt.pointerId);
            }

            m_IsDragging = false;
            m_ActivePointerId = PointerId.invalidPointerId;
            evt.StopPropagation();
        }

        private static Rect BuildGraphRect(Rect contentRect)
        {
            float size = Mathf.Max(1f, Mathf.Min(contentRect.width, contentRect.height) - Padding * 2f);
            float left = contentRect.xMin + (contentRect.width - size) * 0.5f;
            float top = contentRect.yMin + (contentRect.height - size) * 0.5f;
            return new Rect(left, top, size, size);
        }

        private static Rect ComputeWorldBounds(IReadOnlyList<SampleViewData> samples)
        {
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;
            bool hasSample = false;

            if (samples != null)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    SampleViewData sample = samples[i];
                    if (!hasSample)
                    {
                        minX = maxX = sample.PositionX;
                        minY = maxY = sample.PositionY;
                        hasSample = true;
                    }
                    else
                    {
                        minX = Mathf.Min(minX, sample.PositionX);
                        maxX = Mathf.Max(maxX, sample.PositionX);
                        minY = Mathf.Min(minY, sample.PositionY);
                        maxY = Mathf.Max(maxY, sample.PositionY);
                    }
                }
            }

            minX = Mathf.Min(minX, 0f);
            maxX = Mathf.Max(maxX, 0f);
            minY = Mathf.Min(minY, 0f);
            maxY = Mathf.Max(maxY, 0f);

            float width = maxX - minX;
            float height = maxY - minY;
            float dominantSize = Mathf.Max(width, height);
            if (dominantSize < 0.001f)
            {
                dominantSize = 1f;
            }

            float padding = dominantSize * 0.15f;
            float paddedWidth = width + padding * 2f;
            float paddedHeight = height + padding * 2f;
            float squareSize = Mathf.Max(paddedWidth, paddedHeight, 1f);

            float centerX = (minX + maxX) * 0.5f;
            float centerY = (minY + maxY) * 0.5f;
            return new Rect(centerX - squareSize * 0.5f, centerY - squareSize * 0.5f, squareSize, squareSize);
        }

        private static void DrawGrid(Painter2D painter, Rect graphRect, Rect worldBounds)
        {
            DrawRectOutline(painter, graphRect, BorderColor, 1f);

            float[] gridFractions = { 0.25f, 0.5f, 0.75f };
            for (int i = 0; i < gridFractions.Length; i++)
            {
                float fraction = gridFractions[i];
                float x = Mathf.Lerp(graphRect.xMin, graphRect.xMax, fraction);
                float y = Mathf.Lerp(graphRect.yMin, graphRect.yMax, fraction);
                DrawLine(painter, new Vector2(x, graphRect.yMin), new Vector2(x, graphRect.yMax), GridColor, GridLineWidth);
                DrawLine(painter, new Vector2(graphRect.xMin, y), new Vector2(graphRect.xMax, y), GridColor, GridLineWidth);
            }

            if (TryMapWorldToLocal(graphRect, worldBounds, Vector2.zero, out Vector2 origin))
            {
                DrawLine(painter, new Vector2(origin.x, graphRect.yMin), new Vector2(origin.x, graphRect.yMax), AxisColor, 1.5f);
                DrawLine(painter, new Vector2(graphRect.xMin, origin.y), new Vector2(graphRect.xMax, origin.y), AxisColor, 1.5f);
            }
        }

        private static void DrawWeightRings(
            Painter2D painter,
            Rect graphRect,
            Rect worldBounds,
            IReadOnlyList<SampleViewData> samples)
        {
            if (samples == null)
            {
                return;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                SampleViewData sample = samples[i];
                if (sample.Weight < 0.01f ||
                    !TryMapWorldToLocal(graphRect, worldBounds, new Vector2(sample.PositionX, sample.PositionY), out Vector2 position))
                {
                    continue;
                }

                float radius = Mathf.Lerp(MinWeightRingRadius, MaxWeightRingRadius, sample.Weight);
                DrawCircleStroke(painter, position, radius, WeightRingColor, 2f);
            }
        }

        private static void DrawSamples(
            Painter2D painter,
            Rect graphRect,
            Rect worldBounds,
            IReadOnlyList<SampleViewData> samples)
        {
            if (samples == null)
            {
                return;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                SampleViewData sample = samples[i];
                if (!TryMapWorldToLocal(graphRect, worldBounds, new Vector2(sample.PositionX, sample.PositionY), out Vector2 position))
                {
                    continue;
                }

                DrawDiamond(painter, position, SampleRadius + 1.5f, SampleColor);
                DrawDiamond(painter, position, SampleRadius - 0.5f, SampleInnerColor);
            }
        }

        private static void DrawCurrentPoint(
            Painter2D painter,
            Rect graphRect,
            Rect worldBounds,
            Vector2 currentPosition,
            bool dragEnabled)
        {
            if (!TryMapWorldToLocal(graphRect, worldBounds, currentPosition, out Vector2 position))
            {
                return;
            }

            float outerRadius = dragEnabled ? CurrentPointRadius + 1f : CurrentPointRadius;
            DrawFilledCircle(painter, position, outerRadius, CurrentPointColor);
            DrawFilledCircle(painter, position, CurrentPointRadius - 1.5f, CurrentPointInnerColor);
        }

        private bool TryMapLocalPointToWorld(Vector2 localPoint, out Vector2 worldPoint)
        {
            if (m_GraphRect.width <= 0f || m_GraphRect.height <= 0f)
            {
                worldPoint = Vector2.zero;
                return false;
            }

            float x = Mathf.Clamp(localPoint.x, m_GraphRect.xMin, m_GraphRect.xMax);
            float y = Mathf.Clamp(localPoint.y, m_GraphRect.yMin, m_GraphRect.yMax);
            float normalizedX = Mathf.InverseLerp(m_GraphRect.xMin, m_GraphRect.xMax, x);
            float normalizedY = Mathf.InverseLerp(m_GraphRect.yMax, m_GraphRect.yMin, y);
            worldPoint = new Vector2(
                Mathf.Lerp(m_WorldBounds.xMin, m_WorldBounds.xMax, normalizedX),
                Mathf.Lerp(m_WorldBounds.yMin, m_WorldBounds.yMax, normalizedY));
            return true;
        }

        private static bool TryMapWorldToLocal(Rect graphRect, Rect worldBounds, Vector2 worldPoint, out Vector2 localPoint)
        {
            if (graphRect.width <= 0f || graphRect.height <= 0f || worldBounds.width <= 0f || worldBounds.height <= 0f)
            {
                localPoint = Vector2.zero;
                return false;
            }

            float normalizedX = Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, worldPoint.x);
            float normalizedY = Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, worldPoint.y);
            localPoint = new Vector2(
                Mathf.Lerp(graphRect.xMin, graphRect.xMax, normalizedX),
                Mathf.Lerp(graphRect.yMax, graphRect.yMin, normalizedY));
            return true;
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

        private static void DrawLine(Painter2D painter, Vector2 from, Vector2 to, Color color, float lineWidth)
        {
            painter.lineWidth = lineWidth;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static void DrawCircleStroke(Painter2D painter, Vector2 center, float radius, Color color, float lineWidth)
        {
            painter.lineWidth = lineWidth;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();
        }

        private static void DrawFilledCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
        }

        private static void DrawDiamond(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(center.x, center.y - radius));
            painter.LineTo(new Vector2(center.x + radius, center.y));
            painter.LineTo(new Vector2(center.x, center.y + radius));
            painter.LineTo(new Vector2(center.x - radius, center.y));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
#endif
