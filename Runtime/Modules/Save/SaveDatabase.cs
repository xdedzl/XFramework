using System;

namespace XFramework.Save
{
    /// <summary>
    /// 标记 SaveDatabase 子类对应的存档文件名（不含扩展名）
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class SaveDatabaseAttribute : Attribute
    {
        /// <summary>
        /// 文件名（不含扩展名），例如 "pet" 对应 pet.json
        /// </summary>
        public string FileName { get; }

        public SaveDatabaseAttribute(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("SaveDatabase file name cannot be null or empty.", nameof(fileName));
            FileName = fileName;
        }
    }

    /// <summary>
    /// 存档数据基类。每个子类对应存档文件夹内的一个 JSON 文件。
    /// 子类必须标记 [SaveDatabase("filename")] 特性。
    /// </summary>
    public abstract class SaveDatabase
    {
        /// <summary>
        /// 存档加载后的回调，可用于数据迁移或修复
        /// </summary>
        public virtual void OnAfterLoad() { }

        /// <summary>
        /// 存档保存前的回调，可用于数据整理
        /// </summary>
        public virtual void OnBeforeSave() { }
    }
}
