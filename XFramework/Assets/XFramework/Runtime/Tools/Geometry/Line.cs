using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 直线
    /// </summary>
    public struct Line
    {
        /// <summary>
        /// 直线上一点 
        /// </summary>
        public Vector3 point;
        /// <summary>
        /// 直线方向（方向取反对直线无影响）
        /// </summary>
        private Vector3 m_Direction;

        public Vector3 direction
        {
            get
            {
                return m_Direction;
            }
            set
            {
                m_Direction = value.normalized;
            }
        }

        public Line(Vector3 point, Vector3 direction)
        {
            this.point = point;
            m_Direction = direction.normalized;
        }

        public Vector3 GetPoint(float distance)
        {
            return point + m_Direction * distance;
        }
    }
}