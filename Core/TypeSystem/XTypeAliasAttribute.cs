using System;

namespace XFramework.TypeSystem
{
    /// <summary>
    /// 为普通 C# 类声明一个用于反射构造的全局唯一别名。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class XTypeAliasAttribute : Attribute
    {
        public XTypeAliasAttribute(string alias)
        {
            Alias = alias;
        }

        /// <summary>
        /// 类型别名，由 <see cref="XType"/> 按需扫描并建立索引。
        /// </summary>
        public string Alias { get; }
    }
}
