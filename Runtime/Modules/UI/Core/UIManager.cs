using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using XFramework.Entity;
using XFramework.Resource;

namespace XFramework.UI
{
    /// <summary>
    /// 主界面（所有主界面会放到一个栈中（用于back等操作），主界面不能同时存在多个）
    /// </summary>
    public class MainPanelAttribute : Attribute { }
    
    /// <summary>
    /// 定义面板的名称和路径
    /// </summary>
    public class PanelInfoAttribute : Attribute
    {
        public string name;
        public string path;
        public int level;
        
        public PanelInfoAttribute(string name, string path, int level = 0)
        {
            this.name = name;
            this.path = path;
            this.level = level;
        }
    }
    
    [DependenceModule(typeof(ResourceManager))]
    public class UIManager : PersistentMonoGameModuleBase<UIManager>
    {
        private RectTransform canvasTransform;
        private RectTransform CanvasTransform
        {
            get
            {
                if (canvasTransform == null)
                {
                    var canvas = GameObject.Find("Canvas");
                    if (canvas == null)
                    {
                        canvas = new GameObject("Canvas").AddComponent<RectTransform>().gameObject;
                    }
                    canvasTransform = canvas.GetComponent<RectTransform>();
                }
                return canvasTransform;
            }
        }
        
        private readonly Dictionary<string, PanelInfoAttribute> m_PanelName2Info = new ();
        private readonly Dictionary<string, Type> m_PanelName2Type = new ();
        private readonly Dictionary<Type, string> m_PanelType2Name = new ();
        
        private readonly Dictionary<string, PanelBase> m_PanelDict = new ();
        private readonly Dictionary<int, List<PanelBase>> m_OnDisplayPanelDic = new ();
        
        private GameObject m_TipsPrefab;


        public UIManager()
        {
            InitPathDic();
            GameObject.DontDestroyOnLoad(CanvasTransform);
        }
        
        public void OpenPanel(string uiName, params object[] args)
        {
            PanelBase panel = GetPanel(uiName);
            if (null ==panel)
                return;

            if (m_OnDisplayPanelDic.TryGetValue(panel.Level, out var value))
            {
                if (value.Contains(panel))
                {
                    return;
                }
            }
            else
            {
                m_OnDisplayPanelDic.Add(panel.Level, new List<PanelBase>());
            }
            m_OnDisplayPanelDic[panel.Level].Add(panel);
            
            // if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1))
            // {
            //     m_OnDisplayPanelDic[panel.Level - 1].End().OnPause();
            // }
            
            // todo 应该放到OnBeforeOpen的时候
            panel.gameObject.SetActive(true);
            panel.transform.SetAsLastSibling();
            
            panel.OnOpen(args);
            panel.OpenSubPanels();
        }
        
        public void ClosePanel(string uiName)
        {
            PanelBase panel = GetPanel(uiName);
            if (m_OnDisplayPanelDic.ContainsKey(panel.Level) && m_OnDisplayPanelDic[panel.Level].Contains(panel))
            {
                panel.OnClose();
                panel.CloseSubPanels();
                
                // todo 应该放到OnClose的时候
                panel.gameObject.SetActive(false);
                
                m_OnDisplayPanelDic[panel.Level].Remove(panel);
                
                panel.OnAfterClose();
            }

            // int index = panel.Level + 1;
            // while (m_OnDisplayPanelDic.ContainsKey(index))
            // {
            //     var temp = m_OnDisplayPanelDic[index];
            //     if (temp.Count > 0)
            //     {
            //         temp.End().OnClose();
            //         temp.End().CloseSubPanels();
            //
            //         temp.RemoveAt(temp.Count - 1);
            //     }
            //     else
            //     {
            //         break;
            //     }
            // }
            // if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1))
            // {
            //     m_OnDisplayPanelDic[panel.Level - 1].End().OnResume();
            // }
        }
        
        private PanelBase GetPanel(string uiName)
        {
            if (m_PanelDict.TryGetValue(uiName, out PanelBase _panel))
            {
                if (_panel == null)
                    throw new XFrameworkException("[UI] The panel you want has been unloaded");
                return _panel;
            }

            m_PanelName2Info.TryGetValue(uiName, out var panelInfo);
            if (panelInfo == null)
            {
                throw new XFrameworkException($"[UI] The panel info you want is not exist, panel name: {uiName}");
            }
            GameObject panelGo = DefaultPanelLoader(panelInfo.path);
            var panel = AddPanel(uiName, panelGo);
            return panel;
        }
        
