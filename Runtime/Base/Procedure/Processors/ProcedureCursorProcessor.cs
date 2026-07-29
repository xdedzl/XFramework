using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程鼠标状态处理器
    /// </summary>
    public class ProcedureCursorProcessor : IProcedureProcessor
    {
        private bool m_HasBaseline;
        private CursorLockMode m_BaselineLockMode;
        private bool m_BaselineVisible;

        public void OnRefreshProcedureState(ProcedureRefreshContext context)
        {
            var cursorAttr = context.SubContext?.CursorAttr ?? context.ParentContext?.CursorAttr;
            for (int i = 0; i < context.ParallelBranches.Count; i++)
            {
                var branch = context.ParallelBranches[i];
                cursorAttr = branch.SubContext?.CursorAttr ?? branch.ParentContext?.CursorAttr ?? cursorAttr;
            }

            cursorAttr = context.OverlayContext?.CursorAttr ?? cursorAttr;

            if (cursorAttr != null)
            {
                if (!m_HasBaseline)
                {
                    m_BaselineLockMode = Cursor.lockState;
                    m_BaselineVisible = Cursor.visible;
                    m_HasBaseline = true;
                }

                Cursor.lockState = cursorAttr.CursorLockMode;
                Cursor.visible = cursorAttr.Visible;
            }
            else if (m_HasBaseline)
            {
                Cursor.lockState = m_BaselineLockMode;
                Cursor.visible = m_BaselineVisible;
                m_HasBaseline = false;
            }
        }
    }
}
