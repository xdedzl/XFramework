using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using XFramework.UI;

namespace Quick.Code
{
    public class GenerateCodeWindow : EditorWindow
    {
        [MenuItem("XFramework/UI/QuickGenCode")]
        public static void OpenWindow()
        {
            if (codeWindow == null)
                codeWindow = GetWindow(typeof(GenerateCodeWindow)) as GenerateCodeWindow;

            Texture2D icon = (Texture2D)EditorGUIUtility.Load(iconPath);
            codeWindow.titleContent = new GUIContent("QuickUICode", icon);
            codeWindow.Show();

        }

        private static string iconPath = "Assets/Editor/QuickUICode/icon.png";
        private static GenerateCodeWindow codeWindow = null;
        private SerializedObject serializedObj;

        //选择的根游戏体
        private GameObject root;
        //视图宽度一半
        private float halfViewWidth;
        //视图高度一半
        private float halfViewHeight;

        private Vector2 scrollWidgetPos;

        private List<BaseGUI> baseGUIList;
        private List<bool> hasCacheList;
        private List<bool> isLamdaList;
        private string[] GUIName;
        private List<string> funNames;

        private string codeText;

        void OnEnable()
        {
            serializedObj = new SerializedObject(this);
            baseGUIList = new List<BaseGUI>();
            hasCacheList = new List<bool>();
            isLamdaList = new List<bool>();
        }

