using UnityEngine;
using XFramework.Resource;

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
            return ResourceManager.Instance.Load<TextAsset>(assetPath);
        }

        public AnimationClip LoadAnimationClip(string assetPath)
        {
            return ResourceManager.Instance.Load<AnimationClip>(assetPath);
        }

        public AvatarMask LoadAvatarMask(string assetPath)
        {
            return ResourceManager.Instance.Load<AvatarMask>(assetPath);
        }
    }
}
