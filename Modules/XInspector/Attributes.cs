using System;

namespace XFramework.UI
{
    /// <summary>
    /// 定义自定义Element类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class CustomerElementAttribute : Attribute
    {
        public Type type;
        public object[] args;

        public CustomerElementAttribute(Type type, params object[] args)
        {
            if (type != null && !type.IsSubclassOf(typeof(XInspectorElement)))
            {
                throw new Exception($"参数type必须为{typeof(XInspectorElement).Name}的派生类   type{type.Name}");
            }
            this.type = type;
            this.args = args;
        }
    }

    /// <summary>
    /// 定义自定义Element类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ArrayCustomerElementAttribute : CustomerElementAttribute
    {
        public ArrayCustomerElementAttribute(Type type, params object[] args) : base(type, args) { }
    }

    /// <summary>
    /// 定义数组/List元素使用的PropertyAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ArrayItemPropertyAttribute : Attribute
    {
        public Type propertyAttributeType;
        public object[] args;

        public ArrayItemPropertyAttribute(Type propertyAttributeType, params object[] args)
        {
            if (propertyAttributeType != null && !typeof(UnityEngine.PropertyAttribute).IsAssignableFrom(propertyAttributeType))
            {
                throw new Exception($"参数propertyAttributeType必须为{typeof(UnityEngine.PropertyAttribute).Name}的派生类   type{propertyAttributeType.Name}");
            }

            this.propertyAttributeType = propertyAttributeType;
            this.args = args;
        }
    }

    /// <summary>
    /// 定义变量在UI上的显示名称
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ElementPropertyAttribute : Attribute
    {
        public string propertyName;
        public ElementPropertyAttribute(string propertyName)
        {
            this.propertyName = propertyName;
        }
    }

    /// <summary>
    /// 定义变量在UI上的覆盖显示名称
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class DisplayNameAttribute : Attribute
    {
        public string displayName;

        public DisplayNameAttribute(string displayName)
        {
            this.displayName = displayName;
        }
    }

    /// <summary>
    /// 忽略该变量
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ElementIgnoreAttribute : Attribute { }

    /// <summary>
    /// 定义该Element型支持的类型
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class DefaultSportTypesAttribute : Attribute
    {
        public Type[] types;
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
        public ISupport support;
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
