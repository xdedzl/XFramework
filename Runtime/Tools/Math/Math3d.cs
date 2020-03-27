using System;
using UnityEngine;

namespace XFramework.Mathematics
{
    /// <summary>
    /// 有关3D数学的计算
    /// 有关法向量的计算要求法向量为单位向量
    /// </summary>
    public static class Math3d
    {
        #region 点线面关系

        /// <summary>
        /// 两个面是否相交
        /// </summary>
        /// <param name="linePoint">交线上一点</param>
        /// <param name="lineVec">交线的方向</param>
        /// <param name="face_1">平面1法线</param>
        /// <param name="face_2">平面1上一点</param>
        /// <returns>是否相交</returns>
        public static bool PlanePlaneIntersection(Face face_1, Face face_2, out Line line)
        {
            Vector3 lineVec = Vector3.Cross(face_1.normal, face_2.normal);
            Vector3 vector = Vector3.Cross(face_2.normal, lineVec);
            float num = Vector3.Dot(face_1.normal, vector);
            if (Mathf.Abs(num) > 0.006f)
            {
                Vector3 rhs = face_1.point - face_2.point;
                float d = Vector3.Dot(face_1.normal, rhs) / num;
                Vector3 linePoint = face_2.point + d * vector;
                line.point = linePoint;
                line.direction = lineVec;
                return true;
            }
            line = default;
            return false;
        }

