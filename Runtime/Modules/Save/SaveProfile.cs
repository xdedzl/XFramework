using System;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework.Save
{
    /// <summary>
    /// 一个存档的运行时表示。包含元数据和所有 Database 实例。
    /// </summary>
    public class SaveProfile
    {
        /// <summary>
        /// 存档元数据
        /// </summary>
        public SaveMeta Meta { get; set; }

        /// <summary>
        /// 存档文件夹路径（绝对路径）
        /// </summary>
        public string DirectoryPath { get; set; }

        // Type -> SaveDatabase 实例
        private readonly Dictionary<Type, SaveDatabase> databases = new Dictionary<Type, SaveDatabase>();

        /// <summary>
        /// 获取指定类型的 Database
        /// </summary>
        public T GetDatabase<T>() where T : SaveDatabase
        {
            var type = typeof(T);
            if (databases.TryGetValue(type, out var db))
            {
                return (T)db;
            }

            throw new XFrameworkException($"[SaveProfile] Database type {type.Name} not found in save '{Meta?.saveName}'. " +
                                          $"Make sure the class has [SaveDatabase] attribute.");
        }

        /// <summary>
        /// 尝试获取指定类型的 Database
        /// </summary>
        public bool TryGetDatabase<T>(out T database) where T : SaveDatabase
        {
            if (databases.TryGetValue(typeof(T), out var db))
            {
                database = (T)db;
                return true;
            }

            database = null;
            return false;
        }

        /// <summary>
        /// 设置一个 Database 实例
        /// </summary>
        internal void SetDatabase(Type type, SaveDatabase database)
        {
            databases[type] = database;
        }

        /// <summary>
        /// 获取所有 Database
        /// </summary>
        internal IEnumerable<KeyValuePair<Type, SaveDatabase>> GetAllDatabases()
        {
            return databases;
        }

        /// <summary>
        /// 获取指定 SaveDatabase 类型的文件名
        /// </summary>
        internal static string GetFileName(Type dbType)
        {
            var attr = dbType.GetCustomAttribute<SaveDatabaseAttribute>();
            if (attr == null)
            {
                throw new XFrameworkException($"[SaveProfile] SaveDatabase type {dbType.Name} missing [SaveDatabase] attribute.");
            }

            return attr.FileName + ".json";
        }
    }
}
