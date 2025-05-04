using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    /// <summary>
    /// 图形绘制类
    /// </summary>
    public class ShapeGraphic : Graphic
    {
        private List<ShapeGraphicBase> m_Graphics;

        private List<UIVertex> m_Vertexs;
        private List<int> m_Triangles;

        protected override void Awake()
        {
            m_Graphics = new List<ShapeGraphicBase>();
            m_Vertexs = new List<UIVertex>();
            m_Triangles = new List<int>();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (m_Graphics != null && m_Graphics.Count > 0)
            {
                foreach (var item in m_Graphics)
                {
                    int offset = m_Vertexs.Count;
                    item.GetVertexs(ref m_Vertexs);
                    item.GetTriangles(ref m_Triangles, offset);
                }
            }
            vh.AddUIVertexStream(m_Vertexs, m_Triangles);
            m_Vertexs?.Clear();
            m_Triangles?.Clear();
        }

        public void Refresh()
        {
            SetVerticesDirty();
        }

        public void Clear()
        {
            m_Graphics.Clear();
            Refresh();
        }

        public void Add(ShapeGraphicBase graphic)
        {
            m_Graphics.Add(graphic);
            Refresh();
        }

        public void Add(List<ShapeGraphicBase> graphics)
        {
            m_Graphics.AddRange(graphics);
            Refresh();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 属性面板值发生变化时调用
        /// </summary>
        protected override void OnValidate()
        {
            SetVerticesDirty();
        }
#endif
    }
}