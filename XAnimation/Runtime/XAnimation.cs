using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UObject = UnityEngine.Object;

namespace XFramework.Animation
{
    public static class XAnimation
    {
        public delegate UObject LoadAssetDelegate(string assetPath, Type assetType);
        public delegate UObject LoadSubAssetDelegate(string assetPath, string subAssetName, Type assetType);

        private static LoadAssetDelegate s_LoadAsset;
        private static LoadSubAssetDelegate s_LoadSubAsset;

        public static void SetAssetLoaders(
            LoadAssetDelegate loadAsset,
            LoadSubAssetDelegate loadSubAsset)
        {
            s_LoadAsset = loadAsset ?? throw new ArgumentNullException(nameof(loadAsset));
            s_LoadSubAsset = loadSubAsset ?? throw new ArgumentNullException(nameof(loadSubAsset));
        }

        public static void ClearAssetLoaders()
        {
            s_LoadAsset = null;
            s_LoadSubAsset = null;
        }

        public static T Load<T>(string assetPath) where T : UObject
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
#else
            return LoadRuntimeAsset(assetPath, typeof(T)) as T;
#endif
        }

        public static T LoadSubAsset<T>(string assetPath, string subAssetName) where T : UObject
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(subAssetName))
            {
                return Load<T>(assetPath);
            }

#if UNITY_EDITOR
            UObject[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (UObject asset in assets)
            {
                if (asset is T typedAsset && asset.name == subAssetName)
                {
                    return typedAsset;
                }
            }

            return null;
#else
            return LoadRuntimeSubAsset(assetPath, subAssetName, typeof(T)) as T;
#endif
        }

#if !UNITY_EDITOR
        private static UObject LoadRuntimeAsset(string assetPath, Type assetType)
        {
            if (s_LoadAsset == null)
            {
                throw new XAnimationException("XAnimation asset loader is not set. Call XAnimation.SetAssetLoaders before loading assets by path.");
            }

            return s_LoadAsset(assetPath, assetType);
        }

        private static UObject LoadRuntimeSubAsset(string assetPath, string subAssetName, Type assetType)
        {
            if (s_LoadSubAsset == null)
            {
                throw new XAnimationException("XAnimation sub-asset loader is not set. Call XAnimation.SetAssetLoaders before loading sub-assets by path.");
            }

            return s_LoadSubAsset(assetPath, subAssetName, assetType);
        }
#endif
    }
}
