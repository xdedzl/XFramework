using System;

namespace XFramework
{
    /// <summary>
    /// 为枚举提供自定义字符串,结合Utility.Enum.ToCustomStrs使用
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class EnumStrAttribute : Attribute
    {
        public string str;
        public EnumStrAttribute(string str)
        {
            this.str = str;
        }
    }
}