// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-11-08 10:44:17
// 版本： V 1.0
// ==========================================
using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 鼠标连续点击
    /// </summary>
    public static class InputEvent
    {
        public class RepeatInfo
        {
            public int count;
            public float time;

            public RepeatInfo()
            {
                count = 0;
                time = 0;
            }
        }


        /// <summary>
        /// 是否响应单击事件
        /// </summary>
        private static bool activeSingleClick = true;
        /// <summary>
        /// 连击的最长间隔
        /// </summary>
        private static readonly float inteval = 0.2f;
        private static Dictionary<float, RepeatInfo> repeatInfoDic = new Dictionary<float, RepeatInfo>();


        /// <summary>
        /// 连击
        /// </summary>
        /// <param name="key"></param>
        /// <param name="clickCount"></param>
        /// <returns></returns>
        public static bool RepeatKey(KeyCode key, int clickCount)
        {
            float dicKey = (float)key + ((clickCount - 0.05f)) / 2;  // 强行制造一个浮点Key
            if (!repeatInfoDic.ContainsKey(dicKey))
            {
                repeatInfoDic.Add(dicKey, new RepeatInfo());
            }
            RepeatInfo info = repeatInfoDic[dicKey];

            bool ret = false;
            if (Input.GetKeyUp(key))
            {
                info.count = info.count == 0 ? 1 : info.count;
                if (Time.time - info.time <= inteval)
                {
                    info.count++;
                }

                info.time = Time.time;
            }
            if (Time.time - info.time > inteval && info.count != 0)
            {
                if (info.count == clickCount)
                {
                    ret = true;
                }
                info.count = 0;
            }
            return ret;
        }

        /// <summary>
        /// 组合按键
        /// </summary>
        /// <param name="firstKey"></param>
        /// <param name="sencondKey"></param>
        /// <returns></returns>
        public static bool CombineKey(KeyCode firstKey, KeyCode sencondKey)
        {
            if (Input.GetKey(firstKey))
            {
                activeSingleClick = false;
                if (Input.GetKeyUp(sencondKey))
                {
                    return true;
                }
            }

            if (Input.GetKeyUp(firstKey))
            {
                activeSingleClick = true;
            }
            return false;
        }

        /// <summary>
        /// 替代Input.GetKeyDown
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool GetKeyDown(KeyCode key)
        {
            return Input.GetKeyDown(key) && activeSingleClick;
        }

        /// <summary>
        /// 替代Input.GetKeyUp
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool GetKeyUp(KeyCode key)
        {
            return Input.GetKeyUp(key) && activeSingleClick;
        }
    }
}