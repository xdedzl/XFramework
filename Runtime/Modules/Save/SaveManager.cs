using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace XFramework.Save
{
    /// <summary>
    /// 存档管理模块。管理存档的创建、加载、保存、删除。
    /// 通过 SaveManager.Instance 访问。
    /// </summary>
    [ModuleLifecycle(ModuleLifecycle.RuntimePersistent)]
    public class SaveManager : GameModuleBase<SaveManager>
    {
        private const string SaveRootFolderName = "Saves";
        private const string MetaFileName = "meta.json";

        /// <summary>
        /// 存档根目录
        /// </summary>
        public string SaveRootPath { get; private set; }

        /// <summary>
        /// 当前活跃的存档
        /// </summary>
        public SaveProfile ActiveProfile { get; private set; }

        /// <summary>
        /// 当前是否有活跃存档
        /// </summary>
        public bool HasActiveProfile => ActiveProfile != null;

        // 所有已发现的 SaveDatabase 子类型
        private readonly List<Type> registeredDbTypes = new List<Type>();

        // JSON 序列化设置
        private JsonSerializerSettings jsonSettings;

        public override void Initialize()
        {
            SaveRootPath = Path.Combine(Application.persistentDataPath, SaveRootFolderName);
            Utility.IO.CreateFolder(SaveRootPath);

            jsonSettings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();
            jsonSettings.Formatting = Formatting.Indented;

            DiscoverDatabaseTypes();

            Debug.Log($"[SaveManager] Initialized. Root: {SaveRootPath}, Registered {registeredDbTypes.Count} database types.");
        }

        public override void Shutdown()
        {
            // 关闭时自动保存当前存档
            if (HasActiveProfile)
            {
                Save();
            }
        }

        public override int Priority => (int)GameModulePriority.Save;

        #region Public API

        /// <summary>
        /// 创建一个新存档并将其设为活跃存档
        /// </summary>
        /// <param name="saveName">存档文件夹名称，为空则自动生成</param>
        /// <param name="displayName">显示名称，为空则使用 saveName</param>
        /// <returns>新创建的存档</returns>
        public SaveProfile CreateSave(string saveName = null, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(saveName))
            {
                saveName = GenerateNextSaveName();
            }

            string savePath = Path.Combine(SaveRootPath, saveName);
            if (Directory.Exists(savePath))
            {
                throw new XFrameworkException($"[SaveManager] Save '{saveName}' already exists.");
            }

            Utility.IO.CreateFolder(savePath);

            var meta = new SaveMeta
            {
                saveName = saveName,
                displayName = string.IsNullOrWhiteSpace(displayName) ? saveName : displayName,
                createdAt = DateTime.UtcNow.ToString("o"),
                updatedAt = DateTime.UtcNow.ToString("o"),
                version = 1,
            };

            var profile = new SaveProfile
            {
                Meta = meta,
                DirectoryPath = savePath,
            };

            // 为每个已注册的 Database 类型创建默认实例
            foreach (var dbType in registeredDbTypes)
            {
                var db = (SaveDatabase)Activator.CreateInstance(dbType);
                profile.SetDatabase(dbType, db);
            }

            // 写入 meta
            WriteJson(Path.Combine(savePath, MetaFileName), meta);

            ActiveProfile = profile;
            Debug.Log($"[SaveManager] Created save '{saveName}'.");
            return profile;
        }

        /// <summary>
        /// 加载指定存档并设为活跃存档
        /// </summary>
        /// <param name="saveName">存档文件夹名称</param>
        /// <returns>加载的存档</returns>
        public SaveProfile Load(string saveName)
        {
            string savePath = Path.Combine(SaveRootPath, saveName);
            if (!Directory.Exists(savePath))
            {
                throw new XFrameworkException($"[SaveManager] Save '{saveName}' not found.");
            }

            // 读取 meta
            string metaPath = Path.Combine(savePath, MetaFileName);
            SaveMeta meta;
            if (File.Exists(metaPath))
            {
                meta = ReadJson<SaveMeta>(metaPath);
            }
            else
            {
                // 兼容：没有 meta 文件的存档
                meta = new SaveMeta
                {
                    saveName = saveName,
                    displayName = saveName,
                    createdAt = DateTime.UtcNow.ToString("o"),
                    updatedAt = DateTime.UtcNow.ToString("o"),
                };
                WriteJson(metaPath, meta);
            }

            var profile = new SaveProfile
            {
                Meta = meta,
                DirectoryPath = savePath,
            };

            // 加载每个 Database
            foreach (var dbType in registeredDbTypes)
            {
                string fileName = SaveProfile.GetFileName(dbType);
                string filePath = Path.Combine(savePath, fileName);

                SaveDatabase db;
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    db = (SaveDatabase)JsonConvert.DeserializeObject(json, dbType, jsonSettings);
                    db ??= (SaveDatabase)Activator.CreateInstance(dbType);
                    db.OnAfterLoad();
                }
                else
                {
                    // 文件不存在，创建默认实例
                    db = (SaveDatabase)Activator.CreateInstance(dbType);
                }

                profile.SetDatabase(dbType, db);
            }

            ActiveProfile = profile;
            Debug.Log($"[SaveManager] Loaded save '{saveName}'.");
            return profile;
        }

        /// <summary>
        /// 保存当前活跃存档到磁盘
        /// </summary>
        public void Save()
        {
            if (!HasActiveProfile)
            {
                Debug.LogWarning("[SaveManager] No active profile to save.");
                return;
            }

            var profile = ActiveProfile;
            Utility.IO.CreateFolder(profile.DirectoryPath);

            // 更新元数据时间
            profile.Meta.updatedAt = DateTime.UtcNow.ToString("o");
            WriteJson(Path.Combine(profile.DirectoryPath, MetaFileName), profile.Meta);

            // 保存每个 Database
            foreach (var kvp in profile.GetAllDatabases())
            {
                kvp.Value.OnBeforeSave();

                string fileName = SaveProfile.GetFileName(kvp.Key);
                string filePath = Path.Combine(profile.DirectoryPath, fileName);
                WriteJson(filePath, kvp.Value);
            }

            Debug.Log($"[SaveManager] Saved '{profile.Meta.saveName}' to path: {profile.DirectoryPath}");
        }

        /// <summary>
        /// 删除指定存档
        /// </summary>
        public void DeleteSave(string saveName)
        {
            string savePath = Path.Combine(SaveRootPath, saveName);
            if (!Directory.Exists(savePath))
            {
                Debug.LogWarning($"[SaveManager] Save '{saveName}' not found, nothing to delete.");
                return;
            }

            // 如果删除的是当前活跃存档，卸载它
            if (HasActiveProfile && ActiveProfile.Meta.saveName == saveName)
            {
                ActiveProfile = null;
            }

            Directory.Delete(savePath, true);
            Debug.Log($"[SaveManager] Deleted save '{saveName}'.");
        }

        /// <summary>
        /// 卸载当前活跃存档（不保存）
        /// </summary>
        public void UnloadProfile()
        {
            ActiveProfile = null;
        }

        /// <summary>
        /// 获取当前活跃存档中的 Database
        /// </summary>
        public T GetDatabase<T>() where T : SaveDatabase
        {
            if (!HasActiveProfile)
            {
                throw new XFrameworkException("[SaveManager] No active profile. Call Load() or CreateSave() first.");
            }

            return ActiveProfile.GetDatabase<T>();
        }

        /// <summary>
        /// 获取所有存档的元数据列表
        /// </summary>
        public List<SaveMeta> GetAllSaveMetas()
        {
            var metas = new List<SaveMeta>();
            if (!Directory.Exists(SaveRootPath))
            {
                return metas;
            }

            foreach (var dir in Directory.GetDirectories(SaveRootPath))
            {
                string metaPath = Path.Combine(dir, MetaFileName);
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var meta = ReadJson<SaveMeta>(metaPath);
                        metas.Add(meta);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[SaveManager] Failed to read meta from {dir}: {e.Message}");
                    }
                }
                else
                {
                    // 没有 meta 的文件夹也作为存档列出
                    metas.Add(new SaveMeta
                    {
                        saveName = Path.GetFileName(dir),
                        displayName = Path.GetFileName(dir),
                    });
                }
            }

            return metas;
        }

        /// <summary>
        /// 检查指定存档是否存在
        /// </summary>
        public bool SaveExists(string saveName)
        {
            return Directory.Exists(Path.Combine(SaveRootPath, saveName));
        }

        /// <summary>
        /// 加载或创建指定存档。存在则加载，不存在则创建。
        /// </summary>
        public SaveProfile LoadOrCreate(string saveName, string displayName = null)
        {
            if (SaveExists(saveName))
            {
                return Load(saveName);
            }

            return CreateSave(saveName, displayName);
        }

        #endregion

        #region Internal

        /// <summary>
        /// 通过反射发现所有标记了 [SaveDatabase] 的类型
        /// </summary>
        private void DiscoverDatabaseTypes()
        {
            var types = Utility.Reflection.GetAssignableTypes(typeof(SaveDatabase), "Assembly-CSharp", "XFrameworkRuntime");
            foreach (var type in types)
            {
                if (type.IsAbstract || !type.IsClass) continue;

                var attr = type.GetCustomAttribute<SaveDatabaseAttribute>();
                if (attr == null)
                {
                    Debug.LogWarning($"[SaveManager] SaveDatabase subclass {type.Name} missing [SaveDatabase] attribute, skipped.");
                    continue;
                }

                // 检查文件名冲突
                bool conflict = false;
                foreach (var existing in registeredDbTypes)
                {
                    var existingAttr = existing.GetCustomAttribute<SaveDatabaseAttribute>();
                    if (existingAttr.FileName == attr.FileName)
                    {
                        Debug.LogError($"[SaveManager] Duplicate file name '{attr.FileName}' between {existing.Name} and {type.Name}.");
                        conflict = true;
                        break;
                    }
                }

                if (!conflict)
                {
                    registeredDbTypes.Add(type);
                }
            }
        }

        /// <summary>
        /// 自动生成递增的存档名
        /// </summary>
        private string GenerateNextSaveName()
        {
            int index = 1;
            while (true)
            {
                string name = $"save_{index:D3}";
                if (!Directory.Exists(Path.Combine(SaveRootPath, name)))
                {
                    return name;
                }

                index++;
            }
        }

        private void WriteJson(string path, object data)
        {
            string json = JsonConvert.SerializeObject(data, jsonSettings);
            File.WriteAllText(path, json);
        }

        private T ReadJson<T>(string path)
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json, jsonSettings);
        }

        #endregion
    }
}
