namespace XFramework.Pool
{
    /// <summary>
    /// 对象工厂
    /// </summary>
    public interface IObjectFactory<T>
    {
        T Create(object data);
    }
}