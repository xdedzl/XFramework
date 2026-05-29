using System;

namespace XFramework
{
    public enum ProcedureAttributeMode
    {
        Replace,
        Additive
    }

    /// <summary>
    /// 标记流程所需的UI面板，流程切换时自动打开/关闭
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ProcedureUIAttribute : Attribute
    {
        public string[] PanelNames { get; }
        public ProcedureAttributeMode Mode { get; }

        public ProcedureUIAttribute(params string[] panelNames)
        {
            Mode = ProcedureAttributeMode.Replace;
            PanelNames = panelNames ?? Array.Empty<string>();
        }

        public ProcedureUIAttribute(ProcedureAttributeMode mode, params string[] panelNames)
        {
            Mode = mode;
            PanelNames = panelNames ?? Array.Empty<string>();
        }
    }
}
