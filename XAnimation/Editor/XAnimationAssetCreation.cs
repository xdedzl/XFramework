#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework.Animation;

namespace XFramework.Editor
{
    internal static class XAnimationAssetCreation
    {
        private const string CreateAssetMenuPath = "Assets/Create/XAnimation/XAnimation Asset";
        private const string CreateOverrideMenuPath = "Assets/Create/XAnimation/XAnimation Override Asset";

        [MenuItem(CreateAssetMenuPath, false, 2000)]
        private static void CreateAnimationAsset()
        {
            string directoryPath = ResolveTargetDirectoryPath();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(directoryPath, $"NewXAnimation{XAnimationAssetUtility.AnimationAssetExtension}"));

            XAnimationAsset asset = new();
            CreateAssetFile(assetPath, asset.Serialize());
        }

        [MenuItem(CreateOverrideMenuPath, false, 2001)]
        private static void CreateAnimationOverrideAsset()
        {
            string directoryPath = ResolveTargetDirectoryPath();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(directoryPath, $"NewXAnimationOverride{XAnimationAssetUtility.AnimationOverrideExtension}"));

            XAnimationOverrideAsset asset = new XAnimationOverrideAsset
            {
                baseAssetPath = ResolveSelectedBaseAssetPath(),
            };
            CreateAssetFile(assetPath, asset.Serialize());
        }

        [MenuItem(CreateAssetMenuPath, true)]
        [MenuItem(CreateOverrideMenuPath, true)]
        private static bool ValidateCreateAsset()
        {
            return !string.IsNullOrWhiteSpace(ResolveTargetDirectoryPath());
        }

        private static string ResolveTargetDirectoryPath()
        {
            Object activeObject = Selection.activeObject;
            if (activeObject == null)
            {
                return "Assets";
            }

            string selectedPath = AssetDatabase.GetAssetPath(activeObject);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return "Assets";
            }

            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return selectedPath;
            }

            string directoryPath = Path.GetDirectoryName(selectedPath);
            return string.IsNullOrWhiteSpace(directoryPath) ? "Assets" : directoryPath.Replace('\\', '/');
        }

        private static string ResolveSelectedBaseAssetPath()
        {
            Object activeObject = Selection.activeObject;
            if (activeObject == null)
            {
                return string.Empty;
            }

            string selectedPath = AssetDatabase.GetAssetPath(activeObject);
            if (string.IsNullOrWhiteSpace(selectedPath) ||
                !selectedPath.EndsWith(XAnimationAssetUtility.AnimationAssetExtension))
            {
                return string.Empty;
            }

            return selectedPath.Replace('\\', '/');
        }

        private static void CreateAssetFile(string assetPath, string content)
        {
            string normalizedAssetPath = assetPath.Replace('\\', '/');
            File.WriteAllText(normalizedAssetPath, content);
            AssetDatabase.Refresh();

            TextAsset createdAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(normalizedAssetPath);
            if (createdAsset != null)
            {
                Selection.activeObject = createdAsset;
                EditorGUIUtility.PingObject(createdAsset);
            }
        }
    }
}
#endif
