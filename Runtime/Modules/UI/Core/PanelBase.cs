using System.Collections.Generic;
using UnityEngine;
using XFramework.Event;

namespace XFramework.UI
{
    /// <summary>
    /// 面板基类
    /// </summary>
    public class PanelBase : MonoBehaviour
    {
        /// <summary>
        /// UI层级,层级最低的显示在底层
        /// </summary>
        public int Level { get; protected set; }
        /// <summary>
        /// 面板名
        /// </summary>
        public string Name { get; protected set; }

        protected RectTransform rect;

        private List<SubPanelBase> m_SubPanels;

        private ComponentFindHelper<XUIBase> m_ComponentFindHelper;

        private readonly EventRegersterHelper regersterHelper;

        /// <summary>
        /// 面板初始化，只会执行一次，在Awake后start前执行
        /// </summary>
        internal void Init(string name)
        {
            Name = name;
            m_ComponentFindHelper = ComponentFindHelper<XUIBase>.CreateHelper(this.gameObject);
            rect = transform.GetComponent<RectTransform>();
            Vector3 rectSize = rect.localScale;
            rect.localScale = rectSize;

            OnInit();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        public virtual void OnInit()
        {

        }

        /// <summary>
        /// 界面显示
        /// </summary>
        public virtual void OnOpen(params object[] args)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        internal void OpenSubPanels()
        {
            if (m_SubPanels != null)
            {
                foreach (var item in m_SubPanels)
                {
                    item.OnOpen();
                }
            }
        }

        /// <summary>
        /// 每帧运行
        /// </summary>
        public virtual void OnUpdate()
        {

        }

        /// <summary>
        /// 界面暂停,被遮挡
        /// </summary>
        public virtual void OnPause()
        {

        }

        /// <summary>
        /// 界面恢复
        /// </summary>
        public virtual void OnResume()
        {

        }

        /// <summary>
        /// 退出界面，界面被关闭
        /// </summary>
        public virtual void OnClose()
        {
            gameObject.SetActive(false);
        }

        internal void CloseSubPanels()
        {
            if (m_SubPanels != null)
            {
                foreach (var item in m_SubPanels)
                {
                    item.OnClose();
                }
            }
        }

        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public XUIBase this[string key]
        {
            get
            {
                return m_ComponentFindHelper[key];
            }
        }

        /// <summary>
        /// 创建子面板
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T CreateSubPanel<T>(GameObject obj) where T : SubPanelBase
        {
            if(m_SubPanels == null)
            {
                m_SubPanels = new List<SubPanelBase>();
            }

            T subPanel = obj.AddComponent<T>();
            subPanel.Config(this);
            subPanel.Reg();
            m_SubPanels.Add(subPanel);

            return subPanel;
        }

        public T Find<T>(string path) where T : XUIBase
        {
            return this[path] as T;
        }

        public T FindNode<T>(string path) where T : UINodeBase, new()
        {
            var child = transform.Find(path);
            if (child == null)
            {
                throw new XFrameworkException($"[UI] FindNode, node not exist, path={path}");
            }
            var uiObj = new T();
            uiObj.Init(child, this);
            return uiObj;
        }
    }

    public class UINodeBase
    {
        public Transform transform { get; private set; }
        protected PanelBase parent { get; private set; }

        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public XUIBase this[string key]
        {
            get
            {
                return parent[key];
            }
        }

        internal void Init(Transform transform, PanelBase panel)
        {
            this.transform = transform;
            this.parent = panel;
            OnInit();
        }

        public T Find<T>(string path) where T : UINodeBase, new()
        {
            var child = transform.Find(path);
            var uiObj = new T();
            uiObj.Init(child, parent);
            return uiObj;
        }

        protected virtual void OnInit()
        {

        }

        public T GetComponent<T>() where T : Component
        {
            return transform.GetComponent<T>();
        }
    }
}