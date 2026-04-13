namespace XFramework
{
    /// <summary>
    /// 流程处理器接口，用于解耦 ProcedureManager 的自动化业务逻辑。
    /// </summary>
    public interface IProcedureProcessor
    {
        /// <summary>
        /// 在流程状态刷新时被调用。
        /// </summary>
        /// <param name="procedure">当前流程实例</param>
        /// <param name="subContext">子流程的特性上下文（如果存在的话）</param>
        /// <param name="parentContext">父流程的特性上下文</param>
        void OnRefreshProcedureState(ProcedureBase procedure, ProcedureAttributeContext subContext, ProcedureAttributeContext parentContext);
    }
}
