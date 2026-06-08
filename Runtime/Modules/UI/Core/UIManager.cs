using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using XFramework.Entity;
using XFramework.Resource;

namespace XFramework.UI
{
#if UNITY_EDITOR
    public readonly struct UIPanelDebugSnapshot
    {
        public UIPanelDebugSnapshot(
            string panelName,
            Type panelType,
            string path,
            int level,
            bool isCached,
            bool isOpened,
            bool isVisible,
            bool hasCloseCallback,
            GameObject gameObject,
            string hierarchyPath,
            bool isUIToolkitPanel)
        {
            PanelName = panelName;
            PanelType = panelType;
            Path = path;
            Level = level;
            IsCached = isCached;
            IsOpened = isOpened;
            IsVisible = isVisible;
            HasCloseCallback = hasCloseCallback;
            GameObject = gameObject;
            HierarchyPath = hierarchyPath;
            IsUIToolkitPanel = isUIToolkitPanel;
        }

        public string PanelName { get; }
        public Type PanelType { get; }
        public string Path { get; }
        public int Level { get; }
        public bool IsCached { get; }
        public bool IsOpened { get; }
        public bool IsVisible { get; }
        public bool HasCloseCallback { get; }
        public GameObject GameObject { get; }
        public string HierarchyPath { get; }
        public bool IsUIToolkitPanel { get; }
    }
#endif

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
    [ModuleLifecycle(ModuleLifecycle.RuntimePersistent)]
    public class UIManager : MonoGameModuleBase<UIManager>
    {
        private RectTransform canvasTransform;
        private RectTransform CanvasTransform
        {
            get
            {
                if (canvasTransform == null)
                {
                    var canvas = GameObject.FindFirstObjectByType<Canvas>();
                    if (canvas == null)
                    {
                        throw new XFrameworkException("[UI] Canvas is not exist in the scene, please add a Canvas to the scene or open a panel with a valid path to let UIManager create a Canvas for you");
                        // canvas = new GameObject("Canvas").AddComponent<RectTransform>();
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
        private readonly Dictionary<string, Action> m_PanelCloseCallbacks = new ();
        
        private GameObject m_TipsPrefab;


        public UIManager()
        {
            InitPathDic();
        }
        
        public void OpenPanel(string uiName, params object[] args)
        {
            OpenPanelInternal(uiName, null, args);
        }

        public void OpenPanel(string uiName, Action onClose, params object[] args)
        {
            OpenPanelInternal(uiName, onClose, args);
        }

        private void OpenPanelInternal(string uiName, Action onClose, params object[] args)
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

            if (onClose != null)
            {
                m_PanelCloseCallbacks[uiName] = onClose;
            }
            
            // if (m_OnDisplayPanelDic.ContainsKey(panel.Level - 1))
            // {
            //     m_OnDisplayPanelDic[panel.Level - 1].End().OnPause();
            // }
            
            // todo 应该放到OnBeforeOpen的时候
            panel.SetVisible(true);
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
                panel.SetVisible(false);
                
                m_OnDisplayPanelDic[panel.Level].Remove(panel);
                
                panel.OnAfterClose();

                InvokePanelCloseCallback(uiName);
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

        private void InvokePanelCloseCallback(string uiName)
        {
            if (!m_PanelCloseCallbacks.Remove(uiName, out Action callback))
            {
                return;
            }

            callback?.Invoke();
        }
        
        /// <summary>
        /// 检查面板是否正在显示
        /// </summary>
        public bool IsPanelOpened(string uiName)
        {
            foreach (var list in m_OnDisplayPanelDic.Values)
            {
                foreach (var panel in list)
                {
                    if (panel.PanelName == uiName) return true;
                }
            }
            return false;
        }

#if UNITY_EDITOR
        public List<UIPanelDebugSnapshot> GetDebugPanelSnapshots()
        {
            var openedPanels = new HashSet<PanelBase>();
            foreach (var list in m_OnDisplayPanelDic.Values)
            {
                foreach (var panel in list)
                {
                    if (panel != null)
                    {
                        openedPanels.Add(panel);
                    }
                }
            }

            var snapshots = new List<UIPanelDebugSnapshot>(m_PanelName2Info.Count);
            foreach (var pair in m_PanelName2Info)
            {
                string panelName = pair.Key;
                PanelInfoAttribute panelInfo = pair.Value;
                m_PanelName2Type.TryGetValue(panelName, out Type panelType);

                bool isCached = m_PanelDict.TryGetValue(panelName, out PanelBase panel);
                GameObject panelGameObject = panel != null ? panel.gameObject : null;
                snapshots.Add(new UIPanelDebugSnapshot(
                    panelName,
                    panelType,
                    panelInfo.path ?? string.Empty,
                    panelInfo.level,
                    isCached,
                    panel != null && openedPanels.Contains(panel),
                    panel != null && panel.IsVisible,
                    m_PanelCloseCallbacks.ContainsKey(panelName),
                    panelGameObject,
                    panelGameObject != null ? GetHierarchyPath(panelGameObject.transform) : string.Empty,
                    panelType != null && typeof(UIToolkitPanelBase).IsAssignableFrom(panelType)));
            }

            return snapshots;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
#endif

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
            Type panelType = m_PanelName2Type[uiName];
            GameObject panelGo = DefaultPanelLoader(uiName, panelInfo.path, panelType);
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
            EnsureRuntimeUIToolkitPanel(uiName, panelGo, panelType);
            if (panel == null)
            {
                panel = (PanelBase)panelGo.AddComponent(panelType);
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

        private void EnsureRuntimeUIToolkitPanel(string uiName, GameObject panelGo, Type panelType)
        {
            var panelInfo = m_PanelName2Info[uiName];
            if (!string.IsNullOrEmpty(panelInfo.path) || !typeof(UIToolkitPanelBase).IsAssignableFrom(panelType))
            {
                return;
            }

            if (!panelGo.TryGetComponent<UIDocument>(out var uiDocument))
            {
                uiDocument = panelGo.AddComponent<UIDocument>();
            }

            if (uiDocument.panelSettings ==null)
            {
                if (XApplication.Setting.defaultUIToolkitPanelSettings == null)
                {
                    throw new XFrameworkException($"[UI] The defaultUIToolkitPanelSettings is not set in XFrameworkSetting, please set it or assign a PanelSettings to {uiName} panel");
                }
                
                uiDocument.panelSettings = XApplication.Setting.defaultUIToolkitPanelSettings;
            }
        }

        /// <summary>
        /// 关闭某个层级的所有面板
        /// </summary>
        public void CloseLevelPanel(int level)
        {
            if (m_OnDisplayPanelDic.TryGetValue(level, out List<PanelBase> panels))
            {
                var panelNames = new List<string>(panels.Count);
                foreach (var item in panels)
                {
                    panelNames.Add(item.PanelName);
                }

                foreach (var panelName in panelNames)
                {
                    ClosePanel(panelName);
                }

                if (panels.Count == 0)
                {
                    m_OnDisplayPanelDic.Remove(level);
                }
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
                
                bool hasPanelPath = !string.IsNullOrEmpty(panelInfo.path);
                if (hasPanelPath && path2TypeDict.TryGetValue(panelInfo.path, out Type _type))
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
                if (hasPanelPath)
                {
                    path2TypeDict.Add(panelInfo.path, type);
                }
            }
        }

        internal void RegisterExistPanel(GameObject panelGo, Type panelType)
        {
            var uiName = m_PanelType2Name[panelType];
            if (!m_PanelDict.ContainsKey(uiName))
            {
                AddPanel(uiName, panelGo);
            }
            OpenPanel(uiName);
        }

        private GameObject DefaultPanelLoader(string uiName, string path, Type panelType)
        {
            if (string.IsNullOrEmpty(path) || IsUIToolkitAssetPath(path, panelType))
            {
                var runtimePanelGo = new GameObject(uiName, typeof(RectTransform));
                return runtimePanelGo;
            }

            var res = ResourceManager.Instance.Load<GameObject>(path);
            if(res == null)
            {
                throw new XFrameworkException($"[UI] The panel you want to load is not exist, path: {path}");
            }

            var panelGo = GameObject.Instantiate(res); 
            return panelGo;
        }

        private static bool IsUIToolkitAssetPath(string path, Type panelType)
        {
            if (!typeof(UIToolkitPanelBase).IsAssignableFrom(panelType))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".uxml", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".uss", StringComparison.OrdinalIgnoreCase);
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
            m_PanelCloseCallbacks.Clear();
            EntityManager.Instance.RemoveTemplate("ui-tip-entity");
        }

        #endregion
    }
}
