// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-10-18 15:30:33
// 版本： V 1.0
// ==========================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.Mathematics
{
    /// <summary>
    /// 所有物理数学相关计算方法
    /// </summary>
    public class PhysicsMath
    {
        #region 公式类计算

        /// <summary>
        /// 检查所给点集是否为顺时针排序(向量叉乘的法线朝向)
        /// </summary>
        /// <returns></returns>
        public static bool CheckVector(List<Vector3> points)
        {
            // 创建一个除去自身的 多边形，判断是否为内点（凹点）
            List<Vector3> polygon = new List<Vector3>(points);
            polygon.RemoveAt(0);

            Vector3 vector3_1 = points[0] - points[1];
            Vector3 vector3_2 = points[points.Count - 1] - points[0];

            Vector3 nom = Vector3.Cross(vector3_1, vector3_2);  // 算出法线方向。Unity为左手坐标系, 使用左手定则

            //是否是凹点
            if (IsPointInsidePolygon(points[0], polygon))
            {
                // 法线方向朝下。即逆时针排序，需要反转
                if (nom.y < 0)
                {
                    return false;
                }
            }
            else   // 凸点
            {
                // 法线方向朝上。即逆时针排序，需要反转
                if (nom.y > 0)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region 点线面之间的关系

        /// <summary>
        /// 获取两点组成的矩形边框
        /// </summary>
        /// <param name="_start">起始点</param>
        /// <param name="_end">终止点</param>
        /// <param name="_width">宽度</param>
        /// <returns></returns>
        public static Vector3[] GetRect(Vector3 _start, Vector3 _end, float _width)
        {
            Vector3[] rect = new Vector3[4];
            Vector3 dir = Math2d.GetHorizontalDir(_start, _end);
            rect[0] = _start + dir * _width;
            rect[1] = _start - dir * _width;
            rect[2] = _end + dir * _width;
            rect[3] = _end - dir * _width;

            return rect;
        }
        public static Vector2[] GetRect(Vector2 _start, Vector2 _end, float _width)
        {
            Vector2[] rect = new Vector2[4];
            Vector2 dir = Math2d.GetHorizontalDir(_end - _start);
            rect[0] = _start + dir * _width;
            rect[1] = _start - dir * _width;
            rect[2] = _end + dir * _width;
            rect[3] = _end - dir * _width;

            return rect;
        }

        #endregion

        #region 曲线相关

        /// <summary>
        /// 获取贝塞尔曲线(3个点为二次,4个点为三次,其他返回空)
        /// </summary>
        /// <param name="_points">控制点集</param>
        /// <param name="_count">曲线段数</param>
        /// <returns></returns>
        public static List<Vector3> GetBezierList(Vector3[] _points, int _count = 10)
        {
            List<Vector3> outList = new List<Vector3>();
            if (_points.Length == 3)
            {
                for (float i = 0; i <= _count; i++)
                {
                    outList.Add(Math3d.GetBezierPoint(i / _count, _points[0], _points[1], _points[2]));
                }
            }
            if (_points.Length == 4)
            {
                for (float i = 0; i <= _count; i++)
                {
                    outList.Add(Math3d.GetBezierPoint(i / _count, _points[0], _points[1], _points[2], _points[3]));
                }
            }
            return outList;
        }

        /// <summary>         
        /// 获取曲线上面的所有路径点
        /// </summary> 
        /// <returns>The list.</returns> 
        /// <param name="wayPoints">路点列表</param> 
        /// <param name="pointSize">两个点之间的节点数量</param> 
        public static List<Vector3> GetHermitPoints(List<Vector3> wayPoints)
        {
            Vector3[] controlPointList = HermitPathControlPoint(wayPoints.ToArray());
            int smoothAmount = 0;       // wayPoints.Length * pointSize;     

            // 根据 路点 间的距离计算所需 路径点 的数量
            for (int i = 0, length = wayPoints.Count - 1; i < length; i++)
            {
                smoothAmount += (int)Vector3.Distance(wayPoints[i + 1], wayPoints[i]) / 4;
            }

            Vector3[] pointArr = new Vector3[smoothAmount];
            for (int index = 1; index <= smoothAmount; index++)
            {
                pointArr[index - 1] = HermitInterp(controlPointList, (float)index / smoothAmount);
            }
            return pointArr.ToList();
        }

        /// <summary>
        /// 获取 Hermit 曲线上的某点
        /// </summary>
        /// <param name="wayPoints"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static Vector3 GetHermitPoint(List<Vector3> wayPoints, float t)
        {
            Vector3[] controlPointList = HermitPathControlPoint(wayPoints.ToArray());
            return HermitInterp(controlPointList, t);
        }
        /// <summary> 
        /// 获取控制点 
        /// </summary> 
        /// <returns>The control point generator.</returns> 
        /// <param name="path">Path.</param> 
        public static Vector3[] HermitPathControlPoint(Vector3[] path)
        {
            int offset = 2;
            Vector3[] suppliedPath = path;
            Vector3[] controlPoint = new Vector3[suppliedPath.Length + offset];

            Array.Copy(suppliedPath, 0, controlPoint, 1, suppliedPath.Length);

            controlPoint[0] = controlPoint[1] + (controlPoint[1] - controlPoint[2]);
            controlPoint[controlPoint.Length - 1] = controlPoint[controlPoint.Length - 2] +
                (controlPoint[controlPoint.Length - 2] - controlPoint[controlPoint.Length - 3]);

            if (controlPoint[1] == controlPoint[controlPoint.Length - 2])
            {
                Vector3[] tmpLoopSpline = new Vector3[controlPoint.Length];

                Array.Copy(controlPoint, tmpLoopSpline, controlPoint.Length);

                tmpLoopSpline[0] = tmpLoopSpline[tmpLoopSpline.Length - 3];

                tmpLoopSpline[tmpLoopSpline.Length - 1] = tmpLoopSpline[2];

                controlPoint = new Vector3[tmpLoopSpline.Length];

                Array.Copy(tmpLoopSpline, controlPoint, tmpLoopSpline.Length);
            }
            return (controlPoint);
        }
        /// <summary> 
        /// 根据 T 获取曲线上面的点位置 ，
        /// 插值函数，
        /// Hermit曲线方程
        /// </summary> 
        /// <param name="pts">控制点点集</param> 
        /// <param name="t">分割进度</param> 
        public static Vector3 HermitInterp(Vector3[] pts, float t)
        {
            int numSections = pts.Length - 3;       // 控制点总数减3
            int currPt = Mathf.Min(Mathf.FloorToInt(t * (float)numSections), numSections - 1);

            float u = t * (float)numSections - (float)currPt;

            Vector3 a = pts[currPt];
            Vector3 b = pts[currPt + 1];
            Vector3 c = pts[currPt + 2];
            Vector3 d = pts[currPt + 3];

            //Debug.DrawLine(b, b + (-a + c).normalized * 10, Color.red,50);

            return .5f * ((-a + 3f * b - 3f * c + d) * (u * u * u) +
                (2f * a - 5f * b + 4f * c - d) * (u * u) + (-a + c) * u + 2f * b);
        }

        #endregion

        #region 半球形与圆形

        ///// <summary>
        ///// 获取扇形区域的点集合（包括圆心点）
        ///// </summary>
        ///// <param name="origin">圆心点</param>
        ///// <param name="egdePoint">扇形弧边右边缘点</param>
        ///// <param name="angleDiffer">x，z轴张角</param>
        ///// <returns></returns>
        //public static Vector3[] GetSectorPoints_1(Vector3 origin, Vector3 egdePoint, float angleDiffer)
        //{
        //    float angle;
        //    Vector3 dir = egdePoint - origin;                               //取两点的向量
        //    float radius = dir.magnitude;                                   //获取扇形的半径

        //    //取数组长度 如60度的弧边取61个点 0~60 再加上一个圆心点
        //    Vector3[] points = new Vector3[(int)(angleDiffer / 3) + 2];
        //    points[0] = origin;                                             //取圆心点
        //    int startEuler = (int)Vector2.Angle(Vector2.right, new Vector2(dir.x, dir.z));
        //    for (int i = startEuler, j = 1; i <= angleDiffer + startEuler; j++, i += 3)
        //    {
        //        angle = Mathf.Deg2Rad * i;
        //        float differ = Mathf.Abs(Mathf.Cos(angle - (float)(0.5 * angleDiffer * Mathf.Deg2Rad)) * egdePoint.y - egdePoint.y);//高度差的绝对值
        //        points[j] = origin + new Vector3(radius * Mathf.Cos(angle), egdePoint.y + differ, radius * Mathf.Sin(angle));       //给底面点赋值
        //    }
        //    return points;
        //}

        /// <summary>
        /// 获取扇形区域的点集合（不包括扇形的圆心点与边缘两点）
        /// </summary>
        /// <param name="origin">圆心点</param>
        /// <param name="egdePoint">扇形弧边右边缘点</param>
        /// <param name="angleDiffer">x，z轴张角</param>
        /// <returns></returns>
        public static List<Vector3> GetSectorPoints_1(Vector3 origin, Vector3 egdePoint, float angleDiffer)
        {
            List<Vector3> outList = new List<Vector3>();
            float angle;
            Vector3 dir = egdePoint - origin;                               // 取两点的向量
            float radius = dir.magnitude;                                   // 获取扇形的半径

            int startEuler = (int)Vector2.SignedAngle(Vector2.right, new Vector2(dir.x, dir.z));        // 取出起始角度(带符号)
            for (int i = startEuler + 10, j = 0; i < angleDiffer + startEuler; j++, i += 10)
            {
                angle = Mathf.Deg2Rad * i;
                outList.Add(origin + new Vector3(radius * Mathf.Cos(angle), 0, radius * Mathf.Sin(angle)));       // 给底面点赋值
            }

            return outList;
        }

        /// <summary>
        /// 获取扇形区域的点集合（包括圆心点）没有地平线以下的区域
        /// </summary>
        /// <param name="origin">圆心点</param>
        /// <param name="tarPoint"></param>
        /// <param name="alpha">x,z轴张角</param>
        /// <param name="theta">x,y轴张角</param>
        /// <returns></returns>
        public static Vector3[] GetSectorPoints_2(Vector3 origin, Vector3 tarPoint, float alpha, float theta)
        {
            List<Vector3> points = new List<Vector3>();

            int halfAlpha = (int)(alpha / 2);

            Vector3 startVector_1 = tarPoint - origin;                                    // 获取底面中间向量

            float tempHeight = Mathf.Tan(theta * Mathf.Deg2Rad) * startVector_1.magnitude;// 扇形区域最边缘高度          

            // 获取扇形下表面和下表面的半径
            float radiusDown = (tarPoint - origin).magnitude;
            float radiusUp = radiusDown / Mathf.Cos(theta * Mathf.Deg2Rad);  // 获得扇形顶面半径

            // 获取扇形弧边所有点的方向向量
            List<Vector3> dirsDown = new List<Vector3>();
            List<Vector3> dirsUp = new List<Vector3>();

            for (int i = -halfAlpha, j = 0; i <= halfAlpha; i += 3, j++)
            {
                // 获取下弧边的方向向量
                Vector2 temp = Math2d.GetTargetVector(new Vector2(startVector_1.x, startVector_1.z), i);
                dirsDown.Add(new Vector3(temp.x, 0, temp.y));

                // 获取上弧边的方向向量
                Vector3 targetDir = temp.magnitude / Mathf.Cos(i * Mathf.Deg2Rad) * dirsDown[j].normalized + Vector3.up * tempHeight;
                dirsUp.Add(targetDir);
            }

            // 获取扇形所有点
            points.Add(origin);
            for (int i = 0; i < dirsDown.Count; i++)
            {
                points.Add(dirsDown[i].normalized * radiusDown + origin);
            }
            points.Add(origin);
            for (int i = 0; i < dirsUp.Count; i++)
            {
                points.Add(dirsUp[i].normalized * radiusUp + origin);
            }

            return points.ToArray();
        }

        #endregion

        #region 通用多边形相关

        public static List<int> DrawSimplePolygon(List<Vector3> points)
        {
            List<int> triangles = new List<int>();

            for (int i = 1; i < points.Count - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            return triangles;
        }

        /// <summary>
        /// 以index点和前后两个点构造一个三角形,判断点集内的其余点是否全部在这个三角形外部
        /// </summary>
        public static bool IsFragementIndex(List<Vector3> verts, int index, bool containEdge = true)
        {
            int len = verts.Count;
            List<Vector3> triangleVert = new List<Vector3>();
            int next = (index == len - 1) ? 0 : index + 1;
            int prev = (index == 0) ? len - 1 : index - 1;
            triangleVert.Add(verts[prev]);
            triangleVert.Add(verts[index]);
            triangleVert.Add(verts[next]);
            for (int i = 0; i < len; i++)
            {
                if (i != index && i != prev && i != next)
                {
                    if (IsPointInsidePolygon(verts[i], triangleVert, containEdge))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 判断点是否在多边形区域内
        /// </summary>
        /// <param name="p">待判断的点，格式：{ x: X坐标, y: Y坐标 }</param>
        /// <param name="poly">多边形顶点，数组成员的格式同</param>
        /// <returns>true:在多边形内，凹点   false：在多边形外，凸点</returns>
        public static bool IsPointInsidePolygon(Vector3 p, List<Vector3> poly, bool containEdge = true)
        {
            float px = p.x;
            float py = p.z;
            double sum = 0;

            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i, i++)
            {
                float sx = poly[i].x;
                float sy = poly[i].z;
                float tx = poly[j].x;
                float ty = poly[j].z;

                // 点与多边形顶点重合或在多边形的边上(这个判断有些情况不需要)
                if ((sx - px) * (px - tx) >= 0 && (sy - py) * (py - ty) >= 0 && (px - sx) * (ty - sy) == (py - sy) * (tx - sx))
                {
                    return containEdge;
                }

                // 点与相邻顶点连线的夹角
                var angle = Mathf.Atan2(sy - py, sx - px) - Math.Atan2(ty - py, tx - px);

                // 确保夹角不超出取值范围（-π 到 π）
                if (angle >= Mathf.PI)
                {
                    angle -= Mathf.PI * 2;
                }
                else if (angle <= -Mathf.PI)
                {
                    angle += Mathf.PI * 2;
                }

                sum += angle;
            }

            // 计算回转数并判断点和多边形的几何关系
            return Mathf.RoundToInt((float)(sum / Math.PI)) == 0 ? false : true;
        }

        /// <summary>
        /// 判断点是否在多边形区域内
        /// </summary>
        /// <param name="point"> 待判断的点 </param>
        /// <param name="mPoints"> 多边形 </param>
        /// <returns></returns>
        public static bool IsPointInsidePolygon(Vector3 point, List<Vector3> mPoints)
        {
            int nCross = 0;

            for (int i = 0; i < mPoints.Count; i++)
            {
                Vector3 p1 = mPoints[i];
                Vector3 p2 = mPoints[(i + 1) % mPoints.Count];

                // 取多边形任意一个边,做点point的水平延长线,求解与当前边的交点个数
                // p1p2是水平线段,要么没有交点,要么有无限个交点, 计为无交点
                if (p1.z == p2.z)
                {
                    continue;
                }

                // point 在p1p2 底部 --> 无交点   (等于底部时, 算有交点)
                if (point.z < Mathf.Min(p1.z, p2.z))
                {
                    continue;
                }

                // point 在p1p2 顶部 或等于顶部 --> 无交点
                if (point.z >= Mathf.Max(p1.z, p2.z))
                {
                    continue;
                }

                // 求解 point点水平线与当前p1p2边的交点的 X 坐标
                // 直线两点式方程：(y-y2)/(y1-y2) = (x-x2)/(x1-x2)
                float x = (point.z - p1.z) * (p2.x - p1.x) / (p2.z - p1.z) + p1.x;

                if (x > point.x)        // 当 x = point.x 时,说明 point 在 p1p2 线段上, 不成立
                {
                    nCross++;           // 只统计单边交点
                }
            }

            // 单边交点为奇数，点在多边形之内
            return (nCross % 2 == 1);
        }

        /// <summary>
        /// 判断点是否在多边形区域内
        /// </summary>
        /// <param name="point"> 待判断的点 </param>
        /// <param name="mPoints"> 多边形 </param>
        /// <returns></returns>
        public static bool IsPointInsidePolygon(Vector3 point, List<Vector3> mPoints, out List<Vector3> crossPoints)
        {
            int nCross = 0;
            crossPoints = new List<Vector3>();

            for (int i = 0; i < mPoints.Count; i++)
            {
                Vector3 p1 = mPoints[i];
                Vector3 p2 = mPoints[(i + 1) % mPoints.Count];

                // 取多边形任意一个边,做点point的水平延长线,求解与当前边的交点个数
                // p1p2是水平线段,要么没有交点,要么有无限个交点, 计为无交点
                if (p1.z == p2.z)
                {
                    // point点与p1p2在同一高度,加入p1点和p2点
                    if (p1.z == point.z)
                    {
                        crossPoints?.Add(p1);
                        crossPoints?.Add(p2);
                    }
                    continue;
                }
                // point 在p1p2 底部 --> 无交点   (等于底部时, 算有交点)
                if (point.z < Mathf.Min(p1.z, p2.z))
                {
                    continue;
                }

                // point 在p1p2 顶部 或等于顶部 --> 无交点
                if (point.z >= Mathf.Max(p1.z, p2.z))
                {
                    continue;
                }

                // 求解 point点水平线与当前p1p2边的交点的 X 坐标
                // 直线两点式方程：(y-y2)/(y1-y2) = (x-x2)/(x1-x2)
                float x = (point.z - p1.z) * (p2.x - p1.x) / (p2.z - p1.z) + p1.x;

                // 加上交点
                crossPoints?.Add(new Vector3(x, 0, point.z));

                if (x > point.x)        // 当 x = point.x 时,说明 point 在 p1p2 线段上, 不成立
                {
                    nCross++;           // 只统计单边交点
                }
            }

            // 单边交点为奇数，点在多边形之内
            return (nCross % 2 == 1);
        }
        public static bool IsPointInsidePolygon(Vector2 point, Vector2[] mPoints, out List<Vector2> crossPoints)
        {
            int nCross = 0;
            crossPoints = new List<Vector2>();

            for (int i = 0; i < 3; i++)
            {
                Vector2 p1 = mPoints[i];
                Vector2 p2 = mPoints[(i + 1) % 3];

                // 取多边形任意一个边,做点point的水平延长线,求解与当前边的交点个数
                // p1p2是水平线段,要么没有交点,要么有无限个交点, 计为无交点
                if (p1.y == p2.y)
                {
                    // point点与p1p2在同一高度,加入p1点和p2点
                    if (p1.y == point.y)
                    {
                        crossPoints?.Add(p1);
                        crossPoints?.Add(p2);
                    }
                    continue;
                }

                // 加上水平线与线段的端点交点
                if (point.y == p1.y)
                {
                    crossPoints?.Add(p1);
                }
                else if (point.y == p2.y)
                {
                    crossPoints?.Add(p2);
                }


                // point 在p1p2 底部 --> 无交点   (等于底部时, 算有交点)
                if (point.y < Mathf.Min(p1.y, p2.y))
                {
                    continue;
                }

                // point 在p1p2 顶部 或等于顶部 --> 无交点
                if (point.y >= Mathf.Max(p1.y, p2.y))
                {
                    continue;
                }

                // 求解 point点水平线与当前p1p2边的交点的 X 坐标
                // 直线两点式方程：(y-y2)/(y1-y2) = (x-x2)/(x1-x2)
                float x = (point.y - p1.y) * (p2.x - p1.x) / (p2.y - p1.y) + p1.x;

                // 加上交点
                crossPoints?.Add(new Vector2(x, point.y));

                if (x > point.x)        // 当 x = point.x 时,说明 point 在 p1p2 线段上, 不成立
                {
                    nCross++;           // 只统计单边交点
                }
            }

            // 单边交点为奇数，点在多边形之内
            return (nCross % 2 == 1);
        }

        ///// <summary>
        ///// 将一个多边形转化为多个三角形
        ///// </summary>
        ///// <param name="points"></param>
        ///// <returns></returns>
        //public static List<Vector3[]> PolygonToTriangles(List<Vector3> points)
        //{
        //    if (points.Count < 3)
        //    {
        //        return null;
        //    }
        //    List<Vector3[]> triangles = new List<Vector3[]>();
        //    int index = points.Count - 1;
        //    int next;
        //    int prev;

        //    while (points.Count > 3)
        //    {
        //        List<Vector3> polygon = new List<Vector3>(points);
        //        polygon.RemoveAt(index);

        //        //是否是凹点
        //        if (!IsPointInsidePolygon(points[index], polygon, false))
        //        {
        //            // 是否是可划分顶点:新的多边形没有顶点在分割的三角形内
        //            if (IsFragementIndex(index, points.ToList(), false))
        //            {
        //                //可划分，剖分三角形
        //                next = (index == points.Count - 1) ? 0 : index + 1;
        //                prev = (index == 0) ? points.Count - 1 : index - 1;

        //                triangles.Add(new Vector3[]
        //                {
        //                points[index],
        //                points[prev],
        //                points[next]
        //                });

        //                points.RemoveAt(index);

        //                index = (index + points.Count - 1) % points.Count;       // 防止出现index超出值域
        //                continue;
        //            }
        //        }
        //        index = (index + 1) % points.Count;
        //    }
        //    triangles.Add(new Vector3[] { points[1], points[0], points[2] });

        //    return triangles;
        //}

        /// <summary>
        /// 返回三角形的内部(整数)顶点
        /// </summary>
        /// <param name="trianglePoints">三角形的三个顶点</param>
        /// <returns></returns>
        public static List<Vector3> GetPointsInTriangle(List<Vector3> trianglePoints)
        {
            // 取出所围的四边形
            float xMin = Mathf.Min(Mathf.Min(trianglePoints[0].x, trianglePoints[1].x), trianglePoints[2].x);
            float zMax = Mathf.Max(Mathf.Max(trianglePoints[0].z, trianglePoints[1].z), trianglePoints[2].z);
            float zMin = Mathf.Min(Mathf.Min(trianglePoints[0].z, trianglePoints[1].z), trianglePoints[2].z);

            List<Vector3> InsidePoints = new List<Vector3>();   // 内部点集
            Vector3 pointTmp = Vector3.zero;                    // 临时点
            List<Vector3> crossPoints = new List<Vector3>();    // 交点点集（2个点）

            // 遍历四边形的内部所有点
            for (int z = (int)zMin, iLength = (int)zMax + 1; z < iLength; z++)
            {
                pointTmp.Set(xMin, 0, z);       // 设置临时点(固定x，将z递增)
                PhysicsMath.IsPointInsidePolygon(pointTmp, trianglePoints, out crossPoints);     // 获取z轴平行线与三角形的交点

                // 循环添加两个交点之间的网格点
                for (int x = (int)crossPoints[0].x, length = (int)crossPoints[crossPoints.Count - 1].x + 1; x < length; x++)
                {
                    InsidePoints.Add(new Vector3(x, 0, z));
                }
            }
            return InsidePoints;
        }

        /// <summary>
        /// 通过给定网格间隔，获取三角形内部的网格点
        /// </summary>
        /// <param name="trianglePoints">三角形顶点</param>
        /// <param name="pieceX">X的间隔</param>
        /// <param name="pieceZ">Z的间隔</param>
        /// <returns></returns>
        public static List<Vector3> GetPointsInTriangle(Vector3[] trianglePoints, float pieceX, float pieceZ)
        {
            float halfX = 0.5f * pieceX;
            float halfZ = 0.5f * pieceZ;

            // 归置三角形顶点
            for (int i = 0; i < trianglePoints.Length; i++)
            {
                trianglePoints[i].x = trianglePoints[i].x - trianglePoints[i].x % pieceX + halfX;
                trianglePoints[i].z = trianglePoints[i].z - trianglePoints[i].z % pieceZ + halfZ;
            }
            // 取出所围的四边形
            float xMin = Mathf.Min(trianglePoints[0].x, trianglePoints[1].x, trianglePoints[2].x);
            xMin = xMin - xMin % pieceX - halfX;      // 取到所在的网格中心点
            float zMax = Mathf.Max(trianglePoints[0].z, trianglePoints[1].z, trianglePoints[2].z);
            zMax = zMax + zMax % pieceZ + halfZ;
            float zMin = Mathf.Min(trianglePoints[0].z, trianglePoints[1].z, trianglePoints[2].z);
            zMin = zMin - zMin % pieceZ - halfZ;


            List<Vector3> InsidePoints = new List<Vector3>();   // 内部点集
            Vector3 pointTmp = Vector3.zero;                    // 临时点
            List<Vector3> crossPoints = new List<Vector3>();    // 交点点集（2个点）

            // 遍历四边形的内部所有点
            for (float z = zMin; z <= zMax; z += pieceZ)
            {
                pointTmp.Set(xMin, 0, z);       // 设置临时点(固定x，将z递增)

                IsPointInsidePolygon(pointTmp, trianglePoints.ToList(), out crossPoints);     // 获取z轴平行线与三角形的交点

                if (crossPoints.Count == 2)
                {
                    // 循环添加两个交点之间的网格中心点
                    for (float x = Mathf.Min(crossPoints[0].x, crossPoints[1].x); x <= Mathf.Max(crossPoints[0].x, crossPoints[1].x); x += pieceX)
                    {
                        InsidePoints.Add(new Vector3(x, 0, z));
                    }
                }
                crossPoints.Clear();
            }
            return InsidePoints;
        }

        #endregion

        #region 圆和球相关

        /// <summary>
        /// 获取一个球面表面的所有点坐标集合
        /// </summary>
        /// <param name="fromPos">球心</param>
        /// <param name="radius">半径</param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static Vector3[] GetVertices(Vector3 fromPos, float radius, int angle)
        {
            List<Vector3> vertices = new List<Vector3>();
            vertices.Clear();
            Vector3 direction = Vector3.zero;  // 射线方向
            Vector3 point = Vector3.zero;    // 坐标点

            point = new Vector3(fromPos.x, fromPos.y + radius, fromPos.z);    // 算出圆球的最高点

            vertices.Add(point);     // 添加圆球最高点

            // 通过球坐标系方式遍历所需的点并加入网格顶点中
            for (int theta = 1; theta <= angle; theta += 1)
            {
                for (int alpha = 0; alpha < 360; alpha += 1)
                {
                    float radTheta = theta * Mathf.Deg2Rad;
                    float radAlpha = alpha * Mathf.Deg2Rad;

                    // 计算出方向向量
                    direction.Set(Mathf.Sin(radTheta) * Mathf.Cos(radAlpha),
                           Mathf.Cos(radTheta),
                           Mathf.Sin(radTheta) * Mathf.Sin(radAlpha));

                    point = fromPos + radius * direction;
                    vertices.Add(point);
                }
            }

            // 加入圆心点
            vertices.Add(fromPos);

            return vertices.ToArray();
        }

        /// <summary>
        /// 从上而下顺时针排列三角形顶点渲染顺序
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static int[] Sort3(int angle)
        {
            List<int> triangles = new List<int>();
            triangles.Clear();
            int m = 360;
            int n = angle;

            for (int i = 1; i <= m; i++)
            {
                //为最上层顶点排序
                triangles.Add(i);
                triangles.Add(0);
                triangles.Add((i % m) + 1);

                triangles.Add(i);
                triangles.Add((i % m) + 1);
                triangles.Add(i + m);

                // 下层顶点排序
                triangles.Add(i + m * (n - 1));
                triangles.Add((i % m) + 1 + m * (n - 2));
                triangles.Add((i % m) + 1 + m * (n - 1));

                triangles.Add(i + m * (n - 1));
                triangles.Add(m * n + 1);
                triangles.Add((i % m) + 1 + m * (n - 1));
            }

            for (int j = 1; j < n - 1; j++)
            {
                //循环遍历为中层顶点排序
                for (int i = 1; i <= m; i++)
                {
                    triangles.Add(i + (m * j));
                    triangles.Add((i % m) + 1 + (j - 1) * m);
                    triangles.Add((i % m) + 1 + (m * j));

                    triangles.Add(i + (m * j));
                    triangles.Add((i % m) + 1 + (m * j));
                    triangles.Add(i + (1 + j) * m);
                }
            }

            return triangles.ToArray();
        }
        #endregion

        #region 空中走廊相关

        /// <summary>
        /// Resion 1.0 获取空中走廊所有顶点(按节数排列)
        /// </summary>
        /// 侧面与地面垂直，连接处是角，连接处连接于底面平行
        /// <param name="_list"> 路径点 </param>
        /// <param name="_width"> 宽度 </param>
        /// <param name="_height"> 高度 </param>
        /// <returns></returns>
        public static Vector3[] GetAirCorridorSpace(List<Vector3> _list, float _width, float _height)
        {
            List<Vector3> verticesList = new List<Vector3>();
            int count = _list.Count;

            Vector3 _dir2 = Vector3.zero;
            Vector3 _dir3 = Vector3.zero;

            _width /= 2;
            _height /= 2;

            Vector3 _dir = Math2d.GetHorizontalDir(_list[0], _list[1]);        // 获取两点之间的垂直向量;
            verticesList.Add(_list[0] + _dir * _width - Vector3.up * _height);       // 右下点
            verticesList.Add(_list[0] - _dir * _width - Vector3.up * _height);       // 左下点

            for (int i = 1; i < count - 1; i++)
            {
                //计算相连三个点夹角的补角的一半的余弦值
                Vector2 pos_1 = new Vector2(_list[i + 1].x - _list[i].x, _list[i + 1].z - _list[i].z);
                Vector2 pos_2 = new Vector2(_list[i].x - _list[i - 1].x, _list[i].z - _list[i - 1].z);
                float theta = Vector2.Angle(pos_1, pos_2) / 2;
                float cosTheta = Mathf.Cos(Mathf.Deg2Rad * theta);

                _dir = Math2d.GetHorizontalDir(_list[i - 1], _list[i]);        // 获取两点之间的垂直向量
                _dir2 = Math2d.GetHorizontalDir(_list[i], _list[i + 1]);       // 获取两点之间的垂直向量
                _dir3 = ((_dir + _dir2) / 2).normalized;              // 获取方向向量

                verticesList.Add(_list[i] + _dir3 * _width / cosTheta - Vector3.up * _height);
                verticesList.Add(_list[i] - _dir3 * _width / cosTheta - Vector3.up * _height);
            }

            _dir = Math2d.GetHorizontalDir(_list[count - 2], _list[count - 1]);    // 获取两点之间的垂直向量        
            verticesList.Add(_list[count - 1] + _dir * _width - Vector3.up * _height);       // 右下点
            verticesList.Add(_list[count - 1] - _dir * _width - Vector3.up * _height);       // 左下点

            return verticesList.ToArray();
        }

        /// <summary>
        /// Resion 2.0 获取空中走廊下表面点集(顶角边)
        /// </summary>
        /// <param name="_list"> 路径点 </param>
        /// <param name="_width"> 宽度 </param>
        /// <param name="_height"> 高度 </param>
        /// <returns></returns>
        public static Vector3[] GetAirSpaceBottomPoints(List<Vector3> _list, float _width, float _height)
        {
            List<Vector3> verticesList = new List<Vector3>();
            List<Vector3> tmpList = new List<Vector3>();
            int count = _list.Count;
            if (count < 2)
            {
                Debug.LogError($"空中走廊List数量：{count}");
                return verticesList.ToArray();
            }

            _height /= 2;
            _width /= 2;

            Vector3 _dir2 = Vector3.zero;
            Vector3 _dir3 = Vector3.zero;

            Vector3 _dir = Math2d.GetHorizontalDir(_list[0], _list[1]);        // 获取两点之间的垂直向量;
            verticesList.Add(_list[0] + _dir * _width - Vector3.up * _height);       // 右下点
            tmpList.Add(_list[0] - _dir * _width - Vector3.up * _height);            // 左下点

            for (int i = 1; i < count - 1; i++)
            {
                #region MyRegion

                //_dir = GetVerticalDir(_list[i - 1], _list[i]);        // 获取两点之间的垂直向量
                //_dir2 = GetVerticalDir(_list[i], _list[i + 1]);       // 获取两点之间的垂直向量

                //pos1 = _list[i - 1] + _dir * _width - Vector3.up * _height;
                //pos2 = _list[i] + _dir * _width - Vector3.up * _height;
                //pos3 = _list[i] + _dir2 * _width - Vector3.up * _height;
                //pos4 = _list[i + 1] + _dir2 * _width - Vector3.up * _height;
                //verticesList.Add(LianZX_JD(pos1, pos2, pos3, pos4));        //  右下点

                //pos1 = _list[i - 1] + _dir * _width + Vector3.up * _height;
                //pos2 = _list[i] + _dir * _width + Vector3.up * _height;
                //pos3 = _list[i] + _dir2 * _width + Vector3.up * _height;
                //pos4 = _list[i + 1] + _dir2 * _width + Vector3.up * _height;
                //verticesList.Add(LianZX_JD(pos1, pos2, pos3, pos4));        //  右上点

                //pos1 = _list[i - 1] - _dir * _width + Vector3.up * _height;
                //pos2 = _list[i] - _dir * _width + Vector3.up * _height;
                //pos3 = _list[i] - _dir2 * _width + Vector3.up * _height;
                //pos4 = _list[i + 1] - _dir2 * _width + Vector3.up * _height;
                //tmpList.Add(LianZX_JD(pos1, pos2, pos3, pos4));             //  左上点

                //pos1 = _list[i - 1] - _dir * _width - Vector3.up * _height;
                //pos2 = _list[i] - _dir * _width - Vector3.up * _height;
                //pos3 = _list[i] - _dir2 * _width - Vector3.up * _height;
                //pos4 = _list[i + 1] - _dir2 * _width - Vector3.up * _height;
                //tmpList.Add(LianZX_JD(pos1, pos2, pos3, pos4));             //  左下点

                #endregion

                ////计算相连三个点夹角的补角的一半的余弦值
                //Vector2 pos_1 = new Vector2(_list[i + 1].x - _list[i].x, _list[i + 1].z - _list[i].z);
                //Vector2 pos_2 = new Vector2(_list[i].x - _list[i - 1].x, _list[i].z - _list[i - 1].z);
                //float theta = Vector2.Angle(pos_1, pos_2);
                //theta /= 2.00f;
                //float cosTheta = Mathf.Cos(Mathf.Deg2Rad * theta);

                // 计算相连三点夹角的一半的正弦值
                Vector2 pos1 = new Vector2(_list[i].x - _list[i - 1].x, _list[i].z - _list[i - 1].z);
                Vector2 pos2 = new Vector2(_list[i].x - _list[i + 1].x, _list[i].z - _list[i + 1].z);
                float alpha = Vector2.Angle(pos1, pos2) / 2.00f;
                float sinAlpha = Mathf.Sin(Mathf.Deg2Rad * alpha);

                _dir = Math2d.GetHorizontalDir(_list[i - 1], _list[i]);        // 获取两点之间的垂直向量
                _dir2 = Math2d.GetHorizontalDir(_list[i], _list[i + 1]);       // 获取两点之间的垂直向量
                _dir3 = ((_dir + _dir2) / 2).normalized;

                float maxDir = _width / sinAlpha;       // 限制宽度不能超过前后两点之间的距离
                if (maxDir > Vector3.Magnitude(_list[i] - _list[i - 1]))
                {
                    maxDir = Vector3.Magnitude(_list[i] - _list[i - 1]);
                }
                if (maxDir > Vector3.Magnitude(_list[i] - _list[i + 1]))
                {
                    maxDir = Vector3.Magnitude(_list[i] - _list[i + 1]);
                }

                verticesList.Add(_list[i] + _dir3 * maxDir - Vector3.up * _height);      // 右下点
                tmpList.Add(_list[i] - _dir3 * maxDir - Vector3.up * _height);           // 左下点

            }


            _dir = Math2d.GetHorizontalDir(_list[count - 2], _list[count - 1]);    // 获取两点之间的垂直向量        
            verticesList.Add(_list[count - 1] + _dir * _width - Vector3.up * _height);              // 右下点
            tmpList.Add(_list[count - 1] - _dir * _width - Vector3.up * _height);                   // 左下点

            for (int i = tmpList.Count - 1; i >= 0; i--)
            {
                verticesList.Add(tmpList[i]);
            }

            return verticesList.ToArray();
        }

        /// <summary>
        /// Resion 2.0 获取空中走廊下表面顶点(弧边)
        /// </summary>
        /// <param name="_bottomList"> 下表面点集 </param>
        /// <param name="_width"> 宽度 </param>
        /// <param name="_height"> 高度 </param>
        /// <returns></returns>
        public static Vector3[] GetAirBottomSpaceWithSector(List<Vector3> _bottomList, float _width)
        {
            List<Vector3> verticesList = new List<Vector3>();       // 输出点集

            List<Vector3> squareList = new List<Vector3>();         // 方形点集
            List<Vector3> arcList = new List<Vector3>();            // 圆弧点集
            int bottomCount = _bottomList.Count;
            //_width /= 2;

            // 加入方形前两点
            squareList.Add(_bottomList[0]);
            squareList.Add(_bottomList[bottomCount - 1]);

            List<Vector3> polygon;
            for (int i = 1; i < bottomCount / 2 - 1; i++)
            {
                //创建一个除去自身前一点的 多边形，判断前一点是否为内点（凹点）
                polygon = new List<Vector3>(_bottomList.ToArray());
                polygon.RemoveAt(i);

                // 对 index 点进行弧边处理
                Vector3 _dir1 = (_bottomList[i] - _bottomList[i - 1]).normalized;        // 获取两点之间的垂直向量
                Vector3 _dir2 = (_bottomList[i + 1] - _bottomList[i]).normalized;        // 获取两点之间的垂直向量

                //是否是凹点
                if (IsPointInsidePolygon(_bottomList[i], polygon))
                {
                    Vector2 _dirV2 = Math2d.GetTargetVector(new Vector2(_dir1.x, _dir1.z), 90);
                    _dir1 = new Vector3(_dirV2.x, 0, _dirV2.y);    // 获取左弧边向量
                    _dirV2 = Math2d.GetTargetVector(new Vector2(_dir2.x, _dir2.z), 90);
                    _dir2 = new Vector3(_dirV2.x, 0, _dirV2.y);    // 获取右弧边向量

                    arcList.Add(_bottomList[i]);                                                                // 添加扇心

                    //加上0.05中和浮点数精度问题
                    for (float j = 0; j <= 1.05f; j += 0.1f)
                    {
                        if (_dir1 == -_dir2)
                        {
                            arcList.Add(_dir1 * _width + _bottomList[i]);
                            Debug.Log("???");
                        }
                        else
                        {
                            arcList.Add(Vector3.Slerp(_dir1, _dir2, j).normalized * _width + _bottomList[i]);       // 添加进圆弧点集
                        }
                    }

                    Vector3 leftArcPoint = _bottomList[i] + _dir1.normalized * _width;                          // 圆弧左点
                    Vector3 rightArcPoint = _bottomList[i] + _dir2.normalized * _width;                         // 圆弧右点

                    squareList.Add(_bottomList[i]);                // 添加进方形点集
                    squareList.Add(leftArcPoint);
                    squareList.Add(_bottomList[i]);
                    squareList.Add(rightArcPoint);
                }
                else    // 不是凹点， 则对面顶点为凹点
                {
                    Vector2 _dirV2 = Math2d.GetTargetVector(new Vector2(_dir1.x, _dir1.z), -90);
                    _dir1 = new Vector3(_dirV2.x, 0, _dirV2.y);    // 获取左弧边向量
                    _dirV2 = Math2d.GetTargetVector(new Vector2(_dir2.x, _dir2.z), -90);
                    _dir2 = new Vector3(_dirV2.x, 0, _dirV2.y);    // 获取右弧边向量

                    arcList.Add(_bottomList[bottomCount - 1 - i]);                                              // 添加扇心
                    List<Vector3> vectors = new List<Vector3>();                                                // 方向向量集 

                    //减去0.05中和浮点数精度问题
                    for (float j = 1; j >= -0.05; j -= 0.1f)
                    {
                        arcList.Add(Vector3.Slerp(_dir1, _dir2, j).normalized * _width + _bottomList[bottomCount - 1 - i]);        // 添加进圆弧点集
                    }

                    Vector3 leftArcPoint = _bottomList[bottomCount - 1 - i] + _dir1.normalized * _width;        // 圆弧左点
                    Vector3 rightArcPoint = _bottomList[bottomCount - 1 - i] + _dir2.normalized * _width;       // 圆弧右点

                    squareList.Add(leftArcPoint);                                                               // 添加进方形点集
                    squareList.Add(_bottomList[bottomCount - 1 - i]);
                    squareList.Add(rightArcPoint);
                    squareList.Add(_bottomList[bottomCount - 1 - i]);
                }

                polygon.Clear();
            }

            // 添加方形最后两点
            squareList.Add(_bottomList[bottomCount / 2 - 1]);
            squareList.Add(_bottomList[bottomCount / 2]);

            // 合并方形点集和扇形点集
            verticesList.AddRange(squareList);
            verticesList.AddRange(arcList);

            return verticesList.ToArray();
        }

        #endregion

        /// <summary>
        /// 计算线段是否相交
        /// 端点相交不算相交
        /// </summary>
        public static bool LineLineIntersection(Vector3 line0Start, Vector3 line0End, Vector3 line1Start, Vector3 line1End)
        {
            Vector3 p = line0Start;
            Vector3 r = line0End - line0Start;
            Vector3 q = line1Start;
            Vector3 s = line1End - line1Start;
            Vector3 pq = q - p;
            float rxs = r.x * s.z - r.z * s.x;
            float pqxr = pq.x * r.z - pq.z * r.x;
            if (MathX.IsApproximately(rxs, 0f))
            {
                if (MathX.IsApproximately(pqxr, 0f))
                {
                    return true;
                }
                return false;
            }
            float pqxs = pq.x * s.z - pq.z * s.x;
            float t = pqxs / rxs;
            float u = pqxr / rxs;
            return t > 0 && t < 1 && u > 0 && u < 1;
        }
    }
}