using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using XFramework.Resource;
using XFramework.Json;

namespace XFramework.NodeKit
{
    #if UNITY_EDITOR
    [Serializable]
    public struct NodePositon
    {
        public string nodeId;
        public Vector2 position;
    }
    #endif
    
    [XTextAssetAlias("xframework.node-graph")]
    public class XNodeGraphAsset : XTextAsset
    {
        public string[] entries;
        [JsonConverter(typeof(PolyListConverter<IXNode>))]
        public IXNode[] nodes;
#if UNITY_EDITOR
        public NodePositon[] nodePositions;
#endif
        
#if UNITY_EDITOR
        public IDictionary<string, Vector2> GetNodePositionDict()
        {
            var dict = new Dictionary<string, Vector2>();
            if (nodePositions != null)
            {
                foreach (var nodePos in nodePositions)
                {
                    dict[nodePos.nodeId] = nodePos.position;
                }
            }
            return dict;
        }
#endif
    } 
}
