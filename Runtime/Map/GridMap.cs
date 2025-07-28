using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// ��������ϵ
    /// �����ڵѿ�������ϵ�е�λ��Ϊ�������ĵ��λ��
    /// ����Z�ᣬtodoҪ֧�ֺ��Բ�ͬ��
    /// </summary>
    public struct GridCoordinate
    {
        public Vector2 position;
        //public float angle;
        public Vector2 size;

        public readonly Vector2 halfSize => size * 0.5f;

        /// <summary>
        /// �ѿ���ת��������
        /// </summary>
        public readonly Vector2Int DCToGrid(Vector3 pos)
        {
            return DcToGrid(pos.XY());
        }

        /// <summary>
        /// �ѿ���ת��������
        /// </summary>
        public readonly Vector2Int DcToGrid(Vector2 pos)
        {
            return new Vector2Int(Mathf.FloorToInt(pos.x / size.x), Mathf.FloorToInt(pos.y / size.y));
        }

        public readonly Vector2Int WorldDCToGrid(Vector3 pos)
        {
            return WorldDCToGrid(pos.XY());
        }

        public readonly Vector2Int WorldDCToGrid(Vector2 pos)
        {
            return DCToGrid(pos - position);
        }

        public readonly Vector2 DcToCenterDC(Vector3 pos)
        {
            return DcToCenterDC(pos.XY());
        }

        public readonly Vector2 DcToCenterDC(Vector2 pos)
        {
            return GridToDC(DcToGrid(pos)); 
        }

        public readonly Vector2 WorldDCToCenterDC(Vector3 pos)
        {
            return WorldDCToCenterDC(pos.XY());
        }

        public readonly Vector2 WorldDCToCenterDC(Vector2 pos)
        {
            pos -= position;
            var centerDC = DcToCenterDC(pos);
            return centerDC += position;
        }

        /// <summary>
        /// ����ת�ѿ�������
        /// </summary>
        public readonly Vector2 GridToDC(Vector2Int pos)
        {
            return GridToDC(pos.x, pos.y);
        }

        /// <summary>
        /// ����ת�ѿ�������
        /// </summary>
        public readonly Vector2 GridToDC(int x, int y)
        {
            return new Vector2(x * size.x + halfSize.x, y * size.y + halfSize.y);
        }

        /// <summary>
        /// ����ת�ѿ�������(����ռ�)
        /// </summary>
        public readonly Vector2 GridToWorldDC(Vector2Int pos)
        {
            return GridToDC(pos) + position;
        }

        /// <summary>
        /// ����ת�ѿ�������(����ռ�)
        /// </summary>
        public readonly Vector2 GridToWorldDC(int x, int y)
        {
            return GridToDC(x, y) + position;
        }
    }

    public class Grid<T> : IEnumerable
    {
        protected T[,] map; // ��ά�����ͼ��0���ϰ���1���ϰ���
        protected Vector2Int offset;

        public int width { get { return map.GetLength(0); } }
        public int height { get { return map.GetLength(1); } }

        public Grid(int width, int height)
        {
            map = new T[width, height];
            offset = new Vector2Int(width / 2, height / 2);
        }

        public Grid(int width, int height, Vector2Int offset)
        {
            map = new T[width, height];
            this.offset = offset;
        }

        public T this[Vector2Int pos]
        {
            get
            {
                return this[pos.x, pos.y];
            }
            set
            {
                this[pos.x, pos.y] = value;
            }
        }

        public T this[int x, int y]
        {
            get
            {
                return map[x + offset.x, y + offset.y];
            }
            set
            {
                map[x + offset.x, y + offset.y] = value;
            }
        }

        public bool CheckPosVaild(Vector2Int pos)
        {
            int x = pos.x + offset.x;
            int y = pos.y + offset.y;
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            return true;
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var item in map)
            {
                yield return item;
            }
        }
    }

    public class GridMap<T> : Grid<T>
    {
        public GridMap(int width, int height) : base(width, height) { }

        public GridMap(int width, int height, Vector2Int offset) : base(width, height, offset) { }
        
    }

    public class AStarPathfinder
    {
        private int[,] map; // ��ά�����ͼ��0���ϰ���1���ϰ���
        private int width, height;

        public AStarPathfinder(int[,] map)
        {
            this.map = map;
            width = map.GetLength(0);
            height = map.GetLength(1);
        }

        // �ڵ���洢Ѱ·��Ϣ
        private class Node
        {
            public Vector2Int position;
            public int gCost;   // ��㵽��ǰ�ڵ��ʵ�ʴ���
            public int hCost;   // ��ǰ�ڵ㵽�յ������ʽ���ƴ���
            public int FCost => gCost + hCost; // �ܴ���
            public Node parent; // ����·���ĸ��ڵ�

            public Node(Vector2Int pos, int g, int h, Node p = null)
            {
                position = pos;
                gCost = g;
                hCost = h;
                parent = p;
            }
        }

        /// <summary>
        /// A*Ѱ·���ķ���
        /// </summary>
        /// <param name="startPos">�������</param>
        /// <param name="endPos">�յ�����</param>
        /// <returns>·�������б�����ͷβ��</returns>
        public IList<Vector2Int> FindPath(Vector2Int startPos, Vector2Int endPos)
        {
            // ��֤������Ч��
            if (!IsPositionValid(startPos) || !IsPositionValid(endPos) || map[startPos.x, startPos.y] == 1 || map[endPos.x, endPos.y] == 1)
                return new List<Vector2Int>();

            // ��ʼ�����ݽṹ
            Dictionary<Vector2Int, Node> openSet = new Dictionary<Vector2Int, Node>(); // �����б���̽���ڵ㣩
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();                    // �ر��б���̽���ڵ㣩

            Node startNode = new Node(startPos, 0, CalculateHeuristic(startPos, endPos));
            openSet.Add(startPos, startNode);

            // ��ѭ��
            while (openSet.Count > 0)
            {
                Node currentNode = GetLowestFCostNode(openSet); // ��ȡ�ܴ�����͵Ľڵ�
                openSet.Remove(currentNode.position);
                closedSet.Add(currentNode.position);

                // �����յ㣺��������·��
                if (currentNode.position == endPos)
                    return RetracePath(startNode, currentNode);

                // ̽�����ڽڵ�
                foreach (Vector2Int neighborPos in GetNeighbors(currentNode.position))
                {
                    if (closedSet.Contains(neighborPos) || map[neighborPos.x, neighborPos.y] == 1)
                        continue; // ������̽�����ϰ���unity 

                    int newGCost = currentNode.gCost + CalculateDistance(currentNode.position, neighborPos);
                    Node neighborNode = openSet.ContainsKey(neighborPos) ? openSet[neighborPos] : null;

                    // ��·�����Ż��״η��ָýڵ�
                    if (neighborNode == null || newGCost < neighborNode.gCost)
                    {
                        int hCost = CalculateHeuristic(neighborPos, endPos);
                        if (neighborNode == null)
                        {
                            neighborNode = new Node(neighborPos, newGCost, hCost, currentNode);
                            openSet.Add(neighborPos, neighborNode);
                        }
                        else
                        {
                            neighborNode.gCost = newGCost;
                            neighborNode.parent = currentNode;
                        }
                    }
                }
            }
            return new List<Vector2Int>(); // �޿���·��
        }

        // ��������·��
        private List<Vector2Int> RetracePath(Node startNode, Node endNode)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Node currentNode = endNode;

            while (currentNode != null)
            {
                path.Add(currentNode.position);
                currentNode = currentNode.parent;
            }
            path.Reverse(); // ��תʹ·������㵽�յ�
            return path;
        }

        // ��ȡ��ǰ�ڵ��������꣨�ķ���
        private List<Vector2Int> GetNeighbors(Vector2Int pos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();
            Vector2Int[] directions = {
            new Vector2Int(0, 1),  // ��
            new Vector2Int(1, 0),  // ��
            new Vector2Int(0, -1), // ��
            new Vector2Int(-1, 0)  // ��
        };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = pos + dir;
                if (IsPositionValid(neighbor)) neighbors.Add(neighbor);
            }
            return neighbors;
        }

        // �����پ�����������������������
        private int CalculateHeuristic(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // ��ʽ��|dx| + |dy|

        // �ڵ���ƶ����ۣ�ֱ��=10��
        private int CalculateDistance(Vector2Int a, Vector2Int b)
            => (a.x == b.x || a.y == b.y) ? 10 : 14; // �򻯴����ķ����ƶ�

        // ��ȡ�����б�����СFCost�ڵ�
        private Node GetLowestFCostNode(Dictionary<Vector2Int, Node> openSet)
        {
            Node lowestNode = null;
            foreach (Node node in openSet.Values)
                if (lowestNode == null || node.FCost < lowestNode.FCost)
                    lowestNode = node;
            return lowestNode;
        }

        // ������Ч�Լ�飨�߽�+�ϰ���
        private bool IsPositionValid(Vector2Int pos)
            => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }
}
