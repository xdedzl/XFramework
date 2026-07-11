using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework.Save
{
    /// <summary>
    /// All save profiles currently discoverable on disk for editor debugging.
    /// </summary>
    public readonly struct SaveStorageDebugSnapshot
    {
        public SaveStorageDebugSnapshot(string saveRootPath, IReadOnlyList<SaveProfileDebugSnapshot> profiles)
        {
            SaveRootPath = saveRootPath ?? string.Empty;
            Profiles = profiles ?? Array.Empty<SaveProfileDebugSnapshot>();
        }

        public string SaveRootPath { get; }
        public IReadOnlyList<SaveProfileDebugSnapshot> Profiles { get; }
    }

    /// <summary>
    /// A single save folder, its metadata, and its individual database files.
    /// </summary>
    public readonly struct SaveProfileDebugSnapshot
    {
        public SaveProfileDebugSnapshot(
            SaveMeta meta,
            string directoryPath,
            IReadOnlyList<SaveDatabaseDebugSnapshot> databases,
            bool isActive,
            string error = null)
        {
            Meta = meta;
            DirectoryPath = directoryPath ?? string.Empty;
            Databases = databases ?? Array.Empty<SaveDatabaseDebugSnapshot>();
            IsActive = isActive;
            Error = error ?? string.Empty;
        }

        public SaveMeta Meta { get; }
        public string DirectoryPath { get; }
        public IReadOnlyList<SaveDatabaseDebugSnapshot> Databases { get; }
        public bool IsActive { get; }
        public string Error { get; }
    }

    /// <summary>
    /// A save database instance and the JSON file it is persisted to.
    /// </summary>
    public readonly struct SaveDatabaseDebugSnapshot
    {
        public SaveDatabaseDebugSnapshot(
            Type databaseType,
            string fileName,
            SaveDatabase database,
            string rawJson = null,
            string error = null)
        {
            DatabaseType = databaseType;
            FileName = fileName ?? string.Empty;
            Database = database;
            RawJson = rawJson ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public Type DatabaseType { get; }
        public string FileName { get; }
        public SaveDatabase Database { get; }
        public string RawJson { get; }
        public string Error { get; }
    }

    public readonly struct SaveManagerDebugSnapshot
    {
        public SaveManagerDebugSnapshot(
            string saveRootPath,
            SaveMeta activeSaveMeta,
            IReadOnlyList<SaveDatabaseDebugSnapshot> databases)
        {
            SaveRootPath = saveRootPath ?? string.Empty;
            ActiveSaveMeta = activeSaveMeta;
            Databases = databases ?? Array.Empty<SaveDatabaseDebugSnapshot>();
        }

        public string SaveRootPath { get; }
        public SaveMeta ActiveSaveMeta { get; }
        public bool HasActiveProfile => ActiveSaveMeta != null;
        public IReadOnlyList<SaveDatabaseDebugSnapshot> Databases { get; }
    }

    public partial class SaveManager
    {
        /// <summary>
        /// Gets the active profile state for editor debugging. The database instances are live
        /// references so a debugger can inspect and edit them before calling <see cref="Save"/>.
        /// </summary>
        public SaveManagerDebugSnapshot GetDebugSnapshot()
        {
            if (!HasActiveProfile)
            {
                return new SaveManagerDebugSnapshot(
                    SaveRootPath,
                    null,
                    Array.Empty<SaveDatabaseDebugSnapshot>());
            }

            var databases = new List<SaveDatabaseDebugSnapshot>();
            foreach (KeyValuePair<Type, SaveDatabase> pair in ActiveProfile.GetAllDatabases())
            {
                databases.Add(new SaveDatabaseDebugSnapshot(
                    pair.Key,
                    SaveProfile.GetFileName(pair.Key),
                    pair.Value));
            }

            databases.Sort(CompareDebugDatabases);
            return new SaveManagerDebugSnapshot(SaveRootPath, ActiveProfile.Meta, databases);
        }

        /// <summary>
        /// Gets every save profile on disk, replacing the active profile with its live runtime data.
        /// </summary>
        public SaveStorageDebugSnapshot GetDebugStorageSnapshot()
        {
            SaveStorageDebugSnapshot storedSnapshot = GetStoredDebugSnapshot();
            if (!HasActiveProfile)
            {
                return storedSnapshot;
            }

            var profiles = new List<SaveProfileDebugSnapshot>(storedSnapshot.Profiles);
            bool activeProfileFound = false;
            for (int i = 0; i < profiles.Count; i++)
            {
                if (!IsActiveDebugProfile(profiles[i]))
                {
                    continue;
                }

                profiles[i] = CreateLiveDebugProfile(profiles[i]);
                activeProfileFound = true;
                break;
            }

            if (!activeProfileFound)
            {
                profiles.Add(CreateLiveDebugProfile(new SaveProfileDebugSnapshot(
                    ActiveProfile.Meta,
                    ActiveProfile.DirectoryPath,
                    Array.Empty<SaveDatabaseDebugSnapshot>(),
                    false)));
            }

            profiles.Sort(CompareDebugProfiles);
            return new SaveStorageDebugSnapshot(SaveRootPath, profiles);
        }

        /// <summary>
        /// Reads all save folders from disk. This can be used in Edit Mode without loading SaveManager.
        /// </summary>
        public static SaveStorageDebugSnapshot GetStoredDebugSnapshot()
        {
            string saveRootPath = GetDefaultSaveRootPath();
            if (!Directory.Exists(saveRootPath))
            {
                return new SaveStorageDebugSnapshot(saveRootPath, Array.Empty<SaveProfileDebugSnapshot>());
            }

            IReadOnlyList<DebugDatabaseRegistration> registrations = DiscoverDebugDatabaseTypes();
            var profiles = new List<SaveProfileDebugSnapshot>();
            foreach (string directoryPath in Directory.GetDirectories(saveRootPath))
            {
                profiles.Add(ReadStoredDebugProfile(directoryPath, registrations));
            }

            profiles.Sort(CompareDebugProfiles);
            return new SaveStorageDebugSnapshot(saveRootPath, profiles);
        }

        /// <summary>
        /// Creates a new profile on disk for editor debugging. When auto_save is available, it is
        /// used first; later profiles use the save_001, save_002 naming sequence.
        /// </summary>
        public static SaveProfileDebugSnapshot CreateStoredDebugProfile(string saveName = null, string displayName = null)
        {
            string saveRootPath = GetDefaultSaveRootPath();
            if (string.IsNullOrWhiteSpace(saveName))
            {
                saveName = GenerateDebugSaveName(saveRootPath);
            }

            string directoryPath = Path.Combine(saveRootPath, saveName);
            if (Directory.Exists(directoryPath))
            {
                throw new XFrameworkException($"[SaveManager] Save '{saveName}' already exists.");
            }

            var meta = new SaveMeta
            {
                saveName = saveName,
                displayName = string.IsNullOrWhiteSpace(displayName) ? saveName : displayName,
                createdAt = DateTime.UtcNow.ToString("o"),
                updatedAt = DateTime.UtcNow.ToString("o"),
                version = 1
            };

            IReadOnlyList<DebugDatabaseRegistration> registrations = DiscoverDebugDatabaseTypes();
            var databases = new List<SaveDatabaseDebugSnapshot>(registrations.Count);
            foreach (DebugDatabaseRegistration registration in registrations)
            {
                var database = (SaveDatabase)Activator.CreateInstance(registration.DatabaseType);
                database.OnAfterCreate();
                databases.Add(new SaveDatabaseDebugSnapshot(
                    registration.DatabaseType,
                    registration.FileName + ".json",
                    database));
            }

            var profile = new SaveProfileDebugSnapshot(meta, directoryPath, databases, false);
            SaveDebugProfile(profile);
            return profile;
        }

        /// <summary>
        /// Writes a profile inspected from disk. This method does not invoke database lifecycle callbacks.
        /// </summary>
        public static void SaveDebugProfile(SaveProfileDebugSnapshot profile)
        {
            if (profile.Meta == null || string.IsNullOrWhiteSpace(profile.DirectoryPath))
            {
                throw new XFrameworkException("[SaveManager] Debug profile is missing metadata or a directory path.");
            }

            Utility.IO.CreateFolder(profile.DirectoryPath);
            JsonSerializerSettings settings = CreateDebugJsonSettings();
            WriteDebugJson(Path.Combine(profile.DirectoryPath, MetaFileName), profile.Meta, settings);

            foreach (SaveDatabaseDebugSnapshot database in profile.Databases)
            {
                if (database.Database == null || string.IsNullOrWhiteSpace(database.FileName))
                {
                    continue;
                }

                WriteDebugJson(Path.Combine(profile.DirectoryPath, database.FileName), database.Database, settings);
            }
        }

        /// <summary>
        /// Returns a serialized signature for change detection when a profile is edited in X Inspector.
        /// </summary>
        public static string GetDebugProfileContentSignature(SaveProfileDebugSnapshot profile)
        {
            if (profile.Meta == null)
            {
                return string.Empty;
            }

            JsonSerializerSettings settings = CreateDebugJsonSettings();
            var builder = new StringBuilder();
            builder.Append(JsonConvert.SerializeObject(profile.Meta, settings));

            foreach (SaveDatabaseDebugSnapshot database in profile.Databases)
            {
                if (database.Database == null)
                {
                    continue;
                }

                builder.Append('\n');
                builder.Append(database.FileName);
                builder.Append('\n');
                builder.Append(JsonConvert.SerializeObject(database.Database, settings));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Deletes one save folder discovered by <see cref="GetStoredDebugSnapshot"/>.
        /// </summary>
        public static void DeleteDebugProfile(SaveProfileDebugSnapshot profile)
        {
            if (string.IsNullOrWhiteSpace(profile.DirectoryPath))
            {
                throw new XFrameworkException("[SaveManager] Debug profile directory path is empty.");
            }

            EnsurePathIsInsideSaveRoot(profile.DirectoryPath);
            if (Directory.Exists(profile.DirectoryPath))
            {
                Directory.Delete(profile.DirectoryPath, true);
            }
        }

        /// <summary>
        /// Deletes every save folder while retaining the save root directory itself.
        /// </summary>
        public static void ClearStoredDebugProfiles()
        {
            string saveRootPath = GetDefaultSaveRootPath();
            if (!Directory.Exists(saveRootPath))
            {
                return;
            }

            foreach (string directoryPath in Directory.GetDirectories(saveRootPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }

        private SaveProfileDebugSnapshot CreateLiveDebugProfile(SaveProfileDebugSnapshot storedProfile)
        {
            SaveManagerDebugSnapshot activeSnapshot = GetDebugSnapshot();
            var databases = new List<SaveDatabaseDebugSnapshot>(storedProfile.Databases);
            var liveDatabaseIndexes = new Dictionary<string, SaveDatabaseDebugSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (SaveDatabaseDebugSnapshot database in activeSnapshot.Databases)
            {
                liveDatabaseIndexes[database.FileName] = database;
            }

            for (int i = 0; i < databases.Count; i++)
            {
                SaveDatabaseDebugSnapshot storedDatabase = databases[i];
                if (!liveDatabaseIndexes.TryGetValue(storedDatabase.FileName, out SaveDatabaseDebugSnapshot liveDatabase))
                {
                    continue;
                }

                databases[i] = new SaveDatabaseDebugSnapshot(
                    liveDatabase.DatabaseType,
                    liveDatabase.FileName,
                    liveDatabase.Database,
                    storedDatabase.RawJson,
                    storedDatabase.Error);
                liveDatabaseIndexes.Remove(storedDatabase.FileName);
            }

            foreach (SaveDatabaseDebugSnapshot liveDatabase in liveDatabaseIndexes.Values)
            {
                databases.Add(liveDatabase);
            }

            databases.Sort(CompareDebugDatabases);
            return new SaveProfileDebugSnapshot(
                ActiveProfile.Meta,
                ActiveProfile.DirectoryPath,
                databases,
                true,
                storedProfile.Error);
        }

        private bool IsActiveDebugProfile(SaveProfileDebugSnapshot profile)
        {
            return string.Equals(profile.DirectoryPath, ActiveProfile.DirectoryPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(profile.Meta?.saveName, ActiveProfile.Meta?.saveName, StringComparison.Ordinal);
        }

        private static SaveProfileDebugSnapshot ReadStoredDebugProfile(
            string directoryPath,
            IReadOnlyList<DebugDatabaseRegistration> registrations)
        {
            SaveMeta meta = ReadDebugMeta(directoryPath, out string profileError);
            var rawJsonByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var fileErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(filePath);
                if (string.Equals(fileName, MetaFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    rawJsonByFileName[fileName] = File.ReadAllText(filePath);
                }
                catch (Exception exception)
                {
                    fileErrors[fileName] = exception.Message;
                }
            }

            JsonSerializerSettings settings = CreateDebugJsonSettings();
            var databases = new List<SaveDatabaseDebugSnapshot>();
            foreach (DebugDatabaseRegistration registration in registrations)
            {
                string fileName = registration.FileName + ".json";
                if (!rawJsonByFileName.TryGetValue(fileName, out string rawJson))
                {
                    string missingFileError = fileErrors.TryGetValue(fileName, out string fileError)
                        ? fileError
                        : "文件不存在";
                    databases.Add(new SaveDatabaseDebugSnapshot(registration.DatabaseType, fileName, null, null, missingFileError));
                    continue;
                }

                SaveDatabase database = null;
                string error = null;
                try
                {
                    database = JsonConvert.DeserializeObject(rawJson, registration.DatabaseType, settings) as SaveDatabase;
                    if (database == null)
                    {
                        error = "数据库内容为空或无法反序列化";
                    }
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                }

                databases.Add(new SaveDatabaseDebugSnapshot(
                    registration.DatabaseType,
                    fileName,
                    database,
                    rawJson,
                    error));
                rawJsonByFileName.Remove(fileName);
            }

            foreach (KeyValuePair<string, string> pair in rawJsonByFileName)
            {
                databases.Add(new SaveDatabaseDebugSnapshot(
                    null,
                    pair.Key,
                    null,
                    pair.Value,
                    "未找到对应的 SaveDatabase 类型"));
            }

            foreach (KeyValuePair<string, string> pair in fileErrors)
            {
                if (ContainsDebugDatabaseFile(databases, pair.Key))
                {
                    continue;
                }

                databases.Add(new SaveDatabaseDebugSnapshot(null, pair.Key, null, null, pair.Value));
            }

            databases.Sort(CompareDebugDatabases);
            return new SaveProfileDebugSnapshot(meta, directoryPath, databases, false, profileError);
        }

        private static SaveMeta ReadDebugMeta(string directoryPath, out string error)
        {
            string folderName = Path.GetFileName(directoryPath);
            string metaPath = Path.Combine(directoryPath, MetaFileName);
            error = null;

            if (File.Exists(metaPath))
            {
                try
                {
                    SaveMeta meta = JsonConvert.DeserializeObject<SaveMeta>(File.ReadAllText(metaPath), CreateDebugJsonSettings());
                    if (meta != null)
                    {
                        meta.saveName = string.IsNullOrWhiteSpace(meta.saveName) ? folderName : meta.saveName;
                        meta.displayName = string.IsNullOrWhiteSpace(meta.displayName) ? meta.saveName : meta.displayName;
                        return meta;
                    }

                    error = "meta.json 内容为空或无法反序列化";
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                }
            }
            else
            {
                error = "缺少 meta.json";
            }

            return new SaveMeta
            {
                saveName = folderName,
                displayName = folderName
            };
        }

        private static IReadOnlyList<DebugDatabaseRegistration> DiscoverDebugDatabaseTypes()
        {
            IEnumerable<Type> types = Utility.Reflection.GetAssignableTypes(typeof(SaveDatabase), "Assembly-CSharp", "XFrameworkRuntime");
            var candidates = new List<Type>();
            foreach (Type type in types)
            {
                if (type != null && type.IsClass && !type.IsAbstract
                    && type.GetCustomAttribute<SaveDatabaseAttribute>() != null)
                {
                    candidates.Add(type);
                }
            }

            candidates.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
            var registrations = new List<DebugDatabaseRegistration>();
            var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Type type in candidates)
            {
                SaveDatabaseAttribute attribute = type.GetCustomAttribute<SaveDatabaseAttribute>();
                if (attribute == null || !usedFileNames.Add(attribute.FileName))
                {
                    continue;
                }

                registrations.Add(new DebugDatabaseRegistration(type, attribute.FileName));
            }

            registrations.Sort((left, right) => string.Compare(left.FileName, right.FileName, StringComparison.Ordinal));
            return registrations;
        }

        private static string GetDefaultSaveRootPath()
        {
            return Path.Combine(Application.persistentDataPath, SaveRootFolderName);
        }

        private static string GenerateDebugSaveName(string saveRootPath)
        {
            const string autoSaveName = "auto_save";
            if (!Directory.Exists(Path.Combine(saveRootPath, autoSaveName)))
            {
                return autoSaveName;
            }

            int index = 1;
            while (true)
            {
                string saveName = $"save_{index:D3}";
                if (!Directory.Exists(Path.Combine(saveRootPath, saveName)))
                {
                    return saveName;
                }

                index++;
            }
        }

        private static JsonSerializerSettings CreateDebugJsonSettings()
        {
            JsonSerializerSettings settings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();
            settings.Formatting = Formatting.Indented;
            return settings;
        }

        private static void WriteDebugJson(string path, object value, JsonSerializerSettings settings)
        {
            string json = JsonConvert.SerializeObject(value, settings);
            File.WriteAllText(path, json);
        }

        private static bool ContainsDebugDatabaseFile(IReadOnlyList<SaveDatabaseDebugSnapshot> databases, string fileName)
        {
            for (int i = 0; i < databases.Count; i++)
            {
                if (string.Equals(databases[i].FileName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsurePathIsInsideSaveRoot(string directoryPath)
        {
            string rootPath = Path.GetFullPath(GetDefaultSaveRootPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string profilePath = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string rootPrefix = rootPath + Path.DirectorySeparatorChar;

            if (!profilePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new XFrameworkException("[SaveManager] Debug profile path is outside the save root.");
            }
        }

        private static int CompareDebugProfiles(SaveProfileDebugSnapshot left, SaveProfileDebugSnapshot right)
        {
            int activeResult = right.IsActive.CompareTo(left.IsActive);
            if (activeResult != 0)
            {
                return activeResult;
            }

            string leftName = left.Meta?.saveName ?? Path.GetFileName(left.DirectoryPath);
            string rightName = right.Meta?.saveName ?? Path.GetFileName(right.DirectoryPath);
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        private static int CompareDebugDatabases(SaveDatabaseDebugSnapshot left, SaveDatabaseDebugSnapshot right)
        {
            int fileNameResult = string.Compare(left.FileName, right.FileName, StringComparison.Ordinal);
            if (fileNameResult != 0)
            {
                return fileNameResult;
            }

            return string.Compare(
                left.DatabaseType?.FullName,
                right.DatabaseType?.FullName,
                StringComparison.Ordinal);
        }

        private readonly struct DebugDatabaseRegistration
        {
            public DebugDatabaseRegistration(Type databaseType, string fileName)
            {
                DatabaseType = databaseType;
                FileName = fileName;
            }

            public Type DatabaseType { get; }
            public string FileName { get; }
        }
    }
}
