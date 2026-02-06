using System;
using System.Collections.Generic;
using UnityEngine;
using XFramework.Event;
using System.Reflection;

namespace XFramework.UI
{
    /// <summary>
    /// 面板基类
    /// </summary>
    [DisallowMultipleComponent]
    public class PanelBase : MonoBehaviour, IComponentFindIgnore
    {
        /// <summary>
        /// UI层级,层级最低地显示在底层
        /// </summary>
        public int Level => GetType().GetCustomAttribute<PanelInfoAttribute>().level;

        /// <summary>
        /// 面板名(全局唯一)
        /// </summary>
        public string PanelName => GetType().GetCustomAttribute<PanelInfoAttribute>().name;
        
        /// <summary>
        /// 面板路径
        /// </summary>
        public string PanelPath => GetType().GetCustomAttribute<PanelInfoAttribute>().path;

        protected RectTransform rect;

        private List<SubPanelBase> m_SubPanels;

        private ComponentFindHelper<XUIBase> m_ComponentFindHelper;

        private readonly EventRegisterHelper _registerHelper;

        /// <summary>
        /// 面板初始化，只会执行一次，在Awake后start前执行
        /// </summary>
        internal void Init()
        {
            m_ComponentFindHelper = ComponentFindHelper<XUIBase>.CreateHelper(this.gameObject);
            rect = transform.GetComponent<RectTransform>();
            Vector3 rectSize = rect.localScale;
            rect.localScale = rectSize;

            OnInit();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        protected virtual void OnInit()
        {

        }

        /// <summary>
        /// 界面显示
        /// </summary>
        public virtual void OnOpen(params object[] args)
        {
            
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
            
        }

        public virtual void OnAfterClose()
        {
            
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


        public void Open(params object[] args)
        {
            UIManager.Instance.OpenPanel(PanelName, args);
        }

        public void Close()
        {
            UIManager.Instance.ClosePanel(PanelName);
        }
        
        
        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public XUIBase this[string key] => m_ComponentFindHelper[key];

        /// <summary>
        /// 创建子面板
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        protected T CreateSubPanel<T>(GameObject obj) where T : SubPanelBase
        {
            m_SubPanels ??= new List<SubPanelBase>();

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

        public T FindNode<T>(string path) where T : UINode, new()
        {
            return UINode.FindNode<T>(transform, path);
        }

        public UINode FindNode(string path)
        {
            return FindNode<UINode>(path);
        }
    }

    public abstract class UINodeBase: MonoBehaviour, IComponentFindIgnore
    {
        private ComponentFindHelper<XUIBase> m_ComponentFindHelper;
        
        protected PanelBase parent { get; private set; }

        /// <summary>
        /// Find UI组件的索引器
        /// </summary>
        public XUIBase this[string key] => parent[key];

        public void Awake()
        {
            m_ComponentFindHelper = ComponentFindHelper<XUIBase>.CreateHelper(this.gameObject);
        }
    }

    public class UINode : UINodeBase
    {
        public static T FindNode<T>(Transform transform, string path) where T : UINodeBase
        {
            var child = transform.Find(path);
            return GetOrAddNode<T>(child.gameObject);
        }
        
        public T FindNode<T>(string path) where T : UINodeBase
        {
            return FindNode<T>(transform, path);
        }
        
        public static T GetOrAddNode<T>(GameObject go, bool forceReplace=true) where T : UINodeBase
        {
            var node = go.GetComponent<UINodeBase>();

            if (node ==null)
            {
                node = go.AddComponent<T>();
            }
            else
            {
                if (typeof(T) != node.GetType())
                {
                    if (forceReplace)
                    {
                        DestroyImmediate(node);
                        node = go.AddComponent<T>();
                    }
                    else
                    {
                        throw new XFrameworkException($"[UI] AddNode type mismatch, name={go.name}, expect={typeof(T)}, actual={node.GetType()}");
                    }
                }
            }

            return node as T;
        }
        
        public static T GetOrAddNode<T>(Transform transform, bool forceReplace=true) where T : UINodeBase
        {
            return GetOrAddNode<T>(transform.gameObject, forceReplace);
        }
    }
}