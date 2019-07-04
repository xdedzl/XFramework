namespace XFramework.Pool
{
    /// <summary>
    /// 一个new出对象的工厂
    /// </summary>
    public class DefultObjectFactory<T> : IObjectFactory<T> where T : new()
    {
        public T Create(object obj)
        {
            return new T();
        }
    }
}