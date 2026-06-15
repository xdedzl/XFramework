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
        public XUIBase this[string key]
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
        public virtual void OnBeforeOpen() { }
        public virtual void OnOpened() { }
        public virtual void OnBeforeClose() { }
        public virtual void OnClosed() { }

        public T FindNode<T>(string path) where T : UINodeBase, new()
        {
            return UINode.FindNode<T>(transform, path);
        }
    }
}
