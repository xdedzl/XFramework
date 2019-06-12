using UnityEngine;

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
        /// 节点被选中的事件
        /// </summary>
        public TreeEvent onSelectNode = new TreeEvent();
        /// <summary>
        /// 节点展开关闭事件
        /// </summary>
        public SwitchEvent onOn_Off = new SwitchEvent();

        private void Awake()
        {
            NodeTemplate = transform.Find("NodeTemplate").gameObject;
            NodeTemplate.GetComponent<RectTransform>().anchoredPosition = new Vector2(10000, 10000);
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

        public class TreeEvent : UnityEngine.Events.UnityEvent<TreeNode> { }
        public class SwitchEvent : UnityEngine.Events.UnityEvent<bool, TreeNode> { }
    }
}