using System.Collections.Generic;
using UnityEngine;
using Action = System.Action;

namespace XFramework
{
    public class Timer
    {
        private const string DefaultgroupName = "default";
        private static TimerManager s_Manager;
        private static TimerManager Manager
        {
            get
            {
                if (Timer.s_Manager == null)
                {
                    TimerManager managerInScene = Object.FindObjectOfType<TimerManager>();
                    if (managerInScene != null)
                    {
                        Timer.s_Manager = managerInScene;
                    }
                    else
                    {
                        GameObject managerObject = new GameObject { name = "TimerManager" };
                        Timer.s_Manager = managerObject.AddComponent<TimerManager>();
                    }
                }
                return s_Manager;
            }
        }

        private Action trriggerEvent;
        /// <summary>
        /// 是否执行
        /// </summary>
        private bool m_isRunning;
        /// <summary>
        /// 已执行时间（每次满足运行间隔就会加这个）
        /// </summary>
        private float m_useTime;
        /// <summary>
        /// 时间运行速度
        /// </summary>
        private float m_timeScale;
        /// <summary>
        /// 计时器所属组名
        /// </summary>
        private string m_groupName;
        /// <summary>
        /// 运行间隔
        /// </summary>
        private readonly float interval;
        /// <summary>
        /// 设置的运行次数
        /// </summary>
        private readonly int repeatCount;
        /// <summary>
        /// 运行时间
        /// </summary>
        public float RunTime { get; private set; }
        /// <summary>
        /// 已运行次数
        /// </summary>
        public int UseCount { get; private set; }

        /// <summary>
        /// 构造一个计时器
        /// </summary>
        /// <param name="interval">时间间隔，单位是毫秒</param>
        /// <param name="repeatCount">运行次数，一秒一次的话MaxValue可以执行68年</param>
        public Timer(float interval, int repeatCount = 1) : this("default", interval, repeatCount) { }

        /// <summary>
        /// 构造一个计时器
        /// </summary>
        /// <param name="groupName">计时器所属组名</param>
        /// <param name="interval">时间间隔，单位是毫秒</param>
        /// <param name="repeatCount">运行次数，一秒一次的话MaxValue可以执行68年</param>
        public Timer(string groupName, float interval, int repeatCount = 1)
        {
            RunTime = 0f;
            if (interval <= 0)
                interval = 0.01f;
            this.m_groupName = groupName;
            this.m_timeScale = 1;
            this.interval = interval;
            this.repeatCount = repeatCount;
        }

        /// <summary>
        /// 是否运行中
        /// </summary>
        private bool IsRunning
        {
            get
            {
                return m_isRunning;
            }
            set
            {
                if (m_isRunning != value)
                {
                    m_isRunning = value;
                    if (m_isRunning)
                    {
                        Manager.AddTimer(this);
                    }
                    else
                    {
                        Manager.RemoveTimer(this);
                    }
                    // TODO 这里可以加一个计时器状态变换的委托
                }
            }
        }

        /// <summary>
        /// 时间运行速度
        /// </summary>
        public float timeScale
        {
            get
            {
                return m_timeScale;
            }
            set
            {
                if (value < 0)
                    throw new XFrameworkException("the timeScale of Timer can not less than 0");
                m_timeScale = value;
            }
        }

        /// <summary>
        /// 计时器所属组名
        /// </summary>
        public string groupName
        {
            get
            {
                return m_groupName;
            }
            set
            {
                if (IsRunning)
                {
                    throw new XFrameworkException("[Timer] running timer can not modify group name");
                }
                m_groupName = value;
            }
        }

