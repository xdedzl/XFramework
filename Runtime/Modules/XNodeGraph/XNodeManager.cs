using System;
using System.Collections.Generic;
using XFramework;
using UnityEngine;

namespace XFramework.NodeKit
{
    /// <summary>
    /// 剧情管理
    /// </summary>
    [ModuleLifecycle(ModuleLifecycle.Persistent)]
    public class XNodeManager : GameModuleBase<XNodeManager>
    {
        /// <summary>
        /// 用于处理故事的处理器集合
        /// </summary>
        private readonly Dictionary<string, XNodeGraph> m_RunningGraphs = new Dictionary<string, XNodeGraph>();
        
        public event Action<string, bool> OnGraphStop;
        public event Action<string> OnGraphStart;

        public string StartNodeGraph(XNodeGraphAsset asset)
        {
            return StartNodeGraph(asset, null);
        }

        public string StartNodeGraph(XNodeGraphAsset asset, Action<string> onGraphCreated)
        {
            var storyGraph = new XNodeGraph(this, asset);
            m_RunningGraphs.Add(storyGraph.id, storyGraph);
            onGraphCreated?.Invoke(storyGraph.id);
            storyGraph.Start();
            if (m_RunningGraphs.ContainsKey(storyGraph.id))
            {
                OnGraphStart?.Invoke(storyGraph.id);
            }

            return storyGraph.id;
        }

        public bool IsGraphRunning(string id)
        {
            return m_RunningGraphs.ContainsKey(id);
        }

        public void StopGraph(string id)
        {
            StopGraph(id, false);
        }

        public void StopGraph(string id, bool isCompleted)
        {
            if (m_RunningGraphs.Remove(id))
            {
                OnGraphStop?.Invoke(id, isCompleted);
            }
        }
    }
}
