using UnityEngine;

namespace XFramework.Animation
{
    public interface IXAnimationAssetResolver
    {
        TextAsset LoadTextAsset(string assetPath);
        AnimationClip LoadAnimationClip(string assetPath);
        AvatarMask LoadAvatarMask(string assetPath);
    }

    public sealed class XAnimationRuntimeAssetResolver : IXAnimationAssetResolver
    {
        public TextAsset LoadTextAsset(string assetPath)
        {
            return XAnimation.Load<TextAsset>(assetPath);
        }

        public AnimationClip LoadAnimationClip(string assetPath)
        {
            XAnimationClipPathUtility.Split(assetPath, out string mainAssetPath, out string clipName);
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return XAnimation.Load<AnimationClip>(mainAssetPath);
            }

            return XAnimation.LoadSubAsset<AnimationClip>(mainAssetPath, clipName);
        }

        public AvatarMask LoadAvatarMask(string assetPath)
        {
            return XAnimation.Load<AvatarMask>(assetPath);
        }
    }
}
