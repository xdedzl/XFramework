using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.NodeKit
{
    public abstract class GraphRefNodeBase : ProcessNode
    {
        public abstract string GetGraphAssetPath();

        public override void OnNodeStart(IXNodeGraph storyGraph)
        {
            string graphAssetPath = GetGraphAssetPath();
            if (string.IsNullOrWhiteSpace(graphAssetPath))
            {
                Debug.LogError($"[GraphRefNodeBase] Node {GetId()} graph asset path is empty.");
                storyGraph.FinishNode(this);
                return;
            }

            TextAsset textAsset;
            try
            {
                textAsset = ResourceManager.Instance.Load<TextAsset>(graphAssetPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphRefNodeBase] Load graph text asset failed: {graphAssetPath}. {e.Message}");
                storyGraph.FinishNode(this);
                return;
            }

            if (textAsset == null)
            {
                Debug.LogError($"[GraphRefNodeBase] Load graph text asset failed: {graphAssetPath}");
                storyGraph.FinishNode(this);
                return;
            }

            XNodeGraphAsset graphAsset;
            try
            {
                graphAsset = textAsset.ToXTextAsset<XNodeGraphAsset>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphRefNodeBase] Parse graph asset failed: {graphAssetPath}. {e.Message}");
                storyGraph.FinishNode(this);
                return;
            }

            if (graphAsset == null)
            {
                Debug.LogError($"[GraphRefNodeBase] Graph asset is null after parse: {graphAssetPath}");
                storyGraph.FinishNode(this);
                return;
            }

            string runtimeGraphId;
            try
            {
                runtimeGraphId = XNodeManager.Instance.StartNodeGraph(graphAsset);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GraphRefNodeBase] Start graph failed: {graphAssetPath}. {e.Message}");
                storyGraph.FinishNode(this);
                return;
            }

            if (string.IsNullOrEmpty(runtimeGraphId))
            {
                Debug.LogError($"[GraphRefNodeBase] Runtime graph id is invalid for path={graphAssetPath}.");
                storyGraph.FinishNode(this);
                return;
            }

            storyGraph.FinishNode(this);
        }

        public override IEnumerable<string> GetNextNodeIds(IXNodeGraph storyGraph, params object[] args)
        {
            return nextNodeIds ?? Array.Empty<string>();
        }
    }

    public class GraphRefNode : GraphRefNodeBase
    {
        [InspectorName("Graph资源路径")]
        [AssetPath(typeof(TextAsset))]
        public string graphAssetPath;

        public override string GetGraphAssetPath()
        {
            return graphAssetPath;
        }
    }
}
