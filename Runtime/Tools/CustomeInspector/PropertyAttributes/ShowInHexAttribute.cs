using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 使整数字段在面板中以十六进制形式显示。
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property)]
    public class ShowInHexAttribute : PropertyAttribute
    {
        public int places;

        /// <summary>
        /// 使整数字段在面板中以十六进制形式显示。
        /// 参数为位数，当转换为十六进制的数值位数不足时，在左侧补0，最多8位。
        /// 默认为-1不补位。
        /// </summary>
        /// <param name="places"></param>
        public ShowInHexAttribute(int places = -1)
        {
            if (places > 8)
            {
                Debug.LogError("[ShowInHexAttribute] 最多只能显示8位。");
                places = 8;
            }

            this.places = places;
        }
    }
}
