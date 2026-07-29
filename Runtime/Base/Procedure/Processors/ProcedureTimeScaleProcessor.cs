using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程时间缩放处理器
    /// </summary>
    public class ProcedureTimeScaleProcessor : IProcedureProcessor
    {
        private bool m_HasBaseline;
        private float m_BaselineTimeScale;

        public void OnRefreshProcedureState(ProcedureRefreshContext context)
        {
            var timeScaleAttr = context.SubContext?.TimeScaleAttr ?? context.ParentContext?.TimeScaleAttr;
            for (int i = 0; i < context.ParallelBranches.Count; i++)
            {
                var branch = context.ParallelBranches[i];
                timeScaleAttr = branch.SubContext?.TimeScaleAttr ?? branch.ParentContext?.TimeScaleAttr ?? timeScaleAttr;
            }

            timeScaleAttr = context.OverlayContext?.TimeScaleAttr ?? timeScaleAttr;

            if (timeScaleAttr != null)
            {
                if (!m_HasBaseline)
                {
                    m_BaselineTimeScale = Time.timeScale;
                    m_HasBaseline = true;
                }

                Time.timeScale = timeScaleAttr.TimeScale;
            }
            else if (m_HasBaseline)
            {
                Time.timeScale = m_BaselineTimeScale;
                m_HasBaseline = false;
            }
        }
    }
}
