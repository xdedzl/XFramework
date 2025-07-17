using System;
using System.Collections.Generic;
using System.Linq;

namespace XFramework.Collections
{
    /// <summary>泛型无向图（支持带权边）</summary>
    public class Graph<T> where T : IEquatable<T>
    {
        /// <summary>带权无向边</summary>
        public class Edge
        {
            public T Vertex1 { get; }
            public T Vertex2 { get; }
            public double Weight { get; }

            public Edge(T v1, T v2, double weight)
            {
                Vertex1 = v1;
                Vertex2 = v2;
                Weight = weight;
            }

            public T GetOtherVertex(T vertex)
            {
                return EqualityComparer<T>.Default.Equals(vertex, Vertex1) ? Vertex2 : Vertex1;
            }
        }

        private readonly Dictionary<T, List<Edge>> _adjacencyList = new();

        public void AddVertex(T vertex)
        {
            if (!_adjacencyList.ContainsKey(vertex))
                _adjacencyList[vertex] = new List<Edge>();
        }

        /// <summary>添加带权边（自动创建顶点）</summary>
        public void AddEdge(T v1, T v2, double weight)
        {
            AddVertex(v1);
            AddVertex(v2);

            // 避免重复边
            if (!_adjacencyList[v1].Any(e => e.GetOtherVertex(v1).Equals(v2)))
            {
                var edge = new Edge(v1, v2, weight);
                _adjacencyList[v1].Add(edge);
                _adjacencyList[v2].Add(edge);
            }
        }

        public bool TryFindPath(T start, T end, out IList<T> path)
        {
            // 如果起点或终点不在图中，直接返回 false
            if (!_adjacencyList.ContainsKey(start) || !_adjacencyList.ContainsKey(end))
            {
                path = null;
                return false;
            }

            // 使用 Dijkstra 获取最短路径结果
            var result = Dijkstra(start);

            // 获取终点的路径信息
            var (distance, foundPath) = result[end];

            // 如果距离为无穷大，说明不可达
            if (double.IsPositiveInfinity(distance))
            {
                path = null;
                return false;
            }

            path = foundPath; // 输出有效路径
            return true;      // 找到路径
        }

        /// <summary>Dijkstra最短路径算法</summary>
        public Dictionary<T, (double Distance, List<T> Path)> Dijkstra(T start)
        {
            var distances = new Dictionary<T, double>();
            var previous = new Dictionary<T, T>();
            var unvisited = new HashSet<T>(_adjacencyList.Keys);

            // 初始化距离
            foreach (var vertex in _adjacencyList.Keys)
                distances[vertex] = vertex.Equals(start) ? 0 : double.PositiveInfinity;

            while (unvisited.Count > 0)
            {
                T current = unvisited.OrderBy(v => distances[v]).First();
                unvisited.Remove(current);

                foreach (var edge in _adjacencyList[current])
                {
                    T neighbor = edge.GetOtherVertex(current);
                    double alt = distances[current] + edge.Weight;

                    if (alt < distances[neighbor])
                    {
                        distances[neighbor] = alt;
                        previous[neighbor] = current;
                    }
                }
            }

            // 重构路径
            var paths = new Dictionary<T, List<T>>();
            foreach (var vertex in _adjacencyList.Keys)
            {
                var path = new List<T>();
                for (T at = vertex; !EqualityComparer<T>.Default.Equals(at, default); at = previous.GetValueOrDefault(at, default!))
                    path.Add(at);
                path.Reverse();
                paths[vertex] = path;
            }

            return distances.ToDictionary(
                kv => kv.Key,
                kv => (kv.Value, paths[kv.Key])
            );
        }

        /// <summary>打印邻接表</summary>
        public void PrintGraph()
        {
            foreach (var vertex in _adjacencyList)
            {
                Console.Write($"{vertex.Key}: ");
                foreach (var edge in vertex.Value)
                    Console.Write($"{edge.GetOtherVertex(vertex.Key)}({edge.Weight}) ");
                Console.WriteLine();
            }
        }
    }
}