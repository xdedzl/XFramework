using System;
using UnityEngine;

namespace XFramework
{
    public partial class UUtility
    {
        /// <summary>
        /// UI相关工具
        /// </summary>
        public class Vector
        {
            public static Vector2 String2Vector2(string value)
            {
                string[] temp = value.Substring(1, value.Length - 2).Split(',');
                float x = System.Convert.ToSingle(temp[0]);
                float y = System.Convert.ToSingle(temp[1]);
                return new Vector2(x, y);
            }

            public static Vector3 String2Vector3(string value)
            {
                string[] temp = value.Substring(1, value.Length - 2).Split(',');
                float x = System.Convert.ToSingle(temp[0].Trim());
                float y = System.Convert.ToSingle(temp[1].Trim());
                float z = System.Convert.ToSingle(temp[2].Trim());
                return new Vector3(x, y, z);
            }
        }
    }
}