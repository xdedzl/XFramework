using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Geometry
{
    [Serializable]
    public struct Point : ISpace
    {
        public Vector3 value;

        /// <summary>
        /// 两点之间的距离
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <returns></returns>
        public static float Distance(Point point1, Point point2)
        {
            return Vector3.Distance(point1.value, point2.value);
        }

        public override bool Equals(object obj)
        {
            return obj is Point point &&
                   value.Equals(point.value);
        }

        public override int GetHashCode()
        {
            return -1584136870 + EqualityComparer<Vector3>.Default.GetHashCode(value);
        }

        public static bool operator ==(Point p1, Point p2)
        {
            return p1.value == p2.value;
        }
        public static bool operator !=(Point p1, Point p2)
        {
            return p1.value != p2.value;
        }
    }
}