using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程鼠标状态处理器
    /// </summary>
    public class ProcedureCursorProcessor : IProcedureProcessor
    {
        public void OnRefreshProcedureState(ProcedureBase procedure, ProcedureAttributeContext subContext, ProcedureAttributeContext parentContext)
        {
            var cursorAttr = subContext?.CursorAttr ?? parentContext?.CursorAttr;

            if (cursorAttr != null)
            {
                Cursor.lockState = cursorAttr.CursorLockMode;
                Cursor.visible = cursorAttr.Visible;
            }
        }
    }
}
