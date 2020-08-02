using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 红点系统管理器
    /// </summary>
    public class ReddotManager : Singleton<ReddotManager>
    {
        private readonly Dictionary<string, ReddotNode> m_ReddotNodeDic = new Dictionary<string, ReddotNode>();

        private ReddotManager()
        {
            string path = @"D:\Projects\XDEDZL\XFramework\XFramework\Assets\XFramework\Extend\Core\Modules\UI\Reddot\Demo\ReddotData.json";
            var datas = JsonConvert.DeserializeObject<ReddotData[]>(File.ReadAllText(path));
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

        public void MarkReddot(string key, bool state, string tag = "default")
        {
            if (!m_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                throw new XFrameworkException($"[红点系统] 红点树中没有key为[{key}]的节点，请添加对应节点或更改key");
            }

            reddotNode.Mark(state, tag);
        }

        public void RegisterReddot(string key, Reddot reddot)
        {
            if (m_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                reddotNode.RegisterReddot(reddot);
            }
            else
            {
                throw new XFrameworkException($"[红点系统] 红点树中没有key为[{key}]的节点，请添加对应节点或更改key");
            }
        }

        public void UnRegisterReddot(string key, Reddot reddot)
        {
            if (m_ReddotNodeDic.TryGetValue(key, out ReddotNode reddotNode))
            {
                reddotNode.UnRegisterReddot(reddot);
            }
            else
            {
                throw new XFrameworkException($"[红点系统] 红点树中没有key为[{key}]的节点，请添加对应节点或更改key");
            }
        }

        public void DebugState()
        {
            string content = "";
            foreach (var item in m_ReddotNodeDic.Values)
            {
                content += $"key:{item.key}, state:{item.IsActive}";
                content += "\n";
            }
            Debug.Log(content);
        }

        public IEnumerable<string> GetLeafKeys()
        {
            List<string> keys = new List<string>();
            foreach (var item in m_ReddotNodeDic.Values)
            {
                if (item.IsLeafNode)
                {
                    keys.Add(item.key);
                }
            }
            return keys;
        }
    }

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