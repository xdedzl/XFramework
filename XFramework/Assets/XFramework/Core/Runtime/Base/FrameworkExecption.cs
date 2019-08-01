using System;

namespace XFramework
{
    public class FrameworkException : Exception
    {
        public FrameworkException() : base() { }

        public FrameworkException(string message) : base(message) { }
    }
}