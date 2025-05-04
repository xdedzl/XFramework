using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 此类是为了解决传参类型为Color时不能设置默认值的问题
    /// </summary>
    public static class ColorInt32
    {
        public const int cyan = 16777215;
        public const int clear = 0;
        public const int grey = 2139062271;
        public const int gray = 2139062271;
        public const int magenta = -16711681;
        public const int red = -16776961;
        public const int yellow = -1374977;
        public const int black = 255;
        public const int white = -1;
        public const int green = 16711935;
        public const int blue = 65535;

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

        public static int Int32(this Color32 color)
        {
            return Color2Int(color);
        }

        public static int Int32(this Color color)
        {
            return Color2Int(color);
        }

        public static Color32 Color(this int intColor)
        {
            return Int2Color(intColor);
        }
    }
}