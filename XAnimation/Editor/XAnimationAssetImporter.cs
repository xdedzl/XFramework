using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using XFramework.Animation;

namespace XFramework.Editor
{
    public abstract class XAnimationAssetImporterBase : ScriptedImporter
    {
        private const string DefaultIconPath = "Packages/com.xdedzl.xframework/XAnimation/Editor/Assets/xasset-icon.png";
        private const string AnimationIconPath = "Packages/com.xdedzl.xframework/XAnimation/Editor/Assets/xasset-aniamtion-icon.png";
        private const string AnimationOverrideIconPath = "Packages/com.xdedzl.xframework/XAnimation/Editor/Assets/xasset-aniamtion-override-icon.png";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            TextAsset textAsset = new(text);
            Texture2D icon = ResolveIcon(text);
            if (icon != null)
            {
                ctx.AddObjectToAsset("main obj", textAsset, icon);
            }
            else
            {
                ctx.AddObjectToAsset("main obj", textAsset);
            }

            ctx.SetMainObject(textAsset);
        }

        private static Texture2D ResolveIcon(string text)
        {
            string iconPath = DefaultIconPath;
            if (XAnimationAssetUtility.TryReadMetaInfo(text, out XAnimationMetaInfo metaInfo))
            {
                if (string.Equals(metaInfo.typeAlias, XAnimationAssetUtility.AnimationAssetAlias, System.StringComparison.Ordinal))
                {
                    iconPath = AnimationIconPath;
                }
                else if (string.Equals(metaInfo.typeAlias, XAnimationAssetUtility.AnimationOverrideAlias, System.StringComparison.Ordinal))
                {
                    iconPath = AnimationOverrideIconPath;
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }
    }

    [ScriptedImporter(1, "xanimation")]
    public class XAnimationAssetImporter : XAnimationAssetImporterBase
    {
    }

    [ScriptedImporter(1, "xanimationoverride")]
    public class XAnimationOverrideAssetImporter : XAnimationAssetImporterBase
    {
    }
}
