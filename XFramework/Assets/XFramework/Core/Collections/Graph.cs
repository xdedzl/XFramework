using System;
using XFramework;

namespace XFramework.Collections
{
    /// <summary>
    /// 无向图
    /// 采用邻接多重表存储
    /// </summary>
    public class Graph<T> : IGraph<T>
    {
        /// <summary>
        /// 图的顶点数组
        /// </summary>
        private VertexNode[] vertexs;
        /// <summary>
        /// 下一个添加的顶点索值引，当前顶点数量
        /// </summary>
        private int index;

        public Graph(int capacity = 10, bool _isDirected = true)
        {
            vertexs = new VertexNode[capacity];
        }

        /// <summary>
        /// 添加顶点
        /// </summary>
        /// <param name="data"></param>
        public void AddVertex(T data)
        {
            // 扩容
            if (index >= vertexs.Length)
            {
                VertexNode[] tempNodes = new VertexNode[vertexs.Length * 2];
                for (int i = 0, length = vertexs.Length; i < length; i++)
                {
                    tempNodes[i] = vertexs[i];
                }
                vertexs = tempNodes;
            }
            vertexs[index] = new VertexNode(data);
            index++;
        }

        /// <summary>
        /// 添加边
        /// </summary>
        /// <param name="iIndex"></param>
        /// <param name="jIndex"></param>
        /// <param name="weight"></param>
        public void AddEdge(int iIndex, int jIndex, int weight = 1)
        {
            if (iIndex > index || jIndex > index)
            {
                throw new System.Exception("添加临界点的索引超出范围");
            }

            Edge newEdge = new Edge(iIndex, jIndex, weight);

            Edge edge = vertexs[iIndex].firstEdge;
            vertexs[iIndex].firstEdge = newEdge;
            newEdge.iLink = edge;

            edge = vertexs[jIndex].firstEdge;
            vertexs[jIndex].firstEdge = newEdge;
            newEdge.jLink = edge;
        }

        /// <summary>
        /// 找寻最短路径startIndex到其余所有点的最短路径,Dijkstra
        /// </summary>
        public void GetShortPath(int startIndex)
        {
            // 初始化最短路径集合，第i个值代表从startIndex到i的最短路径
            Path[] paths = new Path[index];
            for (int i = 0, length = paths.Length; i < length; i++)
            {
                paths[i] = new Path();
                paths[i].length = int.MaxValue;
            }
            Edge edge = vertexs[startIndex].firstEdge;
            paths[startIndex].length = 0;
            paths[startIndex].ensure = true;
            // 以startIndex为起点距离最短的边
            Edge shortLink = edge;
            // 给路径长度数组附初始值
            while (edge != null)
            {
                paths[edge.jIndex].length = edge.weight;
                if (edge.weight < shortLink.weight)
                {
                    shortLink = edge;
                }
                edge = edge.iLink;
            }

            // 已经确定了最短路径的顶点数量
            int count = 1;
            while (count < paths.Length)
            {
                // 记录未确定的路径长度最小值
                int tempIndex = 0;
                int min = int.MaxValue;
                for (int i = 0; i < paths.Length; i++)
                {
                    if (!paths[i].ensure && paths[i].length <= min)
                    {
                        min = paths[i].length;
                        tempIndex = i;
                    }
                }

                Edge nextEdge = vertexs[tempIndex].firstEdge;
                while (nextEdge != null)
                {
                    if (paths[nextEdge.iIndex].length != int.MaxValue && paths[nextEdge.iIndex].length + nextEdge.weight < paths[tempIndex].length)
                    {
                        paths[tempIndex].length = paths[nextEdge.iIndex].length + nextEdge.weight;
                    }
                    nextEdge = nextEdge.jLink;
                }
                paths[tempIndex].ensure = true;
                count++;
            }
        }

        /// <summary>
        /// 找寻最短路径i到j短路径,Floyd
        /// </summary>
        public int[,] GetShortPath()
        {
            // 构造矩阵并附初始值
            int[,] d = new int[index, index];
            for (int i = 0; i < d.GetLength(0); i++)
            {
                for (int k = 0; k < d.GetLength(0); k++)
                {
                    d[i, k] = 1000;
                }
            }
            for (int i = 0; i < index; i++)
            {
                Edge edge = vertexs[i].firstEdge;
                while (edge != null)
                {
                    d[edge.iIndex, edge.jIndex] = edge.weight;
                    edge = edge.iLink;
                }
            }

            // 计算最短路径矩阵
            for (int k = 0; k < index; k++)
                for (int i = 0; i < index; i++)
                    for (int j = 0; j < index; j++)
                    {
                        if (i == j)
                            d[i, j] = 0;
                        else if (i != k && k != j && d[i, k] + d[k, j] < d[i, j])
                            d[i, j] = d[i, k] + d[k, j];
                    }
            return d;
        }

        /// <summary>
        /// 遍历所有边并执行同一操作
        /// </summary>
        /// <param name="action"></param>
        public void Foreach(Action<Edge> action)
        {
            Edge edge;
            List<Edge> edges = new List<Edge>();
            for (int i = 0; i < 6; i++)
            {
                edge = vertexs[i].firstEdge;
                while (edge != null)
                {
                    if (edge.visited != true)
                    {
                        action(edge);
                        edge.visited = true;
                    }
                    if (edge.iIndex == i)
                        edge = edge.iLink;
                    else
                        edge = edge.jLink;
                }
            }
            edges.ForEach((a) =>
            {
                a.visited = false;
            });
        }

        public VertexNode this[int i]
        {
            get { return vertexs[i]; }
        }

        /// <summary>
        /// 边表结点
        /// 当Graph为无向图时，head和tail没有首尾之分
        /// </summary>
        public class Edge
        {
            /// <summary>
            /// 无向图：无向边一个顶点对于的下标
            /// </summary>
            public int iIndex;
            /// <summary>
            /// 无向图：无向边另一个顶点对于的下标
            /// </summary>
            public int jIndex;
            /// <summary>
            /// 无向图：依附headIndex的下一条边
            /// </summary>
            public Edge iLink;
            /// <summary>
            /// 无向图：依附tailIndex的下一条边
            /// </summary>
            public Edge jLink;
            /// <summary>
            /// 存储权值
            /// </summary>
            public int weight;
            /// <summary>
            /// 访问标识符
            /// </summary>
            public bool visited;

            public Edge(int _iIdnex, int _jIndex, int _weight = 1)
            {
                iIndex = _iIdnex;
                jIndex = _jIndex;
                weight = _weight;
            }

            public override string ToString()
            {
                return string.Format("{0}, {1}", iIndex, jIndex);
            }
        }

        /// <summary>
        /// 顶点表结构
        /// </summary>
        public class VertexNode
        {
            /// <summary>
            /// 顶点信息
            /// </summary>
            public T data;
            /// <summary>
            /// 无向边：指向边表的第一条边
            /// </summary>
            public Edge firstEdge;
            /// <summary>
            /// 访问标识符
            /// </summary>
            public bool visited;

            public VertexNode(T _data)
            {
                data = _data;
            }
        }

        private class Path
        {
            public List<int> pathIndexes = new List<int>();
            public int length;
            public bool ensure;
        }
    }
}