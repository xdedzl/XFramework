using System;
using System.Collections.Generic;
using System.Reflection;

namespace XFramework.Event
{
    public class MessageListenerAttribute : Attribute
    {
        public string eventName;
        public MessageListenerAttribute(string eventName)
        {
            this.eventName = eventName;
        }
    }

    /// <summary>
    /// 消息自动化注册机
    /// </summary>
    public class RegersterHelper
    {
        private readonly Dictionary<string, Delegate> m_delegates = new Dictionary<string, Delegate>();

        public RegersterHelper(object listener)
        {
            InitEvent(listener);
        }

        private void InitEvent(object listener)
        {
            var methods = listener.GetType().GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<MessageListenerAttribute>();
                if (attr != null)
                {
                    var parms = method.GetParameters();
                    var count = parms.Length;
                    Type actionType = null;
                    if (count == 0)
                    {
                        actionType = typeof(Action);
                    }
                    else if (count == 1)
                    {
                        actionType = typeof(Action<>).MakeGenericType(parms[0].ParameterType);
                    }
                    else if (count == 2)
                    {
                        actionType = typeof(Action<,>).MakeGenericType(parms[0].ParameterType, parms[1].ParameterType);
                    }
                    else if (count == 3)
                    {
                        actionType = typeof(Action<,,>).MakeGenericType(parms[0].ParameterType, parms[1].ParameterType, parms[2].ParameterType);
                    }
                    else
                    {
                        throw new XFrameworkException("[Messenge] do not spport more than 3 parameter，please redesign listener");
                    }

                    Delegate @delegate;
                    if(method.IsStatic)
                        @delegate = Delegate.CreateDelegate(actionType, method);
                    else
                        @delegate = Delegate.CreateDelegate(actionType, listener, method);
                    m_delegates.Add(attr.eventName, @delegate);
                }
            }
        }

        /// <summary>
        /// 注册
        /// </summary>
        public void Register()
        {
            foreach (var item in m_delegates)
            {
                MessageManager.Instance.AddListener(item.Key, item.Value);
            }
        }

        /// <summary>
        /// 反注册
        /// </summary>
        public void UnRegister()
        {
            foreach (var item in m_delegates)
            {
                MessageManager.Instance.RemoveListener(item.Key, item.Value);
            }
        }

        /// <summary>
        /// 创建一个自动话注册机
        /// </summary>
        /// <param name="listener">监听者</param>
        /// <returns></returns>
        public static RegersterHelper Create(object listener)
        {
            return new RegersterHelper(listener);
        }
    }
}