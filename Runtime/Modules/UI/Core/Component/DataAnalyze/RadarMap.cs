using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    /// <summary>
    /// 雷达图
    /// </summary>
    public class RadarMap : Graphic
    {
        
        /// <summary>
        /// 边的数量
        /// </summary>
        [SerializeField] private int m_SideCount = 6;
        /// <summary>
        /// 多边形数量
        /// </summary>
        [SerializeField] private int m_Split = 3;
        /// <summary>
        /// 多边形半径
        /// </summary>
        [SerializeField] private float m_Radius = 100;
        /// <summary>
        /// 边的颜色
        /// </summary>
        [SerializeField] private Color m_LineColor = Color.red;
        /// <summary>
        /// 线宽（像素）
        /// </summary>
        [SerializeField] private float m_Width = 1;
        /// <summary>
        /// 最小值
        /// </summary>
        [SerializeField] private float m_MinValue = -0.33333f;
        /// <summary>
        /// 最大值
        /// </summary>
        [SerializeField] private float m_MaxValue = 1;

        /// <summary>
        /// 数据
        /// </summary>
        private float[] m_Values;


        private readonly List<UIVertex> m_Vertexs = new List<UIVertex>();
        private readonly List<int> m_Triangles = new List<int>();

        /// <summary>
        /// 边的数量
        /// </summary>
        public int SideCount
        {
            get => m_SideCount;
            set
            {
                if (m_SideCount != value)
                {
                    m_SideCount = value;

                    var newValues = new float[value];

                    int length = m_Values.Length < newValues.Length ? m_Values.Length : newValues.Length;

                    for (int i = 0; i < length; i++)
                    {
                        newValues[i] = m_Values[i];
                    }

                    m_Values = newValues;
                }
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 边的数量
        /// </summary>
        public int Split
        {
            get => m_Split;
            set
            {
                m_Split = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 多边形半径
        /// </summary>
        public float Radius
        {
            get => m_Radius;
            set
            {
                m_Radius = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 边的颜色
        /// </summary>
        public Color LineColor
        {
            get => m_LineColor;
            set
            {
                m_LineColor = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 线宽（像素）
        /// </summary>
        public float Width
        {
            get => m_Width;
            set
            {
                m_Width = value;
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            m_Values = new float[m_SideCount];
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Vector2[] polygonVertex = new Vector2[m_SideCount];
            float rad = 2 * Mathf.PI / m_SideCount; // 边对应的弧度
            for (int i = 0; i < m_SideCount; i++)
            {
                float tempRad = rad * i;
                polygonVertex[i].x = m_Radius * Mathf.Sin(tempRad);
                polygonVertex[i].y = m_Radius * Mathf.Cos(tempRad);
            }

            DrawRadar(polygonVertex);
            DrawData(polygonVertex);

            vh.AddUIVertexStream(m_Vertexs, m_Triangles);

            m_Vertexs.Clear();
            m_Triangles.Clear();
        }

        /// <summary>
        /// 绘制雷达基础图
        /// </summary>
        /// <param name="vh"></param>
        private void DrawRadar(Vector2[] bounds)
        {
            Vector2[] polygonVertex = new Vector2[bounds.Length];


            int roundCount = bounds.Length * 2; // 一圈的顶点数量

            // 绘制多边形
            for (int index = 0; index < m_Split; index++)
            {
                // 计算一个多边形 

                int vertexOffset = index * roundCount;  // 顶点索引偏移

                for (int i = 0; i < m_SideCount; i++)
                {
                    polygonVertex[i] = bounds[i] / m_Split * (index + 1);
                }

                for (int i = 0; i < polygonVertex.Length; i++)
                {
                    Vector2 dir = polygonVertex[i].normalized;
                    m_Vertexs.Add(new UIVertex() { position = polygonVertex[i] - dir * m_Width, color = m_LineColor });
                    m_Vertexs.Add(new UIVertex() { position = polygonVertex[i] + dir * m_Width, color = m_LineColor });
                }

                for (int i = 0; i < polygonVertex.Length; i++)
                {
                    m_Triangles.Add(i * 2 + vertexOffset);
                    m_Triangles.Add(i * 2 + 1 + vertexOffset);
                    m_Triangles.Add((i * 2 + 2) % (roundCount) + vertexOffset);

                    m_Triangles.Add((i * 2 + 2) % (roundCount) + vertexOffset);
                    m_Triangles.Add(i * 2 + 1 + vertexOffset);
                    m_Triangles.Add((i * 2 + 3) % (roundCount) + vertexOffset);
                }
            }

            // 绘制中心点到边界的线
            int offset = m_Vertexs.Count;
            //offset = 0;

            for (int i = 0, length = polygonVertex.Length; i < length; i++)
            {
                int next = (i + 1) % length;
                int pre = (i + length - 1) % length;
                var inDir = (polygonVertex[i] + polygonVertex[pre]).normalized;
                var outDir = Mathematics.Math2d.GetHorizontalDir(polygonVertex[i]);

                m_Vertexs.Add(new UIVertex() { position = inDir * m_Width, color = m_LineColor });

                m_Vertexs.Add(new UIVertex() { position = polygonVertex[i] - outDir * m_Width, color = m_LineColor });
                m_Vertexs.Add(new UIVertex() { position = polygonVertex[i] + outDir * m_Width, color = m_LineColor });

                m_Triangles.Add(i * 3 + 0 + offset);
                m_Triangles.Add(i * 3 + 1 + offset);
                m_Triangles.Add(i * 3 + 2 + offset);

                // 注释这一部分，把inDir的 + 替换成 -, 修改width会有一些好玩的效果
                m_Triangles.Add(i * 3 + 0 + offset);
                m_Triangles.Add(i * 3 + 2 + offset);
                m_Triangles.Add(next * 3 + 0 + offset);
            }
        }

        /// <summary>
        /// 绘制数据到雷达图上
        /// </summary>
        /// <param name="vh"></param>
        private void DrawData(Vector2[] bounds)
        {
            if(m_Values.Length != m_SideCount)
            {
                return;
            }

            float c = m_MaxValue - m_MinValue;  // 系数
            int offset = m_Vertexs.Count;

            m_Vertexs.Add(new UIVertex() { position = Vector3.zero, color = color });
            for (int i = 0; i < bounds.Length; i++)
            {
                m_Vertexs.Add(new UIVertex()
                {
                    position = bounds[i] * (m_Values[i] - m_MinValue) / c,
                    color = color,
                });
            }

            for (int i = 0; i < m_SideCount; i++)
            {
                m_Triangles.Add(0 + offset);
                m_Triangles.Add(i + 1 + offset);
                m_Triangles.Add((i + 2 > m_SideCount ? 1 : i + 2) + offset);
            }
        }

        /// <summary>
        /// 设置数据
        /// </summary>
        /// <param name="values">数据集</param>
        /// <param name="useAnmi">是否使用动画</param>
        public void SetData(float[] values, bool useAnmi = false)
        {
            if(values.Length != m_SideCount)
            {
                Debug.LogError("数据应尽量和雷达图边数保持一致");
            }

            SideCount = values.Length;

            // RadarMap不可见Awake没初始化m_Values，在此初始化
            if (m_Values == null)
            {
                m_Values = new float[m_SideCount];
            }

            if (!useAnmi)
            {
                m_Values = values;
                SetVerticesDirty();
            }
            else
            {
                if (twnner != null)
                {
                    MonoEvent.Instance.StopCoroutine(twnner);
                }
                twnner = MonoEvent.Instance.StartCoroutine(StartAnim(m_Values, values));
            }
        }
        private Coroutine twnner;
        IEnumerator StartAnim(float[] start, float[] end)
        {
            m_Values = new List<float>(start).ToArray();

            for (int i = 1; i < 51; i++)
            {
                yield return new WaitForSeconds(0.01f);

                for (int j = 0; j < m_Values.Length; j++)
                {
                    m_Values[j] = Mathf.Lerp(start[j], end[j], i / 50f);
                }

                SetVerticesDirty();
            }
        }

        private void Update()
        {
            
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