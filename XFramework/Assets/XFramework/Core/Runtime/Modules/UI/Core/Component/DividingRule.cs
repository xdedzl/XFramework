using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

namespace XFramework.UI
{
    /// <summary>
    /// 时间单位类型
    /// </summary>
    public enum TimeUnitType
    {
        [Description("ms")]     Millisecond,
        [Description("s")]      Second,
        [Description("m")]      Min,
        [Description("h")]      Hour,
    }

    /// <summary>
    /// 刻度尺
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/DividingRule")]
    public class DividingRule : Graphic, IScrollHandler
    {
        private TimeUnitType m_TimeUnitType = TimeUnitType.Second;

        /// <summary>
        /// 刻度尺颜色
        /// </summary>
        public Color axleColor = Color.black;
        /// <summary>
        /// 刻度尺线宽
        /// </summary>
        public float lineWidth = 2;
        /// <summary>
        /// 刻度线高度
        /// </summary>
        public int scaleLineHeight = 50;
        /// <summary>
        /// 一个格子的最大宽度
        /// </summary>
        public float maxGridSize = 100;
        /// <summary>
        /// 一个格子的最小宽度
        /// </summary>
        public float minGridSize = 50;
        public TimeUnitType minTimeUnitType = TimeUnitType.Millisecond;
        public TimeUnitType maxTimeUnitType = TimeUnitType.Hour;

        public GameObject textTemplate;

        /// <summary>
        /// 刻度尺最左侧代表的时间
        /// </summary>
        private float m_LeftTime = 0;
        /// <summary>
        /// 一个格子代表的时间
        /// </summary>
        private int m_TimePerGrid = 1; 
        /// <summary>
        /// 一个格子的大小
        /// </summary>
        private float m_GridSize = 50;

        /// <summary>
        /// 刻度线数量
        /// </summary>
        private int m_Count;

        /// <summary>
        /// 记录时间
        /// </summary>
        private List<Text> m_Texts;
        private Stack<Text> m_TextPool;
        private List<Vector2> m_LinePoses;

        /// <summary>
        /// 时间文字的父物体
        /// </summary>
        private Transform m_TextParent;
        private Text timeUnitText;

        /// <summary>
        /// 每个像素代表的时间发生变化
        /// </summary>
        public OnTimePerPixelChange onTimePerPixelChange = new OnTimePerPixelChange();

        private readonly Dictionary<int, int[]> ScaleNumData = new Dictionary<int, int[]>
        {
            {
                1000,
                new int[]{ 1,5,10,20,50,100,200,500}
            },
            {
                60,
                new int[]{ 1,5,10,30}
            },
            {
                24,
                new int[]{ 1,2,4,12}
            }
        };

        public DividingRule()
        {
            m_Texts = new List<Text>();
            m_LinePoses = new List<Vector2>();
            m_TextPool = new Stack<Text>();
        }

        /// <summary>
        /// 一个像素代表的时间
        /// </summary>
        public float TimePerPixel
        {
            get
            {
                return m_TimePerGrid / m_GridSize;
            }
        }

        /// <summary>
        /// 一个像素代表的时间，单位为秒
        /// </summary>
        public float TimePerPixel_S
        {
            get
            {
                float v = m_TimePerGrid / m_GridSize;
                return Time2Second(v);
            }
        }

        /// <summary>
        /// 刻度尺最左侧代表的时间
        /// </summary>
        public float LeftTime
        {
            get
            {
                return m_LeftTime;
            }
            set
            {
                m_LeftTime = value;
                Refresh();
            }
        }

        /// <summary>
        /// 刻度尺最左侧代表的时间_单位为秒
        /// </summary>
        public float LeftTime_S
        {
            get
            {
                return Time2Second(LeftTime);
            }
            set
            {
                switch (TimeUnitType)
                {
                    case TimeUnitType.Millisecond:
                        LeftTime = value * 1000;
                        break;
                    case TimeUnitType.Second:
                        LeftTime = value;
                        break;
                    case TimeUnitType.Min:
                        LeftTime = value / 60f;
                        break;
                    case TimeUnitType.Hour:
                        LeftTime = value / 3600f;
                        break;
                    default:
                        throw new System.Exception("请补充所有情况");
                }
            }
        }

        /// <summary>
        /// 当前时间类型
        /// </summary>
        public TimeUnitType TimeUnitType
        {
            get
            {
                return m_TimeUnitType;
            }
            private set
            {
                m_TimeUnitType = value;
                timeUnitText.text = Utility.Enum.GetDescriptions<TimeUnitType>()[(int)value];
            }
        }

