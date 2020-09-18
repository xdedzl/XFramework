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

        Text text_content = null;
        ScrollRect sr_content = null;
        InputField input = null;
        Text text_pageCount = null;

        private LinkedList<string> cmdCache = new LinkedList<string>();
        private LinkedListNode<string> currentCmd;

        public UGUIConsole()
        {

        }

        private void CreateConsoleWindow()
        {
            //canvas
            GameObject windows = new GameObject("UGUIConsole");
            windows.transform.parent = new GameObject("XConsole").transform;
            consoleRoot = windows;

            Canvas canvas = windows.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 88888;

            CanvasScaler cs = windows.AddComponent<CanvasScaler>();

            //only mobile platform should use screen size.
            if (!Application.isEditor)
            {
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                cs.referenceResolution = new Vector2(960f, 640f);
                cs.matchWidthOrHeight = 1.0f;
            }


            GraphicRaycaster gr = windows.AddComponent<GraphicRaycaster>();
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.All;
            //gr.

            InitEventSystem();
            InitBlackground(windows);
            InitScrollView(windows);
            InitScrollContent(sr_content.gameObject);
            InitInput(windows);
        }

        ///Init the event system if not exist.
        private void InitEventSystem()
        {
            UnityEngine.EventSystems.EventSystem es = GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject eventsystem = new GameObject("EventSystem");
                eventsystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                UnityEngine.EventSystems.StandaloneInputModule sim = eventsystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                sim.forceModuleActive = true;
            }
        }

        void InitBlackground(GameObject windows)
        {
            //background
            GameObject background = new GameObject("blackground");
            background.transform.parent = windows.transform;
            Image img_bgm = background.AddComponent<Image>();
            img_bgm.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            img_bgm.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, 0);
            img_bgm.rectTransform.anchorMin = new Vector2(0, 0.3f);
            img_bgm.rectTransform.anchorMax = new Vector2(1, 1);
            img_bgm.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img_bgm.color = new Color(0, 0, 0, 0.8f);
        }

        void InitScrollView(GameObject windows)
        {
            //content_scroll_view
            GameObject scrollView = new GameObject("scrollview");
            scrollView.transform.parent = windows.transform;
            sr_content = scrollView.AddComponent<ScrollRect>();
            sr_content.horizontal = false;
            sr_content.scrollSensitivity = 20;

            Image img_sr = scrollView.AddComponent<Image>();
            img_sr.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            img_sr.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, -30);
            img_sr.rectTransform.anchorMin = new Vector2(0, 0.3f);
            img_sr.rectTransform.anchorMax = new Vector2(1, 1);
            img_sr.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img_sr.color = new Color(0, 0, 0, 1f);
            Mask mask = scrollView.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        void InitScrollContent(GameObject scrollView)
        {
            //content_text
            GameObject scroll_content = new GameObject("content");
            scroll_content.transform.parent = scrollView.transform;
            text_content = scroll_content.AddComponent<Text>();
            text_content.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            text_content.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, 0);
            text_content.rectTransform.anchorMin = new Vector2(0, 0);
            text_content.rectTransform.anchorMax = new Vector2(1, 1);
            text_content.rectTransform.pivot = new Vector2(0f, 0f);
            text_content.color = new Color(1, 1, 1, 1f);
            text_content.font = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
            text_content.fontSize = 12;
            text_content.alignment = TextAnchor.LowerLeft;

            text_content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollView.GetComponent<ScrollRect>().content = text_content.rectTransform;
        }

        void InitInput(GameObject windows)
        {
            GameObject input_obj = new GameObject("input");
            input_obj.transform.parent = windows.transform;
            Image img_input = input_obj.AddComponent<Image>();
            img_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            img_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, -15, 30);
            img_input.rectTransform.anchorMin = new Vector2(0, 0.3f);
            img_input.rectTransform.anchorMax = new Vector2(1, 0.3f);
            img_input.rectTransform.pivot = new Vector2(0f, 0f);
            img_input.color = new Color(0, 0, 0, 1);

            GameObject input_placeholder = new GameObject("placeHolder");
            input_placeholder.transform.parent = input_obj.transform;
            Text text_holder = input_placeholder.AddComponent<Text>();
            text_holder.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            text_holder.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 5, -10);
            text_holder.rectTransform.anchorMin = new Vector2(0, 0);
            text_holder.rectTransform.anchorMax = new Vector2(1, 1);
            text_holder.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text_holder.font = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
            text_holder.color = Color.grey;
            text_holder.text = ">";

            GameObject input_page = new GameObject("pageamount");
            input_page.transform.parent = input_obj.transform;
            text_pageCount = input_page.AddComponent<Text>();
            text_pageCount.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 0);
            text_pageCount.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 5, -10);
            text_pageCount.rectTransform.anchorMin = new Vector2(0, 0);
            text_pageCount.rectTransform.anchorMax = new Vector2(1, 1);
            text_pageCount.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text_pageCount.font = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
            text_pageCount.color = Color.grey;
            text_pageCount.text = "%";
            text_pageCount.alignment = TextAnchor.MiddleRight;

            GameObject input_text_obj = new GameObject("inputText");
            input_text_obj.transform.parent = input_obj.transform;
            Text text_input = input_text_obj.AddComponent<Text>();
            text_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 10, -20);
            text_input.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 5, -10);
            text_input.rectTransform.anchorMin = new Vector2(0, 0);
            text_input.rectTransform.anchorMax = new Vector2(1, 1);
            text_input.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text_input.font = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
            text_input.color = Color.white;
            text_input.supportRichText = false;

            input = input_obj.AddComponent<InputField>();
            input.textComponent = text_input;

            ///Init event
            input.onEndEdit.AddListener(ProcessInput);

        }

        private void ProcessInput(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                XConsole.Excute(str);
                cmdCache.AddLast(str);
                currentCmd = null;
            }
        }

        public void OnInit()
        {
            CreateConsoleWindow();

            XConsole.Instance.LogMessage(Message.System("strat XConsle"));
        }

        public void OnOpen()
        {
            consoleRoot.SetActive(true);
            MonoEvent.Instance.UPDATE += OnUpdate;
        }

        public void OnClose()
        {
            consoleRoot.SetActive(false);
            MonoEvent.Instance.UPDATE -= OnUpdate;
        }

        public void OnLogMessage(Message message)
        {
            var text = text_content.text + message.ToGUIString();
            text_content.text = text;
        }

        public void OnExcuteCmd(string cmd, object value)
        {
            XConsole.Instance.LogMessage(Message.Input(cmd));
            if (value != null)
            {
                XConsole.Log(value);
            }

            input.text = "";

            input.ActivateInputField();
        }

        public void OnClear()
        {
            text_content.text = "";
            cmdCache.Clear();
        }

        private void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (currentCmd == null)
                {
                    currentCmd = cmdCache.Last;
                }
                else if (currentCmd.Previous != null)
                {
                    currentCmd = currentCmd.Previous;
                }
                else
                {
                    return;
                }
                input.text = currentCmd?.Value;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (currentCmd != null && currentCmd.Next != null)
                {
                    currentCmd = currentCmd.Next;
                }
                else
                {
                    return;
                }
                input.text = currentCmd?.Value;
            }
        }
    }
}