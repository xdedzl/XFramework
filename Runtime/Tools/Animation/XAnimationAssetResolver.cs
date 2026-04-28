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
            XAnimationClipPathUtility.Split(assetPath, out string mainAssetPath, out string clipName);
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return ResourceManager.Instance.Load<AnimationClip>(mainAssetPath);
            }

            return ResourceManager.Instance.LoadSubAsset<AnimationClip>(mainAssetPath, clipName);
        }

        public AvatarMask LoadAvatarMask(string assetPath)
        {
            return ResourceManager.Instance.Load<AvatarMask>(assetPath);
        }
    }
}
