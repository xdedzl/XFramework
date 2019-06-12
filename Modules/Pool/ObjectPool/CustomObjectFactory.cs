using System;

namespace XFramework.Pool
{
    public class CustomObjectFactory<T> : IObjectFactory<T>
    {
        protected Func<T> mFactoryMethod;

        public CustomObjectFactory(Func<T> factoryMethod)
        {
            mFactoryMethod = factoryMethod;
        }

        public T Create()
        {
            return mFactoryMethod();
        }
    }
}