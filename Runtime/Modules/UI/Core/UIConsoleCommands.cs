using System;
using XFramework.Command;

namespace XFramework.UI
{
    public sealed class UIConsoleCommands : XCommandGroup
    {
        [XCommandEntry("ui-open", name = "打开界面", order = 0, mode = XCommandMode.Runtime, category = "UI", description = "按 PanelInfo 名称打开面板；带 Request 的面板使用默认 Request。", usage = "ui-open PANEL_NAME", displayType = XCommandDisplayType.Hidden)]
        private static object OpenPanel(string argument)
        {
            string panelName = RequirePanelName(argument, "ui-open");
            bool wasOpened = UIManager.Instance.IsPanelOpened(panelName);
            UIManager.Instance.OpenPanel(panelName);
            bool isOpened = UIManager.Instance.IsPanelOpened(panelName);
            return new {
                panelName,
                wasOpened,
                isOpened,
                changed = wasOpened != isOpened,
            };
        }

        [XCommandEntry("ui-close", name = "关闭界面", order = 10, mode = XCommandMode.Runtime, category = "UI", description = "按 PanelInfo 名称关闭已打开的面板。", usage = "ui-close PANEL_NAME", displayType = XCommandDisplayType.Hidden)]
        private static object ClosePanel(string argument)
        {
            string panelName = RequirePanelName(argument, "ui-close");
            bool wasOpened = UIManager.Instance.IsPanelOpened(panelName);
            UIManager.Instance.ClosePanel(panelName);
            bool isOpened = UIManager.Instance.IsPanelOpened(panelName);
            return new {
                panelName,
                wasOpened,
                isOpened,
                changed = wasOpened != isOpened,
            };
        }

        private static string RequirePanelName(string argument, string command)
        {
            if (string.IsNullOrWhiteSpace(argument))
                throw new ArgumentException($"usage: {command} PANEL_NAME");
            return argument.Trim();
        }
    }
}
