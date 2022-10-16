using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.UI
{
    [DependenceModule(typeof(ResourceManager))]
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
        private Dictionary<string, string> m_PanelPathDict = new Dictionary<string, string>();
        /// <summary>
        /// 保存所有实例化面板的游戏物体身上的BasePanel组件
        /// </summary>
        private Dictionary<string, PanelBase> m_PanelDict = new Dictionary<string, PanelBase>();
        /// <summary>
        /// 处于打开状态的面板字典，key为层级
        /// </summary>
        private Dictionary<int, List<PanelBase>> m_OnDisplayPanelDic = new Dictionary<int, List<PanelBase>>();

        private ResourceManager m_ResMgr;

        public UIMgrDicType()
        {
            InitPathDic();

            m_ResMgr = GameEntry.GetModule<ResourceManager>();
            if (m_ResMgr == null)
            {
                throw new XFrameworkException("[UIMgr] 加载UI模块之前需要加载Resource模块");
            }
        }

        /// <summary>
        /// 打开面板
        /// </summary>
        public void OpenPanel(string uiname, bool closable, params object[] args)
        {
            PanelBase panel = GetPanel(uiname);
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
                m_OnDisplayPanelDic.Add(panel.Level, new List<PanelBase>());
            }

            m_OnDisplayPanelDic[panel.Level].Add(panel);
            if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1)) // 可以改为 if(panel.level > 0)
            {
                m_OnDisplayPanelDic[panel.Level - 1].End().OnPause();
            }

            panel.OnOpen(args);
            panel.OpenSubPanels();
        }

        /// <summary>
        /// 关闭面板
        /// </summary>
        public void ClosePanel(string uiname)
        {
            PanelBase panel = GetPanel(uiname);
            if (m_OnDisplayPanelDic.ContainsKey(panel.Level) && m_OnDisplayPanelDic[panel.Level].Contains(panel))
            {
                panel.OnClose();
                panel.CloseSubPanels();
                m_OnDisplayPanelDic[panel.Level].Remove(panel);
            }

            int index = panel.Level + 1;
            List<PanelBase> temp;
            while (m_OnDisplayPanelDic.ContainsKey(index))
            {
                temp = m_OnDisplayPanelDic[index];
                if (temp.Count > 0)
                {
                    temp.End().OnClose();
                    temp.End().CloseSubPanels();

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
        public PanelBase GetPanel(string uiname)
        {
            if(m_PanelDict.TryGetValue(uiname, out PanelBase panel))
            {
                if (panel == null)
                    throw new XFrameworkException("[UI] The panel you want has been unloaded");
                return panel;
            }
            else
            {
                // 根据prefab去实例化面板
                m_PanelPathDict.TryGetValue(uiname, out string path);
                GameObject instPanel = GameObject.Instantiate(m_ResMgr.Load<GameObject>(path));

                // UICore与派生类不一定在一个程序集类，所以不能直接用Type.GetType
                Assembly asmb = Assembly.Load("Assembly-CSharp");
                Type type = asmb.GetType(uiname);

                if (type == null || !type.IsSubclassOf(typeof(PanelBase)))
                {
                    throw new XFrameworkException("[UI] wrong panel name or panel is not inherit BasePanel");
                }

                PanelBase basePanel = instPanel.AddComponent(type) as PanelBase;
                basePanel.Init(uiname);
                m_PanelDict.Add(uiname, basePanel);

                Transform uiGroup = CanvasTransform.Find("Level" + basePanel.Level);
                if (uiGroup == null)
                {
                    RectTransform rect;
                    rect = (new GameObject("Level" + basePanel.Level)).AddComponent<RectTransform>();

                    int siblingIndex = CanvasTransform.childCount;
                    for (int i = 0, length = CanvasTransform.childCount; i < length; i++)
                    {
                        string levelName = CanvasTransform.GetChild(i).name;
                        if(int.TryParse(levelName[levelName.Length - 1].ToString(), out int level))
                        {
                            if (basePanel.Level < level)
                            {
                                siblingIndex = i;
                                break;
                            }
                        }
                    }
                    rect.SetParent(CanvasTransform);
                    rect.SetSiblingIndex(siblingIndex);
                    rect.sizeDelta = CanvasTransform.GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.anchoredPosition3D = Vector3.zero;
                    rect.sizeDelta = Vector2.zero;
                    rect.localScale = Vector3.one;
                    uiGroup = rect;
                }
                instPanel.transform.SetParent(uiGroup, false);
                return basePanel;
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
            if(m_OnDisplayPanelDic.TryGetValue(level, out List<PanelBase> panels))
            {
                if(panels.Count > 0)
                {
                    panels.End().OnClose();
                    panels.End().CloseSubPanels();
                }

                if(panels.Count == 1)
                {
                    m_OnDisplayPanelDic.Remove(level);
                }
            }
        }

        /// <summary>
        /// 关闭某一层级的所有面板
        /// </summary>
        public void CloseLevelPanel(int level)
        {
            if(m_OnDisplayPanelDic.TryGetValue(level, out List<PanelBase> panels))
            {
                foreach (var item in panels)
                {
                    item.OnClose();
                    item.CloseSubPanels();
                }
                m_OnDisplayPanelDic.Remove(level);
            }
        }

        /// <summary>
        /// 初始化面板预制体路径字典
        /// </summary>
        private void InitPathDic()
        {
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
            m_PanelDict?.Clear();
            m_PanelPathDict.Clear();
            m_OnDisplayPanelDic.Clear();
        }

        #endregion
    }
}