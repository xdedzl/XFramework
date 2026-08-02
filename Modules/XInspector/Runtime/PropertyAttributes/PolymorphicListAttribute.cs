using System;
using UnityEngine;

namespace XFramework
{
    public class PolymorphicListAttribute : PropertyAttribute
    {
        public readonly Type[] types;

        public PolymorphicListAttribute(params Type[] types)
#if UNITY_6000_0_OR_NEWER
            // Unity 6 移除了 useForChildren 字段，需用构造
            : base(true)
#endif
        {
#if !UNITY_6000_0_OR_NEWER
            // 团结引擎(基于2022.3)与2022.3 没有 base(true) 构造，改用字段赋值
            useForChildren = true;
#endif
            this.types = types ?? Type.EmptyTypes;
        }
    }
}
