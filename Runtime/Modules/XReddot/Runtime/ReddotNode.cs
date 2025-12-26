using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace XReddot
{
    public static partial class ReddotManager
    {
        /// <summary>
        /// 红点树节点
        /// </summary>
        private class ReddotNode
        {
            private bool m_isActive;
            private List<ReddotNode> m_children = new ();
            private List<ReddotNode> m_parents = new ();
            private HashSet<string> m_tags = new ();
            private readonly Dictionary<string, HashSet<Reddot>> m_tag2Reddots = new ();
            public string Key { get; private set; }

            public bool IsActive
            {
                get => m_isActive;
                private set
                {
                    if (m_isActive != value)
                    {
                        m_isActive = value;
                        
                        if (m_tag2Reddots != null)
                        {
                            foreach (var reddots in m_tag2Reddots)
                            {
                                foreach (var reddot in reddots.Value)
                                {
                                    UpdateReddotActive(reddot);
                                }
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
            
            public bool IsLeafNode => m_children == null || m_children.Count == 0;

            public ReddotNode(string key)
            {
                this.Key = key;
            }

            public override string ToString()
            {
                return $"key: {Key}, state: {IsActive}, tags: [{string.Join(",", m_tags)}]";
            }

            public void Mark(bool state, string tag)
            {
                if (string.IsNullOrEmpty(tag))
                {
                    Debug.LogError($"[红点系统] 标记tag不能为空 节点key为[{Key}] 请检查Reddot组件是否为active");
                    return;
                }
                if (!IsLeafNode)
                {
                    Debug.LogError($"[红点系统] 不允许标记非叶子节点 节点key为[{Key}] 请检查Reddot组件是否为active");
                    return;
                }

                m_tags ??= new HashSet<string>();

                if (state)
                {
                    m_tags.Add(tag);
                    IsActive = true;
                }
                else
                {
                    m_tags.Remove(tag);
                    if (m_tags.Count <= 0)
                    {
                        IsActive = false;
                    }
                }
                
                UpdateReddotActiveWidthTag(tag);
                PlayerPrefs.GetString("reddot");
            }
            
            public void ClearMark()
            {
                m_isActive = false;
                m_tags?.Clear();
                UpdateAllReddotActive();
            }
            
            public void RegisterReddot(Reddot reddot)
            {
                var tag = reddot.LeafTag;
                if (!m_tag2Reddots.ContainsKey(tag))
                {
                    m_tag2Reddots[tag] = new HashSet<Reddot>();
                }
                
                var reddots = m_tag2Reddots[tag];
                reddots.Add(reddot);
                UpdateReddotActive(reddot);
            }

            public void UnRegisterReddot(Reddot reddot)
            {
                var tag = reddot.LeafTag;
                if (!m_tag2Reddots.ContainsKey(tag))
                {
                    Debug.LogError($"[红点系统] 尝试移除未注册的reddot组件  Tag: {tag}");
                    return;
                }
                
                var reddots = m_tag2Reddots[tag];
                if (!reddots.Contains(reddot))
                {
                    Debug.LogError("[红点系统] 尝试移除未注册的reddot组件");
                }
                reddots.Remove(reddot);
                UpdateReddotActive(reddot);
            }

            private void UpdateAllReddotActive()
            {
                foreach (var reddots in m_tag2Reddots)
                {
                    foreach (var reddot in reddots.Value)
                    {
                        UpdateReddotActive(reddot);
                    }
                }
            }
            
            private void UpdateReddotActiveWidthTag(string tag)
            {
                if (m_tag2Reddots.TryGetValue(tag, out var reddots))
                {
                    foreach (var reddot in reddots)
                    {
                        UpdateReddotActive(reddot);
                    }
                }
            }
            
            private void UpdateReddotActive(Reddot reddot)
            {
                if (!IsLeafNode)
                {
                    reddot.SetActive(IsActive);
                }
                else
                {
                    if (IsActive)
                    {
                        reddot.SetActive(IsTagActive(reddot.LeafTag));
                    }
                    else
                    {
                        reddot.SetActive(false);
                    }
                }
            }
            
            public bool IsTagActive(string tag)
            {
                if (!IsLeafNode)
                {
                    return false;
                }

                if (tag == DEFAULT_RED_DOT_TAG)
                {
                    return IsActive;
                }
                
                if (m_tags == null)
                {
                    return false;
                }
                
                return m_tags.Contains(tag);
            }

            public void AddChild(ReddotNode node)
            {
                m_children ??= new List<ReddotNode>();
                m_children.Add(node);

                node.m_parents ??= new List<ReddotNode>();
                node.m_parents.Add(this);
            }

            public IList<string> GetTags()
            {
                return m_tags?.ToList();
            }
        }
    }
}