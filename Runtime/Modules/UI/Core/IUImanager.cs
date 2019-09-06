/*
 * 想在自己的框架里写几种不同的UI管理方式
 * 外部通过UIHelper调用IUIManager
 * 在UIHelper中觉得以哪一种UIManager中管理
 * 实际项目中UI管理器以接口的形式出现可以方便的进行扩展，但在大多数情况下，一种UI管理方式就够了
 * UIManager只负责管理多个UI面板之间的逻辑，其余扩展性的功能交给UIHelper
 */

namespace XFramework.UI
{
    /// <summary>
    /// UI管理器接口
    /// </summary>
    public interface IUIManager : IGameModule
    {
        /// <summary>
        /// 打开面板
        /// </summary>
        void OpenPanel(string uiname, bool closable, object arg);
        /// <summary>
        /// 关闭面板
        /// </summary>
        void ClosePanel(string uiname);
        /// <summary>
        /// 获取面板
        /// </summary>
        PanelBase GetPanel(string uiname);
        /// <summary>
        /// 关闭最近一次打开的面板
        /// </summary>
        void CloseTopPanel();
    }
}