using System;
using System.Reflection;

namespace XFramework
{
    /// <summary>
    /// 流程特性缓存上下文。
    /// 避免在流程切换时重复执行高开销的反射调用。
    /// </summary>
    public class ProcedureAttributeContext
    {
        public ProcedureModuleAttribute ModuleAttr { get; private set; }
        public ProcedureUIAttribute UIAttr { get; private set; }
        public ProcedureCameraAttribute CameraAttr { get; private set; }
        public ProcedureCursorAttribute CursorAttr { get; private set; }
        public ProcedureTimeScaleAttribute TimeScaleAttr { get; private set; }

        public ProcedureAttributeContext(Type procedureType)
        {
            if (procedureType == null) return;

            // 一次性通过反射获取所有已知特性并缓存
            ModuleAttr = procedureType.GetCustomAttribute<ProcedureModuleAttribute>();
            UIAttr = procedureType.GetCustomAttribute<ProcedureUIAttribute>();
            CameraAttr = procedureType.GetCustomAttribute<ProcedureCameraAttribute>();
            CursorAttr = procedureType.GetCustomAttribute<ProcedureCursorAttribute>();
            TimeScaleAttr = procedureType.GetCustomAttribute<ProcedureTimeScaleAttribute>();
        }
    }
}
