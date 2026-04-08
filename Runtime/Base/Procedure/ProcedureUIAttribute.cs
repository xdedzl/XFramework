using System;

namespace XFramework
{
    /// <summary>
    /// 标记流程所需的UI面板，流程切换时自动打开/关闭
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ProcedureUIAttribute : Attribute
    {
        public string[] PanelNames { get; }

        public ProcedureUIAttribute(params string[] panelNames)
        {
            PanelNames = panelNames ?? Array.Empty<string>();
        }
    }
}
