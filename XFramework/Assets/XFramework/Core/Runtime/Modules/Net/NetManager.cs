using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using Net.Common;

namespace XFramework.Net
{
    public delegate void CallBack(NetData data);
    public class NetManager : IGameModule
    {
        private Connection connection;

        public Queue<NetData> eventDatas = new Queue<NetData>();

        private Dictionary<int, CallBack> m_EventDic = new Dictionary<int, CallBack>();

        public NetManager()
        {
            
        }

        /// <summary>
        /// 开始连接
        /// </summary>
        /// <param name="host">主机名</param>
        /// <param name="port">端口号</param>
        /// <returns>连接是否成功</returns>
        public bool StartConnect(string host, int port)
        {
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(host, port);
                connection = new Connection(socket);
                connection.BeginReceive(HandleCb);
                Debug.Log("[连接成功]");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[连接失败]" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 异步连接
        /// </summary>
        /// <param name="host">主机名</param>
        /// <param name="port">端口号</param>
        /// <param name="cb">连接成功的回调</param>
        /// <returns>回调</returns>
        public IAsyncResult StartConnectAsync(string host, int port, Action cb = null)
        {
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var result = socket.BeginConnect(host, port,(a)=> 
                {
                    connection = new Connection(socket);
                    connection.BeginReceive(HandleCb);
                    cb?.Invoke();
                    Debug.Log("[连接成功]");
                },null);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("[连接失败]" + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 消息处理的回调
        /// </summary>
        /// <param name="asyncResult"></param>
        private void HandleCb(Connection conn)
        {
            while (connection.ReadData(out NetData data))
            {
                eventDatas.Enqueue(data);
            }
        }

        /// <summary>
        /// 数据发送
        /// </summary>
        /// <param name="dataType">数据类型</param>
        /// <param name="data">数据</param>
        public void Send(int dataType, byte[] data)
        {
            connection.Send(dataType, data);
        }
        public void Send(Enum dataType, byte[] data)
        {
            connection.Send(dataType, data);
        }

        #region 事件响应

        /// <summary>
        /// 消息分发
        /// </summary>
        /// <param name="data"></param>
        private void Notify(NetData data)
        {
            if (m_EventDic.ContainsKey(data.dataType))
            {
                m_EventDic[data.dataType].Invoke(data);
            }
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="callBack"></param>
        public void AddListener(Enum eventType, CallBack callBack)
        {
            int id = Convert.ToInt32(eventType);
            AddListener(id, callBack);
        }

        public void AddListener(int id, CallBack callBack)
        {
            if (!m_EventDic.ContainsKey(id))
            {
                m_EventDic.Add(id, null);
            }

            m_EventDic[id] += callBack;
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="eventType"></param>
        /// <param name="callBack"></param>
        public void RemoveListener(Enum eventType, CallBack callBack)
        {
            int id = Convert.ToInt32(eventType);
            RemoveListener(id, callBack);
        }
        public void RemoveListener(int id, CallBack callBack)
        {
            if (!m_EventDic.ContainsKey(id))
            {
                return;
            }

            m_EventDic[id] -= callBack;
        }

        #endregion

        #region 接口实现

        public int Priority => 100;

        public void Shutdown()
        {
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            for (int i = 0; i < 10; i++)
            {
                lock (eventDatas)
                {
                    if (eventDatas.Count > 0)
                    {
                        Notify(eventDatas.Dequeue());
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        #endregion
    }
}