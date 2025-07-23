using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using XFramework.Resource;

namespace XFramework.UI
{
    class MainPanelAttribute : Attribute { }



    /// <summary>
    /// һ��ʹ���ֵ�����UI������
    /// </summary>
    public class UIManager : GameModuleBase<UIManager>
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
        /// �洢�������Prefab��·��
        /// </summary>
        private Dictionary<string, string> m_PanelPathDict = new Dictionary<string, string>();
        /// <summary>
        /// ��������ʵ����������Ϸ�������ϵ�BasePanel���
        /// </summary>
        private Dictionary<string, PanelBase> m_PanelDict = new Dictionary<string, PanelBase>();
        /// <summary>
        /// ���ڴ�״̬������ֵ䣬keyΪ�㼶
        /// </summary>
        private Dictionary<int, List<PanelBase>> m_OnDisplayPanelDic = new Dictionary<int, List<PanelBase>>();


        public UIManager()
        {
            InitPathDic();
        }

        /// <summary>
        /// �����
        /// </summary>
        public void OpenPanel(string uiname, params object[] args)
        {
            PanelBase panel = GetPanel(uiname);
            if (null == panel)
                return;

            if (m_OnDisplayPanelDic.ContainsKey(panel.Level))
            {
                if (m_OnDisplayPanelDic[panel.Level].Contains(panel))
                {
                    return;
                }
            }
            else
            {
                m_OnDisplayPanelDic.Add(panel.Level, new List<PanelBase>());
            }

            m_OnDisplayPanelDic[panel.Level].Add(panel);
            if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1)) // ���Ը�Ϊ if(panel.level > 0)
            {
                m_OnDisplayPanelDic[panel.Level - 1].End().OnPause();
            }

            panel.OnOpen(args);
            panel.OpenSubPanels();
        }

        /// <summary>
        /// �ر����
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
        /// ��ȡ���
        /// </summary>
        private PanelBase GetPanel(string uiname)
        {
            if (m_PanelDict.TryGetValue(uiname, out PanelBase panel))
            {
                if (panel == null)
                    throw new XFrameworkException("[UI] The panel you want has been unloaded");
                return panel;
            }
            else
            {
                // ����prefabȥʵ�������
                m_PanelPathDict.TryGetValue(uiname, out string path);
                GameObject instPanel = DefaultPanelLoader(path);
                PanelBase basePanel = instPanel.GetComponent<PanelBase>();
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
                        if (int.TryParse(levelName[levelName.Length - 1].ToString(), out int level))
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
        /// �ر����ϲ����
        /// </summary>
        public void CloseTopPanel()
        {
            int level = 0;
            foreach (var item in m_OnDisplayPanelDic.Keys)
            {
                if (item > level)
                    level = item;
            }
            if (m_OnDisplayPanelDic.TryGetValue(level, out List<PanelBase> panels))
            {
                if (panels.Count > 0)
                {
                    panels.End().OnClose();
                    panels.End().CloseSubPanels();
                }

                if (panels.Count == 1)
                {
                    m_OnDisplayPanelDic.Remove(level);
                }
            }
        }

        /// <summary>
        /// �ر�ĳһ�㼶���������
        /// </summary>
        public void CloseLevelPanel(int level)
        {
            if (m_OnDisplayPanelDic.TryGetValue(level, out List<PanelBase> panels))
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
        /// ��ʼ�����Ԥ����·���ֵ�
        /// </summary>
        private void InitPathDic()
        {
            string uipaths = Resources.Load<TextAsset>("Panels/UIPath").text;
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
                m_PanelPathDict.Add(nameAndPath[0].Trim(), nameAndPath[1].Trim());
            }
        }

        internal void RegisterExistPanel(PanelBase panel)
        {

        }

        private GameObject DefaultPanelLoader(string path)
        {
            var res = Resources.Load<GameObject>(path);
            return GameObject.Instantiate(res);
        }

        #region �ӿ�ʵ��

        public override int Priority => 200;

        public override void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (var item in m_OnDisplayPanelDic.Values)
            {
                for (int i = 0, length = item.Count; i < length; i++)
                {
                    item[i].OnUpdate();
                }
            }
        }

        public override void Shutdown()
        {
            m_PanelDict?.Clear();
            m_PanelPathDict.Clear();
            m_OnDisplayPanelDic.Clear();
        }

        #endregion
    }
}