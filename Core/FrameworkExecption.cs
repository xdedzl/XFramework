using System;

namespace XFramework
{
    /// <summary>
    /// 框架异常
    /// </summary>
    public class XFrameworkException : Exception
    {
        public XFrameworkException() : base($"[XFramework]") { }

        public XFrameworkException(string message) : base($"[XFramework] {message}") { }

        public XFrameworkException(string message, Exception innerException)
            : base($"[XFramework] {message}", innerException) { }
    }
}