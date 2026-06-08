using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Resource;

namespace XFramework.NodeKit.Editor
{
    public class XNodeGraphView : GraphView
    {
        private readonly Dictionary<string, XEditorNodeBase> m_EditorNodeDict = new ();
        
        public XNodeGraphEditorWindow OwnerWindow { get; }
        
        public XNodeGraphView(XNodeGraphEditorWindow ownerWindow)
        {
            OwnerWindow = ownerWindow;
            style.width = Length.Percent(100);
            style.flexGrow = 1f;
            style.backgroundColor = new Color(0.04f, 0.04f, 0.04f, 1f);
            
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            var grid = new XNodeGraphGridBackground(this);
            Insert(0, grid);
            grid.StretchToParentSize();
            viewTransformChanged += _ => grid.MarkDirtyRepaint();
            
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());
            
            GameEntry.AddModule<ResourceManager>();
            var searchWindowProvider = XNodeSelectWindowProvider.Create(this, ownerWindow);
            
            nodeCreationRequest += context =>
            {
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindowProvider);
            };

            graphViewChanged += OnGraphViewChanged;
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();

            foreach (var port in ports.ToList())
            {
                if (startPort.node == port.node || startPort.direction == port.direction || startPort.portType != port.portType)
                {
                    continue;
                }

                compatiblePorts.Add(port);
            }

            return compatiblePorts;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // 删除节点
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is XEditorNodeBase node)
                    {
                        m_EditorNodeDict.Remove(node.GetId());
                    }
                }
            }

            OwnerWindow?.RefreshWindowStatus();
            
            return change;
        }

        public void AddXNode(XEditorNodeBase node)
        {
            node.SetOwnerGraphView(this);
            AddElement(node);
            m_EditorNodeDict[node.GetRuntimeNode().GetId()] = node;
            OwnerWindow?.RefreshWindowStatus();
        }

        public IEnumerable<XEditorNodeBase> GetAllXNodes()
        {
            return m_EditorNodeDict.Values;
        }

        public XEditorNodeBase GetXNode(string id)
        {
            return m_EditorNodeDict.GetValueOrDefault(id);
        }

        private sealed class XNodeGraphGridBackground : VisualElement
        {
            private const float MinorSpacing = 10f;
            private const int ThickLineEvery = 10;

            private static readonly Color BackgroundColor = new(0.04f, 0.04f, 0.04f, 1f);
            private static readonly Color MinorLineColor = new(0.76f, 0.77f, 0.75f, 0.10f);
            private static readonly Color ThickLineColor = new(0.76f, 0.77f, 0.75f, 0.16f);

            private readonly XNodeGraphView m_GraphView;

            public XNodeGraphGridBackground(XNodeGraphView graphView)
            {
                m_GraphView = graphView;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    return;
                }

                Painter2D painter = context.painter2D;
                DrawFilledRect(painter, rect, BackgroundColor);

                Vector3 translate = m_GraphView.contentViewContainer.resolvedStyle.translate;
                Vector3 scaleVector = m_GraphView.contentViewContainer.resolvedStyle.scale.value;
                float scale = Mathf.Max(0.01f, scaleVector.x);
                float spacing = MinorSpacing * scale;
                float offsetX = Mathf.Repeat(translate.x, spacing);
                float offsetY = Mathf.Repeat(translate.y, spacing);

                DrawGridLines(painter, rect, spacing, offsetX, offsetY);
            }

            private static void DrawGridLines(Painter2D painter, Rect rect, float spacing, float offsetX, float offsetY)
            {
                int lineIndex = Mathf.FloorToInt(-offsetX / spacing);
                for (float x = rect.xMin + offsetX; x < rect.xMax; x += spacing, lineIndex++)
                {
                    DrawLine(painter, new Vector2(x, rect.yMin), new Vector2(x, rect.yMax), GetLineColor(lineIndex), GetLineWidth(lineIndex));
                }

                lineIndex = Mathf.FloorToInt(-offsetY / spacing);
                for (float y = rect.yMin + offsetY; y < rect.yMax; y += spacing, lineIndex++)
                {
                    DrawLine(painter, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), GetLineColor(lineIndex), GetLineWidth(lineIndex));
                }
            }

            private static Color GetLineColor(int lineIndex)
            {
                return lineIndex % ThickLineEvery == 0 ? ThickLineColor : MinorLineColor;
            }

            private static float GetLineWidth(int lineIndex)
            {
                return lineIndex % ThickLineEvery == 0 ? 1.2f : 1f;
            }

            private static void DrawLine(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
            {
                painter.lineWidth = width;
                painter.strokeColor = color;
                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(to);
                painter.Stroke();
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
        }
    }  
}
