using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

        public UIMgrDicType()
        {
            m_OnDisplayPanelDic = new Dictionary<int, List<BasePanel>>();
            InitPathDic();
        }

        /// <summary>
        /// 打开面板
        /// </summary>
        public void OpenPanel(string uiname, object arg)
        {
            BasePanel panel = GetPanel(uiname);
            if (null == panel)
                return;

            if (m_OnDisplayPanelDic.ContainsKey(panel.Level))
            {
                if (m_OnDisplayPanelDic[panel.Level].Contains(panel))
                {
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
                GameObject instPanel = GameObject.Instantiate(Resources.Load(path)) as GameObject;

                // UICore与派生类不一定在一个程序集类，所以不能直接用Type.GetType
                Assembly asmb = Assembly.Load("Assembly-CSharp");
                Type type = asmb.GetType(uiname);
                BasePanel basePanel = Activator.CreateInstance(type) as BasePanel;
                basePanel.Init(instPanel, uiname);
                if (basePanel == null)
                {
                    throw new FrameworkException("[UI]面板类名错误");
                }
                m_PanelDict.Add(uiname, basePanel);

                Transform uiGroup = CanvasTransform.Find("Level" + basePanel.Level);
                if (uiGroup == null)
                {
                    RectTransform rect;
                    rect = (new GameObject("Level" + basePanel.Level)).AddComponent<RectTransform>();
                    rect.SetParent(CanvasTransform);
                    rect.sizeDelta = CanvasTransform.sizeDelta;
                    rect.position = CanvasTransform.position;
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
            string rootPath = "UIPanelPrefabs/";
            TextAsset textAsset = Resources.Load<TextAsset>("UIPath");
            if (textAsset == null)
            {
                Debug.LogWarning("没有UI面板的路径配置文件或文件路径有误");
                return;
            }
            string uipaths = textAsset.text;
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
                m_PanelPathDict.Add(nameAndPath[0], rootPath + temp);
            }
        }

        public int Priority { get { return 4000; } }

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
    }
}