#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using XFramework.Animation;

namespace XFramework.Editor
{
    internal sealed class XAnimationEditorAssetResolver : IXAnimationAssetResolver
    {
        public TextAsset LoadTextAsset(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }

        public AnimationClip LoadAnimationClip(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        public AvatarMask LoadAvatarMask(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<AvatarMask>(assetPath);
        }
    }
}
#endif
