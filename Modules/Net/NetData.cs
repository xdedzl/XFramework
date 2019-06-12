using XFramework.Net;

namespace Net.Common
{
    public struct NetData
    {
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