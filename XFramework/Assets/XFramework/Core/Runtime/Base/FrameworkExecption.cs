using System;

namespace XFramework
{
    public class FrameworkExecption : Exception
    {
        public FrameworkExecption() : base() { }

        public FrameworkExecption(string message) : base(message) { }
    }
}