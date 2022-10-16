using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 圆
    /// </summary>
    public struct Circle : IPlane
    {
        /// <summary>
        /// 圆心点
        /// </summary>
        public Vector3 center;
        /// <summary>
        /// 半径
        /// </summary>
        public float radius;
        /// <summary>
        /// 角度
        /// </summary>
        public Vector3 angle;
        /// <summary>
        /// 法线
        /// </summary>
        private Vector3 m_normal;
        /// <summary>
        /// 法线
        /// </summary>
        public Vector3 normal
        {
            get
            {
                return m_normal;
            }
            set
            {
                normal = value.normalized;
            }
        }

        /// <summary>
        /// 一个与法向量normal垂直的、在圆平面内的向量
        /// </summary>
        public Vector3 U
        {
            get
            {
                return new Vector3(normal.y, -normal.x, 0).normalized;
            }
        }

        /// <summary>
        /// 一个与法向量normal和U都垂直，在圆平面内的向量
        /// </summary>
        public Vector3 V
        {
            get
            {
                return Vector3.Cross(normal, U);
            }
        }

        /// <summary>
        /// 面积
        /// </summary>
        public float Area
        {
            get
            {
                return Mathf.PI * radius * radius;
            }
        }

        /// <summary>
        /// 获取圆上的点
        /// </summary>
        /// <param name="count">点的数量</param>
        /// <returns></returns>
        public Vector3[] GetBound(int count)
        {
            if (count < 0)
            {
                throw new System.Exception($"Count不能小于0  count:{count}");
            }

            Vector3[] points = new Vector3[count];

            var u = U;
            var v = V;

            float subRad = 2 * Mathf.PI;
            for (int i = 0; i < count; i++)
            {
                float rad = subRad * i;
                points[i].x = center.x + radius * (u.x * Mathf.Cos(rad) + v.x * Mathf.Sin(rad));
                points[i].y = center.y + radius * (u.y * Mathf.Cos(rad) + v.y * Mathf.Sin(rad));
                points[i].z = center.z + radius * (u.z * Mathf.Cos(rad) + v.z * Mathf.Sin(rad));
            }

            return points;
        }

        public SR R(Point point)
        {
            throw new System.NotImplementedException();
        }

        public SR R(MulitLineSegment point)
        {
            throw new System.NotImplementedException();
        }
    }
}