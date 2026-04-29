#if UNITY_EDITOR
using UnityEngine;

namespace XFramework.Editor
{
    [UnityEditor.InitializeOnLoad]
    internal static class XTextAssetDataTableOpener
    {
        private const string DataTableAlias = "xframework.data-table";
        private const string DataTableHasKeyAlias = "xframework.data-table-haskey";
        private const string DataTableHasAliasAlias = "xframework.data-table-hasalias";

        static XTextAssetDataTableOpener()
        {
            XTextAssetOpenRegistry.Register(DataTableAlias, OpenDataTable);
            XTextAssetOpenRegistry.Register(DataTableHasKeyAlias, OpenDataTable);
            XTextAssetOpenRegistry.Register(DataTableHasAliasAlias, OpenDataTable);
        }

        private static bool OpenDataTable(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                return false;
            }

            XDataTableEditorWindow.ShowWindow(textAsset);
            return true;
        }
    }
}
#endif
