using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Draws a list or one-dimensional array with a compact reorderable list UI.
    /// </summary>
    public class PrettyListAttribute : PropertyAttribute
    {
        public PrettyListAttribute()
#if UNITY_6000_0_OR_NEWER
            // Unity 6：移除了 useForChildren 字段，用构造
            : base(true)
#endif
        {
#if !UNITY_6000_0_OR_NEWER
            // 团结引擎：base(true) 与 useForChildren 均不支持，保持默认
#endif
        }
    }
}
