using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 此类是为了解决传参类型为Color时不能设置默认值的问题
    /// </summary>
    public class ColorInt32
    {
        public static int cyan { get { return 16777215; } }
        public static int clear { get { return 0; } }
        public static int grey { get { return 2139062271; } }
        public static int gray { get { return 2139062271; } }
        public static int magenta { get { return -16711681; } }
        public static int red { get { return -16776961; } }
        public static int yellow { get { return -1374977; } }
        public static int black { get { return 255; } }
        public static int white { get { return -1; } }
        public static int green { get { return 16711935; } }
        public static int blue { get { return 65535; } }

        public static Color32 Int2Color(int i)
        {
            byte[] result = new byte[4];
            result[0] = (byte)((i >> 24));
            result[1] = (byte)((i >> 16));
            result[2] = (byte)((i >> 8));
            result[3] = (byte)(i);
            return new Color32(result[0], result[1], result[2], result[3]);
        }

        public static int Color2Int(Color32 color)
        {
            byte[] result = new byte[4];
            result[0] = color.r;
            result[1] = color.g;
            result[2] = color.b;
            result[3] = color.a;
            return (int)(result[0] << 24 | result[1] << 16 | result[2] << 8 | result[3]);
        }
    }
}