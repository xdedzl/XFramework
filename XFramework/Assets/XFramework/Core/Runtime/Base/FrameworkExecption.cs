using System;

namespace XFramework
{
    /// <summary>
    /// 框架异常
    /// </summary>
    public class XFrameworkException : Exception
    {
        public XFrameworkException() : base() { }

        public XFrameworkException(string message) : base(message) { }
    }
}