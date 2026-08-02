using System;
using UnityEngine;

namespace XFramework
{
    public class PolymorphicListAttribute : PropertyAttribute
    {
        public readonly Type[] types;

        public PolymorphicListAttribute(params Type[] types)
#if UNITY_6000_0_OR_NEWER
            // Unity 6：移除了 useForChildren 字段，用构造
            : base(true)
#endif
        {
#if !UNITY_6000_0_OR_NEWER
            // 团结引擎：base(true) 与 useForChildren 均不支持，保持默认
#endif
            this.types = types ?? Type.EmptyTypes;
        }
    }
}
