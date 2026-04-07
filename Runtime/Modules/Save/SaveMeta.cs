using System;

namespace XFramework.Save
{
    /// <summary>
    /// 存档元数据，序列化为 meta.json
    /// </summary>
    [Serializable]
    public class SaveMeta
    {
        /// <summary>
        /// 存档文件夹名称（唯一标识）
        /// </summary>
        public string saveName;

        /// <summary>
        /// 存档显示名称（玩家可修改）
        /// </summary>
        public string displayName;

        /// <summary>
        /// 创建时间 (UTC)
        /// </summary>
        public string createdAt;

        /// <summary>
        /// 最后保存时间 (UTC)
        /// </summary>
        public string updatedAt;

        /// <summary>
        /// 存档版本号，用于数据迁移
        /// </summary>
        public int version = 1;
    }
}
