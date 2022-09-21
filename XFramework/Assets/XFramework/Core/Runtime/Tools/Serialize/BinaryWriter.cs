using System;
using System.Collections.Generic;
using System.Text;

namespace XFramework
{
    public class BinaryWriter
    {
        /// <summary>
        /// 用于编码时
        /// </summary>
        private List<byte> bufferList;

        /// <summary>
        /// 构造成编码器
        /// </summary>
        public BinaryWriter()
        {
            bufferList = new List<byte>();
        }

        /// <summary>
        /// 编码器
        /// </summary>
        /// <returns></returns>
        public byte[] Encode()
        {
            return bufferList.ToArray();
        }

        /// <summary>
        /// 协议内容 提取每一个字节并组成字符串 用于查看消息
        /// </summary>
        /// <returns></returns>
        public string GetDesc()
        {
            string str = "";
            if (bufferList == null) return str;
            for (int i = 0; i < bufferList.Count; i++)
            {
                int b = (int)bufferList[i];
                str += b.ToString() + " ";
            }
            return str;
        }

        /// <summary>
        /// 将字符转转为字节数组加入字节流
        /// </summary>
        /// <param name="str">要添加的字符串</param>
        public void AddString(string str)
        {
            byte[] strBytes = Encoding.UTF8.GetBytes(str);
            Int32 len = strBytes.Length;
            byte[] lenBytes = BitConverter.GetBytes(len);
            bufferList.AddRange(lenBytes);
            bufferList.AddRange(strBytes);
        }

        /// <summary>
        /// 将Int32转化成字节数组加入字节流
        /// </summary>
        /// <param name="num">要转化的Int32</param>
        public void AddInt32(int num)
        {
            bufferList.Add((byte)num);
            bufferList.Add((byte)(num >> 8));
            bufferList.Add((byte)(num >> 16));
            bufferList.Add((byte)(num >> 24));
        }

        /// <summary>
        /// 将float转化成字节数组加入字节流
        /// </summary>
        /// <param name="num">要转化的float</param>
        public unsafe void AddFloat(float num)
        {
            uint temp = *(uint*)&num;
            bufferList.Add((byte)temp);
            bufferList.Add((byte)(temp >> 8));
            bufferList.Add((byte)(temp >> 16));
            bufferList.Add((byte)(temp >> 24));
        }

        /// <summary>
        /// 将double转化成字节数组加入字节流
        /// </summary>
        /// <param name="num">要转化的double</param>
        public unsafe void AddDouble(double num)
        {
            ulong temp = *(ulong*)&num;
            bufferList.Add((byte)temp);
            bufferList.Add((byte)(temp >> 8));
            bufferList.Add((byte)(temp >> 16));
            bufferList.Add((byte)(temp >> 24));
            bufferList.Add((byte)(temp >> 32));
            bufferList.Add((byte)(temp >> 40));
            bufferList.Add((byte)(temp >> 48));
            bufferList.Add((byte)(temp >> 56));
        }

        public void AddBoolen(bool value)
        {
            bufferList.Add((byte)(value ? 1 : 0));
        }

        #region 数组

        public void AddFloatArray1(float[] array)
        {
            AddInt32(array.GetLength(0));

            for (int i = 0, length_0 = array.GetLength(0); i < length_0; i++)
            {
                AddFloat(array[i]);
            }
        }

        public void AddFloatArray2(float[,] array)
        {
            AddInt32(array.GetLength(0));
            AddInt32(array.GetLength(1));

            for (int i = 0, length_0 = array.GetLength(0); i < length_0; i++)
            {
                for (int j = 0, length_1 = array.GetLength(1); j < length_1; j++)
                {
                    AddFloat(array[i, j]);
                }
            }
        }

        public void AddFloatArray3(float[,,] array)
        {
            AddInt32(array.GetLength(0));
            AddInt32(array.GetLength(1));
            AddInt32(array.GetLength(2));

            for (int i = 0, length_0 = array.GetLength(0); i < length_0; i++)
            {
                for (int j = 0, length_1 = array.GetLength(1); j < length_1; j++)
                {
                    for (int k = 0, length_2 = array.GetLength(2); k < length_2; k++)
                    {
                        AddFloat(array[i, j, k]);
                    }
                }
            }
        }


        public void AddIntArray1(int[] array)
        {
            AddInt32(array.GetLength(0));

            for (int i = 0, length_0 = array.GetLength(0); i < length_0; i++)
            {
                AddInt32(array[i]);
            }
        }

        public void AddIntArray2(int[,] array)
        {
            AddInt32(array.GetLength(0));
            AddInt32(array.GetLength(1));

            for (int i = 0, length_0 = array.GetLength(0); i < length_0; i++)
            {
                for (int j = 0, length_1 = array.GetLength(1); j < length_1; j++)
                {
                    AddInt32(array[i, j]);
                }
            }
        }

        public void AddIntArray3(int[,,] array)
        {
            AddInt32(array.GetLength(0));
            AddInt32(array.GetLength(1));
            AddInt32(array.GetLength(2));

            for (int i = 0, length_0 = array.GetLength(0); i < length_0; i++)
            {
                for (int j = 0, length_1 = array.GetLength(1); j < length_1; j++)
                {
                    for (int k = 0, length_2 = array.GetLength(2); k < length_2; k++)
                    {
                        AddInt32(array[i, j, k]);
                    }
                }
            }
        }


        public void AddVectorArray1(float[] array)
        {
            AddInt32(array.GetLength(0));

            for (int i = 0, length_0 = array.GetLength(0); i < length_0; i++)
            {
                AddFloat(array[i]);
            }
        }

        #endregion

        /// <summary>
        /// 清理缓存
        /// </summary>
        public void Clear()
        {
            bufferList.Clear();
        }
    }
}
