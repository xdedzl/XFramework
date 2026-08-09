using System;
using System.Collections.Generic;

namespace XFramework
{
    public readonly struct ProcedureCacheDebugSnapshot
    {
        public ProcedureCacheDebugSnapshot(
            ProcedureBase procedure,
            IReadOnlyList<SubProcedureBase> subProcedures)
        {
            Procedure = procedure;
            SubProcedures = subProcedures;
        }

        public ProcedureBase Procedure { get; }
        public IReadOnlyList<SubProcedureBase> SubProcedures { get; }
    }

    public readonly struct ParallelProcedureDebugSnapshot
    {
        public ParallelProcedureDebugSnapshot(
            ParallelProcedureBase procedure,
            SubProcedureBase currentSubProcedure,
            int priority)
        {
            Procedure = procedure;
            CurrentSubProcedure = currentSubProcedure;
            Priority = priority;
        }

        public ParallelProcedureBase Procedure { get; }
        public SubProcedureBase CurrentSubProcedure { get; }
        public int Priority { get; }
    }

    public readonly struct ProcedureManagerDebugSnapshot
    {
        public ProcedureManagerDebugSnapshot(
            MainProcedure currentProcedure,
            SubProcedureBase currentSubProcedure,
            ProcedureOverlayBase currentOverlay,
            IReadOnlyList<ProcedureCacheDebugSnapshot> cachedProcedures,
            IReadOnlyList<string> managedPanelNames,
            string activeCameraName,
            IReadOnlyList<Type> processorTypes)
            : this(
                currentProcedure,
                currentSubProcedure,
                currentOverlay,
                Array.Empty<ParallelProcedureDebugSnapshot>(),
                cachedProcedures,
                managedPanelNames,
                activeCameraName,
                processorTypes)
        {
        }

        public ProcedureManagerDebugSnapshot(
            MainProcedure currentProcedure,
            SubProcedureBase currentSubProcedure,
            ProcedureOverlayBase currentOverlay,
            IReadOnlyList<ParallelProcedureDebugSnapshot> parallelProcedures,
            IReadOnlyList<ProcedureCacheDebugSnapshot> cachedProcedures,
            IReadOnlyList<string> managedPanelNames,
            string activeCameraName,
            IReadOnlyList<Type> processorTypes)
        {
            CurrentProcedure = currentProcedure;
            CurrentSubProcedure = currentSubProcedure;
            CurrentOverlay = currentOverlay;
            ParallelProcedures = parallelProcedures;
            CachedProcedures = cachedProcedures;
            ManagedPanelNames = managedPanelNames;
            ActiveCameraName = activeCameraName;
            ProcessorTypes = processorTypes;
        }

        public MainProcedure CurrentProcedure { get; }
        public SubProcedureBase CurrentSubProcedure { get; }
        public ProcedureOverlayBase CurrentOverlay { get; }
        public IReadOnlyList<ParallelProcedureDebugSnapshot> ParallelProcedures { get; }
        public IReadOnlyList<ProcedureCacheDebugSnapshot> CachedProcedures { get; }
        public IReadOnlyList<string> ManagedPanelNames { get; }
        public string ActiveCameraName { get; }
        public IReadOnlyList<Type> ProcessorTypes { get; }
    }

    public partial class ProcedureManager
    {
        public ProcedureManagerDebugSnapshot GetDebugSnapshot()
        {
            var cachedProcedures = new List<ProcedureCacheDebugSnapshot>(m_ProcedureDic.Count);
            foreach (ProcedureBase procedure in m_ProcedureDic.Values)
            {
                cachedProcedures.Add(new ProcedureCacheDebugSnapshot(
                    procedure,
                    procedure.GetDebugSubProcedures()));
            }

            cachedProcedures.Sort((left, right) => string.Compare(
                left.Procedure.GetType().FullName,
                right.Procedure.GetType().FullName,
                StringComparison.Ordinal));

            IReadOnlyList<string> managedPanels = Array.Empty<string>();
            string activeCameraName = null;
            var processorTypes = new List<Type>(m_Processors.Count);
            foreach (IProcedureProcessor processor in m_Processors)
            {
                processorTypes.Add(processor.GetType());
                if (processor is ProcedureUIProcessor uiProcessor)
                {
                    managedPanels = uiProcessor.GetDebugManagedPanels();
                }
                else if (processor is ProcedureCameraProcessor cameraProcessor)
                {
                    activeCameraName = cameraProcessor.DebugActiveCameraName;
                }
            }

            var parallelProcedures = new List<ParallelProcedureDebugSnapshot>(m_ParallelProcedures.Count);
            for (int i = 0; i < m_ParallelProcedures.Count; i++)
            {
                var procedure = m_ParallelProcedures[i];
                parallelProcedures.Add(new ParallelProcedureDebugSnapshot(
                    procedure,
                    procedure.CurrentSubProcedure,
                    GetParallelProcedurePriority(procedure.GetType())));
            }

            return new ProcedureManagerDebugSnapshot(
                m_CurrentProcedure,
                CurrenSubProcedure,
                m_CurrentOverlay,
                parallelProcedures,
                cachedProcedures,
                managedPanels,
                activeCameraName,
                processorTypes.ToArray());
        }
    }
}
