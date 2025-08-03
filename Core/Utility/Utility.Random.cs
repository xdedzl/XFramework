using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using URandom = UnityEngine.Random;
using CRandom = System.Random;

namespace XFramework
{
    public static partial class Utility
    {

        public static class Random
        {
            private static readonly CRandom _random = new();
            public static Vector3 RandomVector3(float x, float y, float z)
            {
                return new Vector3(URandom.Range(-x, x), URandom.Range(-y, y), URandom.Range(-z, z));
            }

            public static Quaternion RandomQuaternion()
            {
                return Quaternion.Euler(URandom.Range(-90, 90), URandom.Range(-90, 90), URandom.Range(-90, 90));
            }

            public static Color RandomColor()
            {
                return new Color(URandom.Range(0, 1), URandom.Range(0, 1), URandom.Range(0, 1), 1);
            }

            public static T RandomValue<T>(IList<T> values, IList<float> weights)
            {
                if (values == null || weights == null || values.Count != weights.Count)
                    throw new ArgumentException("参数不匹配");

                float totalWeight = weights.Sum();
                float randomValue = (float)_random.NextDouble() * totalWeight;

                float cumulativeWeight = 0f;
                for (int i = 0; i < weights.Count; i++)
                {
                    cumulativeWeight += weights[i];
                    if (randomValue <= cumulativeWeight)
                        return values[i];
                }

                return default; // 理论上不会执行到这里
            }

            public static T RandomValue<T>(IList<T> values)
            {
                var index = URandom.Range(0, values.Count);
                return values[index];
            }


            /// <summary>
            /// 从列表中取count个不重复的数据（​​Fisher-Yates洗牌算法）
            /// </summary>
            public static List<T> GetRandomElements<T>(IList<T> list, int count)
            {
                if (count > list.Count)
                {
                    throw new XFrameworkException("count > list.count");
                }
                var tempList = new List<T>(list);
                // 洗牌算法
                for (int i = tempList.Count - 1; i > 0; i--)
                {
                    int j = _random.Next(i + 1);
                    T temp = tempList[i];
                    tempList[i] = tempList[j];
                    tempList[j] = temp;
                }
                // 取前count个元素
                return tempList.GetRange(0, Math.Min(count, list.Count));
            }
        }
    }
}