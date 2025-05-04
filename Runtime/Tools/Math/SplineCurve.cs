using System.Collections.Generic;
using UnityEngine;

namespace XFramework.Mathematics
{
    /// <summary>
    /// 样条类型
    /// </summary>
    public enum SplineMode
    {
        Hermite,               // 埃尔米特样条
        Catmull_Rom,           // Catmull_Rom 建议选择
        CentripetalCatmull_Rom,// 向心Catmull_Rom
        B_Spline,              // 均匀B样条
    }

    /// <summary>
    /// 样条曲线
    /// </summary>
    public class SplineCurve
    {
        /// <summary>
        /// 曲线起始节点
        /// </summary>
        private Node startNode;
        /// <summary>
        /// 曲线终结点
        /// </summary>
        private Node endNode;
        /// <summary>
        /// 节点集合
        /// </summary>
        private List<Node> nodeList;
        /// <summary>
        /// 节点法线集合
        /// </summary>
        private List<Vector3> tangentsList;
        /// <summary>
        /// 曲线段集合
        /// </summary>
        public List<CurveSegement> segmentList { get; private set; }
        /// <summary>
        /// 曲线构造类型
        /// </summary>
        public SplineMode mode { get; private set; }

        public SplineCurve(SplineMode _mode = SplineMode.Catmull_Rom)
        {
            nodeList = new List<Node>();
            tangentsList = new List<Vector3>();
            segmentList = new List<CurveSegement>();
            mode = _mode;
        }

        /// <summary>
        /// 
        /// </summary>
        public void AddCatmull_RomControl()
        {
            if (mode != SplineMode.Catmull_Rom)
            {
                Debug.Log("不是Catmull样条");
                return;
            }
            if (nodeList.Count < 2)
            {
                Debug.Log("Catmull_Rom样条取点要大于等于2");
                return;
            }
            Node node = new Node(startNode.pos + (nodeList[0].pos - nodeList[1].pos), null, nodeList[0]);
            nodeList.Insert(0, node);
            node = new Node(endNode.pos + (endNode.pos - nodeList[nodeList.Count - 2].pos), nodeList[nodeList.Count - 1]);
            nodeList.Add(node);
        }

        /// <summary>
        /// 添加节点
        /// </summary>
        /// <param name="newNode"></param>
        public void AddNode(Vector3 pos, float c)
        {
            Node node;
            if (nodeList.Count < 1)
            {
                node = new Node(pos);
            }
            else
            {
                node = new Node(pos, nodeList[nodeList.Count - 1]);
            }
            nodeList.Add(node);


            if (nodeList.Count > 1)
            {
                CurveSegement a = new CurveSegement(endNode, node, this);
                a.c = c;
                segmentList.Add(a);
                CaculateTangents(segmentList.Count - 1);               // 计算新加入的曲线段起始切线
            }
            else // 加入第一个节点
            {
                startNode = node;
            }
            endNode = node;
        }

        /// <summary>
        /// 获取点
        /// </summary>
        /// <param name="index"></param>
        /// <param name="t"></param>
        public void GetPoint(int index, float t)
        {
            segmentList[index].GetPoint(t);
        }

        /// <summary>
        /// 获取切线
        /// </summary>
        /// <param name="index"></param>
        /// <param name="t"></param>
        public void GetTangents(int index, float t)
        {
            segmentList[index].GetTangents(t);
        }

        /// <summary>
        /// 计算曲线段首尾切线
        /// </summary>
        /// <param name="index"></param>
        private void CaculateTangents(int index)
        {
            CurveSegement segement = segmentList[index];

            if (index == 0)
            {
                segement.startTangents = segement.endNode.pos - segement.endNode.pos;
                segement.endTangents = segement.endNode.pos - segement.startNode.pos;
                return;
            }

            CurveSegement preSegement = segmentList[index - 1];

            segement.startTangents = 0.5f * (1 - segement.c) * (segement.endNode.pos - preSegement.endNode.pos);
            segement.endTangents = segement.endNode.pos - segement.startNode.pos;
            preSegement.endTangents = segement.startTangents;

        }