        /// <summary>
        /// 当前进制
        /// </summary>
        private int ScaleNum
        {
            get
            {
                return GetScaleNum(TimeUnitType);
            }
        }

        protected override void Awake()
        {
            if (Application.isPlaying)
            {
                m_TextParent = new GameObject("TextRoot").transform;
                m_TextParent.SetParent(transform, false);
            }
            textTemplate = GetComponentInChildren<Text>().gameObject;
            timeUnitText = transform.Find("timeUnitText").GetComponent<Text>();

            TimeUnitType = minTimeUnitType; 
            Refresh();
        }

        #region 渲染

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            DrawAxle(vh);
        }

        /// <summary>
        /// 坐标轴
        /// </summary>
        /// <param name="vh"></param>
        private void DrawAxle(VertexHelper vh)
        {
            UIVertex[] verts = new UIVertex[4];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].color = axleColor;
            }

            Rect rect = rectTransform.rect;
            float ruleWidth = rect.width;

            // X轴
            verts[0].position = rect.min + Vector2.zero;
            verts[1].position = rect.min + new Vector2(0, lineWidth);
            verts[2].position = rect.min + new Vector2(ruleWidth, lineWidth);
            verts[3].position = rect.min + new Vector2(ruleWidth, 0);
            vh.AddUIVertexQuad(verts);

            //刻度线
            foreach(Vector2 pos in m_LinePoses)
            {
                DrawScaleLine(pos);
            }

            void DrawScaleLine(Vector2 pos)
            {
                float halfWidth = lineWidth / 2;
                pos.x -= halfWidth;

                verts[0].position = pos;
                verts[1].position = new Vector2(lineWidth, 0) + pos;
                verts[2].position = new Vector2(lineWidth, scaleLineHeight) + pos;
                verts[3].position = new Vector2(0, scaleLineHeight) + pos;
                vh.AddUIVertexQuad(verts);
            }
        }

        private void Refresh()
        {
            RefreshData();
            SetAllDirty();

            if (Application.isPlaying)
            {
                RefreshText();
            }
        }

        private void RefreshData()
        {
            m_Count = (int)(rectTransform.rect.width / m_GridSize);

            float offsetX = m_GridSize - (m_LeftTime / TimePerPixel) % m_GridSize;
            m_LinePoses = new List<Vector2>(m_Count);
            for (int i = 0; i < m_Count; i++)
            {
                var pos = rectTransform.rect.min + new Vector2(m_GridSize * i + offsetX, lineWidth);
                m_LinePoses.Add(pos);
            }
        }

        public void RefreshText()
        {
            // 刷新Text数量
            if(m_Texts.Count > m_Count)
            {
                while(m_Texts.Count > m_Count)
                {
                    Recycle(m_Texts[m_Texts.Count - 1]);
                }
            }
            else if(m_Texts.Count < m_Count)
            {
                while (m_Texts.Count < m_Count)
                {
                    Allocate();
                }
            }

            int offsetX = (int)m_LeftTime / m_TimePerGrid + 1;
            // 刷新Text位置
            for (int i = 0; i < m_Texts.Count; i++)
            {
                m_Texts[i].transform.localPosition = m_LinePoses[i] + new Vector2(lineWidth, scaleLineHeight);
                m_Texts[i].text = Time2Str((i + offsetX) * m_TimePerGrid);
            }
        }

        private Text Allocate()
        {
            Text text;
            if (m_TextPool.Count > 0)
            {
                text = m_TextPool.Pop();
                text.enabled = true;
            }
            else
            {
                var obj = Instantiate(textTemplate, m_TextParent);
                text = obj.GetComponent<Text>();
            }
            m_Texts.Add(text);

            return text;
        }

        private void Recycle(Text text)
        {
            text.enabled = false;
            m_TextPool.Push(text);
            m_Texts.Remove(text);
        }

        #endregion

        /// <summary>
        /// 将时间从当前时间单位转为秒
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        private float Time2Second(float v)
        {
            switch (TimeUnitType)
            {
                case TimeUnitType.Millisecond:
                    return v / 1000;
                case TimeUnitType.Second:
                    return v;
                case TimeUnitType.Min:
                    return v * 60;
                case TimeUnitType.Hour:
                    return v * 3600;
                default:
                    throw new System.Exception("请补充所有情况");
            }
        }

        /// <summary>
        /// 时间转字符串
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private string Time2Str(int time)
        {
            //return time.ToString();
            string timeStr = "";
            switch (TimeUnitType)
            {
                case TimeUnitType.Millisecond:
                    timeStr = $"{time:d2}s";
                    break;
                case TimeUnitType.Second:
                case TimeUnitType.Min:
                    TimeConvert();
                    break;
                case TimeUnitType.Hour:
                    timeStr = $"{time:d2}s";
                    break;
            }
            return timeStr;

            void TimeConvert()
            {
                if (time < 60)
                {
                    timeStr = $"{time:d2}";
                }
                else
                {
                    int min = time / 60;
                    int sec = time % 60;
                    timeStr = $"{min:d2}:{sec:d2}";
                }
            }
        }

        private int GetScaleNum(TimeUnitType timeUnitType)
        {
            switch (timeUnitType)
            {
                case TimeUnitType.Millisecond:
                    return 1000;
                case TimeUnitType.Second:
                case TimeUnitType.Min:
                    return 60;
                case TimeUnitType.Hour:
                    return 24;
                default:
                    throw new System.Exception("请设置所有枚举的返回值");
            }
        }

        private int GetNextTimePerGrid()
        {
            var array = ScaleNumData[ScaleNum];

            for (int i = 0; i < array.Length; i++)
            {
                if (m_TimePerGrid == array[i])
                {
                    if (i + 1 == array.Length)
                    {
                        if (TimeUnitType == maxTimeUnitType)
                        {
                            return 0;
                        }
                        else
                        {
                            var type = TimeUnitType + 1;
                            var temp = ScaleNumData[GetScaleNum(type)];
                            return temp[0];
                        }
                    }
                    else
                    {
                        return array[i + 1];
                    }
                }
            }
            //return 0;
            throw new System.Exception("检擦代码逻辑");
        }

        private int GetPrevTimePerGrid()
        {
            var array = ScaleNumData[ScaleNum];

            for (int i = array.Length - 1; i >= 0; i--)
            {
                if (m_TimePerGrid == array[i])
                {
                    if (i == 0)
                    {
                        if (TimeUnitType == minTimeUnitType)
                        {
                            return 0;
                        }
                        else
                        {
                            var type = TimeUnitType - 1;
                            var temp = ScaleNumData[GetScaleNum(type)];
                            return temp[temp.Length - 1];
                        }
                    }
                    else
                    {
                        return array[i - 1];
                    }
                }
            }
            throw new System.Exception("检擦代码逻辑");
        }

        public void OnScroll(PointerEventData eventData)
        {
            float value = eventData.scrollDelta.y;

            if(value > 0)
            {
                float targetGridSize = m_GridSize /** value*/ * 1.2f;
                if (targetGridSize > maxGridSize)
                {
                    // new
                    int preTimePerGrid = GetPrevTimePerGrid();

                    if(preTimePerGrid == 0) // 切换时间单位
                    {
                        targetGridSize = m_GridSize;
                    }
                    else // 切换
                    {
                        float ratio;
                        targetGridSize /= 2;
                        if (preTimePerGrid > m_TimePerGrid)
                        {
                            float second = Time2Second(m_TimePerGrid);
                            TimeUnitType--;
                            m_TimePerGrid = preTimePerGrid;
                            ratio = Time2Second(m_TimePerGrid) / second;
                        }
                        else
                        {
                            ratio = preTimePerGrid / (float)m_TimePerGrid;
                            m_TimePerGrid = preTimePerGrid;
                        }

                        LeftTime_S *= ratio;
                    }
                }
                m_GridSize = targetGridSize;
            }
            else
            {
                float targetGridSize = m_GridSize /** -value */* 0.8f;

                if (targetGridSize < minGridSize)
                {
                    // new
                    int preTimePerGrid = GetNextTimePerGrid();

                    if (preTimePerGrid == 0) // 切换时间单位
                    {
                        targetGridSize = m_GridSize;
                    }
                    else // 切换
                    {
                        float ratio;
                        targetGridSize *= 2;
                        if (preTimePerGrid < m_TimePerGrid)
                        {
                            float second = Time2Second(m_TimePerGrid);
                            TimeUnitType++;
                            m_TimePerGrid = preTimePerGrid;
                            ratio = Time2Second(m_TimePerGrid) / second;
                        }
                        else
                        {
                            ratio = preTimePerGrid / (float)m_TimePerGrid;
                            m_TimePerGrid = preTimePerGrid;
                        }
                        LeftTime_S *= ratio;
                    }
                }
                m_GridSize = targetGridSize;
            }

            onTimePerPixelChange.Invoke(TimePerPixel);
            Refresh();
        }

        //protected override void OnValidate()
        //{
        //    RefreshData();
        //    SetAllDirty();  
        //}

        public class OnTimePerPixelChange : UnityEvent<float> { }
    }
}