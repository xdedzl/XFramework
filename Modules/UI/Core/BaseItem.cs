using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// UI元素数据类
    /// </summary>
    public class BaseItem
    {
        /// <summary>
        /// 对应的Transform
        /// </summary>
        public Transform transform;
        public virtual void OnInit() { }
        public virtual void OnPanelOpen() { }
        public virtual void OnPanelClose() { }
    }
}