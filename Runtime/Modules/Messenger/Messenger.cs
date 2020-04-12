using System;
using System.Collections.Generic;
using XFramework.Singleton;

namespace XFramework.Event
{
    /// <summary>
    /// 消息类 全局类消息
    /// </summary>
    public class MessageManager : Singleton<MessageManager>
    {
        private Dictionary<int, Delegate> m_eventDictionary = new Dictionary<int, Delegate>();

        #region AddEventListener

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener(Enum eventType, Action handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_eventDictionary[id] = (Action)m_eventDictionary[id] + handler;
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener<T>(Enum eventType, Action<T> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_eventDictionary[id] = (Action<T>)m_eventDictionary[id] + handler;
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener<T, U>(Enum eventType, Action<T, U> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_eventDictionary[id] = (Action<T, U>)m_eventDictionary[id] + handler;
        }

        /// <summary>
        /// 添加监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <typeparam name="V">参数三类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void AddListener<T, U, V>(Enum eventType, Action<T, U, V> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_eventDictionary[id] = (Action<T, U, V>)m_eventDictionary[id] + handler;
        }

        #endregion AddEventListener

        #region RemoveEventListener

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener(Enum eventType, Action handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_eventDictionary[id] = (Action)m_eventDictionary[id] - handler;
            OnListenerRemoved(id);
        }

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener<T>(Enum eventType, Action<T> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_eventDictionary[id] = (Action<T>)m_eventDictionary[id] - handler;
            OnListenerRemoved(id);
        }

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener<T, U>(Enum eventType, Action<T, U> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_eventDictionary[id] = (Action<T, U>)m_eventDictionary[id] - handler;
            OnListenerRemoved(id);
        }

        /// <summary>
        /// 移除监听事件
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <typeparam name="V">参数三类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="handler">事件</param>
        public void RemoveListener<T, U, V>(Enum eventType, Action<T, U, V> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_eventDictionary[id] = (Action<T, U, V>)m_eventDictionary[id] - handler;
            OnListenerRemoved(id);
        }

        #endregion RemoveEventListener

        #region OnListenerAdding OnListenerRemoving

        private void OnListenerAdding(int eventType, Delegate listenerBeingAdded)
        {
            if (!m_eventDictionary.ContainsKey(eventType))
            {
                m_eventDictionary.Add(eventType, null);
            }

            Delegate d = m_eventDictionary[eventType];

            if (d != null && d.GetType() != listenerBeingAdded.GetType())
            {
                throw new Exception(string.Format("Attempting to add listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being added has type {2}", eventType, d.GetType().Name, listenerBeingAdded.GetType().Name));
            }
        }

        private void OnListenerRemoving(int eventType, Delegate listenerBeingRemoved)
        {
            if (m_eventDictionary.ContainsKey(eventType))
            {
                Delegate d = m_eventDictionary[eventType];

                if (d == null)
                {
                    throw new Exception(string.Format("Attempting to remove listener with for event type \"{0}\" but current listener is null.", eventType));
                }
                else if (d.GetType() != listenerBeingRemoved.GetType())
                {
                    throw new Exception(string.Format("Attempting to remove listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being removed has type {2}", eventType, d.GetType().Name, listenerBeingRemoved.GetType().Name));
                }
            }
            else
            {
                throw new Exception(string.Format("Attempting to remove listener for type \"{0}\" but Messenger doesn't know about this event type.", eventType));
            }
        }

        private void OnListenerRemoved(int eventType)
        {
            if (m_eventDictionary[eventType] == null)
            {
                m_eventDictionary.Remove(eventType);
            }
        }

        #endregion OnListenerAdding OnListenerRemoving

        #region BroadCastEventMsg

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <param name="eventType">事件类型</param>
        public void BroadCast(Enum eventType)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.TryGetValue(id, out Delegate d))
            {
                if (d is Action callback)
                {
                    callback();
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventType);
                }
            }
        }

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="arg1">参数</param>
        public void BroadCast<T>(Enum eventType, T arg1)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.TryGetValue(id, out Delegate d))
            {
                if (d is Action<T> callback)
                {
                    callback(arg1);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventType);
                }
            }
        }

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        public void BroadCast<T, U>(Enum eventType, T arg1, U arg2)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.TryGetValue(id, out Delegate d))
            {

                if (d is Action<T, U> callback)
                {
                    callback(arg1, arg2);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventType);
                }
            }
        }

        /// <summary>
        /// 事件广播
        /// </summary>
        /// <typeparam name="T">参数一类型</typeparam>
        /// <typeparam name="U">参数二类型</typeparam>
        /// <typeparam name="V">参数三类型</typeparam>
        /// <param name="eventType">事件类型</param>
        /// <param name="arg1">参数1</param>
        /// <param name="arg2">参数2</param>
        /// <param name="arg3">参数3</param>
        public void BroadCast<T, U, V>(Enum eventType, T arg1, U arg2, V arg3)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.TryGetValue(id, out Delegate d))
            {
                if (d is Action<T, U, V> callback)
                {
                    callback(arg1, arg2, arg3);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventType);
                }
            }
        }

        #endregion BroadCastEventMsg

        #region CheckEventListener

        public bool CheckListener(Enum eventType, Action handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.ContainsKey(id))
            {
                Delegate d = m_eventDictionary[id];

                if (d == null)
                {
                    return false;
                }
                else if (d.GetType() != handler.GetType())
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        //Single parameter
        public bool CheckListener<T>(Enum eventType, Action<T> handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.ContainsKey(id))
            {
                Delegate d = m_eventDictionary[id];

                if (d == null)
                {
                    return false;
                }
                else if (d.GetType() != handler.GetType())
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        //Two parameters
        public bool CheckListener<T, U>(Enum eventType, Action<T, U> handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.ContainsKey(id))
            {
                Delegate d = m_eventDictionary[id];

                if (d == null)
                {
                    return false;
                }
                else if (d.GetType() != handler.GetType())
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        //Three parameters
        public bool CheckListener<T, U, V>(Enum eventType, Action<T, U, V> handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_eventDictionary.ContainsKey(id))
            {
                Delegate d = m_eventDictionary[id];

                if (d == null)
                {
                    return false;
                }
                else if (d.GetType() != handler.GetType())
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private FrameworkException CreateBroadcastSignatureException(Enum eventType)
        {
            return new FrameworkException(string.Format("Broadcasting message \"{0}\" but listeners have a different signature than the broadcaster.", eventType));
        }

        #endregion CheckEventListener
    }
}