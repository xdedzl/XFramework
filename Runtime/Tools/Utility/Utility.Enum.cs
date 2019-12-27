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
                        throw new FrameworkException($"{type.Name} 的 {item.Name} 没有设置EnumStrAttribute特性");
                    }
                }

                return strArray;
            }
        }
    }

}