        /// <summary>
        /// 线面是否相交
        /// </summary>
        /// <param name="intersection">交点</param>
        /// <param name="linePoint"></param>
        /// <param name="lineVec"></param>
        /// <param name="face"></param>
        /// <returns>是否相交</returns>
        public static bool LinePlaneIntersection(Vector3 linePoint, Vector3 lineVec, Face face, out Vector3 intersection)
        {
            intersection = Vector3.zero;
            float num = Vector3.Dot(face.point - linePoint, face.normal);
            float num2 = Vector3.Dot(lineVec, face.normal);
            if (num2 != 0f)
            {
                float size = num / num2;
                Vector3 b = Math3d.SetVectorLength(lineVec, size);
                intersection = linePoint + b;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 判断线与线之间的相交
        /// </summary>
        /// <param name="intersection">交点</param>
        /// <param name="p1">直线1上一点</param>
        /// <param name="v1">直线1方向</param>
        /// <param name="p2">直线2上一点</param>
        /// <param name="v2">直线2方向</param>
        /// <returns>是否相交</returns>
        public static bool LineLineIntersection(Vector3 p1, Vector3 v1, Vector3 p2, Vector3 v2, out Vector3 intersection)
        {
            intersection = Vector3.zero;
            if (Vector3.Dot(v1, v2) == 1)
            {
                // 两线平行
                return false;
            }

            Vector3 startPointSeg = p2 - p1;
            Vector3 vecS1 = Vector3.Cross(v1, v2);            // 有向面积1
            Vector3 vecS2 = Vector3.Cross(startPointSeg, v2); // 有向面积2
            float num = Vector3.Dot(startPointSeg, vecS1);

            // 用于在场景中观察向量
            //Debug.DrawLine(p1, p1 + v1, Color.white, 20000);
            //Debug.DrawLine(p2, p2 + v2, Color.black, 20000);

            //Debug.DrawLine(p1, p1 + startPointSeg, Color.red, 20000);
            //Debug.DrawLine(p1, p1 + vecS1, Color.blue, 20000);
            //Debug.DrawLine(p1, p1 + vecS2, Color.yellow, 20000);

            // 判断两这直线是否共面
            if (num >= 1E-05f || num <= -1E-05f)
            {
                return false;
            }

            // 有向面积比值，利用点乘是因为结果可能是正数或者负数
            float num2 = Vector3.Dot(vecS2, vecS1) / vecS1.sqrMagnitude;

            intersection = p1 + v1 * num2;
            return true;
        }

        /// <summary>
        /// 计算两直线的起点分别到交点的有向距离
        /// </summary>
        public static bool LineLineIntersection(Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2, out float tLine1, out float tLine2)
        {
            tLine1 = float.PositiveInfinity;
            tLine2 = float.PositiveInfinity;
            Vector3 lhs = linePoint2 - linePoint1;
            Vector3 rhs = Vector3.Cross(lineVec1, lineVec2);
            Vector3 lhs2 = Vector3.Cross(lhs, lineVec2);
            Vector3 lhs3 = Vector3.Cross(lhs, lineVec1);
            float num = Vector3.Dot(lhs, rhs);
            if (num >= 1E-05f || num <= -1E-05f)
            {
                return false;
            }
            tLine1 = Vector3.Dot(lhs2, rhs) / rhs.sqrMagnitude;
            tLine2 = Vector3.Dot(lhs3, rhs) / rhs.sqrMagnitude;
            return true;
        }

        /// <summary>
        /// 三维空间中两条直线两个最近的点
        /// </summary>
        /// <param name="closestPointLine1"></param>
        /// <param name="closestPointLine2"></param>
        /// <param name="linePoint1"></param>
        /// <param name="lineVec1"></param>
        /// <param name="linePoint2"></param>
        /// <param name="lineVec2"></param>
        /// <returns></returns>
        public static bool ClosestPointsOnTwoLines(Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2, out Vector3 closestPointLine1, out Vector3 closestPointLine2)
        {
            closestPointLine1 = Vector3.zero;
            closestPointLine2 = Vector3.zero;
            float num = Vector3.Dot(lineVec1, lineVec1);
            float num2 = Vector3.Dot(lineVec1, lineVec2);
            float num3 = Vector3.Dot(lineVec2, lineVec2);
            float num4 = num * num3 - num2 * num2;
            if (num4 != 0f)
            {
                Vector3 rhs = linePoint1 - linePoint2;
                float num5 = Vector3.Dot(lineVec1, rhs);
                float num6 = Vector3.Dot(lineVec2, rhs);
                float d = (num2 * num6 - num5 * num3) / num4;
                float d2 = (num * num6 - num5 * num2) / num4;
                closestPointLine1 = linePoint1 + lineVec1 * d;
                closestPointLine2 = linePoint2 + lineVec2 * d2;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 三维空间中两条直线两个最近的点
        /// </summary>
        /// <param name="s"></param>
        /// <param name="t"></param>
        /// <param name="linePoint1"></param>
        /// <param name="lineVec1"></param>
        /// <param name="linePoint2"></param>
        /// <param name="lineVec2"></param>
        /// <returns></returns>
        public static bool ClosestPointsOnTwoLines(Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2, out float s, out float t)
        {
            t = (s = 0f);
            float num = Vector3.Dot(lineVec1, lineVec1);
            float num2 = Vector3.Dot(lineVec1, lineVec2);
            float num3 = Vector3.Dot(lineVec2, lineVec2);
            float num4 = num * num3 - num2 * num2;
            if (num4 != 0f)
            {
                Vector3 rhs = linePoint1 - linePoint2;
                float num5 = Vector3.Dot(lineVec1, rhs);
                float num6 = Vector3.Dot(lineVec2, rhs);
                s = (num2 * num6 - num5 * num3) / num4;
                t = (num * num6 - num5 * num2) / num4;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 两个线段最近的两个点
        /// </summary>
        /// <param name="closestPointLine"></param>
        /// <param name="closestPointSegment"></param>
        /// <param name="lineT"></param>
        /// <param name="segmentT"></param>
        /// <param name="linePoint"></param>
        /// <param name="lineVec"></param>
        /// <param name="segmentPoint1"></param>
        /// <param name="segmentPoint2"></param>
        /// <returns></returns>
        public static bool ClosestPointsOnLineSegment(Vector3 linePoint, Vector3 lineVec, Vector3 segmentPoint1, Vector3 segmentPoint2, out Vector3 closestPointLine, out Vector3 closestPointSegment, out float lineT, out float segmentT)
        {
            Vector3 vector = segmentPoint2 - segmentPoint1;
            closestPointLine = Vector3.zero;
            closestPointSegment = Vector3.zero;
            segmentT = 0f;
            lineT = 0f;
            float num = Vector3.Dot(lineVec, lineVec);
            float num2 = Vector3.Dot(lineVec, vector);
            float num3 = Vector3.Dot(vector, vector);
            float num4 = num * num3 - num2 * num2;
            if (num4 != 0f)
            {
                Vector3 rhs = linePoint - segmentPoint1;
                float num5 = Vector3.Dot(lineVec, rhs);
                float num6 = Vector3.Dot(vector, rhs);
                float num7 = (num2 * num6 - num5 * num3) / num4;
                float value = (num * num6 - num5 * num2) / num4;
                lineT = num7;
                segmentT = Mathf.Clamp01(value);
                closestPointLine = linePoint + lineVec * num7;
                closestPointSegment = segmentPoint1 + vector * segmentT;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 一个点在一条直线上的投影点
        /// </summary>
        /// <param name="linePoint"></param>
        /// <param name="lineVec"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Vector3 ProjectPointOnLine(Vector3 linePoint, Vector3 lineVec, Vector3 point)
        {
            Vector3 lhs = point - linePoint;
            float d = Vector3.Dot(lhs, lineVec);
            return linePoint + lineVec * d;
        }

        /// <summary>
        ///  一个点在一条线段上的投影点
        /// </summary>
        /// <param name="linePoint1"></param>
        /// <param name="linePoint2"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Vector3 ProjectPointOnLineSegment(Vector3 linePoint1, Vector3 linePoint2, Vector3 point)
        {
            Vector3 vector = Math3d.ProjectPointOnLine(linePoint1, (linePoint2 - linePoint1).normalized, point);
            int num = Math3d.PointOnWhichSideOfLineSegment(linePoint1, linePoint2, vector);
            if (num == 0)
            {
                return vector;
            }
            if (num == 1)
            {
                return linePoint1;
            }
            if (num == 2)
            {
                return linePoint2;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// 空间中一个点在一个面上的投影点
        /// </summary>
        /// <param name="face"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Vector3 ProjectPointOnPlane(Face face, Vector3 point)
        {
            float num = Math3d.SignedDistancePlanePoint(face, point);
            num *= -1f;
            Vector3 b = Math3d.SetVectorLength(face.normal, num);
            return point + b;
        }

        /// <summary>
        /// 一个向量在一个面上的投影向量
        /// </summary>
        /// <param name="planeNormal"></param>
        /// <param name="vector"></param>
        /// <returns></returns>
        public static Vector3 ProjectVectorOnPlane(Vector3 planeNormal, Vector3 vector)
        {
            return vector - Vector3.Dot(vector, planeNormal) * planeNormal;
        }

        /// <summary>
        /// 点到面的距离
        /// </summary>
        /// <param name="face"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static float SignedDistancePlanePoint(Face face, Vector3 point)
        {
            return Vector3.Dot(face.normal, point - face.point);
        }

        // Token: 0x060019FB RID: 6651 RVA: 0x000BA4A4 File Offset: 0x000B86A4
        public static float SignedDotProduct(Vector3 vectorA, Vector3 vectorB, Vector3 normal)
        {
            Vector3 lhs = Vector3.Cross(normal, vectorA);
            return Vector3.Dot(lhs, vectorB);
        }

        /// <summary>
        /// 一个向量和平面的夹角
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static float AngleVectorPlane(Vector3 vector, Vector3 normal)
        {
            float num = Vector3.Dot(vector, normal);
            float num2 = (float)Math.Acos((double)num);
            return 1.57079637f - num2;


            // mine
            //float ComplementaryAngle = (float)Math.Acos(Vector3.Dot(vector, normal) / vector.magnitude);
            //return (float)(Math.PI / 2 - ComplementaryAngle); 
        }

        // Token: 0x060019FE RID: 6654 RVA: 0x000BA51C File Offset: 0x000B871C
        public static float DotProductAngle(Vector3 vec1, Vector3 vec2)
        {
            double num = (double)Vector3.Dot(vec1, vec2);
            if (num < -1.0)
            {
                num = -1.0;
            }
            if (num > 1.0)
            {
                num = 1.0;
            }
            double num2 = Math.Acos(num);
            return (float)num2;
        }

        /// <summary>
        /// 三个点构造一个平面
        /// </summary>
        public static Face PlaneFrom3Points(Vector3 pointA, Vector3 pointB, Vector3 pointC)
        {
            Vector3 vector = pointB - pointA;
            Vector3 vector2 = pointC - pointA;
            Vector3 planeNormal = Vector3.Normalize(Vector3.Cross(vector, vector2));
            Vector3 vector3 = pointA + vector / 2f;
            Vector3 vector4 = pointA + vector2 / 2f;
            Vector3 lineVec = pointC - vector3;
            Vector3 lineVec2 = pointB - vector4;
            Math3d.ClosestPointsOnTwoLines(vector3, lineVec, vector4, lineVec2, out Vector3 planePoint, out Vector3 vector5);
            return new Face(planePoint, planeNormal);
        }

        /// <summary>
        /// 判断一个点在一个线段的哪一边
        /// </summary>
        /// <param name="linePoint1">线段起点</param>
        /// <param name="linePoint2">线段终点</param>
        /// <param name="point">点</param>
        /// <returns>0：线段两点之间， 1：线段外，更靠近p1，2：线段外，更靠近p2</returns>
        public static int PointOnWhichSideOfLineSegment(Vector3 linePoint1, Vector3 linePoint2, Vector3 point)
        {
            Vector3 lineDir = linePoint2 - linePoint1;
            Vector3 lhs = point - linePoint1;
            float num = Vector3.Dot(lhs, lineDir);
            if (num <= 0f)
            {
                return 1;
            }
            if (lhs.magnitude <= lineDir.magnitude)
            {
                return 0;
            }
            return 2;
        }

        /// <summary>
        /// 鼠标位置到一条线段的距离
        /// </summary>
        public static float MouseDistanceToLine(Vector3 linePoint1, Vector3 linePoint2)
        {
            Camera main = Camera.main;
            Vector3 mousePosition = Input.mousePosition;
            Vector3 linePoint3 = main.WorldToScreenPoint(linePoint1);
            Vector3 linePoint4 = main.WorldToScreenPoint(linePoint2);
            Vector3 a = Math3d.ProjectPointOnLineSegment(linePoint3, linePoint4, mousePosition);
            a = new Vector3(a.x, a.y, 0f);
            return (a - mousePosition).magnitude;
        }

        /// <summary>
        /// 鼠标位置和圆心位置的距离
        /// </summary>
        /// <param name="point">圆心点</param>
        /// <param name="radius">半径</param>
        public static float MouseDistanceToCircle(Vector3 point, float radius)
        {
            Camera main = Camera.main;
            Vector3 mousePosition = Input.mousePosition;
            Vector3 a = main.WorldToScreenPoint(point);
            a = new Vector3(a.x, a.y, 0f);
            float magnitude = (a - mousePosition).magnitude;
            return magnitude - radius;
        }

        /// <summary>
        /// 判断线段是否在矩形内
        /// </summary>
        public static bool IsLineInRectangle(Vector3 linePoint1, Vector3 linePoint2, Vector3 rectA, Vector3 rectB, Vector3 rectC, Vector3 rectD)
        {
            bool flag = false;
            bool flag2 = Math3d.IsPointInRectangle(linePoint1, rectA, rectC, rectB, rectD);
            if (!flag2)
            {
                flag = Math3d.IsPointInRectangle(linePoint2, rectA, rectC, rectB, rectD);
            }
            if (!flag2 && !flag)
            {
                bool flag3 = Math3d.AreLineSegmentsCrossing(linePoint1, linePoint2, rectA, rectB);
                bool flag4 = Math3d.AreLineSegmentsCrossing(linePoint1, linePoint2, rectB, rectC);
                bool flag5 = Math3d.AreLineSegmentsCrossing(linePoint1, linePoint2, rectC, rectD);
                bool flag6 = Math3d.AreLineSegmentsCrossing(linePoint1, linePoint2, rectD, rectA);
                return flag3 || flag4 || flag5 || flag6;
            }
            return true;
        }

        /// <summary>
        /// 判断点point是否在矩形内
        /// </summary>
        public static bool IsPointInRectangle(Vector3 point, Vector3 rectA, Vector3 rectC, Vector3 rectB, Vector3 rectD)
        {
            Vector3 vector = rectC - rectA;
            float size = -(vector.magnitude / 2f);
            vector = Math3d.AddVectorLength(vector, size);
            Vector3 linePoint = rectA + vector;
            Vector3 vector2 = rectB - rectA;
            float num = vector2.magnitude / 2f;
            Vector3 vector3 = rectD - rectA;
            float num2 = vector3.magnitude / 2f;
            Vector3 a = Math3d.ProjectPointOnLine(linePoint, vector2.normalized, point);
            float magnitude = (a - point).magnitude;
            a = Math3d.ProjectPointOnLine(linePoint, vector3.normalized, point);
            float magnitude2 = (a - point).magnitude;
            return magnitude2 <= num && magnitude <= num2;
        }

        // Token: 0x06001A0D RID: 6669 RVA: 0x000BA974 File Offset: 0x000B8B74
        public static bool AreLineSegmentsCrossing(Vector3 pointA1, Vector3 pointA2, Vector3 pointB1, Vector3 pointB2)
        {
            Vector3 vector = pointA2 - pointA1;
            Vector3 vector2 = pointB2 - pointB1;
            bool flag = Math3d.ClosestPointsOnTwoLines(pointA1, vector.normalized, pointB1, vector2.normalized, out Vector3 point, out Vector3 point2);
            if (flag)
            {
                int num = Math3d.PointOnWhichSideOfLineSegment(pointA1, pointA2, point);
                int num2 = Math3d.PointOnWhichSideOfLineSegment(pointB1, pointB2, point2);
                return num == 0 && num2 == 0;
            }
            return false;
        }

        /// <summary>
        /// 求入射方向的反射方向
        /// </summary>
        /// <param name="v1">入射方向</param>
        /// <param name="n">法向量</param>
        /// <returns>反射方向</returns>
        public static Vector3 GetReflectedDir(Vector3 v1, Vector3 n)
        {
            return v1 - 2 * Vector3.Dot(v1, n) * n;
        }

        #endregion

        #region 方向,旋转相关

        /// <summary>
        /// 给vector的长度加上size
        /// </summary>
        public static Vector3 AddVectorLength(Vector3 vector, float size)
        {
            float num = Vector3.Magnitude(vector);
            num += size;
            Vector3 a = Vector3.Normalize(vector);
            return Vector3.Scale(a, new Vector3(num, num, num));
        }

        /// <summary>
        /// 将vector的长度设为size
        /// </summary>
        public static Vector3 SetVectorLength(Vector3 vector, float size)
        {
            Vector3 a = Vector3.Normalize(vector);
            return a * size;
        }

        /// <summary>
        /// 获取一个四元数对应的方向
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        public static Vector3 GetForwardVector(Quaternion q)
        {
            return q * Vector3.forward;
        }

        public static Vector3 GetUpVector(Quaternion q)
        {
            return q * Vector3.up;
        }

        public static Vector3 GetRightVector(Quaternion q)
        {
            return q * Vector3.right;
        }

        public static Vector3 GetLeftVector(Quaternion q)
        {
            return q * Vector3.left;
        }

        /// <summary>
        /// 通过矩阵获取一个四元数
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
        {
            // GetColum是从矩阵中取出第i行构造一个Vector4
            return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
        }

        /// <summary>
        /// 通过矩阵获取一个位置
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static Vector3 PositionFromMatrix(Matrix4x4 m)
        {
            Vector4 column = m.GetColumn(3);
            return new Vector3(column.x, column.y, column.z);
        }

        // Token: 0x06001A05 RID: 6661 RVA: 0x000BA688 File Offset: 0x000B8888
        public static void LookRotationExtended(ref GameObject gameObjectInOut, Vector3 alignWithVector, Vector3 alignWithNormal, Vector3 customForward, Vector3 customUp)
        {
            Quaternion lhs = Quaternion.LookRotation(alignWithVector, alignWithNormal);
            Quaternion rotation = Quaternion.LookRotation(customForward, customUp);
            gameObjectInOut.transform.rotation = lhs * Quaternion.Inverse(rotation);
        }

        // Token: 0x06001A06 RID: 6662 RVA: 0x000BA6C0 File Offset: 0x000B88C0
        public static void PreciseAlign(ref GameObject gameObjectInOut, Vector3 alignWithVector, Vector3 alignWithNormal, Vector3 alignWithPosition, Vector3 triangleForward, Vector3 triangleNormal, Vector3 trianglePosition)
        {
            Math3d.LookRotationExtended(ref gameObjectInOut, alignWithVector, alignWithNormal, triangleForward, triangleNormal);
            Vector3 b = gameObjectInOut.transform.TransformPoint(trianglePosition);
            Vector3 translation = alignWithPosition - b;
            gameObjectInOut.transform.Translate(translation, Space.World);
        }

        #endregion
    }

    #region Other Class

    /// <summary>
    /// 表示一个平面
    /// </summary>
    public struct Face
    {
        public Vector3 point;   // 平面上一点
        public Vector3 normal;  // 平面的法线 

        public Face(Vector3 _point, Vector3 _normal)
        {
            point = _point;
            normal = _normal;
        }
    }

    public struct Line
    {
        public Vector3 point;   // 直线上一点
        public Vector3 direction;     // 直线的方向

        public Line(Vector3 startPoint, Vector3 endPointOrVec, CreatType creatType = CreatType.TwoPoint)
        {
            point = startPoint;
            if (creatType == CreatType.TwoPoint)
                direction = endPointOrVec - startPoint;
            else
                direction = endPointOrVec;
        }

        public enum CreatType
        {
            OnePoint,
            TwoPoint,
        }
    }

    #endregion

    /// <summary>
    /// 坐标系
    /// </summary>
    public struct GameCoordinate
    {
        /// <summary>
        /// 坐标原点在世界中的位置
        /// </summary>
        public Vector2 origin;
        /// <summary>
        /// 和世界坐标系的夹角(y轴向x轴方向)
        /// </summary>
        private readonly float theta;

        #region 构造函数

        public GameCoordinate(Vector2 _origin, float _theta)
        {
            origin = _origin;
            theta = _theta;
        }

        public GameCoordinate(Vector3 _origin, Vector3 dir)
        {
            origin = new Vector2(_origin.x, _origin.z);
            theta = Mathf.Atan(-dir.z / dir.x);
        }

        public GameCoordinate(Vector2 _origin, Vector2 dir)
        {
            origin = _origin;
            theta = Mathf.Atan(-dir.y / dir.x);
        }

        public GameCoordinate(Vector3 _origin, float _theta)
        {
            origin = new Vector2(_origin.x, _origin.z);
            theta = _theta;
        }

        #endregion

        #region 世界坐标转本地坐标

        public Vector2 World2Loacal(Vector2 pos)
        {
            Vector2 rel = pos - origin;
            float x = rel.x * Mathf.Cos(theta) - rel.y * Mathf.Sin(theta);
            float y = rel.x * Mathf.Sin(theta) + rel.y * Mathf.Cos(theta);
            return new Vector2(x, y);
        }

        public Vector2 World2Loacal(Vector3 pos)
        {
            return World2Loacal(new Vector2(pos.x, pos.z));
        }

        public Vector3 World2Loacal3(Vector2 pos)
        {
            Vector2 vec = World2Loacal(pos);
            return new Vector3(vec.x, 0, vec.y);
        }

        public Vector3 World2Loacal3(Vector3 pos)
        {
            return World2Loacal3(new Vector2(pos.x, pos.z));
        }

        #endregion

        #region 本地坐标转世界坐标
        public Vector2 Local2World(Vector2 pos)
        {
            float x = pos.x * Mathf.Cos(theta) + pos.y * Mathf.Sin(theta) + origin.x;
            float y = pos.y * Mathf.Cos(theta) - pos.x * Mathf.Sin(theta) + origin.y;
            return new Vector2(x, y);
        }

        public Vector2 Local2World(Vector3 pos)
        {
            return Local2World(new Vector2(pos.x, pos.z));
        }

        public Vector3 Local2World3(Vector2 pos)
        {
            Vector2 vec = Local2World(pos);
            return new Vector3(vec.x, 0, vec.y);
        }

        public Vector3 Local2World3(Vector3 pos)
        {
            return Local2World3(new Vector2(pos.x, pos.z));
        }

        #endregion
    }
}