        /// <summary>
        /// 曲线段
        /// </summary>
        public class CurveSegement
        {
            /// <summary>
            /// 所属曲线
            /// </summary>
            public SplineCurve rootCurve;

            /// <summary>
            /// 曲线段起始位置
            /// </summary>
            public Node startNode { get; private set; }
            /// <summary>
            /// 曲线段末尾位置
            /// </summary>
            public Node endNode { get; private set; }

            public Vector3 startTangents;
            public Vector3 endTangents;

            /// <summary>
            /// 张力系数
            /// </summary>
            public float c { get; set; }

            public CurveSegement(Node _startNode, Node _endNode, SplineCurve _rootCurve)
            {
                startNode = _startNode;
                endNode = _endNode;
                rootCurve = _rootCurve;
                c = -5f;
            }

            /// <summary>
            /// 获取点
            /// </summary>
            /// <param name="t"></param>
            /// <returns></returns>
            public Vector3 GetPoint(float t)
            {
                Vector3 x = Vector3.zero;
                switch (rootCurve.mode)
                {
                    case SplineMode.Hermite:
                        x = (2 * t * t * t - 3 * t * t + 1) * startNode.pos;
                        x += (-2 * t * t * t + 3 * t * t) * endNode.pos;
                        x += (t * t * t - 2 * t * t + t) * startTangents;
                        x += (t * t * t - t * t) * endTangents;
                        break;
                    case SplineMode.Catmull_Rom:
                        x += startNode.preNode.pos * (-0.5f * t * t * t + t * t - 0.5f * t);
                        x += startNode.pos * (1.5f * t * t * t - 2.5f * t * t + 1.0f);
                        x += endNode.pos * (-1.5f * t * t * t + 2.0f * t * t + 0.5f * t);
                        x += endNode.nextNode.pos * (0.5f * t * t * t - 0.5f * t * t);
                        break;
                    case SplineMode.CentripetalCatmull_Rom:
                        break;
                    default:
                        break;
                }

                return x;

            }

            /// <summary>
            /// 获取切线
            /// </summary>
            /// <param name="t"></param>
            /// <returns></returns>
            public Vector3 GetTangents(float t)
            {
                Vector3 tangents = Vector3.zero;
                switch (rootCurve.mode)
                {
                    case SplineMode.Hermite:
                        tangents = (6 * t * t - 6 * t) * startNode.pos;
                        tangents += (-6 * t * t + 6 * t) * endNode.pos;
                        tangents += (3 * t * t - 4 * t + 1) * startTangents;
                        tangents += (3 * t * t - 2 * t) * endTangents;
                        break;
                    case SplineMode.Catmull_Rom:
                        tangents = startNode.preNode.pos * (-1.5f * t * t + 2 * t - 0.5f);
                        tangents += startNode.pos * (3.0f * t * t - 5.0f * t);
                        tangents += endNode.pos * (-3.0f * t * t + 4.0f * t + 0.5f);
                        tangents += endNode.nextNode.pos * (1.5f * t * t - 1.0f * t);
                        break;
                    case SplineMode.CentripetalCatmull_Rom:
                        break;
                    default:
                        break;
                }

                return tangents;
            }
        }

        /// <summary>
        /// 曲线节点
        /// </summary>
        public class Node
        {
            /// <summary>
            /// 节点位置
            /// </summary>
            public Vector3 pos;
            /// <summary>
            /// 前连接节点
            /// </summary>
            public Node preNode;
            /// <summary>
            /// 后连接节点
            /// </summary>
            public Node nextNode;

            public Node(Vector3 _pos)
            {
                pos = _pos;
            }

            public Node(Vector3 _pos, Node _preNode, Node _nextNode = null)
            {
                pos = _pos;
                if (_preNode != null)
                {
                    preNode = _preNode;
                    _preNode.nextNode = this;
                }
                if (_nextNode != null)
                {
                    nextNode = _nextNode;
                    _nextNode.preNode = this;
                }
            }
        }
    }
}