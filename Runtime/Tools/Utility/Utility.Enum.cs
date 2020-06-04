using System;
using System.Collections.Generic;

namespace XFramework
{
    public static partial class Utility
    {
        public static class Enum
        {
            /// <summary>
            /// 获取枚举的自定义字符串,需要结合EnumStrAttribute特性使用
            /// </summary>
            /// <typeparam name="T">枚举类型</typeparam>
            /// <returns>字符串集合</returns>
            public static List<string> GetDescriptions<T>() where T : System.Enum
            {
                Type attrType = typeof(DescriptionAttribute);
                Type type = typeof(T);
                var fields = type.GetFields();
                List<string> strArray = new List<string>();
                foreach (var item in fields) 
                {
                    if (item.FieldType != type)
                    {
                        continue;
                    }

                    Attribute attribute = Attribute.GetCustomAttribute(item, attrType);
                    if (attribute != null)
                    {
                        strArray.Add((attribute as DescriptionAttribute).str);
                    }
                    else
                    {
                        throw new XFrameworkException($"{type.Name} 的 {item.Name} 没有设置EnumStrAttribute特性");
                    }
                }

                return strArray;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="args"></param>
            /// <returns></returns>
            public static int CombineTag<T>(params T[] args) where T : System.Enum
            {
                int v = 0;
                foreach (var item in args)
                {
                    v |= Convert.ToInt32(item);
                }
                return v;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="tag_0"></param>
            /// <param name="tag_1"></param>
            /// <returns></returns>
            public static bool HasTag<T>(T tag_0, T tag_1) where T : System.Enum
            {
                return (Convert.ToInt32(tag_0) & Convert.ToInt32(tag_1)) != 0;
            }
        }
    }
}