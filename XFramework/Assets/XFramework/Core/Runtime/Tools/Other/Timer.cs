using System.Collections.Generic;
using UnityEngine;
using Action = System.Action;

namespace XFramework
{
    public class Timer
    {
        private static TimerManager s_Manager;
        private TimerManager Manager
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
        private Action timeUpDel;
        /// <summary>
        /// 是否执行
        /// </summary>
        private bool _isRunning;
        /// <summary>
        /// 已执行时间（每次满足运行间隔就会加这个）
        /// </summary>
        private float _useTime;
        /// <summary>
        /// 运行时间
        /// </summary>
        public float RunTime { get; private set; }
        /// <summary>
        /// 已运行次数
        /// </summary>
        public int UseCount { get; private set; }
        /// <summary>
        /// 运行间隔
        /// </summary>
        private float interval;
        /// <summary>
        /// 设置的运行次数
        /// </summary>
        private int repeatCount;

        /// <summary>
        /// <param name="interval">时间间隔，单位是毫秒</param>
        /// <param name="repeatCount">运行次数，一秒一次的话MaxValue可以执行68年</param>
        /// </summary>
        public Timer(float interval, int repeatCount = 1)
        {
            RunTime = 0f;
            if (interval <= 0)
                interval = 0.01f;
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
                return _isRunning;
            }
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    if (_isRunning)
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
        /// 每帧执行
        /// </summary>
        /// <param name="deltaTime"></param>
        internal void Update(float deltaTime)
        {
            if (IsRunning && UseCount < repeatCount)
            {
                RunTime += deltaTime;
                while (RunTime - _useTime > interval && UseCount < repeatCount)
                {
                    UseCount++;
                    _useTime += interval;
                    timeUpDel?.Invoke();
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
            timeUpDel += fun;
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fun"></param>
        public void RemoveListener(Action fun)
        {
            timeUpDel -= fun;
        }

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
            _useTime = 0f;
            UseCount = 0;
        }

        #region 外部调用的静态函数

        /// <summary>
        /// 注册一个延时执行的任务
        /// </summary>
        /// <param name="interval">间隔时间</param>
        /// <param name="action">任务</param>
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
        public static Timer Register(float interval, int repeatCount, Action action)
        {
            Timer timer = new Timer(interval, repeatCount);
            timer.AddListener(action);
            timer.Start();
            return timer;
        }

        #endregion

        /// <summary>
        /// 计时器管理
        /// 除了计时器以外其他类暂时不需要调用，以后需要再放到外面去
        /// </summary>
        public class TimerManager : MonoBehaviour
        {
            private readonly List<Timer> _timers = new List<Timer>();

            private void Update()
            {
                for (var i = 0; i < _timers.Count; i++)
                {
                    if (_timers[i].IsRunning)
                    {
                        // unscaledDeltaTime和deltaTime一样，但是不受TimeScale影响
                        _timers[i].Update(Time.unscaledDeltaTime);
                    }
                }
            }

            public void AddTimer(Timer timer)
            {
                if (_timers.Contains(timer) == false)
                {
                    _timers.Add(timer);
                }
            }

            public void RemoveTimer(Timer timer)
            {
                if (_timers.Contains(timer))
                {
                    _timers.Remove(timer);
                }
            }
        }
    }
}