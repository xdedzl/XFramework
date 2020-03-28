using System.Collections.Generic;
using UnityEngine;

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

        private Dictionary<string, GUIBase> mUIDic;

        private List<SubPanelBase> m_SubPanels;

        /// <summary>
        /// 面板初始化，只会执行一次，在Awake后start前执行
        /// </summary>
        internal void Init(string name)
        {
            Name = name;
            InitGUIDic();
            rect = transform.GetComponent<RectTransform>();
            Vector3 rectSize = rect.localScale;
            rect.localScale = rectSize;

            Reg();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        public virtual void Reg()
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
        /// 初始化UI组件字典
        /// </summary>
        private void InitGUIDic()
        {
            mUIDic = new Dictionary<string, GUIBase>();
            GUIBase[] uis = transform.GetComponentsInChildren<GUIBase>();
            for (int i = 0; i < uis.Length; i++)
            {
                if (mUIDic.ContainsKey(uis[i].name))
                {
                    throw new FrameworkException($"{this.name}已有名为{uis[i].name}的GUBase组件");
                }
                mUIDic.Add(uis[i].name, uis[i]);
            }
        }

        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public GUIBase this[string key]
        {
            get
            {
                if (mUIDic.ContainsKey(key))
                    return mUIDic[key];
                else
                {
                    throw new System.Exception(this + " : 没有名为" + key + "的UI组件");
                }
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
    }
}