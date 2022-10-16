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
            if (Math2d.IsPointInsidePolygon(points[0], polygon))
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

        #region 半球形与圆形

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
                Math2d.IsPointInsidePolygon(pointTmp, trianglePoints.ToArray(), out crossPoints);     // 获取z轴平行线与三角形的交点

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

                Math2d.IsPointInsidePolygon(pointTmp, trianglePoints, out crossPoints);     // 获取z轴平行线与三角形的交点

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