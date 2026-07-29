using System;
using System.Collections.Generic;

namespace XFramework
{
    public class ProcedureBranchContext
    {
        public ProcedureBase Procedure { get; }
        public SubProcedureBase SubProcedure { get; }
        public ProcedureAttributeContext ParentContext { get; }
        public ProcedureAttributeContext SubContext { get; }
        public int Priority { get; }

        public ProcedureBranchContext(
            ProcedureBase procedure,
            SubProcedureBase subProcedure,
            ProcedureAttributeContext parentContext,
            ProcedureAttributeContext subContext,
            int priority)
        {
            Procedure = procedure;
            SubProcedure = subProcedure;
            ParentContext = parentContext;
            SubContext = subContext;
            Priority = priority;
        }
    }

    public class ProcedureRefreshContext
    {
        public ProcedureBase Procedure { get; }
        public SubProcedureBase SubProcedure { get; }
        public ProcedureOverlayBase Overlay { get; }
        public ProcedureAttributeContext ParentContext { get; }
        public ProcedureAttributeContext SubContext { get; }
        public ProcedureAttributeContext OverlayContext { get; }
        public IReadOnlyList<ProcedureBranchContext> ParallelBranches { get; }

        public ProcedureRefreshContext(
            ProcedureBase procedure,
            SubProcedureBase subProcedure,
            ProcedureOverlayBase overlay,
            ProcedureAttributeContext parentContext,
            ProcedureAttributeContext subContext,
            ProcedureAttributeContext overlayContext)
            : this(
                procedure,
                subProcedure,
                overlay,
                parentContext,
                subContext,
                overlayContext,
                Array.Empty<ProcedureBranchContext>())
        {
        }

        public ProcedureRefreshContext(
            ProcedureBase procedure,
            SubProcedureBase subProcedure,
            ProcedureOverlayBase overlay,
            ProcedureAttributeContext parentContext,
            ProcedureAttributeContext subContext,
            ProcedureAttributeContext overlayContext,
            IReadOnlyList<ProcedureBranchContext> parallelBranches)
        {
            Procedure = procedure;
            SubProcedure = subProcedure;
            Overlay = overlay;
            ParentContext = parentContext;
            SubContext = subContext;
            OverlayContext = overlayContext;
            ParallelBranches = parallelBranches;
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