        void OnGUI()
        {
            serializedObj.Update();

            if (codeWindow == null)
            {
                codeWindow = GetWindow<GenerateCodeWindow>();
            }
            halfViewWidth = codeWindow.position.width / 2f;
            halfViewHeight = codeWindow.position.height / 2f;

            using (new EditorGUILayout.HorizontalScope())
            {
                //左半部分 
                using (EditorGUILayout.VerticalScope vScope = new EditorGUILayout.VerticalScope(GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.5f)))
                {
                    GUI.backgroundColor = Color.white;
                    Rect rect = vScope.rect;
                    rect.height = codeWindow.position.height;
                    GUI.Box(rect, "");

                    DrawSelectUI();
                    if(root == null)
                    {
                        return;
                    }
                    DrawFindWidget();
                    DrawBaseGUIList();
                }
                //右半部分
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.5f)))
                {
                    DrawCodeGenTitle();
                }
            }

            serializedObj.ApplyModifiedProperties();
        }

        #region 左边区域

        /// <summary>
        /// 绘制 选择要分析的UI
        /// </summary>
        private void DrawSelectUI()
        {
            EditorGUILayout.Space();
            using (EditorGUILayout.HorizontalScope hScope = new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = Color.yellow;
                Rect rect = hScope.rect;
                rect.height = EditorGUIUtility.singleLineHeight;
                GUI.Box(rect, "");

                EditorGUILayout.LabelField("选择UI面板:", GUILayout.Width(halfViewWidth / 4f));
                GameObject lastRoot = root;
                root = EditorGUILayout.ObjectField(root, typeof(GameObject), true) as GameObject;
            }
        }

        /// <summary>
        /// 绘制 查找UI控件
        /// </summary>
        private void DrawFindWidget()
        {
            EditorGUILayout.Space();
            using (EditorGUILayout.HorizontalScope hScope = new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = Color.white;
                Rect rect = hScope.rect;
                rect.height = EditorGUIUtility.singleLineHeight;
                GUI.Box(rect, "");

                if (GUILayout.Button("刷新控件", GUILayout.Width(halfViewWidth / 2f)))
                {
                    if (root == null)
                    {
                        Debug.LogWarning("请先选择一个UI物体!");
                        return;
                    }
                    RefreshUIList();
                }

                if (GUILayout.Button("清除控件", GUILayout.Width(halfViewWidth / 4)))
                {
                }
                if (GUILayout.Button("清除其他", GUILayout.Width(halfViewWidth / 4)))
                {
                }
            }
        }

        /// <summary>
        /// 绘制 控件列表
        /// </summary>
        private void DrawBaseGUIList()
        {
            EditorGUILayout.Space();

            scrollWidgetPos = EditorGUILayout.BeginScrollView(scrollWidgetPos);

            DrawUIList();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制UI控件列表
        /// </summary>
        private void DrawUIList()
        {
            using (var hScope = new EditorGUILayout.VerticalScope(GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                GUI.backgroundColor = Color.white;
                Rect rect = hScope.rect;
                GUI.Box(rect, "");

                for (int i = 0; i < baseGUIList.Count; i++)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(baseGUIList[i].name, GUILayout.Width(halfViewWidth / 5));

                    EditorGUILayout.LabelField("是否缓存", GUILayout.Width(halfViewWidth / 5));
                    hasCacheList[i] = EditorGUILayout.Toggle(hasCacheList[i], GUILayout.Width(halfViewWidth / 20));

                    EditorGUILayout.LabelField("Lambda", GUILayout.Width(halfViewWidth / 5));
                    isLamdaList[i] = EditorGUILayout.Toggle(isLamdaList[i], GUILayout.Width(halfViewWidth / 20));
                    GUILayout.EndHorizontal();
                }
            }
        }

        /// <summary>
        /// 更新UI控件列表
        /// </summary>
        private void RefreshUIList()
        {
            if (baseGUIList == null && baseGUIList.Count <= 0)
            {
                baseGUIList = new List<BaseGUI>(root.GetComponentsInChildren<BaseGUI>());
                hasCacheList = new List<bool>(new bool[baseGUIList.Count]);
                for (int i = 0, length = hasCacheList.Count; i < length; i++)
                {
                    hasCacheList[i] = true;
                }
                isLamdaList = new List<bool>(new bool[baseGUIList.Count]);
            }
            else
            {
                BaseGUI[] tempArray = root.GetComponentsInChildren<BaseGUI>();
                for (int i = 0; i < tempArray.Length; i++)
                {
                    if (!baseGUIList.Contains(tempArray[i]))
                    {
                        baseGUIList.Add(tempArray[i]);
                        hasCacheList.Add(true);
                        isLamdaList.Add(true);
                    }
                }
            }
        }

        #endregion

        #region 右边区域

        private Vector2 codeScrollPos;
        private void DrawCodeGenTitle()
        {
            EditorGUILayout.Space();
            using (var vScope = new EditorGUILayout.VerticalScope(GUILayout.Height(EditorGUIUtility.singleLineHeight * 100)))
            {
                GUI.backgroundColor = new Color32(0,0,0,30);
                Rect rect = vScope.rect;
                GUI.Box(rect, "");

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("代码预览", GUILayout.Width(halfViewWidth / 4));
                if (GUILayout.Button("刷新", GUILayout.Width(halfViewWidth / 4)))
                {
                    InitCodeText();
                }

                if (GUILayout.Button("生成代码", GUILayout.Width(halfViewWidth / 4)))
                {
                    InitCodeText();
                    // 创建.cs文件
                }
                GUILayout.EndHorizontal();

                float width = halfViewWidth - 15;
                float height = halfViewHeight * 2 - 55;
                codeScrollPos = EditorGUILayout.BeginScrollView(codeScrollPos, GUILayout.Width(width), GUILayout.Height(height));
                GUI.backgroundColor = new Color32(0, 146, 255, 100);
                GUILayout.TextArea(codeText);

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space();
            }
        }

        /// <summary>
        /// 初始化代码文本
        /// </summary>
        private void InitCodeText()
        {
            GUIName = new string[baseGUIList.Count];

            codeText = CodeConfig.nameSpcae;
            codeText += string.Format(CodeConfig.classStart, root.name);

            InitGUICache();
            InitRegText();
            InitEventFun();

            codeText += CodeConfig.classEnd;
        }

        /// <summary>
        /// 写入GUI缓存
        /// </summary>
        private void InitGUICache()
        {
            // UI组件缓存
            for (int i = 0; i < baseGUIList.Count; i++)
            {
                if (hasCacheList[i])
                {
                    Type type = baseGUIList[i].GetType();

                    char[] s = baseGUIList[i].name.ToCharArray();
                    char c = s[0];
                    if ('A' <= c && c <= 'Z')
                        c = (char)(c + 32);
                    s[0] = c;

                    GUIName[i] = new string(s);
                    codeText += string.Format("\t{0} {1};\n", type.Name, GUIName[i]);
                }
            }
        }

        /// <summary>
        /// 写入Reg函数
        /// </summary>
        private void InitRegText()
        {
            codeText += string.Format(CodeConfig.FunOverrideStart, "Reg");

            for (int i = 0; i < baseGUIList.Count; i++)
            {
                if (hasCacheList[i])
                {
                    codeText += string.Format("\t\t{0} = ", GUIName[i]);
                    codeText += string.Format("this[\"{0}\"] as {1};\n", baseGUIList[i].name, baseGUIList[i].GetType().Name);
                }
            }

            codeText += "\n";

            InitAddEvent();

            codeText += CodeConfig.FunEnd; 
        }

        /// <summary>
        /// 写入UI组件的事件注册
        /// </summary>
        private void InitAddEvent()
        {
            funNames = new List<string>();
            for (int i = 0; i < baseGUIList.Count; i++)
            {
                string caller = hasCacheList[i] ? GUIName[i] : string.Format(CodeConfig.FindBaseGUI, baseGUIList[i].name, baseGUIList[i].GetType().Name);
                string funName = string.Format("On{0}Click", baseGUIList[i].name);
                string lambda = string.Format(CodeConfig.Lamda);
                string argsStr = isLamdaList[i] ? lambda : funName;

                if (!isLamdaList[i])
                    funNames.Add(funName);

                switch (baseGUIList[i].GetType().Name)
                {
                    case "GUButton":
                    case "GUDropdown":
                        codeText += CodeConfig.AddCallFun(caller, "AddListener", argsStr);
                        break;
                }
                    
            }
        }

        /// <summary>
        /// 写入被注册的方法
        /// </summary>
        private void InitEventFun()
        {
            foreach (var item in funNames)
            {
                codeText += CodeConfig.AddEmptyFun(item);
            }
        }

        #endregion
    }
}