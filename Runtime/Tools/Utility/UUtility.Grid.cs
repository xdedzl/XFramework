using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public enum GridDirection : int
    {
        TOP = 0,
        RIGHT = 1,
        BOTTOM = 2,
        LEFT = 3
    }

    public partial class UUtility
    {
        public class Grid
        {
            private static readonly Dictionary<Vector2Int, GridDirection> _Vector2GridDirection = new()
            {
                {new Vector2Int(0, 1), GridDirection.TOP},
                {new Vector2Int(1, 0), GridDirection.RIGHT},
                {new Vector2Int(0, -1), GridDirection.BOTTOM},
                {new Vector2Int(-1, 0), GridDirection.LEFT},
            };
            private static readonly Vector2Int[] _Grid2VectorDirection =
            {
                new(0, 1),
                new(1, 0),
                new(0, -1),
                new(-1, 0),
             };

            public static Vector2Int GridToVectorDirection(GridDirection direction)
            {
                return _Grid2VectorDirection[(int)direction];
            }

            public static GridDirection Vector2GridDirection(Vector2Int direction)
            {
                return _Vector2GridDirection[direction];
            }

            /// <summary>
            /// �ѿ���ת��������
            /// </summary>
            public static Vector2Int DCToGrid(Vector3 pos)
            {
                return new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            }

            /// <summary>
            /// �ѿ���ת��������
            /// </summary>
            public static Vector2Int DcToGrid(Vector2 pos)
            {
                return new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            }

            /// <summary>
            /// ����ת�ѿ�������
            /// </summary>
            public static Vector2 GridToDC(Vector2Int pos)
            {
                return new Vector2(pos.x + 0.5f, pos.y + 0.5f);
            }

            /// <summary>
            /// ͨ���ѿ��������ȡ��Ӧ����ĵѿ�������
            /// </summary>
            public static Vector2 WorldToWorld(Vector2 pos)
            {
                return new Vector2(Mathf.Floor(pos.x) + 0.5f, Mathf.Floor(pos.y) + 0.5f);
            }
        }
    }
}
