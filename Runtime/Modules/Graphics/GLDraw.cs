using System.Collections.Generic;
using System.Linq;
using XFramework.Mathematics;
using UnityEngine;

namespace XFramework.Draw
{
    /// <summary>
    /// 用于构造几何体Mesh
    /// </summary>
    public static class GLDraw
    {
        /// <summary>
        /// 创建一个轴的箭头网格
        /// </summary>
        public static Mesh CreateArrow(Color color, float scale)
        {
            int segmentsCount = 12;  // 侧面三角形数量
            float size = 1.0f / 5;
            size *= scale;

            Vector3[] vertices = new Vector3[segmentsCount + 2];
            int[] triangles = new int[segmentsCount * 6];
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; ++i)
            {
                // 顶点颜色
                colors[i] = color;
            }

            float radius = size / 2.6f; // 地面半径
            float height = size;        // 高
            float deltaAngle = Mathf.PI * 2.0f / segmentsCount;

            float y = -height;

            vertices[vertices.Length - 1] = new Vector3(0, -height, 0); // 圆心点
            vertices[vertices.Length - 2] = Vector3.zero;               // 锥顶

            // 底面圆上的点
            for (int i = 0; i < segmentsCount; i++)
            {
                float angle = i * deltaAngle;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                vertices[i] = new Vector3(x, y, z);
            }

            for (int i = 0; i < segmentsCount; i++)
            {
                // 底面三角形排序
                triangles[i * 6] = vertices.Length - 1;
                triangles[i * 6 + 1] = i;
                triangles[i * 6 + 2] = (i + 1) % segmentsCount;

                // 侧面三角形排序
                triangles[i * 6 + 3] = vertices.Length - 2;
                triangles[i * 6 + 4] = i;
                triangles[i * 6 + 5] = (i + 1) % segmentsCount;
            }

            Mesh cone = new Mesh
            {
                name = "Cone",
                vertices = vertices,
                triangles = triangles,
                colors = colors
            };

            return cone;
        }
        public static Mesh CreateArrow(float scale, int colorInt = ColorInt32.white)
        {
            return CreateArrow(colorInt.Color(), scale);
        }

