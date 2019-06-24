// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-11-13 08:38:23
// 版本： V 1.0
// ==========================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 提供了一种基于字节流的协议
    /// </summary>
    public class ProtocolBytes
    {
        private byte[] buffer;     //传输的字节流
        private List<byte> bufferList;
        private int index;

        public ProtocolBytes()
        {
            index = 0;
            bufferList = new List<byte>();
        }

        public ProtocolBytes(byte[] _bytes)
        {
            index = 0;
            buffer = _bytes;
            bufferList = new List<byte>(_bytes);
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
            if (buffer == null) return str;
            for (int i = 0; i < buffer.Length; i++)
            {
                int b = (int)buffer[i];
                str += b.ToString() + " ";
            }
            return str;
        }

        #region 添加和获取字符串

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

        #endregion

        #region 添加获取整数

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

        #endregion

        #region 添加获取浮点数

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

        #endregion

        #region 添加获取布尔值

        public void AddBoolen(bool value)
        {
            bufferList.Add((byte)(value ? 1 : 0));
        }

        public bool GetBoolen()
        {
            return (buffer[index++] == 1);
        }

        #endregion

        #region 添加获取Vector

        public void AddVector3(Vector3 v)
        {
            AddFloat(v.x);
            AddFloat(v.y);
            AddFloat(v.z);
        }

        public Vector3 GetVector3()
        {
            float x = GetFloat();
            float y = GetFloat();
            float z = GetFloat();
            return new Vector3(x, y, z);
        }

        public void AddVector2(Vector2 v)
        {
            AddFloat(v.x);
            AddFloat(v.y);
        }

        public Vector2 GetVector2()
        {
            float x = GetFloat();
            float y = GetFloat();
            return new Vector2(x, y);
        }

        #endregion

        #region 添加获取Color

        public void AddColor(Color c)
        {
            AddFloat(c.r);
            AddFloat(c.g);
            AddFloat(c.b);
            AddFloat(c.a);
        }

        public Color GetColor()
        {
            return new Color(GetFloat(), GetFloat(), GetFloat(), GetFloat());
        }

        public void AddColor32(Color32 c)
        {
            bufferList.Add(c.r);
            bufferList.Add(c.g);
            bufferList.Add(c.b);
            bufferList.Add(c.a);
        }

        public Color32 GetColor32()
        {
            return new Color(buffer[index++], buffer[index++], buffer[index++], buffer[index++]);
        }

        #endregion

        #region 添加获取数组

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
        /// 序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="bindingAttr"></param>
        public void Serialize(object obj, BindingFlags bindingAttr = BindingFlags.Default)
        {
            Type type = obj.GetType();
            foreach (var field in obj.GetType().GetFields())
            {
                var arg = type.GetField(field.Name).GetValue(obj);
                switch (field.FieldType.Name)
                {
                    case "Int32":
                        this.AddInt32((int)arg);
                        break;
                    case "Single":
                        this.AddFloat((float)arg);
                        break;
                    case "Double":
                        this.AddDouble((double)arg);
                        break;
                    case "Boolean":
                        this.AddBoolen((bool)arg);
                        break;
                    case "String":
                        arg = arg ?? "";
                        this.AddString((string)arg);
                        break;
                    case "Enum":
                        this.AddInt32(Convert.ToInt32(arg));
                        break;
                    case "Vector3":
                        this.AddVector3((Vector3)arg);
                        break;
                    case "Vector2":
                        this.AddVector2((Vector2)arg);
                        break;
                    case "Color":
                        this.AddColor((Color)arg);
                        break;
                    case "Color32":
                        this.AddColor32((Color32)arg);
                        break;
                    case "GameObject":
                        GameObject gameObj = (GameObject)arg;
                        if (gameObj)
                        {
                            Transform trans = gameObj.transform;
                            if (trans == null)
                            {
                                Debug.LogError("只能序列化场景中的GameObject");
                                continue;
                            }
                            string path = gameObj.name;
                            while (trans.parent != null)
                            {
                                trans = trans.parent;
                                path = trans.name + "/" + path;
                            }
                            this.AddString(path);
                        }
                        else
                        {
                            this.AddString("");
                        }
                        break;
                    case "Transform":
                        Transform transform = (Transform)arg;
                        if (transform)
                        {
                            string path = transform.name;
                            while (transform.parent != null)
                            {
                                transform = transform.parent;
                                path = transform.name + "/" + path;
                            }
                            this.AddString(path);
                        }
                        else
                        {
                            this.AddString("");
                        }
                        break;
                    default:
                        Debug.LogError("暂不支持序列化" + field.FieldType.Name);
                        break;
                }
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="bindingAttr"></param>
        public void DeSerialize(object obj, BindingFlags bindingAttr = BindingFlags.Default)
        {
            Type type = obj.GetType();

            foreach (var field in type.GetFields())
            {
                object arg = null;
                switch (field.FieldType.Name)
                {
                    case "Int32":
                        arg = this.GetInt32();
                        field.SetValue(obj, arg);
                        break;
                    case "Single":
                        arg = this.GetFloat();
                        field.SetValue(obj, arg);
                        break;
                    case "Double":
                        arg = this.GetDouble();
                        field.SetValue(obj, arg);
                        break;
                    case "Boolean":
                        arg = this.GetBoolen();
                        field.SetValue(obj, arg);
                        break;
                    case "String":
                        arg = this.GetString();
                        field.SetValue(obj, arg);
                        break;
                    case "Enum":
                        arg = (this.GetInt32());
                        field.SetValue(obj, arg);
                        break;
                    case "Vector3":
                        arg = this.GetVector3();
                        field.SetValue(obj, arg);
                        break;
                    case "Vector2":
                        arg = this.GetVector2();
                        field.SetValue(obj, arg);
                        break;
                    case "Color":
                        arg = this.GetColor();
                        field.SetValue(obj, arg);
                        break;
                    case "Color32":
                        arg = this.GetColor32();
                        field.SetValue(obj, arg);
                        break;
                    case "GameObject":
                        string objStr = this.GetString();
                        if (string.IsNullOrEmpty(objStr))
                            continue;
                        field.SetValue(obj, GameObject.Find(objStr));
                        break;
                    case "Transform":
                        string tranStr = this.GetString();
                        if (string.IsNullOrEmpty(tranStr))
                            continue;
                        field.SetValue(obj, GameObject.Find(tranStr).transform);
                        break;
                }
            }
        }

        public void Clear()
        {
            bufferList.Clear();
            index = 0;
        }
    }
}