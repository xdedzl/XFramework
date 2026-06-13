using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 显示状态检视器 - 参数condition为显示条件判断方法的名称，返回值必须为bool。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class DisplayAttribute : PropertyAttribute
    {
        public string Condition { get; private set; }

        public DisplayAttribute(string condition)
        {
            Condition = condition;
        }
    }
}
