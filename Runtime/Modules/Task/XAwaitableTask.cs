using System;
using System.Runtime.CompilerServices;

// ！！！ 一条任务链，要么全用 AwaitableTask, 要么全用XTask, 不要混用
namespace XFramework.Tasks
{
    // 无返回值的XTask（核心类）
    [AsyncMethodBuilder(typeof(XAwaitableTaskMethodBuilder))]
    public partial class XAwaitableTask
    {
        private readonly ITask m_SourceTask;
        private bool m_IsCompleted;
        private Action m_Continuation;
        
        private bool IsCompleted
        {
            get
            {
                if (m_SourceTask == null)
                {
                    return m_IsCompleted;
                }
                else
                {
                    return m_SourceTask.IsDone;
                }
            }
        }

        public XAwaitableTask(): this(null) { }
        
        public XAwaitableTask(ITask sourceTask)
        {
            m_SourceTask = sourceTask;
        }

        // 异步等待（核心：通过Awaiter实现await语法）
        public XTaskAwaiter GetAwaiter()
        {
            return new XTaskAwaiter(this);
        }
        
        internal void SetResult()
        {
            m_IsCompleted = true;
            m_Continuation?.Invoke();
        }

        public void Forget()
        {
            
        }

        // Awaiter：实现await的核心接口
        public readonly struct XTaskAwaiter : ICriticalNotifyCompletion
        {
            private readonly XAwaitableTask m_Task;

            public XTaskAwaiter(XAwaitableTask task) => m_Task = task;

            // 是否完成（await会检查这个属性）
            public bool IsCompleted => m_Task.IsCompleted;

            // 完成后执行（同步执行）
            public void GetResult() { }

            // 注册回调（异步执行）
            public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

            // 无安全检查的回调注册（ICriticalNotifyCompletion要求）
            public void UnsafeOnCompleted(Action continuation)
            {
                if (m_Task.m_SourceTask == null)
                {
                    m_Task.m_Continuation = continuation;
                }
                else
                {
                    if (IsCompleted)
                    {
                        continuation();
                    }
                    else
                    {
                        m_Task.m_SourceTask.AddCompleteListener(continuation);
                    }
                }
            }
        }
    }

    // 有返回值的XTask<T>
    [AsyncMethodBuilder(typeof(XAwaitableTaskMethodBuilder<>))]
    public class XAwaitableTask<T>
    {
        private readonly ITask<T> m_SourceTask;
        private bool m_IsCompleted;
        private T m_Result;

        private bool IsCompleted
        {
            get
            {
                if (m_SourceTask == null)
                {
                    return m_IsCompleted;
                }
                else
                {
                    return m_SourceTask.IsDone;
                }
            }
        }

        private T Result
        {
            get
            {
                if (m_SourceTask == null)
                {
                    return m_Result;
                }
                else
                {
                    return m_SourceTask.Result;
                }
            }
        }

        private Action m_Continuation;

        public XAwaitableTask(): this(null) { }
        
        public XAwaitableTask(ITask<T> task)
        {
            m_SourceTask = task;
        }

        public XTaskAwaiter GetAwaiter()
        {
            return new XTaskAwaiter(this);
        }
        
        internal void SetResult(T result)
        {
            m_IsCompleted = true;
            m_Result = result;
            m_Continuation?.Invoke();
        }

        public readonly struct XTaskAwaiter : ICriticalNotifyCompletion
        {
            private readonly XAwaitableTask<T> m_Task;

            public XTaskAwaiter(XAwaitableTask<T> task) => m_Task = task;

            public bool IsCompleted => m_Task.IsCompleted;

            // 获取任务结果（await的返回值）
            public T GetResult() => m_Task.Result;

            public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);
            
            public void Forget()
            {
            
            }
            
            public void UnsafeOnCompleted(Action continuation)
            {
                if (m_Task.m_SourceTask == null)
                {
                    m_Task.m_Continuation = continuation;
                }
                else
                {
                    if (IsCompleted)
                    {
                        continuation();
                    }
                    else
                    {
                        m_Task.m_SourceTask.AddCompleteListener(continuation);
                    }
                }
            }
        }
    }
}