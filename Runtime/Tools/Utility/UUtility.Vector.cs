using System;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public partial class UUtility
    {
        /// <summary>
        /// UI相关工具
        /// </summary>
        public class Vector
        {
            public static Vector2 String2Vector2(string value)
            {
                string[] temp = value.Substring(1, value.Length - 2).Split(',');
                float x = System.Convert.ToSingle(temp[0]);
                float y = System.Convert.ToSingle(temp[1]);
                return new Vector2(x, y);
            }

            public static Vector3 String2Vector3(string value)
            {
                string[] temp = value.Substring(1, value.Length - 2).Split(',');
                float x = System.Convert.ToSingle(temp[0].Trim());
                float y = System.Convert.ToSingle(temp[1].Trim());
                float z = System.Convert.ToSingle(temp[2].Trim());
                return new Vector3(x, y, z);
            }

            public static Vector2 Slerp(Vector2 v1, Vector2 v2, float t)
            {
                return Vector3.Slerp(v1, v2, t);
            }

            public static bool IsBetweenPoints(Vector2Int p1, Vector2Int p2, Vector2Int p)
            {
                if(p1.x == p2.x && p2.x == p.x)
                {
                    var minX = Math.Min(p1.x, p2.x);
                    var maxX = Math.Max(p1.x, p2.x);
                    return p.x >= minX && p.x <= maxX;
                }

                if (p1.y == p2.y && p2.y == p.y)
                {
                    var minY = Math.Min(p1.y, p2.y);
                    var maxY = Math.Max(p1.y, p2.y);
                    return p.y >= minY && p.y <= maxY;
                }

                return false;
            }

            public static float Distance(IList<Vector3> vectors)
            {
                float distance = 0;
                for (int i = 0; i < vectors.Count - 1; i++)
                {
                    distance += Vector3.Distance(vectors[i], vectors[i + 1]);
                }
                return distance;
            }
        }
    }
}