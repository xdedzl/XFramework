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
            // Unity 6 移除了 useForChildren 字段，需用构造
            : base(true)
#endif
        {
#if !UNITY_6000_0_OR_NEWER
            // 团结引擎(基于2022.3)与2022.3 没有 base(true) 构造，改用字段赋值
            useForChildren = true;
#endif
        }
    }
}
