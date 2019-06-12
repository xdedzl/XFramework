using System;
using System.Collections.Generic;

namespace XFramework
{
    public static partial class Extend
    {
        #region Collection

        /// <summary>
        /// 对枚举器的所以数据进行某种操作
        /// </summary>
        /// <typeparam name="T">目标对象</typeparam>
        /// <param name="value">目标</param>
        /// <param name="action">操作事件</param>
        public static void ForEach<T>(this IEnumerable<T> value, Action<T> action)
        {
            foreach (T obj in value)
            {
                action(obj);
            }
        }

        /// <summary>
        /// 转变数组类型
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="array">原数组</param>
        /// <returns></returns>
        public static T[] Convert<T>(this Array array) where T : class
        {
            T[] tArray = new T[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                tArray[i] = array.GetValue(i) as T;
            }
            return tArray;
        }

        /// <summary>
        /// 根据Key获取字典的一个值
        /// </summary>
        public static Value GetValue<Key, Value>(this Dictionary<Key, Value> dic, Key key)
        {
            Value value;
            dic.TryGetValue(key, out value);
            return value;
        }

        /// <summary>
        /// 连接两个数组的第一维返回一个新数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static T[,] Concat0<T>(this T[,] array_0, T[,] array_1)
        {
            if (array_0.GetLength(0) != array_1.GetLength(0))
            {
                throw new System.Exception("两个数组第一维不一致");
            }
            T[,] ret = new T[array_0.GetLength(0), array_0.GetLength(1) + array_1.GetLength(1)];
            for (int i = 0; i < array_0.GetLength(0); i++)
            {
                for (int j = 0; j < array_0.GetLength(1); j++)
                {
                    ret[i, j] = array_0[i, j];
                }
            }
            for (int i = 0; i < array_1.GetLength(0); i++)
            {
                for (int j = 0; j < array_1.GetLength(1); j++)
                {
                    ret[i, j + array_0.GetLength(1)] = array_1[i, j];
                }
            }
            return ret;
        }

        /// <summary>
        /// 连接两个数组的第二位返回一个新数组
        /// </summary>
        public static T[,] Concat1<T>(this T[,] array_0, T[,] array_1)
        {
            if (array_0.GetLength(1) != array_1.GetLength(1))
            {
                throw new System.Exception("两个数组第二维不一致");
            }
            T[,] ret = new T[array_0.GetLength(0) + array_1.GetLength(0), array_0.GetLength(1)];
            for (int i = 0; i < array_0.GetLength(0); i++)
            {
                for (int j = 0; j < array_0.GetLength(1); j++)
                {
                    ret[i, j] = array_0[i, j];
                }
            }
            for (int i = 0; i < array_1.GetLength(0); i++)
            {
                for (int j = 0; j < array_1.GetLength(1); j++)
                {
                    ret[i + array_0.GetLength(0), j] = array_1[i, j];
                }
            }
            return ret;
        }

        /// <summary>
        /// 获取一个二维数组的某一部分并返回
        /// </summary>
        /// <param name="array">目标数组</param>
        /// <param name="base_0">第一维的起始索引</param>
        /// <param name="base_1">第二维的起始索引</param>
        /// <param name="length_0">第一维要获取的数据长度</param>
        /// <param name="length_1">第二维要获取的数据长度</param>
        /// <returns></returns>
        public static T[,] GetPart<T>(this T[,] array, int base_0, int base_1, int length_0, int length_1)
        {
            if (base_0 + length_0 > array.GetLength(0) || base_1 + length_1 > array.GetLength(1))
            {
                throw new System.Exception("索引超出范围");
            }
            T[,] ret = new T[length_0, length_1];
            for (int i = 0; i < length_0; i++)
            {
                for (int j = 0; j < length_1; j++)
                {
                    ret[i, j] = array[i + base_0, j + base_1];
                }
            }
            return ret;
        }


        /// <summary>
        /// 获取除_exclude之外的集合
        /// </summary>
        /// <param name="_sourList"></param>
        /// <param name="_exclude"></param>
        /// <returns></returns>
        public static List<T> WithOut<T>(this List<T> _sourList, T _exclude) where T : class
        {
            List<T> outList = new List<T>();
            foreach (var item in _sourList)
            {
                if (item != _exclude)
                {
                    outList.Add(item);
                }
            }
            return outList;
        }

        /// <summary>
        /// 获取列表最后一个元素
        /// </summary>
        public static T End<T>(this List<T> list)
        {
            return list[list.Count - 1];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="value"></param>
        public static void MoveToEnd<T>(this List<T> list, T value)
        {
            list.Remove(value);
            list.Add(value);
        }

        #endregion

        #region String

        /// <summary>
        /// 首字母大写
        /// </summary>
        public static string InitialToUpper(this string str)
        {
            return str.Substring(0,1).ToUpper() + str.Substring(1);
        }

        /// <summary>
        /// 在大写字符前加空格
        /// </summary>
        /// <param name="upper">首字母是否大写</param>
        /// <returns></returns>
        public static string AddSpace(this string str,bool upper = true)
        {
            char[] chars = str.ToCharArray();
            List<int> indexs = new List<int>();

            if(upper && chars[0] >= 97 && chars[0] <= 122)
            {
                chars[0] = (char)(chars[0] - 32);
            }

            for (int i = 1,length = chars.Length; i < length; i++)
            {
                if(chars[i] >= 65 && chars[i] <= 90)
                {
                    indexs.Add(i);
                }
            }

            char[] ret = new char[chars.Length + indexs.Count];

            // i => chars, k => ret, j => indexs
            for (int i = 0,k = 0,j = 0 ,length = chars.Length; i < length; i++,k++)
            {
                if(j<indexs.Count && i == indexs[j])
                {
                    ret[k] = ' ';
                    k++;
                    j++;
                }

                ret[k] = chars[i];
            }

            return new string(ret);
        }

        #endregion
    }
}