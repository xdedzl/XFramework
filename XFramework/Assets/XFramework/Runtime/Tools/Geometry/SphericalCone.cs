using System;
using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 球顶锥体
    /// 这里是特殊的球顶锥体，圆锥面的顶点与球面的球心重合
    /// </summary>
    public struct SphericalCone : IVolume
    {
        /// <summary>
        /// 圆锥顶点,球心点
        /// </summary>
        public Vector3 center;
        /// <summary>
        /// 球半径
        /// </summary>
        public float radius;
        /// <summary>
        /// 展开角度
        /// </summary>
        public float fieldAngle;
        /// <summary>
        /// 体积
        /// </summary>
        public float Volume
        {
            get
            {
                return 0;
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
        /// 圆锥面半径
        /// </summary>
        public float circularRadius
        {
            get
            {
                return Mathf.Sin(fieldRedAngle / 2) * radius;
            }
        }
        /// <summary>
        /// 圆锥部分的高度
        /// </summary>
        public float Height
        {
            get
            {
                return radius * Mathf.Cos(fieldRedAngle / 2);
            }
        }
    }
}