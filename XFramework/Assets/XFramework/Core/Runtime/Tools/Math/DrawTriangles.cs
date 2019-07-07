// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-10-31 12:01:34
// 版本： V 1.0
// ==========================================
using System.Collections.Generic;
using XFramework.Mathematics;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 封装所有画三角形的方法
    /// </summary>
    public class DrawTriangles
    {
        /// <summary>
        /// 圆柱空域
        /// </summary>
        public static void DrawCylinder(Vector3[] positions, float height, MeshFilter meshFilter,LineRenderer[] lineRenderers,Color BoradColor)
        {
            List<Vector3> verticesDown = new List<Vector3>();
            List<Vector3> verticesUp = new List<Vector3>();
            List<Vector3> vertices = new List<Vector3>();

            List<int> triangles = new List<int>();
            //上下面坐标赋值 数组转List
            for (int i = 0; i < positions.Length; i++)
            {
                verticesDown.Add(positions[i]);
            }
            for (int i = 0; i < positions.Length; i++)
            {
                verticesUp.Add(positions[i] + new Vector3(0, height, 0));
            }
            //计算一个平面的点数量
            int count = verticesDown.Count;

            //将上下平面点合并
            for (int i = 0; i < verticesDown.Count; i++)
            {
                vertices.Add(verticesDown[i]);
            }
            for (int i = 0; i < verticesUp.Count; i++)
            {
                vertices.Add(verticesUp[i]);
            }

            //获取底面和顶面的三角形排序
            List<int> trianglesDown = PhysicsMath.DrawPolygon(verticesDown, false);
            List<int> trianglesUp = PhysicsMath.DrawPolygon(verticesUp, true);

            for (int i = 0; i < trianglesDown.Count; i++)
            {
                trianglesUp[i] += count;
            }

            //合并底面顶面三角形排序
            for (int i = 0; i < trianglesDown.Count; i++)
            {
                triangles.Add(trianglesDown[i]);
            }
            for (int i = 0; i < trianglesUp.Count; i++)
            {
                triangles.Add(trianglesUp[i]);
            }

            //侧面三角形排序
            for (int i = 0; i < count - 1; i++)
            {
                // 加上侧面的点集
                vertices.Add(vertices[i]);
                vertices.Add(vertices[i + 1]);
                vertices.Add(vertices[i + count + 1]);
                vertices.Add(vertices[i + count]);

                // 侧面三角形排序
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 0 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 2 + i * 4);
            }

            // 加上最后一个侧面的点集
            vertices.Add(vertices[count - 1]);
            vertices.Add(vertices[0]);
            vertices.Add(vertices[count]);
            vertices.Add(vertices[2 * count - 1]);

            // 加上最后一个侧面的三角形排序
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 4);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 2);

            meshFilter.mesh.vertices = vertices.ToArray();
            meshFilter.mesh.triangles = triangles.ToArray();

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线

            //设置LineRenderer的属性
            foreach (var item in lineRenderers)
            {
                item.startWidth = 0.005f * height;
                item.endWidth = 0.005f * height;
                item.positionCount = positions.Length + 1;
                item.startColor = BoradColor;
                item.endColor = BoradColor;
            }

            //画上表面和下表面的连接线
            for (int i = 0; i < positions.Length; i++)
            {
                lineRenderers[0].SetPosition(i, positions[i]);
                lineRenderers[1].SetPosition(i, positions[i] + new Vector3(0, height, 0));
            }
            lineRenderers[0].SetPosition(positions.Length, positions[0]);
            lineRenderers[1].SetPosition(positions.Length, positions[0] + new Vector3(0, height, 0));
        }

        /// <summary>
        /// 通过两个mesh控制侧面和底面材质不同时(多边形空域)
        /// </summary>
        public static void DrawWithPointsDifferent(Vector3[] positions, float height, MeshFilter meshFilter_1, MeshFilter meshFilter_2, Material material_1, Material material_2)
        {
            int differentM = 0;
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < positions.Length; i++)
            {
                vertices.Add(positions[i]);
                vertices.Add(positions[i] + new Vector3(0, height, 0));
            }
            meshFilter_1.mesh.vertices = vertices.ToArray();
            meshFilter_2.mesh.vertices = vertices.ToArray();

            List<int> trangles_1 = new List<int>();
            List<int> trangles_2 = new List<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == positions.Length - 1)
                {
                    trangles_1.Add(i * 2);
                    trangles_1.Add(i * 2 + 1);
                    trangles_1.Add(0);

                    trangles_1.Add(0);
                    trangles_1.Add(i * 2 + 1);
                    trangles_1.Add(1);
                }
                else
                {
                    //构成一个侧面的两个三角形
                    trangles_1.Add(i * 2);
                    trangles_1.Add(i * 2 + 1);
                    trangles_1.Add((i + 1) * 2);

                    trangles_1.Add((i + 1) * 2);
                    trangles_1.Add(i * 2 + 1);
                    trangles_1.Add((i + 1) * 2 + 1);
                }
            }

            for (int i = 0; i < positions.Length - 2; i++)
            {
                //顶面
                trangles_2.Add(1);
                trangles_2.Add((i + 2) * 2 + 1);
                trangles_2.Add((i + 1) * 2 + 1);
                //底面
                trangles_2.Add(0);
                trangles_2.Add((i + 1) * 2);
                trangles_2.Add((i + 2) * 2);
                differentM += 6;
            }
            meshFilter_1.mesh.triangles = trangles_1.ToArray();
            meshFilter_2.mesh.triangles = trangles_2.ToArray();

            meshFilter_1.mesh.RecalculateBounds();     // 重置范围
            meshFilter_1.mesh.RecalculateNormals();    // 重置法线
            meshFilter_1.mesh.RecalculateTangents();    // 重置切线
            meshFilter_2.mesh.RecalculateBounds();     // 重置范围
            meshFilter_2.mesh.RecalculateNormals();    // 重置法线
            meshFilter_2.mesh.RecalculateTangents();    // 重置切线
        }
        
        /// <summary>
        /// 凹多边形空域
        /// </summary>
        /// <param name="positions"></param>
        /// <param name="height"></param>
        /// <param name="meshFilter"></param>
        public static void DrawPolygon(Vector3[] positions, float height, MeshFilter meshFilter,LineRenderer[] lineRenderers,Color BoradColor)
        {
            List<Vector3> verticesDown = new List<Vector3>();
            List<Vector3> verticesUp = new List<Vector3>();
            List<Vector3> vertices = new List<Vector3>();

            List<int> triangles = new List<int>();
            //上下面坐标赋值 数组转List
            for (int i = 0; i < positions.Length; i++)
            {
                verticesDown.Add(positions[i]);
            }
            for (int i = 0; i < positions.Length; i++)
            {
                verticesUp.Add(positions[i] + new Vector3(0, height, 0));
            }
            //计算一个平面的点数量
            int count = verticesDown.Count;



            //将上下平面点合并
            for (int i = 0; i < verticesDown.Count; i++)
            {
                vertices.Add(verticesDown[i]);
            }
            for (int i = 0; i < verticesUp.Count; i++)
            {
                vertices.Add(verticesUp[i]);
            }

            //获取底面和顶面的三角形排序
            List<int> trianglesDown = PhysicsMath.DrawPolygon(verticesDown, false);
            List<int> trianglesUp = PhysicsMath.DrawPolygon(verticesUp, true);

            for (int i = 0; i < trianglesDown.Count; i++)
            {
                trianglesUp[i] += count;
            }

            //合并底面顶面三角形排序
            for (int i = 0; i < trianglesDown.Count; i++)
            {
                triangles.Add(trianglesDown[i]);
            }
            for (int i = 0; i < trianglesUp.Count; i++)
            {
                triangles.Add(trianglesUp[i]);
            }

            //侧面三角形排序
            for (int i = 0; i < count - 1; i++)
            {
                // 加上侧面的点集
                vertices.Add(vertices[i]);
                vertices.Add(vertices[i + 1]);
                vertices.Add(vertices[i + count + 1]);
                vertices.Add(vertices[i + count]);
                                
                // 侧面三角形排序
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 0 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 2 + i * 4);
            }

            // 加上最后一个侧面的点集
            vertices.Add(vertices[count - 1]);
            vertices.Add(vertices[0]);
            vertices.Add(vertices[count]);
            vertices.Add(vertices[2 * count - 1]);

            // 加上最后一个侧面的三角形排序
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 4);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 2);
            
            meshFilter.mesh.vertices = vertices.ToArray();
            meshFilter.mesh.triangles = triangles.ToArray();

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线

            //设置LineRenderer的属性
            foreach (var item in lineRenderers)
            {
                item.startWidth = 0.005f * height;
                item.endWidth = 0.005f * height;
                item.positionCount = positions.Length + 1;
                item.startColor = BoradColor;
                item.endColor = BoradColor;
            }
            lineRenderers[2].positionCount = positions.Length * 3;

            //画上表面和下表面的连接线
            for (int i = 0; i < positions.Length; i++)
            {
                lineRenderers[0].SetPosition(i, positions[i]);
                lineRenderers[1].SetPosition(i, positions[i] + new Vector3(0, height, 0));
            }
            lineRenderers[0].SetPosition(positions.Length, positions[0]);
            lineRenderers[1].SetPosition(positions.Length, positions[0] + new Vector3(0, height, 0));

            //画侧面的线
            for (int i = 0, j = 0; i < positions.Length; i++)
            {
                lineRenderers[2].SetPosition(j++, vertices[i]);
                lineRenderers[2].SetPosition(j++, vertices[i + positions.Length]);
                lineRenderers[2].SetPosition(j++, vertices[i]);
            }
        }

        /// <summary>
        /// 凹多边形空域 每个点高度不一样时 可以用来画扇形区域
        /// </summary>
        /// <param name="positions"></param>
        /// <param name="meshFilter"></param>
        /// <param name="lineRenderers"></param>
        public static void DrawPolygon(Vector3[] positions, MeshFilter meshFilter,LineRenderer[] lineRenderers,Color BoradColor)
        {
            List<Vector3> verticesDown = new List<Vector3>();
            List<Vector3> verticesUp = new List<Vector3>();
            List<Vector3> vertices = new List<Vector3>();

            List<int> triangles = new List<int>();
            //上下面坐标赋值 数组转List
            for (int i = 0; i < positions.Length / 2; i++)
            {
                verticesDown.Add(positions[i]);
            }
            for (int i = positions.Length / 2; i < positions.Length; i++)
            {
                verticesUp.Add(positions[i]);
            }
            //计算一个平面的点数量
            int count = verticesDown.Count;

            //将上下平面点合并
            for (int i = 0; i < verticesDown.Count; i++)
            {
                vertices.Add(verticesDown[i]);
            }
            for (int i = 0; i < verticesUp.Count; i++)
            {
                vertices.Add(verticesUp[i]);
            }

            //获取底面和顶面的三角形排序
            List<int> trianglesDown = PhysicsMath.DrawPolygon(verticesDown, false);
            List<int> trianglesUp = PhysicsMath.DrawPolygon(verticesUp, true);

            for (int i = 0; i < trianglesDown.Count; i++)
            {
                trianglesUp[i] += count;
            }

            //合并底面顶面三角形排序
            for (int i = 0; i < trianglesDown.Count; i++)
            {
                triangles.Add(trianglesDown[i]);
            }
            for (int i = 0; i < trianglesUp.Count; i++)
            {
                triangles.Add(trianglesUp[i]);
            }

            //侧面三角形排序
            for (int i = 0; i < count - 1; i++)
            {
                // 加上侧面的点集
                vertices.Add(vertices[i]);
                vertices.Add(vertices[i + 1]);
                vertices.Add(vertices[i + count + 1]);
                vertices.Add(vertices[i + count]);

                // 侧面三角形排序
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 0 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 2 + i * 4);
            }

            // 加上最后一个侧面的点集
            vertices.Add(vertices[count - 1]);
            vertices.Add(vertices[0]);
            vertices.Add(vertices[count]);
            vertices.Add(vertices[2 * count - 1]);

            // 加上最后一个侧面的三角形排序
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 4);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 2);

            meshFilter.mesh.vertices = vertices.ToArray();
            meshFilter.mesh.triangles = triangles.ToArray();

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线
            
            //设置LineRenderer属性
            foreach (var item in lineRenderers)
            {
                item.startWidth = 5;
                item.endWidth = 5;
                item.positionCount = 2;
                item.startColor = BoradColor;
                item.endColor = BoradColor;
            }
            lineRenderers[0].positionCount = positions.Length + 1;

            //画两个弧面线及和圆心点的连接线
            for (int i = 0; i < positions.Length; i++)
            {
                lineRenderers[0].SetPosition(i, positions[i]);
            }
            lineRenderers[0].SetPosition(positions.Length, positions[0]);


            lineRenderers[1].SetPosition(0, positions[1]);
            lineRenderers[1].SetPosition(1, positions[positions.Length / 2 + 1]);
            lineRenderers[2].SetPosition(0, positions[positions.Length / 2 - 1]);
            lineRenderers[2].SetPosition(1, positions[positions.Length - 1]);
        }

        /// <summary>
        /// Resion 1.0 空中走廊
        /// </summary>
        /// <param name="positions">空中走廊所有顶点</param>
        public static void DrawAirCorridorSpace(Vector3[] positions, MeshFilter meshFilter,LineRenderer[] lineRenderers,Color BoradColor)
        {
            //meshFilter.mesh.vertices = positions;   // 赋值点集

            List<Vector3> verices = new List<Vector3>();
            List<int> trangles = new List<int>();
            int count = positions.Length - 4;

            // 左侧面
            verices.Add(positions[0]);
            verices.Add(positions[1]);
            verices.Add(positions[2]);
            verices.Add(positions[3]);
            trangles.Add(3);
            trangles.Add(1);
            trangles.Add(0);
            trangles.Add(3);
            trangles.Add(2);
            trangles.Add(1);



            // 走廊框体
            for (int i = 0; i < count; i++)
            {
                if (i % 4 == 3)
                {
                    verices.Add(positions[i]);
                    verices.Add(positions[i - 3]);
                    verices.Add(positions[i + 1]);
                    verices.Add(positions[i + 4]);
                }
                else
                {
                    verices.Add(positions[i]);
                    verices.Add(positions[i + 1]);
                    verices.Add(positions[i + 5]);
                    verices.Add(positions[i + 4]);
                }

                trangles.Add((i + 1) * 4);
                trangles.Add((i + 1) * 4 + 2);
                trangles.Add((i + 1) * 4 + 3);
                trangles.Add((i + 1) * 4);
                trangles.Add((i + 1) * 4 + 1);
                trangles.Add((i + 1) * 4 + 2);
            }

            // 右侧面
            verices.Add(positions[count]);
            verices.Add(positions[count + 1]);
            verices.Add(positions[count + 2]);
            verices.Add(positions[count + 3]);
            trangles.Add(verices.Count - 4);
            trangles.Add(verices.Count -2);
            trangles.Add(verices.Count - 1);
            trangles.Add(verices.Count - 4);
            trangles.Add(verices.Count - 3);
            trangles.Add(verices.Count - 2);

            meshFilter.mesh.vertices = verices.ToArray();
            meshFilter.mesh.triangles = trangles.ToArray();

            //立体感
            float tempHeight = positions[1].y - positions[0].y;
            foreach (var item in lineRenderers)
            {
                item.startWidth = 0.005f * tempHeight;
                item.endWidth = 0.005f * tempHeight;
                item.positionCount = positions.Length / 4;
                item.startColor = BoradColor;
                item.endColor = BoradColor;
            }

            //画三条顶面和底面线
            for (int i = 0, j = 0; i < positions.Length / 4; i++, j += 4)
            {
                lineRenderers[0].SetPosition(i, positions[j + 1]);//右上
                lineRenderers[1].SetPosition(i, positions[j + 2]);//左上
                lineRenderers[2].SetPosition(i, positions[j + 3]);//左下
            }

            lineRenderers[3].positionCount = (positions.Length / 4) * 5;

            for (int i = 0, j = 0; i < positions.Length; i += 4)
            {
                lineRenderers[3].SetPosition(j++, positions[i]);
                lineRenderers[3].SetPosition(j++, positions[i + 1]);
                lineRenderers[3].SetPosition(j++, positions[i + 2]);
                lineRenderers[3].SetPosition(j++, positions[i + 3]);
                lineRenderers[3].SetPosition(j++, positions[i]);
            }

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线
        }

        /// <summary>
        /// Resion 2.0 带弧形的空中走廊
        /// </summary>
        /// <param name="_bottomList">下底面排序</param>
        /// <param name="_width">宽度</param>
        /// <param name="_height">高度</param>
        /// <param name="_squareCount">弧边数量</param>
        /// <returns></returns>
        public static void GetAirSpaceWithSector(List<Vector3> _bottomList, float _width, float _height, int _squareCount,MeshFilter meshFilter)
        {
            List<Vector3> outList = new List<Vector3>();       //输出点集合
            List<Vector3> topList = new List<Vector3>();       //顶面点集合
            List<Vector3> allSquareList = new List<Vector3>(); // 所有矩形点集
            List<Vector3> allArcList = new List<Vector3>();    // 所有弧形点集
            List<int> triangles = new List<int>();             // 存储三角形排序集合

            foreach (var item in _bottomList)
            {
                topList.Add(item + Vector3.up * _height);// 添加顶面点集
            }

            #region 矩形所有
            //合并矩形上下底面点集
            for (int i = 0; i < _squareCount * 4; i += 2)
            {
                allSquareList.Add(_bottomList[i]);
                allSquareList.Add(_bottomList[i + 1]);
                allSquareList.Add(topList[i + 1]);
                allSquareList.Add(topList[i]);
            }
            int bottomCount = _bottomList.Count;

            //画最左侧面
            outList.Add(allSquareList[0]);
            outList.Add(allSquareList[1]);
            outList.Add(allSquareList[2]);
            outList.Add(allSquareList[3]);

            triangles.Add(3);
            triangles.Add(0);
            triangles.Add(1);
            triangles.Add(3);
            triangles.Add(1);
            triangles.Add(2);

            //获得矩形区域点集合 4是为了去除左侧四个点
            for (int i = 0; i < _squareCount; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    //取余对内层循环的最后一次做处理  （i * 8）是为了去除前面i个矩形的点
                    if (j % 4 == 3)
                    {
                        outList.Add(allSquareList[i * 8 + j]);
                        outList.Add(allSquareList[i * 8 + (j - 3)]);
                        outList.Add(allSquareList[i * 8 + (j + 1)]);
                        outList.Add(allSquareList[i * 8 + (j + 4)]);
                    }
                    else
                    {
                        //以下四个点是组成了矩形的一个面
                        outList.Add(allSquareList[i * 8 + j]);
                        outList.Add(allSquareList[i * 8 + j + 1]);
                        outList.Add(allSquareList[i * 8 + j + 5]);
                        outList.Add(allSquareList[i * 8 + j + 4]);
                    }

                    // 第一个4是为了去除左侧面的四个点，16是为了去除一个矩形四个面的16个点，第二个4是为了去除内层循环j个面的点
                    triangles.Add(4 + i * 16 + j * 4);
                    triangles.Add(4 + i * 16 + j * 4 + 2);
                    triangles.Add(4 + i * 16 + j * 4 + 1);

                    triangles.Add(4 + i * 16 + j * 4);
                    triangles.Add(4 + i * 16 + j * 4 + 3);
                    triangles.Add(4 + i * 16 + j * 4 + 2);
                }
            }

            //画最右侧面
            outList.Add(allSquareList[allSquareList.Count - 4]);
            outList.Add(allSquareList[allSquareList.Count - 3]);
            outList.Add(allSquareList[allSquareList.Count - 2]);
            outList.Add(allSquareList[allSquareList.Count - 1]);

            triangles.Add(outList.Count - 1);
            triangles.Add(outList.Count - 3);
            triangles.Add(outList.Count - 4);
            triangles.Add(outList.Count - 1);
            triangles.Add(outList.Count - 2);
            triangles.Add(outList.Count - 3);
            #endregion

            #region 弧形所有

            int squarePointCount = outList.Count;     // 记录用于画矩形的点的数量
            int squareBottomCount = _squareCount * 4; // 记录底面矩形点的数量

            // 添加弧形底面点
            for (int i = squareBottomCount; i < _bottomList.Count; i++)
            {
                outList.Add(_bottomList[i]);
            }
            // 添加弧形顶面点
            for (int i = squareBottomCount; i < _bottomList.Count; i++)
            {
                outList.Add(_bottomList[i] + Vector3.up * _height);
            }
            // 弧形顶面和底面三角形排序
            for (int i = 0; i < _squareCount - 1; i++)
            {
                for (int j = 1; j < 11; j++)
                {
                    triangles.Add(squarePointCount + i * 12);
                    triangles.Add(squarePointCount + i * 12 + j + 1);
                    triangles.Add(squarePointCount + i * 12 + j);
                }
            }
            for (int i = _squareCount - 1; i < (_squareCount - 1) * 2; i++)
            {
                for (int j = 1; j < 11; j++)
                {
                    triangles.Add(squarePointCount + i * 12);
                    triangles.Add(squarePointCount + i * 12 + j);
                    triangles.Add(squarePointCount + i * 12 + j + 1);
                }
            }

            int prePointCount = outList.Count; // 记录当前输出集合的点的数量

            // 添加弧形侧面点集
            for (int i = squareBottomCount; i < _bottomList.Count; i++)
            {
                outList.Add(_bottomList[i]);
                outList.Add(_bottomList[i] + Vector3.up * _height);
            }

            // 弧形侧面三角形排序
            for (int i = 0; i < _squareCount - 1; i++)
            {
                //j从2开始去除两个弧心点,i*24是为了去除每次内层循环后加入的22个点
                for (int j = 2; j < 22; j += 2)
                {
                    triangles.Add(prePointCount + i * 24 + j);
                    triangles.Add(prePointCount + i * 24 + j + 3);
                    triangles.Add(prePointCount + i * 24 + j + 1);
                    triangles.Add(prePointCount + i * 24 + j);
                    triangles.Add(prePointCount + i * 24 + j + 2);
                    triangles.Add(prePointCount + i * 24 + j + 3);
                }
            }
            #endregion

            meshFilter.mesh.vertices = outList.ToArray();     //给网格顶点集合赋值
            meshFilter.mesh.triangles = triangles.ToArray();  //给网格三角形排序赋值
            meshFilter.mesh.RecalculateBounds();              // 重置范围
            meshFilter.mesh.RecalculateNormals();             // 重置法线
            meshFilter.mesh.RecalculateTangents();            // 重置切线

        }

        /// <summary>
        /// 半球防空区域
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="radius"></param>
        /// <param name="angle">默认为半球，通过修改角度可以修改球的缺省</param>
        /// <param name="meshFilter"></param>
        public static void DrawHemisphere(Vector3 pos, float radius, MeshFilter meshFilter, int angle = 90)
        {
            meshFilter.mesh.vertices = PhysicsMath.GetVertices(pos, radius, angle);    // 圆球形
            meshFilter.mesh.triangles = PhysicsMath.Sort3(angle);

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线
        }

        /// <summary>
        /// 线条
        /// </summary>
        /// <param name="positions">位置点</param>
        /// <param name="wide">宽度</param>
        public static void DrawLine(Vector3[] positions, float wide, LineRenderer lineRenderer, Color Fillcolor)
        {
            // 设置属性
            lineRenderer.positionCount = positions.Length;
            lineRenderer.SetPositions(positions);
            lineRenderer.startWidth = wide;
            lineRenderer.endWidth = wide;
            lineRenderer.startColor = Fillcolor;
            lineRenderer.endColor = Fillcolor;
        }

        #region 暂时弃用的方法

        /// <summary>
        /// 凸多边型空域
        /// </summary>
        /// <param name="positions">所有底面顶点集合</param>
        /// <param name="height">按照高度从底面点向上的延伸顶面点</param>
        /// <param name="meshFilter">需要画三角形的mesh</param>
        public static void DrawWithPoints(Vector3[] positions, float height, MeshFilter meshFilter, LineRenderer[] lineRenderers)
        {
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < positions.Length; i++)
            {
                vertices.Add(positions[i]);
                vertices.Add(positions[i] + new Vector3(0, height, 0));
            }
            meshFilter.mesh.vertices = vertices.ToArray();
            /*xiewanta*/
            List<int> trangles = new List<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == positions.Length - 1)
                {
                    trangles.Add(i * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add(0);

                    trangles.Add(0);
                    trangles.Add(i * 2 + 1);
                    trangles.Add(1);
                }
                else
                {
                    //构成一个侧面的两个三角形
                    trangles.Add(i * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add((i + 1) * 2);

                    trangles.Add((i + 1) * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add((i + 1) * 2 + 1);

                    if (i < positions.Length - 2)
                    {
                        //顶面
                        trangles.Add(1);
                        trangles.Add((i + 2) * 2 + 1);
                        trangles.Add((i + 1) * 2 + 1);
                        //底面
                        trangles.Add(0);
                        trangles.Add((i + 1) * 2);
                        trangles.Add((i + 2) * 2);
                    }
                }
            }

            meshFilter.mesh.triangles = trangles.ToArray();

            //用LineRender增强立体感
            foreach (var item in lineRenderers)
            {
                item.startWidth = 0.5f;
                item.endWidth = 0.5f;
                item.positionCount = positions.Length + 1;
            }
            //顶面底面线条
            for (int i = 0; i < positions.Length; i++)
            {
                lineRenderers[0].SetPosition(i, positions[i]);
                lineRenderers[1].SetPosition(i, positions[i] + new Vector3(0, height, 0));
            }
            lineRenderers[0].SetPosition(positions.Length, positions[0]);
            lineRenderers[1].SetPosition(positions.Length, positions[0] + new Vector3(0, height, 0));

            lineRenderers[2].positionCount = positions.Length * 3;

            //侧面线条
            for (int i = 0, j = 0; i < positions.Length; i++)
            {
                lineRenderers[2].SetPosition(j++, vertices[i * 2]);
                lineRenderers[2].SetPosition(j++, vertices[i * 2 + 1]);
                lineRenderers[2].SetPosition(j++, vertices[i * 2]);
            }

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线
        }

        /// <summary>
        /// 多边型空域(每个点对应的空域高度不同时)
        /// </summary>
        /// <param name="positionDatas">所有底面顶点集合</param>
        /// <param name="meshFilter">需要画三角形的mesh</param>
        public static void DrawWithPoints(Vector3Extend[] positionDatas, MeshFilter meshFilter)
        {
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < positionDatas.Length; i++)
            {
                vertices.Add(positionDatas[i].position);
                vertices.Add(positionDatas[i].position + new Vector3(0, positionDatas[i].height, 0));
            }
            meshFilter.mesh.vertices = vertices.ToArray();

            List<int> trangles = new List<int>();
            for (int i = 0; i < positionDatas.Length; i++)
            {
                if (i == positionDatas.Length - 1)
                {
                    trangles.Add(1);
                    trangles.Add(i * 2);
                    trangles.Add(i * 2 + 1);

                    trangles.Add(0);
                    trangles.Add(i * 2);
                    trangles.Add(1);
                }
                else
                {
                    //构成一个侧面的两个三角形
                    trangles.Add(i * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add((i + 1) * 2);

                    trangles.Add((i + 1) * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add((i + 1) * 2 + 1);

                    if (i < positionDatas.Length - 2)
                    {
                        //顶面
                        trangles.Add(1);
                        trangles.Add((i + 2) * 2 + 1);
                        trangles.Add((i + 1) * 2 + 1);
                        //底面
                        trangles.Add(0);
                        trangles.Add((i + 1) * 2);
                        trangles.Add((i + 2) * 2);
                    }
                }
            }
            meshFilter.mesh.triangles = trangles.ToArray();

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线
        }

        /// <summary>
        /// 通过修改一个mesh的子三角形数组材质控制侧面和底面材质不同时(多边形空域)
        /// </summary>
        public static int DrawWithPointsDifferent(Vector3[] positions, float height, MeshFilter meshFilter, MeshRenderer meshRenderer, Material material_1)
        {
            int differentM = 0;
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < positions.Length; i++)
            {
                vertices.Add(positions[i]);
                vertices.Add(positions[i] + new Vector3(0, height, 0));
            }
            meshFilter.mesh.vertices = vertices.ToArray();

            List<int> trangles = new List<int>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == positions.Length - 1)
                {
                    trangles.Add(i * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add(0);

                    trangles.Add(0);
                    trangles.Add(i * 2 + 1);
                    trangles.Add(1);
                }
                else
                {
                    //构成一个侧面的两个三角形
                    trangles.Add(i * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add((i + 1) * 2);

                    trangles.Add((i + 1) * 2);
                    trangles.Add(i * 2 + 1);
                    trangles.Add((i + 1) * 2 + 1);
                }
            }

            for (int i = 0; i < positions.Length - 2; i++)
            {
                //顶面
                trangles.Add(1);
                trangles.Add((i + 2) * 2 + 1);
                trangles.Add((i + 1) * 2 + 1);
                //底面
                trangles.Add(0);
                trangles.Add((i + 1) * 2);
                trangles.Add((i + 2) * 2);
                differentM += 6;
            }
            meshFilter.mesh.triangles = trangles.ToArray();

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线

            return differentM;

            //Mesh mesh = meshFilter.mesh;
            //mesh.subMeshCount = 3;
            //meshRenderer.materials = new Material[3];

            //Debug.Log("材质二 ： " + differentM);
            //Debug.Log("总数" + meshFilter.mesh.triangles.Length);
            ////mesh.SetTriangles(GetRangeArray(mesh.triangles, 0, mesh.triangles.Length - differentM - 1), 0);
            ////Debug.Log(0 + " : " + mesh.triangles.Length - differentM - 1)
            //mesh.SetTriangles(Singleton<PhysicsMath>.GetInstance().GetRangeArray(mesh.triangles, mesh.triangles.Length - differentM, mesh.triangles.Length - differentM + 5), 1);
            //mesh.SetTriangles(Singleton<PhysicsMath>.GetInstance().GetRangeArray(mesh.triangles, mesh.triangles.Length - differentM + 6, mesh.triangles.Length - 1), 2);
            ////mesh.SetTriangles(GetRangeArray(mesh.triangles, mesh.triangles.Length - differentM, mesh.triangles.Length - 1), 1);

            //Material[] materials = new Material[3];
            //materials[0] = material_1;
            //materials[1] = material_1;
            //materials[2] = material_1;
            //meshRenderer.materials = materials;
            //meshRenderer.materials[0].color = Color.red;
            //meshRenderer.materials[1].color = Color.blue;
            //meshRenderer.materials[2].color = Color.green;
        }

        /// <summary>
        /// 凹多边形空域 每个点高度不一样时 可以用来画扇形区域
        /// </summary>
        /// <param name="positions"></param>
        /// <param name="meshFilter"></param>
        /// <param name="lineRenderers"></param>
        public static void DrawPolygon(Vector3Extend[] positions, MeshFilter meshFilter)
        {
            List<Vector3> verticesDown = new List<Vector3>();
            List<Vector3> verticesUp = new List<Vector3>();
            List<Vector3> vertices = new List<Vector3>();

            List<int> triangles = new List<int>();
            //上下面坐标赋值 数组转List
            for (int i = 0; i < positions.Length; i++)
            {
                verticesDown.Add(positions[i].position);
            }
            for (int i = 0; i < positions.Length; i++)
            {
                verticesUp.Add(positions[i].position + new Vector3(0, positions[i].height, 0));
            }
            //计算一个平面的点数量
            int count = verticesDown.Count;

            //将上下平面点合并
            for (int i = 0; i < verticesDown.Count; i++)
            {
                vertices.Add(verticesDown[i]);
            }
            for (int i = 0; i < verticesUp.Count; i++)
            {
                vertices.Add(verticesUp[i]);
            }

            //获取底面和顶面的三角形排序
            List<int> trianglesDown = PhysicsMath.DrawPolygon(verticesDown, false);
            List<int> trianglesUp = PhysicsMath.DrawPolygon(verticesUp, true);

            for (int i = 0; i < trianglesDown.Count; i++)
            {
                trianglesUp[i] += count;
            }

            //合并底面顶面三角形排序
            for (int i = 0; i < trianglesDown.Count; i++)
            {
                triangles.Add(trianglesDown[i]);
            }
            for (int i = 0; i < trianglesUp.Count; i++)
            {
                triangles.Add(trianglesUp[i]);
            }

            //侧面三角形排序
            for (int i = 0; i < count - 1; i++)
            {
                // 加上侧面的点集
                vertices.Add(vertices[i]);
                vertices.Add(vertices[i + 1]);
                vertices.Add(vertices[i + count + 1]);
                vertices.Add(vertices[i + count]);

                // 侧面三角形排序
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 0 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 1 + i * 4);
                triangles.Add(2 * count + 3 + i * 4);
                triangles.Add(2 * count + 2 + i * 4);
            }

            // 加上最后一个侧面的点集
            vertices.Add(vertices[count - 1]);
            vertices.Add(vertices[0]);
            vertices.Add(vertices[count]);
            vertices.Add(vertices[2 * count - 1]);

            // 加上最后一个侧面的三角形排序
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 4);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 3);
            triangles.Add(vertices.Count - 1);
            triangles.Add(vertices.Count - 2);

            meshFilter.mesh.vertices = vertices.ToArray();
            meshFilter.mesh.triangles = triangles.ToArray();

            meshFilter.mesh.RecalculateBounds();     // 重置范围
            meshFilter.mesh.RecalculateNormals();    // 重置法线
            meshFilter.mesh.RecalculateTangents();    // 重置切线
        }

        #endregion

    }

    /// <summary>
    /// 带二重高度Vector3
    /// </summary>
    public struct Vector3Extend
    {
        public Vector3 position;
        public float height;
    }
}



