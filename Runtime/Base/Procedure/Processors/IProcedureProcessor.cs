namespace XFramework
{
    public class ProcedureRefreshContext
    {
        public ProcedureBase Procedure { get; }
        public SubProcedureBase SubProcedure { get; }
        public ProcedureOverlayBase Overlay { get; }
        public ProcedureAttributeContext ParentContext { get; }
        public ProcedureAttributeContext SubContext { get; }
        public ProcedureAttributeContext OverlayContext { get; }

        public ProcedureRefreshContext(
            ProcedureBase procedure,
            SubProcedureBase subProcedure,
            ProcedureOverlayBase overlay,
            ProcedureAttributeContext parentContext,
            ProcedureAttributeContext subContext,
            ProcedureAttributeContext overlayContext)
        {
            Procedure = procedure;
            SubProcedure = subProcedure;
            Overlay = overlay;
            ParentContext = parentContext;
            SubContext = subContext;
            OverlayContext = overlayContext;
        }
    }

    /// <summary>
    /// 流程处理器接口，用于解耦 ProcedureManager 的自动化业务逻辑。
    /// </summary>
    public interface IProcedureProcessor
    {
        /// <summary>
        /// 在流程状态刷新时被调用。
        /// </summary>
        /// <param name="context">当前流程、子流程、覆盖层及其特性上下文</param>
        void OnRefreshProcedureState(ProcedureRefreshContext context);
    }
}
