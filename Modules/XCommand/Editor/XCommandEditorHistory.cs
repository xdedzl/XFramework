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
        private static readonly string s_ProjectKey = $"XFramework.XCommand.{Hash128.Compute(Application.dataPath)}";
        private static readonly string s_HistoryKey = $"{s_ProjectKey}.History";
        private static readonly string s_RecordsKey = $"{s_ProjectKey}.Records";

        static XCommandEditorHistory()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            XCommandHub.RestoreRecords(LoadRecordSnapshots());
            XCommandHub.RestoreCommandHistory(LoadCommandHistory());
            XCommandHub.CommandHistoryChanged += SaveCommandHistory;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            EditorApplication.quitting += OnEditorQuitting;
        }

        internal static void EnsureInitialized()
        {
        }

        private static IEnumerable<string> LoadCommandHistory()
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

        private static IEnumerable<XCommandRecordSnapshot> LoadRecordSnapshots()
        {
            string json = SessionState.GetString(s_RecordsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return Array.Empty<XCommandRecordSnapshot>();
            }

            RecordListStorage storage = JsonUtility.FromJson<RecordListStorage>(json);
            if (storage == null || storage.records == null)
            {
                return Array.Empty<XCommandRecordSnapshot>();
            }
            return storage.records;
        }

        private static void SaveCommandHistory()
        {
            var storage = new StringListStorage();
            storage.values.AddRange(XCommandHub.CommandHistory);
            EditorPrefs.SetString(s_HistoryKey, JsonUtility.ToJson(storage));
        }

        private static void SaveRecordSnapshots()
        {
            var storage = new RecordListStorage();
            storage.records.AddRange(XCommandHub.CaptureRecordSnapshots());
            SessionState.SetString(s_RecordsKey, JsonUtility.ToJson(storage));
        }

        private static void BeforeAssemblyReload()
        {
            SaveCommandHistory();
            SaveRecordSnapshots();
            Unsubscribe();
        }

        private static void OnEditorQuitting()
        {
            SaveCommandHistory();
            SessionState.EraseString(s_RecordsKey);
            Unsubscribe();
        }

        private static void Unsubscribe()
        {
            XCommandHub.CommandHistoryChanged -= SaveCommandHistory;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
        }

        [Serializable]
        private sealed class StringListStorage
        {
            public List<string> values = new List<string>();
        }

        [Serializable]
        private sealed class RecordListStorage
        {
            public List<XCommandRecordSnapshot> records = new List<XCommandRecordSnapshot>();
        }
    }
}
