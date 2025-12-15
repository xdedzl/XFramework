using UnityEngine;
using System;

namespace XFramework
{
    /// <summary>
    /// 进度
    /// </summary>
    public interface IProgress
    {
        /// <summary>
        /// 是否完成
        /// </summary>
        bool IsDone { get; }
        /// <summary>
        /// 当前进度
        /// </summary>
        float Progress { get; }
    }
    
    public interface IProgress<T> : IProgress
    {
        /// <summary>
        /// 结果
        /// </summary>
        T Result { get; }
    }

    /// <summary>
    /// 默认直接完成的进度
    /// </summary>
    public class DefaultProgress : IProgress
    {
        /// <summary>
        /// 是否完成 （恒为true）
        /// </summary>
        public bool IsDone => true;
        /// <summary>
        /// 当前进度（恒为1）
        /// </summary>
        public float Progress => 1;
    }
    
    /// <summary>
    /// 默认直接完成的进度
    /// </summary>
    public class DefaultProgress<T> : IProgress<T>
    {
        /// <summary>
        /// 是否完成 （恒为true）
        /// </summary>
        public bool IsDone => true;
        /// <summary>
        /// 当前进度（恒为1）
        /// </summary>
        public float Progress => 1;
        
        private readonly T _result;
        private readonly Func<T> getter;

        public T Result
        {
            get
            {
                if (getter != null)
                {
                    return getter();
                }
                return _result;
            }
        }

        public DefaultProgress(T result)
        {
            _result = result;
        }
        
        public DefaultProgress(Func<T> getter)
        {
            this.getter = getter;
        }
    }

    /// <summary>
    /// 包含多个子任务的进度
    /// </summary>
    public class MultiProgress : IProgress
    {
        private readonly IProgress[] progresses;

        public MultiProgress(IProgress[] progresses)
        {
            this.progresses = progresses;
        }

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsDone
        {
            get
            {
                foreach (var item in progresses)
                {
                    if (!item.IsDone)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// 当前进度
        /// </summary>
        public float Progress
        {
            get
            {
                float p = 0;
                foreach (var item in progresses)
                {
                    p += item.Progress;
                }
                return p / progresses.Length;
            }
        }
    }

    /// <summary>
    /// 执行时可以添加子任务的进度
    /// </summary>
    public class DynamicMultiProgress : IProgress
    {
        private int index = 0;
        private readonly IProgress[] progresses;
        private readonly float[] ratios;

        public DynamicMultiProgress(int count, params float[] ratios)
        {
            if (ratios == null || count != ratios.Length)
            {
                throw new XFrameworkException("[DynamicMultiProgress] params error");
            }

            float plus = 0;
            foreach (var item in ratios)
            {
                plus += item;
            }
            if (!Mathf.Approximately(plus, 1))
            {
                throw new XFrameworkException("[DynamicMultiProgress] ratio error");
            }
            progresses = new IProgress[count];
            this.ratios = ratios;
        }

        /// <summary>
        /// 添加一个子任务
        /// </summary>
        /// <param name="progress">子任务进度</param>
        public void Add(IProgress progress)
        {
            progresses[index] = progress;
            index++;
        }

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsDone
        {
            get
            {
                foreach (var item in progresses)
                {
                    if (item == null || !item.IsDone)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// 当前进度
        /// </summary>
        public float Progress
        {
            get
            {
                float p = 0;
                for (int i = 0; i < progresses.Length; i++)
                {
                    if (progresses[i] != null)
                    {
                        p += progresses[i].Progress * ratios[i];
                    }
                }

                return p;
            }
        }
    }
}