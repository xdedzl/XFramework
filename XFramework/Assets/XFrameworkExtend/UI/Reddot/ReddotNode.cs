using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XFramework.UI
{
    public partial class ReddotManager
    {
        /// <summary>
        /// 红点树节点
        /// </summary>
        private class ReddotNode
        {
            private bool m_isActive;
            private List<ReddotNode> m_children;
            private List<ReddotNode> m_parents;
            private List<ReddotTag> m_tags;
            private List<Reddot> m_Reddots;
            public string Key { get; private set; }

            public bool IsActive
            {
                get
                {
                    return m_isActive;
                }
                private set
                {
                    if (m_isActive != value)
                    {
                        m_isActive = value;

                        if (m_Reddots != null)
                        {
                            foreach (var reddot in m_Reddots)
                            {
                                reddot.SetActive(IsActive);
                            }
                        }

                        if (m_parents != null)
                        {
                            foreach (var node in m_parents)
                            {
                                if (value)
                                {
                                    node.IsActive = true;
                                }
                                else
                                {
                                    if (node.m_children != null)
                                    {
                                        foreach (var item in node.m_children)
                                        {
                                            if (item.IsActive)
                                            {
                                                return;
                                            }
                                        }
                                        node.IsActive = false;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public bool IsLeafNode
            {
                get
                {
                    return m_children == null || m_children.Count == 0;
                }
            }

            public ReddotNode(string key)
            {
                this.Key = key;
            }

            public void Mark(bool state, string tag)
            {
                if (!IsLeafNode)
                {
                    throw new XFrameworkException($"[红点系统] 不允许标记非叶子节点 节点key为[{Key}]");
                }

                if (m_tags is null)
                {
                    m_tags = new List<ReddotTag>();
                }


                ReddotTag reddotState = null;
                for (int i = 0; i < m_tags.Count; i++)
                {
                    if (m_tags[i].tag == tag)
                    {
                        reddotState = m_tags[i];
                        reddotState.state = state;
                    }
                }

                if (reddotState is null)
                {
                    reddotState = new ReddotTag
                    {
                        tag = tag,
                        state = state,
                    };
                    m_tags.Add(reddotState);
                }


                foreach (var item in m_tags)
                {
                    if (item.state)
                    {
                        IsActive = true;
                        return;
                    }
                }
                IsActive = false;
            }

            public void RegisterReddot(Reddot reddot)
            {
                if (m_Reddots is null)
                {
                    m_Reddots = new List<Reddot>();
                }

                if (!m_Reddots.Contains(reddot))
                {
                    m_Reddots.Add(reddot);
                }
            }

            public void UnRegisterReddot(Reddot reddot)
            {
                m_Reddots?.Remove(reddot);
            }

            public void AddChild(ReddotNode node)
            {
                if (m_children is null)
                {
                    m_children = new List<ReddotNode>();
                }
                m_children.Add(node);

                if (node.m_parents is null)
                {
                    node.m_parents = new List<ReddotNode>();
                }
                node.m_parents.Add(this);
            }

            private class ReddotTag
            {
                public bool state;
                public string tag;
            }
        }
    }
}