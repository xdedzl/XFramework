using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.UI
{
    /// <summary>
    /// 树节点
    /// </summary>
    public class TreeNode : IEnumerable<TreeNode>
    {
        public RectTransform Rect { get; private set; }
        /// <summary>
        /// 当前结点所归属的树
        /// </summary>
        private Tree m_Tree;
        /// <summary>
        /// 子结点
        /// </summary>
        private List<TreeNode> m_Childs;
        /// <summary>
        /// 父结点
        /// </summary>
        private TreeNode m_Parent;
        /// <summary>
        /// 开启状态
        /// </summary>
        private bool m_IsOn;
        /// <summary>
        /// 层级
        /// </summary>
        private int m_Level;

        private string text;

        /// <summary>
        /// 显示文字
        /// </summary>
        public string Text
        {
            get
            {
                return text;
            }
            set
            {
                text = value;
                if (Rect) // 如果实体已经创建
                {
                    Rect.Find("Button").Find("Text").GetComponent<Text>().text = text;
                }
            }
        }

        /// <summary>
        /// 根结点
        /// </summary>
        public TreeNode Root
        {
            get
            {
                TreeNode item = this;
                while (item.m_Parent != null)
                {
                    item = item.m_Parent;
                }
                return item;
            }
        }

        /// <summary>
        /// 子节点数量
        /// </summary>
        public int ChildCount
        {
            get
            {
                return m_Childs.Count;
            }
        }

        public TreeNode()
        {
            m_IsOn = true;
            m_Childs = new List<TreeNode>();
        }

        public TreeNode(string text) : this()
        {
            this.Text = text;
        }

        /// <summary>
        /// 刷新显示隐藏
        /// </summary>
        private void RefreshView(bool isOn)
        {
            isOn &= m_IsOn;
            if (isOn)
            {
                foreach (var item in m_Childs)
                {
                    item.RefreshView(isOn);
                    item.Rect.localScale = new Vector3(1, 1, 1);
                }
            }
            else
            {
                foreach (var item in m_Childs)
                {
                    item.RefreshView(isOn);
                    item.Rect.localScale = new Vector3(1, 0, 1);
                }
            }
        }

        /// <summary>
        /// 刷新位置
        /// </summary>
        public void RefreshPos()
        {
            int index = 0;

            if (m_Parent != null)
            {
                foreach (var item in m_Parent.m_Childs)
                {
                    if (item == this)
                        break;
                    index += item.GetItemCount();
                }
            }

            Rect.anchoredPosition = new Vector2(0, -index * Rect.sizeDelta.y);

            foreach (var item in m_Childs)
            {
                item.RefreshPos();
            }
        }

        /// <summary>
        /// 添加一个父子关系
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public TreeNode AddChild(TreeNode item)
        {
            m_Childs.Add(item);
            item.m_Parent = this;
            return this;
        }

        public TreeNode AddChild(string text)
        {
            return AddChild(new TreeNode(text));
        }

        /// <summary>
        /// 创建一个棵树并刷新位置
        /// </summary>
        public void CreateTree(Tree tree)
        {
            InternalCreateTree(tree);
            RefreshPos();
        }

        /// <summary>
        /// 添加一个父子关系并创建实体
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public TreeNode CreateChild(TreeNode item)
        {
            if (ChildCount == 0)
                SetToggle(true);
            AddChild(item);

            item.InternalCreateTree(m_Tree);
            RefreshView(m_IsOn);
            Root.RefreshPos();
            return this;
        }

        public TreeNode CreateChild(string text)
        {
            TreeNode item = new TreeNode(text);
            return CreateChild(item);
        }

        /// <summary>
        /// 删除自身
        /// </summary>
        public void Delete()
        {
            if (Rect == null)
            {
                return;
            }
            Object.Destroy(Rect.gameObject);
            if (m_Parent != null)
            {
                m_Parent.m_Childs.Remove(this);
                if (m_Parent.ChildCount == 0)
                    SetToggle(false);
            }
            Root.RefreshPos();
        }

        /// <summary>
        /// 根据已有的父子关系创建一颗（子）树
        /// </summary>
        /// <param name="m_Parent"></param>
        /// <param name="gameObject"></param>
        private void InternalCreateTree(Tree tree)
        {
            InitEnity(tree);
            if (m_Childs.Count == 0)
            {
                SetToggle(false);
            }
            else
            {
                // 继续创建
                foreach (var child in m_Childs)
                {
                    child.InternalCreateTree(m_Tree);
                }
            }
        }

        /// <summary>
        /// 初始化场景中对应的实体
        /// </summary>
        /// <param name="tree"></param>
        private void InitEnity(Tree tree)
        {
            m_Tree = tree;

            // 创建自己
            if (m_Parent == null)
            {
                Rect = Object.Instantiate(m_Tree.NodeTemplate, m_Tree.NodeTemplate.transform.parent.Find("Root")).GetComponent<RectTransform>();
                m_Level = 0;
            }
            else
            {
                Rect = Object.Instantiate(m_Tree.NodeTemplate, m_Parent.Rect.Find("Child")).GetComponent<RectTransform>();
                m_Level = m_Parent.m_Level + 1;
            }

            // UI组件设置
            Rect.Find("Toggle").GetComponent<Toggle>().onValueChanged.AddListener((value) =>
            {
                m_IsOn = value;

                RefreshView(value);
                Root.RefreshPos();

                tree.onOn_Off.Invoke(value, this);
            });

            Rect.Find("Button").GetComponent<Button>().onClick.AddListener(() =>
            {
                tree.onSelectNode.Invoke(this);
            });

            Rect.Find("Button").Find("Text").GetComponent<Text>().text = this.Text;
        }

        /// <summary>
        /// 设置Toggle的显示隐藏
        /// </summary>
        private void SetToggle(bool isActive)
        {
            Rect.Find("Toggle").gameObject.SetActive(isActive);
        }

        #region 节点信息获取

        /// <summary>
        /// 所有子物体的数量 +1, 不仅仅是下一级
        /// </summary>
        public int GetItemCount()
        {
            if (m_Childs.Count == 0 || !m_IsOn)
            {
                return 1;
            }
            else
            {
                int count = 0;
                foreach (var item in m_Childs)
                {
                    count += item.GetItemCount();
                }
                return count + 1;
            }
        }

        /// <summary>
        /// 获取自己在父物体种的索引
        /// </summary>
        public int GetSiblingIndex()
        {
            if (m_Parent != null)
            {
                int index = 0;
                foreach (var item in m_Parent.m_Childs)
                {
                    if (item == this)
                        return index;
                }
            }
            return 0;
        }

        /// <summary>
        /// 重置父物体
        /// </summary>
        /// <param name="parent"></param>
        public void SetParent(TreeNode parent)
        {
            m_Parent.m_Childs.Remove(this);
            parent.AddChild(this);
        }

        /// <summary>
        /// 通过字符串找寻子节点
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public TreeNode Find(string path)
        {
            var temp = path.Split(new char[] { '/' }, 2);
            if (temp.Length == 1)
                return this;

            TreeNode node = null;
            foreach (var item in m_Childs)
            {
                if (item.Text == temp[0])
                {
                    node = item;
                    break;
                }
            }

            if (node == null)
                return node;
            else
                return node.Find(temp[1]);
        }

        /// <summary>
        /// 根据索引获取子节点
        /// </summary>
        public TreeNode GetChild(int index)
        {
            return m_Childs[index];
        }

        #endregion

        #region 迭代器

        public IEnumerator<TreeNode> GetEnumerator()
        {
            return m_Childs.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_Childs.GetEnumerator();
        }

        #endregion
    }
}