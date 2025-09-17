using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.Console
{
    /// <summary>
    /// UGUI版控制台
    /// </summary>
    public class UGUIConsole : IConsole
    {
        private GameObject consoleRoot;

        private Text m_TextContent = null;
        private ScrollRect m_ScrollViewContent = null;
        private InputField m_InputField = null;
        private Text m_TextPageCount = null;

        public UGUIConsole()
        {

        }

        private void CreateConsoleWindow()
        {
            var root = new GameObject("XConsole").transform;
            Object.DontDestroyOnLoad(root);
            consoleRoot = new GameObject("UGUIConsole");
            consoleRoot.transform.SetParent(root);
        
            Canvas canvas = consoleRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 6553;
            canvas.overrideSorting = true;
        
            CanvasScaler cs = consoleRoot.AddComponent<CanvasScaler>();
        
            // only mobile platform should use screen size.
            if (!Application.isEditor) 
            { 
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                cs.referenceResolution = new Vector2(960f, 640f);
                 cs.matchWidthOrHeight = 1.0f;
            }
        
        
            GraphicRaycaster gr = consoleRoot.AddComponent<GraphicRaycaster>();
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.All;

            InitEventSystem();
            InitBlackground(consoleRoot);
            InitScrollView(consoleRoot);
            InitScrollContent(m_ScrollViewContent.gameObject);
            InitInput(consoleRoot);
            InitCodeEditor(consoleRoot);
        }

        ///Init the event system if not exist.
        private void InitEventSystem()
        {
            UnityEngine.EventSystems.EventSystem es = GameObject.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject eventsystem = new GameObject("EventSystem");
                eventsystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventsystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        void InitBlackground(GameObject parent)
        {
            //background
            GameObject background = new GameObject("blackground");
            background.transform.parent = parent.transform;
            Image img_bgm = background.AddComponent<Image>();
            img_bgm.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            img_bgm.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, 0);
            img_bgm.rectTransform.anchorMin = new Vector2(0, 0.3f);
            img_bgm.rectTransform.anchorMax = new Vector2(1, 1);
            img_bgm.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img_bgm.color = new Color(0, 0, 0, 0.8f);
        }

        void InitScrollView(GameObject parent)
        {
            //content_scroll_view
            GameObject scrollView = new GameObject("scrollview");
            scrollView.transform.parent = parent.transform;
            m_ScrollViewContent = scrollView.AddComponent<ScrollRect>();
            m_ScrollViewContent.horizontal = false;
            m_ScrollViewContent.scrollSensitivity = 20;

            Image img_sr = scrollView.AddComponent<Image>();
            img_sr.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            img_sr.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, -30);
            img_sr.rectTransform.anchorMin = new Vector2(0, 0.3f);
            img_sr.rectTransform.anchorMax = new Vector2(1, 1);
            img_sr.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img_sr.color = new Color(0, 0, 0, 1f);
            img_sr.raycastTarget = true;
            Mask mask = scrollView.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        void InitScrollContent(GameObject scrollView)
        {
            //content_text
            GameObject scroll_content = new GameObject("content");
            scroll_content.transform.parent = scrollView.transform;
            m_TextContent = scroll_content.AddComponent<Text>();
            m_TextContent.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            m_TextContent.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, 0);
            m_TextContent.rectTransform.anchorMin = new Vector2(0, 0);
            m_TextContent.rectTransform.anchorMax = new Vector2(1, 1);
            m_TextContent.rectTransform.pivot = new Vector2(0f, 0f);
            m_TextContent.color = new Color(1, 1, 1, 1f);
            m_TextContent.font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
            m_TextContent.fontSize = 12;
            m_TextContent.alignment = TextAnchor.LowerLeft;

            m_TextContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollView.GetComponent<ScrollRect>().content = m_TextContent.rectTransform;
        }

        void InitInput(GameObject parent)
        {
            GameObject input_obj = new GameObject("input");
            input_obj.transform.parent = parent.transform;
            Image img_input = input_obj.AddComponent<Image>();
            img_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            img_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, -15, 30);
            img_input.rectTransform.anchorMin = new Vector2(0, 0.3f);
            img_input.rectTransform.anchorMax = new Vector2(1, 0.3f);
            img_input.rectTransform.pivot = new Vector2(0f, 0f);
            img_input.color = new Color(0, 0, 0, 1);
            img_input.raycastTarget = true;

            GameObject input_placeholder = new GameObject("placeHolder");
            input_placeholder.transform.parent = input_obj.transform;
            Text text_holder = input_placeholder.AddComponent<Text>();
            text_holder.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            text_holder.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 5, -10);
            text_holder.rectTransform.anchorMin = new Vector2(0, 0);
            text_holder.rectTransform.anchorMax = new Vector2(1, 1);
            text_holder.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text_holder.font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
            text_holder.color = Color.grey;
            text_holder.text = ">";

            GameObject input_page = new GameObject("pageamount");
            input_page.transform.parent = input_obj.transform;
            m_TextPageCount = input_page.AddComponent<Text>();
            m_TextPageCount.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            m_TextPageCount.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 5, -10);
            m_TextPageCount.rectTransform.anchorMin = new Vector2(0, 0);
            m_TextPageCount.rectTransform.anchorMax = new Vector2(1, 1);
            m_TextPageCount.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            m_TextPageCount.font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
            m_TextPageCount.color = Color.grey;
            m_TextPageCount.text = "%";
            m_TextPageCount.alignment = TextAnchor.MiddleRight;

            GameObject input_text_obj = new GameObject("inputText");
            input_text_obj.transform.parent = input_obj.transform;
            Text text_input = input_text_obj.AddComponent<Text>();
            text_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 10, -20);
            text_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 5, -10);
            text_input.rectTransform.anchorMin = new Vector2(0, 0);
            text_input.rectTransform.anchorMax = new Vector2(1, 1);
            text_input.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text_input.font = Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf") as Font;
            text_input.color = Color.white;
            text_input.supportRichText = false;

            m_InputField = input_obj.AddComponent<InputField>();
            m_InputField.textComponent = text_input;

            // Init event
            m_InputField.onEndEdit.AddListener(ProcessInput);
        }

        public void InitCodeEditor(GameObject parent)
        {

        }

        private void ProcessInput(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                XConsole.Execute(str);
            }
        }

        public void OnInit()
        {
            CreateConsoleWindow();

            XConsole.LogMessage(Message.System("strat XConsle"));
        }

        public void OnOpen()
        {
            consoleRoot.SetActive(true);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(m_InputField.gameObject);
            m_InputField.ActivateInputField();
        } 

        public void OnClose()
        {
            consoleRoot.SetActive(false);
        }

        public void OnLogMessage(Message message)
        {
            var text = m_TextContent.text + message.ToGUIString();
            m_TextContent.text = text;
        }

        public void OnExecuteCmd(string cmd, object value)
        {
            m_InputField.text = "";
            m_InputField.ActivateInputField();
        }

        public void OnClear()
        {
            m_TextContent.text = "";
        }

        public void OnCurrentCmdChanged(string cmd)
        {
            m_InputField.text = cmd;
        }
    }
}