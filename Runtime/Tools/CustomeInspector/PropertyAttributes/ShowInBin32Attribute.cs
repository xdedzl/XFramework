using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 使整数字段在面板中以32位二进制形式显示。
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
    public class ShowInBin32Attribute : PropertyAttribute
    {
        /// <summary>
        /// 使整数字段在面板中以32位二进制形式显示。
        /// </summary>
        public ShowInBin32Attribute()
        {
        }
    }
}
