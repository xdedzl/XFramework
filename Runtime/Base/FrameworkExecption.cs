using System;

namespace XFramework
{
    /// <summary>
    /// 框架异常
    /// </summary>
    public class FrameworkException : Exception
    {
        public FrameworkException() : base() { }

        public FrameworkException(string message) : base(message) { }
    }
}