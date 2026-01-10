using System;
using UnityEngine;

namespace XFramework
{
    public class Grid : MonoBehaviour
    {
        public Vector2Int size;
        public Vector2 cellSize;
        public Vector2 spacing;
        public float width => size.x;
        public float height => size.y;
        
        
        public void Reset()
        {
            size = new Vector2Int(10, 10);
            cellSize = new Vector2Int(1, 1);
            spacing = Vector2.zero;
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Vector2 realCellSize = cellSize + spacing;
            // 绘制网格中的矩形面片
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector3 localBottomLeft = new Vector3(x * (realCellSize.x), 0, y * (realCellSize.y));
                    Vector3 localBottomRight = localBottomLeft + new Vector3(realCellSize.x, 0, 0);
                    Vector3 localTopRight = localBottomLeft + new Vector3(realCellSize.x, 0, realCellSize.y);
                    Vector3 localTopLeft = localBottomLeft + new Vector3(0, 0, realCellSize.y);
                    
                    Vector3 bottomLeft = transform.TransformPoint(localBottomLeft);
                    Vector3 bottomRight = transform.TransformPoint(localBottomRight);
                    Vector3 topRight = transform.TransformPoint(localTopRight);
                    Vector3 topLeft = transform.TransformPoint(localTopLeft);
                    
                    // 绘制矩形的四条边
                    Gizmos.DrawLine(bottomLeft, bottomRight);
                    Gizmos.DrawLine(bottomRight, topRight);
                    Gizmos.DrawLine(topRight, topLeft);
                    Gizmos.DrawLine(topLeft, bottomLeft);
                }
            }

            Gizmos.color = new Color(0, 1, 0, 0.3f); // 绿色半透明
            // 保存当前Gizmos矩阵
            Matrix4x4 oldMatrix = Gizmos.matrix;
            // 设置Gizmos矩阵为GameObject的变换矩阵（包含位置、旋转、缩放）
            Gizmos.matrix = transform.localToWorldMatrix;
            
            // 绘制网格中的矩形面片
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    Vector3 localCenter = new Vector3(
                        x * (cellSize.x + spacing.x) + (cellSize.x + spacing.x) * 0.5f,
                        0,
                        y * (cellSize.y + spacing.y) + (cellSize.y + spacing.y) * 0.5f
                    );
                    
                    // 使用本地坐标绘制（因为已经设置了Gizmos.matrix）
                    Vector3 scale = new Vector3(
                        cellSize.x,
                        0.01f,
                        cellSize.y
                    );

                    // 绘制实心立方体作为矩形面片
                    Gizmos.DrawCube(localCenter, scale);
                }
            }
            
            // 恢复Gizmos矩阵
            Gizmos.matrix = oldMatrix;
        }
    }
}