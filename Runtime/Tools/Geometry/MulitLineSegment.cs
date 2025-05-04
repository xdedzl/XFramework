using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Geometry
{
    /// <summary>
    /// 多段线
    /// </summary>
    public class MulitLineSegment : ISpace
    {
        /// <summary>
        /// 关键点集
        /// </summary>
        private List<Point> points;

        /// <summary>
        /// 线的长度
        /// </summary>
        public float length
        {
            get
            {
                if(points.Count < 2)
                {
                    return 0;
                }
                else
                {
                    float plus = 0;
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        plus += Point.Distance(points[i], points[i + 1]);
                    }
                    return plus;
                }
            }
        }

        public MulitLineSegment()
        {
            points = new List<Point>();
        }

        /// <summary>
        /// 添加一个关键点
        /// </summary>
        /// <param name="point"></param>
        public void AddPoint(Vector3 point)
        {
            points.Add(new Point { value = point });
        }

        public SR R(Point p)
        {
            if(points.Count > 1)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    if(p == points[i] || p == points[i + 1])
                    {
                        return SR.Contain;
                    }
                    else
                    {
                        Vector3 dirP1P = p.value - points[i].value;
                        Vector3 dirPP2 = points[i + 1].value - p.value;

                        if(dirP1P.normalized == dirPP2.normalized)
                        {
                            return SR.Contain;
                        }
                    }
                }
                return SR.Separation;
            }
            else if(points.Count != 0)
            {
                return p == points[0] ? SR.Equal : SR.Separation;
            }
            else
            {
                return SR.Separation;
            }
        }

        public SR R(MulitLineSegment l)
        {
            throw new System.Exception();
        }

        public override bool Equals(object obj)
        {
            return obj is MulitLineSegment line &&
                   EqualityComparer<List<Point>>.Default.Equals(points, line.points) &&
                   length == line.length;
        }

        public override int GetHashCode()
        {
            var hashCode = 1710265035;
            hashCode = hashCode * -1521134295 + EqualityComparer<List<Point>>.Default.GetHashCode(points);
            hashCode = hashCode * -1521134295 + length.GetHashCode();
            return hashCode;
        }
    }
}