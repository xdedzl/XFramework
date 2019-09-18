using UnityEngine;

namespace XFramework.Mathematics
{
    public static class MathUtility
    {
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
                throw new FrameworkException("取值范围不够");
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
            return Random.Range(min, max + 1);
        }
    }
}