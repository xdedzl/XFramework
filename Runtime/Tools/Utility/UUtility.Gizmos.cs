using System;
using UnityEngine;

namespace XFramework
{
    public partial class UUtility
    {
        /// <summary>
        /// UI相关工具
        /// </summary>
        public class Gizmos
        {
            /// <summary>
            /// 绘制扇形
            /// </summary>
            public static void DrawWireSector(Vector3 origin, Vector3 direction, float radius, float angle)
            {
                DrawWireSector(origin, direction, radius, angle, Vector3.up);
            }
            

            public static void DrawWireSector(Vector3 origin, Vector3 direction, float radius, float angle, Vector3 axis)
            {
                Vector3 leftdir = Quaternion.AngleAxis(-angle / 2, axis) * direction;
                Vector3 rightdir = Quaternion.AngleAxis(angle / 2, axis) * direction;

                Vector3 currentP = origin + leftdir * radius;
                Vector3 oldP;
                if (angle != 360)
                {
                    UnityEngine.Gizmos.DrawLine(origin, currentP);
                }
                for (int i = 0; i < angle / 10; i++)
                {
                    Vector3 dir = Quaternion.AngleAxis(10 * i, axis) * leftdir;
                    oldP = currentP;
                    currentP = origin + dir * radius;
                    UnityEngine.Gizmos.DrawLine(oldP, currentP);
                }
                oldP = currentP;
                currentP = origin + rightdir * radius;
                UnityEngine.Gizmos.DrawLine(oldP, currentP);
                if (angle != 360)
                {
                    UnityEngine.Gizmos.DrawLine(currentP, origin);
                }

            }

            public static Mesh SemicircleMesh(float radius, int angle, Vector3 axis)
            {
                Vector3 leftdir = Quaternion.AngleAxis(-angle / 2, axis) * Vector3.forward;
                Vector3 rightdir = Quaternion.AngleAxis(angle / 2, axis) * Vector3.forward;
                int pcount = angle / 10;
                //顶点
                Vector3[] vertexs = new Vector3[3 + pcount];
                vertexs[0] = Vector3.zero;
                int index = 1;
                vertexs[index] = leftdir * radius;
                index++;
                for (int i = 0; i < pcount; i++)
                {
                    Vector3 dir = Quaternion.AngleAxis(10 * i, axis) * leftdir;
                    vertexs[index] = dir * radius;
                    index++;
                }
                vertexs[index] = rightdir * radius;
                //三角面
                int[] triangles = new int[3 * (1 + pcount)];
                for (int i = 0; i < 1 + pcount; i++)
                {
                    triangles[3 * i] = 0;
                    triangles[3 * i + 1] = i + 1;
                    triangles[3 * i + 2] = i + 2;
                }

                Mesh mesh = new Mesh();
                mesh.vertices = vertexs;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                return mesh;
            }
        }
    }
}