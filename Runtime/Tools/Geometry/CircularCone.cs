using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 圆锥体
    /// </summary>
    public struct CircularCone : IVolume
    {
        /// <summary>
        /// 圆锥顶点
        /// </summary>
        public Vector3 peak;
        /// <summary>
        /// 旋转角
        /// </summary>
        public Quaternion rotation;
        /// <summary>
        /// 圆锥张角
        /// </summary>
        public float fieldAngle;
        /// <summary>
        /// 高
        /// </summary>
        public float height;

        public CircularCone(Vector3 peak, Quaternion rotation, float fieldAngle, float height)
        {
            this.peak = peak;
            this.rotation = rotation;
            this.fieldAngle = fieldAngle;
            this.height = height;
        }

        /// <summary>
        /// 底面圆心点
        /// </summary>
        public Vector3 center
        {
            get
            {
                var v = peak;
                v.y -= height;
                return v;
            }
        }
        /// <summary>
        /// 边长
        /// </summary>
        public float sideLength
        {
            get
            {
                return height / Mathf.Cos(fieldAngle / 2);
            }
            set
            {
                height = value * Mathf.Cos(fieldAngle / 2);
            }
        }
        /// <summary>
        /// 底面半径
        /// </summary>
        public float radius
        {
            get
            {
                return Mathf.Tan(fieldRedAngle / 2) * height;
            }
        }
        /// <summary>
        /// 圆锥张角弧度值
        /// </summary>
        public float fieldRedAngle
        {
            get
            {
                return Mathf.Deg2Rad * fieldAngle;
            }
            set
            {
                fieldAngle = value * Mathf.Rad2Deg;
            }
        }
        /// <summary>
        /// 体积
        /// </summary>
        public float Volume
        {
            get
            {
                return 1 / 3f * Mathf.PI * radius * radius * height;
            }
        }

        /// <summary>
        /// 获取直线和圆锥的交点
        /// </summary>
        /// <param name="line">直线</param>
        /// <param name="_p1">交点1</param>
        /// <param name="_p2">交点2</param>
        /// <returns>交点数量</returns>
        public int TryGetIntersectionPoints(Line line, out Vector3 _p1, out Vector3 _p2)
        {
            Matrix4x4 matrix4X4 = Matrix4x4.TRS(peak, rotation, Vector3.one).inverse; // 旋转矩阵

            int count; // 交点数量

            Vector3 p1 = line.point;
            Vector3 p2 = line.GetPoint(10); // 随便再取一个直线上的点
            p1 = matrix4X4.MultiplyPoint3x4(p1);
            p2 = matrix4X4.MultiplyPoint3x4(p2);

            // 交点1，2
            _p1 = default;
            _p2 = default;

            // start 联立以下由直线和圆锥表面的参数方程公式综合计算
            // 直线两点式     (x - x1)/(x2 - x1) = (y - y1)/(y2 - y1) = (z - z1)/(z2 - z1) = k
            // 圆锥面参数方程  x = r * cost, y = r / tanθ，  在 z = r * sint
            // 最后联立求k
            float a1 = p2.x - p1.x, b1 = p1.x;
            float a2 = p2.y - p1.y, b2 = p1.y;
            float a3 = p2.z - p1.z, b3 = p1.z;
            float tanθ = Mathf.Tan(0.5f * fieldAngle * Mathf.Deg2Rad);
            float a = a1 * a1 + a3 * a3 - tanθ * tanθ * a2 * a2;
            float b = 2 * a1 * b1 + 2 * a3 * b3 - 2 * tanθ * tanθ * a2 * b2;
            float c = b1 * b1 + b3 * b3 - tanθ * tanθ * b2 * b2;
            float Δ = b * b - 4 * a * c;
            if (Δ < 0)
            {
                count = 0;
            }
            else if (Δ == 0)
            {
                float k = (-b + Mathf.Sqrt(Δ)) / (2 * a);
                _p1 = new Vector3(k * a1 + b1, k * a2 + b2, k * a3 + b3);
                count = 1;
            }
            else
            {
                float k1 = (-b + Mathf.Sqrt(Δ)) / (2 * a), k2 = (-b - Mathf.Sqrt(Δ)) / (2 * a);
                _p1 = new Vector3(k1 * a1 + b1, k1 * a2 + b2, k1 * a3 + b3);
                _p2 = new Vector3(k2 * a1 + b1, k2 * a2 + b2, k2 * a3 + b3);
                count = 2;
            }
            // end

            if (count == 0)
            {
                return 0;
            }
            else
            {
                Matrix4x4 inverse = Matrix4x4.Inverse(matrix4X4);
                if (count == 1)
                {
                    if (p1.y == p2.y || (p1.x != p2.x && p1.z != p2.z))
                    {
                        _p1 = inverse.MultiplyPoint3x4(_p1);
                        return 1;
                    }
                    else
                    {
                        _p2 = _p1;
                        _p2.y += height;
                        _p2 = inverse.MultiplyPoint3x4(_p2);
                        return 2;
                    }
                }
                else // count = 2
                {
                    if(_p1.y < 0 && _p2.y < 0)
                    {
                        return 0;
                    }
                    //else if (_p2.y < 0)
                    //{
                    //    _p2 = _p1;
                    //    _p2.y += height;
                    //}
                    //else if(_p1.y < 0)
                    //{
                    //    _p1 = _p2;

                    //    _p2 = _p1;
                    //    _p2.y += height;
                    //}

                    _p1 = inverse.MultiplyPoint3x4(_p1);
                    _p2 = inverse.MultiplyPoint3x4(_p2);
                    return 2;
                }
            }
        }

        /// <summary>
        /// 获取一条线段在圆锥内部的长度
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        public float GetDistanceIn(Vector3 p1, Vector3 p2)
        {
            return GetDistanceIn(p1, p2, out _, out _);
        }

        /// <summary>
        /// 获取一条线段在圆锥内部的长度
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        public float GetDistanceIn(Vector3 p1, Vector3 p2, out Vector3 validP1, out Vector3 validP2)
        {
            Line line = new Line(p1, p2 - p1);
            int count = TryGetIntersectionPoints(line, out Vector3 _p1, out Vector3 _p2);
            if (count != 2)
            {
                validP1 = validP2 = Vector3.zero;
                return 0;
            }
            else
            {
                validP1 = GetValidPoint(_p1, _p2, p1);
                validP2 = GetValidPoint(_p1, _p2, p2);

                return Vector3.Distance(validP1, validP2);
            }

            // point_1,point_2组成一条线段，point如果在线段内返回自身，否则返回离自己近的线段端点
            Vector3 GetValidPoint(Vector3 point_1, Vector3 point_2, Vector3 point)
            {
                Vector3 dir1 = point_1 - point;
                Vector3 dir2 = point_2 - point;
                if (dir1.normalized == dir2.normalized) // 点在两交点组成的线段外
                {
                    return Vector3.Distance(point, point_1) < Vector3.Distance(point, point_2) ? point_1 : point_2;
                }
                else // 点在两交点组成的线段内
                {
                    return point;
                }
            }
        }
    }
}