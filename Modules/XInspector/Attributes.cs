using System;

namespace XFramework.UI
{
    /// <summary>
    /// 定义变量在UI上的显示名称
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ElementPropertyAttribute : Attribute
    {
        public readonly string propertyName;
        public ElementPropertyAttribute(string propertyName)
        {
            this.propertyName = propertyName;
        }
    }

    /// <summary>
    /// 定义该Element型支持的类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DefaultSportTypesAttribute : Attribute
    {
        public readonly Type[] types;
        public DefaultSportTypesAttribute(params Type[] types)
        {
            this.types = types;
        }
    }

    /// <summary>
    /// 定义该Element型支持的类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class SupportHelperAttribute : Attribute
    {
        public readonly ISupport support;
        public SupportHelperAttribute(Type supportType)
        {
            this.support = Activator.CreateInstance(supportType) as ISupport;
        }
    }

    public interface ISupport
    {
        bool Support(Type type);
    }
}