        public T GetPanel<T>() where T : PanelBase
        {
            string uiName = m_PanelType2Name[typeof(T)];
            return GetPanel(uiName) as T;
        }

        private PanelBase AddPanel(string uiName, GameObject panelGo)
        {
            if (m_PanelDict.ContainsKey(uiName))
            {
                throw new XFrameworkException("[UI] The panel you want add is already exist");
            }
            
            var panel = panelGo.GetComponent<PanelBase>();
            var panelType = m_PanelName2Type[uiName];
            if (panel == null)
            {
                panelGo.AddComponent(panelType);
            }
            else
            {
                if (panel.GetType() != panelType)
                {
                    throw new XFrameworkException($"[UI] The panel type in {m_PanelName2Info[uiName].path} is wrong");
                }
            }

            panel.Init();
            m_PanelDict.Add(uiName, panel);

            Transform uiGroup = CanvasTransform.Find("Level" + panel.Level);
            if (uiGroup == null)
            {
                var rect = (new GameObject("Level" + panel.Level)).AddComponent<RectTransform>();

                int siblingIndex = CanvasTransform.childCount;
                for (int i = 0, length = CanvasTransform.childCount; i < length; i++)
                {
                    string levelName = CanvasTransform.GetChild(i).name;
                    if (int.TryParse(levelName[^1].ToString(), out int level))
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
            return panel;
        }

        /// <summary>
        /// 关闭某个层级的所有面板
        /// </summary>
        public void CloseLevelPanel(int level)
        {
            if (m_OnDisplayPanelDic.TryGetValue(level, out List<PanelBase> panels))
            {
                foreach (var item in panels)
                {
                    ClosePanel(item.PanelName);
                }
                m_OnDisplayPanelDic.Remove(level);
            }
        }

        /// <summary>
        /// 初始化面板路径
        /// </summary>
        private void InitPathDic()
        {
            var path2TypeDict = new Dictionary<string, Type>();
            foreach (var type in Utility.Reflection.GetSonTypes<PanelBase>())
            {
                var panelInfo = type.GetCustomAttribute<PanelInfoAttribute>();

                if (panelInfo == null)
                {
                    throw new XFrameworkException($"[{type.Name}] PanelInfoAttribute is missing");
                }
                
                if (path2TypeDict.TryGetValue(panelInfo.path, out Type _type))
                {
                    throw new XFrameworkException($"[UI] The panel path is repeat, type1:{_type}, type2: {type}, path: {panelInfo.path}");
                }
                
                if (m_PanelName2Type.TryGetValue(panelInfo.name, out Type __type))
                {
                    throw new XFrameworkException($"[UI] The panel name is repeat, type1:{__type}, type2: {type}, path: {panelInfo.name}");
                }
                
                m_PanelName2Info.Add(panelInfo.name, panelInfo);
                m_PanelName2Type.Add(panelInfo.name, type);
                m_PanelType2Name.Add(type, panelInfo.name);
                path2TypeDict.Add(panelInfo.path, type);
            }
        }

        internal void RegisterExistPanel(GameObject panelGo, Type panelType)
        {
            var uiName = m_PanelType2Name[panelType];
            AddPanel(uiName, panelGo);
            OpenPanel(uiName);
        }

        private GameObject DefaultPanelLoader(string path)
        {
            var res = ResourceManager.Instance.Load<GameObject>(path);
            if(res == null)
            {
                throw new XFrameworkException($"[UI] The panel you want to load is not exist, path: {path}");
            }

            var panelGo = GameObject.Instantiate(res); 
            return panelGo;
        }

        #region Tips

        public void ShowTips(string content, Vector3 position, Color color)
        {
            var point = Camera.main.WorldToScreenPoint(position).XY();
            ShowTips(content, point, color);
        }

        public void ShowTips(string content, Vector2 position, Color color)
        {
            InitTipsTemplate();
            var entity = EntityManager.Instance.Allocate<TipEntity>("ui-tip-entity");
            entity.transform.SetParent(canvasTransform);
            entity.position = position;
            entity.text = content;
            entity.color = color;
        }

        private void InitTipsTemplate()
        {
            if(!EntityManager.Instance.ContainsTemplate("ui-tip-entity"))
            {
                var root = new GameObject("ui-tip-template");
                root.transform.SetParent(canvasTransform);
                var tmp = root.AddComponent<TextMeshProUGUI>();
                var rectTransform = root.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(200, 100);

                tmp.font = XApplication.Setting.font;
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
            m_PanelName2Info.Clear();
            m_OnDisplayPanelDic.Clear();
            EntityManager.Instance.RemoveTemplate("ui-tip-entity");
        }

        #endregion
    }
}