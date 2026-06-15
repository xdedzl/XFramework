using System;

namespace XFramework
{
    /// <summary>
    /// Groups multiple top-level members in XInspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class PrettyGroupAttribute : Attribute
    {
        public string Title { get; }

        public PrettyGroupAttribute(string title)
        {
            Title = title;
        }
    }
}
