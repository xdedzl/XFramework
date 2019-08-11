using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.UI
{
    /// <summary>
    /// 一个使用字典管理的UI管理器
    /// </summary>
    public class UIMgrDicType : IUIManager
    {
        private RectTransform canvasTransform;
        private RectTransform CanvasTransform
        {
            get
            {
                if (canvasTransform == null)
                {
                    canvasTransform = GameObject.Find("Canvas").GetComponent<RectTransform>();
                }
                return canvasTransform;
            }
        }

        /// <summary>
        /// 存储所有面板Prefab的路径
        /// </summary>
        private Dictionary<string, string> m_PanelPathDict;
        /// <summary>
        /// 保存所有实例化面板的游戏物体身上的BasePanel组件
        /// </summary>
        private Dictionary<string, BasePanel> m_PanelDict;
        /// <summary>
        /// 处于打开状态的面板字典，key为层级
        /// </summary>
        private Dictionary<int, List<BasePanel>> m_OnDisplayPanelDic;

        private ResourceManager m_ResMgr;

        public UIMgrDicType()
        {
            m_OnDisplayPanelDic = new Dictionary<int, List<BasePanel>>();
            InitPathDic();

            m_ResMgr = GameEntry.GetModule<ResourceManager>();
            if (m_ResMgr == null)
            {
                throw new FrameworkException("[UIMgr]加载UI模块之前需要加载Resource模块");
            }
        }

        /// <summary>
        /// 打开面板
        /// </summary>
        public void OpenPanel(string uiname, bool closable, object arg)
        {
            BasePanel panel = GetPanel(uiname);
            if (null == panel)
                return;

            if (m_OnDisplayPanelDic.ContainsKey(panel.Level))
            {
                if (m_OnDisplayPanelDic[panel.Level].Contains(panel))
                {
                    if (closable)
                        ClosePanel(uiname);
                    return;
                }
            }
            else
            {
                m_OnDisplayPanelDic.Add(panel.Level, new List<BasePanel>());
            }

            m_OnDisplayPanelDic[panel.Level].Add(panel);
            if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1)) // 可以改为 if(panel.level > 0)
            {
                m_OnDisplayPanelDic[panel.Level - 1].End().OnPause();
            }

            panel.OnOpen(arg);
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void ClosePanel(string uiname)
        {
            BasePanel panel = GetPanel(uiname);
            if (m_OnDisplayPanelDic.ContainsKey(panel.Level) && m_OnDisplayPanelDic[panel.Level].Contains(panel))
            {
                panel.OnClose();
                m_OnDisplayPanelDic[panel.Level].Remove(panel);
            }

            int index = panel.Level + 1;
            List<BasePanel> temp;
            while (m_OnDisplayPanelDic.ContainsKey(index))
            {
                temp = m_OnDisplayPanelDic[index];
                if (temp.Count > 0)
                {
                    temp.End().OnClose();
                    temp.RemoveAt(temp.Count - 1);
                }
                else
                {
                    break;
                }
            }
            if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1))
            {
                m_OnDisplayPanelDic[panel.Level - 1].End().OnResume();
            }
        }

        /// <summary>
        /// 获取面板
        /// </summary>
        public BasePanel GetPanel(string uiname)
        {
            if (m_PanelDict == null)
            {
                m_PanelDict = new Dictionary<string, BasePanel>();
            }

            m_PanelDict.TryGetValue(uiname, out BasePanel panel);

            if (panel == null)
            {
                // 根据prefab去实例化面板
                m_PanelPathDict.TryGetValue(uiname, out string path);
                GameObject instPanel = GameObject.Instantiate(m_ResMgr.Load<GameObject>(path));

                // UICore与派生类不一定在一个程序集类，所以不能直接用Type.GetType  TODO : 根据不同平台规定路径
                Assembly asmb = Assembly.Load("Assembly-CSharp");
                Type type = asmb.GetType(uiname);
                BasePanel basePanel = (BasePanel)Activator.CreateInstance(type);
                basePanel.Init(instPanel, uiname);
                if (basePanel == null)
                {
                    throw new System.Exception("面板类名错误");
                }
                m_PanelDict.Add(uiname, basePanel);

                Transform uiGroup = CanvasTransform.Find("Level" + basePanel.Level);
                if (uiGroup == null)
                {
                    RectTransform rect;
                    rect = (new GameObject("Level" + basePanel.Level)).AddComponent<RectTransform>();
                    rect.SetParent(CanvasTransform);

                    // 在Canvas的渲染模式为Screen Space-Camera时 在1920*1080的情况下直接设置分辨率有时会获取到1080*1080
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.sizeDelta = Vector2.zero;
                    rect.localPosition = Vector3.zero;
                    rect.localScale = Vector3.one;

                    uiGroup = rect;
                }

                instPanel.transform.SetParent(uiGroup, false);
                return basePanel;
            }
            else
            {
                return panel;
            }
        }

        /// <summary>
        /// 关闭最上层界面
        /// </summary>
        public void CloseTopPanel()
        {
            int level = 0;
            foreach (var item in m_OnDisplayPanelDic.Keys)
            {
                if (item > level)
                    level = item;
            }
            m_OnDisplayPanelDic[level].End().OnClose();
            m_OnDisplayPanelDic.Remove(level);
        }

        /// <summary>
        /// 关闭某一层级的所有面板
        /// </summary>
        public void CloseLevelPanel(int level)
        {
            foreach (var item in m_OnDisplayPanelDic[level])
            {
                item.OnClose();
            }
        }

        /// <summary>
        /// 初始化面板预制体路径字典
        /// </summary>
        private void InitPathDic()
        {
            m_PanelPathDict = new Dictionary<string, string>();
            string uipaths = Resources.Load<TextAsset>("UIPath").text;
            uipaths = uipaths.Replace("\"", "");
            uipaths = uipaths.Replace("\n", "");
            uipaths = uipaths.Replace("\r", "");
            string[] data = uipaths.Split(',');
            string[] nameAndPath;
            for (int i = 0; i < data.Length; i++)
            {
                nameAndPath = data[i].Split(':');
                if (nameAndPath == null || nameAndPath.Length != 2)
                    continue;
                string temp = nameAndPath[1] == "" ? nameAndPath[0] : nameAndPath[1] + "/" + nameAndPath[0];
                //m_PanelPathDict.Add(nameAndPath[0], rootPath + temp);
                m_PanelPathDict.Add(nameAndPath[0], nameAndPath[1] + "/" + nameAndPath[0] + ".prefab");
            }
        }

        #region 接口实现

        public int Priority { get { return 200; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (var item in m_OnDisplayPanelDic.Values)
            {
                for (int i = 0, length = item.Count; i < length; i++)
                {
                    item[i].OnUpdate();
                }
            }
        }

        public void Shutdown()
        {

        }

        #endregion
    }
}