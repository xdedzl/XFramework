using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace XFramework.Data.Editor
{
    [ScriptedImporter(1, "xasset")]
    public class XTextAssetCreator : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var textAsset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("main obj", textAsset);
            ctx.SetMainObject(textAsset);
        }
    }
}
