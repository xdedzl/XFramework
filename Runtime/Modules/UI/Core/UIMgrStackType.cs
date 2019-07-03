﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 栈入式UI管理器
    /// </summary>
    public class UIMgrStackType : IUIManager
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
        /// 存储面板的栈
        /// </summary>
        private Stack<BasePanel> m_PanelStack;

        public UIMgrStackType()
        {
            InitPathDic();
        }

        /// <summary>
        /// 把某个页面入栈，  把某个页面显示在界面上
        /// </summary>
        public void PushPanel(string uiname)
        {
            if (m_PanelStack == null)
                m_PanelStack = new Stack<BasePanel>();

            BasePanel nextPanel = GetPanel(uiname); // 计划打开的页面
            BasePanel currentPanel = null;             // 最近一次关闭的界面
                                                       // 判断一下栈里面是否有页面
            if (m_PanelStack.Count > 0)
            {
                BasePanel topPanel = m_PanelStack.Peek(); // 获取栈顶页面

                // 如果即将打开的页面是栈顶页面，即关闭栈顶页面
                if (topPanel == nextPanel)
                {
                    PopPanel();
                    return;
                }
                // 当栈内有面板时，进行判断
                while (m_PanelStack.Count > 0)
                {
                    if (topPanel.Level < nextPanel.Level)
                    {
                        break;
                    }
                    // 如果栈顶页面的层级不小于要打开的页面层级，关闭它并保存
                    currentPanel = PopPanel();
                    if (m_PanelStack.Count > 0)
                    {
                        topPanel = m_PanelStack.Peek();
                    }
                }
            }
            // 如果最后关闭的界面和要打开的是同一个，就不打开了
            if (currentPanel != nextPanel)
            {
                nextPanel.OnOpen();
                m_PanelStack.Push(nextPanel); // 将打开的面板入栈
            }
        }

        /// <summary>
        /// 出栈 ，把页面从界面上移除
        /// </summary>
        public BasePanel PopPanel()
        {
            if (m_PanelStack == null)
                m_PanelStack = new Stack<BasePanel>();

            if (m_PanelStack.Count <= 0) return null;

            BasePanel topPanel = m_PanelStack.Pop(); // 获取并移除栈顶面板
            topPanel.OnClose();                     // 关闭面板
            return topPanel;
        }

        /// <summary>
        /// 根据面板类型 得到实例化的面板
        /// </summary>
        /// <returns></returns>
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
                Debug.Log(path);
                GameObject instPanel = GameObject.Instantiate(Resources.Load(path)) as GameObject;

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
        /// 返回特定类型的panel
        /// </summary>
        public T GetPanel<T>(string uiname) where T : BasePanel
        {
            return (T)GetPanel(uiname);
        }

        /// <summary>
        /// 判断栈顶界面是否为某一个类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool TopPanelEqual(string type)
        {
            return TopPanel == GetPanel(type);
        }

        /// <summary>
        /// 栈顶界面
        /// </summary>
        public BasePanel TopPanel
        {
            get
            {
                if (m_PanelStack == null || m_PanelStack.Count < 1)
                    return null;
                else
                    return m_PanelStack?.Peek();
            }
        }

        /// <summary>
        /// 返回一个特定类型的栈顶界面，如果不匹配返回Null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T TopPanell<T>() where T : BasePanel
        {
            return (T)TopPanel;
        }

        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public void CloseAll()
        {
            while (m_PanelStack.Count > 0)
            {
                m_PanelStack.Pop().OnClose();
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
            if(textAsset == null)
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

        /// <summary>
        /// 清除所有UI面板
        /// </summary>
        public void Clear()
        {
            while (m_PanelStack?.Count > 0)
            {
                PopPanel();
            }
            m_PanelDict?.Clear();
            m_PanelStack?.Clear();
        }

        public void OpenPanel(string uiname)
        {
            PushPanel(uiname);
        }

        public void ClosePanel(string uiname)
        {

        }

        public void CloseTopPanel()
        {
            PopPanel();
        }

        public int Priority { get { return 4000; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (var item in m_PanelStack)
            {
                item.OnUpdate();
            }
        }

        public void Shutdown()
        {
            
        }
    }
}