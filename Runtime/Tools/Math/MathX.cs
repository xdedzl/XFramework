using System;
using UnityEngine;

namespace XFramework.Mathematics
{
    public class MathX
    {
        #region 公式类计算

        private const double epsilon = 1e-7;
        /// <summary>
        /// 判断a是否小于b
        /// </summary>
        public static bool Less(float a, float b)
        {
            return (b - a) > epsilon;
        }
        /// <summary>
        /// 判断a是否大于b
        /// </summary>
        public static bool Great(float a, float b)
        {
            return (a - b) > epsilon;
        }

        /// <summary>
        /// 计算两个数字是否接近相等,阈值是dvalue
        /// </summary>
        public static bool IsApproximately(double a, double b, double dvalue = 0)
        {
            return Math.Abs(a - b) <= dvalue;
        }

        /// <summary>
        /// 判断a是否等于b
        /// </summary>
        public static bool FloatEqual(float a, float b)
        {
            return Mathf.Abs(a - b) < epsilon;
        }

        /// <summary>
        /// 判断a是否等于b
        /// </summary>
        public static bool Vector3Equal(Vector3 a, Vector3 b)
        {
            return FloatEqual(a.x, b.x) && FloatEqual(a.y, b.y) && FloatEqual(a.z, b.z);
        }

        /// <summary>
        /// 获取平方根 一元二次方程求根公式 x = (-b+(b^2-4ac)^1/2)/2a
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static float GetSqrtOfMath(float a, float b, float d)
        {
            float a1 = (-b + Mathf.Sqrt(d)) / (2 * a);
            float a2 = (-b - Mathf.Sqrt(d)) / (2 * a);

            return a1 > a2 ? a1 : a2;
        }

        public float GetDelta(float a, float b, float c)
        {
            return b * b - 4 * a * c;
        }

        #endregion

        #region 随机数

        /// <summary>
        /// 获取不重复随机数
        /// </summary>
        /// <param name="count">数量</param>
        /// <param name="max">最大值</param>
        /// <param name="min">最小值，默认0</param>
        /// <returns></returns>
        public static int[] GetNoRepeatRandom(int count, int max, int min = 0)
        {
            if (max - min < count - 1)
            {
                throw new XFrameworkException("取值范围不够");
            }

            int[] a = new int[max - min + 1];
            int[] res = new int[count];
            for (int i = 0; i < a.Length; i++)
                a[i] = min++;

            for (int i = 0; i < count; i++)
            {
                int ran = GetRandom(i, a.Length - 1);
                int t = a[ran];
                a[ran] = a[i];
                a[i] = t;
                res[i] = t;
            }
            return res;
        }

        /// <summary>
        /// 获取一个随机数[min,max]
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns>随机数</returns>
        public static int GetRandom(int min, int max)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        #endregion
    }
}