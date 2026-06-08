#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using XFramework.Editor;
using XFramework.Resource;

namespace XFramework.NodeKit.Editor
{
    [InitializeOnLoad]
    internal static class XTextAssetNodeKitAssetOpener
    {
        private const string NodeGraphAlias = "xframework.node-graph";

        static XTextAssetNodeKitAssetOpener()
        {
            XTextAssetOpenRegistry.Register(NodeGraphAlias, OpenNodeGraphAsset);
        }

        private static bool OpenNodeGraphAsset(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                return false;
            }

            XNodeGraphAsset graphAsset = textAsset.ToXTextAsset<XNodeGraphAsset>();
            if (graphAsset == null)
            {
                return false;
            }

            return XNodeGraphEditorWindow.OpenGraphAsset(graphAsset, AssetDatabase.GetAssetPath(textAsset)) != null;
        }
    }
}
#endif
