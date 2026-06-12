using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 覆盖字段在 Unity Inspector 中的显示名称。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class LabelAttribute : PropertyAttribute
    {
        public string Text { get; }

        public LabelAttribute(string text)
        {
            Text = text;
        }
    }
}
