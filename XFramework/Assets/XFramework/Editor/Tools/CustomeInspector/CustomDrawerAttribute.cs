using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class CustomDrawerAttribute : Attribute
{
    internal Type type;

    internal bool useForChildren;

    public CustomDrawerAttribute(Type type)
    {
        this.type = type;
    }

    public CustomDrawerAttribute(Type type, bool useForChildren)
    {
        this.type = type;
        this.useForChildren = useForChildren;
    }
}
