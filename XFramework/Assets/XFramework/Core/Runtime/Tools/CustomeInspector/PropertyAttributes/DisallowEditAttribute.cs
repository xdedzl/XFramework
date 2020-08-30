using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 使字段在Inspector面板中不可编辑。
    /// </summary>
    public class DisallowEditAttribute : PropertyAttribute
    {
        /// <summary>
        /// 使字段在Inspector面板中不可编辑。
        /// </summary>
        public DisallowEditAttribute() { }
    }
}
