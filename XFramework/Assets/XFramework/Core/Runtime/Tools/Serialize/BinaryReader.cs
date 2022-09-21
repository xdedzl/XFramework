// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-11-13 08:38:23
// 版本： V 1.0
// ==========================================
using System;
using System.Text;

namespace XFramework
{
    /// <summary>
    /// 提供了一种基于字节流的协议
    /// </summary>
    public class BinaryReader
    {
        /// <summary>
        /// 传输的字节流
        /// </summary>
        private readonly byte[] buffer;
        private int index;

        /// <summary>
        /// 构造成解码器
        /// </summary>
        /// <param name="_bytes">待解析的数据</param>
        public BinaryReader(byte[] _bytes)
        {
            buffer = _bytes;
        }

        /// <summary>
        /// 协议内容 提取每一个字节并组成字符串 用于查看消息
        /// </summary>
        /// <returns></returns>
        public string GetDesc()
        {
            string str = "";
            if (buffer == null) return str;
            for (int i = 0; i < buffer.Length; i++)
            {
                int b = (int)buffer[i];
                str += b.ToString() + " ";
            }
            return str;
        }

        /// <summary>
        /// 将字节数组转化为字符串
        /// </summary>
        /// <param name="index">索引起点</param>
        /// <param name="end">为下一个转换提供索引起点</param>
        /// <returns></returns>
        public string GetString()
        {
            if (buffer == null)
                return "";
            if (buffer.Length < index + sizeof(int))
                return "";
            int strLen = BitConverter.ToInt32(buffer, index);
            if (buffer.Length < index + sizeof(int) + strLen)
                return "";
            string str = Encoding.UTF8.GetString(buffer, index + sizeof(int), strLen);
            index = index + sizeof(int) + strLen;
            return str;
        }

        /// <summary>
        /// 将字节数组转化成Int32
        /// </summary>
        public int GetInt32()
        {
            if (buffer == null)
                return 0;
            if (buffer.Length < index + 4)
                return 0;

            return (int)(buffer[index++] | buffer[index++] << 8 | buffer[index++] << 16 | buffer[index++] << 24);
        }

        /// <summary>
        /// 将字节数组转化成float
        /// </summary>
        public unsafe float GetFloat()
        {
            if (buffer == null)
                return -1;
            if (buffer.Length < index + sizeof(float))
                return -1;
            uint temp = (uint)(buffer[index++] | buffer[index++] << 8 | buffer[index++] << 16 | buffer[index++] << 24);
            return *((float*)&temp);
        }

        /// <summary>
        /// 将字节数组转化成double
        /// </summary>
        public unsafe double GetDouble()
        {
            if (buffer == null)
                return -1;
            if (buffer.Length < index + sizeof(float))
                return -1;

            //UInt64 temp = (UInt64)(buffer[index++] | buffer[index++] << 8 | buffer[index++] << 16 | buffer[index++] << 24 | buffer[index++] << 32 | buffer[index++] << 40 | buffer[index++] << 48 | buffer[index++] << 56);

            uint lo = (uint)(buffer[index++] | buffer[index++] << 8 | buffer[index++] << 16 | buffer[index++] << 24);
            uint hi = (uint)(buffer[index++] | buffer[index++] << 8 | buffer[index++] << 16 | buffer[index++] << 24);
            ulong temp = ((ulong)hi) << 32 | lo;

            return *((double*)&temp);
        }

        public bool GetBoolen()
        {
            return (buffer[index++] == 1);
        }

        #region 数组
        public float[] GetFloatArray1()
        {
            int length_0 = GetInt32();

            float[] array = new float[length_0];
            for (int i = 0; i < length_0; i++)
            {
                array[i] = GetFloat();
            }

            return array;
        }

        public float[,] GetFloatArray2()
        {
            int length_0 = GetInt32();
            int length_1 = GetInt32();

            float[,] array = new float[length_0, length_1];
            for (int i = 0; i < length_0; i++)
            {
                for (int j = 0; j < length_1; j++)
                {
                    array[i, j] = GetFloat();
                }
            }

            return array;
        }

        public float[,,] GetFloatArray3()
        {
            int length_0 = GetInt32();
            int length_1 = GetInt32();
            int length_2 = GetInt32();

            float[,,] array = new float[length_0, length_1, length_2];
            for (int i = 0; i < length_0; i++)
            {
                for (int j = 0; j < length_1; j++)
                {
                    for (int k = 0; k < length_2; k++)
                    {
                        array[i, j, k] = GetFloat();
                    }
                }
            }

            return array;
        }

        public int[] GetIntArray1()
        {
            int length_0 = GetInt32();

            int[] array = new int[length_0];
            for (int i = 0; i < length_0; i++)
            {
                array[i] = GetInt32();
            }

            return array;
        }

        public int[,] GetIntArray2()
        {
            int length_0 = GetInt32();
            int length_1 = GetInt32();

            int[,] array = new int[length_0, length_1];
            for (int i = 0; i < length_0; i++)
            {
                for (int j = 0; j < length_1; j++)
                {
                    array[i, j] = GetInt32();
                }
            }

            return array;
        }

        public int[,,] GetIntArray3()
        {
            int length_0 = GetInt32();
            int length_1 = GetInt32();
            int length_2 = GetInt32();

            int[,,] array = new int[length_0, length_1, length_2];
            for (int i = 0; i < length_0; i++)
            {
                for (int j = 0; j < length_1; j++)
                {
                    for (int k = 0; k < length_2; k++)
                    {
                        array[i, j, k] = GetInt32();
                    }
                }
            }

            return array;
        }
        #endregion
    }
}
