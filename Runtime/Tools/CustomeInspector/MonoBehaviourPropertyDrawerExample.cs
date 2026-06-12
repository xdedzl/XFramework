using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Data;

namespace XFramework
{
    /// <summary>
    /// PropertyDrawer 示例组件。
    /// </summary>
    public class MonoBehaviourPropertyDrawerExample : MonoBehaviour
    {
        [Serializable]
        public struct RowExample
        {
            public int id;
            public string name;
            public float weight;
        }

        [Serializable]
        public struct BoxExample
        {
            public int id;
            public string text;
            public Vector3 position;
        }

        [Serializable]
        public struct InlineExample
        {
            public int x;
            public int y;
            public int z;
        }

        [Flags]
        public enum FlagExample
        {
            None = 0,
            A = 1 << 0,
            B = 1 << 1,
            C = 1 << 2
        }

        [Serializable]
        public sealed class DataRefExampleData : IDataHasAlias<int>
        {
            public int id;
            public string alias;
            public string name;

            public int PrimaryKey => id;
            public string Alias => alias;
        }

        [Serializable]
        [DataResourcePath("Assets/ABRes/Data/PropertyDrawerExampleDataTable.xasset")]
        [TargetDataType(typeof(DataRefExampleData))]
        public sealed class DataRefExampleTable : XDataTableHasAlias<int, DataRefExampleData>
        {
        }

        [ColorAttribute(0.95f, 0.45f, 0.25f, 1f)]
        public int color;

        [Display(nameof(ShouldDisplay))]
        public string display;

        [Enable(nameof(ShouldEnable))]
        public string enable = "Toggle Should Enable";

        [ReadOnly]
        public string disallowEdit = "Only read in Inspector";

        [ReadOnly]
        public int readOnly = 42;

        [FilePath("txt")]
        public string filePath = "Assets/readme.txt";

        [FolderPath]
        public string folderPath = "Assets";

        [AssetFolderPath]
        public string assetFolderPath = "Assets";

        [AssetPath(typeof(TextAsset))]
        public string assetPath;

        [Hyperlink("Open XFramework")]
        public string hyperlink = "https://github.com/";

        [Layer]
        public int layer;

        [Layer]
        public string layerName = "Default";

        [Password]
        public string password = "password";

        [PrettyBox]
        public BoxExample prettyBox;

        [PrettyList]
        public List<RowExample> prettyList = new List<RowExample>();

        [global::ShowAsFlagsAttribute]
        public FlagExample showAsFlags = FlagExample.A | FlagExample.C;

        [ShowInBin32]
        public int showInBin32 = 42;

        [ShowInHex(4)]
        public int showInHex = 255;

        [ShowInRow(new[] { nameof(InlineExample.x), nameof(InlineExample.y), nameof(InlineExample.z) })]
        public InlineExample showInRow;

        [TextDropdown(nameof(GetTextOptions))]
        public string textDropdown = "Alpha";

        [UIClickSound]
        public string uiClickSound;

        [DataTableRef(typeof(DataRefExampleTable))]
        public int dataTableRef;

        public bool shouldDisplay = true;
        public bool shouldEnable = true;

        private bool ShouldDisplay()
        {
            return shouldDisplay;
        }

        private bool ShouldEnable()
        {
            return shouldEnable;
        }

        private static IEnumerable<string> GetTextOptions()
        {
            return new[] { "Alpha", "Beta", "Gamma" };
        }
    }
}
