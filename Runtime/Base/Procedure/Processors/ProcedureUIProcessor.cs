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

        public void OnRefreshProcedureState(ProcedureRefreshContext context)
        {
            var requiredPanels = new HashSet<string>();
            ApplyPanels(requiredPanels, context.SubContext?.UIAttr ?? context.ParentContext?.UIAttr);
            for (int i = 0; i < context.ParallelBranches.Count; i++)
            {
                var branch = context.ParallelBranches[i];
                ApplyPanels(requiredPanels, branch.SubContext?.UIAttr ?? branch.ParentContext?.UIAttr);
            }
            ApplyPanels(requiredPanels, context.OverlayContext?.UIAttr);

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

        private void ApplyPanels(HashSet<string> panels, ProcedureUIAttribute uiAttr)
        {
            if (uiAttr == null)
            {
                return;
            }

            if (uiAttr.Mode == ProcedureAttributeMode.Replace)
            {
                panels.Clear();
            }

            foreach (var panelName in uiAttr.PanelNames)
            {
                panels.Add(panelName);
            }
        }

        internal IReadOnlyList<string> GetDebugManagedPanels()
        {
            var panels = new List<string>(m_ProcedureManagedPanels);
            panels.Sort();
            return panels;
        }
    }
}
