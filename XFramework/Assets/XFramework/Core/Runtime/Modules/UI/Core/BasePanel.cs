using System.Collections.Generic;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 面板基类
    /// </summary>
    public class BasePanel
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

        private Dictionary<string, BaseGUI> mUIDic;

        protected Transform transform;
        protected GameObject gameObject;

        /// <summary>
        /// 面板初始化，只会执行一次，在Awake后start前执行
        /// </summary>
        public void Init(GameObject _gameObject, string name)
        {
            Name = name;
            gameObject = _gameObject;
            transform = _gameObject.transform;
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
        public virtual void OnOpen(object arg)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
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

        /// <summary>
        /// 初始化UI组件字典
        /// </summary>
        private void InitGUIDic()
        {
            mUIDic = new Dictionary<string, BaseGUI>();
            BaseGUI[] uis = transform.GetComponentsInChildren<BaseGUI>();
            for (int i = 0; i < uis.Length; i++)
            {
                mUIDic.Add(uis[i].name, uis[i]);
            }
        }

        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public BaseGUI this[string key]
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
    }
}