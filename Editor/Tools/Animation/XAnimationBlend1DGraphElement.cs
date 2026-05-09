#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    internal sealed class XAnimationBlend1DGraphElement : VisualElement
    {
        private const float Height = 108f;
        private const float PaddingX = 16f;
        private const float PaddingTop = 12f;
        private const float PaddingBottom = 22f;
        private const float SampleDotRadius = 4f;
        private const float CurrentDotRadius = 4.5f;

        private static readonly Color BackgroundColor = new(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color BorderColor = new(0.32f, 0.36f, 0.44f, 1f);
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.06f);
        private static readonly Color AxisColor = new(1f, 1f, 1f, 0.14f);
        private static readonly Color FillColor = new(0.20f, 0.55f, 0.95f, 0.20f);
        private static readonly Color HighlightFillColor = new(0.20f, 0.55f, 0.95f, 0.38f);
        private static readonly Color BaseLineColor = new(0.47f, 0.74f, 1f, 0.52f);
        private static readonly Color HighlightLineColor = new(0.47f, 0.74f, 1f, 0.98f);
        private static readonly Color CurrentLineColor = new(1f, 0.37f, 0.31f, 0.95f);
        private static readonly Color CurrentDotColor = new(1f, 0.37f, 0.31f, 1f);
        private static readonly Color CurrentDotInnerColor = new(1f, 0.82f, 0.76f, 0.95f);

        internal readonly struct SampleViewData
        {
            public SampleViewData(string clipKey, float threshold, float weight)
            {
                ClipKey = clipKey ?? string.Empty;
                Threshold = threshold;
                Weight = Mathf.Clamp01(weight);
            }

            public string ClipKey { get; }
            public float Threshold { get; }
            public float Weight { get; }
        }

        internal readonly struct GraphData
        {
            public GraphData(
                IReadOnlyList<SampleViewData> samples,
                float currentValue,
                float minValue,
                float maxValue,
                bool dragEnabled,
                Action onDragStarted,
                Action<float> onCurrentValueChanged)
            {
                Samples = samples;
                CurrentValue = currentValue;
                MinValue = minValue;
                MaxValue = maxValue;
                DragEnabled = dragEnabled;
                OnDragStarted = onDragStarted;
                OnCurrentValueChanged = onCurrentValueChanged;
            }

            public IReadOnlyList<SampleViewData> Samples { get; }
            public float CurrentValue { get; }
            public float MinValue { get; }
            public float MaxValue { get; }
            public bool DragEnabled { get; }
            public Action OnDragStarted { get; }
            public Action<float> OnCurrentValueChanged { get; }
        }

        private GraphData m_Data;
        private bool m_IsDragging;
        private int m_ActivePointerId = PointerId.invalidPointerId;
        private Rect m_GraphRect;
        private Vector2 m_ValueRange;
        private Vector2 m_ClampRange;

        public XAnimationBlend1DGraphElement()
        {
            style.height = Height;
            style.minHeight = Height;
            style.maxHeight = Height;
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
            Rect currentRect = contentRect;
            if (currentRect.width <= 0f || currentRect.height <= 0f)
            {
                return;
            }

            m_GraphRect = new Rect(
                currentRect.xMin + PaddingX,
                currentRect.yMin + PaddingTop,
                Mathf.Max(1f, currentRect.width - PaddingX * 2f),
                Mathf.Max(1f, currentRect.height - PaddingTop - PaddingBottom));
            m_ClampRange = NormalizeClampRange(m_Data.MinValue, m_Data.MaxValue);
            m_ValueRange = ComputeDisplayRange(m_Data.Samples, m_ClampRange);

            DrawBackground(painter, m_GraphRect);
            DrawStaticEnvelopes(painter, m_GraphRect, m_ValueRange, m_ClampRange, m_Data.Samples);
            DrawCurrentValueIndicator(
                painter,
                m_GraphRect,
                m_ValueRange,
                Mathf.Clamp(m_Data.CurrentValue, m_ClampRange.x, m_ClampRange.y));
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || !m_Data.DragEnabled)
            {
                return;
            }

            if (!TryMapLocalPointToValue(evt.localPosition, out float value))
            {
                return;
            }

            m_IsDragging = true;
            m_ActivePointerId = evt.pointerId;
            this.CapturePointer(m_ActivePointerId);
            m_Data.OnDragStarted?.Invoke();
            m_Data.OnCurrentValueChanged?.Invoke(value);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!m_IsDragging || m_ActivePointerId != evt.pointerId || !this.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            if (!TryMapLocalPointToValue(evt.localPosition, out float value))
            {
                return;
            }

            m_Data.OnCurrentValueChanged?.Invoke(value);
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

        private static Vector2 NormalizeClampRange(float minValue, float maxValue)
        {
            float min = minValue;
            float max = maxValue;
            if (Mathf.Approximately(min, max))
            {
                max = min + 1f;
            }

            if (min > max)
            {
                (min, max) = (max, min);
            }

            return new Vector2(min, max);
        }

        private static Vector2 ComputeDisplayRange(IReadOnlyList<SampleViewData> samples, Vector2 clampRange)
        {
            float min = clampRange.x;
            float max = clampRange.y;
            if (samples != null)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    float threshold = samples[i].Threshold;
                    min = Mathf.Min(min, threshold);
                    max = Mathf.Max(max, threshold);
                }
            }

            if (Mathf.Approximately(min, max))
            {
                max = min + 1f;
            }

            float padding = Mathf.Max(0.05f, (max - min) * 0.08f);
            return new Vector2(min - padding, max + padding);
        }

        private static void DrawBackground(Painter2D painter, Rect graphRect)
        {
            DrawRectOutline(painter, graphRect, BorderColor, 1f);

            float midY = graphRect.yMin + graphRect.height * 0.5f;
            DrawLine(painter, new Vector2(graphRect.xMin, graphRect.yMax), new Vector2(graphRect.xMax, graphRect.yMax), AxisColor, 1.25f);
            DrawLine(painter, new Vector2(graphRect.xMin, midY), new Vector2(graphRect.xMax, midY), GridColor, 1f);
        }

        private static void DrawStaticEnvelopes(
            Painter2D painter,
            Rect graphRect,
            Vector2 displayRange,
            Vector2 clampRange,
            IReadOnlyList<SampleViewData> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                SampleViewData sample = samples[i];
                float weight = Mathf.Clamp01(sample.Weight);
                Color fillColor = Color.Lerp(FillColor, HighlightFillColor, weight);
                Color lineColor = Color.Lerp(BaseLineColor, HighlightLineColor, weight);

                float leftThreshold = i > 0 ? samples[i - 1].Threshold : clampRange.x;
                float rightThreshold = i < samples.Count - 1 ? samples[i + 1].Threshold : clampRange.y;
                float apexThreshold = Mathf.Clamp(sample.Threshold, clampRange.x, clampRange.y);

                float xLeft = MapValueToX(graphRect, displayRange, leftThreshold);
                float xApex = MapValueToX(graphRect, displayRange, apexThreshold);
                float xRight = MapValueToX(graphRect, displayRange, rightThreshold);
                float xClampMin = MapValueToX(graphRect, displayRange, clampRange.x);
                float xClampMax = MapValueToX(graphRect, displayRange, clampRange.y);
                float yTop = graphRect.yMin;
                float yBottom = graphRect.yMax;

                painter.fillColor = fillColor;
                painter.BeginPath();
                if (i == 0)
                {
                    painter.MoveTo(new Vector2(xClampMin, yBottom));
                    painter.LineTo(new Vector2(xClampMin, yTop));
                    painter.LineTo(new Vector2(xApex, yTop));
                    painter.LineTo(new Vector2(xRight, yBottom));
                }
                else if (i == samples.Count - 1)
                {
                    painter.MoveTo(new Vector2(xLeft, yBottom));
                    painter.LineTo(new Vector2(xApex, yTop));
                    painter.LineTo(new Vector2(xClampMax, yTop));
                    painter.LineTo(new Vector2(xClampMax, yBottom));
                }
                else
                {
                    painter.MoveTo(new Vector2(xLeft, yBottom));
                    painter.LineTo(new Vector2(xApex, yTop));
                    painter.LineTo(new Vector2(xRight, yBottom));
                }

                painter.ClosePath();
                painter.Fill();

                painter.lineWidth = 1.5f;
                painter.strokeColor = lineColor;
                painter.BeginPath();
                if (i == 0)
                {
                    painter.MoveTo(new Vector2(xClampMin, yTop));
                    painter.LineTo(new Vector2(xApex, yTop));
                    painter.MoveTo(new Vector2(xApex, yTop));
                    painter.LineTo(new Vector2(xRight, yBottom));
                }
                else if (i == samples.Count - 1)
                {
                    painter.MoveTo(new Vector2(xLeft, yBottom));
                    painter.LineTo(new Vector2(xApex, yTop));
                    painter.MoveTo(new Vector2(xApex, yTop));
                    painter.LineTo(new Vector2(xClampMax, yTop));
                }
                else
                {
                    painter.MoveTo(new Vector2(xLeft, yBottom));
                    painter.LineTo(new Vector2(xApex, yTop));
                    painter.MoveTo(new Vector2(xApex, yTop));
                    painter.LineTo(new Vector2(xRight, yBottom));
                }

                painter.Stroke();
                DrawFilledCircle(painter, new Vector2(xApex, yTop), SampleDotRadius, lineColor);
            }
        }

        private static void DrawCurrentValueIndicator(Painter2D painter, Rect graphRect, Vector2 valueRange, float currentValue)
        {
            float x = Mathf.Lerp(graphRect.xMin, graphRect.xMax, Mathf.InverseLerp(valueRange.x, valueRange.y, currentValue));
            Vector2 top = new(x, graphRect.yMin);
            Vector2 bottom = new(x, graphRect.yMax);
            DrawLine(painter, top, bottom, CurrentLineColor, 1.5f);
            Vector2 dotCenter = new(x, graphRect.yMax);
            DrawFilledCircle(painter, dotCenter, CurrentDotRadius, CurrentDotColor);
            DrawFilledCircle(painter, dotCenter, CurrentDotRadius - 1.75f, CurrentDotInnerColor);
        }

        private bool TryMapLocalPointToValue(Vector2 localPoint, out float value)
        {
            if (m_GraphRect.width <= 0f)
            {
                value = 0f;
                return false;
            }

            float minX = Mathf.Lerp(m_GraphRect.xMin, m_GraphRect.xMax, Mathf.InverseLerp(m_ValueRange.x, m_ValueRange.y, m_ClampRange.x));
            float maxX = Mathf.Lerp(m_GraphRect.xMin, m_GraphRect.xMax, Mathf.InverseLerp(m_ValueRange.x, m_ValueRange.y, m_ClampRange.y));
            float x = Mathf.Clamp(localPoint.x, minX, maxX);
            float normalized = Mathf.InverseLerp(minX, maxX, x);
            value = Mathf.Lerp(m_ClampRange.x, m_ClampRange.y, normalized);
            return true;
        }

        private static float MapValueToX(Rect graphRect, Vector2 valueRange, float value)
        {
            return Mathf.Lerp(graphRect.xMin, graphRect.xMax, Mathf.InverseLerp(valueRange.x, valueRange.y, value));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
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

        private static void DrawFilledCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Fill();
        }
    }
}
#endif
