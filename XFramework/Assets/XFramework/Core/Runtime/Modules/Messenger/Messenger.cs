using System;
using System.Collections.Generic;

namespace XFramework
{
    //=====================================================================================/
    /// <summary>
    /// 消息类 全局类消息
    /// </summary>
    //=====================================================================================.
    public class MessengerManager : IGameModule
    {
        public delegate void Callback();

        public delegate void Callback<T>(T arg1);

        public delegate void Callback<T, U>(T arg1, U arg2);

        public delegate void Callback<T, U, V>(T arg1, U arg2, V arg3);

        public Dictionary<int, Delegate> m_EventDic = new Dictionary<int, Delegate>();


        #region AddEventListener

        public void AddEventListener(Enum eventType, Callback handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_EventDic[id] = (Callback)m_EventDic[id] + handler;
        }

        //一个参数 parameter
        public void AddEventListener<T>(Enum eventType, Callback<T> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_EventDic[id] = (Callback<T>)m_EventDic[id] + handler;
        }

        //两个参数 parameter
        public void AddEventListener<T, U>(Enum eventType, Callback<T, U> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_EventDic[id] = (Callback<T, U>)m_EventDic[id] + handler;
        }

        //三个参数 parameter
        public void AddEventListener<T, U, V>(Enum eventType, Callback<T, U, V> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerAdding(id, handler);
            m_EventDic[id] = (Callback<T, U, V>)m_EventDic[id] + handler;
        }

        #endregion AddEventListener

        #region RemoveEventListener

        public void RemoveEventListener(Enum eventType, Callback handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_EventDic[id] = (Callback)m_EventDic[id] - handler;
            OnListenerRemoved(id);
        }

        public void RemoveEventListener<T>(Enum eventType, Callback<T> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_EventDic[id] = (Callback<T>)m_EventDic[id] - handler;
            OnListenerRemoved(id);
        }

        public void RemoveEventListener<T, U>(Enum eventType, Callback<T, U> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_EventDic[id] = (Callback<T, U>)m_EventDic[id] - handler;
            OnListenerRemoved(id);
        }

        public void RemoveEventListener<T, U, V>(Enum eventType, Callback<T, U, V> handler)
        {
            int id = Convert.ToInt32(eventType);
            OnListenerRemoving(id, handler);
            m_EventDic[id] = (Callback<T, U, V>)m_EventDic[id] - handler;
            OnListenerRemoved(id);
        }

        #endregion RemoveEventListener

        #region BroadCastEventMsg

        public void BroadCastEventMsg(Enum eventType)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.TryGetValue(id, out Delegate d))
            {
                if (d is Callback callback)
                {
                    callback();
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventType);
                }
            }
        }

        public void BroadCastEventMsg<T>(Enum eventType, T arg1)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.TryGetValue(id, out Delegate d))
            {
                if (d is Callback<T> callback)
                {
                    callback(arg1);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventType);
                }
            }
        }

        public void BroadCastEventMsg<T, U>(Enum eventType, T arg1, U arg2)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.TryGetValue(id, out Delegate d))
            {

                if (d is Callback<T, U> callback)
                {
                    callback(arg1, arg2);
                }
                else
                {
                    throw CreateBroadcastSignatureException(eventType);
                }
            }
        }

        public void BroadCastEventMsg<T, U, V>(Enum eventType, T arg1, U arg2, V arg3)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.TryGetValue(id, out Delegate d))
            {
                Callback<T, U, V> callback = d as Callback<T, U, V>;

                if (callback != null)
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

        #region OnListenerAdding OnListenerRemoving

        private void OnListenerAdding(int eventType, Delegate listenerBeingAdded)
        {
            if (!m_EventDic.ContainsKey(eventType))
            {
                m_EventDic.Add(eventType, null);
            }

            Delegate d = m_EventDic[eventType];

            if (d != null && d.GetType() != listenerBeingAdded.GetType())
            {
                throw new Exception(string.Format("Attempting to add listener with inconsistent signature for event type {0}. Current listeners have type {1} and listener being added has type {2}", eventType, d.GetType().Name, listenerBeingAdded.GetType().Name));
            }
        }

        private void OnListenerRemoving(int eventType, Delegate listenerBeingRemoved)
        {
            if (m_EventDic.ContainsKey(eventType))
            {
                Delegate d = m_EventDic[eventType];

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
            if (m_EventDic[eventType] == null)
            {
                m_EventDic.Remove(eventType);
            }
        }

        #endregion OnListenerAdding OnListenerRemoving

        #region CheckEventListener

        public bool CheckEventListener(Enum eventType, Callback handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.ContainsKey(id))
            {
                Delegate d = m_EventDic[id];

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
        public bool CheckEventListener<T>(Enum eventType, Callback<T> handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.ContainsKey(id))
            {
                Delegate d = m_EventDic[id];

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
        public bool CheckEventListener<T, U>(Enum eventType, Callback<T, U> handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.ContainsKey(id))
            {
                Delegate d = m_EventDic[id];

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
        public bool CheckEventListener<T, U, V>(Enum eventType, Callback<T, U, V> handler)
        {
            int id = Convert.ToInt32(eventType);
            if (m_EventDic.ContainsKey(id))
            {
                Delegate d = m_EventDic[id];

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

        public Exception CreateBroadcastSignatureException(Enum eventType)
        {
            return new System.Exception(string.Format("Broadcasting message \"{0}\" but listeners have a different signature than the broadcaster.", eventType));
        }

        #endregion CheckEventListener

        #region 接口实现

        public int Priority { get { return 2000; } }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {

        }

        public void Shutdown()
        {
            m_EventDic.Clear();
        }

        #endregion
    }
}