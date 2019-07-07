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
        // 大小比较
        private const double epsilon = 1e-7;
        // 小与
        private bool FloatLess(float value, float other)
        {
            return (other - value) > epsilon;
        }
        // 大于
        private bool FloatGreat(float value, float other)
        {
            return (value - other) > epsilon;
        }
        // float等于
        public static bool FloatEqual(float value, float other)
        {
            return Mathf.Abs(value - other) < epsilon;
        }
        // Vector等于
        public static bool Vector3Equal(Vector3 a, Vector3 b)
        {
            return FloatEqual(a.x, b.x) && FloatEqual(a.y, b.y) && FloatEqual(a.z, b.z);
        }

        //获取平方根 一元二次方程求根公式 x = (-b+(b^2-4ac)^1/2)/2a
        public static float GetSqrtOfMath(float a, float b, float d)
        {
            float a1 = (-b + Mathf.Sqrt(d)) / (2 * a);
            float a2 = (-b - Mathf.Sqrt(d)) / (2 * a);

            return a1 > a2 ? a1 : a2;
        }

        private float GetDelta(float a, float b, float c)
        {
            return b * b - 4 * a * c;
        }

        /// <summary>
        /// 获取平面向量向右旋转theta后的目标向量
        /// </summary>
        /// <param name="startVector"></param>
        /// <param name="theta"></param>
        /// <returns></returns>
        public static Vector2 GetTargetVector(Vector2 startVector, float theta = 90)
        {
            float x = startVector.x * Mathf.Cos(theta * Mathf.Deg2Rad) - startVector.y * Mathf.Sin(theta * Mathf.Deg2Rad);
            float y = startVector.x * Mathf.Sin(theta * Mathf.Deg2Rad) + startVector.y * Mathf.Cos(theta * Mathf.Deg2Rad);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 获取三维空间向量绕已知轴旋转θ角后的得到的向量
        /// </summary>
        /// <param name="startVector">待旋转向量</param>
        /// <param name="n">旋转轴</param>
        /// <param name="theta">旋转角度(弧度)</param>
        /// <returns></returns>
        public static Vector3 GetTargetVector(Vector3 startVector, Vector3 n, float theta)
        {
            float x = (n.x * n.x * (1 - Mathf.Cos(theta)) + Mathf.Cos(theta)) * startVector.x +
                (n.x * n.y * (1 - Mathf.Cos(theta)) + n.z * Mathf.Sin(theta)) * startVector.y +
                (n.x * n.z * (1 - Mathf.Cos(theta)) - n.y * Mathf.Sin(theta)) * startVector.z;

            float y = (n.x * n.y * (1 - Mathf.Cos(theta)) - n.z * Mathf.Sin(theta)) * startVector.x +
                (n.y * n.y * (1 - Mathf.Cos(theta)) + Mathf.Cos(theta)) * startVector.y +
                (n.y * n.z * (1 - Mathf.Cos(theta)) + n.x * Mathf.Sin(theta)) * startVector.z;

            float z = (n.x * n.z * (1 - Mathf.Cos(theta)) + n.y * Mathf.Sin(theta)) * startVector.x +
                (n.z * n.y * (1 - Mathf.Cos(theta)) - n.x * Mathf.Sin(theta)) * startVector.y +
                (n.z * n.z * (1 - Mathf.Cos(theta)) + Mathf.Cos(theta)) * startVector.z;

            return new Vector3(x, y, z);
        }

        // 排序所给点为顺时针方向
        public static Vector3[] CheckVector(List<Vector3> points)
        {
            // 创建一个除去自身前一点的 多边形，判断前一点是否为内点（凹点）
            List<Vector3> polygon = new List<Vector3>(points);
            polygon.RemoveAt(0);

            Vector3 vector3_1 = points[0] - points[1];
            Vector3 vector3_2 = points[points.Count - 1] - points[0];

            Vector3 nom = Vector3.Cross(vector3_1, vector3_2);  // 算出法线方向

            //是否是凹点
            if (IsPointInsidePolygon(points[0], polygon))
            {
                // 法线方向朝上。即逆时针排序，需要反转
                if (nom.y > 0)
                {
                    points.Reverse();
                }
            }
            else   // 凸点
            {
                // 法线方向朝上。即逆时针排序，需要反转
                if (nom.y < 0)
                {
                    points.Reverse();
                }
            }

            return points.ToArray();
        }

        // 获取扇形弧边中点
        public static Vector3 GetSectorOutPoint(Vector3 origin, Vector3 leftPoint, Vector3 rightPoint)
        {
            Vector3 leftVector = leftPoint - origin;                // 左向量
            Vector3 rightVector = rightPoint - origin;              // 右向量
            float radius = Vector3.Distance(origin, leftPoint);     // 半径

            return (leftPoint + rightPoint).normalized * radius;
        }

        #endregion

        #region 点线面之间的关系

        // y为0的伯努利方程,angle为弧度
        public static Vector3 GetBernoulli(int a, float angle, float y)
        {
            float p = Mathf.Sqrt(a * a * Mathf.Cos(2 * angle));
            float x = p * Mathf.Cos(angle);
            float z = p * Mathf.Sin(angle);
            if ((0.75) * Mathf.PI < angle && angle < (1.25) * Mathf.PI)
            {
                return new Vector3(x, y, -z);
            }
            else
            {
                return new Vector3(x, y, z);
            }
        }

        /// <summary>
        /// 从一个数组中取固定位置子数组
        /// </summary>
        /// <param name="SourceArray"> 原始数组 </param>
        /// <param name="StartIndex"> 起始位置 </param>
        /// <param name="EndIndex"> 终止位置 </param>
        /// <returns></returns>
        public static int[] GetRangeArray(int[] SourceArray, int StartIndex, int EndIndex)
        {
            try
            {
                int[] result = new int[EndIndex - StartIndex + 1];      // 存放结果的数组
                for (int i = 0; i <= EndIndex - StartIndex; i++)
                {
                    result[i] = SourceArray[i + StartIndex];
                }
                return result;
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new System.Exception(ex.Message);
            }
        }

        /// <summary>
        ///求两直线的交点坐标
        ///给定两个点P1和P2，直线上的点为P
        ///参数方程：
        ///   P＝ P1 ＋ t*(P2-P1）
        ///展开就是
        ///   p.x = p1.x + t*(p2.x-p1.x)
        ///   p.y = p1.y + t*(p2.y-p1.y)
        ///   p.z = p1.z + t*(p2.z-p1.z)
        ///这种写法就比用等式的好，因为不存在分母为0的问题
        /// </summary>
        /// <param name="pn1">L1上的点</param>
        /// <param name="pn2">L1上的点</param>
        /// <param name="pn3">L2上的点</param>
        /// <param name="pn4">L2上的点</param>
        /// <returns></returns>
        public static Vector3 GetIntersectionPoint(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
        {
            float P1x = 0.0f;
            float P1y = 0.0f;
            float P1z = 0.0f;
            double plr1_x = p2.x - p1.x;
            double plr1_y = p2.y - p1.y;
            double plr1_z = p2.z - p1.z;
            double plr2_x = p4.x - p3.x;
            double plr2_y = p4.y - p3.y;
            double plr2_z = p4.z - p3.z;
            double t = 1.0f;
            //有且只有一条直线垂直于X轴时
            if (((plr1_x != 0) && (plr2_x == 0)) || ((plr1_x == 0) && (plr2_x != 0)))
            {
                if (plr2_x == 0)
                {
                    t = (p3.x - p1.x) / plr1_x;
                    P1x = (float)(p1.x + t * plr1_x);
                    P1y = (float)(p1.y + t * plr1_y);
                    P1z = (float)(p1.z + t * plr1_z);
                    return new Vector3(P1x, P1y, P1z);
                }
                else
                {
                    t = (p1.x - p3.x) / plr2_x;
                    P1x = (float)(p3.x + t * plr2_x);
                    P1y = (float)(p3.y + t * plr2_y);
                    P1z = (float)(p3.z + t * plr2_z);
                    return new Vector3(P1x, P1y, P1z);
                }
            }
            //有且只有一条直线垂直于Y轴时
            else if (((plr1_y != 0) && (plr2_y == 0)) || ((plr1_y == 0) && (plr2_y != 0)))
            {
                if (plr2_y == 0)
                {
                    t = (p3.y - p1.y) / plr1_y;
                    P1x = (float)(p1.x + t * plr1_x);
                    P1y = (float)(p1.y + t * plr1_y);
                    P1z = (float)(p1.z + t * plr1_z);
                    return new Vector3(P1x, P1y, P1z);
                }
                else
                {
                    t = (p1.y - p3.y) / plr2_y;
                    P1x = (float)(p3.x + t * plr2_x);
                    P1y = (float)(p3.y + t * plr2_y);
                    P1z = (float)(p3.z + t * plr2_z);
                    return new Vector3(P1x, P1y, P1z);
                }
            }
            //有且只有一条直线垂直于Z轴时
            else if (((plr1_z != 0) && (plr2_z == 0)) || ((plr1_z == 0) && (plr2_z != 0)))
            {
                if (plr2_z == 0)
                {
                    t = (p3.z - p1.z) / plr1_z;
                    P1x = (float)(p1.x + t * plr1_x);
                    P1y = (float)(p1.y + t * plr1_y);
                    P1z = (float)(p1.z + t * plr1_z);
                    return new Vector3(P1x, P1y, P1z);
                }
                else
                {
                    t = (p1.z - p3.z) / plr2_z;
                    P1x = (float)(p3.x + t * plr2_x);
                    P1y = (float)(p3.y + t * plr2_y);
                    P1z = (float)(p3.z + t * plr2_z);
                    return new Vector3(P1x, P1y, P1z);
                }
            }
            //其他情况
            else
            {
                if (((plr1_x != 0) && (plr2_x != 0)) && ((plr1_y != 0) && (plr2_y != 0)))
                {
                    double fz = (p3.x * plr2_y - p3.y * plr2_x - plr2_y * p1.x + plr2_x * p1.y);
                    double fm = (plr1_x * plr2_y - plr1_y * plr2_x);
                    t = fz / fm;
                    P1x = (float)(p1.x + t * plr1_x);
                    P1y = (float)(p1.y + t * plr1_y);
                    P1z = (float)(p1.z + t * plr1_z);
                    return new Vector3(P1x, P1y, P1z);
                }
                else if (((plr1_x != 0) && (plr2_x != 0)) && ((plr1_z != 0) && (plr2_z != 0)))
                {
                    double fz = (p3.x * plr2_z - p3.z * plr2_x - plr2_z * p1.x + plr2_x * p1.z);
                    double fm = (plr1_x * plr2_z - plr1_z * plr2_x);
                    t = fz / fm;
                    P1x = (float)(p1.x + t * plr1_x);
                    P1y = (float)(p1.y + t * plr1_y);
                    P1z = (float)(p1.z + t * plr1_z);
                    return new Vector3(P1x, P1y, P1z);
                }
                else if (((plr1_y != 0) && (plr2_y != 0)) && ((plr1_z != 0) && (plr2_z != 0)))
                {
                    double fz = (p3.y * plr2_z - p3.z * plr2_y - plr2_z * p1.y + plr2_y * p1.z);
                    double fm = (plr1_y * plr2_z - plr1_z * plr2_y);
                    t = fz / fm;
                    P1x = (float)(p1.x + t * plr1_x);
                    P1y = (float)(p1.y + t * plr1_y);
                    P1z = (float)(p1.z + t * plr1_z);
                    return new Vector3(P1x, P1y, P1z);
                }
                else
                {
                    return Vector3.zero;
                }
            }
        }

        /// <summary>
        /// 获取两点间向右的水平垂直向量，忽略Y轴
        /// </summary>
        /// <param name="_start"> 起始点 </param>
        /// <param name="_end"> 终止点 </param>
        /// <returns></returns>
        public static Vector3 GetHorizontalDir(Vector3 _start, Vector3 _end)
        {
            Vector3 _dirValue = (_end - _start);
            return GetHorizontalDir(_dirValue);
        }
        public static Vector3 GetHorizontalDir(Vector3 _dirValue)
        {
            Vector3 returnVec = new Vector3(_dirValue.z, 0, -_dirValue.x);
            return returnVec.normalized;
        }
        public static Vector2 GetHorizontalDir(Vector2 _dir)
        {
            return new Vector2(_dir.y, -_dir.x).normalized;
        }

        /// <summary>
        /// 获取两点间向上的竖直垂直向量
        /// </summary>
        /// <param name="_start"> 起始点 </param>
        /// <param name="_end"> 终止点 </param>
        /// <returns></returns>
        public static Vector3 GetVerticalDir(Vector3 _start, Vector3 _end)
        {
            Vector3 vector = _end - _start;             // 两点之间的向量
            return GetVerticalDir(vector);
        }
        public static Vector3 GetVerticalDir(Vector3 vector)
        {
            Vector3 dirUp;                              // 两点间向量的垂直向量
            if (vector.y == 0)
            {
                dirUp = Vector3.up;
            }
            else if (vector.y > 0)
            {
                //向上的垂直向量的x和z的值和原向量相等，且两个向量内积等于0    
                dirUp = new Vector3(vector.x, (vector.x * vector.x + vector.z * vector.z) / -vector.y, vector.z).normalized;
                dirUp = -dirUp;
            }
            else
            {
                dirUp = new Vector3(vector.x, (vector.x * vector.x + vector.z * vector.z) / -vector.y, vector.z).normalized;
            }
            return dirUp;
        }

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
            Vector3 dir = GetHorizontalDir(_start, _end);
            rect[0] = _start + dir * _width;
            rect[1] = _start - dir * _width;
            rect[2] = _end + dir * _width;
            rect[3] = _end - dir * _width;

            return rect;
        }
        public static Vector2[] GetRect(Vector2 _start, Vector2 _end, float _width)
        {
            Vector2[] rect = new Vector2[4];
            Vector2 dir = GetHorizontalDir(_end - _start);
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
                    outList.Add(GetBezierPoint(i / _count, _points[0], _points[1], _points[2]));
                }
            }
            if (_points.Length == 4)
            {
                for (float i = 0; i <= _count; i++)
                {
                    outList.Add(GetBezierPoint(i / _count, _points[0], _points[1], _points[2], _points[3]));
                }
            }
            return outList;
        }

        /// <summary>
        /// 获取二次贝塞尔曲线上点
        /// </summary>
        /// <param name="_t">间隔</param>
        /// <param name="_P0">起始点</param>
        /// <param name="_P1">控制点</param>
        /// <param name="_P2">末尾点</param>
        /// <returns></returns>
        private static Vector3 GetBezierPoint(float _t, Vector3 _P0, Vector3 _P1, Vector3 _P2)
        {
            float u = 1 - _t;
            return u * u * _P0 + 2 * _t * u * _P1 + _t * _t * _P2;
        }

        /// <summary>
        /// 获取三次贝塞尔曲线上点
        /// </summary>
        /// <param name="_t">间隔</param>
        /// <param name="_P0">起始点</param>
        /// <param name="_P1">控制点1</param>
        /// <param name="_P2">控制点2</param>
        /// <param name="_P3">末尾点</param>
        /// <returns></returns>
        private static Vector3 GetBezierPoint(float _t, Vector3 _P0, Vector3 _P1, Vector3 _P2, Vector3 _P3)
        {
            float u = 1 - _t;
            return u * u * u * _P0 + 3 * u * u * _t * _P1 + 3 * _t * _t * u * _P2 + _t * _t * _t * _P3;
        }


        /// <summary>         
        /// 获取曲线上面的所有路径点
        /// </summary> 
        /// <returns>The list.</returns> 
        /// <param name="wayPoints">路点列表</param> 
        /// <param name="pointSize">两个点之间的节点数量</param> 
        public static List<Vector3> GetPoints(List<Vector3> wayPoints)
        {
            Vector3[] controlPointList = PathControlPointGenerator(wayPoints.ToArray());
            int smoothAmount = 0;       // wayPoints.Length * pointSize;     

            // 根据 路点 间的距离计算所需 路径点 的数量
            for (int i = 0, length = wayPoints.Count - 1; i < length; i++)
            {
                smoothAmount += (int)Vector3.Distance(wayPoints[i + 1], wayPoints[i]) / 4;
            }

            Vector3[] pointArr = new Vector3[smoothAmount];
            for (int index = 1; index <= smoothAmount; index++)
            {
                pointArr[index - 1] = Interp(controlPointList, (float)index / smoothAmount);
            }
            return pointArr.ToList();
        }
        /// <summary> 
        /// 获取控制点 
        /// </summary> 
        /// <returns>The control point generator.</returns> 
        /// <param name="path">Path.</param> 
        private static Vector3[] PathControlPointGenerator(Vector3[] path)
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
        private static Vector3 Interp(Vector3[] pts, float t)
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

        /// <summary>
        /// 获取半径为r的圆的点坐标集合
        /// </summary>
        /// <param name="origin"> 圆心 </param>
        /// <param name="radius"> 半径 </param>
        /// <returns></returns>
        public static Vector3[] GetCirclePoints(Vector3 origin, float radius, int seg = 120)
        {
            float angle;
            Vector3[] points = new Vector3[seg];
            for (int i = 0, j = 0; i < 360; j++, i += 360 / seg)
            {
                angle = Mathf.Deg2Rad * i;
                points[j] = origin + new Vector3(radius * Mathf.Cos(angle), 0, radius * Mathf.Sin(angle));
            }
            //points[360] = origin;     // 圆心点
            return points;
        }

        /// <summary>
        /// 获取椭圆上的点
        /// </summary>
        /// <param name="_r">短轴</param>
        /// <param name="_R">长轴</param>
        /// <param name="_origin">中点</param>
        /// <param name="seg">间隔</param>
        /// <returns></returns>
        public static Vector3[] GetEllipsePoints(float _r, float _R, Vector3 _origin, int seg)
        {
            float angle;
            Vector3[] points = new Vector3[seg];
            int j = 0;
            for (float i = 0; i < 360; j++, i += 360 / seg)
            {
                angle = (i / 180) * Mathf.PI;
                points[j] = _origin + new Vector3(_r * Mathf.Cos(angle), 0, _R * Mathf.Sin(angle));
            }
            return points;
        }

        /// <summary>
        /// 获取扇形区域的点集合（包括圆心点）
        /// </summary>
        /// <param name="origin">圆心点</param>
        /// <param name="egdePoint">扇形弧边右边缘点</param>
        /// <param name="angleDiffer">x，z轴张角</param>
        /// <returns></returns>
        public static Vector3[] GetSectorPoints_1(Vector3 origin, Vector3 egdePoint, float angleDiffer)
        {
            float angle;
            Vector3 dir = egdePoint - origin;                               //取两点的向量
            float radius = dir.magnitude;                                   //获取扇形的半径

            //取数组长度 如60度的弧边取61个点 0~60 再加上一个圆心点
            Vector3[] points = new Vector3[(int)(angleDiffer / 3) + 2];
            points[0] = origin;                                             //取圆心点
            int startEuler = (int)Vector2.Angle(Vector2.right, new Vector2(dir.x, dir.z));
            for (int i = startEuler, j = 1; i <= angleDiffer + startEuler; j++, i += 3)
            {
                angle = Mathf.Deg2Rad * i;
                float differ = Mathf.Abs(Mathf.Cos(angle - (float)(0.5 * angleDiffer * Mathf.Deg2Rad)) * egdePoint.y - egdePoint.y);//高度差的绝对值
                points[j] = origin + new Vector3(radius * Mathf.Cos(angle), egdePoint.y + differ, radius * Mathf.Sin(angle));       //给底面点赋值
            }
            return points;
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

            Vector3 startVector_1 = tarPoint - origin;                                    //获取底面中间向量

            float tempHeight = Mathf.Tan(theta * Mathf.Deg2Rad) * startVector_1.magnitude;//扇形区域最边缘高度          
            Vector3 startVector_2 = startVector_1 + Vector3.up * tempHeight;              //获取顶边中间向量

            float radius_1 = (tarPoint - origin).magnitude;                //获得扇形底面半径
            float radius_2 = radius_1 / Mathf.Cos(theta * Mathf.Deg2Rad);  //获得扇形顶面半径

            float tempWidth = radius_1 / Mathf.Cos(halfAlpha);             //圆心点 到底面中间半径线和地面弧边两个端点连接线的交点 的长度

            List<Vector3> dirs_1 = new List<Vector3>();//底边方向向量集合
            List<Vector3> dirs_2 = new List<Vector3>();//顶边方向向量集合 
                                                       //获取扇形弧边所有点的方向向量
            for (int i = -halfAlpha, j = 0; i <= halfAlpha; i += 3, j++)
            {
                //获取下弧边的方向向量
                Vector2 temp = GetTargetVector(new Vector2(startVector_1.x, startVector_1.z), i);
                dirs_1.Add(new Vector3(temp.x, 0, temp.y));

                //获取上弧边的方向向量
                Vector3 targetDir = temp.magnitude / Mathf.Cos(i * Mathf.Deg2Rad) * dirs_1[j].normalized + Vector3.up * tempHeight;
                dirs_2.Add(targetDir);
            }

            //获取扇形所有点
            points.Add(origin);
            for (int i = 0; i < dirs_1.Count; i++)
            {
                points.Add(dirs_1[i].normalized * radius_1 + origin);
            }
            points.Add(origin);
            for (int i = 0; i < dirs_2.Count; i++)
            {
                points.Add(dirs_2[i].normalized * radius_2 + origin);
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
        /// 获取凹多边形平面排序
        /// </summary>
        /// <param name="points"> 关键点集合 </param>
        /// <returns></returns>
        public static List<int> DrawPolygon(List<Vector3> points, bool isUp)
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
            while (indexs.Count > 3)
            {
                List<Vector3> polygon = new List<Vector3>(points.ToArray());
                polygon.RemoveAt(index);

                //是否是凹点
                if (!IsPointInsidePolygon(points[index], polygon))
                {
                    // 是否是可划分顶点:新的多边形没有顶点在分割的三角形内
                    if (IsFragementIndex(index, points))
                    {
                        //可划分，剖分三角形
                        next = (index == indexs.Count - 1) ? 0 : index + 1;
                        prev = (index == 0) ? indexs.Count - 1 : index - 1;
                        if (isUp)
                        {
                            triangles.Add(indexs[index]);
                            triangles.Add(indexs[prev]);
                            triangles.Add(indexs[next]);
                        }
                        else
                        {
                            triangles.Add(indexs[next]);
                            triangles.Add(indexs[prev]);
                            triangles.Add(indexs[index]);
                        }

                        indexs.RemoveAt(index);
                        points.RemoveAt(index);

                        index = (index + indexs.Count - 1) % indexs.Count;       // 防止出现index超出值域

                        continue;
                    }
                }
                index = (index + 1) % indexs.Count;
            }
            next = (index == indexs.Count - 1) ? 0 : index + 1;
            prev = (index == 0) ? indexs.Count - 1 : index - 1;
            if (isUp)
            {
                triangles.Add(indexs[prev]);
                triangles.Add(indexs[next]);
                triangles.Add(indexs[index]);
            }
            else
            {
                triangles.Add(indexs[index]);
                triangles.Add(indexs[next]);
                triangles.Add(indexs[prev]);
            }

            return triangles;
        }

        /// <summary>
        /// 是否是可划分顶点:新的多边形没有顶点在分割的三角形内
        /// </summary>
        public static bool IsFragementIndex(int index, List<Vector3> verts, bool containEdge = true)
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
                    angle = angle - Mathf.PI * 2;
                }
                else if (angle <= -Mathf.PI)
                {
                    angle = angle + Mathf.PI * 2;
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
        public static bool IsPointInsidePolygon001(Vector3 point, List<Vector3> mPoints)
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
        public static bool IsPointInsidePolygon001(Vector3 point, List<Vector3> mPoints, List<Vector3> crossPoints = null)
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
        public static bool IsPointInsidePolygon001(Vector2 point, Vector2[] mPoints, List<Vector2> crossPoints = null)
        {
            int nCross = 0;

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

        /// <summary>
        /// 将一个多边形转化为多个三角形
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static List<Vector3[]> PolygonToTriangles(List<Vector3> points)
        {
            if (points.Count < 3)
            {
                return null;
            }
            List<Vector3[]> triangles = new List<Vector3[]>();
            int index = points.Count - 1;
            int next;
            int prev;

            while (points.Count > 3)
            {
                List<Vector3> polygon = new List<Vector3>(points);
                polygon.RemoveAt(index);

                //是否是凹点
                if (!IsPointInsidePolygon(points[index], polygon, false))
                {
                    // 是否是可划分顶点:新的多边形没有顶点在分割的三角形内
                    if (IsFragementIndex(index, points.ToList(), false))
                    {
                        //可划分，剖分三角形
                        next = (index == points.Count - 1) ? 0 : index + 1;
                        prev = (index == 0) ? points.Count - 1 : index - 1;

                        triangles.Add(new Vector3[]
                        {
                        points[index],
                        points[prev],
                        points[next]
                        });

                        points.RemoveAt(index);

                        index = (index + points.Count - 1) % points.Count;       // 防止出现index超出值域
                        continue;
                    }
                }
                index = (index + 1) % points.Count;
            }
            triangles.Add(new Vector3[] { points[1], points[0], points[2] });

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
                PhysicsMath.IsPointInsidePolygon001(pointTmp, trianglePoints, crossPoints);     // 获取z轴平行线与三角形的交点

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

                IsPointInsidePolygon001(pointTmp, trianglePoints.ToList(), crossPoints);     // 获取z轴平行线与三角形的交点

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

            Vector3 _dir = GetHorizontalDir(_list[0], _list[1]);        // 获取两点之间的垂直向量;
            verticesList.Add(_list[0] + _dir * _width - Vector3.up * _height);       // 右下点
            verticesList.Add(_list[0] + _dir * _width + Vector3.up * _height);       // 
            verticesList.Add(_list[0] - _dir * _width + Vector3.up * _height);       // 
            verticesList.Add(_list[0] - _dir * _width - Vector3.up * _height);       // 左下点

            for (int i = 1; i < count - 1; i++)
            {
                //计算相连三个点夹角的补角的一半的余弦值
                Vector2 pos_1 = new Vector2(_list[i + 1].x - _list[i].x, _list[i + 1].z - _list[i].z);
                Vector2 pos_2 = new Vector2(_list[i].x - _list[i - 1].x, _list[i].z - _list[i - 1].z);
                float theta = Vector2.Angle(pos_1, pos_2) / 2;
                float cosTheta = Mathf.Cos(Mathf.Deg2Rad * theta);

                _dir = GetHorizontalDir(_list[i - 1], _list[i]);        // 获取两点之间的垂直向量
                _dir2 = GetHorizontalDir(_list[i], _list[i + 1]);       // 获取两点之间的垂直向量
                _dir3 = ((_dir + _dir2) / 2).normalized;              // 获取方向向量

                verticesList.Add(_list[i] + _dir3 * _width / cosTheta - Vector3.up * _height);
                verticesList.Add(_list[i] + _dir3 * _width / cosTheta + Vector3.up * _height);
                verticesList.Add(_list[i] - _dir3 * _width / cosTheta + Vector3.up * _height);
                verticesList.Add(_list[i] - _dir3 * _width / cosTheta - Vector3.up * _height);
            }

            _dir = GetHorizontalDir(_list[count - 2], _list[count - 1]);    // 获取两点之间的垂直向量        
            verticesList.Add(_list[count - 1] + _dir * _width - Vector3.up * _height);       // 右下点
            verticesList.Add(_list[count - 1] + _dir * _width + Vector3.up * _height);       // 右上点
            verticesList.Add(_list[count - 1] - _dir * _width + Vector3.up * _height);       // 左上点
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
            _height /= 2;
            _width /= 2;

            Vector3 _dir2 = Vector3.zero;
            Vector3 _dir3 = Vector3.zero;

            Vector3 _dir = GetHorizontalDir(_list[0], _list[1]);        // 获取两点之间的垂直向量;
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

                //计算相连三个点夹角的补角的一半的余弦值
                Vector2 pos_1 = new Vector2(_list[i + 1].x - _list[i].x, _list[i + 1].z - _list[i].z);
                Vector2 pos_2 = new Vector2(_list[i].x - _list[i - 1].x, _list[i].z - _list[i - 1].z);
                float theta = Vector2.Angle(pos_1, pos_2);

                theta /= 2.00f;

                float cosTheta = Mathf.Cos(Mathf.Deg2Rad * theta);

                _dir = GetHorizontalDir(_list[i - 1], _list[i]);        // 获取两点之间的垂直向量
                _dir2 = GetHorizontalDir(_list[i], _list[i + 1]);       // 获取两点之间的垂直向量
                _dir3 = ((_dir + _dir2) / 2).normalized;

                verticesList.Add(_list[i] + _dir3 * _width / cosTheta - Vector3.up * _height);      // 右下点
                tmpList.Add(_list[i] - _dir3 * _width / cosTheta - Vector3.up * _height);           // 左下点

            }


            _dir = GetHorizontalDir(_list[count - 2], _list[count - 1]);    // 获取两点之间的垂直向量        
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
                Vector3 _dir1 = _bottomList[i] - _bottomList[i - 1];        // 获取两点之间的垂直向量
                Vector3 _dir2 = _bottomList[i + 1] - _bottomList[i];        // 获取两点之间的垂直向量

                //是否是凹点
                if (IsPointInsidePolygon(_bottomList[i], polygon))
                {
                    Vector2 _dirV2 = GetTargetVector(new Vector2(_dir1.x, _dir1.z), 90);
                    _dir1 = new Vector3(_dirV2.x, 0, _dirV2.y);    // 获取左弧边向量
                    _dirV2 = GetTargetVector(new Vector2(_dir2.x, _dir2.z), 90);
                    _dir2 = new Vector3(_dirV2.x, 0, _dirV2.y);    // 获取右弧边向量

                    arcList.Add(_bottomList[i]);                                                                // 添加扇心

                    //加上0.05中和浮点数精度问题
                    for (float j = 0; j <= 1.05f; j += 0.1f)
                    {
                        arcList.Add(Vector3.Slerp(_dir1, _dir2, j).normalized * _width + _bottomList[i]);       // 添加进圆弧点集
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
                    Vector2 _dirV2 = GetTargetVector(new Vector2(_dir1.x, _dir1.z), -90);
                    _dir1 = new Vector3(_dirV2.x, 0, _dirV2.y);    // 获取左弧边向量
                    _dirV2 = GetTargetVector(new Vector2(_dir2.x, _dir2.z), -90);
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
        /// 计算多边形面积(忽略y轴)
        /// </summary>
        /// <param name="points"></param>
        /// <returns>平方米</returns>
        public static float ComputePolygonArea(List<Vector3> points)
        {
            float iArea = 0;

            for (int iCycle = 0, iCount = points.Count; iCycle < iCount; iCycle++)
            {
                iArea += (points[iCycle].x * points[(iCycle + 1) % iCount].z - points[(iCycle + 1) % iCount].x * points[iCycle].z);
            }

            return (float)Math.Abs(0.5 * iArea);
        }

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
            if (IsApproximately(rxs, 0f))
            {
                if (IsApproximately(pqxr, 0f))
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

        /// <summary>
        /// 计算两个数字是否接近相等,阈值是dvalue
        /// </summary>
        public static bool IsApproximately(double a, double b, double dvalue = 0)
        {
            double delta = a - b;
            return delta >= -dvalue && delta <= dvalue;
        }
    }
}