using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public static class GLDraw
    {
        /// <summary>
        /// 创建一个轴的箭头网格
        /// </summary>
        public static Mesh CreateArrow(Color color, float scale)
        {
            int segmentsCount = 12;  // 侧面三角形数量
            float size = 1.0f / 5;
            size *= scale;

            Vector3[] vertices = new Vector3[segmentsCount + 2];
            int[] triangles = new int[segmentsCount * 6];
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; ++i)
            {
                // 顶点颜色
                colors[i] = color;
            }

            float radius = size / 2.6f; // 地面半径
            float height = size;        // 高
            float deltaAngle = Mathf.PI * 2.0f / segmentsCount;

            float y = -height;

            vertices[vertices.Length - 1] = new Vector3(0, -height, 0); // 圆心点
            vertices[vertices.Length - 2] = Vector3.zero;               // 锥顶

            // 底面圆上的点
            for (int i = 0; i < segmentsCount; i++)
            {
                float angle = i * deltaAngle;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                vertices[i] = new Vector3(x, y, z);
            }

            for (int i = 0; i < segmentsCount; i++)
            {
                // 底面三角形排序
                triangles[i * 6] = vertices.Length - 1;
                triangles[i * 6 + 1] = i;
                triangles[i * 6 + 2] = (i + 1) % segmentsCount;

                // 侧面三角形排序
                triangles[i * 6 + 3] = vertices.Length - 2;
                triangles[i * 6 + 4] = i;
                triangles[i * 6 + 5] = (i + 1) % segmentsCount;
            }

            Mesh cone = new Mesh
            {
                name = "Cone",
                vertices = vertices,
                triangles = triangles,
                colors = colors
            };

            return cone;
        }

        /// <summary>
        /// 创建一个方块网格 
        /// </summary>
        public static Mesh CreateCubeMesh(Color color, Vector3 center, float scale, float cubeLength = 1, float cubeWidth = 1, float cubeHeight = 1)
        {
            cubeHeight *= scale;
            cubeWidth *= scale;
            cubeLength *= scale;

            Vector3 vertice_0 = center + new Vector3(-cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_1 = center + new Vector3(cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_2 = center + new Vector3(cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
            Vector3 vertice_3 = center + new Vector3(-cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
            Vector3 vertice_4 = center + new Vector3(-cubeLength * .5f, cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_5 = center + new Vector3(cubeLength * .5f, cubeWidth * .5f, cubeHeight * .5f);
            Vector3 vertice_6 = center + new Vector3(cubeLength * .5f, cubeWidth * .5f, -cubeHeight * .5f);
            Vector3 vertice_7 = center + new Vector3(-cubeLength * .5f, cubeWidth * .5f, -cubeHeight * .5f);
            Vector3[] vertices = new[]
            {
                // Bottom Polygon
                vertice_0, vertice_1, vertice_2, vertice_3,
                // Left Polygon
                vertice_7, vertice_4, vertice_0, vertice_3,
                // Front Polygon
                vertice_4, vertice_5, vertice_1, vertice_0,
                // Back Polygon
                vertice_6, vertice_7, vertice_3, vertice_2,
                // Right Polygon
                vertice_5, vertice_6, vertice_2, vertice_1,
                // Top Polygon
                vertice_7, vertice_6, vertice_5, vertice_4
            };

            int[] triangles = new[]
            {
                // Cube Bottom Side Triangles
                3, 1, 0,
                3, 2, 1,    
                // Cube Left Side Triangles
                3 + 4 * 1, 1 + 4 * 1, 0 + 4 * 1,
                3 + 4 * 1, 2 + 4 * 1, 1 + 4 * 1,
                // Cube Front Side Triangles
                3 + 4 * 2, 1 + 4 * 2, 0 + 4 * 2,
                3 + 4 * 2, 2 + 4 * 2, 1 + 4 * 2,
                // Cube Back Side Triangles
                3 + 4 * 3, 1 + 4 * 3, 0 + 4 * 3,
                3 + 4 * 3, 2 + 4 * 3, 1 + 4 * 3,
                // Cube Rigth Side Triangles
                3 + 4 * 4, 1 + 4 * 4, 0 + 4 * 4,
                3 + 4 * 4, 2 + 4 * 4, 1 + 4 * 4,
                // Cube Top Side Triangles
                3 + 4 * 5, 1 + 4 * 5, 0 + 4 * 5,
                3 + 4 * 5, 2 + 4 * 5, 1 + 4 * 5,
            };

            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; ++i)
            {
                colors[i] = color;
            }

            Mesh cubeMesh = new Mesh();
            cubeMesh.name = "cube";
            cubeMesh.vertices = vertices;
            cubeMesh.triangles = triangles;
            cubeMesh.colors = colors;
            cubeMesh.RecalculateNormals();
            return cubeMesh;
        }

        public static Mesh CreateSphere(Vector3 center, float scale, Color color)
        {
            Mesh mesh = new Mesh();
            mesh.name = "sphere";

            return mesh;
        }
    }
}