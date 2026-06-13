using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 激活状态检视器 - 参数condition为激活条件判断方法的名称，返回值必须为bool。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class EnableAttribute : PropertyAttribute
    {
        public string Condition { get; private set; }

        public EnableAttribute(string condition)
        {
            Condition = condition;
        }
    }
}
