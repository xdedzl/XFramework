#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework.Animation;

namespace XFramework.Editor
{
    internal sealed class XAnimationEditorAssetResolver : IXAnimationAssetResolver
    {
        public static AnimationClip ResolveAnimationClip(string clipPath)
        {
            XAnimationClipPathUtility.Split(clipPath, out string assetPath, out string clipName);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(clipName))
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                for (int i = 0; i < assets.Length; i++)
                {
                    if (assets[i] is AnimationClip animationClip &&
                        !animationClip.name.Contains("__preview__") &&
                        string.Equals(animationClip.name, clipName, StringComparison.Ordinal))
                    {
                        return animationClip;
                    }
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        public static string BuildClipPath(AnimationClip clip)
        {
            if (clip == null)
            {
                return string.Empty;
            }

            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            if (!string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                return assetPath;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            int clipCount = 0;
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip animationClip && !animationClip.name.Contains("__preview__"))
                {
                    clipCount++;
                }
            }

            return clipCount > 1 ? XAnimationClipPathUtility.Compose(assetPath, clip.name) : assetPath;
        }

        public TextAsset LoadTextAsset(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        }

        public AnimationClip LoadAnimationClip(string assetPath)
        {
            return ResolveAnimationClip(assetPath);
        }

        public AvatarMask LoadAvatarMask(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<AvatarMask>(assetPath);
        }
    }
}
#endif
