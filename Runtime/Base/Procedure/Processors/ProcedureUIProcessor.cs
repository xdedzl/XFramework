using System.Collections.Generic;
using XFramework.UI;

namespace XFramework
{
    /// <summary>
    /// 流程UI打开/关闭处理器
    /// </summary>
    public class ProcedureUIProcessor : IProcedureProcessor
    {
        /// <summary>
        /// 当前由流程管理的UI面板名称
        /// </summary>
        private readonly HashSet<string> m_ProcedureManagedPanels = new();

        public void OnRefreshProcedureState(ProcedureBase procedure, ProcedureAttributeContext subContext, ProcedureAttributeContext parentContext)
        {
            var requiredPanels = new HashSet<string>();
            var uiAttr = subContext?.UIAttr ?? parentContext?.UIAttr;

            if (uiAttr != null)
            {
                foreach (var panelName in uiAttr.PanelNames)
                {
                    requiredPanels.Add(panelName);
                }
            }

            // 关闭不再需要的面板
            foreach (var panelName in m_ProcedureManagedPanels)
            {
                if (!requiredPanels.Contains(panelName))
                {
                    UIManager.Instance.ClosePanel(panelName);
                }
            }

            // 打开需要的面板
            foreach (var panelName in requiredPanels)
            {
                if (!m_ProcedureManagedPanels.Contains(panelName))
                {
                    UIManager.Instance.OpenPanel(panelName);
                }
            }

            m_ProcedureManagedPanels.Clear();
            foreach (var panelName in requiredPanels)
            {
                m_ProcedureManagedPanels.Add(panelName);
            }
        }
    }
}
