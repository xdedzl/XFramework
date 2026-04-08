using System;

namespace XFramework
{
    /// <summary>
    /// 标记流程所需的模块类型，流程切换时自动加载/卸载
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ProcedureModuleAttribute : Attribute
    {
        public Type[] ModuleTypes { get; }

        public ProcedureModuleAttribute(params Type[] moduleTypes)
        {
            ModuleTypes = moduleTypes ?? Array.Empty<Type>();
        }
    }
}
