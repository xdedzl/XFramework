using System;
using System.Diagnostics;

namespace XFramework
{
    /// <summary>
    /// 在 XMonoBehaviourInspector 中显示字段。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    [Conditional("UNITY_EDITOR")]
    public sealed class ShowInInspectorAttribute : Attribute
    {
    }
}
