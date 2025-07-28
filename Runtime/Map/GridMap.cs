using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 网格坐标系
    /// 方块在笛卡尔坐标系中的位置为方块中心点的位置
    /// 忽略Z轴，todo要支持忽略不同轴
    /// </summary>
    public struct GridCoordinate
    {
        public Vector2 position;
        //public float angle;
        public Vector2 size;

        public readonly Vector2 halfSize => size * 0.5f;

        /// <summary>
        /// 笛卡尔转网格坐标
        /// </summary>
        public readonly Vector2Int DCToGrid(Vector3 pos)
        {
            return DcToGrid(pos.XY());
        }

        /// <summary>
        /// 笛卡尔转网格坐标
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
        /// 网格转笛卡尔坐标
        /// </summary>
        public readonly Vector2 GridToDC(Vector2Int pos)
        {
            return GridToDC(pos.x, pos.y);
        }

        /// <summary>
        /// 网格转笛卡尔坐标
        /// </summary>
        public readonly Vector2 GridToDC(int x, int y)
        {
            return new Vector2(x * size.x + halfSize.x, y * size.y + halfSize.y);
        }

        /// <summary>
        /// 网格转笛卡尔坐标(世界空间)
        /// </summary>
        public readonly Vector2 GridToWorldDC(Vector2Int pos)
        {
            return GridToDC(pos) + position;
        }

        /// <summary>
        /// 网格转笛卡尔坐标(世界空间)
        /// </summary>
        public readonly Vector2 GridToWorldDC(int x, int y)
        {
            return GridToDC(x, y) + position;
        }
    }

    public class Grid<T> : IEnumerable
    {
        protected T[,] map; // 二维数组地图（0无障碍，1有障碍）
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
        private int[,] map; // 二维数组地图（0无障碍，1有障碍）
        private int width, height;

        public AStarPathfinder(int[,] map)
        {
            this.map = map;
            width = map.GetLength(0);
            height = map.GetLength(1);
        }

        // 节点类存储寻路信息
        private class Node
        {
            public Vector2Int position;
            public int gCost;   // 起点到当前节点的实际代价
            public int hCost;   // 当前节点到终点的启发式估计代价
            public int FCost => gCost + hCost; // 总代价
            public Node parent; // 回溯路径的父节点

            public Node(Vector2Int pos, int g, int h, Node p = null)
            {
                position = pos;
                gCost = g;
                hCost = h;
                parent = p;
            }
        }

        /// <summary>
        /// A*寻路核心方法
        /// </summary>
        /// <param name="startPos">起点坐标</param>
        /// <param name="endPos">终点坐标</param>
        /// <returns>路径坐标列表（包含头尾）</returns>
        public IList<Vector2Int> FindPath(Vector2Int startPos, Vector2Int endPos)
        {
            // 验证输入有效性
            if (!IsPositionValid(startPos) || !IsPositionValid(endPos) || map[startPos.x, startPos.y] == 1 || map[endPos.x, endPos.y] == 1)
                return new List<Vector2Int>();

            // 初始化数据结构
            Dictionary<Vector2Int, Node> openSet = new Dictionary<Vector2Int, Node>(); // 开放列表（待探索节点）
            HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();                    // 关闭列表（已探索节点）

            Node startNode = new Node(startPos, 0, CalculateHeuristic(startPos, endPos));
            openSet.Add(startPos, startNode);

            // 主循环
            while (openSet.Count > 0)
            {
                Node currentNode = GetLowestFCostNode(openSet); // 获取总代价最低的节点
                openSet.Remove(currentNode.position);
                closedSet.Add(currentNode.position);

                // 到达终点：回溯生成路径
                if (currentNode.position == endPos)
                    return RetracePath(startNode, currentNode);

                // 探索相邻节点
                foreach (Vector2Int neighborPos in GetNeighbors(currentNode.position))
                {
                    if (closedSet.Contains(neighborPos) || map[neighborPos.x, neighborPos.y] == 1)
                        continue; // 跳过已探索或障碍节unity 

                    int newGCost = currentNode.gCost + CalculateDistance(currentNode.position, neighborPos);
                    Node neighborNode = openSet.ContainsKey(neighborPos) ? openSet[neighborPos] : null;

                    // 新路径更优或首次发现该节点
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
            return new List<Vector2Int>(); // 无可用路径
        }

        // 回溯生成路径
        private List<Vector2Int> RetracePath(Node startNode, Node endNode)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Node currentNode = endNode;

            while (currentNode != null)
            {
                path.Add(currentNode.position);
                currentNode = currentNode.parent;
            }
            path.Reverse(); // 反转使路径从起点到终点
            return path;
        }

        // 获取当前节点相邻坐标（四方向）
        private List<Vector2Int> GetNeighbors(Vector2Int pos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();
            Vector2Int[] directions = {
            new Vector2Int(0, 1),  // 上
            new Vector2Int(1, 0),  // 右
            new Vector2Int(0, -1), // 下
            new Vector2Int(-1, 0)  // 左
        };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = pos + dir;
                if (IsPositionValid(neighbor)) neighbors.Add(neighbor);
            }
            return neighbors;
        }

        // 曼哈顿距离启发函数（适用于网格）
        private int CalculateHeuristic(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // 公式：|dx| + |dy|

        // 节点间移动代价（直线=10）
        private int CalculateDistance(Vector2Int a, Vector2Int b)
            => (a.x == b.x || a.y == b.y) ? 10 : 14; // 简化处理四方向移动

        // 获取开放列表中最小FCost节点
        private Node GetLowestFCostNode(Dictionary<Vector2Int, Node> openSet)
        {
            Node lowestNode = null;
            foreach (Node node in openSet.Values)
                if (lowestNode == null || node.FCost < lowestNode.FCost)
                    lowestNode = node;
            return lowestNode;
        }

        // 坐标有效性检查（边界+障碍）
        private bool IsPositionValid(Vector2Int pos)
            => pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }
}
