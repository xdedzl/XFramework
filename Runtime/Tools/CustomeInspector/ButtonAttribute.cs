using System;
using System.Diagnostics;

namespace XFramework
{
    /// <summary>
    /// 按钮检视器
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    [Conditional("UNITY_EDITOR")]
    public sealed class ButtonAttribute : Attribute
    {
        public string Text { get; private set; }
        public EnableMode Mode { get; private set; }
        public string Style { get; private set; }
        public int Order { get; private set; }

        public ButtonAttribute(string text = null, EnableMode mode = EnableMode.Always, string style = "Button", int order = 0)
        {
            Text = text;
            Mode = mode;
            Style = style;
            Order = order;
        }

        /// <summary>
        /// 按钮激活模式
        /// </summary>
        public enum EnableMode
        {
            /// <summary>
            /// 总是激活
            /// </summary>
            Always,
            /// <summary>
            /// 只在编辑模式激活
            /// </summary>
            Editor,
            /// <summary>
            /// 只在运行模式激活
            /// </summary>
            Playmode
        }
    }
}
