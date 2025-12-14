using System;
using UnityEngine;

namespace XFramework.Tasks
{
    public abstract partial class XTask
    {
        public virtual bool IsDone { get; protected set; }
        public XTask Next { get; set; }
        public virtual void Update() { }
        
        public static XTask Delay(float time)
        {
            return new TimeTask(time);
        }
        
        public static XTask WhenAll(params XTask[] tasks)
        {
            return new AllTask(tasks);
        }
        
        public static XTask WhenAll(params Func<bool>[] predicates)
        {
            var tasks = new XTask[predicates.Length];
            for (int i = 0; i < predicates.Length; i++)
            {
                tasks[i] = WaitUntil(predicates[i]);
            }
            return WhenAll(tasks);
        }

        public static XTask WhenAny(params XTask[] tasks)
        {
            return new RaceTask(tasks);
        }
        
        public static XTask WhenAny(params Func<bool>[] predicates)
        {
            var tasks = new XTask[predicates.Length];
            for (int i = 0; i < predicates.Length; i++)
            {
                tasks[i] = WaitUntil(predicates[i]);
            }
            return WhenAny(tasks);
        }
        
        public static XTask WaitUntil(Func<bool> predicate)
        {
            return new SingleTask(predicate);
        }

        public static XTask WaitWhile(Func<bool> predicate)
        {
            return new SingleTask(() => !predicate());
        }
        
        /// <summary>
        /// 任务开始
        /// </summary>
        /// <param name="task">任务</param>
        public static void Start(XTask task)
        {
            TaskManager.Instance.StartTask(task);
        }

        /// <summary>
        /// 任务终止
        /// </summary>
        /// <param name="task">任务</param>
        public static void Stop(XTask task)
        {
            TaskManager.Instance.StopTask(task);
        }
    }
}