        /// <summary>
        /// 创建一个方块网格 
        /// </summary>
        public static Mesh CreateCube(Color color, Vector3 center, float scale, float cubeLength = 1, float cubeWidth = 1, float cubeHeight = 1)
        {
            cubeHeight *= scale;
            cubeWidth *= scale;
            cubeLength *= scale;

            Vector3 vertice_0 = center + new Vector3(-cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_1 = center + new Vector3(cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_2 = center + new Vector3(cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
            Vector3 vertice_3 = center + new Vector3(-cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
            Vector3 vertice_4 = center + new Vector3(-cubeLength * .5f, cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_5 = center + new Vector3(cubeLength * .5f, cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_6 = center + new Vector3(cubeLength * .5f, cubeWidth * .5f, -cubeHeight * .5f);
            Vector3 vertice_7 = center + new Vector3(-cubeLength * .5f, cubeWidth * .5f, -cubeHeight * .5f);
            Vector3[] vertices = new[]
            {
                // Bottom Polygon
                vertice_0, vertice_1, vertice_2, vertice_3,
                // Left Polygon
                vertice_7, vertice_4, vertice_0, vertice_3,
                // Front Polygon
                vertice_4, vertice_5, vertice_1, vertice_0,
                // Back Polygon
                vertice_6, vertice_7, vertice_3, vertice_2,
                // Right Polygon
                vertice_5, vertice_6, vertice_2, vertice_1,
                // Top Polygon
                vertice_7, vertice_6, vertice_5, vertice_4
            };

            int[] triangles = new[]
            {
                // Cube Bottom Side Triangles
                3, 1, 0,
                3, 2, 1,    
                // Cube Left Side Triangles
                3 + 4 * 1, 1 + 4 * 1, 0 + 4 * 1,
                3 + 4 * 1, 2 + 4 * 1, 1 + 4 * 1,
                // Cube Front Side Triangles
                3 + 4 * 2, 1 + 4 * 2, 0 + 4 * 2,
                3 + 4 * 2, 2 + 4 * 2, 1 + 4 * 2,
                // Cube Back Side Triangles
                3 + 4 * 3, 1 + 4 * 3, 0 + 4 * 3,
                3 + 4 * 3, 2 + 4 * 3, 1 + 4 * 3,
                // Cube Rigth Side Triangles
                3 + 4 * 4, 1 + 4 * 4, 0 + 4 * 4,
                3 + 4 * 4, 2 + 4 * 4, 1 + 4 * 4,
                // Cube Top Side Triangles
                3 + 4 * 5, 1 + 4 * 5, 0 + 4 * 5,
                3 + 4 * 5, 2 + 4 * 5, 1 + 4 * 5,
            };

            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh cubeMesh = new Mesh();
            cubeMesh.name = "cube";
            cubeMesh.vertices = vertices;
            cubeMesh.triangles = triangles;
            cubeMesh.colors = colors;
            cubeMesh.RecalculateNormals();
            return cubeMesh;
        }
        public static Mesh CreateCube(Vector3 center, float scale, float cubeLength = 1, float cubeWidth = 1, float cubeHeight = 1, int colorInt = ColorInt32.white)
        {
            return CreateCube(colorInt.Color(), center, scale, cubeLength, cubeWidth, cubeHeight);
        }

        /// <summary>
        /// 返回一个球的网格
        /// </summary>
        /// <param name="subdivisions"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static Mesh CreateSphere(int subdivisions = 0, float radius = 1, int colorInt = ColorInt32.white)
        {
            Vector3[] directions = {
                Vector3.left,
                Vector3.back,
                Vector3.right,
                Vector3.forward
            };

            if (subdivisions < 0)
            {
                subdivisions = 0;
                Debug.LogWarning("Octahedron Sphere subdivisions increased to minimum, which is 0.");
            }
            else if (subdivisions > 6)
            {
                subdivisions = 6;
                Debug.LogWarning("Octahedron Sphere subdivisions decreased to maximum, which is 6.");
            }

            int resolution = 1 << subdivisions;
            Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1) * 4 - (resolution * 2 - 1) * 3];
            int[] triangles = new int[(1 << (subdivisions * 2 + 3)) * 3];
            CreateOctahedron(vertices, triangles, resolution);

            Vector3[] normals = new Vector3[vertices.Length];
            Normalize(vertices, normals);

            Vector2[] uv = new Vector2[vertices.Length];
            CreateUV(vertices, uv);

            Vector4[] tangents = new Vector4[vertices.Length];
            CreateTangents(vertices, tangents);

            if (radius != 1f)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] *= radius;
                }
            }

            Color[] colors = new Color[vertices.Length];
            Color color = colorInt.Color();
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh mesh = new Mesh
            {
                name = "Octahedron Sphere",
                vertices = vertices,
                normals = normals,
                uv = uv,
                tangents = tangents,
                triangles = triangles,
                colors = colors,
            };
            return mesh;

            #region 内部函数

            void CreateOctahedron(Vector3[] _vertices, int[] _triangles, int _resolution)
            {
                int v = 0, vBottom = 0, t = 0;

                for (int i = 0; i < 4; i++)
                {
                    _vertices[v++] = Vector3.down;
                }

                for (int i = 1; i <= _resolution; i++)
                {
                    float progress = (float)i / _resolution;
                    Vector3 from, to;
                    _vertices[v++] = to = Vector3.Lerp(Vector3.down, Vector3.forward, progress);
                    for (int d = 0; d < 4; d++)
                    {
                        from = to;
                        to = Vector3.Lerp(Vector3.down, directions[d], progress);
                        t = CreateLowerStrip(i, v, vBottom, t, _triangles);
                        v = CreateVertexLine(from, to, i, v, _vertices);
                        vBottom += i > 1 ? (i - 1) : 1;
                    }
                    vBottom = v - 1 - i * 4;
                }

                for (int i = _resolution - 1; i >= 1; i--)
                {
                    float progress = (float)i / _resolution;
                    Vector3 from, to;
                    _vertices[v++] = to = Vector3.Lerp(Vector3.up, Vector3.forward, progress);
                    for (int d = 0; d < 4; d++)
                    {
                        from = to;
                        to = Vector3.Lerp(Vector3.up, directions[d], progress);
                        t = CreateUpperStrip(i, v, vBottom, t, _triangles);
                        v = CreateVertexLine(from, to, i, v, _vertices);
                        vBottom += i + 1;
                    }
                    vBottom = v - 1 - i * 4;
                }

                for (int i = 0; i < 4; i++)
                {
                    _triangles[t++] = vBottom;
                    _triangles[t++] = v;
                    _triangles[t++] = ++vBottom;
                    _vertices[v++] = Vector3.up;
                }
            }

            int CreateVertexLine(Vector3 from, Vector3 to, int steps, int v, Vector3[] _vertices)
            {
                for (int i = 1; i <= steps; i++)
                {
                    _vertices[v++] = Vector3.Lerp(from, to, (float)i / steps);
                }
                return v;
            }

            int CreateLowerStrip(int steps, int vTop, int vBottom, int t, int[] _triangles)
            {
                for (int i = 1; i < steps; i++)
                {
                    _triangles[t++] = vBottom;
                    _triangles[t++] = vTop - 1;
                    _triangles[t++] = vTop;

                    _triangles[t++] = vBottom++;
                    _triangles[t++] = vTop++;
                    _triangles[t++] = vBottom;
                }
                _triangles[t++] = vBottom;
                _triangles[t++] = vTop - 1;
                _triangles[t++] = vTop;
                return t;
            }

            int CreateUpperStrip(int steps, int vTop, int vBottom, int t, int[] _triangles)
            {
                _triangles[t++] = vBottom;
                _triangles[t++] = vTop - 1;
                _triangles[t++] = ++vBottom;
                for (int i = 1; i <= steps; i++)
                {
                    _triangles[t++] = vTop - 1;
                    _triangles[t++] = vTop;
                    _triangles[t++] = vBottom;

                    _triangles[t++] = vBottom;
                    _triangles[t++] = vTop++;
                    _triangles[t++] = ++vBottom;
                }
                return t;
            }

            void Normalize(Vector3[] _vertices, Vector3[] _normals)
            {
                for (int i = 0; i < _vertices.Length; i++)
                {
                    _normals[i] = _vertices[i] = _vertices[i].normalized;
                }
            }

            void CreateUV(Vector3[] _vertices, Vector2[] _uv)
            {
                float previousX = 1f;
                for (int i = 0; i < _vertices.Length; i++)
                {
                    Vector3 v = _vertices[i];
                    if (v.x == previousX)
                    {
                        _uv[i - 1].x = 1f;
                    }
                    previousX = v.x;
                    Vector2 textureCoordinates;
                    textureCoordinates.x = Mathf.Atan2(v.x, v.z) / (-2f * Mathf.PI);
                    if (textureCoordinates.x < 0f)
                    {
                        textureCoordinates.x += 1f;
                    }
                    textureCoordinates.y = Mathf.Asin(v.y) / Mathf.PI + 0.5f;
                    _uv[i] = textureCoordinates;
                }
                _uv[_vertices.Length - 4].x = _uv[0].x = 0.125f;
                _uv[_vertices.Length - 3].x = _uv[1].x = 0.375f;
                _uv[_vertices.Length - 2].x = _uv[2].x = 0.625f;
                _uv[_vertices.Length - 1].x = _uv[3].x = 0.875f;
            }

            void CreateTangents(Vector3[] _vertices, Vector4[] _tangents)
            {
                for (int i = 0; i < _vertices.Length; i++)
                {
                    Vector3 v = _vertices[i];
                    v.y = 0f;
                    v = v.normalized;
                    Vector4 tangent;
                    tangent.x = -v.z;
                    tangent.y = 0f;
                    tangent.z = v.x;
                    tangent.w = -1f;
                    _tangents[i] = tangent;
                }

                _tangents[_vertices.Length - 4] = _tangents[0] = new Vector3(-1f, 0, -1f).normalized;
                _tangents[_vertices.Length - 3] = _tangents[1] = new Vector3(1f, 0f, -1f).normalized;
                _tangents[_vertices.Length - 2] = _tangents[2] = new Vector3(1f, 0f, 1f).normalized;
                _tangents[_vertices.Length - 1] = _tangents[3] = new Vector3(-1f, 0f, 1f).normalized;
                for (int i = 0; i < 4; i++)
                {
                    _tangents[_vertices.Length - 1 - i].w = _tangents[i].w = -1f;
                }
            }

            #endregion
        }

        /// <summary>
        /// 圆柱
        /// </summary>
        public static Mesh CreateCylinder(Vector3 buttomOrigin, float radius, float height, int colorInt = ColorInt32.white)
        {
            Vector3[] buttomSurface = PhysicsMath.GetCirclePoints(buttomOrigin, radius);

            List<Vector3> verticesDown = new List<Vector3>();
            List<Vector3> verticesUp = new List<Vector3>();
            List<Vector3> vertices = new List<Vector3>();

            List<int> triangles = new List<int>();
            //上下面坐标赋值 数组转List
            for (int i = 0; i < buttomSurface.Length; i++)
            {
                verticesDown.Add(buttomSurface[i]);
            }
            for (int i = 0; i < buttomSurface.Length; i++)
            {
                verticesUp.Add(buttomSurface[i] + new Vector3(0, height, 0));
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
            List<int> trianglesDown = GetPolygonSort(verticesDown, false);
            List<int> trianglesUp = GetPolygonSort(verticesUp, true);

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

            Color[] colors = new Color[vertices.Count];
            Color color = colorInt.Color();
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh mesh = new Mesh
            {
                name = "Cylinder",
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray(),
                colors = colors,
            };

            mesh.RecalculateBounds();     // 重置范围
            mesh.RecalculateNormals();    // 重置法线
            mesh.RecalculateTangents();    // 重置切线

            return mesh;
        }

        /// <summary>
        /// 凹/凸多边形空域
        /// </summary>
        /// <param name="pointsDown">下表面点集</param>
        /// <param name="height">空域高度</param>
        /// <param name="colorInt">颜色</param>
        public static Mesh CreatePolygon(Vector3[] pointsDown, float height, int colorInt = ColorInt32.white, bool isClockwise = true)
        {
            Vector3[] pointsUp = new Vector3[pointsDown.Length];

            for (int i = 0; i < pointsDown.Length; i++)
            {
                pointsUp[i] = pointsDown[i] + new Vector3(0, height, 0);
            }

            return CreatePolygon(pointsDown.Concat(pointsUp).ToArray(), colorInt, isClockwise);
        }

        /// <summary>
        /// 凹/凸多边形空域 每个点高度不一样时 可以用来画扇形区域
        /// </summary>
        /// <param name="positions">下表面点集 + 上表面点集</param>
        /// <param name="colorInt">颜色</param>
        /// <returns></returns>
        public static Mesh CreatePolygon(Vector3[] positions, int colorInt = ColorInt32.white, bool isClockwise = true)
        {
            if (positions.Length % 2 != 0)
            {
                throw new System.Exception("[CreatePolyon] 传入点集错误，应为下底面加上底面的集合");
            }

            int count = positions.Length / 2;                   // 一个平面的点数量

            List<Vector3> vertices = new List<Vector3>(positions);
            List<int> triangles = new List<int>();

            //获取底面和顶面的三角形排序
            List<int> trianglesDown = GetPolygonSort(positions.Take(count).ToList(), !(false ^ isClockwise));
            List<int> trianglesUp = GetPolygonSort(positions.Take(count - 1, count).ToList(), true & isClockwise);

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
                triangles.AddTriangles(2 * count + 0 + i * 4, 2 * count + 1 + i * 4, 2 * count + 3 + i * 4, !isClockwise);
                triangles.AddTriangles(2 * count + 1 + i * 4, 2 * count + 2 + i * 4, 2 * count + 3 + i * 4, !isClockwise);
            }

            // 加上最后一个侧面的点集
            vertices.Add(vertices[count - 1]);
            vertices.Add(vertices[0]);
            vertices.Add(vertices[count]);
            vertices.Add(vertices[2 * count - 1]);

            // 加上最后一个侧面的三角形排序
            triangles.AddTriangles(vertices.Count - 4, vertices.Count - 3, vertices.Count - 1, !isClockwise);
            triangles.AddTriangles(vertices.Count - 3, vertices.Count - 2, vertices.Count - 1, !isClockwise);

            Color[] colors = new Color[vertices.Count];
            Color color = colorInt.Color();
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh mesh = new Mesh
            {
                name = "Polygon",
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray(),
                colors = colors,
            };

            mesh.RecalculateBounds();     // 重置范围
            mesh.RecalculateNormals();    // 重置法线
            mesh.RecalculateTangents();    // 重置切线

            return mesh;
        }

        /// <summary>
        /// 获取凹多边形平面排序
        /// </summary>
        /// <param name="points"> 关键点集合 </param>
        /// <returns></returns>
        public static List<int> GetPolygonSort(List<Vector3> points, bool isUp)
        {
            List<int> indexs = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                indexs.Add(i);
            }

            List<int> triangles = new List<int>();

            //创建一个除去自身前一点的 多边形，判断前一点是否为内点（凹点）
            int index = points.Count - 1;
            int next;
            int prev;

            int maxCount = indexs.Count + 1;
            int count = 0;
            // 切分到无法切割三角形为止
            while (indexs.Count > 2)
            {
                if (maxCount > indexs.Count) // 正确
                {
                    maxCount = indexs.Count;
                    count = 0;
                }
                else
                {
                    if (count > maxCount)
                    {
                        throw new System.Exception("当前有" + (count - 1).ToString() + "个数据死在循环中");
                    }
                    count++;
                }

                // 判断要判断是否切割的三角形的三个点是否在同一位置
                next = (index == indexs.Count - 1) ? 0 : index + 1;
                prev = (index == 0) ? indexs.Count - 1 : index - 1;
                if (points[index] == points[prev] && points[index] == points[next])
                {
                    throw new System.Exception("[DrawPolygon data error]三个点的个位置两两相等");
                }

                List<Vector3> polygon = new List<Vector3>(points.ToArray());
                polygon.RemoveAt(index);

                //是否是凹点
                if (!PhysicsMath.IsPointInsidePolygon(points[index], polygon))
                {
                    // 是否是可划分顶点:新的多边形没有顶点在被分割的三角形内
                    if (PhysicsMath.IsFragementIndex(points, index))
                    {
                        //可划分，剖分三角形
                        next = (index == indexs.Count - 1) ? 0 : index + 1;
                        prev = (index == 0) ? indexs.Count - 1 : index - 1;
                        if (isUp)
                        {
                            triangles.Add(indexs[next]);
                            triangles.Add(indexs[prev]);
                            triangles.Add(indexs[index]);
                        }
                        else
                        {
                            triangles.Add(indexs[index]);
                            triangles.Add(indexs[prev]);
                            triangles.Add(indexs[next]);
                        }

                        indexs.RemoveAt(index);
                        points.RemoveAt(index);

                        index = (index + indexs.Count - 1) % indexs.Count;       // 防止出现index超出值域,类似于i--

                        continue;
                    }
                }
                index = (index + 1) % indexs.Count;
            }

            return triangles;
        }

