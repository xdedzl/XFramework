#if UNITY_EDITOR
using UnityEngine;

namespace XFramework.Editor
{
    [UnityEditor.InitializeOnLoad]
    internal static class XTextAssetAnimationAssetOpener
    {
        private const string AnimationAssetAlias = "xframework.animation.asset";
        private const string AnimationOverrideAlias = "xframework.animation.override";

        static XTextAssetAnimationAssetOpener()
        {
            XTextAssetOpenRegistry.Register(AnimationAssetAlias, OpenAnimationAsset);
            XTextAssetOpenRegistry.Register(AnimationOverrideAlias, OpenAnimationAsset);
        }

        private static bool OpenAnimationAsset(TextAsset textAsset)
        {
            if (textAsset == null || !XFramework.Animation.XAnimationAssetLoader.IsXAnimationAssetText(textAsset.text))
            {
                return false;
            }

            XAnimationPreviewWindow existingWindow = XAnimationPreviewWindow.HasOpenInstances<XAnimationPreviewWindow>()
                ? XAnimationPreviewWindow.GetWindow<XAnimationPreviewWindow>()
                : null;
            GameObject prefab = existingWindow?.CurrentSelectedPrefab;
            XAnimationPreviewWindow.ShowWindow(textAsset, prefab, autoLoad: true);
            return true;
        }
    }
}
#endif
