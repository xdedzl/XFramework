using System;

namespace XFramework.Data
{
    public class DataResourcePath : Attribute
    {
        public string[] paths;

        public DataResourcePath(params string[] paths)
        {
            this.paths = paths ?? Array.Empty<string>();
        }

        public string[] GetPaths()
        {
            return paths ?? Array.Empty<string>();
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
