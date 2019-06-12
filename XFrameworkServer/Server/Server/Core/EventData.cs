using Net.Core;

namespace Net.Common
{
    public struct NetData
    {
        /// <summary>
        /// 当前接收消息的连接
        /// </summary>
        public Connection conn;
        /// <summary>
        /// 数据类型
        /// </summary>
        public int dataType;
        /// <summary>
        /// 数据
        /// </summary>
        public byte[] data;
    }
}
