using System;
using System.Net.Sockets;
using System.Linq;
using Net.Common;

namespace XFramework.Net
{
    public delegate void ReceiveCallbcak(Connection conn);

    /// <summary>
    /// 连接信息
    /// </summary>
    public class Connection
    {
        public const int BUFFER_SIZE = 1024;

        /// <summary>
        /// 缓冲区
        /// </summary>
        private byte[] m_ReadBuff;
        /// <summary>
        /// 有效缓冲大小
        /// </summary>
        private int m_BufferCount;
        /// <summary>
        /// 套接字
        /// </summary>
        private Socket m_Socket;
        /// <summary>
        /// 回调事件
        /// </summary>
        private ReceiveCallbcak m_Callback;
        /// <summary>
        /// 缓冲数组剩余空间
        /// </summary>
        private int m_BufferRemain { get { return BUFFER_SIZE - m_BufferCount; } }

        public Connection(Socket socket)
        {
            m_Socket = socket;
            m_ReadBuff = new byte[BUFFER_SIZE];
            m_BufferCount = 0;
        }

        /// <summary>
        /// 获取地址
        /// </summary>
        /// <returns></returns>
        public string GetAdress()
        {
            return m_Socket.RemoteEndPoint.ToString();
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            m_Socket.Close();
        }

        #region 数据接收

        /// <summary>
        /// 开启数据传输回调
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        public bool BeginReceive(ReceiveCallbcak callback, object state = null)
        {
            m_Callback = callback;
            try
            {
                m_Socket.BeginReceive(m_ReadBuff, m_BufferCount, m_BufferRemain, SocketFlags.None, ReceiveCb, state);
                return true;
            }
            catch (Exception e)
            {
                Utility.Log("[数据接收错误]：" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 数据传输回调
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveCb(IAsyncResult ar)
        {
            try
            {
                int count = m_Socket.EndReceive(ar);     // 收到的字节数
                if (count <= 0)
                {
                    Console.WriteLine("收到[" + GetAdress() + "]断开连接  收到的数据小于0");
                    Close();
                    return;
                }
                m_BufferCount += count;
                m_Callback(this);

                //继续接收
                m_Socket.BeginReceive(m_ReadBuff, m_BufferCount, m_BufferRemain, SocketFlags.None, ReceiveCb, ar.AsyncState);
            }
            catch (Exception e)
            {
                Console.WriteLine("ReceiveCb失败:" + e.Message);
                Close();
            }
        }

        /// <summary>
        /// 读取一条数据
        /// </summary>
        /// <returns></returns>
        public bool ReadData(out NetData data)
        {
            // 校验消息头
            if (m_BufferCount < 8)
            {
                data = default;
                return false;
            }

            int length = BitConverter.ToInt32(m_ReadBuff, 0);

            // 校验消息是否完整
            if (m_BufferCount - 4 < length)
            {
                data = default;
                return false;
            }

            // 读取数据

            int type = BitConverter.ToInt32(m_ReadBuff, 4);
            byte[] dataBytes = new byte[length - 4];
            Array.Copy(m_ReadBuff, 8, dataBytes, 0, length - 4);

            data = new NetData
            {
                conn = this,
                dataType = type,
                data = dataBytes,
            };

            Array.Copy(m_ReadBuff, sizeof(Int32) + length, m_ReadBuff, 0, m_BufferRemain);
            m_BufferCount -= length + 4;

            return true;
        }

        #endregion

        #region 数据发送

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data"></param>
        public void Send(int dataType, byte[] data)
        {
            m_Socket.Send(PackageData(BitConverter.GetBytes(dataType).Concat(data).ToArray()));
        }
        public void Send(Enum dataType, byte[] data)
        {
            Send(Convert.ToInt32(dataType), data);
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">源数据</param>
        /// <param name="callback">传输完成回调</param>
        /// <param name="state">包含请求的状态信息的对象</param>
        public void SendAsync(int dataType, byte[] data, AsyncCallback callback = null, object state = null)
        {
            data = PackageData(BitConverter.GetBytes(dataType).Concat(data).ToArray());
            m_Socket.BeginSend(data, 0, data.Length, SocketFlags.None, callback, state);
        }
        public void SendAsync(Enum dataType, byte[] data, AsyncCallback callback = null, object state = null)
        {
            SendAsync(Convert.ToInt32(dataType), data, callback, state);
        }

        /// <summary>
        /// 打包数据
        /// </summary>
        /// <param name="data">源数据</param>
        /// <returns></returns>
        private byte[] PackageData(byte[] data)
        {
            byte[] length = BitConverter.GetBytes(data.Length);
            return length.Concat(data).ToArray();
        }

        #endregion
    }
}