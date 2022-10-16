using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 红点系统管理器
    /// </summary>
    public partial class ReddotManager : Singleton<ReddotManager>
    {
        private Dictionary<string, ReddotNode> m_ReddotNodeDic;

        private ReddotManager() { }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="datas">一般应由红点编辑生成的json文件获得，也可以自定义</param>
        public void Init(ReddotData[] datas)
        {
            m_ReddotNodeDic = new Dictionary<string, ReddotNode>();
            foreach (var data in datas)
            {
                if (!m_ReddotNodeDic.TryGetValue(data.key, out ReddotNode node))
                {
                    node = new ReddotNode(data.key);
                    m_ReddotNodeDic.Add(data.key, node);
                }

                if (data.children != null)
                {
                    foreach (var child in data.children)
                    {
                        if (!m_ReddotNodeDic.TryGetValue(child, out ReddotNode childNode))
                        {
                            childNode = new ReddotNode(child);
                            m_ReddotNodeDic.Add(child, childNode);
                        }
                        node.AddChild(childNode);
                    }
                }
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="jsonText">ReddotData的json字符串</param>
        public void Init(string jsonText)
        {
            var datas = JsonConvert.DeserializeObject<ReddotData[]>(jsonText);
            Init(datas);
        }

        /// <summary>
        /// 编辑节点状态
        /// </summary>
        /// <param name="key"></param>
        /// <param name="state"></param>
        /// <param name="tag"></param>
        public void MarkReddot(string key, bool state, string tag = "default")
        {
            CheckInit();
            if (!m_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                throw new XFrameworkException($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }

            reddotNode.Mark(state, tag);
        }

        /// <summary>
        /// 注册UI
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="reddot">红点组件</param>
        public void RegisterReddot(string key, Reddot reddot)
        {
            CheckInit();
            if (m_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                reddotNode.RegisterReddot(reddot);
                reddot.SetActive(reddotNode.IsActive);
            }
            else
            {
                throw new XFrameworkException($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }

        /// <summary>
        /// 移除UI
        /// </summary>
        /// <param name="key">键值</param>
        /// <param name="reddot">红点组件</param>
        public void UnRegisterReddot(string key, Reddot reddot)
        {
            CheckInit();
            if (m_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                reddotNode.UnRegisterReddot(reddot);
                reddot.SetActive(false);
            }
            else
            {
                throw new XFrameworkException($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }

        /// <summary>
        /// 获取键值对应的节点状态
        /// </summary>
        /// <param name="key">键值</param>
        /// <returns>状态</returns>
        public bool GetKeyState(string key)
        {
            CheckInit();
            if (m_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                return reddotNode.IsActive;
            }
            else
            {
                throw new XFrameworkException($"[Reddot System] there is no node which key is '{key}' in reddot tree，please add node to reddot tree or modify key");
            }
        }

        /// <summary>
        /// 在控制台打印状态
        /// </summary>
        public void DebugState()
        {
            string content = "";
            foreach (var item in m_ReddotNodeDic.Values)
            {
                content += $"key:{item.Key}, state:{item.IsActive}";
                content += "\n";
            }
            Debug.Log(content);
        }

        /// <summary>
        /// 获取所有的叶节点键值
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetLeafKeys()
        {
            List<string> keys = new List<string>();
            foreach (var item in m_ReddotNodeDic.Values)
            {
                if (item.IsLeafNode)
                {
                    keys.Add(item.Key);
                }
            }
            return keys;
        }

        /// <summary>
        /// 检查是否初始化
        /// </summary>
        private void CheckInit()
        {
            if (m_ReddotNodeDic is null)
            {
                throw new XFrameworkException("[Reddot System] please init reddot system before use it  -->  ReddotManager.Instance.Init()");
            }
        }
    }

    /// <summary>
    /// 红点数据
    /// </summary>
    public class ReddotData
    {
        public string key;
        public string[] children;
#if UNITY_EDITOR
        // 仅用于节点编辑器
        public string name;
        [JsonConverter(typeof(JsonConvter.Vector2Converter))]
        public Vector2 position;
#endif
    }
}