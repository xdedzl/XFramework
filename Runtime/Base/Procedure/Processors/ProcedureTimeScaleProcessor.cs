using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程时间缩放处理器
    /// </summary>
    public class ProcedureTimeScaleProcessor : IProcedureProcessor
    {
        private bool m_HasOverlaySnapshot;
        private float m_PreOverlayTimeScale;

        public void OnRefreshProcedureState(ProcedureRefreshContext context)
        {
            var overlayAttr = context.OverlayContext?.TimeScaleAttr;
            var baseAttr = context.SubContext?.TimeScaleAttr ?? context.ParentContext?.TimeScaleAttr;
            if (overlayAttr != null && !m_HasOverlaySnapshot)
            {
                m_PreOverlayTimeScale = Time.timeScale;
                m_HasOverlaySnapshot = true;
            }

            var timeScaleAttr = overlayAttr ?? baseAttr;

            if (timeScaleAttr != null)
            {
                Time.timeScale = timeScaleAttr.TimeScale;
            }
            else if (m_HasOverlaySnapshot)
            {
                Time.timeScale = m_PreOverlayTimeScale;
            }

            if (overlayAttr == null)
            {
                m_HasOverlaySnapshot = false;
            }
        }
    }
}