        /// <summary>
        /// 每帧执行
        /// </summary>
        /// <param name="deltaTime"></param>
        internal void Update(float deltaTime)
        {
            if (IsRunning && UseCount < repeatCount)
            {
                RunTime += deltaTime * m_timeScale;
                while (RunTime - m_useTime > interval && UseCount < repeatCount)
                {
                    UseCount++;
                    m_useTime += interval;
                    trriggerEvent?.Invoke();
                }
            }
            if (UseCount >= repeatCount)
            {
                IsRunning = false;
            }
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fun"></param>
        public void AddListener(Action fun)
        {
            trriggerEvent += fun;
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fun"></param>
        public void RemoveListener(Action fun)
        {
            trriggerEvent -= fun;
        }

        #region 生命周期

        /// <summary>
        /// 开始(调用了IsRunning的Set,初始化了TimerManager)
        /// </summary>
        public void Start()
        {
            IsRunning = true;
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Pause()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            IsRunning = false;
            RunTime = 0f;
            m_useTime = 0f;
            UseCount = 0;
        }

        #endregion

        #region 外部调用的静态函数

        /// <summary>
        /// 注册一个延时执行的任务
        /// </summary>
        /// <param name="interval">间隔时间</param>
        /// <param name="action">任务</param>
        /// <returns>计时器</returns>
        public static Timer Register(float interval, Action action)
        {
            return Register(interval, 1, action);
        }

        /// <summary>
        /// 注册一个每隔一段时间执行一次的任务
        /// </summary>
        /// <param name="interval">时间间隔</param>
        /// <param name="repeatCount">重复次数</param>
        /// <param name="action">任务</param>
        /// <returns>计时器</returns>
        public static Timer Register(float interval, int repeatCount, Action action)
        {
            return Register(DefaultgroupName, interval, repeatCount, action);
        }

        /// <summary>
        /// 注册一个延时执行的任务
        /// </summary>
        /// <param name="groupName">计时器所属组名</param>
        /// <param name="interval">间隔时间</param>
        /// <param name="action">任务</param>
        /// <returns>计时器</returns>
        public static Timer Register(string groupName, float interval, Action action)
        {
            return Register(groupName, interval, 1, action);
        }

        /// <summary>
        /// 注册一个每隔一段时间执行一次的任务
        /// </summary>
        /// <param name="groupName">计时器所属组名</param>
        /// <param name="interval">时间间隔</param>
        /// <param name="repeatCount">重复次数</param>
        /// <param name="action">任务</param>
        /// <returns>计时器</returns>
        public static Timer Register(string groupName, float interval, int repeatCount, Action action)
        {
            Timer timer = new Timer(groupName, interval, repeatCount);
            timer.AddListener(action);
            timer.Start();
            return timer;
        }

        /// <summary>
        /// 清空一组计时器
        /// </summary>
        /// <param name="groupName"></param>
        public static void ClearTimers(string groupName)
        {
            Manager.ClearTimers(groupName);
        }

        /// <summary>
        /// 清空默认计数器组
        /// </summary>
        public static void ClearDefaultTimers()
        {
            ClearTimers(DefaultgroupName);
        }

        /// <summary>
        /// 计时器的时间速度
        /// </summary>
        public static float TimeScale
        {
            get
            {
                return Manager.timeScale;
            }
            set
            {
                if (value < 0)
                    throw new XFrameworkException("the timeScale of Timer can not less than 0");
                Manager.timeScale = value;
            }
        }

        /// <summary>
        /// 设置一组计时器的时间速度
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="timeScale"></param>
        public static void SetGroupTimerScale(string groupName, float timeScale)
        {
            var timers = Manager.GetTimers(groupName);
            foreach (var timer in timers)
            {
                timer.timeScale = timeScale;
            }
        }

        #endregion

        /// <summary>
        /// 计时器管理
        /// 除了计时器以外其他类暂时不需要调用，以后需要再放到外面去
        /// </summary>
        private class TimerManager : MonoBehaviour
        {
            private readonly Dictionary<string, List<Timer>> m_timerDic = new Dictionary<string, List<Timer>>();
            private readonly List<Timer> m_toRemveTimers = new List<Timer>();
            internal float timeScale = 1;

            private void Update()
            {
                float deltaTime = Time.unscaledDeltaTime * timeScale;

                foreach (var timers in m_timerDic.Values)
                {
                    for (int i = 0; i < timers.Count; i++)
                    {
                        if (timers[i].IsRunning)
                        {
                            timers[i].Update(deltaTime);
                        }
                    }
                }

                foreach (var timer in m_toRemveTimers)
                {
                    if (m_timerDic.TryGetValue(timer.groupName, out List<Timer> timers))
                    {
                        if (timers.Remove(timer))
                        {
                            if (timers.Count == 0)
                            {
                                m_timerDic.Remove(timer.groupName);
                            }
                            continue;
                        }
                    }
                    throw new XFrameworkException("[Timer] try to delete a timer whitch is not managerd by TimeManager");
                }
                m_toRemveTimers.Clear();
            }

            public void AddTimer(Timer timer)
            {
                if (m_timerDic.TryGetValue(timer.groupName, out List<Timer> timers))
                {
                    if (!timers.Contains(timer))
                    {
                        timers.Add(timer);
                    }
                }
                else
                {
                    timers = new List<Timer>
                    {
                        timer
                    };
                    m_timerDic.Add(timer.groupName, timers);
                }
            }

            public void RemoveTimer(Timer timer)
            {
                m_toRemveTimers.Add(timer);
            }

            public void ClearTimers(string groupName)
            {
                m_timerDic.Remove(groupName);
            }

            public IEnumerable<Timer> GetTimers(string groupName)
            {
                if (m_timerDic.TryGetValue(groupName, out List<Timer> timers))
                {
                    return timers;
                }
                throw new XFrameworkException($"[Timer] there is no timer group which name is {groupName}");
            }
        }
    }
}