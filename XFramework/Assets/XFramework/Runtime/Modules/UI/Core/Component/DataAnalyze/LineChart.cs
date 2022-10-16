using System.Collections.Generic;
using XFramework.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    /// <summary>
    /// 折线图
    /// </summary>
    public class LineChart : Graphic
    {
        [Header("坐标轴显示属性")]
        public float width = 200;
        public float height = 200;
        public float lineWidth = 5;
        public Vector2 offset = new Vector2(2, 2);
        public Color axleColor = Color.red;

        [Header("坐标轴数据属性")]
        public Vector2 axleMaxValue = new Vector2(100, 100);
        public Vector2 axleMinValue = Vector3.zero;
        public List<Vector2[]> datas = new List<Vector2[]>();

        [Space]
        [Header("刻度线")]
        public int scaleLineHeight = 5;
        public Vector2 subdivisions = new Vector2(10, 10);

        [Space]
        [Header("数据")]
        public Color lineColor = Color.blue;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            DrawAxle(vh);
            DrawLine(vh);
        }

        /// <summary>
        /// 坐标轴
        /// </summary>
        /// <param name="vh"></param>
        private void DrawAxle(VertexHelper vh)
        {
            UIVertex[] verts = new UIVertex[4];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].color = axleColor;
            }

            // Y轴
            verts[0].position = offset;
            verts[1].position = new Vector2(lineWidth, 0) + offset;
            verts[2].position = new Vector2(lineWidth, height) + offset;
            verts[3].position = new Vector2(0, height) + offset;
            vh.AddUIVertexQuad(verts);

            // X轴
            verts[0].position = offset;
            verts[1].position = new Vector2(width, 0) + offset;
            verts[2].position = new Vector2(width, lineWidth) + offset;
            verts[3].position = new Vector2(0, lineWidth) + offset;
            vh.AddUIVertexQuad(verts);

            ScaleLine();
            Arrow();

            // 刻度线
            void ScaleLine()
            {
                Vector2 offset;

                for (int i = 1; i < subdivisions.x; i++)
                {
                    offset = this.offset + new Vector2(width / subdivisions.x * i, 0);
                    verts[0].position = offset;
                    verts[1].position = new Vector2(lineWidth, 0) + offset;
                    verts[2].position = new Vector2(lineWidth, scaleLineHeight) + offset;
                    verts[3].position = new Vector2(0, scaleLineHeight) + offset;
                    vh.AddUIVertexQuad(verts);
                }

                for (int i = 1; i < subdivisions.y; i++)
                {
                    offset = this.offset + new Vector2(0, height / subdivisions.y * i);
                    verts[0].position = offset;
                    verts[1].position = new Vector2(scaleLineHeight, 0) + offset;
                    verts[2].position = new Vector2(scaleLineHeight, lineWidth) + offset;
                    verts[3].position = new Vector2(0, lineWidth) + offset;
                    vh.AddUIVertexQuad(verts);
                }
            }

            // 箭头
            void Arrow()
            {
                Vector2 offset;
                float arrowLength = width / subdivisions.x / 2;

                // X轴
                offset = this.offset + new Vector2(width, 0);
                verts[0].position = offset;
                verts[1].position = new Vector2(0, lineWidth) + offset;
                verts[2].position = new Vector2(-arrowLength, lineWidth + scaleLineHeight) + offset;
                verts[3].position = new Vector2(-arrowLength, scaleLineHeight) + offset;
                vh.AddUIVertexQuad(verts);

                verts[0].position = new Vector2(0, lineWidth) + offset;
                verts[1].position = offset;
                verts[2].position = new Vector2(-arrowLength, -lineWidth - scaleLineHeight) + offset;
                verts[3].position = new Vector2(-arrowLength, -scaleLineHeight) + offset;
                vh.AddUIVertexQuad(verts);

                // Y轴
                offset = this.offset + new Vector2(0, height);
                verts[0].position = offset;
                verts[1].position = new Vector2(lineWidth, 0) + offset;
                verts[2].position = new Vector2(lineWidth + scaleLineHeight, -arrowLength) + offset;
                verts[3].position = new Vector2(scaleLineHeight, -arrowLength) + offset;
                vh.AddUIVertexQuad(verts);

                verts[0].position = offset;
                verts[1].position = new Vector2(lineWidth, 0) + offset;
                verts[2].position = new Vector2(lineWidth - scaleLineHeight, -arrowLength) + offset;
                verts[3].position = new Vector2(-scaleLineHeight, -arrowLength) + offset;
                vh.AddUIVertexQuad(verts);
            }
        }

        /// <summary>
        /// 画数据折线
        /// </summary>
        private void DrawLine(VertexHelper vh)
        {
            foreach (var data in datas)
            {
                Vector2[] pixelPoints = new Vector2[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    pixelPoints[i].x = (data[i].x - axleMinValue.x) / axleMaxValue.x * width;
                    pixelPoints[i].y = (data[i].y - axleMinValue.y) / axleMaxValue.y * height;
                    pixelPoints[i] += offset;
                }

                UIVertex[] verts = new UIVertex[4];
                for (int i = 0; i < verts.Length; i++)
                {
                    verts[i].color = lineColor;
                }

                for (int i = 0; i < pixelPoints.Length - 1; i++)
                {
                    SetVerts(pixelPoints[i], pixelPoints[i + 1], lineWidth, verts);
                    vh.AddUIVertexQuad(verts);
                }

                // 点状数据
                //foreach (var item in pixelPoints)
                //{
                //    verts[0].position = item;
                //    verts[1].position = new Vector2(5, 0) + item;
                //    verts[2].position = new Vector2(5, 5) + item;
                //    verts[3].position = new Vector2(0, 5) + item;
                //    vh.AddUIVertexQuad(verts);
                //}

                void SetVerts(Vector2 _start, Vector2 _end, float _width, UIVertex[] _verts)
                {
                    Vector2[] tmp = Math2d.GetRect(_start, _end, _width);
                    _verts[0].position = tmp[0];
                    _verts[1].position = tmp[1];
                    _verts[2].position = tmp[3];
                    _verts[3].position = tmp[2];
                }
            }
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="datas"></param>
        public void AddDatas(Vector2[] datas)
        {
            this.datas.Add(datas);
            SetVerticesDirty();
        }

        /// <summary>
        /// 清空数据
        /// </summary>
        public void Clear()
        {
            datas.Clear();
            SetVerticesDirty();
        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        public void Refresh()
        {
            SetAllDirty();
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