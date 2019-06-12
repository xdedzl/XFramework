using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Net.Common;

namespace Net.Core
{
    public delegate void CallBack(NetData data);

    /// <summary>
    /// 服务器
    /// </summary>
    public class Server
    {
        /// <summary>
        /// 服务端套接字
        /// </summary>
        private Socket m_Sccket;
        /// <summary>
        /// 最大连接数量
        /// </summary>
        private int m_MaxConn;
        /// <summary>
        /// 和当前服务器连接的客户端
        /// </summary>
        private List<Connection> m_Connects;

        private Dictionary<int, CallBack> m_EventDic;

        public Server(int maxConn)
        {
            m_MaxConn = maxConn;
            m_Connects = new List<Connection>();
            m_EventDic = new Dictionary<int, CallBack>();
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public bool Start(string host, int port)
        {
            try
            {
                m_Sccket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ipAdr = IPAddress.Parse(host);
                IPEndPoint ipEp = new IPEndPoint(ipAdr, port);
                m_Sccket.Bind(ipEp);
                m_Sccket.Listen(m_MaxConn);
                m_Sccket.BeginAccept(AcceptCb, null);
                Console.WriteLine("[服务器]启动成功");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("[服务器]启动失败 ： " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 客户端连接回调
        /// </summary>
        /// <param name="ar"></param>
        private void AcceptCb(IAsyncResult ar)
        {
            try
            {
                Socket socket = m_Sccket.EndAccept(ar);
                if (m_Connects.Count >= m_MaxConn)
                {
                    socket.Close();
                    Console.WriteLine("[警告]连接已满");
                }
                else
                {
                    Connection conn = new Connection(socket);
                    m_Connects.Add(conn);
                    string adr = conn.GetAdress();
                    Console.WriteLine("客户端连接[" + adr + "]conn池ID：" + m_Connects.Count);
                    conn.BeginReceive(HandleConnData, conn);
                }
                m_Sccket.BeginAccept(AcceptCb, null);
            }
            catch (Exception e)
            {
                Console.WriteLine("AcceptCb失败：" + e.Message);
            }
        }

        /// <summary>
        /// 处理客户端传递的数据
        /// </summary>
        /// <param name="conn"></param>
        private void HandleConnData(Connection conn)
        {
            while(conn.ReadData(out NetData data))
            {
                Notify(conn, data);
            }
        }

        /// <summary>
        /// 消息分发
        /// </summary>
        /// <param name="data"></param>
        private void Notify(Connection connect, NetData data)
        {
            if (m_EventDic.ContainsKey(data.dataType))
            {
                m_EventDic[data.dataType].Invoke(data);
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            foreach (var conn in m_Connects)
            {
                lock (conn)
                {
                    conn.Close();
                }
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

        public void Broadcast(int dataType, byte[] data)
        {
            foreach (var item in m_Connects)
            {
                item.Send(dataType, data);
            }
        }
    }
}