using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.Data.Editor
{
    [ScriptedImporter(2, "xasset")]
    public class XTextAssetCreator : ScriptedImporter
    {
        private const string DefaultIconPath = "Packages/com.xdedzl.xframework/Editor/Tools/Animation/xasset-icon.png";
        private const string DataTableIconPath = "Packages/com.xdedzl.xframework/Editor/Tools/Animation/xasset-table-icon.png";
        private const string AnimationIconPath = "Packages/com.xdedzl.xframework/Editor/Tools/Animation/xasset-aniamtion-icon.png";
        private const string AnimationOverrideIconPath = "Packages/com.xdedzl.xframework/Editor/Tools/Animation/xasset-aniamtion-override-icon.png";

        private const string DataTableAlias = "xframework.data-table";
        private const string DataTableHasKeyAlias = "xframework.data-table-haskey";
        private const string DataTableHasAliasAlias = "xframework.data-table-hasalias";
        private const string AnimationAssetAlias = "xframework.animation.asset";
        private const string AnimationOverrideAlias = "xframework.animation.override";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string text = File.ReadAllText(ctx.assetPath);
            var textAsset = new TextAsset(text);
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
            if (XTextUtility.TryReadMetaInfo(text, out XTextMetaInfo metaInfo))
            {
                switch (metaInfo.typeAlias)
                {
                    case DataTableAlias:
                    case DataTableHasKeyAlias:
                    case DataTableHasAliasAlias:
                        iconPath = DataTableIconPath;
                        break;
                    case AnimationAssetAlias:
                        iconPath = AnimationIconPath;
                        break;
                    case AnimationOverrideAlias:
                        iconPath = AnimationOverrideIconPath;
                        break;
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }
    }
}
