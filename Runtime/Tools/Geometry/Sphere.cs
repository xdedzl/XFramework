using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 球
    /// </summary>
    public struct Sphere : IVolume
    {
        /// <summary>
        /// 球心
        /// </summary>
        public Vector3 center;
        /// <summary>
        /// 半径
        /// </summary>
        public float radius;

        /// <summary>
        /// 体积
        /// </summary>
        public float Volume
        {
            get
            {
                return 4f / 3f * Mathf.PI * radius * radius * radius;
            }
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