using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Video;
using XFramework.Entity;
using XFramework.Resource;
using static XFramework.Utility;

namespace XFramework.UI
{
    class MainPanelAttribute : Attribute { }



    /// <summary>
    /// 一个使用字典管理的UI管理器
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


        private GameObject m_TipsPrefab;


        public UIManager()
        {
            InitPathDic();
            GameObject.DontDestroyOnLoad(CanvasTransform);
        }

        /// <summary>
        /// 打开面板
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
        private PanelBase GetPanel(string uiname)
        {
            if (m_PanelDict.TryGetValue(uiname, out PanelBase _panel))
            {
                if (_panel == null)
                    throw new XFrameworkException("[UI] The panel you want has been unloaded");
                return _panel;
            }
            else
            {
                // 根据prefab去实例化面板
                m_PanelPathDict.TryGetValue(uiname, out string path);
                PanelBase panel = DefaultPanelLoader(path);
                AddPanel(uiname, panel);
                return panel;
            }
        }

        private void AddPanel(string uiname, PanelBase panel)
        {
            if (m_PanelDict.ContainsKey(uiname))
            {
                throw new XFrameworkException("[UI] The panel you want add is already exist");
            }

            panel.Init(uiname);
            m_PanelDict.Add(uiname, panel);

            Transform uiGroup = CanvasTransform.Find("Level" + panel.Level);
            if (uiGroup == null)
            {
                RectTransform rect;
                rect = (new GameObject("Level" + panel.Level)).AddComponent<RectTransform>();

                int siblingIndex = CanvasTransform.childCount;
                for (int i = 0, length = CanvasTransform.childCount; i < length; i++)
                {
                    string levelName = CanvasTransform.GetChild(i).name;
                    if (int.TryParse(levelName[levelName.Length - 1].ToString(), out int level))
                    {
                        if (panel.Level < level)
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
            panel.transform.SetParent(uiGroup, false);
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
        /// 关闭某一层级的所有面板
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
        /// 初始化面板预制体路径字典
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

        internal void RegisterExistPanel(string name, PanelBase panel, bool openPanel=true)
        {
            AddPanel(name, panel);
            if (openPanel)
            {
                OpenPanel(name);
            }
        }

        private PanelBase DefaultPanelLoader(string path)
        {
            var res = Resources.Load<GameObject>(path);
            if(res == null)
            {
                throw new XFrameworkException($"[UI] The panel you want to load is not exist, path: {path}");
            }
            var panel = GameObject.Instantiate(res).GetComponent<PanelBase>();
            if (panel == null)
            {
                throw new XFrameworkException($"[UI] The panel you want to load is has no PanelBase {path}");
            }
            return panel;
        }

        #region Tips

        public void ShowTips(string content, Vector3 position, Color color)
        {
            var point = Camera.main.WorldToScreenPoint(position).XY();
            ShowTips(content, point, color);
        }

        public void ShowTips(string content, Vector2 position, Color color)
        {
            InitTipsTemplete();
            var entity = EntityManager.Instance.Allocate<TipEntity>("ui-tip-entity");
            entity.transform.SetParent(canvasTransform);
            entity.position = position;
            entity.text = content;
            entity.color = color;
        }

        private void InitTipsTemplete()
        {
            if(!EntityManager.Instance.ContainsTemplete("ui-tip-entity"))
            {
                var root = new GameObject("ui-tip-templete");
                root.transform.SetParent(canvasTransform);
                var tmp = root.AddComponent<TextMeshProUGUI>();
                var rectTransform = root.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(200, 100);

                tmp.font = GameBase.Setting.font;
                tmp.alignment = TextAlignmentOptions.Center;

                tmp.transform.position = new Vector3(0, 999999, 0);
                EntityManager.Instance.AddTemplate<TipEntity>("ui-tip-entity", root, "");
            }
        }

        #endregion

        #region 接口实现

        public override int Priority => 200;

        public override void Update()
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
            EntityManager.Instance.RemoveTemplate("ui-tip-entity");

        }

        #endregion
    }
}