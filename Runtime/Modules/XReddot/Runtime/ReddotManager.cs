using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace XReddot
{
    /// <summary>
    /// 红点系统管理器
    /// </summary>
    public static partial class ReddotManager
    {
        public const string RED_DOT_TREE_ASSET_PATH = "Assets/Resources/XReddot/reddot_tree.asset";
        public const string RED_DOT_TREE_RESOURCES_PATH = "XReddot/reddot_tree";
        
        public const string DEFAULT_RED_DOT_TAG = "__default__";

        private static Dictionary<string, ReddotNode> s_ReddotNodeDic;
        private static string s_SaveName = "";

        public static event Action<string, bool> onNodeStateChange;
        public static event Action onReddotTreeLoad;

        static ReddotManager()
        {
            Reload();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="datas">一般应由红点编辑生成的json文件获得，也可以自定义</param>
        private static void Init(ReddotData[] dates)
        {
            s_ReddotNodeDic = new Dictionary<string, ReddotNode>();
            foreach (var data in dates)
            {
                if (!s_ReddotNodeDic.TryGetValue(data.key, out ReddotNode node))
                {
                    node = new ReddotNode(data.key);
                    s_ReddotNodeDic.Add(data.key, node);
                }

                if (data.children != null)
                {
                    foreach (var child in data.children)
                    {
                        if (!s_ReddotNodeDic.TryGetValue(child, out ReddotNode childNode))
                        {
                            childNode = new ReddotNode(child);
                            s_ReddotNodeDic.Add(child, childNode);
                        }
                        node.AddChild(childNode);
                    }
                }
            }
            LoadMark();
        }

        public static void Reload()
        {
            var reddotTree = Resources.Load<ReddotTreeAsset>(RED_DOT_TREE_RESOURCES_PATH);
            onReddotTreeLoad?.Invoke();
            Init(reddotTree.items);
        }

        private static void LoadMark()
        {
            ClearMark();
            var key = string.IsNullOrEmpty(s_SaveName) ? "reddot" : $"{s_SaveName}_reddot";
            var reddotCache = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(reddotCache))
            {
                return;
            }
            
            var nodeMarks = reddotCache.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var nodeMark in nodeMarks)
            {
                var keyAndTags = nodeMark.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (keyAndTags.Length != 2)
                {
                    continue;
                }

                var nodeKey = keyAndTags[0];
                var tags = keyAndTags[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (s_ReddotNodeDic.TryGetValue(nodeKey, out ReddotNode node))
                {
                    foreach (var tag in tags)
                    {
                        node.Mark(true, tag);
                    }
                }
            }
        }

        public static void SaveMark()
        {
            var key = string.IsNullOrEmpty(s_SaveName) ? "reddot" : $"{s_SaveName}_reddot";
            var sb = new StringBuilder();
            foreach (var node in s_ReddotNodeDic.Values)
            {
                sb.Append(node.Key);
                sb.Append(":");
                sb.Append(string.Join(",", node.GetTags()));
                sb.Append(";");
            }
            PlayerPrefs.SetString(key, sb.ToString());
        }

        private static void ClearMark()
        {
            foreach (var node in s_ReddotNodeDic.Values)
            {
                node.ClearMark();
            }
        }
        
        /// <summary>
        /// 设置当前存档,不同存档存到不同的文件
        /// </summary>
        public static void SetSaveName(string name)
        {
            s_SaveName = name;
            LoadMark();
        }

        /// <summary>
        /// 编辑节点状态
        /// </summary>
        /// <param name="key"></param>
        /// <param name="state"></param>
        /// <param name="tag"></param>
        public static void MarkReddot(string key, bool state, string tag = DEFAULT_RED_DOT_TAG)
        {
            CheckInit();
            if (!s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }

            reddotNode.Mark(state, tag);
            
            // todo 优化存储时机
            SaveMark();
        }
        
        /// <summary>
        /// 清空一个节点的所有标记
        /// </summary>
        public static void ClearNodeMark(string key)
        {
            CheckInit();
            if (!s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }

            reddotNode.ClearMark();
            
            // todo 优化存储时机
            SaveMark();
        }
        
        /// <summary>
        /// 注册UI
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="reddot">红点组件</param>
        public static void RegisterReddot(string key, Reddot reddot)
        {
            CheckInit();
            if (s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                reddotNode.RegisterReddot(reddot);
            }
            else
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }

        /// <summary>
        /// 移除UI
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="reddot">红点组件</param>
        public static void UnRegisterReddot(string key, Reddot reddot)
        {
            CheckInit();
            if (s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                reddotNode.UnRegisterReddot(reddot);
                reddot.SetActive(false);
            }
            else
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }
        
        /// <summary>
        /// 是否包含该节点
        /// </summary>
        public static bool ContainsNode(string key)
        {
            return s_ReddotNodeDic.ContainsKey(key);
        }
        
        /// <summary>
        /// 获取节点状态
        /// </summary>
        public static bool GetNodeIsActive(string key)
        {
            CheckInit();
            if (s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                return reddotNode.IsActive;
            }
            else
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }
        
        /// <summary>
        /// 获取节点的对应tag是否处于激活状态
        /// </summary>
        public static bool GetNodeTagIsActive(string key, string tag)
        {
            CheckInit();
            if (s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                return reddotNode.IsTagActive(tag);
            }
            else
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }

        /// <summary>
        /// 获取一个节点所有激活的标签
        /// </summary>
        public static IList<string> GetNodeTags(string key)
        {
            CheckInit();
            if (s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                var tags = reddotNode.GetTags();
                return tags;
            }
            else
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }
        
        /// <summary>
        /// 在控制台打印状态
        /// </summary>
        public static void DebugState(params string[] nodeKeys)
        {
            string content = "";
            foreach (var item in s_ReddotNodeDic.Values)
            {
                if (nodeKeys is not null || nodeKeys.Length == 0 || nodeKeys.Contains(item.Key))
                {
                    content += item.ToString();
                    content += "\n";
                }
            }
            Debug.Log(content);
        }

        public static string GetNodeDebugString(string key)
        {
            if (s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                return reddotNode.ToString();
            }
            else
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }
        
        /// <summary>
        /// 是否为叶节点
        /// </summary>
        public static bool GetNodeIsLeaf(string key)
        {
            if (s_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                return reddotNode.IsLeafNode;
            }
            else
            {
                throw new Exception($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }
        
        /// <summary>
        /// 获取所有的叶节点键值
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetLeafKeys()
        {
            var keys = new List<string>();
            foreach (var item in s_ReddotNodeDic.Values)
            {
                if (item.IsLeafNode)
                {
                    keys.Add(item.Key);
                }
            }
            return keys;
        }
        
        /// <summary>
        /// 获取所有节点键值
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<string> GetAllKeys()
        {
            return s_ReddotNodeDic.Keys;
        }

        /// <summary>
        /// 检查是否初始化
        /// </summary>
        private static void CheckInit()
        {
            if (s_ReddotNodeDic is null)
            {
                throw new Exception("[Reddot System] please init reddot system before use it  -->  ReddotManager.Instance.Init()");
            }
        }
    }

    /// <summary>
    /// 红点数据
    /// </summary>
    [Serializable] 
    public class ReddotData
    {
        public string key;
        public string[] children;
#if UNITY_EDITOR
        // 仅用于节点编辑器
        public string name;
        public Vector2 position;
#endif
    }
}