using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.Editor
{
    public static class XTextAssetOpenRegistry
    {
        private static readonly Dictionary<string, Func<TextAsset, bool>> S_Handlers = new(StringComparer.Ordinal);

        public static void Register(string typeAlias, Func<TextAsset, bool> opener)
        {
            if (string.IsNullOrWhiteSpace(typeAlias))
            {
                throw new ArgumentException("typeAlias cannot be empty.", nameof(typeAlias));
            }

            if (opener == null)
            {
                throw new ArgumentNullException(nameof(opener));
            }

            if (S_Handlers.ContainsKey(typeAlias))
            {
                Debug.LogError($"XTextAsset opener already registered for alias '{typeAlias}'.");
                return;
            }

            S_Handlers.Add(typeAlias, opener);
        }

        public static bool TryOpen(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                return false;
            }

            if (!XTextUtility.TryReadMetaInfo(textAsset.text, out XTextMetaInfo metaInfo))
            {
                return false;
            }

            if (!S_Handlers.TryGetValue(metaInfo.typeAlias, out Func<TextAsset, bool> opener))
            {
                return false;
            }

            try
            {
                return opener(textAsset);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to open XTextAsset '{textAsset.name}' with alias '{metaInfo.typeAlias}': {exception}");
                return false;
            }
        }

        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            UnityEngine.Object target = EditorUtility.InstanceIDToObject(instanceId);
            if (target is not TextAsset textAsset)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(textAsset);
            if (!string.Equals(Path.GetExtension(assetPath), ".xasset", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryOpen(textAsset);
        }

        [MenuItem("Assets/Open with Text Editor", false, 20)]
        private static void OpenWithTextEditor()
        {
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string fullPath = Path.GetFullPath(assetPath);
            InternalEditorUtility.OpenFileAtLineExternal(fullPath, 0);
        }

        [MenuItem("Assets/Open with Text Editor", true)]
        private static bool OpenWithTextEditorValidation()
        {
            if (Selection.activeObject == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            return string.Equals(Path.GetExtension(assetPath), ".xasset", StringComparison.OrdinalIgnoreCase);
        }
    }
}
