using System.Collections.Generic;
using System;

namespace XFramework.Collections
{
    /// <summary>
    /// 有向图
    /// 采用十字链表存储
    /// </summary>
    public class GraphDir<T> : IGraph<T>
    {
        /// <summary>
        /// 图的顶点数组
        /// </summary>
        private VertexNode[] vertexs;
        /// <summary>
        /// 下一个添加的顶点索值引，当前顶点数量
        /// </summary>
        private int index;

        public GraphDir(int capacity = 10, bool _isDirected = true)
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
        /// <param name="fromIndex"></param>
        /// <param name="toIndex"></param>
        /// <param name="weight"></param>
        public void AddEdge(int fromIndex, int toIndex, int weight = 1)
        {
            if (fromIndex > index || toIndex > index)
            {
                throw new System.Exception("添加临界点的索引超出范围");
            }

            Edge newEdge = new Edge(fromIndex, toIndex, weight);

            // 添加邻接表元素
            Edge edge = vertexs[fromIndex].firstOut;
            if (edge == null)
            {
                vertexs[fromIndex].firstOut = newEdge;
            }
            else
            {
                while (edge.headLink != null)
                {
                    // 重复添加的判断
                    if (edge.headIndex == newEdge.headIndex && edge.tailIndex == newEdge.tailIndex)
                        return;
                    edge = edge.headLink;
                }
                edge.headLink = newEdge;
            }

            // 添加逆邻接表元素
            edge = vertexs[toIndex].firstIn;
            if (edge == null)
            {
                vertexs[toIndex].firstIn = newEdge;
            }
            else
            {
                while (edge.tailLink != null)
                {
                    // 重复添加的判断在邻接表中判断
                    edge = edge.tailLink;
                }
                edge.tailLink = newEdge;
            }
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
            Edge edge = vertexs[startIndex].firstOut;
            paths[startIndex].length = 0;
            paths[startIndex].ensure = true;
            // 以startIndex为起点距离最短的边
            Edge shortLink = edge;
            // 给路径长度数组附初始值
            while (edge != null)
            {
                paths[edge.tailIndex].length = edge.weight;
                if (edge.weight < shortLink.weight)
                {
                    shortLink = edge;
                }
                edge = edge.headLink;
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

                Edge edgeIn = vertexs[tempIndex].firstIn;
                while (edgeIn != null)
                {
                    if (paths[edgeIn.headIndex].length != int.MaxValue && paths[edgeIn.headIndex].length + edgeIn.weight < paths[tempIndex].length)
                    {
                        paths[tempIndex].length = paths[edgeIn.headIndex].length + edgeIn.weight;
                    }
                    edgeIn = edgeIn.tailLink;
                }
                paths[tempIndex].ensure = true;
                count++;
            }

            foreach (var item in paths)
            {
                UnityEngine.Debug.Log(item.length);
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
                Edge edge = vertexs[i].firstOut;
                while (edge != null)
                {
                    d[edge.headIndex, edge.tailIndex] = edge.weight;
                    edge = edge.headLink;
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
            for (int i = 0; i < vertexs.Length; i++)
            {
                edge = vertexs[i].firstOut;
                while (edge != null)
                {
                    action(edge);
                    edge = edge.headLink;
                }
            }
        }

        public VertexNode this[int i]
        {
            get { return vertexs[i]; }
        }

        /// <summary>
        /// 边表结点
        /// 当Graph为有向图时,head和tail代表首尾，当Graph为无向图时，head和tail没有首尾之分
        /// </summary>
        public class Edge
        {
            /// <summary>
            /// 有向图：有向边起点对应的下标
            /// </summary>
            public int headIndex;
            /// <summary>
            /// 有向图：有向边尾点对应的下标
            /// </summary>
            public int tailIndex;
            /// <summary>
            /// 有向图：指向起点相同的下一条边
            /// </summary>
            public Edge headLink;
            /// <summary>
            /// 有向图：指向终点相同的下一条边
            /// </summary>
            public Edge tailLink;
            /// <summary>
            /// 存储权值
            /// </summary>
            public int weight;
            /// <summary>
            /// 访问标识符
            /// </summary>
            public bool visited;

            public Edge(int _headIdnex, int _tailIndex, int _weight = 1)
            {
                headIndex = _headIdnex;
                tailIndex = _tailIndex;
                weight = _weight;
            }

            public override string ToString()
            {
                return string.Format("{0}, {1}", headIndex, tailIndex);
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
            /// 有向边：指向出边表的第一条边，组成邻接表
            /// </summary>
            public Edge firstOut;
            /// <summary>
            /// 有向边：指向入边表的第一条边，组成逆邻接表
            /// </summary>
            public Edge firstIn;
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