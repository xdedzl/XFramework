using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    [InitializeOnLoad]
    internal static class XCommandEditorHistory
    {
        private static readonly string s_HistoryKey = $"XFramework.XCommand.{Hash128.Compute(Application.dataPath)}.History";

        static XCommandEditorHistory()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            XCommandHub.RestoreCommandHistory(Load());
            XCommandHub.CommandHistoryChanged += Save;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        private static IEnumerable<string> Load()
        {
            string json = EditorPrefs.GetString(s_HistoryKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return Array.Empty<string>();
            }

            StringListStorage storage = JsonUtility.FromJson<StringListStorage>(json);
            if (storage == null || storage.values == null)
            {
                return Array.Empty<string>();
            }
            return storage.values;
        }

        private static void Save()
        {
            var storage = new StringListStorage();
            storage.values.AddRange(XCommandHub.CommandHistory);
            EditorPrefs.SetString(s_HistoryKey, JsonUtility.ToJson(storage));
        }

        private static void Shutdown()
        {
            Save();
            XCommandHub.CommandHistoryChanged -= Save;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;
        }

        [Serializable]
        private sealed class StringListStorage
        {
            public List<string> values = new List<string>();
        }
    }
}
