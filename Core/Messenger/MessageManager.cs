using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace XFramework.Event
{
    /// <summary>
    /// 消息类 全局类消息
    /// </summary>
    [Preserve]
    public class MessageManager : Singleton<MessageManager>
    {
        private readonly Dictionary<string, Delegate> m_eventDictionary = new Dictionary<string, Delegate>();

        #region AddEventListener

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener(string eventKey, Action handler)
        {
            OnPreListenerAdd(eventKey, handler);
            m_eventDictionary[eventKey] = (Action)m_eventDictionary[eventKey] + handler;
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener<T>(string eventKey, Action<T> handler)
        {
            OnPreListenerAdd(eventKey, handler);
            m_eventDictionary[eventKey] = (Action<T>)m_eventDictionary[eventKey] + handler;
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener<T, U>(string eventKey, Action<T, U> handler)
        {
            OnPreListenerAdd(eventKey, handler);
            m_eventDictionary[eventKey] = (Action<T, U>)m_eventDictionary[eventKey] + handler;
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <typeparam name="V">参数三类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener<T, U, V>(string eventKey, Action<T, U, V> handler)
        {
            OnPreListenerAdd(eventKey, handler);
            m_eventDictionary[eventKey] = (Action<T, U, V>)m_eventDictionary[eventKey] + handler;
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener(string eventKey, Delegate handler)
        {
            OnPreListenerAdd(eventKey, handler);
            Delegate old = m_eventDictionary[eventKey];
            m_eventDictionary[eventKey] = Delegate.Combine(old, handler);
        }

        #endregion

        #region RemoveEventListener

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener(string eventKey, Action handler)
        {
            OnPreListenerRemove(eventKey, handler);
            m_eventDictionary[eventKey] = (Action)m_eventDictionary[eventKey] - handler;
            OnPostListenerRemove(eventKey);
        }

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener<T>(string eventKey, Action<T> handler)
        {
            OnPreListenerRemove(eventKey, handler);
            m_eventDictionary[eventKey] = (Action<T>)m_eventDictionary[eventKey] - handler;
            OnPostListenerRemove(eventKey);
        }

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener<T, U>(string eventKey, Action<T, U> handler)
        {
            OnPreListenerRemove(eventKey, handler);
            m_eventDictionary[eventKey] = (Action<T, U>)m_eventDictionary[eventKey] - handler;
            OnPostListenerRemove(eventKey);
        }

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <typeparam name="V">参数三类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener<T, U, V>(string eventKey, Action<T, U, V> handler)
        {
            OnPreListenerRemove(eventKey, handler);
            m_eventDictionary[eventKey] = (Action<T, U, V>)m_eventDictionary[eventKey] - handler;
            OnPostListenerRemove(eventKey);
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <param name="eventKey">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener(string eventKey, Delegate handler)
        {
            OnPreListenerRemove(eventKey, handler);
            Delegate old = m_eventDictionary[eventKey];
            m_eventDictionary[eventKey] = Delegate.Remove(old, handler);
            OnPostListenerRemove(eventKey);
        }

        #endregion

        #region OnPreListenerAdd OnPreListenerRemove OnPostListenerRemove

        private void OnPreListenerAdd(string eventKey, Delegate listenerBeingAdded)
        {
            if (!m_eventDictionary.ContainsKey(eventKey))
            {
                m_eventDictionary.Add(eventKey, null);
            }

            Delegate d = m_eventDictionary[eventKey];

            if (d != null && d.GetType() != listenerBeingAdded.GetType())
            {
                throw new XFrameworkException(string.Format("Attempting to add listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being added has type {2}", eventKey, d.GetType().Name, listenerBeingAdded.GetType().Name));
            }
        }

        private void OnPreListenerRemove(string eventKey, Delegate listenerBeingRemoved)
        {
            if (m_eventDictionary.ContainsKey(eventKey))
            {
                Delegate d = m_eventDictionary[eventKey];

                if (d == null)
                {
                    throw new XFrameworkException(string.Format("Attempting to remove listener with for event type \"{0}\" but current listener is null.", eventKey));
                }
                else if (d.GetType() != listenerBeingRemoved.GetType())
                {
                    throw new XFrameworkException(string.Format("Attempting to remove listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being removed has type {2}", eventKey, d.GetType().Name, listenerBeingRemoved.GetType().Name));
                }
            }
            else
            {
                throw new XFrameworkException(string.Format("Attempting to remove listener for type \"{0}\" but Messenger doesn't know about this event type.", eventKey));
            }
        }

        private void OnPostListenerRemove(string eventKey)
        {
            if (m_eventDictionary[eventKey] == null)
            {
                m_eventDictionary.Remove(eventKey);
            }
        }

        #endregion

        #region BroadCastEventMsg

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <param name="eventKey">事件类型</param>
        public void BroadCast(string eventKey)
        {
            if (m_eventDictionary.TryGetValue(eventKey, out Delegate d))
            {
                if (d is Action callback)
                {
                    callback();
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventKey);
                }
            }
        }

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="arg1">参数</param>
        public void BroadCast<T>(string eventKey, T arg1)
        {
            if (m_eventDictionary.TryGetValue(eventKey, out Delegate d))
            {
                if (d is Action<T> callback)
                {
                    callback(arg1);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventKey);
                }
            }
        }

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        public void BroadCast<T, U>(string eventKey, T arg1, U arg2)
        {
            if (m_eventDictionary.TryGetValue(eventKey, out Delegate d))
            {

                if (d is Action<T, U> callback)
                {
                    callback(arg1, arg2);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventKey);
                }
            }
        }

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <typeparam name="V">参数三类型</typeparam>
        /// <param name="eventKey">事件类型</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        public void BroadCast<T, U, V>(string eventKey, T arg1, U arg2, V arg3)
        {
            if (m_eventDictionary.TryGetValue(eventKey, out Delegate d))
            {
                if (d is Action<T, U, V> callback)
                {
                    callback(arg1, arg2, arg3);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventKey);
                }
            }
        }

        private XFrameworkException CreateBroadcastSignatureException(string eventKey)
        {
            return new XFrameworkException($"Broadcasting message \"{eventKey}\" but listeners have a different signature than the broadcaster.");
        }

        #endregion
    }
}