        /// <summary>
        /// 立体线，可用于画空中走廊
        /// </summary>
        /// <param name="_bottomList">下底面排序</param>
        /// <param name="_width">宽度</param>
        /// <param name="_height">高度</param>
        /// <returns></returns>
        public static Mesh CreateLineMesh(List<Vector3> list, float _width, float _height, int colorInt = ColorInt32.white)
        {
            Vector3[] bottomPoints = PhysicsMath.GetAirSpaceBottomPoints(list, _width, _height);
            Vector3[] vertices = PhysicsMath.GetAirBottomSpaceWithSector(bottomPoints.ToList(), _width);

            // 计算Mesh
            int _squareCount = list.Count - 1;
            var _bottomList = vertices.ToList();

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

            Color[] colors = new Color[outList.Count];
            Color color = colorInt.Color();
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh mesh = new Mesh
            {
                name = "LineMesh",
                vertices = outList.ToArray(),
                triangles = triangles.ToArray(),
                colors = colors,
            };
            mesh.RecalculateBounds();              // 重置范围
            mesh.RecalculateNormals();             // 重置法线
            mesh.RecalculateTangents();            // 重置切线

            return mesh;
        }

        /// <summary>
        /// 半球
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="radius"></param>
        /// <param name="angle">默认为半球，通过修改角度可以修改球的缺省</param>
        public static Mesh CreateHemisphere(Vector3 pos, float radius, int angle = 90, int colorInt = ColorInt32.white)
        {
            var vertices = PhysicsMath.GetVertices(pos, radius, angle);    // 圆球形
            var triangles = PhysicsMath.Sort3(angle);

            Color[] colors = new Color[vertices.Length];
            Color color = colorInt.Color();
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh mesh = new Mesh
            {
                name = "HemiSphere",
                vertices = vertices,
                triangles = triangles,
                colors = colors,
            };

            mesh.RecalculateBounds();     // 重置范围
            mesh.RecalculateNormals();    // 重置法线
            mesh.RecalculateTangents();    // 重置切线

            return mesh;
        }

        /// <summary>
        /// 凸多边形
        /// </summary>
        /// <param name="positions">所有底面顶点集合</param>
        /// <param name="height">按照高度从底面点向上的延伸顶面点</param>
        public static Mesh CreatConvexPolgonao(Vector3[] positions, float height, int colorInt = ColorInt32.white)
        {
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < positions.Length; i++)
            {
                vertices.Add(positions[i]);
                vertices.Add(positions[i] + new Vector3(0, height, 0));
            }
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

            Color[] colors = new Color[vertices.Count];
            Color color = colorInt.Color();
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh mesh = new Mesh
            {
                name = "Polygon",
                vertices = vertices.ToArray(),
                triangles = trangles.ToArray(),
                colors = colors,
            };

            mesh.RecalculateBounds();     // 重置范围
            mesh.RecalculateNormals();    // 重置法线
            mesh.RecalculateTangents();    // 重置切线

            return mesh;
        }

        private static void AddTriangles(this List<int> triangles, int a, int b, int c, bool isReverse = false)
        {
            if (!isReverse)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(a);
            }
        }
    }
}