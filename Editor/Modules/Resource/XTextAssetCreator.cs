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
        private const string DefaultIconPath = "Packages/com.xdedzl.xframework/Editor/Assets/xasset-icon.png";
        private const string DataTableIconPath = "Packages/com.xdedzl.xframework/Editor/Assets/xasset-table-icon.png";

        private const string DataTableAlias = "xframework.data-table";
        private const string DataTableHasKeyAlias = "xframework.data-table-haskey";
        private const string DataTableHasAliasAlias = "xframework.data-table-hasalias";

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
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }
    }
}
