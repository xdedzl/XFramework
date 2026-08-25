using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace XFramework.Command
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
        private Dropdown m_Dropdown = null;
        private Text m_ExceptionDetailsButtonText;
        private readonly List<DisplayEntry> m_DisplayEntries = new List<DisplayEntry>();
        private readonly HashSet<long> m_DisplayedCommandIds = new HashSet<long>();
        private long m_VisibleAfterRecordId;
        private bool m_HasOpened;
        private bool m_IsSubscribed;
        private bool m_ShowExceptionDetails;

        public UGUIConsole()
        {

        }

        private void CreateConsoleWindow()
        {
            var root = new GameObject("XCommand").transform;
            Object.DontDestroyOnLoad(root);
            consoleRoot = new GameObject("UGUIConsole");
            consoleRoot.transform.SetParent(root);
        
            Canvas canvas = consoleRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 6553;
            canvas.overrideSorting = true;

            CanvasScaler cs = consoleRoot.AddComponent<CanvasScaler>();

            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cs.referenceResolution = new Vector2(960f, 640f);
            cs.matchWidthOrHeight = 1.0f;


            GraphicRaycaster gr = consoleRoot.AddComponent<GraphicRaycaster>();
            gr.blockingObjects = GraphicRaycaster.BlockingObjects.All;

            InitEventSystem();
            InitBlackGround(consoleRoot);
            InitScrollView(consoleRoot);
            InitScrollContent(m_ScrollViewContent.gameObject);
            InitInput(consoleRoot);
            
            RefreshDropDown();
        }

        ///Init the event system if not exist.
        private static void InitEventSystem()
        {
            UnityEngine.EventSystems.EventSystem es = GameObject.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject eventsystem = new GameObject("EventSystem");
                eventsystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventsystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private static void InitBlackGround(GameObject parent)
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

        private void InitScrollView(GameObject parent)
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

        private void InitScrollContent(GameObject scrollView)
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

        private void InitInput(GameObject parent)
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
            text_input.rectTransform.offsetMax = new Vector2(-200f, text_input.rectTransform.offsetMax.y);

            m_InputField = input_obj.AddComponent<InputField>();
            m_InputField.textComponent = text_input;

            // Init event
            m_InputField.onEndEdit.AddListener(ProcessInput);

            CreateInputButton(input_obj, "历史", 130f, 70f, LoadHistory);
            m_ExceptionDetailsButtonText = CreateInputButton(input_obj, "异常:关", 60f, 70f, ToggleExceptionDetails);
            InitDropDown(input_obj);
        }

        private static Text CreateInputButton(GameObject parent, string label, float rightOffset, float width, System.Action callback)
        {
            GameObject buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent.transform, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => callback.Invoke());

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 0.5f);
            rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rectTransform.pivot = new Vector2(1f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(-rightOffset, 0f);
            rectTransform.sizeDelta = new Vector2(width, 30f);

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            Text text = textObject.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.green;
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return text;
        }

        public void InitDropDown(GameObject parent)
        {
            // 创建Dropdown主体
            GameObject dropdownGO = new GameObject("ConsoleDropdown");
            dropdownGO.transform.SetParent(parent.transform, false);

            Dropdown dropdown = dropdownGO.AddComponent<Dropdown>();
            RectTransform rectTransform = dropdownGO.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(60, 30);
            rectTransform.anchorMin = new Vector2(1, 0.5f);
            rectTransform.anchorMax = new Vector2(1, 0.5f);
            rectTransform.pivot = new Vector2(1f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0, 0);

            // 添加背景图像
            Image backgroundImage = dropdownGO.AddComponent<Image>();
            backgroundImage.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.raycastTarget = true;
            
            // 创建标签文本
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(dropdownGO.transform, false);

            Text labelText = labelGO.AddComponent<Text>();
            labelText.text = "A";
            labelText.color = Color.green;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;

            RectTransform labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(5, 0);
            labelRect.sizeDelta = new Vector2(40, 30);
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.pivot = new Vector2(0, 0.5f);

            // 创建箭头指示器
            GameObject arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(dropdownGO.transform, false);

            Text arrowText = arrowGO.AddComponent<Text>();
            arrowText.text = "▼";
            arrowText.color = Color.green;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.fontSize = 12;

            RectTransform arrowRect = arrowGO.GetComponent<RectTransform>();
            arrowRect.anchoredPosition = new Vector2(-10, 0);
            arrowRect.sizeDelta = new Vector2(20, 30);
            arrowRect.anchorMin = new Vector2(1, 0);
            arrowRect.anchorMax = new Vector2(1, 1);
            arrowRect.pivot = new Vector2(1, 0.5f);

            // --- 创建模板（Template）--- 
            GameObject templateGO = new GameObject("Template");
            templateGO.transform.SetParent(dropdownGO.transform, false);
            var templateTransform = templateGO.AddComponent<RectTransform>();
            templateTransform.pivot = new Vector2(0.5f, 1f);
            templateTransform.anchorMin = new Vector2(0, 0);
            templateTransform.anchorMax = new Vector2(1, 0);
            templateTransform.anchoredPosition = new Vector2(0, 0);
            templateTransform.offsetMin = new Vector2(0, templateTransform.offsetMin.y);
            templateTransform.offsetMax = new Vector2(0, templateTransform.offsetMax.y);
            templateGO.SetActive(false);

            Image templateImage = templateGO.AddComponent<Image>();
            templateImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);  
            templateImage.type = Image.Type.Sliced;

            ScrollRect templateScroll = templateGO.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;
            templateScroll.decelerationRate = 0.135f;

            // --- 创建滚动视图的视口（Viewport） ---
            GameObject viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(templateGO.transform, false);

            Image viewportImage = viewportGO.AddComponent<Image>(); // Viewport 需要一个Image组件来支持Mask
            viewportImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);

            Mask viewportMask = viewportGO.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            RectTransform viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchoredPosition = Vector2.zero;
            viewportRect.sizeDelta = Vector2.zero; // 大小Delta为零，将拉伸填充父节点（Template）
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0, 1);

            // --- 创建滚动视图的内容（Content） ---
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);

            RectTransform contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 32); // 高度设置为所有选项的总高度估算值
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);

            // --- 创建选项项（Item）模板 ---
            GameObject itemGO = new GameObject("Item");
            itemGO.transform.SetParent(contentGO.transform, false);

            Toggle itemToggle = itemGO.AddComponent<Toggle>();
            Image itemBackgroundImage = itemGO.AddComponent<Image>(); // 这是Toggle的targetGraphic
            itemBackgroundImage.raycastTarget = true;
            itemBackgroundImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            itemToggle.targetGraphic = itemBackgroundImage;
            itemToggle.isOn = false;

            RectTransform itemRect = itemGO.GetComponent<RectTransform>();
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.offsetMin = new Vector2(0, itemRect.offsetMin.y);
            itemRect.offsetMax = new Vector2(0, itemRect.offsetMax.y);
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 30);

            // --- 创建选项文本 ---
            GameObject itemLabelGO = new GameObject("Item Label");
            itemLabelGO.transform.SetParent(itemGO.transform, false);

            Text itemLabelText = itemLabelGO.AddComponent<Text>();
            itemLabelText.color = Color.green;
            itemLabelText.alignment = TextAnchor.MiddleLeft;
            itemLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabelText.fontSize = 14;

            RectTransform itemLabelRect = itemLabelGO.GetComponent<RectTransform>();
            itemLabelRect.anchoredPosition = new Vector2(0, 0);
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.pivot = new Vector2(0.5f, 0.5f);
            itemLabelRect.sizeDelta = Vector2.zero;

            // --- 关键步骤：正确关联Dropdown组件的各个部分 ---
            dropdown.template = templateTransform;
            dropdown.captionText = labelText;
            dropdown.itemText = itemLabelText;
            
            templateScroll.content = contentRect;
            templateScroll.viewport = viewportRect;
            
            m_Dropdown = dropdown;
        }

        private void ProcessInput(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                XCommand.Execute(str, out object value, XCommandSource.Ugui);
            }
        }

        public void OnInit()
        {
            CreateConsoleWindow();
            m_VisibleAfterRecordId = XCommandHub.LatestRecordId;
        }

        public void OnOpen()
        {
            if (m_HasOpened)
            {
                ResetVisibleOutput();
            }
            m_HasOpened = true;
            Subscribe();
            consoleRoot.SetActive(true);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(m_InputField.gameObject);
            m_InputField.ActivateInputField();
        } 

        public void OnClose()
        {
            Unsubscribe();
            consoleRoot.SetActive(false);
        }

        public void OnLogMessage(Message message)
        {
            if (m_TextContent == null)
            {
                return;
            }

            m_DisplayEntries.Add(DisplayEntry.FromLog(message));
            RenderOutput();
        }

        public void OnExecuteCmd(string cmd, object value)
        {
            m_InputField.text = "";
            m_InputField.ActivateInputField();
        }

        public void OnCommandChange()
        {
            RefreshDropDown();
        }

        private void RefreshDropDown()
        {
            if (!m_Dropdown)
            {
                return;
            }
            var keys = XCommand.CommandKeys;
            var key = XCommand.CurrentCommandKey;
            var index = keys.ToList().IndexOf(key);
            m_Dropdown.onValueChanged.RemoveAllListeners();
            m_Dropdown.ClearOptions();
            m_Dropdown.AddOptions(keys.ToList());
            m_Dropdown.value = index;
            m_Dropdown.RefreshShownValue();
            m_Dropdown.onValueChanged.AddListener((idx) =>
            {
                var key = XCommand.CommandKeys[idx];
                XCommand.ChangeCommand(key);
            });
        }

        public void OnClear()
        {
            ResetVisibleOutput();
        }

        public void OnCurrentCmdChanged(string cmd)
        {
            m_InputField.text = cmd;
            m_InputField.caretPosition = cmd.Length;
        }

        internal void Dispose()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (m_IsSubscribed)
            {
                return;
            }
            XCommandHub.RecordAdded += OnCommandRecordAdded;
            XCommandHub.RecordUpdated += OnCommandRecordUpdated;
            XCommandHub.RecordsTrimmed += OnCommandRecordTrimmed;
            XCommandHub.DisplayClearRequested += OnDisplayClearRequested;
            XCommandHub.DisplayHistoryRequested += OnDisplayHistoryRequested;
            m_IsSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!m_IsSubscribed)
            {
                return;
            }
            XCommandHub.RecordAdded -= OnCommandRecordAdded;
            XCommandHub.RecordUpdated -= OnCommandRecordUpdated;
            XCommandHub.RecordsTrimmed -= OnCommandRecordTrimmed;
            XCommandHub.DisplayClearRequested -= OnDisplayClearRequested;
            XCommandHub.DisplayHistoryRequested -= OnDisplayHistoryRequested;
            m_IsSubscribed = false;
        }

        private void OnDisplayClearRequested(XCommandSource source)
        {
            if (source == XCommandSource.Ugui)
            {
                ResetVisibleOutput();
            }
        }

        private void OnDisplayHistoryRequested(XCommandSource source)
        {
            if (source == XCommandSource.Ugui)
            {
                LoadHistory();
            }
        }

        private void OnCommandRecordAdded(XCommandRecord record)
        {
            if (record.Id <= m_VisibleAfterRecordId)
            {
                return;
            }
            m_DisplayEntries.Add(DisplayEntry.FromCommand(record));
            m_DisplayedCommandIds.Add(record.Id);
            RenderOutput();
        }

        private void OnCommandRecordUpdated(XCommandRecord record)
        {
            if (m_DisplayedCommandIds.Contains(record.Id))
            {
                RenderOutput();
            }
        }

        private void OnCommandRecordTrimmed(long recordId)
        {
            if (!m_DisplayedCommandIds.Remove(recordId))
            {
                return;
            }
            m_DisplayEntries.RemoveAll(entry => entry.Command?.Id == recordId);
            RenderOutput();
        }

        private void ResetVisibleOutput()
        {
            m_VisibleAfterRecordId = XCommandHub.LatestRecordId;
            m_DisplayEntries.Clear();
            m_DisplayedCommandIds.Clear();
            RenderOutput();
        }

        private void LoadHistory()
        {
            var history = new List<DisplayEntry>();
            IReadOnlyList<XCommandRecord> records = XCommandHub.Records;
            for (int i = 0; i < records.Count; i++)
            {
                XCommandRecord record = records[i];
                if (m_DisplayedCommandIds.Add(record.Id))
                {
                    history.Add(DisplayEntry.FromCommand(record));
                }
            }
            m_DisplayEntries.InsertRange(0, history);
            m_VisibleAfterRecordId = records.Count == 0 ? XCommandHub.LatestRecordId : records[0].Id - 1;
            RenderOutput();
        }

        private void ToggleExceptionDetails()
        {
            m_ShowExceptionDetails = !m_ShowExceptionDetails;
            m_ExceptionDetailsButtonText.text = m_ShowExceptionDetails ? "异常:开" : "异常:关";
            m_ExceptionDetailsButtonText.color = m_ShowExceptionDetails ? Color.cyan : Color.green;
            RenderOutput();
        }

        private void RenderOutput()
        {
            if (m_TextContent == null)
            {
                return;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < m_DisplayEntries.Count; i++)
            {
                DisplayEntry entry = m_DisplayEntries[i];
                if (entry.Command == null)
                {
                    builder.Append(entry.LogMessage.ToGUIString());
                    continue;
                }

                XCommandRecord command = entry.Command;
                builder.Append(Message.Input($"[{FormatSource(command.Source)}] {command.CommandLine}").ToGUIString());
                if (command.State != XCommandRecordState.Completed)
                {
                    continue;
                }
                if (command.Succeeded)
                {
                    builder.Append(Message.Output(command.Output).ToGUIString());
                    continue;
                }

                string failure = $"[{command.Status}] {command.Message}";
                if (m_ShowExceptionDetails && !string.IsNullOrEmpty(command.Exception))
                {
                    failure += $"\n{command.Exception}";
                }
                builder.Append(Message.Error(failure, string.Empty).ToGUIString());
            }
            m_TextContent.text = builder.ToString();
        }

        private static string FormatSource(XCommandSource source)
        {
            switch (source)
            {
                case XCommandSource.Api: return "API";
                case XCommandSource.Ugui: return "UGUI";
                case XCommandSource.Cli: return "CLI";
                default: return source.ToString();
            }
        }

        private sealed class DisplayEntry
        {
            private DisplayEntry(Message logMessage, XCommandRecord command)
            {
                LogMessage = logMessage;
                Command = command;
            }

            public Message LogMessage;
            public XCommandRecord Command { get; }

            public static DisplayEntry FromLog(Message message)
            {
                return new DisplayEntry(message, null);
            }

            public static DisplayEntry FromCommand(XCommandRecord command)
            {
                return new DisplayEntry(default, command);
            }
        }
    }
}
