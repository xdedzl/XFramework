using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 子面板基类
    /// </summary>
    public class SubPanelBase : MonoBehaviour
    {
        /// <summary>
        /// 父面板
        /// </summary>
        protected PanelBase parentPanel;

        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public GUIBase this[string key]
        {
            get
            {
                return parentPanel[key];
            }
        }

        internal void Config(PanelBase parentPanel)
        {
            this.parentPanel = parentPanel;
        }

        public virtual void Reg() { }
        public virtual void OnOpen() { }
        public virtual void OnClose() { }
    }
}