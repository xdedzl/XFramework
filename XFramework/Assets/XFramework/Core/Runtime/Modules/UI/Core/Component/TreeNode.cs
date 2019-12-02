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
        /// <summary>
        /// 节点Rect
        /// </summary>
        public RectTransform Rect { get; private set; }
        /// <summary>
        /// 节点自身Toggle
        /// </summary>
        public Toggle NodeToggle { get; private set; }

        /// <summary>
        /// 当前结点所归属的树
        /// </summary>
        private Tree m_Tree;
        /// <summary>
        /// 父结点
        /// </summary>
        public TreeNode Parent { get; private set; }
        /// <summary>
        /// 子结点
        /// </summary>
        protected List<TreeNode> m_Childs;
        // 是否展开
        private bool m_IsOn;
        // 节点文字
        private string text;
        // 是否可交互
        private bool m_Interactable;

        /// <summary>
        /// 构造一个树的节点
        /// </summary>
        public TreeNode()
        {
            m_IsOn = true;
            m_Interactable = true;
            m_Childs = new List<TreeNode>();
        }

        public TreeNode(string text) : this()
        {
            this.Text = text;
        }

        #region 属性

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
                    Rect.Find("Body").Find("Text").GetComponent<Text>().text = text;
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
                while (item.Parent != null)
                {
                    item = item.Parent;
                }
                return item;
            }
        }

        /// <summary>
        /// 是否可交互
        /// </summary>
        public bool Interactable
        {
            get
            {
                return m_Interactable;
            }
            set
            {
                if (m_Interactable != value)
                {
                    m_Interactable = value;
                    Rect.Find("Body").GetComponent<Toggle>().interactable = value;
                }
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

        #endregion

        #region 内部使用

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

            m_Tree.onRefresh.Invoke();
        }

        /// <summary>
        /// 刷新位置
        /// </summary>
        private void RefreshPos()
        {
            int index = 0;

            if (Parent != null)
            {
                foreach (var item in Parent.m_Childs)
                {
                    if (item == this)
                        break;
                    index += item.GetItemCount();
                }
            }

            Rect.anchoredPosition = new Vector2(0, -index * (Rect.sizeDelta.y + m_Tree.interval));

            foreach (var item in m_Childs)
            {
                item.RefreshPos();
            }

            m_Tree.onRefresh.Invoke();
        }

        /// <summary>
        /// 设置Toggle的显示隐藏
        /// </summary>
        private void SetToggle(bool isActive)
        {
            Rect.Find("Toggle").gameObject.SetActive(isActive);
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
        /// 节点创建后的初始化
        /// </summary>
        /// <param name="tree"></param>
        private void InitEnity(Tree tree)
        {
            m_Tree = tree;

            // 创建自己
            if (Parent == null)
            {
                Rect = Object.Instantiate(m_Tree.NodeTemplate, m_Tree.NodeTemplate.transform.parent.Find("Root")).GetComponent<RectTransform>();
            }
            else
            {
                Rect = Object.Instantiate(m_Tree.NodeTemplate, Parent.Rect.Find("Child")).GetComponent<RectTransform>();
            }

            Toggle toggle = Rect.Find("Toggle").GetComponent<Toggle>();
            Image toggleImage = toggle.GetComponent<Image>();
            // UI组件设置
            toggle.onValueChanged.AddListener((value) =>
            {
                m_IsOn = value;

                if (value)
                    toggleImage.sprite = m_Tree.ToggleSpriteOn;
                else
                    toggleImage.sprite = m_Tree.ToggleSpriteOff;

                RefreshView(value);
                Root.RefreshPos();

                tree.onOn_Off.Invoke(value, this);
            });
            toggleImage.sprite = m_Tree.ToggleSpriteOn;

            NodeToggle = Rect.Find("Body").GetComponent<Toggle>();
            NodeToggle.onValueChanged.AddListener((value) =>
            {
                if (value)
                {
                    m_Tree.SelectTreeNode(this);
                }
                else
                {
                    m_Tree.UnCheckTreeNode(this);
                }
            });

            Rect.Find("Body").Find("Text").GetComponent<Text>().text = this.Text;
        }

        #endregion

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
            if (Parent != null)
            {
                int index = 0;
                foreach (var item in Parent.m_Childs)
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
            if (parent != this)
            {
                Parent.m_Childs.Remove(this);
                parent.AddChild(this);
            }
            else
            {
                throw new FrameworkException("节点不能把自己设为自己的父物体");
            }
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
            {
                foreach (var item in m_Childs)
                {
                    if (item.Text == temp[0])
                    {
                        return item;
                    }
                }
                return null;
            }

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

        #region 外部调用

        /// <summary>
        /// 添加一个父子关系
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public TreeNode AddChild(TreeNode item)
        {
            m_Childs.Add(item);
            item.Parent = this;
            return this;
        }

        /// <summary>
        /// 添加一个父子关系
        /// </summary>
        /// <param name="text">节点文字</param>
        /// <returns></returns>
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

        /// <summary>
        /// 添加一个父子关系并创建实体
        /// </summary>
        /// <param name="text">节点文字</param>
        /// <returns></returns>
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
            if (Parent != null)
            {
                Parent.m_Childs.Remove(this);
                if (Parent.ChildCount == 0)
                {
                    Parent.SetToggle(false);
                }
            }
            Root.RefreshPos();
        }

        /// <summary>
        /// 设置节点的隐藏
        /// </summary>
        public void SetHide(bool isHide = true)
        {
            Rect.GetChild(0).gameObject.SetActive(!isHide);
            Rect.GetChild(1).gameObject.SetActive(!isHide);
        }

        #endregion

        #region 循环

        public IEnumerator<TreeNode> GetEnumerator()
        {
            return m_Childs.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_Childs.GetEnumerator();
        }

        /// <summary>
        /// 获取 当前节点所在的树中 与 _id 相同的节点
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public T When<T>(System.Func<T, bool> func) where T : TreeNode
        {
            return InternalWhen(func);
        }

        private T InternalWhen<T>(System.Func<T, bool> func) where T : TreeNode
        {
            foreach (TreeNode item in m_Childs)
            {
                if (func.Invoke(item as T))
                {
                    return item as T;
                }
                else
                {
                    TreeNode node = item.InternalWhen(func);
                    if (node != null)
                        return node as T;
                }
            }
            return null;
        }

        public void Foreach(System.Action<TreeNode> action, bool incluedSelf = true)
        {
            if (incluedSelf)
                action.Invoke(this);
            Foreach(action);
        }

        /// <summary>
        /// 循环所有节点执行任务
        /// </summary>
        /// <param name="action">任务</param>
        private void Foreach(System.Action<TreeNode> action)
        {
            if (m_Childs.Count > 0)
            {
                foreach (TreeNode item in m_Childs)
                {
                    action.Invoke(item);
                    item.Foreach(action);
                }
            }
        }

        public void Foreach<T>(System.Action<T> action, bool incluedSelf = true) where T : TreeNode
        {
            if (incluedSelf)
                action.Invoke(this as T);
            Foreach<T>(action);
        }

        private void Foreach<T>(System.Action<T> action) where T : TreeNode
        {
            if (m_Childs.Count > 0)
            {
                foreach (TreeNode item in m_Childs)
                {
                    action.Invoke(item as T);
                    item.Foreach(action);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 泛型节点
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class TreeNode<T> : TreeNode
    {
        /// <summary>
        /// 节点数据
        /// </summary>
        public T data;

        public TreeNode(string name, T data) : base(name)
        {
            this.data = data;
        }
    }
}