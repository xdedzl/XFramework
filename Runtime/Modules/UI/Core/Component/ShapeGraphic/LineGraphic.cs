using System.Collections.Generic;
using XFramework.Mathematics;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 线绘制类
    /// </summary>
    public class LineGraphic : ShapeGraphicBase
    {
        protected List<Vector3> m_Poses;
        public float width = 0.5f;
        public Color color = Color.red;

        public LineGraphic() { }

        public LineGraphic(List<Vector3> poses)
        {
            m_Poses = poses;
        }

        public LineGraphic(List<Vector2> poses)
        {
            m_Poses = new List<Vector3>(poses.Count);

            foreach (var item in poses)
            {
                m_Poses.Add(new Vector3(item.x, item.y, 0));
            }
        }

        public override void GetVertexs(ref List<UIVertex> vertexs)
        {
            if (m_Poses.Count < 2)
            {
                return;
            }

            int count = m_Poses.Count;
            Vector2 dir = Math2d.GetHorizontalDir((m_Poses[1] - m_Poses[0]).XY());
            vertexs.Add(new UIVertex() { position = m_Poses[0].XY() - dir * width, color = color, });
            vertexs.Add(new UIVertex() { position = m_Poses[0].XY() + dir * width, color = color, });


            for (int i = 1; i < count - 1; i++)
            {
                dir = Math2d.GetHorizontalDir((m_Poses[i + 1] - m_Poses[i - 1]).XY());
                vertexs.Add(new UIVertex() { position = m_Poses[i].XY() - dir * width, color = color, });
                vertexs.Add(new UIVertex() { position = m_Poses[i].XY() + dir * width, color = color });
            }

            dir = Math2d.GetHorizontalDir((m_Poses[count - 1] - m_Poses[count - 2]).XY());
            vertexs.Add(new UIVertex() { position = m_Poses[count - 1].XY() - dir * width, color = color, });
            vertexs.Add(new UIVertex() { position = m_Poses[count - 1].XY() + dir * width, color = color, });
        }

        public override void GetTriangles(ref List<int> triangles, int offset)
        {
            if (m_Poses.Count < 2)
            {
                return;
            }

            for (int i = 0; i < m_Poses.Count; i++)
            {
                int next = (i + 1) % m_Poses.Count;
                // 添加三jio形排序
                triangles.Add(2 * i + 1 + offset);
                triangles.Add(2 * i + offset);
                triangles.Add(2 * next + offset);

                triangles.Add(2 * i + 1 + offset);
                triangles.Add(2 * next + offset);
                triangles.Add(2 * next + 1 + offset);
            }
        }
    }
}