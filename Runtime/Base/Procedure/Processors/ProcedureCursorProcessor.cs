using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程鼠标状态处理器
    /// </summary>
    public class ProcedureCursorProcessor : IProcedureProcessor
    {
        private bool m_HasOverlaySnapshot;
        private CursorLockMode m_PreOverlayLockMode;
        private bool m_PreOverlayVisible;

        public void OnRefreshProcedureState(ProcedureRefreshContext context)
        {
            var overlayAttr = context.OverlayContext?.CursorAttr;
            var baseAttr = context.SubContext?.CursorAttr ?? context.ParentContext?.CursorAttr;
            if (overlayAttr != null && !m_HasOverlaySnapshot)
            {
                m_PreOverlayLockMode = Cursor.lockState;
                m_PreOverlayVisible = Cursor.visible;
                m_HasOverlaySnapshot = true;
            }

            var cursorAttr = overlayAttr ?? baseAttr;

            if (cursorAttr != null)
            {
                Cursor.lockState = cursorAttr.CursorLockMode;
                Cursor.visible = cursorAttr.Visible;
            }
            else if (m_HasOverlaySnapshot)
            {
                Cursor.lockState = m_PreOverlayLockMode;
                Cursor.visible = m_PreOverlayVisible;
            }

            if (overlayAttr == null)
            {
                m_HasOverlaySnapshot = false;
            }
        }
    }
}
