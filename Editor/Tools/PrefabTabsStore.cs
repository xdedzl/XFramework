using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace XFramework.Editor
{
    [InitializeOnLoad]
    internal static class PrefabTabsStore
    {
        private const string PrefsKey = "XFramework.PrefabTabs.State";
        private const int MaxRecentTabs = 20;

        public static event Action Changed;

        static PrefabTabsStore()
        {
            EditorApplication.projectChanged += CleanupInvalidTabs;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
        }

        public static IReadOnlyList<PrefabTabViewInfo> GetTabs()
        {
            PrefabTabsState state = LoadState();
            RemoveInvalidTabs(state.tabs);

            return state.tabs
                .Select(tab => new PrefabTabViewInfo(tab.guid, tab.pinned, tab.lastOpenTicks, AssetDatabase.GUIDToAssetPath(tab.guid)))
                .Where(tab => !string.IsNullOrEmpty(tab.AssetPath))
                .OrderByDescending(tab => tab.Pinned)
                .ThenByDescending(tab => tab.LastOpenTicks)
                .ToList();
        }

        public static string GetCurrentPrefabPath()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            return prefabStage == null ? string.Empty : prefabStage.assetPath;
        }

        public static void RegisterPrefabPath(string assetPath)
        {
            if (!IsPrefabAssetPath(assetPath))
            {
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            PrefabTabsState state = LoadState();
            PrefabTabInfo tabInfo = state.tabs.FirstOrDefault(tab => string.Equals(tab.guid, guid, StringComparison.Ordinal));
            if (tabInfo == null)
            {
                tabInfo = new PrefabTabInfo
                {
                    guid = guid,
                    lastOpenTicks = DateTime.Now.Ticks
                };
                state.tabs.Add(tabInfo);
                PruneRecentTabs(state.tabs);
                SaveState(state);
            }

            NotifyChanged();
        }

        public static void OpenPrefab(string guid)
        {
            PrefabTabsState state = LoadState();
            PrefabTabInfo tabInfo = state.tabs.FirstOrDefault(tab => string.Equals(tab.guid, guid, StringComparison.Ordinal));
            if (tabInfo == null)
            {
                NotifyChanged();
                return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsPrefabAssetPath(assetPath))
            {
                state.tabs.Remove(tabInfo);
                SaveState(state);
                Debug.LogWarning($"Prefab tab target no longer exists: {assetPath}");
                NotifyChanged();
                return;
            }

            PrefabStageUtility.OpenPrefab(assetPath);
            NotifyChanged();
        }

        public static void TogglePinned(string guid)
        {
            PrefabTabsState state = LoadState();
            PrefabTabInfo tabInfo = state.tabs.FirstOrDefault(tab => string.Equals(tab.guid, guid, StringComparison.Ordinal));
            if (tabInfo == null)
            {
                return;
            }

            tabInfo.pinned = !tabInfo.pinned;
            PruneRecentTabs(state.tabs);
            SaveState(state);
            NotifyChanged();
        }

        public static void Remove(string guid)
        {
            PrefabTabsState state = LoadState();
            state.tabs.RemoveAll(tab => string.Equals(tab.guid, guid, StringComparison.Ordinal));
            SaveState(state);
            NotifyChanged();
        }

        public static void ClearAll()
        {
            PrefabTabsState state = LoadState();
            if (state.tabs.Count == 0)
            {
                return;
            }

            state.tabs.Clear();
            SaveState(state);
            NotifyChanged();
        }

        public static void CleanupInvalidTabs()
        {
            PrefabTabsState state = LoadState();
            int oldCount = state.tabs.Count;
            RemoveInvalidTabs(state.tabs);
            SaveState(state);

            if (state.tabs.Count != oldCount)
            {
                NotifyChanged();
            }
        }

        public static void PingPrefab(string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Prefab tab target no longer exists: {assetPath}");
                Remove(guid);
                return;
            }

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        public static void PingCurrentPrefab()
        {
            string currentPrefabPath = GetCurrentPrefabPath();
            if (string.IsNullOrEmpty(currentPrefabPath))
            {
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(currentPrefabPath);
            if (!string.IsNullOrEmpty(guid))
            {
                PingPrefab(guid);
            }
        }

        [OnOpenAsset(2)]
        public static bool OnOpenAsset(int instanceId, int line)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            UnityEngine.Object target = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618 // Type or member is obsolete
            string assetPath = AssetDatabase.GetAssetPath(target);
            RegisterPrefabPath(assetPath);
            return false;
        }

        private static void OnPrefabStageOpened(PrefabStage prefabStage)
        {
            if (prefabStage != null)
            {
                RegisterPrefabPath(prefabStage.assetPath);
            }
        }

        private static bool IsPrefabAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && string.Equals(Path.GetExtension(assetPath), ".prefab", StringComparison.OrdinalIgnoreCase)
                && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
        }

        private static PrefabTabsState LoadState()
        {
            string json = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new PrefabTabsState();
            }

            try
            {
                PrefabTabsState state = JsonUtility.FromJson<PrefabTabsState>(json);
                if (state == null || state.tabs == null)
                {
                    return new PrefabTabsState();
                }

                state.tabs.RemoveAll(tab => tab == null || string.IsNullOrEmpty(tab.guid));
                return state;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load Prefab Tabs state: {exception.Message}");
                return new PrefabTabsState();
            }
        }

        private static void SaveState(PrefabTabsState state)
        {
            state ??= new PrefabTabsState();
            state.tabs ??= new List<PrefabTabInfo>();
            RemoveInvalidTabs(state.tabs);
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(state));
        }

        private static void RemoveInvalidTabs(List<PrefabTabInfo> tabs)
        {
            tabs.RemoveAll(tab => tab == null
                || string.IsNullOrEmpty(tab.guid)
                || string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(tab.guid)));
        }

        private static void PruneRecentTabs(List<PrefabTabInfo> tabs)
        {
            int recentCount = tabs.Count(tab => !tab.pinned);
            if (recentCount <= MaxRecentTabs)
            {
                return;
            }

            List<PrefabTabInfo> overflowTabs = tabs
                .Where(tab => !tab.pinned)
                .OrderBy(tab => tab.lastOpenTicks)
                .Take(recentCount - MaxRecentTabs)
                .ToList();

            foreach (PrefabTabInfo tabInfo in overflowTabs)
            {
                tabs.Remove(tabInfo);
            }
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        [Serializable]
        private sealed class PrefabTabsState
        {
            public List<PrefabTabInfo> tabs = new List<PrefabTabInfo>();
        }

        [Serializable]
        private sealed class PrefabTabInfo
        {
            public string guid;
            public bool pinned;
            public long lastOpenTicks;
        }
    }

    internal readonly struct PrefabTabViewInfo
    {
        public PrefabTabViewInfo(string guid, bool pinned, long lastOpenTicks, string assetPath)
        {
            Guid = guid;
            Pinned = pinned;
            LastOpenTicks = lastOpenTicks;
            AssetPath = assetPath;
        }

        public string Guid { get; }
        public bool Pinned { get; }
        public long LastOpenTicks { get; }
        public string AssetPath { get; }
        public string DisplayName => Path.GetFileNameWithoutExtension(AssetPath);
    }
}
