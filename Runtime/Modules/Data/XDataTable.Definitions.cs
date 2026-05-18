using System;

namespace XFramework.Data
{
    public class DataResourcePath : Attribute
    {
        public string path;

        public DataResourcePath(string path)
        {
            this.path = path;
        }
    }

    public class TargetDataType : Attribute
    {
        public Type targetType;

        public TargetDataType(Type targetType)
        {
            this.targetType = targetType;
        }
    }

    public interface IData
    {
    }

    public interface IDataHasAlias : IData
    {
        public string Alias { get; }
    }

    public interface IDataHasKey<out TKey> : IData
    {
        public TKey PrimaryKey { get; }
    }

    public interface IDataHasAlias<out TKey> : IDataHasKey<TKey>, IDataHasAlias
    {
    }
}
