using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace XFramework.UI
{
    /// <summary>
    /// 目录树
    /// </summary>
    public class Tree : MonoBehaviour
    {
        /// <summary>
        /// 模板
        /// </summary>
        public GameObject NodeTemplate { get; private set; }
        /// <summary>
        /// 树的根节点
        /// </summary>
        [HideInInspector]
        public TreeNode m_RootTreeNode;

        public string rootText = "Root";

        /// <summary>
        /// 节点展开时Toggle的贴图
        /// </summary>
        public Sprite ToggleSpriteOn;
        /// <summary>
        /// 节点收起时Toggle的贴图
        /// </summary>
        public Sprite ToggleSpriteOff;
        /// <summary>
        /// 节点之间的距离
        /// </summary>
        public float interval;
        /// <summary>
        /// 是否允许多选
        /// </summary>
        private bool m_AllowMulChoice = false;
        public bool AllowMulChoice
        {
            get
            {
                return m_AllowMulChoice;
            }
            set
            {
                if (m_AllowMulChoice != value)
                {
                    if (value)
                    {
                        if (m_SelectedNodes == null)
                            m_SelectedNodes = new List<TreeNode>();
                        if (m_SelectedNode != null)
                        {
                            m_SelectedNodes.Add(m_SelectedNode);
                            m_SelectedNode = null;
                        }
                    }
                    else
                    {
                        if (m_SelectedNodes != null)
                        {
                            for (int i = m_SelectedNodes.Count - 1; i >= 0; i--)
                            {
                                m_SelectedNodes[i].NodeToggle.isOn = false;
                            }
                            m_SelectedNodes.Clear();
                            m_SelectedNodes = null;
                        }
                    }
                    m_AllowMulChoice = value;
                }
            }
        }


        private TreeNode m_SelectedNode;
        /// <summary>
        /// 单选时当前选中的Node
        /// </summary>
        public TreeNode SelectedNode
        {
            get
            {
                return m_SelectedNode;
            }
        }

        private List<TreeNode> m_SelectedNodes;
        /// <summary>
        /// 可以多选时已选中的节点列表
        /// </summary>
        public List<TreeNode> SelectedNodes
        {
            get
            {
                return m_SelectedNodes;
            }
        }

        /// <summary>
        /// 节点被选中的事件
        /// </summary>
        public TreeEvent onSelectNode = new TreeEvent();
        /// <summary>
        /// 节点展开关闭事件
        /// </summary>
        public SwitchEvent onOn_Off = new SwitchEvent();
        /// <summary>
        /// 树的刷新事件
        /// </summary>
        public UnityEvent onRefresh = new UnityEvent();

        private Tree()
        {
            if (m_AllowMulChoice)
            {
                m_SelectedNodes = new List<TreeNode>();
            }
        }

        private void Awake()
        {
            NodeTemplate = transform.Find("NodeTemplate").gameObject;
            NodeTemplate.GetComponent<RectTransform>().anchoredPosition = new Vector2(10000, 10000);
        }

        /// <summary>
        /// 整个树在y轴上的大小（单位：像素）
        /// </summary>
        public float Height
        {
            get
            {
                float y1 = m_RootTreeNode.Rect.position.y;
                TreeNode node = m_RootTreeNode;

                while (node.ChildCount != 0)
                {
                    node = node.GetChild(node.ChildCount - 1);
                }

                float y2 = node.Rect.position.y;
                return y1 - y2 + node.Rect.sizeDelta.y;
            }
        }

        /// <summary>
        /// 构造一棵树
        /// </summary>
        /// <param name="rootNode">父子关系已经设置好的根节点</param>
        public void GenerateTree(TreeNode rootNode)
        {
            if (m_RootTreeNode != null)
                m_RootTreeNode.Delete();

            m_RootTreeNode = rootNode;

            m_RootTreeNode.CreateTree(this);
        }

        /// <summary>
        /// 删除某个节点
        /// </summary>
        /// <param name="path">路径</param>
        public bool Delete(string path)
        {
            TreeNode node = m_RootTreeNode.Find(path);
            if (node != null)
            {
                node.Delete();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 树的自删除
        /// </summary>
        /// <returns></returns>
        public bool Delete()
        {
            if (m_RootTreeNode == null)
            {
                return false;
            }
            else
            {
                if (m_AllowMulChoice)
                {
                    foreach (var item in m_SelectedNodes)
                    {
                        item.NodeToggle.isOn = false;
                    }
                    m_SelectedNodes.Clear();
                }
                else
                {
                    if (m_SelectedNodes != null)
                    {
                        m_SelectedNode.NodeToggle.isOn = false;
                        m_SelectedNode = null;
                    }
                }

                m_RootTreeNode.Delete();
                return true;
            }
        }

        /// <summary>
        /// 获取树的长度
        /// </summary>
        /// <returns></returns>
        public float GetTreeHeight()
        {
            return m_RootTreeNode != null ? m_RootTreeNode.GetItemCount() * NodeTemplate.GetComponent<RectTransform>().sizeDelta.y : 0;
        }

        /// <summary>
        /// 取消所有选中
        /// </summary>
        public void SetAllSelectOff()
        {
            if (m_AllowMulChoice)
            {
                foreach (var item in m_SelectedNodes)
                {
                    item.NodeToggle.isOn = false;
                }
                m_SelectedNodes.Clear();
            }
            else if (m_SelectedNode != null)
            {
                m_SelectedNode = null;
            }
        }

        /// <summary>
        /// 设置根节点的显示影藏
        /// </summary>
        public void SetRootNodeActive(bool isActive)
        {
            if (m_RootTreeNode != null)
            {
                m_RootTreeNode.Rect.GetChild(0).gameObject.SetActive(isActive);
                m_RootTreeNode.Rect.GetChild(1).gameObject.SetActive(isActive);
            }
        }

        /// <summary>
        /// 选中一个节点,只可以在TreeNode中调用
        /// </summary>
        /// <param name="node"></param>
        internal void SelectTreeNode(TreeNode node)
        {
            if (m_AllowMulChoice)
            {
                m_SelectedNodes.Add(node);
                onSelectNode.Invoke(node);

                for (int i = 0; i < node.ChildCount; i++)
                {
                    node.GetChild(i).NodeToggle.isOn = true;
                }
            }
            else
            {
                if (m_SelectedNode != null)
                {
                    m_SelectedNode.NodeToggle.isOn = false;
                }
                m_SelectedNode = node;
                onSelectNode.Invoke(node);
            }
        }

        /// <summary>
        /// 取消一个节点的选中
        /// </summary>
        /// <param name="node"></param>
        internal void UnCheckTreeNode(TreeNode node)
        {
            if (m_AllowMulChoice)
            {
                if (!m_SelectedNodes.Remove(node))
                {
                    throw new XFrameworkException("多选状态时存在逻辑隐患");
                }

                for (int i = 0; i < node.ChildCount; i++)
                {
                    node.GetChild(i).NodeToggle.isOn = false;
                }
            }
            else if (node != m_SelectedNode)
            {
                throw new XFrameworkException("单选状态时存在逻辑隐患");
            }
            else
            {
                m_SelectedNode = null;
            }
        }

        /// <summary>
        /// 设置选中节点
        /// </summary>
        /// <param name="_node"></param>
        public void SetSelectNode(TreeNode _node)
        {
            if (_node != null)
            {
                _node.NodeToggle.isOn = true;
            }
        }

        public class TreeEvent : UnityEngine.Events.UnityEvent<TreeNode> { }
        public class SwitchEvent : UnityEngine.Events.UnityEvent<bool, TreeNode> { }
    }
}