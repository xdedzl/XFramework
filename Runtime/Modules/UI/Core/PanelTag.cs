using System;

namespace XFramework.UI
{
    /// <summary>
    /// 声明面板打开期间持有的标签。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class PanelTagAttribute : Attribute
    {
        public PanelTagAttribute(params string[] tags)
        {
            Tags = tags ?? Array.Empty<string>();
        }

        public string[] Tags { get; }
    }

    /// <summary>
    /// 项目侧实现此接口，为一个面板标签定义具体行为。
    /// </summary>
    public interface IPanelTagHandler
    {
        string Tag { get; }

        void OnTagStateChanged(bool active);
    }
}
