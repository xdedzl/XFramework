using System;

namespace XFramework.Animation
{
    public class XAnimationException : Exception
    {
        public XAnimationException()
        {
        }

        public XAnimationException(string message)
            : base(message)
        {
        }

        public XAnimationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
